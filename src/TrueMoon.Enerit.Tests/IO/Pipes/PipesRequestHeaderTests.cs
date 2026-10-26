using System.Buffers.Binary;
using System.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests.IO.Pipes;

public sealed class PipesRequestHeaderTests
{
    private static readonly Guid Id = new("00112233-4455-6677-8899-aabbccddeeff");
    private static ActivityContext Context(ActivityTraceFlags flags = ActivityTraceFlags.Recorded) => new(
        ActivityTraceId.CreateFromString("00112233445566778899aabbccddeeff"),
        ActivitySpanId.CreateFromString("0123456789abcdef"), flags, "private=not-on-wire");

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 1048576)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 1048576)]
    public void Header_RoundTripsPayloadBoundariesWithoutReadingPayload(bool traced, int length)
    {
        var bytes = Header(traced ? Context() : null, length);
        var expectedSize = traced ? 51 : 21;
        Assert.Equal(expectedSize, bytes.Length);
        Assert.Equal(Id.ToByteArray(), bytes[..16]); Assert.Equal((byte)255, bytes[16]);
        Assert.Equal(traced ? -2 : length, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(17)));
        using var stream = new MemoryStream(bytes.Concat(new byte[] { 91, 92 }).ToArray());
        var header = PipesRequestHeader.Read(stream);
        Assert.Equal(Id, header.Id); Assert.Equal((byte)255, header.Method); Assert.Equal(length, header.Length);
        Assert.Equal(expectedSize, stream.Position); Assert.Equal(91, stream.ReadByte()); Assert.False(header.InvalidContext);
        if (traced)
        {
            var context = header.Context!.Value;
            Assert.Equal(Context().TraceId, context.TraceId); Assert.Equal(Context().SpanId, context.SpanId);
            Assert.Equal(ActivityTraceFlags.Recorded, context.TraceFlags); Assert.True(context.IsRemote);
            Assert.Null(context.TraceState);
        }
        else Assert.Null(header.Context);
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(2, 0)] [InlineData(3, 1)] [InlineData(255, 1)]
    public void Writer_UsesExactVersionedOffsetsAndOnlyRecordedFlag(int rawFlags, byte expectedFlags)
    {
        var context = Context((ActivityTraceFlags)rawFlags);
        using var writer = new ArrayPoolBufferWriter<byte>();
        var lengthOffset = PipesRequestHeader.Write(writer, Id, 255, context);
        Assert.Equal(47, lengthOffset); Assert.Equal(51, writer.WrittenCount);
        var bytes = writer.WrittenMemory.ToArray();
        Assert.Equal(1, bytes[21]); Assert.Equal(expectedFlags, bytes[22]);
        var trace = new byte[16]; context.TraceId.CopyTo(trace);
        var span = new byte[8]; context.SpanId.CopyTo(span);
        Assert.Equal(trace, bytes[23..39]); Assert.Equal(span, bytes[39..47]);
        Assert.Equal(new byte[4], bytes[47..51]);
        using var input = new MemoryStream(bytes);
        Assert.Equal((ActivityTraceFlags)expectedFlags, PipesRequestHeader.Read(input).Context!.Value.TraceFlags);
    }

    [Theory]
    [InlineData("null")] [InlineData("empty")] [InlineData("trace")] [InlineData("span")]
    [InlineData("trace-bytes")] [InlineData("span-bytes")]
    public void Writer_InvalidOrAbsentContextPreservesLegacyFraming(string kind)
    {
        var valid = Context();
        ActivityContext? context = kind switch
        {
            "null" => null, "empty" => default(ActivityContext),
            "trace" => new(default, valid.SpanId, ActivityTraceFlags.Recorded),
            "trace-bytes" => new(ActivityTraceId.CreateFromBytes(new byte[16]), valid.SpanId, ActivityTraceFlags.Recorded),
            "span-bytes" => new(valid.TraceId, ActivitySpanId.CreateFromBytes(new byte[8]), ActivityTraceFlags.Recorded),
            _ => new(valid.TraceId, default, ActivityTraceFlags.Recorded)
        };
        using var writer = new ArrayPoolBufferWriter<byte>();
        Assert.Equal(17, PipesRequestHeader.Write(writer, Guid.Empty, 0, context));
        Assert.Equal(21, writer.WrittenCount);
        using var stream = new MemoryStream(writer.WrittenMemory.ToArray());
        var header = PipesRequestHeader.Read(stream);
        Assert.Equal(Guid.Empty, header.Id); Assert.Equal(0, header.Method); Assert.Equal(0, header.Length);
        Assert.Null(header.Context); Assert.False(header.InvalidContext);
    }

    [Theory]
    [InlineData("trace")] [InlineData("span")] [InlineData("flags2")] [InlineData("flags255")]
    public void Reader_WellFramedInvalidContextDiscardsOnlyMetadata(string invalid)
    {
        var bytes = Header(Context(), 3);
        if (invalid == "trace") bytes.AsSpan(23, 16).Clear();
        if (invalid == "span") bytes.AsSpan(39, 8).Clear();
        if (invalid == "flags2") bytes[22] = 2;
        if (invalid == "flags255") bytes[22] = 255;
        using var stream = new MemoryStream(bytes.Concat(new byte[] { 7, 8, 9 }).ToArray());
        var header = PipesRequestHeader.Read(stream);
        Assert.True(header.InvalidContext); Assert.Null(header.Context);
        Assert.Equal(Id, header.Id); Assert.Equal(255, header.Method); Assert.Equal(3, header.Length);
        Assert.Equal(51, stream.Position); Assert.Equal(7, stream.ReadByte());
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(255)]
    public void Reader_RejectsUnsupportedVersion(byte version)
    {
        var bytes = Header(Context(), 0); bytes[21] = version;
        using var stream = new MemoryStream(bytes);
        Assert.Throws<InvalidDataException>(() => PipesRequestHeader.Read(stream));
    }

    [Theory]
    [InlineData(false, -1)] [InlineData(false, int.MinValue)] [InlineData(false, 1048577)] [InlineData(false, int.MaxValue)]
    [InlineData(true, -1)] [InlineData(true, -2)] [InlineData(true, int.MinValue)] [InlineData(true, 1048577)] [InlineData(true, int.MaxValue)]
    public void Reader_RejectsInvalidPayloadLengthInBothFormats(bool traced, int length)
    {
        using var stream = new MemoryStream(Header(traced ? Context() : null, length));
        Assert.Throws<InvalidDataException>(() => PipesRequestHeader.Read(stream));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(16)] [InlineData(17)] [InlineData(20)]
    [InlineData(21)] [InlineData(22)] [InlineData(23)] [InlineData(38)] [InlineData(39)] [InlineData(46)] [InlineData(47)] [InlineData(50)]
    public void Reader_RejectsTruncationAcrossEveryHeaderField(int bytesAvailable)
    {
        using var stream = new MemoryStream(Header(Context(), 0)[..bytesAvailable]);
        Assert.Throws<EndOfStreamException>(() => PipesRequestHeader.Read(stream));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Reader_HandlesShortStreamReadsAndLeavesFollowingHeaderIntact(bool traced)
    {
        var first = Header(traced ? Context() : null, 2);
        var second = Header(null, 0);
        using var stream = new OneByteReadStream(first.Concat(new byte[] { 41, 42 }).Concat(second).ToArray());
        var parsed = PipesRequestHeader.Read(stream);
        Assert.Equal(2, parsed.Length); Assert.Equal(41, stream.ReadByte()); Assert.Equal(42, stream.ReadByte());
        var next = PipesRequestHeader.Read(stream);
        Assert.Equal(Id, next.Id); Assert.Null(next.Context); Assert.Equal(0, next.Length);
        Assert.Equal(stream.Length, stream.Position);
    }

    private static byte[] Header(ActivityContext? context, int length)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        var offset = PipesRequestHeader.Write(writer, Id, 255, context);
        BinaryPrimitives.WriteInt32LittleEndian(writer.WrittenSpanWritable.Slice(offset, 4), length);
        return writer.WrittenMemory.ToArray();
    }
    private sealed class OneByteReadStream(byte[] bytes) : MemoryStream(bytes)
    { public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]); }
}
