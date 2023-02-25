namespace TrueMoon.Thorium.Handlers;

public class DelegateSignalHandler<TMessage> : ISignalHandler<TMessage>
{
    private readonly Action<TMessage>? _dataCallback;
    private readonly Action<TMessage,IServiceProvider>? _dataCallback2;
    private readonly Func<IServiceProvider>? _serviceProviderDelegate;

    public DelegateSignalHandler(Action<TMessage> dataCallback)
    {
        _dataCallback = dataCallback;
    }

    public DelegateSignalHandler(Action<TMessage, IServiceProvider> dataCallback, Func<IServiceProvider> serviceProviderDelegate)
    {
        _dataCallback2 = dataCallback;
        _serviceProviderDelegate = serviceProviderDelegate;
    }

    public void Process(TMessage message)
    {
        if (_dataCallback != null)
        {
            _dataCallback(message);
        }
        else if(_dataCallback2 != null)
        {
            var serviceProvider = _serviceProviderDelegate?.Invoke();
            _dataCallback2(message, serviceProvider);
        }
        else
        {
            throw new InvalidOperationException("failed to invoke callback");
        }
    }
}

public class DelegateSignalHandler<TMessage,TResponse> : ISignalHandler<TMessage,TResponse>
{
    private readonly Func<TMessage,TResponse>? _dataCallback;
    private readonly Func<TMessage,IServiceProvider,TResponse>? _dataCallback1;
    private readonly Func<IServiceProvider>? _serviceProviderDelegate;

    public DelegateSignalHandler(Func<TMessage,TResponse> dataCallback)
    {
        _dataCallback = dataCallback;
    }

    public DelegateSignalHandler(Func<TMessage,IServiceProvider,TResponse> dataCallback, Func<IServiceProvider> serviceProviderDelegate)
    {
        _dataCallback1 = dataCallback;
        _serviceProviderDelegate = serviceProviderDelegate;
    }

    public TResponse Process(TMessage message)
    {
        if (_dataCallback != null)
        {
            return _dataCallback(message);
        }

        if (_dataCallback1 == null) throw new InvalidOperationException("failed to invoke callback");
        
        var serviceProvider = _serviceProviderDelegate?.Invoke();
        return _dataCallback1(message, serviceProvider);
    }
}