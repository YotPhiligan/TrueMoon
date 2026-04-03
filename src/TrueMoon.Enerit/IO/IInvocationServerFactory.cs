namespace TrueMoon.Enerit.IO;

public interface IInvocationServerFactory
{
    IInvocationServer<T> Create<T>();
}