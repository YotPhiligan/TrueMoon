using TrueMoon.Dependencies;
using Xunit;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class Nom6BackupTests
{
    [Fact]
    public async Task BackupRestore_FrozenExactStateRetainsIdentitiesOutcomesFloorPinsTombstonesAndDependencyBytes()
    {
        await using var context = new Nom6Context();
        var dependency = await context.Publish(Nom5Context.Asset(1, tags: ["texture"]), [1, 2]);
        var old = await context.Publish(Nom5Context.Asset(2, tags: ["geometry"], dependencies: [dependency.Version]), [10, 11]);
        var manifest = await context.Store.CreateSnapshotAsync([old.Version]); var pin = Nom5Context.Id(901); await context.Store.PinSnapshotAsync(manifest.Id, pin);
        var current = await context.Publish(Nom5Context.Asset(2, tags: ["geometry", "new"], dependencies: [dependency.Version]), [20]);
        var removed = await context.Publish(Nom5Context.Asset(3), [30]); await context.Store.DeleteResourcesAsync(4, [removed.Version.AssetId]);
        await context.Store.PruneResourceOperationsAsync(dependency.Ticket.Sequence); await context.Store.CompactAsync();
        var sourceBefore = Nom5Context.Snapshot(context.Arguments.Root); context.Io.MaximumWrite = 0;
        var receipt = await context.Store.ExportBackupAsync(context.Backup);
        Assert.Equal(context.Backup, receipt.Root); Assert.Equal(context.Arguments.ProjectId, receipt.ProjectId); Assert.Equal(context.Arguments.StoreId, receipt.StoreId);
        Assert.Equal(5, receipt.Generation); Assert.True(receipt.Sequence > receipt.Generation); Assert.True(receipt.Files >= 4); Assert.InRange(context.Io.MaximumWrite, 1, ResourceCodec.BufferBytes);
        Nom5Context.AssertSnapshot(sourceBefore, context.Arguments.Root); Assert.True(File.Exists(Path.Combine(context.Backup, "nom.backup")));
        var backupBefore = Nom5Context.Snapshot(context.Backup);
        var replacement = await context.Publish(Nom5Context.Asset(2, tags: ["future"]), [99]); Assert.Equal(6, context.Store.ContentGeneration);
        await using var restored = context.RestoreFactory.Create(context.RestoreArguments());
        Assert.Equal(5, restored.ContentGeneration); Assert.Equal(context.Arguments.ProjectId, restored.ProjectId); Assert.Equal(context.Arguments.StoreId, restored.StoreId);
        Assert.False(File.Exists(Path.Combine(restored.Root, "nom.backup"))); Assert.Equal(current.Version, await restored.FindResourceAsync(current.Version.AssetId));
        Assert.Null(await restored.FindResourceAsync(removed.Version.AssetId)); Assert.Equal(2, (await restored.QueryAssetsAsync(new())).Items.Length);
        Assert.Equal(3, (await restored.QueryAssetsAsync(new(IncludeDeleted: true))).Items.Length);
        Assert.Equal(new[] { dependency.Version }, await restored.ReadDependenciesAsync(old.Version));
        await Assert.ThrowsAsync<ExpiredOperationException>(() => restored.LookupResourceOperationAsync(dependency.Ticket));
        Assert.Equal(old.Version, Assert.Single((await restored.LookupResourceOperationAsync(old.Ticket)).Resources));
        await Assert.ThrowsAsync<InvalidDataException>(() => restored.RemoveSnapshotAsync(manifest.Id));
        await using (var frozen = await restored.OpenSnapshotAsync(manifest.Id))
        {
            Assert.Equal(manifest.Members, frozen.Manifest.Members); await using var stream = await frozen.OpenResourceAsync(old.Version); await Nom5Context.AssertBytes(stream, [10, 11]);
        }
        await restored.ReleaseSnapshotPinAsync(pin); await restored.RemoveSnapshotAsync(manifest.Id);
        await NomFaultContext.AssertBytes(restored, current.Version, [20]); await NomFaultContext.AssertBytes(context.Store, replacement.Version, [99]);
        Nom5Context.AssertSnapshot(backupBefore, context.Backup);
    }

    [Fact]
    public async Task BackupRestore_EmptyCatalogAndZeroLengthObjectRoundtripRemainWritable()
    {
        await using var context = new Nom6Context(); var emptyBackup = Path.Combine(context.Directory.Root, "empty-backup");
        var first = await context.Store.ExportBackupAsync(emptyBackup); Assert.Equal(0, first.Generation);
        await using (var restored = context.RestoreFactory.Create(context.RestoreArguments(backup: emptyBackup)))
        { Assert.Empty(await restored.ReadAsync()); var created = await Nom3Fixtures.Publish(restored, Nom5Context.Asset(4), []); Assert.Equal(0, created.Version.Length); Assert.Equal(1, restored.ContentGeneration); }
        var zero = await context.Publish(Nom5Context.Asset(1), []); await context.Store.ExportBackupAsync(context.Backup);
        await using var reopened = context.RestoreFactory.Create(context.RestoreArguments(Path.Combine(context.Directory.Root, "zero")));
        Assert.Equal(zero.Version, await reopened.FindResourceAsync(zero.Version.AssetId)); await NomFaultContext.AssertBytes(reopened, zero.Version, []);
    }

    [Fact]
    public async Task Export_OnlySelectedMetadataAndReferencedCasAreCopiedAndBackupCannotOpenAsWritableStore()
    {
        await using var context = new Nom6Context(); var first = await context.Publish(Nom5Context.Asset(1), [10]); var second = await context.Publish(Nom5Context.Asset(1), [20]);
        await context.Store.PruneResourceOperationsAsync(second.Ticket.Sequence); await context.Store.CompactAsync();
        var orphan = Nom5Context.ObjectPath(context.Arguments.Root, NomTestDirectory.Digest([99])); Directory.CreateDirectory(Path.GetDirectoryName(orphan)!); File.WriteAllBytes(orphan, [99]);
        File.WriteAllText(Path.Combine(context.Arguments.Root, "notes.txt"), "owned by caller"); Directory.CreateDirectory(Path.Combine(context.Arguments.Root, "staging")); File.WriteAllBytes(Path.Combine(context.Arguments.Root, "staging", "sentinel"), [88]);
        var receipt = await context.Store.ExportBackupAsync(context.Backup); Assert.False(File.Exists(Path.Combine(context.Backup, "notes.txt"))); Assert.False(Directory.Exists(Path.Combine(context.Backup, "staging")));
        Assert.False(File.Exists(Nom5Context.ObjectPath(context.Backup, NomTestDirectory.Digest([99])))); Assert.True(File.Exists(Nom5Context.ObjectPath(context.Backup, first.Version.Digest)));
        var before = Nom5Context.Snapshot(context.Backup);
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(context.Arguments with { Root = context.Backup, Mode = AssetStoreOpenMode.Open }));
        Nom5Context.AssertSnapshot(before, context.Backup);
        await using var restored = context.RestoreFactory.Create(context.RestoreArguments()); await NomFaultContext.AssertBytes(restored, first.Version, [10]); await NomFaultContext.AssertBytes(restored, second.Version, [20]);
        Assert.Equal(Directory.EnumerateFiles(context.Backup, "*", SearchOption.AllDirectories).Count() - 2, receipt.Files);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Export_ActiveReservationOrReadLeaseRejectsWithoutChangingSourceOrDestination(bool read)
    {
        await using var context = new Nom6Context(); var resource = await context.Publish(Nom5Context.Asset(1), [1, 2]);
        Stream? stream = null; AssetWriteTicket? ticket = null;
        if (read) stream = await context.Store.OpenResourceAsync(resource.Version);
        else ticket = await context.Store.ReserveResourcesAsync(1, [new(Nom5Context.Asset(2))]);
        try
        {
            var before = Nom5Context.Snapshot(context.Arguments.Root); context.Io.Events.Clear();
            await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.ExportBackupAsync(context.Backup));
            Assert.False(Directory.Exists(context.Backup)); Assert.DoesNotContain(context.Io.Events, e => e.Point is "write" or "create-backup" or "publish-backup"); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
            if (stream != null) Assert.Equal(1, stream.ReadByte());
        }
        finally { if (stream != null) await stream.DisposeAsync(); if (ticket != null) await context.Store.AbortResourceOperationAsync(ticket); }
        Assert.True((await context.Store.ExportBackupAsync(context.Backup)).Files > 0);
    }

    [Fact]
    public async Task RestoreFactory_IsLazyNonOwningAppAliasAndStopClosesAdmission()
    {
        await using var context = new Nom6Context(); Assert.Same(context.RestoreFactory, context.App.Services.Resolve<IFactory<IProjectAssetStore, RestoreAssetStoreArguments>>());
        Assert.False(context.RestoreFactory is IDisposable); Assert.False(context.RestoreFactory is IAsyncDisposable); Assert.False(Directory.Exists(context.Backup));
        await context.Store.ExportBackupAsync(context.Backup); var restored = context.RestoreFactory.Create(context.RestoreArguments()); var host = NomTestDirectory.Host(context.App);
        Assert.Equal(2, host.ActiveStoreCount); await host.StopAsync(); Assert.True(restored.IsClosed); Assert.True(context.Store.IsClosed); Assert.True(host.IsReleased); Assert.Equal(0, host.ActiveStoreCount);
        Assert.Throws<ObjectDisposedException>(() => context.RestoreFactory.Create(context.RestoreArguments(Path.Combine(context.Directory.Root, "late"))));
    }

    [Fact]
    public async Task Export_PendingGarbageQueueRejectsAcrossReopenUntilExplicitCleanupResume()
    {
        await using var context=new Nom6Context(); var old=await context.Publish(Nom5Context.Asset(1),[1]); var current=await context.Publish(Nom5Context.Asset(1),[2]);
        await context.Store.PruneResourceOperationsAsync(old.Ticket.Sequence); var plan=await context.Store.PlanGarbageCollectionAsync(); context.Io.Arm("delete",new IOException("retain GC intent"));
        Assert.True((await context.Store.CollectGarbageAsync(plan)).CleanupPending); context.Io.Disarm();
        await Assert.ThrowsAsync<InvalidDataException>(()=>context.Store.ExportBackupAsync(context.Backup)); Assert.False(Directory.Exists(context.Backup));
        await context.Reopen(); await Assert.ThrowsAsync<InvalidDataException>(()=>context.Store.ExportBackupAsync(context.Backup));
        Assert.False((await context.Store.CollectGarbageAsync()).CleanupPending); var receipt=await context.Store.ExportBackupAsync(context.Backup); Assert.Equal(2,receipt.Generation);
        await using var restored=context.RestoreFactory.Create(context.RestoreArguments()); Assert.Equal(current.Version,await restored.FindResourceAsync(current.Version.AssetId)); await NomFaultContext.AssertBytes(restored,current.Version,[2]);
    }
}
