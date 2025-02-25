namespace TrueMoon.Cobalt.Tests;

public class Service2Resolver : IResolver<IService2, Service2>
{
    public IService2 Resolve(IResolvingContext context)
    {
        var subService2 = context.Resolve<SubService2>();
        return new Service2(subService2);
    }

    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}