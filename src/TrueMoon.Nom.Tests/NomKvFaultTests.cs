using System.Security.Cryptography;
using Xunit;
using static TrueMoon.Nom.Tests.NomKvTestData;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomKvFaultTests
{
    [Theory]
    [InlineData("write-journal", 0, false, false)]
    [InlineData("write-journal", 1, false, false)]
    [InlineData("write-journal", 100, false, false)]
    [InlineData("write-journal", 0, true, true)]
    [InlineData("flush-journal", 0, false, true)]
    [InlineData("flush-journal", 0, true, true)]
    public void WriteOrFlushFailure_FaultsOwnerAndResolvesExactTicketAfterReopen(string point, int prefix, bool after, bool committed)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store);
        var failure = new IOException("Injected target disk-full boundary.", unchecked((int)0x80070070)); context.Io.Events.Clear(); context.Io.Arm(point, failure, prefix: prefix, after: after);
        var batch = Batch(2, 1, 2, new NomJournalMutation([1], [20, 21]), new NomJournalMutation([2], null), new NomJournalMutation([3], [30]));
        var error = Assert.Throws<NomKvUnknownOutcomeException>(() => store.WriteBatch(batch));
        Assert.Same(failure, error.InnerException); Assert.Equal(args.ProjectId, error.Ticket.ProjectId); Assert.Equal(args.StoreId, error.Ticket.StoreId);
        Assert.Equal(2, error.Ticket.Sequence); Assert.Equal(Operation, error.Ticket.OperationId);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(NextFrame())), error.Ticket.FrameDigest);
        Assert.True(store.IsFaulted); Assert.False(store.IsClosed); Assert.Equal(1, store.Sequence); Assert.Equal(1, store.Generation);
        var attempts = context.Io.Events.Count;
        Assert.Throws<InvalidOperationException>(() => store.Get([1])); Assert.Throws<InvalidOperationException>(() => store.GetMany([new byte[] { 1 }, new byte[] { 3 }]));
        Assert.Throws<InvalidOperationException>(() => store.WriteBatch(batch)); Assert.Throws<InvalidOperationException>(() => store.LookupOutcome(error.Ticket));
        Assert.Equal(attempts, context.Io.Events.Count); NomKvTestDirectory.AssertLocked(args.Root);
        context.Io.Disarm(); store.Dispose(); NomKvTestDirectory.AssertReleased(args.Root); using var reopened = context.Factory.Open(args);
        Assert.False(reopened.IsFaulted); Assert.Equal(committed ? 2 : 1, reopened.Sequence); Assert.Equal(committed ? 2 : 1, reopened.Generation);
        Assert.Equal(committed ? NomKvWriteOutcome.Committed : NomKvWriteOutcome.NotCommitted, reopened.LookupOutcome(error.Ticket));
        Assert.Equal(committed ? new byte[] { 20, 21 } : new byte[] { 10, 11 }, reopened.Get([1]));
        if (committed) { Assert.Null(reopened.Get([2])); Assert.Equal(new byte[] { 30 }, reopened.Get([3])); }
        else { Assert.Empty(reopened.Get([2])!); Assert.Null(reopened.Get([3])); }
        Assert.Equal(!committed && prefix > 0 ? prefix : 0, reopened.RecoveredTailBytes);
        Assert.Equal(2, reopened.KeyCount); Assert.Equal(194, reopened.IndexBytes);
    }

    [Fact]
    public void CancellationAfterActualWrite_ReturnsCommittedReceiptAndStableReopenOutcome()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store);
        using var cancel = new CancellationTokenSource(); context.Io.AfterJournalWrite = cancel.Cancel;
        var receipt = store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [99])), cancel.Token);
        Assert.True(cancel.IsCancellationRequested); Assert.Equal(2, receipt.Generation); Assert.Equal(2, store.Sequence); Assert.False(store.IsFaulted);
        Assert.Equal(new byte[] { 99 }, store.Get([1])); context.Io.AfterJournalWrite = null; store.Dispose();
        using var reopened = context.Factory.Open(args); Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(receipt.Ticket));
        Assert.Equal(new byte[] { 99 }, reopened.Get([1]));
    }

    [Theory]
    [InlineData("write-journal", false)][InlineData("flush-journal", false)]
    [InlineData("write-pending", false)][InlineData("flush-pending", false)]
    [InlineData("close-pending", false)][InlineData("publish", false)][InlineData("publish", true)]
    public void FailedCreate_PublishesAuthorityLastAndPreservesUninitializedFiles(string point, bool after)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var failure = new IOException("Create failed.");
        context.Io.Arm(point, failure, prefix: 1, after: after);
        var error = Record.Exception(() => context.Factory.Create(args)); Assert.NotNull(error); Assert.Contains(Flatten(error), cause => ReferenceEquals(cause, failure));
        Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed)); NomKvTestDirectory.AssertReleased(args.Root);
        var published = point == "publish" && after; Assert.Equal(published, File.Exists(Path.Combine(args.Root, "nom.root")));
        var saved = NomKvTestDirectory.Snapshot(args.Root); context.Io.Disarm();
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(args)); NomKvTestDirectory.AssertSnapshot(saved, args.Root);
        if (published)
        {
            using var store = context.Factory.Open(args); Assert.Equal(0, store.Sequence); Assert.Equal(0, store.Generation); Assert.Null(store.Get([1]));
        }
        else
        {
            Assert.Throws<FileNotFoundException>(() => context.Factory.Open(args)); NomKvTestDirectory.AssertSnapshot(saved, args.Root);
        }
    }

    [Theory]
    [InlineData("close-journal", false)][InlineData("close-journal", true)]
    [InlineData("close-root", false)][InlineData("close-root", true)]
    [InlineData("close-lock", false)][InlineData("close-lock", true)]
    public void CloseFailure_RetainsActualOwnershipUntilReleaseAndAllowsExplicitRetry(string point, bool after)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store);
        var failure = new IOException("Close acknowledgement failed."); context.Io.Arm(point, failure, persistent: true, after: after);
        var error = Record.Exception(store.Dispose); Assert.NotNull(error); Assert.Contains(Flatten(error), cause => ReferenceEquals(cause, failure));
        var actuallyReleased = point == "close-lock" && after;
        Assert.Equal(actuallyReleased, store.IsClosed); Assert.Equal(actuallyReleased ? 0 : 1, context.Factory.ActiveStoreCount);
        if (actuallyReleased) NomKvTestDirectory.AssertReleased(args.Root); else NomKvTestDirectory.AssertLocked(args.Root);
        Assert.Throws<ObjectDisposedException>(() => store.Get([1]));
        context.Io.Disarm(); store.Dispose(); store.Dispose(); Assert.True(store.IsClosed); Assert.Equal(0, context.Factory.ActiveStoreCount);
        Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed)); NomKvTestDirectory.AssertReleased(args.Root);
        using var reopened = context.Factory.Open(args); Assert.Equal(1, reopened.Sequence); Assert.Equal(new byte[] { 10, 11 }, reopened.Get([1]));
    }

    [Fact]
    public void FailedCreationAndCleanup_PreserveBothCausesAndRetainFactoryCapacityUntilDrainRetry()
    {
        using var context = new NomKvTestContext(maximumOwners: 1); using var other = new NomKvTestDirectory(); var args = context.Directory.Arguments();
        var original = new IOException("Initial journal write failed."); var cleanup = new IOException("Journal close failed.");
        context.Io.Arm("write-journal", original, prefix: 1); context.Io.Arm("close-journal", cleanup, persistent: true);
        var error = Assert.Throws<AggregateException>(() => context.Factory.Create(args));
        Assert.Contains(Flatten(error), cause => ReferenceEquals(cause, original)); Assert.Contains(Flatten(error), cause => ReferenceEquals(cause, cleanup));
        Assert.Equal(1, context.Factory.ActiveStoreCount); Assert.False(context.Factory.IsReleased); NomKvTestDirectory.AssertLocked(args.Root);
        Assert.Throws<InvalidOperationException>(() => context.Factory.Create(other.Arguments())); Assert.False(Directory.Exists(other.Root));
        Assert.Throws<AggregateException>(context.Factory.Dispose); Assert.Equal(1, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertLocked(args.Root);
        context.Io.Disarm(); context.Factory.Dispose(); Assert.True(context.Factory.IsReleased); Assert.Equal(0, context.Factory.ActiveStoreCount);
        Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed)); NomKvTestDirectory.AssertReleased(args.Root);
        Assert.False(File.Exists(Path.Combine(args.Root, "nom.root"))); Assert.Throws<ObjectDisposedException>(() => context.Factory.Open(args));
    }

    [Theory]
    [InlineData("truncate", false)][InlineData("truncate", true)][InlineData("flush-journal", false)][InlineData("flush-journal", true)]
    public void Open_RepairIoFailure_DoesNotResetCommittedDataAndCanBeRetried(string point, bool after)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); var receipt = Seed(store); store.Dispose();
        var journal = NomKvTestDirectory.Journal(args.Root); using (var append = new FileStream(journal, FileMode.Append)) append.Write(NextFrame().AsSpan(0, 100));
        var failure = new IOException("Tail repair failed."); context.Io.Arm(point, failure, after: after);
        Assert.Same(failure, Assert.Throws<IOException>(() => context.Factory.Open(args)));
        Assert.Equal(0, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertReleased(args.Root); context.Io.Disarm();
        using var reopened = context.Factory.Open(args); Assert.Equal(1, reopened.Sequence); Assert.Equal(1, reopened.Generation);
        Assert.Equal(new byte[] { 10, 11 }, reopened.Get([1])); Assert.Empty(reopened.Get([2])!); Assert.Null(reopened.Get([3]));
        Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(receipt.Ticket));
        Assert.Equal(point == "truncate" && !after ? 100 : 0, reopened.RecoveredTailBytes);
    }

    [Fact]
    public void Open_WrongIdentity_NeverTruncatesAnOtherwiseRecoverableTail()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store); store.Dispose();
        var journal = NomKvTestDirectory.Journal(args.Root); using (var append = new FileStream(journal, FileMode.Append)) append.Write(NextFrame().AsSpan(0, 100));
        var saved = NomKvTestDirectory.Snapshot(args.Root); context.Io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => context.Factory.Open(args with { StoreId = args.ProjectId }));
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point is "truncate" or "write-journal" or "flush-journal"); NomKvTestDirectory.AssertSnapshot(saved, args.Root);
        using var valid = context.Factory.Open(args); Assert.Equal(100, valid.RecoveredTailBytes); Assert.Equal(1, valid.Sequence);
    }

    [Theory]
    [InlineData("sealed-tail")][InlineData("chain-hash")]
    public void Open_InvalidSealedPredecessor_RefusesBeforeRepairingActiveTail(string failure)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store); store.Dispose();
        var firstPath = NomKvTestDirectory.Journal(args.Root); var first = File.ReadAllBytes(firstPath);
        var header = new NomJournalHeader(args.ProjectId, args.StoreId, 2, 1, 1,
            failure == "chain-hash" ? new string('a', 64) : Convert.ToHexStringLower(SHA256.HashData(first)));
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 2), NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(header),
            NomJournalCodec.EncodeBatch(header, Batch(2, 1, 2, new NomJournalMutation([1], [30])))[..100]));
        File.WriteAllBytes(Path.Combine(args.Root, "nom.root"), NomRootCodec.Encode(new(args.ProjectId, args.StoreId, ActiveSegmentId: 2)));
        if (failure == "sealed-tail") File.WriteAllBytes(firstPath, first[..^1]);
        var saved = NomKvTestDirectory.Snapshot(args.Root); context.Io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => context.Factory.Open(args)); Assert.Equal(0, context.Factory.ActiveStoreCount);
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point == "truncate"); NomKvTestDirectory.AssertSnapshot(saved, args.Root); NomKvTestDirectory.AssertReleased(args.Root);
    }

    [Fact]
    public async Task FactoryDrain_StopsAllOwnersBeforeWaitingAndRetainsBothLocksUntilActualClose()
    {
        using var context = new NomKvTestContext(); using var other = new NomKvTestDirectory(); using var rejected = new NomKvTestDirectory();
        var first = context.Factory.Create(context.Directory.Arguments()); var second = context.Factory.Create(other.Arguments()); Seed(first); Seed(second);
        var gate = new NomKvTestGate(); context.Io.CloseGate = gate; var drain = Task.Run(context.Factory.Dispose);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<ObjectDisposedException>(() => context.Factory.Create(rejected.Arguments())); Assert.False(Directory.Exists(rejected.Root));
            var free = context.Io.BlockedClosePath!.StartsWith(first.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? second : first;
            Assert.Throws<ObjectDisposedException>(() => free.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [9]))));
            Assert.Equal(2, context.Factory.ActiveStoreCount); Assert.False(context.Factory.IsReleased);
            NomKvTestDirectory.AssertLocked(first.Root); NomKvTestDirectory.AssertLocked(second.Root);
        }
        finally { gate.Release.Set(); }
        await drain.WaitAsync(TimeSpan.FromSeconds(5)); Assert.True(first.IsClosed); Assert.True(second.IsClosed);
        Assert.True(context.Factory.IsReleased); Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed));
    }

    [Fact]
    public async Task Dispose_DuringEnteredWrite_WaitsForReceiptAndActualResourceRelease()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); Seed(store);
        var gate = new NomKvTestGate(); context.Io.WriteGate = gate;
        var write = Task.Run(() => store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [9]))));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); store.RequestStop(); var close = Task.Run(store.Dispose);
        try { Assert.False(store.IsClosed); Assert.Equal(1, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertLocked(args.Root); }
        finally { gate.Release.Set(); }
        var receipt = await write.WaitAsync(TimeSpan.FromSeconds(5)); await close.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, receipt.Generation); Assert.True(store.IsClosed); Assert.Equal(0, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertReleased(args.Root);
        context.Io.WriteGate = null; using var reopened = context.Factory.Open(args); Assert.Equal(new byte[] { 9 }, reopened.Get([1]));
        Assert.Equal(NomKvWriteOutcome.Committed, reopened.LookupOutcome(receipt.Ticket));
    }

    [Theory]
    [InlineData("open-lock")][InlineData("open-journal")][InlineData("open-pending")][InlineData("open-root")]
    public void Factory_OpenFileFailure_ReleasesPartialHandlesAndPreservesAvailableOwnerCapacity(string point)
    {
        using var context = new NomKvTestContext(maximumOwners: 1); var args = context.Directory.Arguments(); var failure = new IOException("Open handle failed.");
        context.Io.Arm(point, failure); Assert.Same(failure, Assert.Throws<IOException>(() => context.Factory.Create(args)));
        Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed));
        if (File.Exists(Path.Combine(args.Root, "owner.lock"))) NomKvTestDirectory.AssertReleased(args.Root);
        context.Io.Disarm();
        if (point == "open-lock")
        {
            Assert.Empty(Directory.EnumerateFileSystemEntries(args.Root)); using var retry = context.Factory.Create(args); Assert.Equal(0, retry.Sequence);
        }
        else if (point == "open-root")
        {
            using var retry = context.Factory.Open(args); Assert.Equal(0, retry.Sequence); Assert.Null(retry.Get([1]));
        }
        else
        {
            using var other = new NomKvTestDirectory(); using var retry = context.Factory.Create(other.Arguments()); Assert.Equal(1, context.Factory.ActiveStoreCount);
            Assert.False(File.Exists(Path.Combine(args.Root, "nom.root")));
        }
    }

    [Fact]
    public void Create_CancelledAfterInitialJournalWrite_DoesNotPublishRootOrLeakHandles()
    {
        using var context = new NomKvTestContext(); using var cancel = new CancellationTokenSource(); var args = context.Directory.Arguments() with { Token = cancel.Token };
        context.Io.AfterJournalWrite = cancel.Cancel;
        var error = Assert.Throws<OperationCanceledException>(() => context.Factory.Create(args)); Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.False(File.Exists(Path.Combine(args.Root, "nom.root"))); Assert.True(File.Exists(Path.Combine(args.Root, "nom.root.pending")));
        Assert.Equal(0, context.Factory.ActiveStoreCount); Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed)); NomKvTestDirectory.AssertReleased(args.Root);
        context.Io.AfterJournalWrite = null; var saved = NomKvTestDirectory.Snapshot(args.Root);
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(args with { Token = default })); NomKvTestDirectory.AssertSnapshot(saved, args.Root);
    }

    [Fact]
    public void GetMany_ReadFailureAfterFirstValue_ReturnsNoPartialViewAndPoisonsOwnerUntilReopen()
    {
        using var context = new NomKvTestContext(); context.Io.UseReadFaultStreams = true; var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], [10]), new NomJournalMutation([2], [20]), new NomJournalMutation([3], [30])));
        var failure = new IOException("Second value read failed."); context.Io.Events.Clear(); context.Io.Arm("read-journal", failure, occurrence: 2);
        Assert.Same(failure, Assert.Throws<IOException>(() => store.GetMany([new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }])));
        Assert.Equal(2, context.Io.Events.Count(entry => entry.Point == "read-journal")); Assert.True(store.IsFaulted); Assert.False(store.IsClosed);
        Assert.Equal(1, store.Sequence); Assert.Equal(1, store.Generation); NomKvTestDirectory.AssertLocked(args.Root); var eventCount = context.Io.Events.Count;
        Assert.Throws<InvalidOperationException>(() => store.Get([1]));
        Assert.Throws<InvalidOperationException>(() => store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [99])))); Assert.Equal(eventCount, context.Io.Events.Count);
        context.Io.Disarm(); store.Dispose(); using var reopened = context.Factory.Open(args);
        var view = reopened.GetMany([new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }]); Assert.Equal(1, view.Sequence); Assert.Equal(1, view.Generation);
        Assert.Equal(new byte[] { 10 }, view.Values[0]); Assert.Equal(new byte[] { 20 }, view.Values[1]); Assert.Equal(new byte[] { 30 }, view.Values[2]);
    }
}
