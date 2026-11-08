using System.Buffers.Binary;
using System.Text;
using Xunit;
using static TrueMoon.Nom.Tests.NomFormatFixtures;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomFormatTests
{
    [Fact]
    public void RootAndSegment_IndependentGoldenBytes_PinLayoutIdentityAndChecksums()
    {
        Assert.Equal(HeaderGolden, NomJournalCodec.EncodeHeader(Header));
        Assert.Equal(Header, NomJournalCodec.DecodeHeader(HeaderGolden));
        Assert.Equal(RootGolden, NomRootCodec.Encode(Root));
        Assert.Equal(Root, NomRootCodec.Decode(RootGolden, ProjectId, StoreId));
        Assert.Equal(Convert.FromHexString("00112233445566778899aabbccddeeff"), HeaderGolden[16..32]);
    }

    [Fact]
    public void Batch_IndependentGolden_SortsKeysAndDistinguishesDeleteFromEmptyPut()
    {
        Assert.Equal(BatchGolden, NomJournalCodec.EncodeBatch(Header, GoldenBatch));
        var decoded = NomJournalCodec.DecodeBatch(Header, BatchGolden);
        Assert.Equal(1, decoded.Sequence); Assert.Equal(0, decoded.ExpectedGeneration); Assert.Equal(1, decoded.Generation);
        Assert.Equal(OperationId, decoded.OperationId); Assert.Equal(3, decoded.Mutations.Count);
        Assert.Equal(new byte[] { 1, 127, 128 }, decoded.Mutations.Select(m => m.Key.Span[0]).ToArray());
        Assert.Equal(new byte[] { 16, 32 }, decoded.Mutations[0].Value.ToArray()); Assert.False(decoded.Mutations[0].IsDelete);
        Assert.Empty(decoded.Mutations[1].Value.ToArray()); Assert.False(decoded.Mutations[1].IsDelete);
        Assert.Empty(decoded.Mutations[2].Value.ToArray()); Assert.True(decoded.Mutations[2].IsDelete);
        Assert.Equal(128, GoldenBatch.Mutations[0].Key.Span[0]); // Encoding does not reorder the caller's list.
    }

    [Fact]
    public void MetadataAndRevision_IndependentGolden_PinUtf8GuidOrderAndHashDomain()
    {
        Assert.Equal(MetadataGolden, NomResourceMetadataCodec.Encode(Asset));
        var decoded = NomResourceMetadataCodec.Decode(MetadataGolden);
        Assert.Equal(Guid.Parse("20314253-6475-8697-a8b9-cadbecfd0e1f"), decoded.Id);
        Assert.Equal(ProjectId, decoded.Source); Assert.Equal(StoreId, decoded.Kind);
        Assert.Equal("mesh/λ", decoded.Locator); Assert.Equal(new byte[] { 0, 255, 1 }, decoded.Metadata);
        Assert.Equal(ResourceRevision, NomResourceMetadataCodec.Revision(decoded, ContentDigest, 3));
    }

    [Fact]
    public void Checksums_RejectEverySingleByteMutationOfCompleteGoldenFrames()
    {
        foreach (var source in new[] { HeaderGolden, RootGolden, BatchGolden })
        for (var offset = 0; offset < source.Length; offset++)
        {
            var bad = source.ToArray(); bad[offset] ^= 1;
            if (source.Length == 160) Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeHeader(bad));
            else if (source.Length == 256) Assert.Throws<InvalidDataException>(() => NomRootCodec.Decode(bad, ProjectId, StoreId));
            else Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header, bad));
        }
    }

    [Theory]
    [InlineData("magic")][InlineData("version")][InlineData("header-size")][InlineData("flags")]
    [InlineData("reserved")][InlineData("project")][InlineData("store")][InlineData("segment-zero")]
    [InlineData("sequence-negative")][InlineData("generation-negative")][InlineData("generation-above-sequence")]
    [InlineData("first-segment-base")][InlineData("first-segment-hash")][InlineData("later-segment-no-hash")]
    public void Header_RejectsAuthenticatedInvalidState(string field)
    {
        var bad = HeaderGolden;
        switch (field)
        {
            case "magic": bad[0]++; break; case "version": bad[8] = 2; break; case "header-size": bad[10]--; break;
            case "flags": bad[12] = 1; break; case "reserved": bad[104] = 1; break;
            case "project": bad.AsSpan(16, 16).Clear(); break; case "store": bad.AsSpan(32, 16).Clear(); break;
            case "segment-zero": Counter(bad, 48, 0); break; case "sequence-negative": Counter(bad, 56, -1); break;
            case "generation-negative": Counter(bad, 64, -1); break; case "generation-above-sequence": Counter(bad, 64, 1); break;
            case "first-segment-base": Counter(bad, 56, 1); break; case "first-segment-hash": bad[72] = 1; break;
            case "later-segment-no-hash": Counter(bad, 48, 2); break;
        }
        AuthenticateHeader(bad);
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeHeader(bad));
    }

    [Theory]
    [InlineData("version")][InlineData("flags")][InlineData("reserved")][InlineData("epoch-zero")]
    [InlineData("negative-counter")][InlineData("generation")][InlineData("floor")][InlineData("first-zero")]
    [InlineData("active-before-first")][InlineData("segment-window")][InlineData("missing-checkpoint")]
    [InlineData("initial-checkpoint-digest")][InlineData("initial-chain-hash")][InlineData("initial-first")]
    public void Root_RejectsAuthenticatedInvalidAuthority(string field)
    {
        var bad = RootGolden;
        switch (field)
        {
            case "version": bad[8] = 2; break; case "flags": bad[12] = 1; break; case "reserved": bad[160] = 1; break;
            case "epoch-zero": Counter(bad, 48, 0); break; case "negative-counter": Counter(bad, 56, -1); break;
            case "generation": Counter(bad, 64, 1); break; case "floor": Counter(bad, 56, 1); Counter(bad, 120, 0); break;
            case "first-zero": Counter(bad, 104, 0); break; case "active-before-first": Counter(bad, 112, 0); break;
            case "segment-window": Counter(bad, 112, 33); break; case "missing-checkpoint": Counter(bad, 56, 1); Counter(bad, 120, 1); break;
            case "initial-checkpoint-digest": bad[72] = 1; break; case "initial-chain-hash": bad[128] = 1; break;
            case "initial-first": Counter(bad, 104, 2); Counter(bad, 112, 2); break;
        }
        AuthenticateRoot(bad);
        Assert.Throws<InvalidDataException>(() => NomRootCodec.Decode(bad, ProjectId, StoreId));
    }

    [Fact]
    public void Root_ValidCheckpointAndLastSegmentWindow_PreserveExactAuthority()
    {
        var root = Root with { Epoch = 9, CheckpointSequence = 7, CheckpointGeneration = 3,
            CheckpointDigest = new string('a', 64), FirstSegmentId = 5, ActiveSegmentId = 36,
            MinimumSequence = 8, PreviousSegmentHash = new string('b', 64) };
        Assert.Equal(root, NomRootCodec.Decode(NomRootCodec.Encode(root), ProjectId, StoreId));
        Assert.Throws<InvalidDataException>(() => NomRootCodec.Encode(root with { ActiveSegmentId = 37 }));
        Assert.Throws<InvalidDataException>(() => NomRootCodec.Decode(RootGolden, StoreId, ProjectId));
        Assert.Throws<InvalidDataException>(() => NomRootCodec.Encode(root with { CheckpointDigest = new string('A', 64) }));
    }

    [Theory]
    [InlineData("version")][InlineData("flags")][InlineData("length-overflow")][InlineData("count-zero")]
    [InlineData("count-overflow")][InlineData("sequence-zero")][InlineData("sequence-overflow")]
    [InlineData("expected-negative")][InlineData("generation-gap")][InlineData("generation-above-sequence")]
    [InlineData("operation-empty")][InlineData("kind")][InlineData("entry-reserved")][InlineData("key-empty")]
    [InlineData("key-overflow")][InlineData("value-overflow")][InlineData("delete-with-value")]
    [InlineData("duplicate")][InlineData("unsorted")][InlineData("count-less-than-payload")]
    [InlineData("footer-magic")][InlineData("footer-size")]
    public void Batch_RejectsAuthenticatedInvalidFramingAndPayload(string field)
    {
        var bad = BatchGolden;
        switch (field)
        {
            case "version": bad[8] = 2; break; case "flags": bad[12] = 1; break;
            case "length-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(16), uint.MaxValue); break;
            case "count-zero": BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(20), 0); break;
            case "count-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(20), uint.MaxValue); break;
            case "sequence-zero": Counter(bad, 24, 0); break; case "sequence-overflow": Counter(bad, 24, -1); break;
            case "expected-negative": Counter(bad, 32, -1); break; case "generation-gap": Counter(bad, 40, 2); break;
            case "generation-above-sequence": Counter(bad, 32, 2); Counter(bad, 40, 2); break;
            case "operation-empty": bad.AsSpan(48, 16).Clear(); break;
            case "kind": bad[96] = 3; break; case "entry-reserved": bad[97] = 1; break;
            case "key-empty": BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(100), 0); break;
            case "key-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(100), uint.MaxValue); break;
            case "value-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(104), uint.MaxValue); break;
            case "delete-with-value": bad[96] = 2; break; case "duplicate": bad[123] = 1; break; case "unsorted": bad[136] = 0; break;
            case "count-less-than-payload": BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(20), 2); break;
            case "footer-magic": bad[^16]++; break; case "footer-size": bad[^8]--; break;
        }
        AuthenticateBatch(bad);
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header, bad));
    }

    [Fact]
    public void Batch_ContextBindingAndInputSnapshots_RejectForeignFramesAndDuplicateKeys()
    {
        var key = new byte[] { 4 }; var value = new byte[] { 5 }; var mutation = new NomJournalMutation(key, value);
        var list = new[] { mutation }; var batch = Batch(1, 0, 0, list); key[0] = 8; value[0] = 9; list[0] = new NomJournalMutation([7], null);
        var decoded = NomJournalCodec.DecodeBatch(Header, NomJournalCodec.EncodeBatch(Header, batch));
        Assert.Equal(new byte[] { 4 }, Assert.Single(decoded.Mutations).Key.ToArray());
        Assert.Equal(new byte[] { 5 }, decoded.Mutations[0].Value.ToArray()); Assert.Equal(0, decoded.Generation);
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header with { StoreId = ProjectId }, BatchGolden));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1, new NomJournalMutation([1], [2]), new NomJournalMutation([1], null))));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1)));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1, new NomJournalMutation[] { null! })));
        Assert.Throws<ArgumentNullException>(() => new NomJournalMutation(null!, []));
        Assert.Throws<ArgumentNullException>(() => new NomJournalBatch(1, 0, 1, OperationId, null!));
    }

    [Fact]
    public void KeyCodec_IndependentGoldenAndMaximumBytes_PinCanonicalFieldBoundaries()
    {
        var expected = Convert.FromHexString("011002000200000000ff0100000041");
        Assert.Equal(expected, NomKeyCodec.Encode(NomKeySpace.Asset, [0, 255], [65])); NomKeyCodec.Validate(expected);
        var maximum = NomKeyCodec.Encode(NomKeySpace.Revision, new byte[1016]); Assert.Equal(1024, maximum.Length); NomKeyCodec.Validate(maximum);
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Encode(NomKeySpace.Revision, new byte[1017]));
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Encode((NomKeySpace)255, [1]));
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Encode(NomKeySpace.Asset));
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Encode(NomKeySpace.Asset, Enumerable.Range(0, 9).Select(_ => new byte[] { 1 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Encode(NomKeySpace.Asset, []));
        var eightFields = NomKeyCodec.Encode(NomKeySpace.Asset, Enumerable.Range(0, 8).Select(i => new byte[] { (byte)i }).ToArray());
        Assert.Equal(44, eightFields.Length); Assert.Equal(8, eightFields[2]); NomKeyCodec.Validate(eightFields);
    }

    [Theory]
    [InlineData("version")][InlineData("space")][InlineData("count-zero")][InlineData("count-nine")]
    [InlineData("empty-field")][InlineData("overflow")][InlineData("truncated")][InlineData("trailing")]
    public void KeyCodec_RejectsInvalidCanonicalFields(string field)
    {
        var bad = Convert.FromHexString("011002000200000000ff0100000041");
        switch (field)
        {
            case "version": bad[0] = 2; break; case "space": bad[1] = 255; break; case "count-zero": bad[2] = 0; break;
            case "count-nine": bad[2] = 9; break; case "empty-field": bad[4] = 0; break;
            case "overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(4), uint.MaxValue); break;
            case "truncated": bad = bad[..^1]; break; case "trailing": bad = Join(bad, [0]); break;
        }
        Assert.Throws<InvalidDataException>(() => NomKeyCodec.Validate(bad));
    }

    [Theory]
    [InlineData("version")][InlineData("reserved")][InlineData("empty-id")][InlineData("empty-source")][InlineData("empty-kind")]
    [InlineData("empty-locator")][InlineData("locator-overflow")][InlineData("metadata-overflow")][InlineData("utf8")]
    [InlineData("control")][InlineData("truncated")][InlineData("trailing")]
    public void Metadata_RejectsInvalidUtf8IdentityLengthsAndFraming(string field)
    {
        var bad = MetadataGolden;
        switch (field)
        {
            case "version": bad[8] = 2; break; case "reserved": bad[10] = 1; break;
            case "empty-id": bad.AsSpan(12, 16).Clear(); break; case "empty-source": bad.AsSpan(28, 16).Clear(); break;
            case "empty-kind": bad.AsSpan(44, 16).Clear(); break; case "empty-locator": bad[60] = 0; break;
            case "locator-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(60), uint.MaxValue); break;
            case "metadata-overflow": BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(64), uint.MaxValue); break;
            case "utf8": bad[68] = 255; break; case "control": bad[68] = 0; break;
            case "truncated": bad = bad[..^1]; break; case "trailing": bad = Join(bad, [0]); break;
        }
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Decode(bad));
    }

    [Fact]
    public void Metadata_MaximumUtf8AndPayload_AndRevisionInputsHaveExactBoundaries()
    {
        var maximum = new AssetRecord(Asset.Id, ProjectId, StoreId, new string('λ', 512), Enumerable.Repeat((byte)0xA5, 65536).ToArray());
        var bytes = NomResourceMetadataCodec.Encode(maximum); var decoded = NomResourceMetadataCodec.Decode(bytes);
        Assert.Equal(68 + 1024 + 65536, bytes.Length); Assert.Equal(new string('λ', 512), decoded.Locator);
        Assert.Equal(65536, decoded.Metadata.Length); Assert.Equal(Enumerable.Repeat((byte)0xA5, 65536), decoded.Metadata);
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Revision(Asset, ContentDigest, -1));
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Revision(Asset, ContentDigest, 512L * 1024 * 1024 + 1));
        Assert.NotEqual(ResourceRevision, NomResourceMetadataCodec.Revision(Asset, ContentDigest, 0));
        Assert.NotEqual(ResourceRevision, NomResourceMetadataCodec.Revision(Asset, ContentDigest, 512L * 1024 * 1024));
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Revision(Asset, ContentDigest.ToUpperInvariant(), 3));
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Revision(Asset, "00", 3));
        var changed = new AssetRecord(Asset.Id, ProjectId, StoreId, "mesh/λ", [0, 255, 2]);
        Assert.NotEqual(ResourceRevision, NomResourceMetadataCodec.Revision(changed, ContentDigest, 3));
    }

    [Fact]
    public void Limits_MinimumFrameAndEmptyPut_AcceptExactSegmentBoundary()
    {
        var limits = new NomFormatLimits { MaximumKeyBytes = 1, MaximumValueBytes = 1, MaximumPayloadBytes = 13,
            MaximumMutations = 1, MaximumSegmentBytes = 317, MaximumIndexKeys = 1, MaximumIndexBytes = 97, MaximumReplayBatches = 1 };
        var batch = Batch(1, 0, 0, new NomJournalMutation([1], [])); var encoded = NomJournalCodec.EncodeBatch(Header, batch, limits);
        Assert.Equal(157, encoded.Length); var decoded = NomJournalCodec.DecodeBatch(Header, encoded, limits);
        Assert.False(Assert.Single(decoded.Mutations).IsDelete); Assert.Empty(decoded.Mutations[0].Value.ToArray());
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, batch, limits with { MaximumSegmentBytes = 316 }));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header, encoded, limits with { MaximumSegmentBytes = 316 }));
        using var stream = new MemoryStream(HeaderGolden, writable: false);
        Assert.Equal(0, NomJournalRecovery.Recover(stream, Header, limits: limits with { MaximumSegmentBytes = 160 }).Sequence);
    }

    [Theory]
    [InlineData("key")][InlineData("value")][InlineData("payload")][InlineData("mutations")]
    public void Batch_LoweredLimits_AcceptExactBoundaryAndRejectAbove(string budget)
    {
        var limits = new NomFormatLimits { MaximumKeyBytes = 1, MaximumValueBytes = 2,
            MaximumPayloadBytes = budget == "mutations" ? 64 : 28, MaximumMutations = 2 };
        var exact = Batch(1, 0, 1, new NomJournalMutation([1], [1, 2]), new NomJournalMutation([2], []));
        Assert.Equal(172, NomJournalCodec.EncodeBatch(Header, exact, limits).Length);
        var tooMuch = budget switch
        {
            "key" => Batch(1, 0, 1, new NomJournalMutation([1, 2], [])), "value" => Batch(1, 0, 1, new NomJournalMutation([1], [1, 2, 3])),
            "payload" => Batch(1, 0, 1, new NomJournalMutation([1], [1, 2]), new NomJournalMutation([2], [3])),
            _ => Batch(1, 0, 1, new NomJournalMutation([1], []), new NomJournalMutation([2], []), new NomJournalMutation([3], []))
        };
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, tooMuch, limits));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header, NomJournalCodec.EncodeBatch(Header, tooMuch), limits));
    }

    [Fact]
    public void Batch_DefaultKeyValueAndCounterCeilings_AcceptMaximumAndRejectAdjacentOversize()
    {
        var key = Enumerable.Repeat((byte)0xFA, 1024).ToArray(); var value = Enumerable.Repeat((byte)0xB7, 131072).ToArray();
        var maximum = Batch(long.MaxValue, long.MaxValue, long.MaxValue, new NomJournalMutation(key, value));
        var bytes = NomJournalCodec.EncodeBatch(Header, maximum); var decoded = NomJournalCodec.DecodeBatch(Header, bytes);
        Assert.Equal(132252, bytes.Length); Assert.Equal(long.MaxValue, decoded.Sequence); Assert.Equal(long.MaxValue, decoded.Generation);
        Assert.Equal(key, Assert.Single(decoded.Mutations).Key.ToArray()); Assert.Equal(value, decoded.Mutations[0].Value.ToArray());
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1, new NomJournalMutation(new byte[1025], []))));
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1, new NomJournalMutation([1], new byte[131073]))));
    }

    [Fact]
    public void Batch_DefaultMutationCeiling_Accepts8192DistinctKeysAndRejects8193()
    {
        var maximum = Enumerable.Range(0, 8192).Select(i => new NomJournalMutation([(byte)(i >> 8), (byte)i], [])).ToArray();
        var bytes = NomJournalCodec.EncodeBatch(Header, Batch(1, 0, 1, maximum));
        var decoded = NomJournalCodec.DecodeBatch(Header, bytes);
        Assert.Equal(8192, decoded.Mutations.Count); Assert.Equal(new byte[] { 0, 0 }, decoded.Mutations[0].Key.ToArray());
        Assert.Equal(new byte[] { 31, 255 }, decoded.Mutations[^1].Key.ToArray()); Assert.False(decoded.Mutations[^1].IsDelete);
        Assert.Throws<InvalidDataException>(() => NomJournalCodec.EncodeBatch(Header,
            Batch(1, 0, 1, [.. maximum, new NomJournalMutation([32, 0], [])])));
    }

    [Theory]
    [InlineData("key", 0)][InlineData("key", 1025)][InlineData("value", 0)][InlineData("value", 131073)]
    [InlineData("payload", 12)][InlineData("payload", 4194305)][InlineData("mutations", 0)][InlineData("mutations", 8193)]
    [InlineData("segment", 159)][InlineData("segment", 67108865)][InlineData("keys", 0)][InlineData("keys", 2000001)]
    [InlineData("index", 96)][InlineData("index", 268435457)][InlineData("replay", 0)][InlineData("replay", 65537)]
    public void Limits_RejectValuesOutsideWireCeilings(string field, long value)
    {
        NomFormatLimits limits = field switch
        {
            "key" => new NomFormatLimits { MaximumKeyBytes = (int)value }, "value" => new() { MaximumValueBytes = (int)value },
            "payload" => new() { MaximumPayloadBytes = (int)value }, "mutations" => new() { MaximumMutations = (int)value },
            "segment" => new() { MaximumSegmentBytes = value }, "keys" => new() { MaximumIndexKeys = (int)value },
            "index" => new() { MaximumIndexBytes = value }, _ => new() { MaximumReplayBatches = (int)value }
        };
        Assert.Throws<InvalidDataException>(() => limits.Validate());
    }

    [Fact]
    public void Decoders_RejectShortAndTrailingFramesBeforeReadingFields()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), HeaderGolden[..159], Join(HeaderGolden, [0]) })
            Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeHeader(bytes));
        foreach (var bytes in new[] { Array.Empty<byte>(), RootGolden[..255], Join(RootGolden, [0]) })
            Assert.Throws<InvalidDataException>(() => NomRootCodec.Decode(bytes, ProjectId, StoreId));
        foreach (var bytes in new[] { Array.Empty<byte>(), BatchGolden[..95], BatchGolden[..^1], Join(BatchGolden, [0]) })
            Assert.Throws<InvalidDataException>(() => NomJournalCodec.DecodeBatch(Header, bytes));
        Assert.Throws<InvalidDataException>(() => NomResourceMetadataCodec.Decode(new byte[67]));
    }
}
