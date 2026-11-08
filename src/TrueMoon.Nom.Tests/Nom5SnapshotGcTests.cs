using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5SnapshotGcTests
{
    [Fact]
    public async Task Snapshot_FrozenDependencyManifestAndDurablePinSurviveReimportReopenAndGarbageCollection()
    {
        await using var context = new Nom5Context(); var b1 = await context.Publish(Nom5Context.Asset(2), [20, 21]);
        var a1 = await context.Publish(Nom5Context.Asset(1, dependencies: [b1.Version]), [10]);
        var manifest = await context.Store.CreateSnapshotAsync([a1.Version]);
        Assert.Equal(2, manifest.Generation); Assert.Equal(new[] { a1.Version, b1.Version }.OrderBy(v => v.Revision, StringComparer.Ordinal), manifest.Members);
        var pin = Nom5Context.Id(701); await context.Store.PinSnapshotAsync(manifest.Id, pin); await context.Store.PinSnapshotAsync(manifest.Id, pin);
        var b2 = await context.Publish(Nom5Context.Asset(2), [22]); var a2 = await context.Publish(Nom5Context.Asset(1, dependencies: [b2.Version]), [11, 12]);
        await context.Store.PruneResourceOperationsAsync(a2.Ticket.Sequence); await context.Store.CompactAsync(); await context.Reopen();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.RemoveSnapshotAsync(manifest.Id));
        var plan = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(0, plan.RevisionsToPrune); Assert.Equal(0, plan.ObjectsToDelete);
        var clean = await context.Store.CollectGarbageAsync(plan); Assert.False(clean.CleanupPending); Assert.Equal(0, clean.ObjectsDeleted);
        await using (var snapshot = await context.Store.OpenSnapshotAsync(manifest.Id))
        {
            Assert.Equal(manifest.Members, snapshot.Manifest.Members);
            await using var oldA = await snapshot.OpenResourceAsync(a1.Version); await Nom5Context.AssertBytes(oldA, [10]);
            await using var oldB = await snapshot.OpenResourceAsync(b1.Version); await Nom5Context.AssertBytes(oldB, [20, 21]);
            await Assert.ThrowsAsync<InvalidDataException>(() => snapshot.OpenResourceAsync(a2.Version));
        }
        await context.Store.ReleaseSnapshotPinAsync(pin); await context.Store.ReleaseSnapshotPinAsync(pin); await context.Store.RemoveSnapshotAsync(manifest.Id);
        var dead = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(2, dead.RevisionsToPrune); Assert.Equal(2, dead.ObjectsToDelete); Assert.Equal(3, dead.BytesToDelete);
        var receipt = await context.Store.CollectGarbageAsync(dead); Assert.Equal(2, receipt.RevisionsPruned); Assert.Equal(2, receipt.ObjectsDeleted); Assert.Equal(3, receipt.BytesDeleted); Assert.False(receipt.CleanupPending);
        await context.Reopen(); Assert.Equal(a2.Version, await context.Store.FindResourceAsync(a1.Version.AssetId)); Assert.Equal(b2.Version, await context.Store.FindResourceAsync(b1.Version.AssetId));
        await NomFaultContext.AssertBytes(context.Store, a2.Version, [11, 12]); await NomFaultContext.AssertBytes(context.Store, b2.Version, [22]);
        Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, a1.Version.Digest))); Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, b1.Version.Digest)));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.OpenResourceAsync(a1.Version));
    }

    [Fact]
    public async Task Snapshot_CloseRejectsNewOpensButExistingStreamRetainsPinUntilActualClose()
    {
        await using var context = new Nom5Context(); var resource = await context.Publish(Nom5Context.Asset(1), [10, 20, 30]);
        var manifest = await context.Store.CreateSnapshotAsync([resource.Version]); var snapshot = await context.Store.OpenSnapshotAsync(manifest.Id);
        var stream = await snapshot.OpenResourceAsync(resource.Version); Assert.Equal(10, stream.ReadByte());
        try
        {
            await snapshot.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => snapshot.OpenResourceAsync(resource.Version));
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.RemoveSnapshotAsync(manifest.Id));
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PlanGarbageCollectionAsync());
            Assert.Equal(20, stream.ReadByte()); Assert.Equal(30, stream.ReadByte()); Assert.Equal(-1, stream.ReadByte());
            NomTestDirectory.AssertRootLocked(context.Arguments);
        }
        finally { await stream.DisposeAsync(); await snapshot.DisposeAsync(); }
        await context.Store.RemoveSnapshotAsync(manifest.Id); Assert.Equal(0, (await context.Store.PlanGarbageCollectionAsync()).ObjectsToDelete);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => context.Store.OpenSnapshotAsync(manifest.Id));
    }

    [Fact]
    public async Task Snapshot_PinIdentityCannotRetargetAndManifestMembersAreDetached()
    {
        await using var context = new Nom5Context(); var a = await context.Publish(Nom5Context.Asset(1), [1]); var b = await context.Publish(Nom5Context.Asset(2), [2]);
        var first = await context.Store.CreateSnapshotAsync([a.Version]); var same = await context.Store.CreateSnapshotAsync([a.Version]); Assert.Equal(first.Id, same.Id);
        var second = await context.Store.CreateSnapshotAsync([b.Version]); Assert.NotEqual(first.Id, second.Id);
        var pin = Nom5Context.Id(702); await context.Store.PinSnapshotAsync(first.Id, pin);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PinSnapshotAsync(second.Id, pin));
        var members = first.Members; members[0] = b.Version;
        await using (var handle = await context.Store.OpenSnapshotAsync(first.Id))
        {
            Assert.Equal(a.Version, Assert.Single(handle.Manifest.Members));
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.RemoveSnapshotAsync(first.Id));
        }
        await context.Reopen(); await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.RemoveSnapshotAsync(first.Id));
        await context.Store.ReleaseSnapshotPinAsync(pin); await context.Store.RemoveSnapshotAsync(first.Id); await context.Store.RemoveSnapshotAsync(second.Id);
    }

    [Fact]
    public async Task GarbageCollection_DryRunPreservesFilesAndRetainedOutcomesThenDeletesOnlyUnreferencedBytes()
    {
        await using var context = new Nom5Context(); var asset = Nom5Context.Asset(1); var old = await context.Publish(asset, [10, 20]); var current = await context.Publish(asset, [30]);
        var retained = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(0, retained.RevisionsToPrune); Assert.Equal(0, retained.ObjectsToDelete);
        Assert.Equal(old.Version, Assert.Single((await context.Store.LookupResourceOperationAsync(old.Ticket)).Resources));
        await context.Store.PruneResourceOperationsAsync(old.Ticket.Sequence);
        var before = Nom5Context.Snapshot(context.Arguments.Root); var dead = await context.Store.PlanGarbageCollectionAsync();
        Assert.Equal(1, dead.RevisionsToPrune); Assert.Equal(1, dead.ObjectsToDelete); Assert.Equal(2, dead.BytesToDelete); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
        await NomFaultContext.AssertBytes(context.Store, old.Version, [10, 20]);
        var result = await context.Store.CollectGarbageAsync(dead); Assert.Equal(new NomGarbageCollectionReceipt(1, 1, 2, false), result);
        Assert.Equal(2, context.Store.ContentGeneration); Assert.Equal(current.Version, await context.Store.FindResourceAsync(asset.Id));
        Assert.Equal(current.Version, Assert.Single((await context.Store.LookupResourceOperationAsync(current.Ticket)).Resources));
        Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, old.Version.Digest))); await NomFaultContext.AssertBytes(context.Store, current.Version, [30]);
        Assert.Equal(new NomGarbageCollectionReceipt(0, 0, 0, false), await context.Store.CollectGarbageAsync());
    }

    [Fact]
    public async Task GarbageCollection_SharedDigestSurvivesUntilLastLogicalReferenceIsDeleted()
    {
        await using var context = new Nom5Context(); var a = await context.Publish(Nom5Context.Asset(1), [9, 8, 7]); var b = await context.Publish(Nom5Context.Asset(2), [9, 8, 7]);
        Assert.Equal(a.Version.Digest, b.Version.Digest); Assert.NotEqual(a.Version.Revision, b.Version.Revision);
        await context.Store.DeleteResourcesAsync(2, [a.Version.AssetId]); await context.Store.PruneResourceOperationsAsync(b.Ticket.Sequence);
        var first = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, first.RevisionsToPrune); Assert.Equal(0, first.ObjectsToDelete);
        Assert.Equal(new NomGarbageCollectionReceipt(1, 0, 0, false), await context.Store.CollectGarbageAsync(first));
        await NomFaultContext.AssertBytes(context.Store, b.Version, [9, 8, 7]); await context.Store.DeleteResourcesAsync(3, [b.Version.AssetId]);
        var last = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, last.RevisionsToPrune); Assert.Equal(1, last.ObjectsToDelete); Assert.Equal(3, last.BytesToDelete);
        Assert.Equal(new NomGarbageCollectionReceipt(1, 1, 3, false), await context.Store.CollectGarbageAsync(last));
        Assert.Empty((await context.Store.QueryAssetsAsync(new())).Items); Assert.Equal(2, (await context.Store.QueryAssetsAsync(new(IncludeDeleted: true))).Items.Length);
        await context.Reopen(); Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, a.Version.Digest)));
    }

    [Fact]
    public async Task GarbageCollection_StaleForeignAndChangedInventoryPlansRejectWithoutMutation()
    {
        await using var context = new Nom5Context(); var a = await context.Publish(Nom5Context.Asset(1), [1]); var plan = await context.Store.PlanGarbageCollectionAsync();
        await using var other = new Nom5Context(); var foreignBefore = Nom5Context.Snapshot(other.Arguments.Root);
        await Assert.ThrowsAsync<InvalidDataException>(() => other.Store.CollectGarbageAsync(plan)); Nom5Context.AssertSnapshot(foreignBefore, other.Arguments.Root);
        var manifest = await context.Store.CreateSnapshotAsync([a.Version]); var before = Nom5Context.Snapshot(context.Arguments.Root);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.CollectGarbageAsync(plan)); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
        var inventory = await context.Store.PlanGarbageCollectionAsync(); var digest = NomTestDirectory.Digest([5, 4, 3]); var orphan = Nom5Context.ObjectPath(context.Arguments.Root, digest);
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!); File.WriteAllBytes(orphan, [5, 4, 3]); before = Nom5Context.Snapshot(context.Arguments.Root);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.CollectGarbageAsync(inventory)); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
        var fresh = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, fresh.ObjectsToDelete); Assert.Equal(0, fresh.RevisionsToPrune);
        var receipt = await context.Store.CollectGarbageAsync(fresh); Assert.Equal(1, receipt.ObjectsDeleted); Assert.False(File.Exists(orphan));
        await context.Store.RemoveSnapshotAsync(manifest.Id);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task GarbageCollection_ActiveReservationOrReadLeaseRejectsBeforeMarkAndLeavesBytesUsable(bool reader)
    {
        await using var context = new Nom5Context(); var resource = await context.Publish(Nom5Context.Asset(1), [1, 2]); Stream? stream = null; AssetWriteTicket? pending = null;
        if (reader) stream = await context.Store.OpenResourceAsync(resource.Version);
        else pending = await context.Store.ReserveResourcesAsync(1, [NomFaultContext.Spec(Nom5Context.Asset(2), [3])]);
        try
        {
            var before = Nom5Context.Snapshot(context.Arguments.Root);
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.PlanGarbageCollectionAsync()); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
            if (stream != null) Assert.Equal(1, stream.ReadByte()); else Assert.Equal("pending", (await context.Store.LookupResourceOperationAsync(pending!)).Status);
        }
        finally { if (stream != null) await stream.DisposeAsync(); else await context.Store.AbortResourceOperationAsync(pending!); }
        Assert.Equal(0, (await context.Store.PlanGarbageCollectionAsync()).ObjectsToDelete);
    }

    [Fact]
    public async Task GarbageCollection_UnknownNamesAndWrongBucketsArePreservedWhileCanonicalOrphanIsDeleted()
    {
        await using var context = new Nom5Context(); var current = await context.Publish(Nom5Context.Asset(1), [1]); var root = context.Arguments.Root;
        var digest = NomTestDirectory.Digest([2]); var orphan = Nom5Context.ObjectPath(root, digest); Directory.CreateDirectory(Path.GetDirectoryName(orphan)!); File.WriteAllBytes(orphan, [2]);
        var bucket = Path.GetDirectoryName(orphan)!;
        var unknown = new[] { Path.Combine(bucket, "notes.txt"), Path.Combine(bucket, digest + ".blob.backup"), Path.Combine(bucket, (digest[..2] + new string('a', 62)).ToUpperInvariant() + ".blob"),
            Path.Combine(root, "objects", "sha256", "zz", digest + ".blob") };
        foreach (var path in unknown) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, [91, 92]); }
        var plan = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, plan.ObjectsToDelete); Assert.Equal(1, plan.BytesToDelete); Assert.Equal(0, plan.RevisionsToPrune);
        var receipt = await context.Store.CollectGarbageAsync(plan); Assert.Equal(new NomGarbageCollectionReceipt(0, 1, 1, false), receipt); Assert.False(File.Exists(orphan));
        foreach (var path in unknown) Assert.Equal(new byte[] { 91, 92 }, File.ReadAllBytes(path));
        await NomFaultContext.AssertBytes(context.Store, current.Version, [1]);
    }

    [Fact]
    public async Task Snapshot_LiveHandleQuotaRejectsSixtyFifthAndReleasesCapacityOnDispose()
    {
        await using var context = new Nom5Context(); var resource = await context.Publish(Nom5Context.Asset(1), [1]); var manifest = await context.Store.CreateSnapshotAsync([resource.Version]);
        var handles = new List<IAssetSnapshot>();
        try
        {
            for (var i = 0; i < 64; i++) handles.Add(await context.Store.OpenSnapshotAsync(manifest.Id));
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.OpenSnapshotAsync(manifest.Id));
            await handles[0].DisposeAsync(); handles.RemoveAt(0);
            await using var replacement = await context.Store.OpenSnapshotAsync(manifest.Id); Assert.Equal(resource.Version, Assert.Single(replacement.Manifest.Members));
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.RemoveSnapshotAsync(manifest.Id));
        }
        finally { foreach (var handle in handles) await handle.DisposeAsync(); }
        await context.Store.RemoveSnapshotAsync(manifest.Id); Assert.Equal(1, context.Store.ContentGeneration);
    }

    [Fact]
    public async Task Snapshot_EnteredReadRetainsManifestAndRootThroughSnapshotCloseStreamDisposeAndStoreStop()
    {
        using var directory = new NomTestDirectory(); var args = directory.Arguments(); var io = new NomCheckpointIo(); await using var factory = new CatalogFactory(4, io);
        var owner = factory.Create(new(args.Root, args.ProjectId, args.StoreId, true)); var asset = Nom5Context.Asset(1); using var input = new MemoryStream([10, 20, 30], writable: false);
        var ticket = await owner.ReserveResourcesAsync(0, [NomFaultContext.Spec(asset, [10, 20, 30])]);
        var version = Assert.Single((await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input })).Resources);
        var manifest = await owner.CreateSnapshotAsync([version]); await using var snapshot = await owner.OpenSnapshotAsync(manifest.Id); var gate = new ResourceGate();
        await using var stream = await owner.OpenResourceAsync(version, readGate: gate, snapshot: (AssetSnapshot)snapshot);
        var reading = Task.Run(() => stream.ReadByte()); Task? closing = null; Task? stopping = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); await snapshot.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => snapshot.OpenResourceAsync(version));
            closing = stream.DisposeAsync().AsTask(); Assert.False(closing.IsCompleted);
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.RemoveSnapshotAsync(manifest.Id));
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.PlanGarbageCollectionAsync());
            stopping = owner.DisposeAsync().AsTask(); Assert.False(stopping.IsCompleted); Assert.False(owner.IsClosed);
            Assert.Equal(1, factory.Count); NomTestDirectory.AssertRootLocked(args);
            Assert.True(File.Exists(Nom5Context.ObjectPath(args.Root, version.Digest)));
        }
        finally { gate.Release.Set(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reading.WaitAsync(TimeSpan.FromSeconds(5)));
        await closing!.WaitAsync(TimeSpan.FromSeconds(5)); await stopping!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(owner.IsClosed); Assert.Equal(0, factory.Count); Assert.All(io.Opened, entry => Assert.True(entry.Handle.IsClosed)); NomTestDirectory.AssertRootReleased(args);
        await using var reopened = factory.Create(new(args.Root, args.ProjectId, args.StoreId, false));
        await using var retained = await reopened.OpenSnapshotAsync(manifest.Id); Assert.Equal(version, Assert.Single(retained.Manifest.Members));
        await using var bytes = await retained.OpenResourceAsync(version); await Nom5Context.AssertBytes(bytes, [10, 20, 30]);
    }
}
