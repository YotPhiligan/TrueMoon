namespace TrueMoon.Enerit.IO;

public interface IInvocationServerHandlerResolver
{
    IInvocationServerHandler<T> Resolve<T>();
}