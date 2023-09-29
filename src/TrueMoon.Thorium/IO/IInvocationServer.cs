namespace TrueMoon.Thorium.IO;

public interface IInvocationServer
{
    string Id { get; }
}

public interface IInvocationServer<TService> : IInvocationServer
{

}