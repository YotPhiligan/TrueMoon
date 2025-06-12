using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class Service1Resolver : IResolver<IService1, Service1>
{
    public IService1 Resolve(IResolvingContext context)
    {
        var subService1 = context.Resolve<SubService1>();
        return new Service1(subService1);
    }

    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}

public interface IService5;
public class Service5;

public class Service5Resolver : IResolver<IService5>
{
    private readonly Func<IServiceResolver, IService5> _factory;

    public Service5Resolver(Func<IServiceResolver,IService5> factory)
    {
        _factory = factory;
    }
    
    public IService5 Resolve(IResolvingContext context)
    {
        return _factory.Invoke(context);
    }

    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}