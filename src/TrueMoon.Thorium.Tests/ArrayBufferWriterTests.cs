using System.Diagnostics;
using TrueMoon.Thorium.IO;

namespace TrueMoon.Thorium.Tests;

public readonly record struct TestStruct1(float X, int Y, bool Z = default);

public readonly record struct TestStruct2(float X, int Y, bool Z = default)
{
    public readonly float X = X;
    public readonly int Y = Y;
    public readonly bool Z = Z;
}

public class ArrayBufferWriterTests
{
    [Fact]
    public void Test1()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(12);

        SerializationUtils.WriteBytes(new byte[10], writer);
        SerializationUtils.Write<int>(4, writer);

        Assert.Equal(18, writer.WrittenCount);
        Assert.Equal(14, writer.FreeCapacity);
        Assert.Equal(32, writer.Capacity);
    }
    
    [Fact]
    public void Test2()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(12);

        SerializationUtils.WriteBytes(new byte[10], writer);
        SerializationUtils.WriteString("1234567890 1234567890 zxcvbnm,./asdfghjkl;'qwertyuiop[]", writer);
        SerializationUtils.Write<int>(4, writer);

        Assert.Equal(133, writer.WrittenCount);
        Assert.Equal(123, writer.FreeCapacity);
        Assert.Equal(256, writer.Capacity);
    }
    
    [Fact]
    public void Test3()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(12);

        SerializationUtils.WriteBytes(new byte[10], writer);
        SerializationUtils.Write<int>(4, writer);
        SerializationUtils.WriteString(null, writer);
        SerializationUtils.WriteString("123", writer);

        Span<byte> a = writer.WrittenMemory.ToArray();

        int offset = 0;
        var mem = SerializationUtils.ReadBytes(a[offset..], ref offset);
        var intVal = SerializationUtils.Read<int>(a[offset..], ref offset);
        var nulString = SerializationUtils.ReadString(a[offset..], ref offset);
        var strValue = SerializationUtils.ReadString(a[offset..], ref offset);
        
        Assert.Equal(10, mem.Length);
        Assert.Equal(4, intVal);
        Assert.Null(nulString);
        Assert.Equal("123", strValue);
    }
    
    [Fact]
    public void Test4()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(12);

        var t1 = new TestStruct1(0.2f, 42, true);
        
        SerializationUtils.Write(t1, writer);

        Span<byte> a = writer.WrittenMemory.ToArray();

        int offset = 0;
        var t1Result = SerializationUtils.Read<TestStruct1>(a[offset..], ref offset);
        
        Assert.Equal(t1.X, t1Result.X);
        Assert.Equal(t1.Y, t1Result.Y);
        Assert.Equal(t1.Z, t1Result.Z);
    }
    
    [Fact]
    public void Test5()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(12);

        var t1 = new TestStruct2(0.2f, 42, true);
        
        SerializationUtils.Write(t1, writer);

        Span<byte> a = writer.WrittenMemory.ToArray();

        int offset = 0;
        var t1Result = SerializationUtils.Read<TestStruct2>(a[offset..], ref offset);
        
        Assert.Equal(t1.X, t1Result.X);
        Assert.Equal(t1.Y, t1Result.Y);
        Assert.Equal(t1.Z, t1Result.Z);
    }
}

public class SerializationExtensionsTests
{
    [Fact]
    public void SerializeTestPoco()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(128);

        var testPoco = new TestPoco
        {
            Text = "test text",
            FloatValue = .032f,
            IntValue = 421,
            Memory = new byte[] {0,1,2,3,4,5} 
        };

        var sw = Stopwatch.StartNew();
        testPoco.Serialize(writer);
        
        sw.Stop();
        var ms = sw.ElapsedMilliseconds;
        
        Assert.Equal(42, writer.WrittenCount);
    }
    
    [Fact]
    public void DeserializeTestPoco()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(128);

        var testPoco = new TestPoco
        {
            Text = "test text",
            FloatValue = .032f,
            IntValue = 421,
            Memory = new byte[] {0,1,2,3,4,5} 
        };

        
        testPoco.Serialize(writer);

        var buffer = writer.WrittenSpan.ToArray();
        
        var offset = 0;
        TestPoco v = default;
        var sw = Stopwatch.StartNew();
        v = v.Deserialize(buffer, ref offset);
        sw.Stop();
        var ms = sw.ElapsedMilliseconds;

        Assert.NotNull(v);
        Assert.Equal(testPoco.Text, v.Text);
        Assert.Equal(testPoco.FloatValue, v.FloatValue);
        Assert.Equal(testPoco.IntValue, v.IntValue);
        Assert.Equal(testPoco.Memory.Length, v.Memory.Length);
    }
}