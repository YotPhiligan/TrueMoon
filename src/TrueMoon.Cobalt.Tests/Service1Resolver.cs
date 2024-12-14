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
}