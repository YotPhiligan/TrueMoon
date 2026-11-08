using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewViewCodecTests
{
    internal static PreviewViewState View(ulong revision = 1) => new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7),
        new(Matrix4x4.CreateFromYawPitchRoll(.6f, -.2f, .1f) * Matrix4x4.CreateTranslation(2, 3, 8), new(MathF.PI / 2, 1, 11), new(1, 2, 3), 8, revision));

    [Fact]
    public void Codec_UsesExplicitOffsetsAndExactUnsignedRevisionWithoutAliasing()
    {
        var view = View(ulong.MaxValue); var bytes = PreviewViewCodec.Encode(view);
        Assert.Equal(256, bytes.Length); Assert.Equal("TMPVIEW1"u8.ToArray(), bytes[..8]);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(256, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Equal(7, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(148)));
        Assert.Equal(ulong.MaxValue, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(156)));
        Assert.Equal(view.Camera.World.M11, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(164)));
        Assert.Equal(2f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(212)));
        Assert.Equal(8f, BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(252)));
        var decoded = PreviewViewCodec.Decode(bytes); Assert.Equal(view, decoded);
        Array.Clear(bytes); Assert.Equal(view, decoded); Assert.Equal(view.Camera.World, decoded.Camera.World);
    }

    [Theory]
    [InlineData("short")] [InlineData("long")] [InlineData("magic")] [InlineData("version")] [InlineData("size")]
    [InlineData("stamp-version")] [InlineData("absent-stamp")] [InlineData("document")] [InlineData("view")] [InlineData("content")]
    [InlineData("scene")] [InlineData("scene-revision")] [InlineData("camera-revision")]
    [InlineData("scale")] [InlineData("non-affine")] [InlineData("nan-position")] [InlineData("fov")] [InlineData("near")] [InlineData("far")]
    [InlineData("pivot")] [InlineData("distance")]
    public void Codec_RejectsEveryMalformedIdentityEnvelopeAndCameraPartition(string kind)
    {
        var b = PreviewViewCodec.Encode(View());
        switch (kind)
        {
            case "short": b = b[..^1]; break;
            case "long": b = [.. b, 0]; break;
            case "magic": b[0] = 0; break;
            case "version": b[8] = 2; break;
            case "size": b[13] = 0; break;
            case "stamp-version": b[16] = 2; break;
            case "absent-stamp": Array.Clear(b, 16, 148); break;
            case "document": Array.Fill(b, (byte)'0', 20, 32); break;
            case "view": Array.Fill(b, (byte)'0', 52, 32); break;
            case "content": b[84] = (byte)'!'; break;
            case "scene": Array.Fill(b, (byte)'0', 116, 32); break;
            case "scene-revision": BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(148), -1); break;
            case "camera-revision": Array.Clear(b, 156, 8); break;
            case "scale": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(164), 2); break;
            case "non-affine": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(176), .01f); break;
            case "nan-position": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(212), float.NaN); break;
            case "fov": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(228), 0); break;
            case "near": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(232), 0); break;
            case "far": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(236), 1); break;
            case "pivot": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(240), float.PositiveInfinity); break;
            case "distance": BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(252), -1); break;
        }
        Assert.Throws<InvalidDataException>(() => PreviewViewCodec.Decode(b));
    }

    [Fact]
    public void FrameV2_PreservesStampSlotAndSimulationCountersAndRejectsOldHeader()
    {
        var id = new SessionIdentity(Guid.NewGuid(), 1, new('A',64), new('B',64), new('C',64));
        var surface = FrameSurface.Create(id, 3, 32, 16); var header = new byte[FrameCodec.HeaderBytes]; FrameCodec.Initialize(header, surface);
        var original = new FrameMetadata(2, 9, 123, 456, 789) { PreviewView = View(ulong.MaxValue).Stamp };
        FrameCodec.WriteSlot(header, surface, original, FrameSlotState.Ready);
        Assert.Equal(original, FrameCodec.ReadSlot(header, surface, 2));
        Assert.Equal(148, PreviewViewCodec.StampBytes);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(FrameCodec.SlotOffset(2) + 176)));
        var absent = original with { PreviewView = null }; FrameCodec.WriteSlot(header, surface, absent, FrameSlotState.Ready);
        Assert.Equal(absent, FrameCodec.ReadSlot(header, surface, 2));
        header[FrameCodec.SlotOffset(2) + 180] = 1;
        Assert.Throws<InvalidDataException>(() => FrameCodec.ReadSlot(header, surface, 2));
        FrameCodec.Initialize(header, surface); header[7] = (byte)'1';
        Assert.Throws<InvalidDataException>(() => FrameCodec.ValidateHeader(header, surface));
        Assert.Throws<InvalidDataException>(() => (id with { ControlVersion = 3 }).Validate());
        Assert.Throws<InvalidDataException>(() => (id with { FrameVersion = 1 }).Validate());
    }

    [Fact]
    public void EneritControlAndGpuJson_RoundTripExplicitCapabilityAndDetachedState()
    {
        var view = View(ulong.MaxValue); var id = new SessionIdentity(Guid.NewGuid(), 1, new('A',64),new('B',64),new('C',64));
        var request = ControlRequest.Create(id, 4, CommandKind.SetPreviewView); request.PreviewView = PreviewViewCodec.Encode(view);
        var writer = new ArrayBufferWriter<byte>(); request.Serialize(writer); var offset = 0;
        var decoded = new ControlRequest().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(view, PreviewViewCodec.Decode(decoded.PreviewView!)); Assert.Equal(writer.WrittenCount, offset); Assert.True(writer.WrittenCount < 64 * 1024);
        var stampBytes = new byte[148]; PreviewViewCodec.WriteStamp(stampBytes, view.Stamp);
        var reply = new ControlReply { Stamp = request, PreviewViewVersion = 1, PreviewViewStamp = stampBytes };
        writer.Clear(); reply.Serialize(writer); offset = 0; var ack = new ControlReply().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(1, ack.PreviewViewVersion); Assert.Equal(view.Stamp, PreviewViewCodec.ReadStamp(ack.PreviewViewStamp!));
        var metadata = new FrameMetadata(2, 6, 0, 0, 1) { PreviewView = view.Stamp };
        Assert.Equal(metadata, JsonSerializer.Deserialize<FrameMetadata>(JsonSerializer.Serialize(metadata)));
        var invalid=metadata with {PreviewView=view.Stamp with {Version=2}};
        Assert.Throws<InvalidDataException>(()=>invalid.PreviewView.Validate());
        var missingVersion=JsonSerializer.Serialize(metadata).Replace(",\"Version\":1", "", StringComparison.Ordinal);
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<FrameMetadata>(missingVersion));
        Array.Clear(request.PreviewView); Assert.Equal(view, PreviewViewCodec.Decode(decoded.PreviewView!));
    }
}
