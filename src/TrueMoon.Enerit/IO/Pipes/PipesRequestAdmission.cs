using System.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

internal sealed class PipesClientAdmission(PipesTransportOptions options, PipesMetrics? metrics = null)
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _slots = new(options.MaxPendingRequests, options.MaxPendingRequests);
    private TaskCompletionSource _drained = Completed();
    private int _pending, _queued;
    private bool _closed;
    internal int Pending { get { lock (_gate) return _pending; } }
    internal int Queued { get { lock (_gate) return _queued; } }
    internal Task Drained { get { lock (_gate) return _drained.Task; } }

    internal async Task EnterAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long waitStarted = 0;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_slots.Wait(0))
            {
                ResetDrained(); _pending++; metrics?.Client(_pending, _queued); return;
            }
            if (_queued == options.MaxQueuedRequests) { metrics?.RejectClient(); throw new PipesInvocationOverloadedException(); }
            ResetDrained(); _queued++; metrics?.Client(_pending, _queued);
            if (metrics != null) waitStarted = Stopwatch.GetTimestamp();
        }
        var entered = false;
        try { await _slots.WaitAsync(token).ConfigureAwait(false); entered = true; }
        finally
        {
            lock (_gate)
            {
                _queued--;
                if (entered) _pending++;
                metrics?.Client(_pending, _queued);
                metrics?.QueueWait(Stopwatch.GetElapsedTime(waitStarted), entered ? PipesQueueWaitOutcome.Admitted :
                    _closed ? PipesQueueWaitOutcome.Stopped : PipesQueueWaitOutcome.Cancelled);
                SignalDrained();
            }
        }
    }

    internal void Release()
    {
        lock (_gate) { _pending--; metrics?.Client(_pending, _queued); _slots.Release(); SignalDrained(); }
    }
    internal void Close() { lock (_gate) _closed = true; }
    private void ResetDrained()
    { if (_pending == 0 && _queued == 0) _drained = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private void SignalDrained() { if (_pending == 0 && _queued == 0) _drained.TrySetResult(); }
    private static TaskCompletionSource Completed()
    { var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); result.SetResult(); return result; }
}

internal sealed class PipesServerAdmission(PipesTransportOptions options, PipesMetrics? metrics = null)
{
    internal PipesMetrics? Metrics => metrics;
    private readonly object _gate = new();
    private int _requests;
    private long _bytes;
    private bool _closed;
    internal int Requests { get { lock (_gate) return _requests; } }
    internal long Bytes { get { lock (_gate) return _bytes; } }
    internal bool TryEnter(int length)
    {
        lock (_gate)
        {
            if (_closed) return false;
            if (_requests == options.MaxConcurrentRequests) { metrics?.RejectServer(false); return false; }
            if (length > options.MaxBufferedRequestBytes - _bytes) { metrics?.RejectServer(true); return false; }
            _requests++; _bytes += length; metrics?.Server(_requests, _bytes); return true;
        }
    }
    internal void Release(int length)
    { lock (_gate) { _requests--; _bytes -= length; metrics?.Server(_requests, _bytes); } }
    internal void Close() { lock (_gate) _closed = true; }
}
