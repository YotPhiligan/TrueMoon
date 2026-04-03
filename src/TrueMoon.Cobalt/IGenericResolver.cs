using TrueMoon.Services;

namespace TrueMoon.Cobalt;

public interface IGenericResolver : IResolver
{
    object? Resolve(Type[] genericArgument, IServiceResolver context);
}