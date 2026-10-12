using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril;

/// <summary>Observes a parent process and an optional cancellable unit control pipe.</summary>
public class UnitParentProcessEventsHandler : IStartable, IStoppable, IDisposable, IAsyncDisposable
{
    /// <summary>Environment/configuration key for the per-launch owned control pipe endpoint.</summary>
    public const string ControlPipeKey = "TrueMoon.Mithril.ControlPipe";
    private readonly IAppLifetime _appLifetime;
    private readonly IEventsSource<UnitParentProcessEventsHandler> _eventsSource;
    private readonly int? _parentId;
    private readonly string? _endpoint;
    private readonly object _gate = new();
    private readonly AsyncLocal<bool> _notifying = new();
    private Process? _parent;
    private NamedPipeServerStream? _pipe;
    private CancellationTokenSource? _cancellation;
    private Task _parentTask = Task.CompletedTask;
    private Task _commandTask = Task.CompletedTask;
    private Task? _stopTask;
    private bool _started;
    private volatile bool _stopping;
    private int _notified;

    /// <summary>Creates a listener; the observed parent is borrowed and is never terminated.</summary>
    /// <param name="configuration">Unit identity, parent id and optional control endpoint.</param>
    /// <param name="appLifetime">The child application's lifetime.</param>
    /// <param name="eventsSource">Diagnostics.</param>
    public UnitParentProcessEventsHandler(IConfiguration configuration, IAppLifetime appLifetime,
        IEventsSource<UnitParentProcessEventsHandler> eventsSource)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(appLifetime);
        ArgumentNullException.ThrowIfNull(eventsSource);
        _appLifetime = appLifetime;
        _eventsSource = eventsSource;
        _parentId = configuration.GetProcessingUnitParentId();
        _endpoint = configuration.Get<string>(ControlPipeKey);
    }

    /// <summary>Completion of the owned monitor and command tasks; also usable after shutdown.</summary>
    public Task Completion { get { lock (_gate) return Task.WhenAll(_parentTask, _commandTask); } }

    /// <summary>Starts observing the supplied parent once. A missing parent cancels the App immediately.</summary>
    /// <param name="parentProcessId">A positive id different from the current process.</param>
    /// <param name="cancellationToken">Cancellation interrupts monitor/pipe operations.</param>
    public void StartListen(int parentProcessId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (parentProcessId <= 0 || parentProcessId == Environment.ProcessId) throw new ArgumentOutOfRangeException(nameof(parentProcessId));
        var missing = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_started) throw new InvalidOperationException("Parent listener is already started.");
            _started = true;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _appLifetime.AppCancellationToken);
            try
            {
                _parent = Process.GetProcessById(parentProcessId);
                if (!string.IsNullOrWhiteSpace(_endpoint))
                    _pipe = new NamedPipeServerStream(_endpoint, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var token = _cancellation.Token;
                _parentTask = Task.Run(() => ObserveParentAsync(token));
                if (_pipe is not null) _commandTask = Task.Run(() => ListenCommandsAsync(token));
            }
            catch (ArgumentException) when (_parent is null) { missing = true; }
            catch
            {
                _pipe?.Dispose(); _parent?.Dispose(); _cancellation.Dispose();
                _stopping = true;
                throw;
            }
        }
        if (missing) Notify("parent process unavailable");
    }

    private async Task ObserveParentAsync(CancellationToken token)
    {
        try
        {
            await _parent!.WaitForExitAsync(token).ConfigureAwait(false);
            Notify("parent process exit");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!_stopping) { _eventsSource.Exception(error); Notify("parent observer failed"); } }
    }

    private async Task ListenCommandsAsync(CancellationToken token)
    {
        try
        {
            await _pipe!.WaitForConnectionAsync(token).ConfigureAwait(false);
            using var reader = new StreamReader(_pipe, System.Text.Encoding.UTF8, false, 128, leaveOpen: true);
            while (!token.IsCancellationRequested)
            {
                var command = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (command is null || command == UnitCodes.Exit)
                {
                    Notify(command is null ? "control pipe EOF" : "exit requested");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!_stopping) { _eventsSource.Exception(error); Notify("control pipe failed"); } }
    }

    private void Notify(string reason)
    {
        if (_stopping || Interlocked.CompareExchange(ref _notified, 1, 0) != 0) return;
        _notifying.Value = true;
        try { _eventsSource.Write(() => reason); _appLifetime.Cancel(); }
        catch (Exception error) { _eventsSource.Exception(error); }
        finally { _notifying.Value = false; }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StartListen(_parentId ?? throw new ArgumentException("Invalid parent process id."), cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _stopping = true;
            // A lifetime callback may dispose its own listener; do not await the calling monitor.
            var stop = _stopTask ??= StopCoreAsync();
            return _notifying.Value ? Task.CompletedTask : stop;
        }
    }

    private async Task StopCoreAsync()
    {
        _cancellation?.Cancel();
        await Task.WhenAll(_parentTask, _commandTask).ConfigureAwait(false);
        _pipe?.Dispose(); _parent?.Dispose(); _cancellation?.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => StopAsync().GetAwaiter().GetResult();
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
