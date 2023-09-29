namespace TrueMoon.Thorium.IO;

public interface IInvocationClientFactory
{
    IInvocationClient<T> Create<T>();
}