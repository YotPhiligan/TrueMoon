using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;

namespace TrueMoon.Mithril.Units;

/// <summary>A handle that owns each process in a bounded restart sequence.</summary>
public abstract class ProcessUnitHandle : UnitHandleBase, IDisposable, IAsyncDisposable
{
    /// <summary>The current process owned by this handle.</summary>
    protected Process? Process;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _state = new();
    private readonly AsyncLocal<bool> _notifying = new();
    private CancellationTokenSource? _runCancellation;
    private Task _completion = Task.CompletedTask;
    private bool _stopping;
    private bool _disposed;
    private string? _commandPipeName;
    private int _restartCount;
    private Exception? _lastRestartError;

    /// <summary>Completion of the owned exit/restart monitor, including its terminal callback.</summary>
    public Task Completion { get { lock (_state) return _completion; } }
    /// <summary>Automatic attempts consumed since the last explicit start, including failed attempts.</summary>
    public int RestartCount => Volatile.Read(ref _restartCount);
    /// <summary>The last automatic launch/monitor failure; null for normal exit or budget exhaustion.</summary>
    public Exception? LastRestartError => Volatile.Read(ref _lastRestartError);

    /// <summary>Creates an owned process handle.</summary>
    protected ProcessUnitHandle(IUnitConfiguration configuration, IEventsSource source) : base(configuration, source) { }

