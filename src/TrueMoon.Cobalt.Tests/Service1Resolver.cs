using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class Service1Resolver : IResolver<IService1, Service1>, IObjectResolver
{
    public IService1 Resolve(IServiceResolver context)
    {
        var subService1 = context.Resolve<SubService1>();
        return new Service1(subService1);
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    
    object IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}

public interface IService5;
public class Service5 : IService5;

public class Service5Resolver : IResolver<IService5>, IObjectResolver
{
    private readonly Func<IServiceResolver, IService5> _factory;

    public Service5Resolver(Func<IServiceResolver,IService5> factory)
    {
        _factory = factory;
    }
    
    public IService5 Resolve(IServiceResolver context)
    {
        return _factory.Invoke(context);
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    object? IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}