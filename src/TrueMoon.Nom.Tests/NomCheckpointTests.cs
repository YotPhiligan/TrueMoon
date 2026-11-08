using Xunit;
using static TrueMoon.Nom.Tests.NomCheckpointFixtures;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomCheckpointTests
{
    [Fact]
    public void Write_IndependentGoldenVector_PinsWireOrderEmptyValueAndWholeFileDigest()
    {
        using var output = new NomCheckpointProbeStream();
        var digest = NomCheckpointCodec.Write(output, Header, Entries);
        Assert.Equal(Golden, output.ToArray()); Assert.Equal(193, output.Length); Assert.Equal(GoldenDigest, digest);
        Assert.False(output.WasDisposed); Assert.True(output.CanWrite);
    }

    [Fact]
    public void Read_IndependentGoldenVector_ProducesDetachedIndexWithCheckpointLocations()
    {
        using var input = new NomCheckpointProbeStream(Golden) { ReadChunk = 2 };
        var result = NomCheckpointCodec.Read(input, Root); Assert.Equal(Header, result.Header); Assert.Equal(193, result.Length); Assert.Equal(292, result.IndexBytes);
        var index = result.CopyEntries(2); Assert.Equal(3, index.Count);
        Assert.Equal(new NomValueLocation(137, 2, 7, 0), index[[1]]); Assert.Equal(new NomValueLocation(148, 0, 7, 0), index[[0x7f]]);
        Assert.Equal(new NomValueLocation(158, 3, 7, 0), index[[0x80, 0]]); Assert.False(input.WasDisposed); Assert.Equal(0, input.Writes);
        index.Keys.First()[0] = 0xff; Assert.True(result.CopyEntries(2).ContainsKey([1]));
        Assert.Throws<InvalidDataException>(() => result.CopyEntries(1));
    }

    [Theory]
    [InlineData("magic")][InlineData("version")][InlineData("size")][InlineData("flags")]
    [InlineData("reserved-first")][InlineData("reserved-last")][InlineData("project-empty")][InlineData("store-empty")]
    [InlineData("epoch-one")][InlineData("sequence-zero")][InlineData("generation-future")][InlineData("sequence-overflow")]
    [InlineData("previous-zero")][InlineData("previous-max")][InlineData("hash-zero")][InlineData("count-overflow")]
    [InlineData("key-zero")][InlineData("key-overflow")][InlineData("value-overflow")][InlineData("duplicate")][InlineData("unsorted")]
    public void Read_AuthenticatedMalformedHeaderOrRecord_IsRejectedBeyondChecksum(string invalid)
    {
        var bytes = Golden;
        switch (invalid)
        {
            case "magic": bytes[0] ^= 1; break; case "version": bytes[8] = 2; break; case "size": bytes[10] = 127; break;
            case "flags": bytes[12] = 1; break; case "reserved-first": bytes[116] = 1; break; case "reserved-last": bytes[127] = 1; break;
            case "project-empty": Array.Clear(bytes, 16, 16); break; case "store-empty": Array.Clear(bytes, 32, 16); break;
            case "epoch-one": Counter(bytes, 48, 1); break; case "sequence-zero": Counter(bytes, 56, 0); break;
            case "generation-future": Counter(bytes, 64, 8); break; case "sequence-overflow": bytes[63] = 0x80; break;
            case "previous-zero": Counter(bytes, 72, 0); break; case "previous-max": Counter(bytes, 72, long.MaxValue); break;
            case "hash-zero": Array.Clear(bytes, 80, 32); break; case "count-overflow": Size(bytes, 112, uint.MaxValue); break;
            case "key-zero": Size(bytes, 128, 0); break; case "key-overflow": Size(bytes, 128, uint.MaxValue); break;
            case "value-overflow": Size(bytes, 132, uint.MaxValue); break; case "duplicate": bytes[147] = 1; break;
            case "unsorted": bytes[147] = 0; break;
        }
        var expected = Authenticate(bytes); using var input = new NomCheckpointProbeStream(bytes);
        Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(input, expected)); Assert.False(input.WasDisposed); Assert.Equal(0, input.Writes);
    }

    [Theory]
    [InlineData("project")][InlineData("store")][InlineData("epoch")][InlineData("sequence")]
    [InlineData("generation")][InlineData("segment")][InlineData("previous-hash")][InlineData("content-address")]
    public void Read_ValidCheckpointWithDifferentRootAuthority_IsRejected(string field)
    {
        var expected = field switch
        {
            "project" => Root with { ProjectId = Root.StoreId }, "store" => Root with { StoreId = Root.ProjectId },
            "epoch" => Root with { Epoch = 3 }, "sequence" => Root with { CheckpointSequence = 8 },
            "generation" => Root with { CheckpointGeneration = 4 }, "segment" => Root with { FirstSegmentId = 7 },
            "previous-hash" => Root with { PreviousSegmentHash = new string('a', 64) }, _ => Root with { CheckpointDigest = new string('a', 64) }
        };
        using var input = new NomCheckpointProbeStream(Golden); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(input, expected));
        Assert.Equal(0, input.Writes); Assert.False(input.WasDisposed);
    }

    [Fact]
    public void Read_EveryTruncationFooterCorruptionAndTrailingByte_IsRejectedWithoutMutatingInput()
    {
        for (var length = 0; length < Golden.Length; length++)
        {
            using var input = new NomCheckpointProbeStream(Golden[..length]);
            Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(input, Root)); Assert.Equal(0, input.Writes); Assert.False(input.WasDisposed);
        }
        var corrupt = Golden; corrupt[^1] ^= 1;
        using var damaged = new MemoryStream(corrupt);
        Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(damaged, Root with { CheckpointDigest = Digest(corrupt) }));
        var trailing = new byte[Golden.Length + 1]; Golden.CopyTo(trailing, 0); var expected = Authenticate(trailing);
        using var extra = new MemoryStream(trailing); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(extra, expected));
    }

    [Theory]
    [InlineData("key")][InlineData("value")][InlineData("count")][InlineData("index")][InlineData("checkpoint")]
    public void Codec_ExactLimitsSucceedAndOneBelowEachBudgetRejects(string quota)
    {
        var exact = new NomFormatLimits { MaximumKeyBytes = 2, MaximumValueBytes = 3, MaximumIndexKeys = 3, MaximumIndexBytes = 292, MaximumCheckpointBytes = 193 };
        using var validOutput = new MemoryStream(); Assert.Equal(GoldenDigest, NomCheckpointCodec.Write(validOutput, Header, Entries, exact));
        using var validInput = new MemoryStream(Golden); Assert.Equal(3, NomCheckpointCodec.Read(validInput, Root, exact).CopyEntries(2).Count);
        var shortLimit = quota switch
        {
            "key" => exact with { MaximumKeyBytes = 1 }, "value" => exact with { MaximumValueBytes = 2 },
            "count" => exact with { MaximumIndexKeys = 2 }, "index" => exact with { MaximumIndexBytes = 291 }, _ => exact with { MaximumCheckpointBytes = 192 }
        };
        using var output = new MemoryStream(); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Write(output, Header, Entries, shortLimit));
        using var input = new MemoryStream(Golden); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(input, Root, shortLimit));
    }

    [Theory]
    [InlineData("checkpoint-small")][InlineData("checkpoint-large")][InlineData("maintenance-small")][InlineData("maintenance-large")]
    public void Codec_InvalidGlobalMaintenanceLimits_RejectBeforeBorrowedIo(string invalid)
    {
        var limits = invalid switch
        {
            "checkpoint-small" => new NomFormatLimits { MaximumCheckpointBytes = 159 }, "checkpoint-large" => new NomFormatLimits { MaximumCheckpointBytes = 268435457 },
            "maintenance-small" => new NomFormatLimits { MaximumMaintenanceBytes = 575 }, _ => new NomFormatLimits { MaximumMaintenanceBytes = 2415919361 }
        };
        using var output = new NomCheckpointProbeStream(); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Write(output, Header, Entries, limits)); Assert.Equal(0, output.Writes);
        using var input = new NomCheckpointProbeStream(Golden); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Read(input, Root, limits)); Assert.Equal(0, input.Reads);
    }

    [Theory]
    [InlineData("duplicate")][InlineData("unsorted")][InlineData("missing")][InlineData("extra")]
    public void Write_InvalidEnumerationClosure_IsRejected(string invalid)
    {
        var rows = invalid switch
        {
            "duplicate" => new NomKvPair[] { Entries[0], Entries[0], Entries[2] }, "unsorted" => Entries.Reverse().ToArray(),
            "missing" => Entries[..2], _ => [.. Entries, new([0xff], [])]
        };
        using var output = new NomCheckpointProbeStream(); Assert.Throws<InvalidDataException>(() => NomCheckpointCodec.Write(output, Header, rows)); Assert.False(output.WasDisposed);
    }

    [Fact]
    public void Codec_EmptyLiveMapPreservesCountersAndMaximumValueUsesBoundedReadBuffer()
    {
        var header = Header with { EntryCount = 0 }; using var empty = new MemoryStream(); var digest = NomCheckpointCodec.Write(empty, header, []);
        Assert.Equal(160, empty.Length); empty.Position = 0; var result = NomCheckpointCodec.Read(empty, Root with { CheckpointDigest = digest });
        Assert.Equal(header, result.Header); Assert.Empty(result.CopyEntries(1)); Assert.Equal(0, result.IndexBytes);
        var value = Enumerable.Range(0, 131072).Select(i => (byte)(i % 251)).ToArray(); using var large = new MemoryStream();
        var largeDigest = NomCheckpointCodec.Write(large, Header with { EntryCount = 1 }, [new([1], value)]);
        using var input = new NomCheckpointProbeStream(large.ToArray()); var read = NomCheckpointCodec.Read(input, Root with { CheckpointDigest = largeDigest });
        Assert.Equal(new NomValueLocation(137, 131072, 7, 0), Assert.Single(read.CopyEntries(1)).Value); Assert.True(input.MaximumRequest <= 65536);
        input.Position = 137; var copy = new byte[131072]; input.ReadExactly(copy); Assert.Equal(value, copy); Assert.False(input.WasDisposed);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Codec_CancellationBeforeOrDuringIo_LeavesBorrowedStreamsOpen(bool during)
    {
        using var cancelWrite = new CancellationTokenSource(); using var output = new NomCheckpointProbeStream();
        if (!during) cancelWrite.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => NomCheckpointCodec.Write(output, Header, Entries, write: bytes =>
        { output.Write(bytes.Span); if (during) cancelWrite.Cancel(); }, token: cancelWrite.Token));
        Assert.Equal(during ? 128 : 0, output.Length); Assert.False(output.WasDisposed);
        using var cancelRead = new CancellationTokenSource(); using var input = new NomCheckpointProbeStream(Golden);
        if (during) input.AfterRead = cancelRead.Cancel; else cancelRead.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => NomCheckpointCodec.Read(input, Root, token: cancelRead.Token));
        Assert.Equal(during ? 1 : 0, input.Reads); Assert.False(input.WasDisposed); Assert.Equal(0, input.Writes);
    }

    [Theory]
    [InlineData("count")][InlineData("index")][InlineData("key")]
    public void Recovery_CheckpointSeedRespectsNewlyReducedIndexLimitsBeforeReturningState(string quota)
    {
        using var checkpointFile = new MemoryStream(Golden); var checkpoint = NomCheckpointCodec.Read(checkpointFile, Root);
        var header = new NomJournalHeader(Root.ProjectId, Root.StoreId, 6, 7, 3, PreviousDigest);
        var limits = quota switch
        {
            "count" => new NomFormatLimits { MaximumIndexKeys = 2 }, "index" => new NomFormatLimits { MaximumIndexBytes = 291 },
            _ => new NomFormatLimits { MaximumKeyBytes = 1 }
        };
        using var journal = new NomRecoveryProbeStream(NomJournalCodec.EncodeHeader(header));
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(journal, header, limits: limits, checkpoint: checkpoint));
        Assert.Equal(0, journal.Writes); Assert.Equal(0, journal.Truncations); Assert.False(journal.WasDisposed);
    }

    [Theory]
    [InlineData("project")][InlineData("store")][InlineData("segment")][InlineData("sequence")][InlineData("generation")][InlineData("hash")]
    public void Recovery_CheckpointSeedMustAnchorExactSuccessorIdentityAndCounters(string invalid)
    {
        using var checkpointFile = new MemoryStream(Golden); var checkpoint = NomCheckpointCodec.Read(checkpointFile, Root);
        var header = new NomJournalHeader(Root.ProjectId, Root.StoreId, 6, 7, 3, PreviousDigest);
        header = invalid switch
        {
            "project" => header with { ProjectId = Root.StoreId }, "store" => header with { StoreId = Root.ProjectId },
            "segment" => header with { SegmentId = 7 }, "sequence" => header with { BaseSequence = 8 },
            "generation" => header with { BaseGeneration = 4 }, _ => header with { PreviousSegmentHash = new string('a', 64) }
        };
        using var journal = new NomRecoveryProbeStream(NomJournalCodec.EncodeHeader(header));
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(journal, header, checkpoint: checkpoint));
        Assert.Equal(0, journal.Writes); Assert.False(journal.WasDisposed);
    }

    [Fact]
    public void Recovery_CheckpointThenSealedAndActiveSegments_PreserveLocationsDeletesAndSeedExclusivity()
    {
        using var checkpointFile = new MemoryStream(Golden); var checkpoint = NomCheckpointCodec.Read(checkpointFile, Root);
        var firstHeader = new NomJournalHeader(Root.ProjectId, Root.StoreId, 6, 7, 3, PreviousDigest);
        var frame = NomJournalCodec.EncodeBatch(firstHeader, NomFormatFixtures.Batch(8, 3, 4,
            new NomJournalMutation([1], null), new NomJournalMutation([2], [0x22])));
        var firstBytes = NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(firstHeader), frame); using var firstFile = new NomRecoveryProbeStream(firstBytes);
        var first = NomJournalRecovery.Recover(firstFile, firstHeader, sealedSegment: true, checkpoint: checkpoint);
        var secondHeader = new NomJournalHeader(Root.ProjectId, Root.StoreId, 7, 8, 4, Digest(firstBytes));
        var secondFrame = NomJournalCodec.EncodeBatch(secondHeader, NomFormatFixtures.Batch(9, 4, 4,
            new NomJournalMutation([3], [0x33]), new NomJournalMutation([0x80, 0], [0x55])));
        var secondBytes = NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(secondHeader), secondFrame); using var secondFile = new NomRecoveryProbeStream(secondBytes);
        var state = NomJournalRecovery.Recover(secondFile, secondHeader, previous: first);
        Assert.Equal(9, state.Sequence); Assert.Equal(4, state.Generation); Assert.Equal(4, state.KeyCount); Assert.False(state.TryGet([1], out _));
        Assert.True(state.TryGet([0x7f], out var old)); Assert.Equal(new NomValueLocation(148, 0, 7, 0), old);
        Assert.True(state.TryGet([2], out var middle)); Assert.Equal(6, middle.SegmentId); Assert.Equal(8, middle.Sequence); Assert.Equal(1, middle.Length);
        Assert.True(state.TryGet([0x80, 0], out var latest)); Assert.Equal(7, latest.SegmentId); Assert.Equal(9, latest.Sequence); Assert.Equal(1, latest.Length);
        using var ambiguous = new MemoryStream(secondBytes);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(ambiguous, secondHeader, previous: first, checkpoint: checkpoint));
        Assert.Equal(0, firstFile.Writes); Assert.Equal(0, secondFile.Writes); Assert.False(firstFile.WasDisposed); Assert.False(secondFile.WasDisposed);
    }
}
