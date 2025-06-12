namespace TrueMoon.Cobalt;

public interface IResolver : IResolverBase
{
    object Resolve(IResolvingContext context);
}

public interface IResolver<TService> : IResolver
{
    TService Resolve(IResolvingContext context);
}

public interface IResolver<TService, TImplementation> : IResolver<TService>;