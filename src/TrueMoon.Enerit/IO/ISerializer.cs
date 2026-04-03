using System.Buffers;

namespace TrueMoon.Enerit.IO;

public interface ISerializer<T>
{
    void Serialize(ref T instance, IBufferWriter<byte> bufferWriter);
    T? Deserialize(ReadOnlySpan<byte> span);
}