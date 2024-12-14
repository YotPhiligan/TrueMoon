using System.Collections.Frozen;

namespace TrueMoon.Cobalt;

public class ResolvingContext : IResolvingContext, IDisposable, IAsyncDisposable
{
    private readonly FrozenDictionary<Type,IResolver> _resolvers;
    private readonly List<object> _disposables = [];
    private readonly Lock _lock = new ();

    public ResolvingContext()
    {
        _resolvers = ServiceResolvers.Shared.GetResolvers();
    }
    
    public T Resolve<T>()
    {
        if (_resolvers.TryGetValue(typeof(T), out var resolver) 
            && resolver is IResolver<T> resolverTyped)
        {
            var value = resolverTyped.Resolve(this);
            
            if (resolver.IsServiceDisposable && value != null)
            {
                AddDisposable(value);
            }
            
            return value;
        }
        
        throw new ServiceResolvingException<T>();
    }

    private void AddDisposable<T>(T value)
    {
        lock (_lock)
        {
            _disposables.Add(value!);
        }
    }

    public void Dispose()
    {
        if (_disposables.Count == 0)
        {
            return;
        }

        foreach (var item in _disposables)
        {
            switch (item)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case IAsyncDisposable asyncDisposable:
                    asyncDisposable
                        .DisposeAsync()
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposables.Count == 0)
        {
            return;
        }
        
        foreach (var item in _disposables)
        {
            switch (item)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}