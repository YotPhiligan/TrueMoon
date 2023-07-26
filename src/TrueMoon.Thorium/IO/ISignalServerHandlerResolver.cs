namespace TrueMoon.Thorium.IO;

public interface ISignalServerHandlerResolver
{
    ISignalServerHandler<T> Resolve<T>();
}