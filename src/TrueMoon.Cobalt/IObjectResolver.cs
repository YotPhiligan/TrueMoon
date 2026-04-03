using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IObjectResolver : IResolver
{
    object? Resolve(IServiceResolver context);
}