using System.Security.Cryptography;
using Xunit;
using static TrueMoon.Nom.Tests.NomKvTestData;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomKvStoreTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Create_InitialAuthorityAndExclusiveLock_AreReleasedOnlyByActualClose(bool existingEmptyRoot)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments();
        if (existingEmptyRoot) Directory.CreateDirectory(args.Root);
        var store = context.Factory.Create(args);
        Assert.Equal(args.Root, store.Root); Assert.Equal(args.ProjectId, store.ProjectId); Assert.Equal(args.StoreId, store.StoreId);
        Assert.Equal(0, store.Sequence); Assert.Equal(0, store.Generation); Assert.Equal(0, store.KeyCount); Assert.Equal(0, store.IndexBytes);
        Assert.False(store.IsClosed); Assert.False(store.IsFaulted); Assert.Equal(1, context.Factory.ActiveStoreCount);
        Assert.Equal(NomFormatFixtures.RootGolden, NomKvTestDirectory.ReadBytes(Path.Combine(args.Root, "nom.root")));
        Assert.Equal(NomFormatFixtures.HeaderGolden, NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root)));
        Assert.False(File.Exists(Path.Combine(args.Root, "nom.root.pending"))); NomKvTestDirectory.AssertLocked(args.Root);
        using var other = new NomKvStoreFactory(); Assert.Throws<IOException>(() => other.Open(args)); Assert.Equal(0, other.ActiveStoreCount);
        store.Dispose(); store.Dispose(); Assert.True(store.IsClosed); Assert.Equal(0, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertReleased(args.Root);
        Assert.Throws<ObjectDisposedException>(() => store.Get([1]));
        Assert.Throws<ObjectDisposedException>(() => store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], [2]))));
        using var reopened = context.Factory.Open(args); Assert.Equal(0, reopened.Sequence); Assert.Null(reopened.Get([1]));
    }

    [Fact]
    public void WriteBatch_GetAndReopen_PreserveExactValuesGenerationsAndPhysicalTickets()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        var key = new byte[] { 1 }; var value = new byte[] { 10, 11 }; var firstBatch = Batch(1, 0, 1, new NomJournalMutation(key, value), new NomJournalMutation([2], []));
        key[0] = 255; value[0] = 255; var first = store.WriteBatch(firstBatch);
        Assert.Equal(1, store.Sequence); Assert.Equal(1, first.Generation); Assert.Equal(1, store.Generation); Assert.Equal(2, store.KeyCount); Assert.Equal(194, store.IndexBytes);
        Assert.Equal(args.ProjectId, first.Ticket.ProjectId); Assert.Equal(args.StoreId, first.Ticket.StoreId); Assert.Equal(1, first.Ticket.Sequence); Assert.Equal(Operation, first.Ticket.OperationId);
        var raw = NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(raw.AsSpan(160))), first.Ticket.FrameDigest);
        Assert.Equal(NomKvWriteOutcome.Committed, store.LookupOutcome(first.Ticket));
        var bytes = store.Get([1])!; Assert.Equal(new byte[] { 10, 11 }, bytes); bytes[0] = 99;
        Assert.Equal(new byte[] { 10, 11 }, store.Get([1])); Assert.Empty(store.Get([2])!); Assert.Null(store.Get([3]));
        var view = store.GetMany([new byte[] { 2 }, new byte[] { 3 }, new byte[] { 1 }, new byte[] { 1 }]);
        Assert.Equal(1, view.Sequence); Assert.Equal(1, view.Generation); Assert.Equal(4, view.Values.Count);
        Assert.Empty(view.Values[0]!); Assert.Null(view.Values[1]); Assert.Equal(new byte[] { 10, 11 }, view.Values[2]);
        view.Values[2]![0] = 77; Assert.Equal(new byte[] { 10, 11 }, view.Values[3]); Assert.Equal(new byte[] { 10, 11 }, store.Get([1]));
        var second = store.WriteBatch(Batch(2, 1, 1, new NomJournalMutation([1], [20]), new NomJournalMutation([2], null), new NomJournalMutation([3], [30])));
        Assert.Equal(2, second.Ticket.Sequence); Assert.Equal(Operation, second.Ticket.OperationId); Assert.Equal(1, second.Generation);
        Assert.Equal(NomKvWriteOutcome.Committed, store.LookupOutcome(first.Ticket)); Assert.Equal(NomKvWriteOutcome.Committed, store.LookupOutcome(second.Ticket));
        store.Dispose(); using var reopened = context.Factory.Open(args);
        Assert.Equal(2, reopened.Sequence); Assert.Equal(1, reopened.Generation); Assert.Equal(2, reopened.KeyCount); Assert.Equal(194, reopened.IndexBytes);
        Assert.Equal(new byte[] { 20 }, reopened.Get([1])); Assert.Null(reopened.Get([2])); Assert.Equal(new byte[] { 30 }, reopened.Get([3]));
        Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(first.Ticket)); Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(second.Ticket));
        Assert.Equal(new byte[] { 10, 11 }, view.Values[3]);
    }

    [Theory]
    [InlineData("null-root")][InlineData("empty-root")][InlineData("relative")][InlineData("unc")][InlineData("forward-unc")]
    [InlineData("volume")][InlineData("project")][InlineData("store")][InlineData("limit")][InlineData("cancel")]
    public void Factory_InvalidArguments_RejectBeforeIoOrAdmission(string invalid)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var bad = invalid switch
        {
            "null-root" => args with { Root = null! }, "empty-root" => args with { Root = " " }, "relative" => args with { Root = "relative/nom" },
            "unc" => args with { Root = @"\\server\share\nom" }, "forward-unc" => args with { Root = "//server/share/nom" },
            "volume" => args with { Root = Path.GetPathRoot(args.Root)! }, "project" => args with { ProjectId = Guid.Empty },
            "store" => args with { StoreId = Guid.Empty }, "limit" => args with { Limits = new() { MaximumKeyBytes = 0 } }, _ => args with { Token = cancel.Token }
        };
        var error = Record.Exception(() => context.Factory.Create(bad)); Assert.NotNull(error);
        if (invalid == "cancel") Assert.IsType<OperationCanceledException>(error);
        else if (invalid == "limit") Assert.IsType<InvalidDataException>(error);
        else Assert.IsAssignableFrom<ArgumentException>(error);
        Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.Empty(context.Io.Events); Assert.False(Directory.Exists(args.Root));
        Assert.Throws<ArgumentNullException>(() => context.Factory.Create(null!));
    }

    [Theory]
    [InlineData("wrong-project")][InlineData("wrong-store")][InlineData("root-version")][InlineData("root-corruption")]
    [InlineData("root-missing")][InlineData("journal-missing")][InlineData("lock-missing")][InlineData("journal-corruption")][InlineData("legacy")]
    [InlineData("minimum-sequence")][InlineData("checkpoint")]
    public void Open_InvalidExistingStore_DoesNotResetRepairOrReplaceAnyData(string invalid)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store); store.Dispose();
        var open = args; var rootFile = Path.Combine(args.Root, "nom.root"); var journal = NomKvTestDirectory.Journal(args.Root);
        switch (invalid)
        {
            case "wrong-project": open = args with { ProjectId = args.StoreId }; break; case "wrong-store": open = args with { StoreId = args.ProjectId }; break;
            case "root-version": var version = NomKvTestDirectory.ReadBytes(rootFile); version[8] = 2; NomFormatFixtures.AuthenticateRoot(version); File.WriteAllBytes(rootFile, version); break;
            case "root-corruption": var root = NomKvTestDirectory.ReadBytes(rootFile); root[128] ^= 1; File.WriteAllBytes(rootFile, root); break;
            case "root-missing": File.Delete(rootFile); break; case "journal-missing": File.Delete(journal); break;
            case "lock-missing": File.Delete(Path.Combine(args.Root, "owner.lock")); break;
            case "journal-corruption": var data = NomKvTestDirectory.ReadBytes(journal); data[^1] ^= 1; File.WriteAllBytes(journal, data); break;
            case "legacy": File.WriteAllBytes(Path.Combine(args.Root, "catalog.litedb"), [1, 2, 3]); break;
            case "minimum-sequence": File.WriteAllBytes(rootFile, NomRootCodec.Encode(new(args.ProjectId, args.StoreId, MinimumSequence: 2))); break;
            case "checkpoint": File.WriteAllBytes(rootFile, NomRootCodec.Encode(new(args.ProjectId, args.StoreId, CheckpointSequence: 1,
                CheckpointGeneration: 1, CheckpointDigest: new string('a', 64), FirstSegmentId: 2, ActiveSegmentId: 2,
                MinimumSequence: 1, PreviousSegmentHash: new string('b', 64)))); break;
        }
        var snapshot = NomKvTestDirectory.Snapshot(args.Root); var error = Record.Exception(() => context.Factory.Open(open)); Assert.NotNull(error);
        if (invalid == "checkpoint") Assert.IsAssignableFrom<IOException>(error);
        else if (invalid.EndsWith("missing", StringComparison.Ordinal)) Assert.IsAssignableFrom<IOException>(error);
        else Assert.IsType<InvalidDataException>(error);
        Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed));
        if (File.Exists(Path.Combine(args.Root, "owner.lock"))) NomKvTestDirectory.AssertReleased(args.Root);
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point == "truncate"); NomKvTestDirectory.AssertSnapshot(snapshot, args.Root);
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(args)); NomKvTestDirectory.AssertSnapshot(snapshot, args.Root);
    }

    [Fact]
    public void Factory_MissingOpenCapacityAndCanonicalRoot_KeepOwnershipExplicit()
    {
        using var first = new NomKvTestContext(maximumOwners: 1); using var second = new NomKvTestDirectory(); var args = first.Directory.Arguments();
        Assert.Throws<DirectoryNotFoundException>(() => first.Factory.Open(args)); Assert.False(Directory.Exists(args.Root)); Assert.Equal(0, first.Factory.ActiveStoreCount);
        var store = first.Factory.Create(args with { Root = args.Root + Path.DirectorySeparatorChar + "." + Path.DirectorySeparatorChar });
        Assert.Equal(args.Root, store.Root); Assert.Throws<InvalidOperationException>(() => first.Factory.Create(second.Arguments())); Assert.False(Directory.Exists(second.Root));
        store.Dispose(); using var next = first.Factory.Create(second.Arguments()); Assert.Equal(1, first.Factory.ActiveStoreCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new NomKvStoreFactory(maximumOwners: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NomKvStoreFactory(maximumOwners: 5));
    }

    [Theory]
    [InlineData("sequence-zero")][InlineData("sequence-gap")][InlineData("old-sequence")][InlineData("stale-generation")]
    [InlineData("generation-gap")][InlineData("duplicate")][InlineData("empty-batch")][InlineData("key")][InlineData("value")][InlineData("cancel")]
    public void WriteBatch_InvalidWork_IsRejectedBeforeIoAndLeavesOwnerUsable(string invalid)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(new() { MaximumKeyBytes = 2, MaximumValueBytes = 2 });
        using var store = context.Factory.Create(args); Seed(store); var original = NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root)); context.Io.Events.Clear();
        var batch = invalid switch
        {
            "sequence-zero" => Batch(0, 1, 1, new NomJournalMutation([1], [2])), "sequence-gap" => Batch(3, 1, 2, new NomJournalMutation([1], [2])),
            "old-sequence" => Batch(1, 1, 1, new NomJournalMutation([1], [2])), "stale-generation" => Batch(2, 0, 1, new NomJournalMutation([1], [2])),
            "generation-gap" => Batch(2, 1, 3, new NomJournalMutation([1], [2])), "duplicate" => Batch(2, 1, 2, new NomJournalMutation([1], [2]), new NomJournalMutation([1], null)),
            "empty-batch" => Batch(2, 1, 2), "key" => Batch(2, 1, 2, new NomJournalMutation([1, 2, 3], [])),
            "value" => Batch(2, 1, 2, new NomJournalMutation([1], [2, 3, 4])), _ => Batch(2, 1, 2, new NomJournalMutation([1], [2]))
        };
        using var cancel = new CancellationTokenSource(); if (invalid == "cancel") cancel.Cancel();
        if (invalid == "cancel") Assert.Throws<OperationCanceledException>(() => store.WriteBatch(batch, cancel.Token));
        else Assert.Throws<InvalidDataException>(() => store.WriteBatch(batch));
        Assert.Empty(context.Io.Events); Assert.Equal(original, NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root)));
        Assert.Equal(1, store.Sequence); Assert.Equal(1, store.Generation); Assert.False(store.IsFaulted); Assert.Equal(new byte[] { 10, 11 }, store.Get([1]));
        store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [2]))); Assert.Equal(new byte[] { 2 }, store.Get([1]));
    }

    [Fact]
    public void Reads_RejectInvalidOrOversizedRequests_WithoutPoisoningOwner()
    {
        using var context = new NomKvTestContext(); using var store = context.Factory.Create(context.Directory.Arguments());
        var large = Enumerable.Repeat((byte)0xA5, 128 * 1024).ToArray(); store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], large)));
        var exact = store.GetMany(Enumerable.Range(0, 8).Select(_ => new byte[] { 1 }).ToArray());
        Assert.Equal(8, exact.Values.Count); Assert.All(exact.Values, value => Assert.Equal(large, value));
        Assert.Throws<InvalidDataException>(() => store.GetMany(Enumerable.Range(0, 9).Select(_ => new byte[] { 1 }).ToArray()));
        Assert.Throws<InvalidDataException>(() => store.GetMany(Array.Empty<byte[]>()));
        Assert.Equal(256, store.GetMany(Enumerable.Range(0, 256).Select(_ => new byte[] { 255 }).ToArray()).Values.Count);
        Assert.Throws<InvalidDataException>(() => store.GetMany(Enumerable.Range(0, 257).Select(_ => new byte[] { 255 }).ToArray()));
        Assert.Throws<ArgumentNullException>(() => store.Get(null!)); Assert.Throws<ArgumentNullException>(() => store.GetMany(null!));
        Assert.Throws<InvalidDataException>(() => store.Get([])); Assert.Throws<InvalidDataException>(() => store.Get(new byte[1025]));
        Assert.False(store.IsFaulted); Assert.Equal(1, store.Sequence); Assert.Equal(large, store.Get([1]));
    }

    [Fact]
    public void Tickets_AreExactPhysicalCommits_AndNeverAutomaticallyReplayOperationIds()
    {
        using var context = new NomKvTestContext(); using var store = context.Factory.Create(context.Directory.Arguments()); var first = Seed(store);
        Assert.Throws<InvalidDataException>(() => store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], [99]))));
        Assert.Equal(NomKvWriteOutcome.NotCommitted, store.LookupOutcome(first.Ticket with { Sequence = 2 }));
        Assert.Equal(NomKvWriteOutcome.NotCommitted, store.LookupOutcome(first.Ticket with { OperationId = Guid.Parse("1ff9c98b-31f6-4a12-b18c-5c737a665026") }));
        Assert.Equal(NomKvWriteOutcome.NotCommitted, store.LookupOutcome(first.Ticket with { FrameDigest = new string('a', 64) }));
        Assert.Throws<InvalidDataException>(() => store.LookupOutcome(first.Ticket with { StoreId = first.Ticket.ProjectId }));
        Assert.Throws<InvalidDataException>(() => store.LookupOutcome(first.Ticket with { Sequence = 0 }));
        Assert.Throws<InvalidDataException>(() => store.LookupOutcome(first.Ticket with { FrameDigest = "aa" }));
        var next = store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [99])));
        Assert.Equal(first.Ticket.OperationId, next.Ticket.OperationId); Assert.NotEqual(first.Ticket.FrameDigest, next.Ticket.FrameDigest);
        Assert.Equal(NomKvWriteOutcome.Committed, store.LookupOutcome(next.Ticket)); Assert.Equal(2, store.Sequence); Assert.Equal(new byte[] { 99 }, store.Get([1]));
    }

    [Theory]
    [InlineData("segment")][InlineData("replay")][InlineData("index-count")][InlineData("index-bytes")]
    public void WriteBatch_CapacityRefusal_HappensBeforeIoAndPreservesReadableState(string quota)
    {
        using var context = new NomKvTestContext(); var first = Batch(1, 0, 1, new NomJournalMutation([1], [3]));
        var length = 160 + NomJournalCodec.EncodeBatch(NomFormatFixtures.Header, first).Length;
        NomFormatLimits limits = quota switch
        {
            "segment" => new NomFormatLimits { MaximumSegmentBytes = length }, "replay" => new() { MaximumReplayBatches = 1 },
            "index-count" => new() { MaximumIndexKeys = 1 }, _ => new() { MaximumIndexBytes = 97 }
        };
        using var store = context.Factory.Create(context.Directory.Arguments(limits)); store.WriteBatch(first); context.Io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([2], [4]))));
        Assert.Empty(context.Io.Events); Assert.False(store.IsFaulted); Assert.Equal(1, store.Sequence); Assert.Equal(1, store.KeyCount); Assert.Equal(97, store.IndexBytes);
        Assert.Equal(new byte[] { 3 }, store.Get([1])); Assert.Null(store.Get([2]));
    }

    [Fact]
    public void Open_ValidIncompleteTail_TruncatesOnlyUncommittedBytesThenAllowsAppend()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); var seed = Seed(store); store.Dispose();
        var journal = NomKvTestDirectory.Journal(args.Root); var committedLength = new FileInfo(journal).Length; var next = NextFrame();
        using (var append = new FileStream(journal, FileMode.Append)) append.Write(next.AsSpan(0, 100));
        context.Io.Events.Clear(); var reopened = context.Factory.Open(args);
        Assert.Equal(100, reopened.RecoveredTailBytes); Assert.Equal(committedLength, new FileInfo(journal).Length);
        Assert.Single(context.Io.Events, entry => entry.Point == "truncate"); Assert.Single(context.Io.Events, entry => entry.Point == "flush-journal");
        Assert.Equal(1, reopened.Sequence); Assert.Equal(new byte[] { 10, 11 }, reopened.Get([1])); Assert.Empty(reopened.Get([2])!);
        Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(seed.Ticket));
        var receipt = reopened.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [20]))); reopened.Dispose();
        using var again = context.Factory.Open(args); Assert.Equal(0, again.RecoveredTailBytes); Assert.Equal(2, again.Sequence);
        Assert.Equal(new byte[] { 20 }, again.Get([1])); Assert.Equal(NomKvWriteOutcome.Committed, again.LookupOutcome(receipt.Ticket));
    }

    [Fact]
    public void Open_RootListedSealedChain_UsesAllSegmentsAndIgnoresUnlistedFiles()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); var first = Seed(store); store.Dispose();
        var firstBytes = NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root));
        var header = new NomJournalHeader(args.ProjectId, args.StoreId, 2, 1, 1, Convert.ToHexStringLower(SHA256.HashData(firstBytes)));
        var frame = NomJournalCodec.EncodeBatch(header, Batch(2, 1, 2, new NomJournalMutation([3], [30])));
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 2), NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(header), frame));
        File.WriteAllBytes(Path.Combine(args.Root, "nom.root"), NomRootCodec.Encode(new(args.ProjectId, args.StoreId, ActiveSegmentId: 2, MinimumSequence: 2)));
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 99), [0xFF]);
        using var reopened = context.Factory.Open(args); Assert.Equal(2, reopened.Sequence); Assert.Equal(2, reopened.Generation);
        Assert.Equal(new byte[] { 10, 11 }, reopened.Get([1])); Assert.Empty(reopened.Get([2])!); Assert.Equal(new byte[] { 30 }, reopened.Get([3]));
        Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(first.Ticket));
        reopened.WriteBatch(Batch(3, 2, 3, new NomJournalMutation([1], [40]))); Assert.Equal(new byte[] { 40 }, reopened.Get([1]));
        Assert.Equal(firstBytes, NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root))); Assert.Equal(new byte[] { 255 }, NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root, 99)));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void Factory_ForeignNonemptyAndPureLegacyRoots_ArePreservedWithoutInitialization(bool legacy)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); Directory.CreateDirectory(args.Root);
        File.WriteAllBytes(Path.Combine(args.Root, legacy ? "catalog.litedb" : "keep.me"), [7, 8, 9]); var saved = NomKvTestDirectory.Snapshot(args.Root);
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(args)); Assert.Empty(context.Io.Events);
        if (legacy) Assert.Throws<InvalidDataException>(() => context.Factory.Open(args));
        else Assert.Throws<FileNotFoundException>(() => context.Factory.Open(args));
        Assert.Equal(0, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertSnapshot(saved, args.Root);
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point.StartsWith("write-", StringComparison.Ordinal) || entry.Point == "publish");
        Assert.False(File.Exists(Path.Combine(args.Root, "owner.lock")));
    }

    [Fact]
    public void WriteBatch_AtomicReplacementAtIndexLimit_DeletesBeforeAddingAndCountsExactLiveKeys()
    {
        using var context = new NomKvTestContext(); using var store = context.Factory.Create(context.Directory.Arguments(new() { MaximumIndexKeys = 1, MaximumIndexBytes = 97 }));
        store.WriteBatch(Batch(1, 0, 0, new NomJournalMutation([255], [1])));
        var receipt = store.WriteBatch(Batch(2, 0, 1, new NomJournalMutation([1], [2]), new NomJournalMutation([255], null)));
        Assert.Equal(1, receipt.Generation); Assert.Equal(2, store.Sequence); Assert.Equal(1, store.KeyCount); Assert.Equal(97, store.IndexBytes);
        var view = store.GetMany([new byte[] { 255 }, new byte[] { 1 }]); Assert.Equal(1, view.Generation);
        Assert.Null(view.Values[0]); Assert.Equal(new byte[] { 2 }, view.Values[1]);
    }

    [Fact]
    public void Owner_DeniesExternalWritesToAuthorityAndAllListedSegments_UntilClose()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var initial = context.Factory.Create(args); Seed(initial); initial.Dispose();
        var first = NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(args.Root));
        var header = new NomJournalHeader(args.ProjectId, args.StoreId, 2, 1, 1, Convert.ToHexStringLower(SHA256.HashData(first)));
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 2), NomJournalCodec.EncodeHeader(header));
        File.WriteAllBytes(Path.Combine(args.Root, "nom.root"), NomRootCodec.Encode(new(args.ProjectId, args.StoreId, ActiveSegmentId: 2)));
        var saved = NomKvTestDirectory.Snapshot(args.Root); var store = context.Factory.Open(args);
        var paths = new[] { Path.Combine(args.Root, "nom.root"), NomKvTestDirectory.Journal(args.Root), NomKvTestDirectory.Journal(args.Root, 2) };
        foreach (var path in paths)
            Assert.Throws<IOException>(() => { using var denied = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
        Assert.Equal(new byte[] { 10, 11 }, store.Get([1])); store.Dispose();
        foreach (var path in paths)
        {
            using var allowed = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            Assert.True(allowed.CanWrite); // Deliberately no data write.
        }
        NomKvTestDirectory.AssertSnapshot(saved, args.Root);
    }
}
