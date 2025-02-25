namespace TrueMoon.Cobalt.Tests;

public class SubService2Resolver : IResolver<SubService2, SubService2>
{
    public SubService2 Resolve(IResolvingContext context)
    {
        return new SubService2();
    }

    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}