    /// <inheritdoc />
    public override async Task<bool?> StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_state)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (Process is not null || !_completion.IsCompleted)
                    throw new InvalidOperationException("The unit is already started. Stop it before starting again.");
                _stopping = false;
                _restartCount = 0;
                _lastRestartError = null;
            }
            ValidateConfiguration();
            if (await LaunchAsync(cancellationToken).ConfigureAwait(false) is not true) return false;
            lock (_state)
            {
                _runCancellation?.Dispose();
                var run = _runCancellation = new CancellationTokenSource();
                if (_stopping) run.Cancel();
                _completion = Task.Run(() => MonitorAsync(run));
            }
            return true;
        }
        finally { _lifecycle.Release(); }
    }

    private void ValidateConfiguration()
    {
        if (Configuration.TerminationDelay is < 0) throw new ArgumentOutOfRangeException(nameof(Configuration.TerminationDelay));
        if (Configuration.RestartLimit is < 0) throw new ArgumentOutOfRangeException(nameof(Configuration.RestartLimit));
        if (Configuration.RestartDelay is < 0) throw new ArgumentOutOfRangeException(nameof(Configuration.RestartDelay));
        if ((Configuration.RestartPolicy ?? UnitRestartPolicy.Always) is not
            (UnitRestartPolicy.Always or UnitRestartPolicy.Once or UnitRestartPolicy.OnCrash or UnitRestartPolicy.Never))
            throw new ArgumentException("Unknown unit restart policy.");
    }

    private async Task<bool?> LaunchAsync(CancellationToken token)
    {
        var info = await GetProcessStartInfoAsync(token).ConfigureAwait(false);
        ProcessLaunchArguments(info.ArgumentList);
        _commandPipeName = null;
        AddEnvironmentVariables(info);
        token.ThrowIfCancellationRequested();
        lock (_state)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_stopping) throw new InvalidOperationException("The unit is stopping.");
            var process = new Process { StartInfo = info };
            Process = process;
            try
            {
                if (process.Start()) return true;
            }
            catch { process.Dispose(); Process = null; throw; }
            process.Dispose(); Process = null;
            return false;
        }
    }

    /// <summary>Adds unit/parent identity and a fresh control endpoint for each launch.</summary>
    protected virtual void AddEnvironmentVariables(ProcessStartInfo info)
    {
        info.Environment[UnitsConfigurationExtensions.UnitIdArg] = $"{Configuration.Index}";
        info.Environment[UnitsConfigurationExtensions.UnitParentIdArg] = $"{Environment.ProcessId}";
        _commandPipeName = $"tm-unit-{Environment.ProcessId}-{Guid.NewGuid():N}";
        info.Environment[UnitParentProcessEventsHandler.ControlPipeKey] = _commandPipeName;
    }

    private async Task MonitorAsync(CancellationTokenSource run)
    {
        var token = run.Token;
        try
        {
            while (true)
            {
                Process current;
                lock (_state) { if (_stopping || Process is null) return; current = Process; }
                await current.WaitForExitAsync(token).ConfigureAwait(false);
                var restart = false;
                await _lifecycle.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    lock (_state) { if (_stopping || !ReferenceEquals(Process, current)) return; }
                    try
                    {
                        WriteEvent($"{Configuration.Name} exited ({current.ExitCode})", "UnitProcessOnExited");
                        OnProcessExited(current.ExitCode, current.ExitTime);
                        restart = ShouldRestart(current.ExitCode) is true && RestartCount < (Configuration.RestartLimit ?? 3);
                    }
                    finally
                    {
                        lock (_state) { current.Dispose(); Process = null; }
                    }
                }
                finally { _lifecycle.Release(); }
                if (!restart) { NotifyTerminalExit(); return; }
                await Task.Delay(Configuration.RestartDelay ?? 250, token).ConfigureAwait(false);
                await _lifecycle.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    lock (_state) { if (_stopping) return; }
                    Interlocked.Increment(ref _restartCount);
                    WriteEvent($"{Configuration.Name} restart attempt {RestartCount}/{Configuration.RestartLimit ?? 3}", "UnitProcessRestart");
                    if (await RestartAsync(token).ConfigureAwait(false) is not true)
                        throw new InvalidOperationException("Automatic unit restart did not start.");
                }
                finally { _lifecycle.Release(); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Volatile.Write(ref _lastRestartError, error);
            WriteError(error);
            NotifyTerminalExit();
        }
        finally
        {
            lock (_state)
            {
                if (_stopping && ReferenceEquals(_runCancellation, run))
                { _runCancellation = null; run.Dispose(); }
            }
        }
    }

    private void NotifyTerminalExit()
    {
        lock (_state) { if (_stopping || _disposed) return; }
        _notifying.Value = true;
        try { OnExitAction?.Invoke(this); }
        catch (Exception error) { WriteError(error); }
        finally { _notifying.Value = false; }
    }

    private void WriteEvent(string detail, string category)
    {
        try { EventsSource.Write(() => detail, category); } catch { }
    }
    private void WriteError(Exception error)
    {
        // A diagnostics sink cannot prevent process ownership cleanup.
        try { EventsSource.Exception(error); } catch { }
    }

    /// <summary>Observes each natural exit before deciding whether to restart.</summary>
    protected virtual void OnProcessExited(int? exitCode, DateTime? exitTime) { }

    /// <summary>Selects the policy; the monitor independently enforces the attempt budget.</summary>
    protected virtual bool? ShouldRestart(int? exitCode) => (Configuration.RestartPolicy ?? UnitRestartPolicy.Always) switch
    {
        UnitRestartPolicy.Always => true,
        UnitRestartPolicy.Once => RestartCount == 0,
        UnitRestartPolicy.OnCrash => exitCode != 0,
        UnitRestartPolicy.Never => false,
        _ => false
    };

    /// <summary>Launches a fresh owned process for an automatic attempt.</summary>
    /// <param name="cancellationToken">Cancellation when the owning handle stops.</param>
    /// <returns>Whether OS start succeeded.</returns>
    protected virtual Task<bool?> RestartAsync(CancellationToken cancellationToken) => LaunchAsync(cancellationToken);

    /// <summary>Builds a fresh launch command for the current application.</summary>
    protected virtual Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcessStartInfo
        {
            FileName = GetProcessFilePath(), UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
        });
    /// <summary>Returns the executable used to launch the unit.</summary>
    protected virtual string GetProcessFilePath() => Environment.ProcessPath ?? throw new InvalidOperationException("Path is not available");
    /// <summary>Adds application-specific launch arguments.</summary>
    protected virtual void ProcessLaunchArguments(Collection<string> collection) { }

    /// <inheritdoc />
    public virtual void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        lock (_state) _disposed = true;
        if (await StopAsync().ConfigureAwait(false) is not true)
            throw new InvalidOperationException("The owned unit could not be stopped during disposal.");
    }

    /// <summary>Stops restart scheduling, then gracefully stops or kills/reaps the owned process and drains the monitor.</summary>
    public override async Task<bool?> StopAsync(CancellationToken cancellationToken = default)
    {
        Task completion;
        lock (_state)
        {
            _stopping = true;
            _runCancellation?.Cancel();
            completion = _completion;
        }
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            Process? process;
            lock (_state) process = Process;
            if (process is null) return true;
            if (!process.HasExited)
            {
                using var timeout = new CancellationTokenSource(Configuration.TerminationDelay ?? 1000);
                using var graceful = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
                try
                {
                    await process.StandardInput.WriteLineAsync(UnitCodes.Exit.AsMemory(), graceful.Token).ConfigureAwait(false);
                    await process.StandardInput.FlushAsync(graceful.Token).ConfigureAwait(false);
                    if (_commandPipeName is { } endpoint && !process.HasExited)
                    {
                        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(graceful.Token);
                        var command = SendControlExitAsync(endpoint, commandCancellation.Token);
                        var exit = process.WaitForExitAsync(commandCancellation.Token);
                        try
                        {
                            await Task.WhenAny(command, exit).ConfigureAwait(false);
                            if (process.HasExited) commandCancellation.Cancel();
                            try { await command.ConfigureAwait(false); }
                            catch (OperationCanceledException) when (process.HasExited) { }
                        }
                        finally
                        {
                            commandCancellation.Cancel();
                            try { await Task.WhenAll(command, exit).ConfigureAwait(false); }
                            catch (Exception error) when (error is OperationCanceledException or IOException) { }
                        }
                    }
                    await process.WaitForExitAsync(graceful.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is OperationCanceledException or IOException or InvalidOperationException) { }
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(reap.Token).ConfigureAwait(false);
                }
            }
            lock (_state) { process.Dispose(); Process = null; }
            return true;
        }
        catch (Exception error) { WriteError(error); return false; }
        finally
        {
            _lifecycle.Release();
            if (!_notifying.Value) await completion.ConfigureAwait(false);
        }
    }

    private static async Task SendControlExitAsync(string endpoint, CancellationToken token)
    {
        using var pipe = new NamedPipeClientStream(".", endpoint, PipeDirection.Out, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(token).ConfigureAwait(false);
        await pipe.WriteAsync(System.Text.Encoding.UTF8.GetBytes(UnitCodes.Exit + "\n"), token).ConfigureAwait(false);
        await pipe.FlushAsync(token).ConfigureAwait(false);
    }
}

/// <summary>Commands accepted by child units through the control pipe or legacy stdin.</summary>
public static class UnitCodes
{
    /// <summary>Requests orderly application shutdown.</summary>
    public const string Exit = "qe";
}
