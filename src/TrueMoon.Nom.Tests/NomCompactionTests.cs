using Xunit;
using static TrueMoon.Nom.Tests.NomKvTestData;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomCompactionTests
{
    private static (NomKvWriteReceipt First, NomKvWriteReceipt Second) SeedState(NomKvStore store)
    {
        var first = Seed(store); var second = store.WriteBatch(Batch(2, 1, 2,
            new NomJournalMutation([1], [20, 21]), new NomJournalMutation([2], null), new NomJournalMutation([3], [])));
        return (first, second);
    }
    private static NomRootDescriptor Selected(string root) => NomRootCodec.Decode(NomKvTestDirectory.ReadBytes(Path.Combine(root, "nom.root")),
        NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId);
    private static void AssertState(NomKvStore store)
    {
        Assert.Equal(2, store.Sequence); Assert.Equal(2, store.Generation); Assert.Equal(2, store.KeyCount); Assert.Equal(194, store.IndexBytes);
        Assert.Equal(new byte[] { 20, 21 }, store.Get([1])); Assert.Null(store.Get([2])); Assert.Empty(store.Get([3])!);
    }
    private static void AssertClosed(NomCheckpointContext context)
    {
        Assert.All(context.Io.Opened, file => Assert.True(file.Handle.IsClosed)); Assert.Equal(0, context.Factory.ActiveStoreCount);
        NomKvTestDirectory.AssertReleased(context.Directory.Root);
    }

    [Fact]
    public void Compact_EmptyStoreAndRepeatedEpoch_AreNoOpsWithoutConsumingSequence()
    {
        using var context = new NomCheckpointContext(); using var store = context.Factory.Create(context.Directory.Arguments()); context.Io.Events.Clear();
        var empty = store.Compact(); Assert.Equal(new NomCompactionReceipt(1, 0, 0, 0, 0, false, false), empty);
        Assert.DoesNotContain(context.Io.Events, e => e.Point.StartsWith("write", StringComparison.Ordinal) || e.Point == "replace-root");
        var (first, second) = SeedState(store); var oldHash = NomCheckpointFixtures.Digest(NomKvTestDirectory.ReadBytes(NomKvTestDirectory.Journal(store.Root)));
        var receipt = store.Compact(); Assert.Equal(new NomCompactionReceipt(2, 2, 2, 2, 180, false, true), receipt); AssertState(store);
        var root = Selected(store.Root); Assert.Equal(2, root.Epoch); Assert.Equal(2, root.CheckpointSequence); Assert.Equal(2, root.MinimumSequence);
        Assert.Equal(2, root.FirstSegmentId); Assert.Equal(2, root.ActiveSegmentId); Assert.Equal(oldHash, root.PreviousSegmentHash);
        Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(first.Ticket)); Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(second.Ticket));
        var physical = NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root")); context.Io.Events.Clear(); var repeat = store.Compact();
        Assert.False(repeat.Changed); Assert.False(repeat.CleanupPending); Assert.Equal(receipt with { Changed = false }, repeat);
        Assert.Equal(physical, NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root"))); Assert.DoesNotContain(context.Io.Events, e => e.Point.StartsWith("write", StringComparison.Ordinal));
    }

    [Fact]
    public void Compact_ReopenAppendAndSecondCheckpoint_PreserveDeletedEmptyAndUpdatedValuesAcrossEpochs()
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(new() { MaximumReplayBatches = 2 });
        var store = context.Factory.Create(args); var (first, second) = SeedState(store); store.Compact(); store.Dispose(); AssertClosed(context);
        store = context.Factory.Open(args); AssertState(store); Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(second.Ticket));
        var view = store.GetMany([new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }]); Assert.Equal(2, view.Sequence); Assert.Equal(2, view.Generation);
        Assert.Equal(new byte[] { 20, 21 }, view.Values[0]); Assert.Null(view.Values[1]); Assert.Empty(view.Values[2]!);
        var third = store.WriteBatch(Batch(3, 2, 3, new NomJournalMutation([1], [40]), new NomJournalMutation([3], null), new NomJournalMutation([4], [50])));
        Assert.Equal(NomKvWriteOutcome.Committed, store.LookupOutcome(third.Ticket)); Assert.Equal(NomKvWriteOutcome.NotCommitted, store.LookupOutcome(third.Ticket with { Sequence = 4 }));
        var page = store.ReadPage([]); Assert.Equal(3, page.Sequence); Assert.Equal(3, page.Generation); Assert.False(page.HasMore);
        Assert.Equal(new byte[] { 1, 4 }, page.Items.Select(row => row.Key[0]).ToArray()); Assert.Equal(new byte[] { 40 }, page.Items[0].Value);
        var receipt = store.Compact(); Assert.Equal(3, receipt.Epoch); Assert.Equal(3, receipt.Sequence); Assert.Equal(3, receipt.Generation); Assert.False(receipt.CleanupPending);
        Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(first.Ticket)); Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(third.Ticket));
        store.Dispose(); using var reopened = context.Factory.Open(args); Assert.Equal(3, reopened.Sequence); Assert.Equal(3, reopened.Generation); Assert.Equal(2, reopened.KeyCount);
        Assert.Equal(new byte[] { 40 }, reopened.Get([1])); Assert.Null(reopened.Get([2])); Assert.Null(reopened.Get([3])); Assert.Equal(new byte[] { 50 }, reopened.Get([4]));
        Assert.Equal(new byte[] { 20, 21 }, view.Values[0]); Assert.Single(Directory.GetFiles(Path.Combine(args.Root, "checkpoints"), "*.nom"));
        Assert.Single(Directory.GetFiles(Path.Combine(args.Root, "journal"), "*.nom"));
    }

    [Fact]
    public void Open_CheckpointWithSealedAndActiveSuccessors_UsesAllBackingFilesAndRepairsOnlyValidFinalTail()
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); Directory.CreateDirectory(args.Root);
        Directory.CreateDirectory(Path.Combine(args.Root, "journal")); Directory.CreateDirectory(Path.Combine(args.Root, "checkpoints"));
        File.WriteAllBytes(Path.Combine(args.Root, "owner.lock"), []);
        File.WriteAllBytes(Path.Combine(args.Root, "checkpoints", NomCheckpointFixtures.GoldenDigest + ".nom"), NomCheckpointFixtures.Golden);
        var header6 = new NomJournalHeader(args.ProjectId, args.StoreId, 6, 7, 3, NomCheckpointFixtures.PreviousDigest);
        var bytes6 = NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(header6), NomJournalCodec.EncodeBatch(header6,
            Batch(8, 3, 4, new NomJournalMutation([1], null), new NomJournalMutation([2], [0x22]))));
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 6), bytes6);
        var header7 = new NomJournalHeader(args.ProjectId, args.StoreId, 7, 8, 4, NomCheckpointFixtures.Digest(bytes6));
        var frame9 = NomJournalCodec.EncodeBatch(header7, Batch(9, 4, 4, new NomJournalMutation([3], [0x33]), new NomJournalMutation([0x80, 0], [0x55])));
        var partial10 = NomJournalCodec.EncodeBatch(header7, Batch(10, 4, 5, new NomJournalMutation([2], [0x99])))[..32];
        File.WriteAllBytes(NomKvTestDirectory.Journal(args.Root, 7), NomFormatFixtures.Join(NomJournalCodec.EncodeHeader(header7), frame9, partial10));
        File.WriteAllBytes(Path.Combine(args.Root, "nom.root"), NomRootCodec.Encode(NomCheckpointFixtures.Root with { ActiveSegmentId = 7, MinimumSequence = 9 }));
        using var store = context.Factory.Open(args); Assert.Equal(9, store.Sequence); Assert.Equal(4, store.Generation); Assert.Equal(4, store.KeyCount); Assert.Equal(32, store.RecoveredTailBytes);
        Assert.Null(store.Get([1])); Assert.Equal(new byte[] { 0x22 }, store.Get([2])); Assert.Equal(new byte[] { 0x33 }, store.Get([3]));
        Assert.Empty(store.Get([0x7f])!); Assert.Equal(new byte[] { 0x55 }, store.Get([0x80, 0]));
        Assert.Single(context.Io.Events, e => e.Point == "truncate");
        foreach (var file in new[] { Path.Combine(args.Root, "nom.root"), Path.Combine(args.Root, "checkpoints", NomCheckpointFixtures.GoldenDigest + ".nom"),
            NomKvTestDirectory.Journal(args.Root, 6), NomKvTestDirectory.Journal(args.Root, 7) })
            Assert.Throws<IOException>(() => { using var external = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); });
        var receipt = store.Compact(); Assert.Equal(3, receipt.Epoch); Assert.Equal(9, receipt.Sequence); Assert.Equal(4, receipt.Keys); Assert.Equal(new byte[] { 0x22 }, store.Get([2]));
    }

    [Theory]
    [InlineData("missing")][InlineData("checksum")][InlineData("schema")]
    public void Open_InvalidSelectedCheckpoint_FailsBeforeAnyTailRepairOrCleanup(string invalid)
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); SeedState(store); store.Compact(); store.Dispose();
        var root = Selected(args.Root); var cpPath = Path.Combine(args.Root, "checkpoints", root.CheckpointDigest + ".nom"); var journalPath = NomKvTestDirectory.Journal(args.Root, root.ActiveSegmentId);
        var header = NomJournalCodec.DecodeHeader(File.ReadAllBytes(journalPath)); var frame = NomJournalCodec.EncodeBatch(header, Batch(3, 2, 3, new NomJournalMutation([1], [99])));
        using (var append = new FileStream(journalPath, FileMode.Append)) append.Write(frame.AsSpan(0, 40));
        if (invalid == "missing") File.Delete(cpPath);
        else
        {
            var bytes = File.ReadAllBytes(cpPath);
            if (invalid == "checksum") bytes[^1] ^= 1;
            else
            {
                bytes[12] = 1; var changed = NomCheckpointFixtures.Authenticate(bytes); var newPath = Path.Combine(args.Root, "checkpoints", changed.CheckpointDigest + ".nom");
                File.Move(cpPath, newPath); cpPath = newPath; File.WriteAllBytes(Path.Combine(args.Root, "nom.root"), NomRootCodec.Encode(root with { CheckpointDigest = changed.CheckpointDigest }));
            }
            File.WriteAllBytes(cpPath, bytes);
        }
        var snapshot = NomKvTestDirectory.Snapshot(args.Root); context.Io.Events.Clear();
        if (invalid == "missing") Assert.ThrowsAny<IOException>(() => context.Factory.Open(args)); else Assert.Throws<InvalidDataException>(() => context.Factory.Open(args));
        Assert.DoesNotContain(context.Io.Events, e => e.Point is "truncate" or "delete" || e.Point.StartsWith("write", StringComparison.Ordinal));
        NomKvTestDirectory.AssertSnapshot(snapshot, args.Root); AssertClosed(context);
    }

    [Theory]
    [InlineData("checkpoint")][InlineData("maintenance")]
    public void Compact_KnownByteLimitsRejectBeforeIoAndExactHeadroomSucceeds(string quota)
    {
        using var context = new NomCheckpointContext(); var limits = quota == "checkpoint" ? new NomFormatLimits { MaximumCheckpointBytes = 179 }
            : new NomFormatLimits { MaximumMaintenanceBytes = 595 };
        var store = context.Factory.Create(context.Directory.Arguments(limits)); SeedState(store); context.Io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => store.Compact()); Assert.Empty(context.Io.Events); Assert.False(store.IsFaulted); AssertState(store); store.Dispose();
        using var reopened = context.Factory.Open(context.Directory.Arguments(new() { MaximumCheckpointBytes = 180, MaximumMaintenanceBytes = 596 }));
        var receipt = reopened.Compact(); Assert.Equal(180, receipt.CheckpointBytes); Assert.True(receipt.Changed); Assert.False(receipt.CleanupPending); AssertState(reopened);
    }

    [Fact]
    public void Compact_AllKeysDeletedAtGenerationZero_SeedsEmptyCheckpointAndNeverResurrectsTombstones()
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        var first = store.WriteBatch(Batch(1, 0, 0, new NomJournalMutation([1], [10])));
        store.WriteBatch(Batch(2, 0, 0, new NomJournalMutation([1], null))); var receipt = store.Compact();
        Assert.Equal(new NomCompactionReceipt(2, 2, 0, 0, 160, false, true), receipt); Assert.Equal(0, store.KeyCount); Assert.Null(store.Get([1]));
        Assert.Equal(NomKvWriteOutcome.Expired, store.LookupOutcome(first.Ticket)); store.Dispose();
        using var reopened = context.Factory.Open(args); Assert.Equal(2, reopened.Sequence); Assert.Equal(0, reopened.Generation); Assert.Equal(0, reopened.IndexBytes);
        Assert.Empty(reopened.ReadPage([]).Items); Assert.Null(reopened.Get([1]));
        reopened.WriteBatch(Batch(3, 0, 1, new NomJournalMutation([2], [20]))); Assert.Null(reopened.Get([1])); Assert.Equal(new byte[] { 20 }, reopened.Get([2]));
    }

    [Fact]
    public void Compact_ManagedDirectoryScanAllows1024EntriesAndRejectsNextWithoutDeletingUserFiles()
    {
        using var context = new NomCheckpointContext(); using var store = context.Factory.Create(context.Directory.Arguments());
        var journal = Path.Combine(store.Root, "journal");
        for (var i = 0; i < 1023; i++) File.WriteAllBytes(Path.Combine(journal, i + ".notes"), [7]);
        context.Io.Events.Clear(); Assert.False(store.Compact().Changed); Assert.Equal(1024, Directory.GetFiles(journal).Length); Assert.Empty(context.Io.Events);
        var excess = Path.Combine(journal, "excess.notes"); File.WriteAllBytes(excess, [9]);
        Assert.Throws<InvalidDataException>(() => store.Compact()); Assert.False(store.IsFaulted); Assert.Empty(context.Io.Events); Assert.Equal(1025, Directory.GetFiles(journal).Length);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(excess)); Assert.Equal(1, Selected(store.Root).Epoch); Assert.Equal(0, store.Sequence);
        File.Delete(excess); Assert.False(store.Compact().CleanupPending); Assert.Equal(1024, Directory.GetFiles(journal).Length);
    }

    [Fact]
    public void Compact_FileCountHeadroomRejects1023BeforePreparationAndAccepts1022WithoutDeletingUserFiles()
    {
        using var context = new NomCheckpointContext(); using var store = context.Factory.Create(context.Directory.Arguments()); SeedState(store);
        var journal = Path.Combine(store.Root, "journal");
        for (var i = 0; i < 1022; i++) File.WriteAllBytes(Path.Combine(journal, i + ".notes"), [7]);
        var root = NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root")); context.Io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => store.Compact()); Assert.Empty(context.Io.Events); Assert.False(store.IsFaulted); AssertState(store);
        Assert.Equal(root, NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root"))); Assert.Equal(1023, Directory.GetFiles(journal).Length);
        File.Delete(Path.Combine(journal, "1021.notes")); var cleanupWitness = 0;
        context.Io.Before = (point, _) =>
        {
            if (point != "delete") return;
            Assert.Equal(1024, Directory.GetFiles(journal).Length + Directory.GetFiles(Path.Combine(store.Root, "checkpoints")).Length); cleanupWitness++;
        };
        var receipt = store.Compact(); Assert.True(receipt.Changed); Assert.False(receipt.CleanupPending); Assert.Equal(1, cleanupWitness); AssertState(store);
        Assert.Equal(1021, Directory.GetFiles(journal, "*.notes").Length); Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Path.Combine(journal, "0.notes")));
        Assert.DoesNotContain(context.Io.Events, e => e.Point == "delete" && e.Path.EndsWith(".notes", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("checkpoint-write-zero")][InlineData("checkpoint-write-partial")][InlineData("checkpoint-write-full")]
    [InlineData("checkpoint-flush-before")][InlineData("checkpoint-flush-after")][InlineData("checkpoint-close-before")][InlineData("checkpoint-close-after")]
    [InlineData("checkpoint-publish-before")][InlineData("checkpoint-publish-after")]
    [InlineData("segment-write-zero")][InlineData("segment-write-partial")][InlineData("segment-write-full")]
    [InlineData("segment-flush-before")][InlineData("segment-flush-after")][InlineData("segment-close-before")][InlineData("segment-close-after")]
    [InlineData("segment-publish-before")][InlineData("segment-publish-after")]
    [InlineData("switch-write-zero")][InlineData("switch-write-partial")][InlineData("switch-write-full")]
    [InlineData("switch-flush-before")][InlineData("switch-flush-after")][InlineData("switch-close-before")][InlineData("switch-close-after")]
    [InlineData("authority-close-before")][InlineData("authority-close-after")][InlineData("replace-before")][InlineData("replace-after")][InlineData("selected-open")]
    public void Compact_FailedPreparationOrSwitch_PoisonsOwnerAndReopenSelectsWholeAuthoritativeEpoch(string cut)
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); var (_, second) = SeedState(store);
        var error = new IOException("Injected compaction cut " + cut); var parts = cut.Split('-'); var stage = parts[0];
        var kind = stage switch { "checkpoint" => "checkpoint-pending", "segment" => "segment-pending", "authority" => "root", _ => "switch" };
        var point = cut switch
        {
            "replace-before" or "replace-after" => "replace-root", "selected-open" => "open-root",
            _ when parts[1] == "publish" => "publish-" + (stage == "checkpoint" ? "checkpoint" : "journal"), _ => parts[1] + "-" + kind
        };
        context.Io.Arm(point, error, prefix: cut.EndsWith("partial", StringComparison.Ordinal) ? 17 : 0,
            after: cut.EndsWith("after", StringComparison.Ordinal) || cut.EndsWith("full", StringComparison.Ordinal));
        var failure = Assert.Throws<NomCompactionException>(() => store.Compact());
        var attempted = stage == "replace" || cut == "selected-open"; Assert.Equal(attempted, failure.RootPublicationAttempted); Assert.Same(error, failure.InnerException);
        Assert.True(store.IsFaulted); NomKvTestDirectory.AssertLocked(args.Root); var calls = context.Io.Events.Count;
        Assert.Throws<InvalidOperationException>(() => store.Get([1])); Assert.Throws<InvalidOperationException>(() => store.Compact());
        Assert.Throws<InvalidOperationException>(() => store.WriteBatch(Batch(3, 2, 3, new NomJournalMutation([1], [99])))); Assert.Equal(calls, context.Io.Events.Count);
        Assert.True(File.Exists(NomKvTestDirectory.Journal(args.Root))); context.Io.Disarm(); store.Dispose(); AssertClosed(context);
        using var reopened = context.Factory.Open(args); AssertState(reopened); var selectedNew = cut is "replace-after" or "selected-open";
        Assert.Equal(selectedNew ? 2 : 1, Selected(args.Root).Epoch); Assert.Equal(selectedNew ? NomKvWriteOutcome.Expired : NomKvWriteOutcome.Committed, reopened.LookupOutcome(second.Ticket));
        var retry = reopened.Compact(); Assert.Equal(!selectedNew, retry.Changed); Assert.False(retry.CleanupPending); AssertState(reopened);
        Assert.Single(Directory.GetFiles(Path.Combine(args.Root, "journal"), "*.nom")); Assert.Single(Directory.GetFiles(Path.Combine(args.Root, "checkpoints"), "*.nom"));
        Assert.Empty(Directory.GetFiles(args.Root, "*.switch")); Assert.Empty(Directory.GetFiles(Path.Combine(args.Root, "journal"), "*.pending"));
    }

    [Theory]
    [InlineData("checkpoint-pending")][InlineData("segment-pending")][InlineData("switch")]
    public void Compact_PreparedFileCorruptionBeforeVerification_CannotPublishOrDeleteOldAuthority(string kind)
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); SeedState(store);
        var root = NomKvTestDirectory.ReadBytes(Path.Combine(args.Root, "nom.root")); var damaged = 0;
        context.Io.After = (point, path) =>
        {
            if (point != "flush-" + kind) return;
            var owned = context.Io.Opened.Last(file => file.Path == path); RandomAccess.Write(owned.Handle, new byte[] { 0xff }, 0); damaged++;
        };
        var error = Assert.Throws<NomCompactionException>(() => store.Compact()); Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.Equal(1, damaged); Assert.False(error.RootPublicationAttempted); Assert.True(store.IsFaulted); Assert.Equal(root, NomKvTestDirectory.ReadBytes(Path.Combine(args.Root, "nom.root")));
        Assert.DoesNotContain(context.Io.Events, e => e.Point == "replace-root"); Assert.True(File.Exists(NomKvTestDirectory.Journal(args.Root)));
        context.Io.Disarm(); store.Dispose(); AssertClosed(context); using var reopened = context.Factory.Open(args); AssertState(reopened); Assert.True(reopened.Compact().Changed);
    }

    [Fact]
    public void Compact_CancelBeforeAdmissionLeavesOwnerUsableAndCancelDuringPreparationRequiresReopen()
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); SeedState(store);
        using var already = new CancellationTokenSource(); already.Cancel(); context.Io.Events.Clear();
        Assert.ThrowsAny<OperationCanceledException>(() => store.Compact(already.Token)); Assert.Empty(context.Io.Events); Assert.False(store.IsFaulted); AssertState(store);
        using var during = new CancellationTokenSource(); context.Io.After = (point, _) => { if (point == "write-checkpoint-pending") during.Cancel(); };
        Assert.ThrowsAny<OperationCanceledException>(() => store.Compact(during.Token)); Assert.True(store.IsFaulted); Assert.Equal(1, Selected(args.Root).Epoch);
        context.Io.Disarm(); store.Dispose(); AssertClosed(context); using var reopened = context.Factory.Open(args); AssertState(reopened); Assert.True(reopened.Compact().Changed);
    }

    [Fact]
    public void Compact_CancelAfterAtomicReplacementReturnsSuccessAndKeepsExclusiveOwnerThroughPublication()
    {
        using var context = new NomCheckpointContext(); using var store = context.Factory.Create(context.Directory.Arguments()); SeedState(store); using var cancel = new CancellationTokenSource(); var witnessed = 0;
        context.Io.After = (point, _) =>
        {
            if (point != "replace-root") return;
            NomKvTestDirectory.AssertLocked(store.Root); Assert.True(File.Exists(NomKvTestDirectory.Journal(store.Root))); witnessed++; cancel.Cancel();
        };
        var receipt = store.Compact(cancel.Token); Assert.Equal(1, witnessed); Assert.True(cancel.IsCancellationRequested); Assert.True(receipt.Changed);
        Assert.False(receipt.CleanupPending); Assert.False(store.IsFaulted); Assert.Equal(2, receipt.Epoch); AssertState(store);
    }

    [Fact]
    public void Compact_DeleteFailureRetriesCleanupWithoutChangingRootOrDeletingUnmanagedFiles()
    {
        using var context = new NomCheckpointContext(); using var store = context.Factory.Create(context.Directory.Arguments()); SeedState(store);
        Directory.CreateDirectory(Path.Combine(store.Root, "checkpoints"));
        var unmanaged = new[] { Path.Combine(store.Root, "journal", "user.notes"), Path.Combine(store.Root, "journal", new string('a', 64) + ".nom"),
            Path.Combine(store.Root, "checkpoints", "00000000000000000099.nom") };
        foreach (var path in unmanaged) File.WriteAllBytes(path, [9, 8, 7]);
        var failure = new IOException("Obsolete-file delete denied."); context.Io.Arm("delete", failure, persistent: true);
        var receipt = store.Compact(); Assert.True(receipt.Changed); Assert.True(receipt.CleanupPending); Assert.False(store.IsFaulted); AssertState(store);
        var oldJournal = NomKvTestDirectory.Journal(store.Root); Assert.True(File.Exists(oldJournal));
        Assert.All(context.Io.Opened.Where(file => file.Path == oldJournal), file => Assert.True(file.Handle.IsClosed));
        var root = NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root")); context.Io.Events.Clear(); var retained = store.Compact();
        Assert.False(retained.Changed); Assert.True(retained.CleanupPending); Assert.Equal(root, NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root")));
        Assert.DoesNotContain(context.Io.Events, e => e.Point.StartsWith("write", StringComparison.Ordinal)); context.Io.Disarm(); var retry = store.Compact();
        Assert.False(retry.Changed); Assert.False(retry.CleanupPending); Assert.Equal(root, NomKvTestDirectory.ReadBytes(Path.Combine(store.Root, "nom.root")));
        Assert.False(File.Exists(oldJournal)); foreach (var path in unmanaged) Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(path)); AssertState(store);
    }

    [Fact]
    public void Compact_OldHandleCloseFailureRetainsFilesAndRootUntilActualCloseRetry()
    {
        using var context = new NomCheckpointContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args); SeedState(store);
        var oldJournal = NomKvTestDirectory.Journal(args.Root); var failure = new IOException("Old journal close retained handle."); context.Io.Arm("close-journal", failure, persistent: true);
        var receipt = store.Compact(); Assert.True(receipt.Changed); Assert.True(receipt.CleanupPending); Assert.False(store.IsFaulted); AssertState(store);
        Assert.True(File.Exists(oldJournal)); Assert.Contains(context.Io.Opened, file => file.Path == oldJournal && !file.Handle.IsClosed); NomKvTestDirectory.AssertLocked(args.Root);
        var error = Assert.Throws<AggregateException>(() => store.Dispose()); Assert.Contains(Flatten(error), cause => ReferenceEquals(cause, failure));
        Assert.False(store.IsClosed); Assert.Equal(1, context.Factory.ActiveStoreCount); NomKvTestDirectory.AssertLocked(args.Root);
        context.Io.Disarm(); store.Dispose(); AssertClosed(context); using var reopened = context.Factory.Open(args); AssertState(reopened);
        var retry = reopened.Compact(); Assert.False(retry.Changed); Assert.False(retry.CleanupPending); Assert.False(File.Exists(oldJournal));
    }

    [Fact]
    public async Task PublicCompact_RetainsOperationJournalPruneFloorAndBothOldAndCurrentCasVersions()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); await using var app = App.Build(context => context.UseNom());
        var factory = NomTestDirectory.Factory(app); var store = factory.Create(args); var asset = NomTestDirectory.Record("versioned");
        var first = await Nom3Fixtures.Publish(store, asset, [10, 20]); var second = await Nom3Fixtures.Publish(store, asset, [30, 40, 50]);
        var unused = await store.ReserveResourcesAsync(2, [NomFaultContext.Spec(NomTestDirectory.Record("unused"), [1])]); await store.AbortResourceOperationAsync(unused);
        var receipt = await store.CompactAsync(); Assert.True(receipt.Changed); Assert.Equal(2, receipt.Generation); Assert.Equal(2, store.ContentGeneration);
        Assert.Equal(first.Version, Assert.Single((await store.LookupResourceOperationAsync(first.Ticket)).Resources));
        Assert.Equal(second.Version, Assert.Single((await store.LookupResourceOperationAsync(second.Ticket)).Resources)); Assert.Equal("aborted", (await store.LookupResourceOperationAsync(unused)).Status);
        await NomFaultContext.AssertBytes(store, first.Version, [10, 20]); await NomFaultContext.AssertBytes(store, second.Version, [30, 40, 50]);
        await store.PruneResourceOperationsAsync(first.Ticket.Sequence); var pruned = await store.CompactAsync(); Assert.True(pruned.Changed); Assert.Equal(2, pruned.Generation);
        await Assert.ThrowsAsync<ExpiredOperationException>(() => store.LookupResourceOperationAsync(first.Ticket)); await store.DisposeAsync(); NomTestDirectory.AssertRootReleased(args);
        await using var reopened = factory.Create(args with { Mode = AssetStoreOpenMode.Open }); Assert.Equal(2, reopened.ContentGeneration); Assert.Equal(second.Version, await reopened.FindResourceAsync(asset.Id));
        await Assert.ThrowsAsync<ExpiredOperationException>(() => reopened.LookupResourceOperationAsync(first.Ticket)); Assert.Equal("committed", (await reopened.LookupResourceOperationAsync(second.Ticket)).Status);
        Assert.Equal("aborted", (await reopened.LookupResourceOperationAsync(unused)).Status); await NomFaultContext.AssertBytes(reopened, first.Version, [10, 20]);
        await NomFaultContext.AssertBytes(reopened, second.Version, [30, 40, 50]); var next = await reopened.ReserveResourcesAsync(2, [NomFaultContext.Spec(NomTestDirectory.Record("next"), [1])]);
        Assert.Equal(4, next.Sequence); await reopened.AbortResourceOperationAsync(next);
    }

    [Fact]
    public async Task PublicCompact_StopWaitsForEnteredIoAndReleasesOnlyAfterSelectedEpochIsReady()
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); var store = context.Factory.Create(args);
        var published = await Nom3Fixtures.Publish(store, NomTestDirectory.Record("old"), [10, 20, 30]); var gate = new NomFaultGate();
        context.Io.BeforeJournalWrite = () =>
        {
            gate.Entered.TrySetResult(); if (!gate.Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Compaction IO gate.");
        };
        var maintenance = store.CompactAsync(); Task? stop = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); stop = context.Host.StopAsync();
            Assert.False(maintenance.IsCompleted); Assert.False(stop.IsCompleted); Assert.False(store.IsClosed);
            Assert.Equal(1, context.Host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(args);
        }
        finally { gate.Release.Set(); }
        var receipt = await maintenance.WaitAsync(TimeSpan.FromSeconds(5)); await stop!.WaitAsync(TimeSpan.FromSeconds(5)); context.Io.Disarm();
        Assert.True(receipt.Changed); Assert.Equal(1, receipt.Generation); Assert.False(receipt.CleanupPending); Assert.True(store.IsClosed);
        Assert.True(context.Host.IsReleased); Assert.Equal(0, context.Host.ActiveStoreCount);
        Assert.All(context.Io.OpenedCatalogFiles, file => Assert.True(file.Handle.IsClosed)); NomTestDirectory.AssertRootReleased(args);
        await using var other = new NomFaultContext(); await using var reopened = other.Factory.Create(args with { Mode = AssetStoreOpenMode.Open });
        Assert.Equal(1, reopened.ContentGeneration); Assert.Equal(published.Version, await reopened.FindResourceAsync(published.Version.AssetId));
        var outcome = await reopened.LookupResourceOperationAsync(published.Ticket); Assert.Equal("committed", outcome.Status); Assert.Equal(1, outcome.Generation);
        Assert.Equal(published.Version, Assert.Single(outcome.Resources)); await NomFaultContext.AssertBytes(reopened, published.Version, [10, 20, 30]);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PublicCompact_ActiveReservationOrReadLeaseRejectsWithoutConsumingOwnership(bool readLease)
    {
        await using var context = new NomFaultContext(); var args = context.Directory.Arguments(); await using var store = context.Factory.Create(args);
        var published = await Nom3Fixtures.Publish(store, NomTestDirectory.Record("old"), [10, 20, 30]); Stream? input = null; AssetWriteTicket? pending = null;
        if (readLease) { input = await store.OpenResourceAsync(published.Version); Assert.Equal(10, input.ReadByte()); }
        else pending = await store.ReserveResourcesAsync(1, [NomFaultContext.Spec(NomTestDirectory.Record("pending"), [1])]);
        var root = NomKvTestDirectory.ReadBytes(Path.Combine(args.Root, "nom.root")); var calls = context.Io.Events.Count;
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => store.CompactAsync()); Assert.Equal(calls, context.Io.Events.Count); Assert.Equal(1, store.ContentGeneration);
            Assert.Equal(root, NomKvTestDirectory.ReadBytes(Path.Combine(args.Root, "nom.root"))); NomTestDirectory.AssertRootLocked(args);
            if (input != null) { Assert.True(input.CanRead); Assert.Equal(1, input.Position); Assert.Equal(20, input.ReadByte()); }
            else Assert.Equal("pending", (await store.LookupResourceOperationAsync(pending!)).Status);
        }
        finally { if (input != null) await input.DisposeAsync(); else await store.AbortResourceOperationAsync(pending!); }
        Assert.True((await store.CompactAsync()).Changed); await NomFaultContext.AssertBytes(store, published.Version, [10, 20, 30]);
    }
}
