namespace TrueMoon.Thorium.IO;

public interface ISignalServerFactory
{
    ISignalServer<T> Create<T>();
}