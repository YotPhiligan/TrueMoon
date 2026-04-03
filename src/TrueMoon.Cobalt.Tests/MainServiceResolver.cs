using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class MainServiceResolver : IResolver<IMainService, MainService>, IObjectResolver
{
    public IMainService? Resolve(IServiceResolver resolver)
    {
        var service1 = resolver.Resolve<IService1>();
        var service2 = resolver.Resolve<IService2>();
        return new MainService(service1, service2);
    }

    public bool IsServiceDisposable { get; } = false;
    public ServiceLifetime ServiceLifetime { get; }
    object? IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}