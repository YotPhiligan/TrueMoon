namespace TrueMoon.Thorium.IO;

public interface ISignalsHandle
{
    string Name { get; }
    bool IsAlive { get; }
}

public interface ISignalsHandle<T> : ISignalsHandle
{
    
}