using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.IO.Pipes;

public class PipesSignalServerConnection : PipeConectionHandler
{
    private readonly IEventsSource _eventsSource;
    private readonly IInvocationServerHandler _handler;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _execTaskScheduler;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Task, byte> _invocations = new();
    private readonly object _lifecycleGate = new();
    private CancellationTokenRegistration _externalCancellation;
    private bool _started;
    private int _disposed;
    public event EventHandler? Disconnected;

    public PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler) : base(name, false)
    {
        _eventsSource = eventsSource;
        _handler = handler;
        _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
        _execTaskScheduler = new TmTaskScheduler($"{Name}_exec", 8);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationToken token;
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (_started) { throw new InvalidOperationException("Connection has already been started."); }
            _started = true;
            _externalCancellation = cancellationToken.Register(static source => ((CancellationTokenSource)source!).Cancel(), _cts);
            token = _cts.Token;
        }
        await ConnectAsync(token).ConfigureAwait(false);
        _eventsSource.Write(() => "Connected");
        _ = Task.Factory.StartNew(() => Listen(token), token, TaskCreationOptions.LongRunning, _listenTaskScheduler);
    }

    private void Listen(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var (guid, method, len) = PipeStream.GetRequestHeader();
                if (guid == Guid.Empty)
                {
                    _eventsSource.Write(() => "empty guid");
                    NotifyDisconnected();
                    return;
                }
                Memory<byte> memory = default;
                try
                {
                    if (len > 0)
                    {
                        memory = MemoryPoolUtils.Create(len);
                        PipeStream.ReadFullBuffer(memory);
                    }
                    var requestMemory = memory;
                    // Do not cancel the task before it can run its buffer-return finally block.
                    var task = Task.Factory.StartNew(() => InvokeAsync(guid, method, requestMemory, token),
                        CancellationToken.None, TaskCreationOptions.PreferFairness, _execTaskScheduler).Unwrap();
                    _invocations.TryAdd(task, 0);
                    _ = task.ContinueWith(completed => _invocations.TryRemove(completed, out _),
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    memory = default; // The invocation now owns the request memory.
                }
                finally { memory.TryReturn(); }
            }
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested)
            {
                _eventsSource.Exception(error);
                NotifyDisconnected();
            }
        }
    }

    private async Task InvokeAsync(Guid guid, byte method, Memory<byte> memory, CancellationToken token)
    {
        try
        {
            using var writer = new ArrayPoolBufferWriter<byte>(128);
            var (isSuccess, exception) = await _handler.HandleAsync(method, memory, writer, token).ConfigureAwait(false);
            if (writer.WrittenCount != 0)
            {
                using var resultWriter = new ArrayPoolBufferWriter<byte>(128);
                byte status = isSuccess ? (byte)0 : (byte)1;
                SerializationUtils.Write(guid, resultWriter);
                SerializationUtils.Write(status, resultWriter);
                if (isSuccess)
                {
                    SerializationUtils.Write(writer.WrittenCount, resultWriter);
                    resultWriter.Write(writer.WrittenSpan);
                }
                else
                {
                    var bytes = Encoding.UTF8.GetBytes($"{exception}");
                    SerializationUtils.Write(bytes.Length, resultWriter);
                    resultWriter.Write(bytes);
                }
                PipeStream.Write(resultWriter.WrittenSpan);
            }
        }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested)
            {
                _eventsSource.Exception(error);
                NotifyDisconnected();
            }
        }
        finally { memory.TryReturn(); }
    }

    private void NotifyDisconnected() =>
        ThreadPool.QueueUserWorkItem(_ => Disconnected?.Invoke(this, EventArgs.Empty));

    public override void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        }
        _externalCancellation.Dispose();
        _cts.Cancel();
        base.Dispose();
        _listenTaskScheduler.Dispose();
        // Async handlers may still need the scheduler for their own awaited continuations.
        Task.WhenAll(_invocations.Keys).GetAwaiter().GetResult();
        _execTaskScheduler.Dispose();
        _cts.Dispose();
    }
}

public class PipesInvocationServer<T> : IInvocationServer<T>, IDisposable
{
    private readonly IEventsSource<PipesInvocationServer<T>> _eventsSource;
    private readonly IInvocationServerHandler<T> _handler;
    private readonly CancellationTokenSource _cts;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly List<PipesSignalServerConnection> _connections = [];
    private readonly Lock _sync = new();
    private readonly Task _listener;
    private bool _disposed;

    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler)
    {
        _eventsSource = eventsSource;
        _handler = handler;
        _taskScheduler = new TmTaskScheduler($"{Id}_exec", 2);
        _cts = new CancellationTokenSource();
        _listener = StartListening(_cts.Token);
    }

    private Task StartListening(CancellationToken token) => Task.Factory.StartNew(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            PipesSignalServerConnection? item = null;
            try
            {
                lock (_sync)
                {
                    if (_disposed) { return; }
                    _eventsSource.Write(() => $"new connection - {_connections.Count}");
                    item = new PipesSignalServerConnection(Id, _eventsSource, _handler);
                    item.Disconnected += ItemOnDisconnected;
                    _connections.Add(item);
                }
                await item.StartAsync(token).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (item is not null)
                {
                    lock (_sync) { _connections.Remove(item); }
                    item.Disconnected -= ItemOnDisconnected;
                    item.Dispose();
                }
                if (!token.IsCancellationRequested) { _eventsSource.Exception(error); }
            }
        }
    }, token, TaskCreationOptions.LongRunning, _taskScheduler).Unwrap();

    private void ItemOnDisconnected(object? sender, EventArgs args)
    {
        if (sender is not PipesSignalServerConnection item) { return; }
        lock (_sync) { _connections.Remove(item); }
        item.Disconnected -= ItemOnDisconnected;
        item.Dispose();
    }

    public string Id => typeof(T).FullName!;

    public void Dispose()
    {
        PipesSignalServerConnection[] connections;
        lock (_sync)
        {
            if (_disposed) { return; }
            _disposed = true;
            connections = _connections.ToArray();
            _connections.Clear();
        }
        _cts.Cancel();
        foreach (var connection in connections)
        {
            connection.Disconnected -= ItemOnDisconnected;
            connection.Dispose();
        }
        try { _listener.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        _taskScheduler.Dispose();
        _cts.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
