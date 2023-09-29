using System.Buffers;
using System.Text;
using TrueMoon.Diagnostics;
using TrueMoon.Threading;

namespace TrueMoon.Thorium.IO.Pipes;

public class PipesSignalServerConnection : PipeConectionHandler
{
    private readonly IEventsSource _eventsSource;
    private readonly IInvocationServerHandler _handler;
    private readonly TmTaskScheduler _listenTaskScheduler;
    private readonly TmTaskScheduler _execTaskScheduler;
    public event EventHandler Disconnected;

    public PipesSignalServerConnection(string name, IEventsSource eventsSource, IInvocationServerHandler handler) : base(name, false)
    {
        _eventsSource = eventsSource;
        _handler = handler;
        _listenTaskScheduler = new TmTaskScheduler($"{Name}_listen", 1);
        _execTaskScheduler = new TmTaskScheduler($"{Name}_exec", 8);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await ConnectAsync(cancellationToken);
        _eventsSource.Write(()=>"Connected");
        
        _ = Task.Factory.StartNew(() =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var (guid, method, len) = PipeStream.GetRequestHeader();
                    
                    if (guid == Guid.Empty)
                    {
                        _eventsSource.Write(() => "empty guid");
                        Disconnected?.Invoke(this, EventArgs.Empty);
                        return;
                    }
                    
                    Memory<byte> mem = default;
                    if (len > 0)
                    {
                        mem = MemoryPoolUtils.Create(len);

                        PipeStream.ReadFullBuffer(mem);
                    }
 
                    _ = Task.Factory.StartNew(async () =>
                        {
                            try
                            {
                                using var writer = new ArrayPoolBufferWriter<byte>(128);
                                var (isSuccess, exception) =
                                    await _handler.HandleAsync(method, mem, writer, cancellationToken);

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
                                        var str = $"{exception}";
                                        var bytes = Encoding.UTF8.GetBytes(str);
                                        SerializationUtils.Write(bytes.Length, resultWriter);
                                        resultWriter.Write(bytes);
                                    }

                                    PipeStream.Write(resultWriter.WrittenSpan);
                                }
                            }
                            catch (Exception e)
                            {
                                _eventsSource.Exception(e);
                                Disconnected?.Invoke(this, EventArgs.Empty);
                            }
                            finally
                            {
                                mem.TryReturn();
                            }
                        },
                        cancellationToken: cancellationToken,
                        TaskCreationOptions.PreferFairness | TaskCreationOptions.RunContinuationsAsynchronously,
                        _execTaskScheduler);
                }
            }
            catch (Exception e)
            {
                _eventsSource.Exception(e);
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        },cancellationToken, TaskCreationOptions.LongRunning, _listenTaskScheduler);
    }
}

public class PipesInvocationServer<T> : IInvocationServer<T>, IDisposable
{
    private readonly IEventsSource<PipesInvocationServer<T>> _eventsSource;
    private readonly IInvocationServerHandler<T> _handler;
    private readonly CancellationTokenSource _cts;
    private readonly TmTaskScheduler _taskScheduler;
    private readonly List<PipesSignalServerConnection> _connections = new ();
    private readonly object _sync = new ();

    public PipesInvocationServer(IEventsSource<PipesInvocationServer<T>> eventsSource, IInvocationServerHandler<T> handler)
    {
        _eventsSource = eventsSource;
        _handler = handler;
        _taskScheduler = new TmTaskScheduler($"{Id}_exec", 2);

        _cts = new CancellationTokenSource();
        StartListening();
    }

    
    private void StartListening()
    {
        Task.Factory.StartNew(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    _eventsSource.Write(() => $"new connection - {_connections.Count}");
                    var item = new PipesSignalServerConnection(Id, _eventsSource, _handler);
                    lock (_sync)
                    {
                        _connections.Add(item);
                    }
                    item.Disconnected += ItemOnDisconnected;

                    await item.StartAsync(_cts.Token);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                }
            }
        },_cts.Token, TaskCreationOptions.LongRunning, _taskScheduler);
    }

    private void ItemOnDisconnected(object? sender, EventArgs e)
    {
        var item = sender as PipesSignalServerConnection;
        lock (_sync)
        {
            _connections.Remove(item);
        }
        item.Dispose();
    }

    public string Id => typeof(T).FullName!;

    public void Dispose()
    {
        _cts.Cancel();
        lock (_sync)
        {
            foreach (var connection in _connections)
            {
                connection.Dispose();
            }
            _connections.Clear();
        }
        _cts.Dispose();
        _taskScheduler.Dispose();
    }
}