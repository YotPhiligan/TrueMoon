using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IResolvingContext : IServiceResolver
{
    T? TryResolve<T>();
}