using System.Buffers.Binary;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

public sealed class RenderSnapshotStampTests
{
    internal static RenderSnapshotStamp Stamp() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7, 17, 4, 2, 3, 5, new('A', 64));
    [Fact]
    public void FixedEncoding_RoundtripsEveryFieldAndUsesExactLittleEndianLayout()
    {
        var stamp = Stamp(); var bytes = stamp.Encode();
        Assert.Equal(132, bytes.Length); Assert.Equal(1U, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(stamp.ViewId, new Guid(bytes.AsSpan(4, 16))); Assert.Equal(stamp.WorldId, new Guid(bytes.AsSpan(20, 16)));
        Assert.Equal(7, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(52))); Assert.Equal(17UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(60)));
        Assert.Equal(4UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(68))); Assert.Equal(5UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(92)));
        Assert.All(bytes.AsSpan(100).ToArray(), value => Assert.Equal((byte)0xaa, value));
        Assert.Equal(stamp, RenderSnapshotStamp.Decode(bytes));
    }
    [Theory]
    [InlineData(0)] [InlineData(131)] [InlineData(133)]
    public void Decode_RejectsTruncationAndTrailingBytes(int length)
        => Assert.Throws<InvalidDataException>(() => RenderSnapshotStamp.Decode(new byte[length]));
    [Fact]
    public void Decode_RejectsUnknownVersionAndIncompleteIdentity()
    {
        var bytes = Stamp().Encode(); bytes[0] = 2;
        Assert.Throws<InvalidDataException>(() => RenderSnapshotStamp.Decode(bytes));
        Assert.Throws<InvalidDataException>(() => (Stamp() with { ViewId = Guid.Empty }).Encode());
        Assert.Throws<InvalidDataException>(() => (Stamp() with { Sequence = 0 }).Encode());
        Assert.Throws<InvalidDataException>(() => (Stamp() with { SourceRevision = -1 }).Encode());
        Assert.Throws<InvalidDataException>(() => (Stamp() with { ManifestHash = new('Z', 64) }).Encode());
    }
}
