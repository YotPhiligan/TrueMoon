using System.Diagnostics;
using System.Text;

namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Caller-owned one-shot operation. Cancellation kills and reaps the isolated worker before completion.</summary>
public sealed class ModelImportOperation : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ModelImportHost _host;
    private readonly ModelImportArguments _arguments;
    private readonly CancellationTokenSource _stop = new();
    private Task<ModelImportCandidate>? _operation;
    private Task? _dispose;
    private bool _disposed;
    private Process? _process;
    private bool _started;
    private ModelImportJob? _job;
    private ModelSourceFiles? _deployment;
    private Task<string>? _stdout, _stderr;
    private string? _scratch;
    private int _workerId;
    private int _reaped;

    internal ModelImportOperation(ModelImportHost host, ModelImportArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments); ArgumentNullException.ThrowIfNull(arguments.Source); ArgumentNullException.ThrowIfNull(arguments.Worker);
        if (!ModelImportCandidate.IsValidGeometryScale(arguments.GeometryScale))
            throw new ArgumentOutOfRangeException(nameof(arguments), "Model geometry scale must be finite and positive.");
        if (!Path.IsPathFullyQualified(arguments.ScratchDirectory)) throw new ArgumentException("Absolute scratch directory required.");
        var deadline = arguments.Deadline ?? TimeSpan.FromSeconds(10);
        if (deadline <= TimeSpan.Zero || deadline > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(arguments), "Deadline must be finite and <= 30 seconds.");
        if (Path.GetExtension(arguments.Source.RootName).ToLowerInvariant() is not (".gltf" or ".glb"))
            throw new NotSupportedException("CPU model import v1 supports glTF 2.0/GLB only.");
        _host = host; _arguments = arguments with { ScratchDirectory = Path.GetFullPath(arguments.ScratchDirectory), Deadline = deadline };
    }
    /// <summary>Actual started worker PID, or zero if no worker was started.</summary>
    public int WorkerProcessId => Volatile.Read(ref _workerId);
    /// <summary>True only after an actual started worker exited and its output pipes drained.</summary>
    public bool WorkerReaped => Volatile.Read(ref _reaped) != 0;

    /// <summary>Acquires admission immediately; no queue. Result is published only after child exit, validation and cleanup.</summary>
    public Task<ModelImportCandidate> ImportAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_operation != null) throw new InvalidOperationException("Model import is one-shot.");
            cancellationToken.ThrowIfCancellationRequested(); _host.Enter(this);
            return _operation = RunAsync(cancellationToken);
        }
    }
    private async Task<ModelImportCandidate> RunAsync(CancellationToken caller)
    {
        await Task.Yield(); // Publish the operation task before shutdown can observe an admitted product.
        using var deadline = new CancellationTokenSource(_arguments.Deadline!.Value);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller, _stop.Token, deadline.Token); var token = linked.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows() || IntPtr.Size != 8) throw new PlatformNotSupportedException("Isolated model import requires Windows x64.");
            _deployment = await _arguments.Worker.PinAsync(token).ConfigureAwait(false);
            _scratch = Path.Combine(_arguments.ScratchDirectory, "model-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratch);
            var input = Path.Combine(_scratch, "input.packet"); var output = Path.Combine(_scratch, "candidate.packet");
            ModelImportProtocol.WriteInput(input, _arguments.Source); token.ThrowIfCancellationRequested();
            var info = new ProcessStartInfo(_arguments.Worker.DotnetHost)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = _scratch
            };
            info.ArgumentList.Add(Path.Combine(_arguments.Worker.Directory, ModelImportWorkerDeployment.EntryAssembly));
            info.ArgumentList.Add(input); info.ArgumentList.Add(output);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _process = new Process { StartInfo = info };
            if (!_process.Start()) throw new IOException("Model worker failed to start.");
            _started = true; Volatile.Write(ref _workerId, _process.Id);
            _stdout = DrainAsync(_process.StandardOutput, ready); _stderr = DrainAsync(_process.StandardError, null);
            _job = ModelImportJob.Attach(_process);
            await ready.Task.WaitAsync(token).ConfigureAwait(false);
            await _process.StandardInput.WriteLineAsync("GO".AsMemory(), token).ConfigureAwait(false); _process.StandardInput.Close();
            await _process.WaitForExitAsync(token).ConfigureAwait(false);
            var stdout = await _stdout.WaitAsync(token).ConfigureAwait(false); var stderr = await _stderr.WaitAsync(token).ConfigureAwait(false);
            Volatile.Write(ref _reaped, 1);
            if (_process.ExitCode != 0 || stdout.Replace("\r", "", StringComparison.Ordinal) != "TMIM/1 READY\nTMIM/1 DONE\n" || stderr.Length != 0)
                throw new InvalidDataException($"Model worker failed (exit {_process.ExitCode}). {stderr}");
            var result = ModelImportProtocol.ReadOutput(output, _arguments.Source).ScaleGeometry(_arguments.GeometryScale, token);
            await _deployment.RevalidateAsync(token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); return result;
        }
        catch (OperationCanceledException error) when (deadline.IsCancellationRequested && !caller.IsCancellationRequested && !_stop.IsCancellationRequested)
        { throw new TimeoutException("Model import deadline expired; worker cleanup follows before completion.", error); }
        finally { await CleanupAsync().ConfigureAwait(false); }
    }
    private static async Task<string> DrainAsync(StreamReader reader, TaskCompletionSource? ready)
    {
        var text = new StringBuilder(); var buffer = new char[1024]; var overflow = false; var signaled = false;
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                var remaining = 8192 - text.Length; text.Append(buffer, 0, Math.Min(remaining, read)); overflow |= read > remaining;
                if (ready != null && !signaled && text.ToString().Contains('\n'))
                {
                    signaled = true;
                    if (text.ToString().StartsWith("TMIM/1 READY\n", StringComparison.Ordinal) || text.ToString().StartsWith("TMIM/1 READY\r\n", StringComparison.Ordinal)) ready.TrySetResult();
                    else ready.TrySetException(new InvalidDataException("Invalid worker handshake."));
                }
            }
            if (overflow) throw new InvalidDataException("Worker diagnostic output quota exceeded.");
            return text.ToString();
        }
        finally { ready?.TrySetException(new EndOfStreamException("Worker exited before ready.")); }
    }
    private async Task CleanupAsync()
    {
        // Failure retains process, job, pinned deployment, scratch and host admission for DisposeAsync retry.
        if (_started && _process != null)
        {
            if (!_process.HasExited)
            {
                try { if (_job != null) _job.Terminate(); else _process.Kill(entireProcessTree: true); }
                catch (Exception) when (_process.HasExited) { }
            }
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            foreach (var pipe in new[] { _stdout, _stderr })
            {
                if (pipe == null) continue;
                try { await pipe.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
                catch when (pipe.IsCompleted) { /* A quota/protocol failure is drained and remains on the import task. */ }
            }
            Volatile.Write(ref _reaped, 1);
        }
        _job?.Dispose(); _job = null; _process?.Dispose(); _process = null; _started = false;
        _deployment?.Dispose(); _deployment = null;
        if (_scratch != null) { if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true); _scratch = null; }
        _host.Exit(this);
    }
    /// <summary>Cancels, waits for the import and retries incomplete cleanup. Import errors remain observable on ImportAsync.</summary>
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
            // A failed cleanup can be retried; concurrent disposers share the current attempt.
            if (_dispose == null || _dispose.IsFaulted) _dispose = DisposeCoreAsync();
            return new(_dispose);
        }
    }
    private async Task DisposeCoreAsync()
    {
        await Task.Yield(); _stop.Cancel(); Task<ModelImportCandidate>? operation;
        lock (_gate) operation = _operation;
        if (operation != null) { try { await operation.ConfigureAwait(false); } catch { } }
        await CleanupAsync().ConfigureAwait(false);
        lock (_gate) _operation = null;
        _stop.Dispose();
    }
}
