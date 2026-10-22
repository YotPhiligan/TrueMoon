using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Checkpoints;

public sealed class CheckpointCodecTests
{
    internal const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    internal static SceneSnapshot Scene(long revision = 7) => new("room", revision, [new() { Id = "switch", Interactive = true }]);
    internal static GameCheckpoint Snapshot(ulong tick = 5) => new(Hash, new('B', 64), "room", 7, new(tick, new(1.25f, -2, 3, 0.1f, 0.2f, -0.3f), true));
    [Theory]
    [InlineData(0ul)] [InlineData(1ul)] [InlineData(ulong.MaxValue)]
    public void Codec_RoundTripsUnsignedTicksExactFloatsAndDetachedState(ulong tick)
    {
        var original = Snapshot(tick); var bytes = GameCheckpointCodec.Encode(original);
        Assert.Equal("TMCK", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(tick, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(82)));
        var decoded = GameCheckpointCodec.Decode(bytes);
        Assert.Equal(original.State, decoded.State); Assert.Equal(original.HostContractId, decoded.HostContractId);
        Assert.Equal(original.SourceGameBuildId, decoded.SourceGameBuildId); Assert.Equal(original.SceneId, decoded.SceneId);
        Assert.Equal(original.SceneRevision, decoded.SceneRevision); Assert.Equal(bytes, GameCheckpointCodec.Encode(decoded));
        bytes[90] ^= 1; Assert.Equal(original.State.Camera, decoded.State.Camera);
    }
    [Theory]
    [InlineData(1)] [InlineData(128)]
    public void Codec_AcceptsSceneCharacterBoundariesAndStrictUnicode(int length)
    {
        var scene = new string('界', length); var checkpoint = new GameCheckpoint(Hash, Hash, scene, 0, new(0, default, false));
        var bytes = GameCheckpointCodec.Encode(checkpoint); var restored = GameCheckpointCodec.Decode(bytes);
        Assert.Equal(scene, restored.SceneId); Assert.Equal(GameCheckpointCodec.FixedBytes + length * 3, bytes.Length);
        Assert.False(restored.State.Interaction); Assert.Equal(0, restored.SceneRevision);
    }
    [Theory]
    [InlineData("magic")] [InlineData("version")] [InlineData("checksum")]
    [InlineData("flag")] [InlineData("nan")] [InlineData("infinity")]
    [InlineData("revision")] [InlineData("scene-length")] [InlineData("utf8")]
    [InlineData("short")] [InlineData("trailing")] [InlineData("max")] [InlineData("over-max")]
    public void Codec_RejectsMalformedOrRehashedInvalidPayload(string invalid)
    {
        var bytes = GameCheckpointCodec.Encode(Snapshot()); var rehash = true;
        switch (invalid)
        {
            case "magic": bytes[0] ^= 1; break;
            case "version": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1); break;
            case "checksum": bytes[^1] ^= 1; rehash = false; break;
            case "flag": bytes[114] = 2; break;
            case "nan": BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(90), float.NaN); break;
            case "infinity": BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(106), float.PositiveInfinity); break;
            case "revision": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(74), -1); break;
            case "scene-length": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(72), 513); break;
            case "utf8": bytes[115] = 0xff; break;
            case "short": bytes = bytes[..(GameCheckpointCodec.FixedBytes - 1)]; rehash = false; break;
            case "trailing": bytes = bytes.Concat(new byte[1]).ToArray(); rehash = false; break;
            case "max": bytes = new byte[GameCheckpointCodec.MaxPayloadBytes]; rehash = false; break;
            case "over-max": bytes = new byte[GameCheckpointCodec.MaxPayloadBytes + 1]; rehash = false; break;
        }
        if (rehash) SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32), bytes.AsSpan(bytes.Length - 32));
        Assert.Throws<InvalidDataException>(() => GameCheckpointCodec.Decode(bytes));
    }
    [Fact]
    public void Envelope_RejectsInvalidIdentitySceneAndCameraBeforeRestore()
    {
        Assert.Throws<InvalidDataException>(() => new GameCheckpoint("bad", Hash, "room", 7, Snapshot().State));
        Assert.Throws<InvalidDataException>(() => new GameCheckpoint(Hash, Hash, new('x', 129), 7, Snapshot().State));
        Assert.Throws<InvalidDataException>(() => GameCheckpointCodec.Encode(new(Hash, Hash, "\ud800", 7, Snapshot().State)));
        var checkpoint = Snapshot(); checkpoint.ValidateFor(Hash, Scene()); // Source build differs intentionally.
        Assert.Throws<InvalidDataException>(() => checkpoint.ValidateFor(new('C', 64), Scene()));
        Assert.Throws<InvalidDataException>(() => checkpoint.ValidateFor(Hash, Scene(8)));
        Assert.Throws<InvalidDataException>(() => checkpoint.ValidateFor(Hash, new("other", 7, [])));
    }
    [Fact]
    public void ProbeGame_RestoreValidatesBeforeAtomicCommitAndCaptureDoesNotTick()
    {
        using var game = new ProbeGame(); game.Load(Scene()); game.Tick(); game.Tick(); game.Tick();
        var before = game.CaptureCheckpoint(); Assert.Equal(3ul, before.Tick); Assert.True(before.Interaction); Assert.Equal(0.375f, before.Camera.X);
        Assert.Equal(before, game.CaptureCheckpoint()); Assert.Equal(3ul, game.TickNumber);
        var invalid = before with { Tick = 99, Camera = before.Camera with { Roll = float.NaN } };
        Assert.Throws<InvalidDataException>(() => game.RestoreCheckpoint(invalid)); Assert.Equal(before, game.CaptureCheckpoint());
        var restored = Snapshot(71).State; game.RestoreCheckpoint(restored);
        Assert.Equal(restored, game.CaptureCheckpoint()); Assert.Equal(7, game.Revision);
    }
    [Fact]
    public void GeneratedWire_RoundTripsCheckpointBytesOutcomeAndUnsignedTickBits()
    {
        var payload = GameCheckpointCodec.Encode(Snapshot(ulong.MaxValue));
        var request = ControlRequest.Create(new(Guid.NewGuid(), 1, Hash, Hash, Hash), 17, CommandKind.RestoreCheckpoint); request.Checkpoint = payload;
        var writer = new ArrayBufferWriter<byte>(); request.Serialize(writer); var offset = 0;
        var decoded = new ControlRequest().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(writer.WrittenCount, offset); Assert.Equal(payload, decoded.Checkpoint); Assert.Equal(request.Identity(), decoded.Identity());
        var reply = new ControlReply { Stamp = request, Phase = "Paused", Tick = -1, CheckpointOutcome = (int)CheckpointOutcome.Captured, Checkpoint = payload };
        writer = new(); reply.Serialize(writer); offset = 0; var roundtrip = new ControlReply().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(writer.WrittenCount, offset); Assert.Equal((int)CheckpointOutcome.Captured, roundtrip.CheckpointOutcome);
        Assert.Equal(ulong.MaxValue, GameCheckpointCodec.Decode(roundtrip.Checkpoint!).State.Tick); Assert.Equal(-1, roundtrip.Tick);
        Assert.True(writer.WrittenCount < 64 * 1024);
    }
}
