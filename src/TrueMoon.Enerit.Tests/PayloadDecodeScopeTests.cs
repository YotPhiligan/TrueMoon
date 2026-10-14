using System.Buffers;
using TrueMoon.Enerit.IO;

namespace TrueMoon.Enerit.Tests;

public class PayloadDecodeScopeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void UncommittedDispose_ReturnsAllWholeRentalsExactlyOnce(int length)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteBytes(new byte[length], writer);
        SerializationUtils.WriteBytes(new byte[] { 19, 255 }, writer);
        var scope = new PayloadDecodeScope();
        var offset = 0;
        var first = scope.ReadBytes(writer.WrittenSpan, ref offset);
        var second = scope.ReadBytes(writer.WrittenSpan[offset..], ref offset);
        Assert.Equal(writer.WrittenCount, offset);
        Assert.Equal(new byte[] { 19, 255 }, second.ToArray());
        scope.Dispose();
        scope.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = first.Span.Length);
        Assert.Throws<ObjectDisposedException>(() => _ = second.Span.Length);
        Assert.False(first.TryReturn());
        Assert.False(second.TryReturn());
        Assert.Throws<ObjectDisposedException>(() => scope.ReadBytes(writer.WrittenSpan, ref offset));
        Assert.Throws<ObjectDisposedException>(scope.Commit);
    }

    [Fact]
    public void Commit_PreservesCallerOwnershipAndRejectsFurtherReads()
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteBytes(new byte[] { 7, 11, 23 }, writer);
        var scope = new PayloadDecodeScope();
        var offset = 0;
        var memory = scope.ReadBytes(writer.WrittenSpan, ref offset);
        try
        {
            scope.Commit();
            scope.Dispose();
            Assert.Equal(new byte[] { 7, 11, 23 }, memory.ToArray());
            Assert.Throws<ObjectDisposedException>(() => scope.ReadBytes(writer.WrittenSpan, ref offset));
            Assert.Throws<ObjectDisposedException>(scope.Commit);
            Assert.True(memory.TryReturn());
            Assert.False(memory.TryReturn());
        }
        finally { scope.Dispose(); memory.TryReturn(); }
    }

    [Fact]
    public void LaterTruncation_ReturnsPreviouslyReadRental()
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.WriteBytes(new byte[] { 3, 5 }, writer);
        SerializationUtils.Write(4, writer);
        writer.Write(new byte[] { 1 });
        var offset = 0;
        Memory<byte> first;
        using (var scope = new PayloadDecodeScope())
        {
            first = scope.ReadBytes(writer.WrittenSpan, ref offset);
            Assert.Equal(6, offset);
            Assert.Throws<InvalidDataException>(() => scope.ReadBytes(writer.WrittenSpan[offset..], ref offset));
            Assert.Equal(6, offset);
        }
        Assert.Throws<ObjectDisposedException>(() => _ = first.Span.Length);
        Assert.False(first.TryReturn());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    public void ConsumedPayload_ExactBoundariesAreAccepted(int offset, int length)
        => Assert.Null(Record.Exception(() => SerializationUtils.EnsureFullyConsumed(offset, length)));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    [InlineData(5, 4)]
    [InlineData(-1, -1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void ConsumedPayload_InvalidCountsAreRejected(int offset, int length)
        => Assert.Throws<InvalidDataException>(() => SerializationUtils.EnsureFullyConsumed(offset, length));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Boolean_CanonicalValuesConsumeOneByte(byte value, bool expected)
    {
        var offset = 0;
        Assert.Equal(expected, SerializationUtils.Read<bool>(new byte[] { value }, ref offset));
        Assert.Equal(1, offset);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void Boolean_InvalidValueDoesNotAdvance(byte value)
    {
        var offset = 0;
        Assert.Throws<InvalidDataException>(() => SerializationUtils.Read<bool>(new byte[] { value }, ref offset));
        Assert.Equal(0, offset);
    }
}
