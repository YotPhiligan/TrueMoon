using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class Service2Resolver : IResolver<IService2, Service2>, IObjectResolver
{
    public IService2 Resolve(IServiceResolver context)
    {
        var subService2 = context.Resolve<SubService2>();
        return new Service2(subService2);
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    object IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}