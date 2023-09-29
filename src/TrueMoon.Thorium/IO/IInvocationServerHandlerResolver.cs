namespace TrueMoon.Thorium.IO;

public interface IInvocationServerHandlerResolver
{
    IInvocationServerHandler<T> Resolve<T>();
}