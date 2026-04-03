namespace TrueMoon.Enerit.IO;

public interface IInvocationServer
{
    string Id { get; }
}

public interface IInvocationServer<TService> : IInvocationServer, IStartable;