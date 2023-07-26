using System.Buffers;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public class TestPoco2Serializer : ISerializer<TestPoco2>
{
    public void Serialize(ref TestPoco2 instance, IBufferWriter<byte> bufferWriter)
    {
        instance.Serialize(bufferWriter);
    }

    public TestPoco2 Deserialize(ReadOnlySpan<byte> span)
    {
        var offset = 0;
        TestPoco2? result = default;
        result = result.Deserialize(span, ref offset);
        return result;
    }
}