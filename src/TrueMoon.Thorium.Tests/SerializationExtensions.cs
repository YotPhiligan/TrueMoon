using System.Buffers;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public static class SerializationExtensions
{
    public static void Serialize(this TestPoco? instance, IBufferWriter<byte> bufferWriter)
    {
        if (instance != null)
        {
            SerializationUtils.WriteInstanceState(true, bufferWriter);
            SerializationUtils.Write(instance.IntValue, bufferWriter);
            SerializationUtils.WriteString(instance.Text, bufferWriter);
            SerializationUtils.Write(instance.FloatValue, bufferWriter);
            SerializationUtils.WriteBytes(instance.Memory, bufferWriter);
        }
        else
        {
            SerializationUtils.WriteInstanceState(false, bufferWriter);
        }
    }
    
    public static void Serialize(this TestPoco2? instance, IBufferWriter<byte> bufferWriter)
    {
        if (instance != null)
        {
            SerializationUtils.WriteInstanceState(true, bufferWriter);
            
            SerializationUtils.Write(instance.IntValue2, bufferWriter);
            SerializationUtils.WriteString(instance.Text2, bufferWriter);
            instance.Poco.Serialize(bufferWriter);
            SerializationUtils.Write(instance.M, bufferWriter);
        }
        else
        {
            SerializationUtils.WriteInstanceState(false, bufferWriter);
        }
    }
    
    public static TestPoco? Deserialize(this TestPoco? instance, ReadOnlySpan<byte> span, ref int offset)
    {
        TestPoco? result = default;
        if (SerializationUtils.ReadInstanceState(span[offset..], ref offset))
        {
            result = instance ?? new TestPoco();
            result.IntValue = SerializationUtils.Read<int>(span[offset..], ref offset);
            result.Text = SerializationUtils.ReadString(span[offset..], ref offset);
            result.FloatValue = SerializationUtils.Read<float>(span[offset..], ref offset);
            result.Memory = SerializationUtils.ReadBytes(span[offset..], ref offset);
        }
        return result;
    }
    
    public static TestPoco2? Deserialize(this TestPoco2? instance, ReadOnlySpan<byte> span, ref int offset)
    {
        TestPoco2? result = default;
        if (SerializationUtils.ReadInstanceState(span[offset..], ref offset))
        {
            result = instance ?? new TestPoco2();
            result.IntValue2 = SerializationUtils.Read<int>(span[offset..], ref offset);
            result.Text2 = SerializationUtils.ReadString(span[offset..], ref offset);
            result.Poco = result.Poco.Deserialize(span, ref offset);
            result.M = SerializationUtils.Read<float>(span[offset..], ref offset);
        }
        return result;
    }
}