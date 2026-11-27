using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Runtime.Checkpoints;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Scenes;
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
        Assert.Equal("TMCK", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(tick, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(82)));
        var decoded = GameCheckpointCodec.Decode(bytes);
        Assert.Null(decoded.State.Payload);
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
    [Fact]
    public void Codec_RoundTripsUnicodeGamePayloadWithExactBytesAndValueEquality()
    {
        const string text = "界🐉e\u0301\0";
        var original = WithPayload(text);
        var bytes = GameCheckpointCodec.Encode(original);
        var utf8 = Encoding.UTF8.GetBytes(text);
        Assert.Equal(GameCheckpointCodec.FixedBytes + 4 + utf8.Length, bytes.Length);
        const int lengthOffset = 115 + 4 + 2;
        Assert.Equal(utf8.Length, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(lengthOffset)));
        Assert.Equal(utf8, bytes[(lengthOffset + 2)..^32]);

        var decoded = GameCheckpointCodec.Decode(bytes);
        Assert.Equal(text, decoded.State.Payload);
        Assert.NotSame(text, decoded.State.Payload);
        Assert.Equal(original.State, decoded.State);
        Assert.Equal(original.State.GetHashCode(), decoded.State.GetHashCode());
        Assert.NotEqual(original.State, decoded.State with { Payload = text.Normalize() });
        Assert.Equal(bytes, GameCheckpointCodec.Encode(decoded));
    }

    [Fact]
    public void GamePayload_EnforcesUtf8ByteBoundAndRejectsEmptyAndMalformedUnicode()
    {
        var maximum = new string('界', GameCheckpointState.MaxPayloadUtf8Bytes / 3);
        var boundary = WithPayload(maximum);
        Assert.Equal(maximum, GameCheckpointCodec.Decode(GameCheckpointCodec.Encode(boundary)).State.Payload);
        Assert.Throws<InvalidDataException>(() => WithPayload(maximum + "a"));
        Assert.Throws<InvalidDataException>(() => WithPayload(""));
        Assert.Throws<InvalidDataException>(() => WithPayload("\ud800"));
    }

    [Theory]
    [InlineData("shorter-length")] [InlineData("longer-length")] [InlineData("zero-length")]
    [InlineData("trailing")] [InlineData("oversized")] [InlineData("utf8")] [InlineData("checksum")]
    public void Codec_RejectsInvalidGamePayloadSectionsEvenWithRecomputedHash(string invalid)
    {
        var bytes = GameCheckpointCodec.Encode(WithPayload("界"));
        const int lengthOffset = 115 + 4 + 2, payloadOffset = lengthOffset + 2;
        switch (invalid)
        {
            case "shorter-length": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(lengthOffset), 2); break;
            case "longer-length": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(lengthOffset), 4); break;
            case "zero-length": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(lengthOffset), 0); break;
            case "trailing": Array.Resize(ref bytes, bytes.Length + 1); break;
            case "oversized":
                const int overLimit = GameCheckpointState.MaxPayloadUtf8Bytes + 1;
                Array.Resize(ref bytes, payloadOffset + overLimit + 32);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(lengthOffset), overLimit);
                bytes.AsSpan(payloadOffset, overLimit).Fill((byte)'a');
                break;
            case "utf8": bytes[payloadOffset] = 0xff; break;
            case "checksum": bytes[payloadOffset + 2] ^= 1; break; // Still valid UTF-8, but different from the hashed bytes.
        }
        if (invalid != "checksum") SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32), bytes.AsSpan(bytes.Length - 32));
        Assert.Throws<InvalidDataException>(() => GameCheckpointCodec.Decode(bytes));
    }

    private static GameCheckpoint WithPayload(string payload) =>
        new(Hash, Hash, "room", 7, Snapshot().State with { Payload = payload });

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
        var unsupported = Snapshot(99).State with { Payload = "game-state" };
        var rejected = Assert.Throws<CheckpointRejectedException>(() => game.RestoreCheckpoint(unsupported));
        Assert.Equal(CheckpointOutcome.RestoreRejected, rejected.Outcome);
        Assert.Equal(before, game.CaptureCheckpoint());
        var restored = Snapshot(71).State; game.RestoreCheckpoint(restored);
        Assert.Equal(restored, game.CaptureCheckpoint()); Assert.Equal(7, game.Revision);
    }
    [Fact]
    public void GeneratedWire_RoundTripsMaximumGamePayloadAndObjectSectionWithinControlBudget()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, Hash, Hash, Hash);
        var objects = new GameObjectStates(Enumerable.Range(0, GameObjectStates.MaximumCount)
            .Select(i => new GameObjectState((char)('一' + i) + new string('界', 127), i % 2 == 0)));
        var state = Snapshot(ulong.MaxValue).State with { Objects = objects, Payload = new string('界', GameCheckpointState.MaxPayloadUtf8Bytes / 3) };
        var payload = GameCheckpointCodec.Encode(new(Hash, Hash, new string('界', 128), 7, state));
        Assert.Equal(GameCheckpointCodec.FixedBytes + 384 + 64 * (3 + 384) + 24 * 1024, payload.Length);
        var request = ControlRequest.Create(identity, 17, CommandKind.RestoreCheckpoint); request.Checkpoint = payload;
        var writer = new ArrayBufferWriter<byte>(); request.Serialize(writer); var offset = 0;
        Assert.True(writer.WrittenCount < BoundedControlClient.PayloadLimit);
        var decoded = new ControlRequest().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(writer.WrittenCount, offset); Assert.Equal(payload, decoded.Checkpoint); Assert.Equal(request.Identity(), decoded.Identity());
        var reply = new ControlReply
        {
            Stamp = ControlRequest.Create(identity, 18, CommandKind.CaptureCheckpoint), Phase = "Paused", Tick = -1,
            CheckpointOutcome = (int)CheckpointOutcome.Captured, Checkpoint = payload
        };
        writer = new(); reply.Serialize(writer); offset = 0; var roundtrip = new ControlReply().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(writer.WrittenCount, offset); Assert.Equal((int)CheckpointOutcome.Captured, roundtrip.CheckpointOutcome);
        Assert.Equal(state, GameCheckpointCodec.Decode(roundtrip.Checkpoint!).State); Assert.Equal(-1, roundtrip.Tick);
        Assert.True(writer.WrittenCount < BoundedControlClient.PayloadLimit);
    }
}
