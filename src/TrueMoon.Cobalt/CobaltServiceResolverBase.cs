using System.Collections;
using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public abstract class CobaltServiceResolverBase : IServiceResolver, IDisposable, IAsyncDisposable
{
    private readonly DisposablesContainer _disposables = new ();
    private readonly FrozenDictionary<Type, ITypeContainer> _resolversContainers;

    private static readonly Type EnumerableType = typeof(IEnumerable);

    protected CobaltServiceResolverBase(FrozenDictionary<Type, ITypeContainer> resolversContainers)
    {
        _resolversContainers = resolversContainers;
    }

    public T Resolve<T>()
    {
        var r = ResolveCore<T>();
        
        if (r != null)
        {
            return r;
        }

        throw new ServiceResolvingException<T>();
    }

    private T? ResolveCore<T>()
    {
        var type = typeof(T);
        
        if (_resolversContainers.TryGetValue(type, out var container))
        {
            if (container.GetResolver() is IResolver<T> resolver)
            {
                var value = resolver.Resolve(this);
                if (value != null && resolver.IsServiceDisposable)
                {
                    _disposables.Add(value);
                }

                return value;
            }
        }
        
        if (EnumerableType.IsAssignableFrom(type))
        {
            var objects = ResolveEnumerable(type);

            return (T)(IEnumerable)objects;
        }
        
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            
            if (_resolversContainers.TryGetValue(genericTypeDefinition, out var c))
            {
                var resolver = c.GetResolver();
                if (resolver is IGenericResolver genericResolver)
                {
                    var value = genericResolver.Resolve(type.GenericTypeArguments,this);
                    
                    if (value != null && resolver.IsServiceDisposable)
                    {
                        _disposables.Add(value);
                    }

                    if (value != null)
                    {
                        return (T)value;
                    }
                    
                    return default;
                }
            }
        }

        if (type == typeof(IServiceResolver) || type == typeof(IServiceProvider))
        {
            return (T)(object)this;
        }

        return default;
    }

    public T? TryResolve<T>() => ResolveCore<T>();

    private object[] ResolveEnumerable(Type type)
    {
        var rcontainer = _resolversContainers.Values
            .FirstOrDefault(t => t.EnumerableType == type);
        
        if (rcontainer == null)
        {
            return [];
        }
        
        var resolvers = rcontainer.GetResolvers();
            
        var objects = new object[resolvers.Length];
        for (var i = 0; i < resolvers.Length; i++)
        {
            var resolver = resolvers[i];
            
            if (resolver is IObjectResolver objectResolver)
            {
                var item = objectResolver.Resolve(this);
                
                if (item != null && resolver.IsServiceDisposable)
                {
                    _disposables.Add(item);
                }
                
                if (item != null)
                {
                    objects[i] = item;
                }
            }
        }

        return objects.Where(t=>t != null).ToArray();
    }

    public object? GetService(Type type)
    {
        if (_resolversContainers.TryGetValue(type, out var container))
        {
            var resolver = container.GetResolver();
            if (resolver is IObjectResolver objectResolver)
            {
                var value = objectResolver.Resolve(this);
            
                if (value != null && resolver.IsServiceDisposable)
                {
                    _disposables.Add(value);
                }
            
                return value;
            }
        }
        
        if (EnumerableType.IsAssignableFrom(type))
        {
            var objects = ResolveEnumerable(type);

            return objects;
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            
            if (_resolversContainers.TryGetValue(genericTypeDefinition, out var c))
            {
                var resolver = c.GetResolver();
                if (resolver is IGenericResolver genericResolver)
                {
                    var value = genericResolver.Resolve(type.GenericTypeArguments,this);

                    if (resolver.IsServiceDisposable && value != null)
                    {
                        _disposables.Add(value);
                    }
                
                    return value;
                }
            }
        }
        
        if (type == typeof(IServiceResolver) || type == typeof(IServiceProvider))
        {
            return this;
        }
        
        throw new ServiceResolvingException(type);
    }
    
    public void Dispose() => _disposables.Dispose();

    public ValueTask DisposeAsync() => _disposables.DisposeAsync();
}