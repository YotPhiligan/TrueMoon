namespace TrueMoon.Thorium.IO;

public interface IMemoryReadHandle
{
    ReadOnlySpan<byte> GetData();
}