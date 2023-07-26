using TrueMoon.Diagnostics;

namespace TrueMoon.Thorium.IO.Pipes;

public abstract class SignalServer<T> : PipeConectionHandler, ISignalServer<T>
{
    protected readonly T _service;
    private readonly IAppLifetime _appLifetime;
    private readonly IEventsSource<SignalServer<T>> _eventsSource;
    private readonly CancellationTokenSource _cts;

    public SignalServer(T service, IAppLifetime appLifetime, IEventsSource<SignalServer<T>> eventsSource) : base(typeof(T).FullName, false)
    {
        _service = service;
        _appLifetime = appLifetime;
        _eventsSource = eventsSource;

        _cts = new CancellationTokenSource();
        _appLifetime.OnStopping(() =>
        {
            _cts.Cancel();
        });
        Initialize();
    }

    private void Initialize()
    {
        Task.Factory.StartNew(async () =>
        {
            await ConnectAsync();
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var (guid, method, len) = PipeStream.GetInput();

                    Memory<byte> mem = Memory<byte>.Empty;
                    if (len > 0)
                    {
                        mem = MemoryPoolUtils.Create(len);

                        await PipeStream.ReadExactlyAsync(mem, _cts.Token);
                    }

                    _ = Task.Factory.StartNew(async () => { await ProcessMessageAsync(guid, method, mem,_cts.Token); },
                        cancellationToken: _cts.Token,
                        TaskCreationOptions.PreferFairness | TaskCreationOptions.RunContinuationsAsynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception e)
                {
                    _eventsSource.Exception(e);
                }
            }
        });
    }

    private async Task ProcessMessageAsync(Guid guid, byte method, Memory<byte> memory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessAsync(guid, method, memory, cancellationToken);
        }
        finally
        {
            memory.TryReturn();
        }
    }
    
    protected virtual async Task ProcessAsync(Guid guid, byte method, Memory<byte> memory,
        CancellationToken cancellationToken = default)
    {
        
    }

    public string Id => typeof(T).FullName!;
}