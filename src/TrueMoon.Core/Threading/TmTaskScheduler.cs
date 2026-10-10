using System.Threading.Channels;

namespace TrueMoon.Threading;

/// <summary>Runs tasks on dedicated workers (STA on Windows).</summary>
/// <remarks>
/// Disposal and constructor-token cancellation stop acceptance and drain accepted tasks.
/// Disposal from another thread waits for all workers; disposal from a worker only requests shutdown.
/// Tasks must cooperate with their own cancellation tokens to interrupt running work.
/// </remarks>
public sealed class TmTaskScheduler : TaskScheduler, IDisposable
{
    private readonly Thread[] _threads;
    private readonly Channel<Task> _channel = Channel.CreateUnbounded<Task>();
    private readonly HashSet<Task> _pending = [];
    private readonly object _gate = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private int _remainingWorkers;
    private bool _completed;

    /// <summary>Creates a scheduler with the requested number of dedicated workers.</summary>
    /// <param name="name">Prefix used in worker thread names.</param>
    /// <param name="threads">Positive maximum worker count.</param>
    /// <param name="cancellationToken">Requests queue completion and draining when cancelled.</param>
    public TmTaskScheduler(string name, int threads, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);
        _threads = new Thread[threads];
        _remainingWorkers = threads;
        for (var i = 0; i < threads; i++)
        {
            var thread = new Thread(ThreadLoop)
            {
                Name = $"{name}Thread_{i}",
                IsBackground = true
            };
            if (OperatingSystem.IsWindows())
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
            _threads[i] = thread;
        }
        _cancellationRegistration = cancellationToken.Register(static state => ((TmTaskScheduler)state!).Complete(), this);
        foreach (var thread in _threads)
        {
            thread.Start();
        }
    }

    /// <summary>Completes after every accepted task has run and all workers have exited.</summary>
    public Task Completion => _completion.Task;

    private void ThreadLoop()
    {
        try
        {
            while (_channel.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
            {
                while (_channel.Reader.TryRead(out var task))
                {
                    lock (_gate)
                    {
                        _pending.Remove(task);
                    }
                    TryExecuteTask(task);
                }
            }
        }
        finally
        {
            if (Interlocked.Decrement(ref _remainingWorkers) == 0)
            {
                _cancellationRegistration.Unregister();
                _completion.TrySetResult();
            }
        }
    }

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_completed, this);
            _pending.Add(task);
            if (!_channel.Writer.TryWrite(task))
            {
                _pending.Remove(task);
                throw new ObjectDisposedException(nameof(TmTaskScheduler));
            }
        }
    }

    /// <inheritdoc />
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (_gate)
        {
            return _pending.ToArray();
        }
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        // A queued task stays owned by the queue. Foreign threads cannot violate worker affinity.
        if (taskWasPreviouslyQueued || !_threads.Contains(Thread.CurrentThread))
        {
            return false;
        }
        lock (_gate)
        {
            if (_completed)
            {
                return false;
            }
        }
        return TryExecuteTask(task);
    }

    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => _threads.Length;

    private void Complete()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }
            _completed = true;
            _channel.Writer.TryComplete();
        }
    }

    /// <summary>Stops acceptance, drains queued work and waits unless called on a worker.</summary>
    public void Dispose()
    {
        Complete();
        _cancellationRegistration.Unregister();
        if (_threads.Contains(Thread.CurrentThread))
        {
            return;
        }
        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }
}
