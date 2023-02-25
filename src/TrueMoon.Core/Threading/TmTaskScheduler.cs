using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace TrueMoon.Threading;

public sealed class TmTaskScheduler : TaskScheduler, IDisposable
{
    private readonly List<Thread> _threads = new ();

    private readonly Channel<Task> _channel;
    //private readonly BlockingCollection<Task> _tasks = new ();
    
    public TmTaskScheduler(string name, int threads, CancellationToken cancellationToken = default)
    {
        if (threads < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threads));
        }

        _channel = Channel.CreateUnbounded<Task>(new UnboundedChannelOptions{SingleReader = false, SingleWriter = false});
        
        for (var i = 0; i < threads; i++)
        {
            var thread = new Thread(()=> ThreadLoop(cancellationToken))
            {
                Name = $"{name}Thread_{i}",
                IsBackground = true
            };
            
#pragma warning disable CA1416
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                thread.SetApartmentState(ApartmentState.STA);
            }
#pragma warning restore CA1416
            
            _threads.Add(thread);
            thread.Start();
        }
    }

    private void ThreadLoop(CancellationToken cancellationToken)
    {
        try
        {
            // foreach (var t in _tasks.GetConsumingEnumerable(cancellationToken))
            // {
            //     TryExecuteTask(t);
            // }

            var spin = new SpinWait();
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_channel.Reader.TryRead(out var task))
                {
                    TryExecuteTask(task);
                }
                else
                {
                    spin.SpinOnce();
                }
            }
        }
        catch (OperationCanceledException)
        {
            
        }
    }

    /// <inheritdoc />
    //protected override void QueueTask(Task task) => _tasks.Add(task);
    protected override void QueueTask(Task task)
    {
        if (_channel.Writer.TryWrite(task))
        {
            return;
        }

        var spin = new SpinWait();

        while (!_channel.Writer.TryWrite(task))
        {
            spin.SpinOnce();
        }
    }

    /// <inheritdoc />
    //protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();
    protected override IEnumerable<Task> GetScheduledTasks() => Array.Empty<Task>();

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
        TryExecuteTask(task);

    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => _threads.Count;
    
    public void Dispose()
    {
        //_tasks.CompleteAdding();
        
        
        // foreach (var thread in _threads)
        // {
        //     thread.Join();
        // }
        
        //_tasks.Dispose();

        _channel.Writer.TryComplete();
    }
}