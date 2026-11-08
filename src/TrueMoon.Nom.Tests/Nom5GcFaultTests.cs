using Xunit;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5GcFaultTests
{
    private static async Task<(AssetResourceVersion Old, AssetResourceVersion Current, NomGarbageCollectionPlan Plan)> Seed(Nom5Context context)
    {
        var old = await context.Publish(Nom5Context.Asset(1), [10, 20]); var current = await context.Publish(Nom5Context.Asset(1), [30]);
        await context.Store.PruneResourceOperationsAsync(old.Ticket.Sequence);
        return (old.Version, current.Version, await context.Store.PlanGarbageCollectionAsync());
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task GarbageCollection_DeleteFailureKeepsDurableIntentAndReopenOnlyResumesOnExplicitCall(bool after)
    {
        var io = new NomCheckpointIo(); await using var context = new Nom5Context(io); var seed = await Seed(context);
        var failure = new IOException("injected CAS deletion"); io.Events.Clear(); io.Arm("delete", failure, after: after);
        var result = await context.Store.CollectGarbageAsync(seed.Plan); Assert.Equal(1, result.RevisionsPruned); Assert.Equal(0, result.ObjectsDeleted); Assert.True(result.CleanupPending);
        var path = Nom5Context.ObjectPath(context.Arguments.Root, seed.Old.Digest); Assert.Equal(!after, File.Exists(path));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Publish(Nom5Context.Asset(2), [50]));
        Assert.Equal(seed.Current, await context.Store.FindResourceAsync(seed.Current.AssetId)); await NomFaultContext.AssertBytes(context.Store, seed.Current, [30]);
        io.Disarm(); io.Events.Clear(); await context.Reopen(); Assert.DoesNotContain(io.Events, e => e.Point == "delete"); Assert.Equal(!after, File.Exists(path));
        var retry = await context.Store.CollectGarbageAsync(); Assert.Equal(new NomGarbageCollectionReceipt(0, 1, 2, false), retry); Assert.False(File.Exists(path));
        Assert.Equal(new NomGarbageCollectionReceipt(0, 0, 0, false), await context.Store.CollectGarbageAsync());
        var published = await context.Publish(Nom5Context.Asset(2), [50]); Assert.Equal(3, context.Store.ContentGeneration);
        Assert.Equal(published.Version, await context.Store.FindResourceAsync(published.Version.AssetId));
    }

    [Fact]
    public async Task GarbageCollection_CancellationBeforePruneLeavesPlanApplicableAndAllFilesUnchanged()
    {
        await using var context = new Nom5Context(); var seed = await Seed(context); var before = Nom5Context.Snapshot(context.Arguments.Root);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Store.CollectGarbageAsync(seed.Plan, cancel.Token));
        Nom5Context.AssertSnapshot(before, context.Arguments.Root); Assert.Equal(2, context.Store.ContentGeneration);
        var receipt = await context.Store.CollectGarbageAsync(seed.Plan); Assert.Equal(new NomGarbageCollectionReceipt(1, 1, 2, false), receipt);
        await NomFaultContext.AssertBytes(context.Store, seed.Current, [30]);
    }

    [Fact]
    public async Task GarbageCollection_CancellationAfterDurablePruneReturnsPendingAndResumesWithoutRepruning()
    {
        var io = new NomCheckpointIo(); await using var context = new Nom5Context(io); var seed = await Seed(context); using var cancel = new CancellationTokenSource();
        io.Events.Clear(); io.After = (point, _) => { if (point == "flush-journal") cancel.Cancel(); };
        var receipt = await context.Store.CollectGarbageAsync(seed.Plan, cancel.Token); io.Disarm();
        Assert.Equal(new NomGarbageCollectionReceipt(1, 0, 0, true), receipt); Assert.DoesNotContain(io.Events, e => e.Point == "delete");
        Assert.True(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, seed.Old.Digest))); Assert.Equal(2, context.Store.ContentGeneration);
        await context.Reopen(); Assert.Equal(new NomGarbageCollectionReceipt(0, 1, 2, false), await context.Store.CollectGarbageAsync());
        await NomFaultContext.AssertBytes(context.Store, seed.Current, [30]);
    }

    [Theory]
    [InlineData(false, "zero")][InlineData(false, "partial")][InlineData(false, "full")][InlineData(false, "flush-before")][InlineData(false, "flush-after")]
    [InlineData(true, "zero")][InlineData(true, "partial")][InlineData(true, "full")][InlineData(true, "flush-before")][InlineData(true, "flush-after")]
    public async Task GarbageCollection_AmbiguousPruneOrDeleteAcknowledgementRequiresReopenAndResumesWholeCommittedState(bool acknowledgement, string mode)
    {
        var io = new NomCheckpointIo(); await using var context = new Nom5Context(io); var seed = await Seed(context); var failure = new IOException("injected GC metadata acknowledgement");
        var whole = mode is "full" or "flush-before" or "flush-after";
        io.Events.Clear(); io.Arm(mode.StartsWith("flush", StringComparison.Ordinal) ? "flush-journal" : "write-journal", failure,
            occurrence: acknowledgement ? 2 : 1, prefix: mode == "partial" ? 100 : 0, after: mode is "full" or "flush-after");
        var error = await Assert.ThrowsAsync<NomKvUnknownOutcomeException>(() => context.Store.CollectGarbageAsync(seed.Plan)); Assert.Same(failure, error.InnerException);
        Assert.Equal(!acknowledgement, File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, seed.Old.Digest)));
        var writes = io.Events.Count(e => e.Point == "write-journal");
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.CollectGarbageAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Publish(Nom5Context.Asset(2), [5])); Assert.Equal(writes, io.Events.Count(e => e.Point == "write-journal"));
        io.Disarm(); io.Events.Clear(); await context.Reopen(); Assert.DoesNotContain(io.Events, e => e.Point == "delete");
        Assert.Equal(2, context.Store.ContentGeneration); Assert.Equal(seed.Current, await context.Store.FindResourceAsync(seed.Current.AssetId));
        if (!acknowledgement && !whole)
        {
            Assert.Equal(new NomGarbageCollectionReceipt(0, 0, 0, false), await context.Store.CollectGarbageAsync());
            var fresh = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, fresh.RevisionsToPrune); Assert.Equal(1, fresh.ObjectsToDelete);
            Assert.Equal(new NomGarbageCollectionReceipt(1, 1, 2, false), await context.Store.CollectGarbageAsync(fresh));
        }
        else
        {
            var pending = !acknowledgement || !whole;
            Assert.Equal(new NomGarbageCollectionReceipt(0, pending ? 1 : 0, pending ? 2 : 0, false), await context.Store.CollectGarbageAsync());
        }
        Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, seed.Old.Digest))); await NomFaultContext.AssertBytes(context.Store, seed.Current, [30]);
    }
}
