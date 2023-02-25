namespace TrueMoon.Thorium.IO;

public interface IMemoryWriteHandle
{
    void Write(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
}