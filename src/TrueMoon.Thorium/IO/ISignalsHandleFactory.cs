namespace TrueMoon.Thorium.IO;

public interface ISignalsHandleFactory
{
    ISignalsHandle<T> Create<T>();
}