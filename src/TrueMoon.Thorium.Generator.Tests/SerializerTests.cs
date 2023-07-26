using System.Diagnostics;
using TrueMoon.Thorium.IO;
using TrueMoon.Thorium.IO.Pipes;

namespace TrueMoon.Thorium.Generator.Tests;

public class SerializerTests
{
    [Fact]
    public void Serialize()
    {
        var poco = new TestPoco
        {
            Text = "Test text",
            FloatValue = 0.42f,
            IntValue = 42,
            Memory = new byte[] {0,1,2,3}
        };
        var serializer = new TestPocoSerializer();

        var bufferWriter = new ArrayPoolBufferWriter<byte>(128);

        var sw = Stopwatch.StartNew();
        serializer.Serialize(ref poco, bufferWriter);
        sw.Stop();
        var ms0 = sw.Elapsed;

        
        var data = bufferWriter.WrittenSpan.ToArray();
        
        sw.Restart();
        var output = serializer.Deserialize(data);
        sw.Stop();
        var ms1 = sw.Elapsed;

    }
    
    [Fact]
    public void Serialize2()
    {
        var poco = new TestPoco
        {
            Text = "Test text",
            FloatValue = 0.42f,
            IntValue = 42,
            Memory = new byte[] {0,1,2,3}
        };

        var poco2 = new TestPoco2
        {
            Text2 = "Test text 2",
            M = .123f,
            Poco = poco,
            IntValue2 = 22
        };
        
        var serializer = new TestPoco2Serializer();

        var bufferWriter = new ArrayPoolBufferWriter<byte>(128);

        var sw = Stopwatch.StartNew();
        serializer.Serialize(ref poco2, bufferWriter);
        sw.Stop();
        var ms0 = sw.Elapsed;

        
        var data = bufferWriter.WrittenSpan.ToArray();
        
        sw.Restart();
        var output = serializer.Deserialize(data);
        sw.Stop();
        var ms1 = sw.Elapsed;

    }
    
    [Fact]
    public void Serialize2_null()
    {
        var poco2 = new TestPoco2
        {
            Text2 = "Test text 2",
            M = .123f,
            Poco = null,
            IntValue2 = 22
        };
        
        var serializer = new TestPoco2Serializer();

        var bufferWriter = new ArrayPoolBufferWriter<byte>(128);

        var sw = Stopwatch.StartNew();
        serializer.Serialize(ref poco2, bufferWriter);
        sw.Stop();
        var ms0 = sw.Elapsed;

        
        var data = bufferWriter.WrittenSpan.ToArray();
        
        sw.Restart();
        var output = serializer.Deserialize(data);
        sw.Stop();
        var ms1 = sw.Elapsed;

    }
}