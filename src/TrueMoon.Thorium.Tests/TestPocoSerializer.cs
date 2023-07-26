using System.Buffers;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public class TestPocoSerializer : ISerializer<TestPoco>
{
    public void Serialize(ref TestPoco instance, IBufferWriter<byte> bufferWriter)
    {
        instance.Serialize(bufferWriter);
    }

    public TestPoco Deserialize(ReadOnlySpan<byte> span)
    {
        var offset = 0;
        TestPoco? result = default;
        result = result.Deserialize(span, ref offset);
        return result;
    }
}