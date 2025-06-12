using System.Collections;
using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public class CobaltResolvingContext(FrozenDictionary<Type, IResolversContainerBase> resolversContainers, 
    IServicesRegistrationAccessor registrationAccessor, 
    DisposablesContainer disposables) 
    : IResolvingContext
{
    private readonly Type _enumerableType = typeof(IEnumerable);

    private Type? _targetResolvingType;
    
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
        
        _targetResolvingType ??= type;
        
        if (resolversContainers.TryGetValue(type, out var container) 
            && container is ITypedResolversContainer<T> typedContainer)
        {
            IResolver<T> resolverTyped = typedContainer.GetLastResolver(registrationAccessor);
            var value = resolverTyped.Resolve(this);
            
            if (resolverTyped.IsServiceDisposable && value != null)
            {
                disposables.Add(value);
            }

            return value;
        }
        
        if (_enumerableType.IsAssignableFrom(type))
        {
            var objects = ResolveEnumerable(type);

            return (T)(IEnumerable)objects;
        }
        
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            
            if (resolversContainers.TryGetValue(genericTypeDefinition, out var c) 
                && c is IUnboundResolversContainer unboundContainer)
            {
                var resolver = unboundContainer.GetLastResolver(registrationAccessor);
                var value = resolver.ResolveGeneric(type.GenericTypeArguments,this);

                if (resolver.IsServiceDisposable && value != null)
                {
                    disposables.Add(value);
                }

                return (T)value;
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
        var rcontainer = resolversContainers.Values
            .Where(t=>t is ITypedResolversContainer)
            .Cast<ITypedResolversContainer>()
            .FirstOrDefault(t => t.EnumerableType == type);
        
        if (rcontainer == null)
        {
            return [];
        }
        
        var resolvers = rcontainer.GetResolvers(registrationAccessor);
            
        //IEnumerable array = resolvers.Select(t => t.Resolve(this)).ToArray();
        //return (T)array;
            
        var objects = new object[resolvers.Length];
        for (int i = 0; i < resolvers.Length; i++)
        {
            var resolver = resolvers[i];
                
            var item = resolver.Resolve(this);
                
            if (resolver.IsServiceDisposable && item != null)
            {
                disposables.Add(item);
            }
                
            objects[i] = item; 
        }

        return objects;
    }

    public object? GetService(Type type)
    {
        _targetResolvingType ??= type;
        
        if (_enumerableType.IsAssignableFrom(type))
        {
            return ResolveEnumerable(type);
        }
        
        if (resolversContainers.TryGetValue(type, out var container) 
            && container is ITypedResolversContainer typedContainer)
        {
            IResolver resolverTyped = typedContainer.GetLastResolver(registrationAccessor);
            var value = resolverTyped.Resolve(this);
            
            if (resolverTyped.IsServiceDisposable && value != null)
            {
                disposables.Add(value);
            }
            
            return value;
        }
        
        if (_enumerableType.IsAssignableFrom(type))
        {
            var objects = ResolveEnumerable(type);

            return objects;
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            
            if (resolversContainers.TryGetValue(genericTypeDefinition, out var c) 
                && c is IUnboundResolversContainer unboundContainer)
            {
                var resolver = unboundContainer.GetLastResolver(registrationAccessor);
                var value = resolver.ResolveGeneric(type.GenericTypeArguments,this);

                if (resolver.IsServiceDisposable && value != null)
                {
                    disposables.Add(value);
                }
                
                return value;
            }
        }
        
        if (type == typeof(IServiceResolver) || type == typeof(IServiceProvider))
        {
            return this;
        }
        
        throw new ServiceResolvingException(type);
    }
}