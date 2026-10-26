namespace TrueMoon.Diagnostics;

internal sealed class DiagnosticSinkWorker
{
    private readonly Lock _gate = new();
    private readonly Queue<(DiagnosticRecord Record, long Bytes)> _queue = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly IDiagnosticSink _sink;
    private readonly DiagnosticPipelineOptions _options;
    private readonly string _name;
    private readonly Task _worker;
    private TaskCompletionSource _progress = NewSignal();
    private Task? _cleanup;
    private bool _closed, _released;
    private long _accepted, _completed, _written, _dropped, _failures, _bytes;
    private int _pending;
    private string? _lastError;
    private readonly DiagnosticEmergencyOutput? _emergency;
    private DiagnosticRetentionStatus? _reportedRetention;
    internal string Name => _name;

    internal DiagnosticSinkWorker(string name, IDiagnosticSink sink, DiagnosticPipelineOptions options,
        DiagnosticEmergencyOutput? emergency = null)
    {
        _name = name; _sink = sink; _options = options;
        _emergency = emergency;
        _worker = Task.Run(RunAsync);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal void Offer(DiagnosticRecord record, long bytes)
    {
        lock (_gate)
        {
            if (_closed) return;
            var limit = record.Level >= DiagnosticLevel.Error ? _options.QueueCapacity :
                _options.QueueCapacity - _options.ReservedErrorCapacity;
            if (_pending >= limit || bytes > _options.QueueByteCapacity - _bytes)
            {
                _dropped++;
                if (record.Level >= DiagnosticLevel.Error) _emergency?.Report("QueueOverflow", _name, sequence: record.Sequence);
                return;
            }
            _queue.Enqueue((record, bytes)); _pending++; _bytes += bytes; _accepted++;
            _available.Release();
        }
    }

    internal DiagnosticSinkStatus Read()
    {
        lock (_gate) return new(_name, _accepted, _written, _dropped, _failures, _pending, _bytes, _lastError);
    }

    internal void Close()
    {
        lock (_gate) { if (_closed) return; _closed = true; _available.Release(); }
    }

    internal Task FlushAsync(CancellationToken token)
    {
        long target; lock (_gate) target = _accepted;
        return WaitAsync(target, token);
    }

    private async Task WaitAsync(long target, CancellationToken token)
    {
        while (true)
        {
            Task progress;
            lock (_gate) { if (_completed >= target) return; progress = _progress.Task; }
            await progress.WaitAsync(token).ConfigureAwait(false);
        }
    }

    private void Failed(Exception error, string stage, long? sequence = null)
    {
        string text;
        try { text = error.GetType().Name + ": " + error.Message; }
        catch { text = error.GetType().Name; }
        lock (_gate) { _failures++; _lastError = text[..Math.Min(text.Length, 1024)]; }
        _emergency?.Report(stage, _name, error, sequence);
    }

    private void ReportRetention()
    {
        if (_sink is not JsonDiagnosticSink json || json.RetentionStatus is not { } status || ReferenceEquals(status, _reportedRetention)) return;
        _reportedRetention = status;
        if (status.Failures > 0 || status.IsOverBudget || status.MaintenanceBusy)
            _emergency?.Report("Retention", _name);
    }

    private async Task RunAsync()
    {
        while (true)
        {
            await _available.WaitAsync().ConfigureAwait(false);
            (DiagnosticRecord Record, long Bytes) item;
            lock (_gate)
            {
                if (_queue.Count == 0) { if (_closed) return; continue; }
                item = _queue.Dequeue();
            }
            try { await _sink.WriteAsync(item.Record).ConfigureAwait(false); lock (_gate) _written++; }
            catch (Exception error) { Failed(error, "Write", item.Record.Sequence); }
            finally
            {
                ReportRetention();
                lock (_gate)
                {
                    _pending--; _bytes -= item.Bytes; _completed++;
                    var progress = _progress; _progress = NewSignal(); progress.TrySetResult();
                }
            }
        }
    }

    internal Task DisposeAsync()
    {
        lock (_gate)
        {
            Close();
            if (_released) return Task.CompletedTask;
            // Dispose may perform synchronous filesystem maintenance before its first await.
            return _cleanup is { IsCompleted: false } ? _cleanup : _cleanup = Task.Run(CleanupAsync);
        }
    }

    private async Task CleanupAsync()
    {
        await _worker.ConfigureAwait(false);
        try { await _sink.DisposeAsync().ConfigureAwait(false); }
        catch (Exception error) { Failed(error, "Dispose"); throw; }
        finally { ReportRetention(); }
        lock (_gate) { _released = true; _available.Dispose(); }
    }
}
