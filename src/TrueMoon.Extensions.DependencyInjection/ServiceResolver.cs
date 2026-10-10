using TrueMoon.Services;

namespace TrueMoon.Extensions.DependencyInjection;

public class ServiceResolver : IServiceResolver, IDisposable, IAsyncDisposable
{
    private readonly Lock _lock = new();
    private IServiceProvider? _serviceProvider;
    private Task? _disposeTask;
    private readonly AsyncLocal<bool> _disposing = new();

    public ServiceResolver(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    // Builder registers the wrapper as a borrowed instance before attaching the owned provider.
    internal ServiceResolver() { }
    internal void Attach(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public object? GetService(Type serviceType)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposeTask != null, this);
            if (serviceType == typeof(IServiceResolver) || serviceType == typeof(IServiceProvider)) return this;
            return _serviceProvider!.GetService(serviceType);
        }
    }
    public T Resolve<T>() => GetService(typeof(T)) is T value ? value : throw new InvalidOperationException($"No service for type '{typeof(T)}' has been registered.");
    public T? TryResolve<T>() => GetService(typeof(T)) is T value ? value : default;
    public void Dispose() => DisposeCore(false).GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => new(DisposeCore(true));

    private Task DisposeCore(bool asynchronous)
    {
        if (_disposing.Value) return Task.CompletedTask;
        TaskCompletionSource completion;
        lock (_lock)
        {
            if (_disposeTask != null) return _disposeTask;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
        }
        _ = DisposeProviderAsync(asynchronous, completion);
        return completion.Task;
    }

    private async Task DisposeProviderAsync(bool asynchronous, TaskCompletionSource completion)
    {
        _disposing.Value = true;
        try
        {
            if (_serviceProvider is IAsyncDisposable asyncDisposable && (asynchronous || _serviceProvider is not IDisposable))
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (_serviceProvider is IDisposable disposable)
                disposable.Dispose();
            completion.TrySetResult();
        }
        catch (Exception exception) { completion.TrySetException(exception); }
        finally { _disposing.Value = false; }
    }
}
