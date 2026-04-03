using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class SubService1Resolver : IResolver<SubService1, SubService1>, IObjectResolver
{
    public SubService1 Resolve(IServiceResolver context)
    {
        return new SubService1();
    }

    public bool IsServiceDisposable { get; }
    public ServiceLifetime ServiceLifetime { get; }
    object IObjectResolver.Resolve(IServiceResolver context) => Resolve(context);
}