using System.Diagnostics;
using TrueMoon.Diagnostics;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.Signals;

namespace TrueMoon.Thorium;

public class SignalProcessor : IDisposable, IStartable
{
    private readonly ThoriumConfiguration _configuration;
    private readonly SignalMappingStorage _mappingStorage;
    private readonly IEventsSource<SignalProcessor> _eventsSource;
    private readonly IServiceProvider _serviceProvider;
    private readonly SignalListener _listener;

    public SignalProcessor(ThoriumConfiguration configuration, 
        SignalMappingStorage mappingStorage,
        IEventsSource<SignalProcessor> eventsSource,
        IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _mappingStorage = mappingStorage;
        _eventsSource = eventsSource;
        _serviceProvider = serviceProvider;
        _listener = new SignalListener(_configuration.Name, _eventsSource, _mappingStorage.GetRegisteredCodes(), _configuration.ReadThreads);
        //_listener = new SignalListener(_configuration.Name, _eventsSource, _mappingStorage.GetRegisteredCodes(), 1);
        _listener.OnSignal(OnSignal);
        _listener.Listen();
    }

    private void OnSignal(Signal signal, IMemoryReadHandle readHandle, IMemoryWriteHandle? writeHandle, CancellationToken cancellationToken)
    {
        var type = _mappingStorage.GetHandlerType(signal.Code, signal.Type);

        if (type == null)
        {
            throw new InvalidOperationException($"handler not found for {signal.Type} and code {signal.Code}");
        }
        
        if (_serviceProvider.GetService(type) is ISignalHandleWrapper instance)
        {
            instance.ProcessMessage(signal.Code,readHandle,writeHandle,cancellationToken);
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _eventsSource.Trace();
        return Task.CompletedTask;
    }
}