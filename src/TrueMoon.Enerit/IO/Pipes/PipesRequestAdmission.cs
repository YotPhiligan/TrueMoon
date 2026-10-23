namespace TrueMoon.Enerit.IO.Pipes;

internal sealed class PipesClientAdmission(PipesTransportOptions options)
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
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_slots.Wait(0))
            {
                ResetDrained(); _pending++; return;
            }
            if (_queued == options.MaxQueuedRequests) throw new PipesInvocationOverloadedException();
            ResetDrained(); _queued++;
        }
        var entered = false;
        try { await _slots.WaitAsync(token).ConfigureAwait(false); entered = true; }
        finally
        {
            lock (_gate)
            {
                _queued--;
                if (entered) _pending++;
                SignalDrained();
            }
        }
    }

    internal void Release()
    {
        lock (_gate) { _pending--; _slots.Release(); SignalDrained(); }
    }
    internal void Close() { lock (_gate) _closed = true; }
    private void ResetDrained()
    { if (_pending == 0 && _queued == 0) _drained = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private void SignalDrained() { if (_pending == 0 && _queued == 0) _drained.TrySetResult(); }
    private static TaskCompletionSource Completed()
    { var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); result.SetResult(); return result; }
}

internal sealed class PipesServerAdmission(PipesTransportOptions options)
{
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
            if (_closed || _requests == options.MaxConcurrentRequests || length > options.MaxBufferedRequestBytes - _bytes) return false;
            _requests++; _bytes += length; return true;
        }
    }
    internal void Release(int length)
    { lock (_gate) { _requests--; _bytes -= length; } }
    internal void Close() { lock (_gate) _closed = true; }
}
