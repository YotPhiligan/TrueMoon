using Xunit;
using static TrueMoon.Nom.Tests.NomKvTestData;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class NomCutoverTests
{
    [Fact]
    public void ReadPage_PrefixExclusiveContinuationAndByteBudget_ReturnDetachedOrderedRows()
    {
        using var context = new NomKvTestContext(); using var store = context.Factory.Create(context.Directory.Arguments());
        store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([0x10, 255], [22, 23]), new NomJournalMutation([0x11, 0], [33]),
            new NomJournalMutation([0x10, 1], [])));
        // A later smallest key makes dictionary insertion order differ from required page order.
        store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([0x10, 0], [11])));
        var prefix = new byte[] { 0x10 }; var first = store.ReadPage(prefix, maximumItems: 2, maximumBytes: 5);
        Assert.Equal(2, first.Sequence); Assert.Equal(2, first.Generation); Assert.True(first.HasMore); Assert.Equal(2, first.Items.Count);
        Assert.Equal(new byte[] { 0x10, 0 }, first.Items[0].Key); Assert.Equal(new byte[] { 11 }, first.Items[0].Value);
        Assert.Equal(new byte[] { 0x10, 1 }, first.Items[1].Key); Assert.Empty(first.Items[1].Value);
        var after = first.Items[1].Key.ToArray(); prefix[0] = 255; first.Items[0].Key[0] = 0; first.Items[0].Value[0] = 99;
        var last = store.ReadPage([0x10], after, maximumItems: 1, maximumBytes: 4);
        Assert.False(last.HasMore); Assert.Equal(new byte[] { 0x10, 255 }, Assert.Single(last.Items).Key); Assert.Equal(new byte[] { 22, 23 }, last.Items[0].Value);
        Assert.Equal(new byte[] { 11 }, store.Get([0x10, 0]));
        var byteBound = store.ReadPage([0x10], maximumBytes: 3); Assert.Single(byteBound.Items); Assert.True(byteBound.HasMore);
        Assert.Equal(new byte[] { 0x10, 0 }, byteBound.Items[0].Key);
        var all = store.ReadPage([]); Assert.Equal(4, all.Items.Count); Assert.False(all.HasMore);
        var beyond = store.ReadPage([0x10], [0x10, 255]); Assert.Empty(beyond.Items); Assert.False(beyond.HasMore);
        Assert.Throws<InvalidDataException>(() => store.ReadPage([0x10], maximumBytes: 2)); Assert.False(store.IsFaulted); Assert.Equal(2, store.Sequence);
    }

    [Theory]
    [InlineData("null-prefix")][InlineData("long-prefix")][InlineData("long-after")]
    [InlineData("zero-items")][InlineData("above-items")][InlineData("zero-bytes")][InlineData("above-bytes")]
    public void ReadPage_InvalidBounds_LeaveStoreUsable(string invalid)
    {
        using var context = new NomKvTestContext(); using var store = context.Factory.Create(context.Directory.Arguments()); Seed(store);
        var error = Record.Exception(() => store.ReadPage(invalid == "null-prefix" ? null! : invalid == "long-prefix" ? new byte[1025] : [],
            invalid == "long-after" ? new byte[1025] : null, invalid == "zero-items" ? 0 : invalid == "above-items" ? 257 : 256,
            invalid == "zero-bytes" ? 0 : invalid == "above-bytes" ? 1048577 : 1048576));
        if (invalid == "null-prefix") Assert.IsType<ArgumentNullException>(error); else Assert.IsType<InvalidDataException>(error);
        Assert.False(store.IsFaulted); Assert.Equal(new byte[] { 10, 11 }, store.Get([1])); Assert.Equal(1, store.Sequence);
    }

    [Fact]
    public void ReadPage_ReadFailure_ReturnsNoPartialPageAndRequiresReopen()
    {
        using var context = new NomKvTestContext(); context.Io.UseReadFaultStreams = true;
        using var store = context.Factory.Create(context.Directory.Arguments());
        store.WriteBatch(Batch(1, 0, 1, new NomJournalMutation([1], [10]), new NomJournalMutation([2], [20])));
        var failure = new IOException("Second page value failed."); context.Io.Arm("read-journal", failure, occurrence: 2);
        Assert.Same(failure, Assert.Throws<IOException>(() => store.ReadPage([]))); Assert.True(store.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => store.ReadPage([])); Assert.Equal(1, store.Sequence);
    }

    [Fact]
    public void DeferredRecovery_ReadsVerifiedPrefixAndBlocksAppendUntilExplicitRepair()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var initial = context.Factory.Create(args); Seed(initial); initial.Dispose();
        var journal = NomKvTestDirectory.Journal(args.Root); var length = new FileInfo(journal).Length;
        using (var append = new FileStream(journal, FileMode.Append)) append.Write(NextFrame().AsSpan(0, 100));
        context.Io.Events.Clear(); var store = context.Factory.Open(args with { RepairIncompleteTail = false });
        Assert.Equal(100, store.RecoveredTailBytes); Assert.Equal(length + 100, new FileInfo(journal).Length);
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point is "truncate" or "flush-journal");
        Assert.Equal(new byte[] { 10, 11 }, store.Get([1])); Assert.Equal(2, store.ReadPage([]).Items.Count);
        Assert.Throws<InvalidOperationException>(() => store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [99])))); Assert.False(store.IsFaulted);
        store.CompleteRecovery(); Assert.Equal(length, new FileInfo(journal).Length); Assert.Single(context.Io.Events, entry => entry.Point == "truncate");
        Assert.Single(context.Io.Events, entry => entry.Point == "flush-journal"); var events = context.Io.Events.Count; store.CompleteRecovery(); Assert.Equal(events, context.Io.Events.Count);
        store.WriteBatch(Batch(2, 1, 2, new NomJournalMutation([1], [99]))); store.Dispose();
        using var reopened = context.Factory.Open(args); Assert.Equal(0, reopened.RecoveredTailBytes); Assert.Equal(2, reopened.Sequence); Assert.Equal(new byte[] { 99 }, reopened.Get([1]));
    }

    [Theory]
    [InlineData("truncate")][InlineData("flush-journal")]
    public void DeferredRecovery_RepairFailure_FaultsAndRetainsOwnerUntilClose(string point)
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); var initial = context.Factory.Create(args); Seed(initial); initial.Dispose();
        var journal = NomKvTestDirectory.Journal(args.Root); using (var append = new FileStream(journal, FileMode.Append)) append.Write(NextFrame().AsSpan(0, 100));
        var store = context.Factory.Open(args with { RepairIncompleteTail = false }); var failure = new IOException("Explicit repair failed."); context.Io.Arm(point, failure);
        Assert.Same(failure, Assert.Throws<IOException>(store.CompleteRecovery)); Assert.True(store.IsFaulted); Assert.False(store.IsClosed); NomKvTestDirectory.AssertLocked(args.Root);
        Assert.Throws<InvalidOperationException>(() => store.Get([1])); context.Io.Disarm(); store.Dispose();
        using var reopened = context.Factory.Open(args); Assert.Equal(1, reopened.Sequence); Assert.Equal(new byte[] { 10, 11 }, reopened.Get([1]));
    }

    [Fact]
    public async Task ResourceReservation_PhysicalPublicationBudget_RejectsBeforeAdmissionAndAcceptsLastReachableBoundary()
    {
        await using var context = new NomFaultContext(); await using var owner = context.Factory.Create(context.Directory.Arguments());
        const int count = 22;
        var assets = Enumerable.Range(0, count).Select(index => new AssetRecord(
            Guid.Parse("00000000-0000-0000-0000-" + (index + 1).ToString("D12", System.Globalization.CultureInfo.InvariantCulture)),
            NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, "r" + index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
            Enumerable.Repeat((byte)0x31, index < count - 1 ? 65536 : 16042).ToArray())).ToArray();
        var emptyDigest = NomTestDirectory.Digest([]);
        var accepted = assets.Select(asset => new AssetResourceWrite(asset, 0, emptyDigest, 1)).ToArray();
        long Estimate(IEnumerable<AssetResourceWrite> specs) => 183 + specs.Sum(spec => 3L * CatalogCodec.Encode(spec.Asset).Length + 567);
        Assert.Equal(4194303, Estimate(accepted)); // Estimate is divisible by3;4MiB itself is unreachable.
        var last = assets[^1]; var rejected = accepted.ToArray();
        rejected[^1] = new(new(last.Id, last.Source, last.Kind, last.Locator, new byte[16043]), 0, emptyDigest, 1);
        Assert.Equal(4194306, Estimate(rejected)); context.Io.Events.Clear();
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.ReserveResourcesAsync(0, rejected));
        Assert.Empty(context.Io.Events); Assert.Equal(0, owner.ContentGeneration); Assert.Empty(await owner.ReadAsync());
        var ticket = await owner.ReserveResourcesAsync(0, accepted); Assert.Equal(1, ticket.Sequence);
        var inputs = assets.ToDictionary(asset => asset.Id, _ => (Stream)new MemoryStream([], writable: false));
        try
        {
            var receipt = await owner.PublishResourcesAsync(ticket, inputs); Assert.Equal(1, receipt.Generation); Assert.False(receipt.CleanupPending);
            Assert.Equal(count, receipt.Resources.Length); Assert.Equal(1, owner.ContentGeneration);
            Assert.All(receipt.Resources, resource => { Assert.Equal(0, resource.Length); Assert.Equal(emptyDigest, resource.Digest); });
            foreach (var resource in receipt.Resources) Assert.Equal(resource, await owner.FindResourceAsync(resource.AssetId));
        }
        finally { foreach (var input in inputs.Values) input.Dispose(); }
    }
}
