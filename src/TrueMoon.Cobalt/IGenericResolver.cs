namespace TrueMoon.Cobalt;

public interface IGenericResolver
{
    object ResolveGeneric(Type[] genericArgument, IResolvingContext context);
}