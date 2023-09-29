namespace TrueMoon.Thorium.IO;

public interface IInvocationServerFactory
{
    IInvocationServer<T> Create<T>();
}