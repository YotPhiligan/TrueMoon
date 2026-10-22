using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests.Checkpoints;

public sealed class ObjectCheckpointTests
{
    private static GameCheckpoint Checkpoint(GameObjectStates objects) => new(CheckpointCodecTests.Hash, CheckpointCodecTests.Hash, "room", 7,
        new(19, new(1, 2, 3, .1f, .2f, .3f), true) { Objects = objects });

    [Fact]
    public void States_AreCanonicalCopiedAndValueEqualAcrossIndependentCaptures()
    {
        var source = new[] { new GameObjectState("b", true), new("a", false) }; var states = new GameObjectStates(source);
        source[0] = new("changed", false);
        Assert.Equal(new[] { "a", "b" }, states.Entries.Select(e => e.EntityId)); Assert.True(states.Entries[1].Active);
        var equivalent = new GameObjectStates([new("a", false), new("b", true)]);
        Assert.Equal(states, equivalent); Assert.Equal(states.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(states, new GameObjectStates([new("a", true), new("b", true)]));
        Assert.Equal(Checkpoint(states).State, Checkpoint(equivalent).State);
        Assert.False(states.Equals(null));
    }

    [Fact]
    public void DiagnosticJson_RoundTripsValidatedStateWithoutWeakeningImmutability()
    {
        var original = Checkpoint(new([new("a", false), new("b", true)])).State;
        var decoded = JsonSerializer.Deserialize<GameCheckpointState>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original, decoded); Assert.NotSame(original.Objects, decoded.Objects);
        Assert.Throws<InvalidDataException>(() => JsonSerializer.Deserialize<GameObjectStates>("{\"Entries\":[{\"EntityId\":\"a\",\"Active\":false},{\"EntityId\":\"a\",\"Active\":true}]}"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(64)] [InlineData(65)]
    public void CountAndUtf8Boundaries_RoundTripCompleteDetachedState(int count)
    {
        var entries = Enumerable.Range(0, count).Select(i => new GameObjectState(i.ToString("D3") + new string('界', 125), i % 2 == 0));
        if (count == 65) { Assert.Throws<InvalidDataException>(() => new GameObjectStates(entries)); return; }
        var original = Checkpoint(new(entries)); var bytes = GameCheckpointCodec.Encode(original); var restored = GameCheckpointCodec.Decode(bytes);
        Assert.Equal(2, restored.Version); Assert.Equal(original.State, restored.State); Assert.Equal(count, restored.State.Objects.Entries.Length);
        Assert.Equal(bytes, GameCheckpointCodec.Encode(restored)); Assert.True(bytes.Length < GameCheckpointCodec.MaxPayloadBytes);
        bytes[^1] ^= 1; Assert.Equal(original.State, restored.State); Assert.NotSame(original.State.Objects, restored.State.Objects);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("surrogate")]
    public void InvalidObjectIds_AreRejected(string? id) => Assert.Throws<InvalidDataException>(() =>
        new GameObjectStates([new(id == "surrogate" ? new string((char)0xd800, 1) : id!, false)]));

    [Fact]
    public void DuplicateLongAndMissingStates_AreRejected()
    {
        Assert.Throws<InvalidDataException>(() => new GameObjectStates([new("a", false), new("a", true)]));
        Assert.Throws<InvalidDataException>(() => new GameObjectStates([new(new string('a', 129), false)]));
        Assert.Throws<ArgumentNullException>(() => new GameObjectStates(null!));
        Assert.Throws<InvalidDataException>(() => (Checkpoint(GameObjectStates.Empty).State with { Objects = null! }).Validate());
    }

    [Theory]
    [InlineData("count")] [InlineData("count-zero")] [InlineData("length-zero")] [InlineData("length-large")] [InlineData("flag")]
    [InlineData("utf8")] [InlineData("duplicate")] [InlineData("order")] [InlineData("truncated")]
    [InlineData("trailing")] [InlineData("v1")] [InlineData("future")]
    public void RehashedMalformedObjectSectionsAndOldSchemas_AreRejected(string invalid)
    {
        var bytes = GameCheckpointCodec.Encode(Checkpoint(new([new("a", false), new("b", true)])));
        const int countOffset = 119, firstLength = 121, firstId = 123, firstFlag = 124, secondId = 127;
        switch (invalid)
        {
            case "count": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(countOffset), 65); break;
            case "count-zero": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(countOffset), 0); break;
            case "length-zero": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(firstLength), 0); break;
            case "length-large": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(firstLength), 513); break;
            case "flag": bytes[firstFlag] = 2; break;
            case "utf8": bytes[firstId] = 0xff; break;
            case "duplicate": bytes[secondId] = (byte)'a'; break;
            case "order": bytes[firstId] = (byte)'z'; break;
            case "truncated": bytes = bytes[..^33].Concat(new byte[32]).ToArray(); break;
            case "trailing": bytes = bytes[..^32].Concat(new byte[33]).ToArray(); break;
            case "v1": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1); break;
            case "future": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3); break;
        }
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32), bytes.AsSpan(bytes.Length - 32));
        Assert.Throws<InvalidDataException>(() => GameCheckpointCodec.Decode(bytes));
    }

    [Fact]
    public void SnapshotNegotiation_RequiresV2WhileControlAndFrameRemainCurrent()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, CheckpointCodecTests.Hash, CheckpointCodecTests.Hash, CheckpointCodecTests.Hash);
        identity.Validate(); Assert.Equal(2, identity.SnapshotVersion); Assert.Equal(3, identity.ControlVersion); Assert.Equal(1, identity.FrameVersion);
        Assert.Throws<InvalidDataException>(() => (identity with { SnapshotVersion = 1 }).Validate());
    }
}
