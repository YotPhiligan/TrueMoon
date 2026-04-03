using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IResolver
{
    bool IsServiceDisposable { get; }
    ServiceLifetime ServiceLifetime { get; }
}

public interface IResolver<TService> : IResolver
{
    TService? Resolve(IServiceResolver resolver);
}

public interface IResolver<TService, TImplementation> : IResolver<TService>;