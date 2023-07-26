namespace TrueMoon.Thorium.IO;

public interface ISignalServer
{
    string Id { get; }
}

public interface ISignalServer<TService> : ISignalServer
{

}