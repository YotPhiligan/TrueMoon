namespace TrueMoon.Cobalt;

public interface IUnboundGenericResolver : IResolverBase
{
    object ResolveGeneric(Type[] genericArgument, IResolvingContext context);
}