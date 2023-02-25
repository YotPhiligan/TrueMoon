using TrueMoon.Dependencies;

namespace TrueMoon.Thorium;

public class SignalMediator : ISignalMediator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public SignalMediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public void Notify<TMessage>(TMessage message)
    {
        if (_isDisposed)
        {
            return;
        }
        
        var anyHandlers = false;
        var handlers = _serviceProvider.ResolveAll<ISignalHandler<TMessage>>();
        if (handlers is IReadOnlyCollection<ISignalHandler<TMessage>> collection && collection.Any())
        {
            anyHandlers = true;
            foreach (var handler in handlers)
            {
                handler.Process(message);
            }
        }
        
        var asyncHandlers = _serviceProvider.ResolveAll<IAsyncSignalHandler<TMessage>>();
        if (asyncHandlers is IReadOnlyCollection<IAsyncSignalHandler<TMessage>> col && col.Any())
        {
            anyHandlers = true;
            var tasks = asyncHandlers.Select(t => t.ProcessAsync(message, CancellationToken.None));
            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }
        
        if (!anyHandlers)
        {
            throw new InvalidOperationException($"handler not found for {typeof(TMessage)}");
        }
    }

    /// <inheritdoc />
    public TResponse Request<TMessage, TResponse>(TMessage message)
    {
        if (_isDisposed)
        {
            return default;
        }
        
        var handler = _serviceProvider.Resolve<ISignalHandler<TMessage,TResponse>>();
        if (handler != null)
        {
            return handler.Process(message);
        }
        
        var asyncHandler = _serviceProvider.Resolve<IAsyncSignalHandler<TMessage,TResponse>>();
        if (asyncHandler != null)
        {
            return asyncHandler.ProcessAsync(message, CancellationToken.None).GetAwaiter().GetResult();
        }
        
        throw new InvalidOperationException($"{typeof(ISignalHandler<TMessage,TResponse>)} handler not found");
    }

    /// <inheritdoc />
    public async Task NotifyAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isDisposed)
        {
            return;
        }
     
        var anyHandlers = false;
        var handlers = _serviceProvider.ResolveAll<ISignalHandler<TMessage>>();
        if (handlers is IReadOnlyCollection<ISignalHandler<TMessage>> collection && collection.Any())
        {
            anyHandlers = true;
            foreach (var handler in handlers)
            {
                handler.Process(message);
            }
        }
        
        var asyncHandlers = _serviceProvider.ResolveAll<IAsyncSignalHandler<TMessage>>();
        if (asyncHandlers is IReadOnlyCollection<IAsyncSignalHandler<TMessage>> col && col.Any())
        {
            anyHandlers = true;
            var tasks = asyncHandlers.Select(t => t.ProcessAsync(message, cancellationToken));
            await Task.WhenAll(tasks).WaitAsync(cancellationToken);
        }
        
        if (!anyHandlers)
        {
            throw new InvalidOperationException($"handler not found for {typeof(TMessage)}");
        }
    }

    public Task<TResponse> RequestAsync<TMessage, TResponse>(TMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_isDisposed)
        {
            return default;
        }
        
        var asyncHandler = _serviceProvider.Resolve<IAsyncSignalHandler<TMessage,TResponse>>();
        if (asyncHandler != null)
        {
            return asyncHandler.ProcessAsync(message, cancellationToken);
        }
        
        var handler = _serviceProvider.Resolve<ISignalHandler<TMessage,TResponse>>();
        if (handler != null)
        {
            var response = handler.Process(message);

            return Task.FromResult(response);
        }

        throw new InvalidOperationException($"{typeof(ISignalHandler<TMessage,TResponse>)} handler not found");
    }

    private bool _isDisposed;
    
    public void Dispose()
    {
        _isDisposed = true;
    }
}