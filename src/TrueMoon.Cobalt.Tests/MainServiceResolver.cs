namespace TrueMoon.Cobalt.Tests;

public class MainServiceResolver : IResolver<IMainService, MainService>
{
    public IMainService Resolve(IResolvingContext context)
    {
        var service1 = context.Resolve<IService1>();
        var service2 = context.Resolve<IService2>();
        return new MainService(service1, service2);
    }

    public bool IsServiceDisposable { get; } = false;
    public ResolvingServiceLifetime ServiceLifetime { get; }
    
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}