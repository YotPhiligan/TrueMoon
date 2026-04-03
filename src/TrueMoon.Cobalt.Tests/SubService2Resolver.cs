using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class SubService2Resolver : IResolver<SubService2, SubService2>, IObjectResolver
{
    public SubService2 Resolve(IServiceResolver context)
    {
        return new SubService2();
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    object IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}