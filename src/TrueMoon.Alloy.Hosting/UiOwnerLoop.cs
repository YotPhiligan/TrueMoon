using TrueMoon.Alloy;

using System.Runtime.CompilerServices;

namespace TrueMoon.Alloy.Hosting;

// No native dependencies: each participant owns its fault and cleanup. Windows hosting
// shares one instance for the process; tests can stop independent instances.
internal sealed class UiOwnerLoop
{
    private readonly object _gate = new();
    private readonly Queue<Work> _pending = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _thread;
    private bool _stopping;

    internal bool IsOwnerThread => ReferenceEquals(Thread.CurrentThread, _thread);

    internal void Schedule(Action initialize, Func<bool> tick, Action cleanup, Func<bool> closing,
        TaskCompletionSource ready, TaskCompletionSource completion, Action? finished = null)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_thread == null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "TrueMoon Alloy owner" };
                try { _thread.Start(); }
                catch { _thread = null; throw; }
            }
            _pending.Enqueue(new Work(initialize, tick, cleanup, closing, ready, completion, finished));
            _wake.Set();
        }
    }

    internal void Wake() => _wake.Set();

    internal Task StopAsync()
    {
        lock (_gate)
        {
            _stopping = true;
            if (_thread == null) _completion.TrySetResult();
            _wake.Set();
            return _completion.Task;
        }
    }

    private void Run()
    {
        var active = new List<Work>();
        while (true)
        {
            bool stopping;
            lock (_gate)
            {
                // Bound startup work so producers cannot starve live windows.
                for (var i = 0; i < 32 && _pending.TryDequeue(out var work); i++) active.Add(work);
                stopping = _stopping;
                if (stopping && active.Count == 0 && _pending.Count == 0) break;
            }
            for (var i = 0; i < active.Count;)
            {
                if (!Process(active[i], stopping)) { i++; continue; }
                active.RemoveAt(i);
            }
            lock (_gate) { if (_pending.Count > 0 || _stopping) continue; }
            // Sleep indefinitely without windows; bounded wait with live windows also
            // permits close/commands while rendering is suspended or rate-limited.
            _wake.WaitOne(active.Count == 0 ? Timeout.Infinite : 1);
        }
        _completion.TrySetResult();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Process(Work work, bool stopping)
    {
        Exception? failure = null;
        var done = stopping;
        try
        {
            done |= work.Closing();
            if (!done && !work.Initialized)
            {
                work.Initialize(); work.Initialized = true; work.Ready.TrySetResult();
            }
            if (!done) done = work.Tick();
        }
        catch (Exception error) { failure = error; done = true; }
        if (!done) return false;
        try { UiCleanup.Complete(failure, work.Cleanup); }
        catch (Exception error) { failure = error; }
        // The persistent Run frame may retain its last queue item. Clear targets
        // before notifying consumers; callbacks/errors stay in this bounded frame.
        work.Initialize = work.Cleanup = static () => { };
        work.Tick = work.Closing = static () => true;
        var finished = work.Finished; work.Finished = null;
        var ready = work.Ready; var completion = work.Completion;
        work.Ready = work.Completion = null!;
        if (failure != null)
        {
            ready.TrySetException(failure); completion.TrySetException(failure);
        }
        else
        {
            if (!work.Initialized) ready.TrySetCanceled();
            completion.TrySetResult();
        }
        finished?.Invoke();
        return true;
    }

    private sealed class Work(Action initialize, Func<bool> tick, Action cleanup, Func<bool> closing,
        TaskCompletionSource ready, TaskCompletionSource completion, Action? finished)
    {
        internal Action Initialize = initialize, Cleanup = cleanup;
        internal Func<bool> Tick = tick, Closing = closing;
        internal TaskCompletionSource Ready = ready, Completion = completion;
        internal Action? Finished = finished;
        internal bool Initialized;
    }
}
