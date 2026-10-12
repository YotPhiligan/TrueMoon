using System.Buffers;
using System.Runtime.InteropServices;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests;

public class SerializationContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Привет 🌙\0𝄞")]
    public void String_NullEmptyAndUnicode_RoundTripAndConsumeExactly(string? value)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteString(value, writer);
        SerializationUtils.Write(42, writer);
        var offset = 0;
        Assert.Equal(value, SerializationUtils.ReadString(writer.WrittenSpan, ref offset));
        Assert.Equal(value is null ? 1 : 5 + System.Text.Encoding.Unicode.GetByteCount(value), offset);
        Assert.Equal(42, SerializationUtils.Read<int>(writer.WrittenSpan[offset..], ref offset));
        Assert.Equal(writer.WrittenCount, offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65536)]
    public void CollectionCount_ValidBoundaries_ConsumeOnlyHeader(int count)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteElementsCount(count, writer);
        writer.Write(new byte[count]);
        var offset = 0;
        Assert.Equal(count, SerializationUtils.ReadElementsCount(writer.WrittenSpan, ref offset));
        Assert.Equal(4, offset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65537)]
    [InlineData(int.MaxValue)]
    public void CollectionCount_Invalid_IsRejectedWithoutAdvancing(int count)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        Assert.Throws<InvalidDataException>(() => SerializationUtils.WriteElementsCount(count, writer));
        Assert.Equal(0, writer.WrittenCount);
        SerializationUtils.Write(count, writer);
        var offset = 0;
        Assert.Throws<InvalidDataException>(() => SerializationUtils.ReadElementsCount(writer.WrittenSpan, ref offset));
        Assert.Equal(0, offset);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 2 })]
    [InlineData(new byte[] { 255 })]
    public void InvalidPresenceState_IsRejectedWithoutAdvancing(byte[] data)
    {
        var offset = 0;
        Assert.Throws<InvalidDataException>(() => SerializationUtils.ReadInstanceState(data, ref offset));
        Assert.Throws<InvalidDataException>(() => SerializationUtils.ReadString(data, ref offset));
        Assert.Equal(0, offset);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 2)]
    [InlineData(int.MaxValue, 0)]
    public void MalformedStringLength_IsRejectedBeforeDecoding(int length, int available)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteInstanceState(true, writer);
        SerializationUtils.Write(length, writer);
        writer.Write(new byte[available]);
        var offset = 0;
        Assert.Throws<InvalidDataException>(() => SerializationUtils.ReadString(writer.WrittenSpan, ref offset));
        Assert.Equal(0, offset);
    }

    [Fact]
    public void TruncatedPrimitivesAndCollection_DoNotAdvance()
    {
        var offset = 0;
        Assert.Throws<InvalidDataException>(() => SerializationUtils.Read<long>(new byte[7], ref offset));
        var data = BitConverter.GetBytes(2);
        Assert.Throws<InvalidDataException>(() => SerializationUtils.ReadElementsCount(data, ref offset));
        Assert.Equal(0, offset);
    }

    [Fact]
    public void RawManagedReferences_AreRejectedBeforeWriting()
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        Assert.Throws<NotSupportedException>(() => SerializationUtils.Write("text", writer));
        Assert.Throws<NotSupportedException>(() => SerializationUtils.Write(new ReferenceValue { Text = "text" }, writer));
        Assert.Equal(0, writer.WrittenCount);
        var guid = Guid.Parse("b957be23-844a-4a5e-bbf9-b3bd6fd85b3f");
        SerializationUtils.Write(guid, writer);
        var offset = 0;
        Assert.Equal(guid, SerializationUtils.Read<Guid>(writer.WrittenSpan, ref offset));
        Assert.Equal(Marshal.SizeOf<Guid>(), offset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1048577)]
    [InlineData(int.MaxValue)]
    public void PipesFrameLength_Invalid_IsRejected(int length)
        => Assert.Throws<InvalidDataException>(() => PipesProtocol.ValidatePayloadLength(length));

    private struct ReferenceValue { public string Text; }
}
