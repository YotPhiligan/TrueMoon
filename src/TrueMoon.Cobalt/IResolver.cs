namespace TrueMoon.Cobalt;

public interface IResolver
{
    bool IsServiceDisposable { get; }
    ResolvingServiceLifetime ServiceLifetime { get; }
    
    object Resolve(IResolvingContext context);
}

public interface IResolver<TService> : IResolver
{
    TService Resolve(IResolvingContext context);
}

public interface IResolver<TService, TImplementation> : IResolver<TService>;