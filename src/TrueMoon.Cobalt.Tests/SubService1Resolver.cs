namespace TrueMoon.Cobalt.Tests;

public class SubService1Resolver : IResolver<SubService1, SubService1>
{
    public SubService1 Resolve(IResolvingContext context)
    {
        return new SubService1();
    }

    public bool IsServiceDisposable { get; }
    public ResolvingServiceLifetime ServiceLifetime { get; }
    object IResolver.Resolve(IResolvingContext context) => Resolve(context);
}