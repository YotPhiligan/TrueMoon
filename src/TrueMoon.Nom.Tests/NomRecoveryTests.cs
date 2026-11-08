using System.Buffers.Binary;
using System.Security.Cryptography;
using Xunit;
using static TrueMoon.Nom.Tests.NomFormatFixtures;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomRecoveryTests
{
    [Fact]
    public void Recovery_IndependentGoldenJournal_IndexesExactValueLocationsWithoutWritingOrDisposing()
    {
        var bytes = Journal(BatchGolden); var saved = bytes.ToArray(); using var stream = new NomRecoveryProbeStream(bytes, maximumRead: 3);
        var result = NomJournalRecovery.Recover(stream, Header, sealedSegment: true, minimumSequence: 1);
        Assert.Equal(Header, result.Header); Assert.Equal(1, result.Sequence); Assert.Equal(1, result.Generation);
        Assert.Equal(345, result.ValidLength); Assert.False(result.HasIncompleteTail); Assert.Equal(2, result.KeyCount); Assert.Equal(194, result.IndexBytes);
        Assert.True(result.TryGet([1], out var first)); Assert.Equal(new NomValueLocation(269, 2, 1, 1), first);
        Assert.Equal(new byte[] { 16, 32 }, bytes.AsSpan((int)first.Offset, first.Length).ToArray());
        Assert.True(result.TryGet([127], out var empty)); Assert.Equal(new NomValueLocation(284, 0, 1, 1), empty);
        Assert.False(result.TryGet([128], out _)); Assert.False(result.TryGet([255], out _));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(saved)), result.SealedSegmentHash);
        Assert.Equal(saved, bytes); Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations); Assert.False(stream.WasDisposed);
        Assert.Throws<ArgumentNullException>(() => result.TryGet(null!, out _));
    }

    [Fact]
    public void Recovery_OverwriteDeleteAndMetadataOnlyBatch_PublishWholeNewView()
    {
        var next = Frame(2, 1, 1, new NomJournalMutation([1], [99]), new NomJournalMutation([2], [9]), new NomJournalMutation([127], null));
        var bytes = Journal(BatchGolden, next); using var stream = new NomRecoveryProbeStream(bytes);
        var result = NomJournalRecovery.Recover(stream, Header, minimumSequence: 2);
        Assert.Equal(2, result.Sequence); Assert.Equal(1, result.Generation); Assert.Equal(2, result.KeyCount); Assert.Equal(194, result.IndexBytes);
        Assert.True(result.TryGet([1], out var changed)); Assert.Equal(new NomValueLocation(454, 1, 2), changed);
        Assert.Equal(99, bytes[changed.Offset]);
        Assert.True(result.TryGet([2], out var added)); Assert.Equal(new NomValueLocation(468, 1, 2), added); Assert.Equal(9, bytes[added.Offset]);
        Assert.False(result.TryGet([127], out _)); Assert.Null(result.SealedSegmentHash); Assert.False(result.HasIncompleteTail);
        Assert.Equal(bytes.Length, result.ValidLength);
    }

    [Fact]
    public void Recovery_EmptyInitialJournal_PreservesIdentityWithoutInventingContent()
    {
        using var stream = new NomRecoveryProbeStream(HeaderGolden);
        var result = NomJournalRecovery.Recover(stream, Header, sealedSegment: true);
        Assert.Equal(0, result.Sequence); Assert.Equal(0, result.Generation); Assert.Equal(0, result.KeyCount); Assert.Equal(0, result.IndexBytes);
        Assert.Equal(160, result.ValidLength); Assert.False(result.HasIncompleteTail); Assert.False(result.TryGet([1], out _));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(HeaderGolden)), result.SealedSegmentHash);
    }

    [Fact]
    public void Recovery_EveryValidFinalFrameTruncation_RetainsOnlyPreviousWholeBatch()
    {
        var next = Frame(2, 1, 2, new NomJournalMutation([1], [77, 88]), new NomJournalMutation([127], null));
        for (var cut = 0; cut < next.Length; cut++)
        {
            var bytes = Journal(BatchGolden, next[..cut]); var saved = bytes.ToArray(); using var stream = new NomRecoveryProbeStream(bytes, maximumRead: 7);
            var result = NomJournalRecovery.Recover(stream, Header, minimumSequence: 1);
            Assert.Equal(1, result.Sequence); Assert.Equal(1, result.Generation); Assert.Equal(345, result.ValidLength);
            Assert.Equal(cut != 0, result.HasIncompleteTail); Assert.Equal(2, result.KeyCount); Assert.Equal(194, result.IndexBytes);
            Assert.True(result.TryGet([1], out var location)); Assert.Equal(new NomValueLocation(269, 2, 1), location);
            Assert.True(result.TryGet([127], out _)); Assert.Equal(saved, bytes);
            Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations); Assert.False(stream.WasDisposed);
            using var required = new MemoryStream(bytes, writable: false);
            Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(required, Header, minimumSequence: 2));
            if (cut != 0)
            {
                using var sealedStream = new MemoryStream(bytes, writable: false);
                Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(sealedStream, Header, sealedSegment: true));
            }
        }
        using var complete = new MemoryStream(Journal(BatchGolden, next), writable: false);
        var committed = NomJournalRecovery.Recover(complete, Header, sealedSegment: true, minimumSequence: 2);
        Assert.Equal(2, committed.Sequence); Assert.Equal(2, committed.Generation); Assert.Equal(1, committed.KeyCount);
        Assert.False(committed.HasIncompleteTail); Assert.False(committed.TryGet([127], out _));
    }

    [Theory]
    [InlineData("magic", 1)][InlineData("version", 9)][InlineData("flags", 13)]
    [InlineData("size", 20)][InlineData("count", 24)][InlineData("sequence", 32)]
    [InlineData("expected", 40)][InlineData("generation", 48)][InlineData("operation", 64)][InlineData("checksum", 96)]
    public void Recovery_InvalidIncompleteHeaderPrefix_IsNeverAcceptedAsHarmlessTail(string field, int prefix)
    {
        var next = Frame(2, 1, 2, new NomJournalMutation([1], [3]));
        switch (field)
        {
            case "magic": next[0] = 0; break; case "version": next[8] = 2; break; case "flags": next[12] = 1; break;
            case "size": BinaryPrimitives.WriteUInt32LittleEndian(next.AsSpan(16), uint.MaxValue); break;
            case "count": BinaryPrimitives.WriteInt32LittleEndian(next.AsSpan(20), 0); break;
            case "sequence": Counter(next, 24, 3); break; case "expected": Counter(next, 32, 0); break;
            case "generation": Counter(next, 40, 4); break; case "operation": next.AsSpan(48, 16).Clear(); break;
            case "checksum": next[64] ^= 1; break;
        }
        using var stream = new MemoryStream(Journal(BatchGolden, next[..prefix]), writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, Header));
    }

    [Theory]
    [InlineData("payload")][InlineData("footer")][InlineData("sequence-gap")][InlineData("sequence-repeat")]
    [InlineData("generation-conflict")][InlineData("impossible-count")]
    public void Recovery_CompleteCorruptionOrConflict_FailsInsteadOfReturningEarlierState(string failure)
    {
        var next = Frame(2, 1, 2, new NomJournalMutation([1], [9]));
        switch (failure)
        {
            case "payload": next[109] ^= 1; break; case "footer": next[^16] ^= 1; break;
            case "sequence-gap": Counter(next, 24, 3); AuthenticateBatch(next); break;
            case "sequence-repeat": Counter(next, 24, 1); Counter(next, 40, 1); AuthenticateBatch(next); break;
            case "generation-conflict": Counter(next, 32, 0); Counter(next, 40, 1); AuthenticateBatch(next); break;
            case "impossible-count": BinaryPrimitives.WriteInt32LittleEndian(next.AsSpan(20), 2); AuthenticateBatch(next); break;
        }
        using var stream = new NomRecoveryProbeStream(Journal(BatchGolden, next));
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, Header));
        Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations); Assert.False(stream.WasDisposed);
    }

    [Fact]
    public void Recovery_IndexBudget_AppliesAtomicDeleteBeforeAdmissionOfReplacement()
    {
        var first = Frame(1, 0, 1, new NomJournalMutation([255], [5])); var next = Frame(2, 1, 2, new NomJournalMutation([1], [7]), new NomJournalMutation([255], null));
        var limits = new NomFormatLimits { MaximumIndexKeys = 1, MaximumIndexBytes = 97 };
        using var stream = new MemoryStream(Journal(first, next), writable: false);
        var result = NomJournalRecovery.Recover(stream, Header, limits: limits);
        Assert.Equal(2, result.Sequence); Assert.Equal(2, result.Generation); Assert.Equal(1, result.KeyCount); Assert.Equal(97, result.IndexBytes);
        Assert.False(result.TryGet([255], out _)); Assert.True(result.TryGet([1], out var location)); Assert.Equal(2, location.Sequence);
        var addition = Frame(2, 1, 2, new NomJournalMutation([1], [7])); using var excessive = new MemoryStream(Journal(first, addition), writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(excessive, Header, limits: limits));
    }

    [Fact]
    public void Recovery_Limits_BoundSegmentIndexBytesAndReplayWithoutPartialSuccess()
    {
        var bytes = Journal(BatchGolden); var exact = new NomFormatLimits { MaximumSegmentBytes = 345, MaximumIndexKeys = 2, MaximumIndexBytes = 194, MaximumReplayBatches = 1 };
        using var accepted = new MemoryStream(bytes, writable: false); Assert.Equal(1, NomJournalRecovery.Recover(accepted, Header, limits: exact).Sequence);
        foreach (var limits in new[] { exact with { MaximumSegmentBytes = 344 }, exact with { MaximumIndexKeys = 1 }, exact with { MaximumIndexBytes = 193 } })
        {
            using var stream = new NomRecoveryProbeStream(bytes);
            Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, Header, limits: limits));
            Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations); Assert.False(stream.WasDisposed);
        }
        var next = Frame(2, 1, 2, new NomJournalMutation([1], [3])); using var tooMany = new MemoryStream(Journal(BatchGolden, next), writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(tooMany, Header, limits: new() { MaximumReplayBatches = 1 }));
    }

    [Fact]
    public void Recovery_SealedChain_CarriesOldLocationsAndKeepsPreviousViewImmutable()
    {
        var firstBytes = Journal(Frame(1, 0, 1, new NomJournalMutation([1], [2, 3]), new NomJournalMutation([2], [4]), new NomJournalMutation([3], [])));
        using var firstStream = new MemoryStream(firstBytes, writable: false);
        var first = NomJournalRecovery.Recover(firstStream, Header, sealedSegment: true);
        Assert.True(first.TryGet([1], out var old)); Assert.True(first.TryGet([3], out var retained));
        var header2 = Header with { SegmentId = 2, BaseSequence = 1, BaseGeneration = 1, PreviousSegmentHash = first.SealedSegmentHash! };
        var frame2 = NomJournalCodec.EncodeBatch(header2, Batch(2, 1, 1, new NomJournalMutation([1], [9]), new NomJournalMutation([2], null)));
        var secondBytes = Join(NomJournalCodec.EncodeHeader(header2), frame2); using var secondStream = new MemoryStream(secondBytes, writable: false);
        var second = NomJournalRecovery.Recover(secondStream, header2, sealedSegment: true, previous: first);
        Assert.Equal(2, second.Sequence); Assert.Equal(1, second.Generation); Assert.Equal(2, second.KeyCount); Assert.Equal(194, second.IndexBytes);
        Assert.True(second.TryGet([1], out var changed)); Assert.Equal(new NomValueLocation(269, 1, 2, 2), changed);
        Assert.True(second.TryGet([3], out var carried)); Assert.Equal(retained, carried); Assert.Equal(1, carried.SegmentId);
        Assert.False(second.TryGet([2], out _)); Assert.True(first.TryGet([2], out _));
        Assert.True(first.TryGet([1], out var stillOld)); Assert.Equal(old, stillOld);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(secondBytes)), second.SealedSegmentHash);
        var header3 = header2 with { SegmentId = 3, BaseSequence = 2, PreviousSegmentHash = second.SealedSegmentHash! };
        using var thirdStream = new MemoryStream(NomJournalCodec.EncodeHeader(header3), writable: false);
        var third = NomJournalRecovery.Recover(thirdStream, header3, previous: second);
        Assert.Equal(2, third.Sequence); Assert.Equal(2, third.KeyCount); Assert.True(third.TryGet([1], out var thirdLocation)); Assert.Equal(changed, thirdLocation);
    }

    [Theory]
    [InlineData("missing")][InlineData("active")][InlineData("project")][InlineData("store")]
    [InlineData("segment-gap")][InlineData("sequence")][InlineData("generation")][InlineData("digest")]
    [InlineData("seed-key-budget")][InlineData("seed-count-budget")][InlineData("seed-byte-budget")]
    public void Recovery_Chain_RejectsMissingUnsealedForeignOrMismatchedPredecessor(string mismatch)
    {
        var firstBytes = Journal(Frame(1, 0, 1, new NomJournalMutation([1, 2], [3]), new NomJournalMutation([4], [5])));
        using var firstStream = new MemoryStream(firstBytes, writable: false);
        var first = NomJournalRecovery.Recover(firstStream, Header, sealedSegment: mismatch != "active");
        var digest = Convert.ToHexStringLower(SHA256.HashData(firstBytes));
        var next = Header with { SegmentId = 2, BaseSequence = 1, BaseGeneration = 1, PreviousSegmentHash = digest };
        next = mismatch switch
        {
            "project" => next with { ProjectId = StoreId }, "store" => next with { StoreId = ProjectId },
            "segment-gap" => next with { SegmentId = 3 }, "sequence" => next with { BaseSequence = 2 },
            "generation" => next with { BaseGeneration = 0 }, "digest" => next with { PreviousSegmentHash = new string('a', 64) }, _ => next
        };
        var limits = mismatch switch
        {
            "seed-key-budget" => new NomFormatLimits { MaximumKeyBytes = 1 },
            "seed-count-budget" => new NomFormatLimits { MaximumIndexKeys = 1 },
            "seed-byte-budget" => new NomFormatLimits { MaximumIndexBytes = 194 }, _ => new NomFormatLimits()
        };
        using var stream = new MemoryStream(NomJournalCodec.EncodeHeader(next), writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, next, limits: limits, previous: mismatch == "missing" ? null : first));
        Assert.Equal(1, first.Sequence); Assert.Equal(2, first.KeyCount); Assert.Equal(195, first.IndexBytes);
    }

    [Theory]
    [InlineData("not-readable")][InlineData("not-seekable")][InlineData("position")][InlineData("truncated-header")]
    [InlineData("short-read-eof")][InlineData("invalid-read-count")][InlineData("changed-length")][InlineData("wrong-identity")]
    public void Recovery_InvalidOrChangingBorrowedStream_FailsWithoutCleanupSideEffects(string failure)
    {
        var bytes = Journal(BatchGolden); if (failure == "truncated-header") bytes = bytes[..159];
        using var stream = new NomRecoveryProbeStream(bytes)
        {
            Readable = failure != "not-readable", Seekable = failure != "not-seekable",
            EofAfterBytes = failure == "short-read-eof" ? 159 : int.MaxValue,
            InvalidReadCount = failure == "invalid-read-count", GrowReportedLength = failure == "changed-length"
        };
        if (failure == "position") stream.Position = 1;
        var expected = failure == "wrong-identity" ? Header with { StoreId = ProjectId } : Header;
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, expected));
        Assert.False(stream.WasDisposed); Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations);
    }

    [Fact]
    public void Recovery_CancellationBeforeAndDuringRead_PreservesCallerTokenAndBorrowedOwnership()
    {
        foreach (var duringRead in new[] { false, true })
        {
            using var cancel = new CancellationTokenSource(); using var stream = new NomRecoveryProbeStream(Journal(BatchGolden));
            if (duringRead) stream.AfterRead = reads => { if (reads == 2) cancel.Cancel(); }; else cancel.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() => NomJournalRecovery.Recover(stream, Header, token: cancel.Token));
            Assert.Equal(cancel.Token, error.CancellationToken); Assert.False(stream.WasDisposed); Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations);
            if (!duringRead) Assert.Equal(0, stream.Reads);
        }
        using var required = new MemoryStream(HeaderGolden, writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(required, Header, minimumSequence: 1));
        using var negative = new MemoryStream(HeaderGolden, writable: false);
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(negative, Header, minimumSequence: -1));
    }

    [Fact]
    public void Recovery_IoFailure_IsPropagatedWithoutTreatingItAsRecoverableTail()
    {
        foreach (var failOn in new[] { 1, 2, 3 })
        {
            var error = new IOException("Read failed."); using var stream = new NomRecoveryProbeStream(Journal(BatchGolden)) { ReadFailure = error, FailOnRead = failOn };
            Assert.Same(error, Assert.Throws<IOException>(() => NomJournalRecovery.Recover(stream, Header)));
            Assert.False(stream.WasDisposed); Assert.Equal(0, stream.Writes); Assert.Equal(0, stream.Truncations);
        }
    }

    [Fact]
    public void Recovery_FrameBudget_IsCheckedBeforeReadingVariablePayload()
    {
        using var stream = new NomRecoveryProbeStream(Journal(BatchGolden))
        { ReadFailure = new IOException("Body must not be read before budget validation."), FailOnRead = 3 };
        Assert.Throws<InvalidDataException>(() => NomJournalRecovery.Recover(stream, Header, limits: new() { MaximumPayloadBytes = 13 }));
        Assert.Equal(2, stream.Reads); Assert.False(stream.WasDisposed);
    }
}
