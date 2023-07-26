namespace TrueMoon.Thorium.IO;

public interface ISignalClientFactory
{
    ISignalClient<T> Create<T>();
}