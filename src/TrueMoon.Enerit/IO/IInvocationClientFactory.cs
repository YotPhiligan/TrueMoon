namespace TrueMoon.Enerit.IO;

public interface IInvocationClientFactory
{
    IInvocationClient<T> Create<T>();
}