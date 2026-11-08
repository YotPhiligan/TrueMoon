using System.Text;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.AssetTool;

/// <summary>A headless consumer of the public catalog, exact resources and maintenance APIs.</summary>
public sealed class AssetTransferWorkflow(
    IFactory<IProjectAssetStore, OpenAssetStoreArguments> openFactory,
    IFactory<IProjectAssetStore, RestoreAssetStoreArguments> restoreFactory)
{
    public async Task<long> ExecuteAsync(ToolArguments arguments, Action<bool, string> check)
    {
        if (arguments.Command == "create") return await CreateFrozenDemo(arguments, check);
        var receipt = DemoReceipt.Read(arguments.Receipt, arguments.ProjectId, arguments.StoreId);
        await using var store = arguments.Command == "restore"
            ? restoreFactory.Create(new(arguments.BackupRoot!, arguments.Root, arguments.ProjectId, arguments.StoreId))
            : openFactory.Create(new(arguments.Root, arguments.ProjectId, arguments.StoreId, AssetStoreOpenMode.Open));
        check(store.ProjectId == receipt.ProjectId && store.StoreId == receipt.StoreId && store.ContentGeneration == receipt.Generation,
            "PersistentProjectStoreIdentityAndGeneration");
        switch (arguments.Command)
        {
            case "verify": await VerifyFrozenState(store, receipt, check); break;
            case "export":
                await VerifyFrozenState(store, receipt, check);
                var exported = await store.ExportBackupAsync(arguments.BackupRoot!);
                check(exported.Root == arguments.BackupRoot && exported.ProjectId == receipt.ProjectId && exported.StoreId == receipt.StoreId &&
                    exported.Generation == 3 && exported.Sequence > exported.Generation && exported.Files >= 4 && exported.Bytes > 0 && DemoReceipt.IsDigest(exported.ManifestDigest),
                    "ExportFrozenStateToVerifiedImmutableBackup");
                break;
            case "restore":
                await VerifyFrozenState(store, receipt, check);
                check(store.Root == arguments.Root, "RestoreFrozenStateThroughTypedFactory");
                break;
            case "collect": await CollectUnpinnedHistory(store, receipt, check); break;
            case "verify-current": await VerifyCurrentState(store, receipt, check, collected: true); break;
        }
        return store.ContentGeneration;
    }
    private async Task<long> CreateFrozenDemo(ToolArguments arguments, Action<bool, string> check)
    {
        await using var store = openFactory.Create(new(arguments.Root, arguments.ProjectId, arguments.StoreId, AssetStoreOpenMode.Create));
        var sourceId = Guid.NewGuid(); var shaderId = Guid.NewGuid(); var meshId = Guid.NewGuid();
        var shaderAsset = new AssetRecord(shaderId, sourceId, DemoReceipt.ShaderKind, "shaders/demo", Encoding.UTF8.GetBytes("opaque-shader/1"), ["scene", "shader"]);
        var shader = await Publish(store, shaderAsset, DemoReceipt.ShaderBytes(), check);
        var oldAsset = new AssetRecord(meshId, sourceId, DemoReceipt.MeshKind, "models/demo", Encoding.UTF8.GetBytes("opaque-mesh/1"), ["geometry", "model", "scene"], [shader.Version]);
        var old = await Publish(store, oldAsset, DemoReceipt.MeshBytes(false), check);
        var snapshot = await store.CreateSnapshotAsync([old.Version]); var pin = Guid.NewGuid(); await store.PinSnapshotAsync(snapshot.Id, pin);
        check(snapshot.Generation == 2 && snapshot.Members.Length == 2 && snapshot.Members.Contains(shader.Version) && snapshot.Members.Contains(old.Version), "CreateFrozenExactDependencyManifest");
        var currentAsset = new AssetRecord(meshId, sourceId, DemoReceipt.MeshKind, "models/demo", Encoding.UTF8.GetBytes("opaque-mesh/2"), ["current", "geometry", "model", "scene"], [shader.Version]);
        var current = await Publish(store, currentAsset, DemoReceipt.MeshBytes(true), check);
        await store.PruneResourceOperationsAsync(old.Ticket.Sequence);
        var receipt = new DemoReceipt(1, arguments.ProjectId, arguments.StoreId, sourceId, shaderId, meshId, shader.Version, old.Version, current.Version,
            shader.Ticket, old.Ticket, current.Ticket, snapshot.Id, snapshot.Generation, pin, store.ContentGeneration);
        receipt.Validate(arguments.ProjectId, arguments.StoreId); await VerifyFrozenState(store, receipt, check);
        await using (var file = new FileStream(arguments.Receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(file, receipt, DemoReceipt.Json); await file.FlushAsync(); file.Flush(true); }
        check(store.ContentGeneration == 3 && receipt.CurrentMeshTicket.Sequence > receipt.OldMeshTicket.Sequence, "CreateFrozenDemoAndDurableNamedReceipt");
        return store.ContentGeneration;
    }
    private static async Task<(AssetWriteTicket Ticket, AssetResourceVersion Version)> Publish(IProjectAssetStore store, AssetRecord asset, byte[] bytes, Action<bool, string> check)
    {
        var ticket = await store.ReserveResourcesAsync(store.ContentGeneration, [new AssetResourceWrite(asset, bytes.Length, DemoReceipt.Digest(bytes), Math.Max(1, bytes.Length))]);
        using var source = new MemoryStream(bytes, writable: false);
        var result = await store.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = source });
        check(!result.CleanupPending && result.Resources.Length == 1 && result.Resources[0].AssetId == asset.Id && result.Resources[0].Length == bytes.Length &&
            result.Resources[0].Digest == DemoReceipt.Digest(bytes) && source.CanRead, "PublishOpaqueResourceWithBorrowedInputAndExactReceipt");
        return (ticket, result.Resources.Single());
    }
    private static async Task VerifyFrozenState(IProjectAssetStore store, DemoReceipt receipt, Action<bool, string> check)
    {
        await VerifyCurrentState(store, receipt, check, collected: false);
        await ExpectExpired(store, receipt.ShaderTicket, check); await ExpectExpired(store, receipt.OldMeshTicket, check);
        var outcome = await store.LookupResourceOperationAsync(receipt.CurrentMeshTicket);
        check(outcome.Status == "committed" && outcome.Generation == 3 && outcome.Resources.Length == 1 && outcome.Resources[0] == receipt.CurrentMesh,
            "DurableCurrentOutcomeAndExpiredOldOperationFloor");
        var refused = false; try { await store.RemoveSnapshotAsync(receipt.SnapshotId); } catch (InvalidDataException) { refused = true; }
        check(refused, "DurablePinRejectsRemovalBeforeAnyRuntimeLease");
        await using (var snapshot = await store.OpenSnapshotAsync(receipt.SnapshotId))
        {
            check(snapshot.Manifest.Id == receipt.SnapshotId && snapshot.Manifest.Generation == receipt.SnapshotGeneration && snapshot.Manifest.Members.Length == 2 &&
                snapshot.Manifest.Members.Contains(receipt.Shader) && snapshot.Manifest.Members.Contains(receipt.OldMesh), "ReopenedFrozenExactDependencyManifest");
            await using var stream = await snapshot.OpenResourceAsync(receipt.OldMesh);
            await VerifyBytes(stream, DemoReceipt.MeshBytes(false), check, "FrozenOldGeometryBytesAfterReimportAndReopen");
        }
        var plan = await store.PlanGarbageCollectionAsync();
        check(plan.RevisionsToPrune == 0 && plan.ObjectsToDelete == 0 && plan.BytesToDelete == 0, "PinnedManifestRetainsOldBytesAfterOutcomePrune");
    }
    private static async Task VerifyCurrentState(IProjectAssetStore store, DemoReceipt receipt, Action<bool, string> check, bool collected)
    {
        check(store.ContentGeneration == 3 && await store.FindResourceAsync(receipt.MeshId) == receipt.CurrentMesh && await store.FindResourceAsync(receipt.ShaderId) == receipt.Shader,
            "CurrentExactHeadsRemainStable");
        var page = await store.QueryAssetsAsync(new(Source: receipt.SourceId), pageSize: 1);
        check(page.Generation == 3 && page.Items.Length == 1 && page.Next != null, "IndexedSourceFirstPage");
        var second = await store.QueryAssetsAsync(new(Source: receipt.SourceId), page.Next, pageSize: 1);
        check(second.Items.Length == 1 && second.Next == null && new[] { page.Items[0].Id, second.Items[0].Id }.SequenceEqual(new[] { receipt.MeshId, receipt.ShaderId }.Order()),
            "IndexedSourceExclusiveContinuationAndGuidOrder");
        var mesh = await store.QueryAssetsAsync(new(receipt.SourceId, DemoReceipt.MeshKind, "current"));
        check(mesh.Items.Length == 1 && mesh.Items[0].Id == receipt.MeshId && mesh.Items[0].Locator == "models/demo" &&
            mesh.Items[0].Metadata.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes("opaque-mesh/2")) && mesh.Items[0].Dependencies.SequenceEqual([receipt.Shader]),
            "IndexedKindTagAndCurrentMetadata");
        var direct = await store.ReadDependenciesAsync(receipt.CurrentMesh); var reverse = await store.ReadDependenciesAsync(receipt.Shader, reverse: true);
        check(direct.SequenceEqual([receipt.Shader]) && reverse.Length == (collected ? 1 : 2) && reverse.Contains(receipt.CurrentMesh) && (collected || reverse.Contains(receipt.OldMesh)),
            "ForwardAndReverseDependenciesUseExactVersions");
        var closure = await store.GetDependencyClosureAsync([receipt.CurrentMesh]);
        check(closure.SequenceEqual(new[] { receipt.Shader, receipt.CurrentMesh }.OrderBy(v => v.Revision, StringComparer.Ordinal)), "CompleteCurrentExactDependencyClosure");
        await using (var current = await store.OpenResourceAsync(receipt.CurrentMesh)) await VerifyBytes(current, DemoReceipt.MeshBytes(true), check, "CurrentGeometryExactBytes");
        await using (var shader = await store.OpenResourceAsync(receipt.Shader)) await VerifyBytes(shader, DemoReceipt.ShaderBytes(), check, "OpaqueShaderExactBytes");
        if (!collected) return;
        await ExpectExpired(store, receipt.ShaderTicket, check); await ExpectExpired(store, receipt.OldMeshTicket, check); await ExpectExpired(store, receipt.CurrentMeshTicket, check);
        var missingOld = false; try { await using var unexpected = await store.OpenResourceAsync(receipt.OldMesh); } catch (InvalidDataException) { missingOld = true; }
        check(missingOld, "CollectedOldExactRevisionCannotOpen");
        var missingSnapshot = false; try { await using var unexpected = await store.OpenSnapshotAsync(receipt.SnapshotId); } catch (KeyNotFoundException) { missingSnapshot = true; }
        check(missingSnapshot, "ExplicitlyRemovedManifestDoesNotReappear");
        var plan = await store.PlanGarbageCollectionAsync(); check(plan.RevisionsToPrune == 0 && plan.ObjectsToDelete == 0 && !plan.HasMore, "NoRemainingGarbageAfterReopen");
    }
    private static async Task CollectUnpinnedHistory(IProjectAssetStore store, DemoReceipt receipt, Action<bool, string> check)
    {
        await store.ReleaseSnapshotPinAsync(receipt.PinId); await store.RemoveSnapshotAsync(receipt.SnapshotId);
        await store.PruneResourceOperationsAsync(receipt.CurrentMeshTicket.Sequence);
        var resumed = await store.CollectGarbageAsync(); check(!resumed.CleanupPending, "ExplicitGcCleanupResume");
        var observedOld = false;
        try { await using var old = await store.OpenResourceAsync(receipt.OldMesh); observedOld = true; }
        catch (InvalidDataException) { }
        var first = await store.PlanGarbageCollectionAsync();
        check(first.RevisionsToPrune == (observedOld ? 1 : 0) && first.ObjectsToDelete == (observedOld ? 1 : 0) &&
            first.BytesToDelete == (observedOld ? DemoReceipt.OldMeshLength : 0), "GcDryRunSelectsOnlyUnpinnedOldGeometry");
        var revisions = 0; var objects = 0; long bytes = 0; var plan = first;
        for (var slice = 0; ; slice++)
        {
            DemoReceipt.Require(slice < 16, "Demo GC slice budget.");
            var result = await store.CollectGarbageAsync(plan); DemoReceipt.Require(!result.CleanupPending, "GC cleanup pending; retry collect after fixing IO.");
            revisions += result.RevisionsPruned; objects += result.ObjectsDeleted; bytes += result.BytesDeleted;
            if (!plan.HasMore) break; plan = await store.PlanGarbageCollectionAsync();
        }
        check(revisions == (observedOld ? 1 : 0) && objects == (observedOld ? 1 : 0) && bytes == (observedOld ? DemoReceipt.OldMeshLength : 0),
            "CollectUnpinnedHistoryPreservesCurrentContentGeneration");
        await store.CompactAsync(); await VerifyCurrentState(store, receipt, check, collected: true);
    }
    private static async Task ExpectExpired(IProjectAssetStore store, AssetWriteTicket ticket, Action<bool, string> check)
    {
        var expired = false; try { await store.LookupResourceOperationAsync(ticket); } catch (ExpiredOperationException) { expired = true; }
        check(expired, "ExpiredOperationTicketCannotReplay");
    }
    private static async Task VerifyBytes(Stream stream, byte[] expected, Action<bool, string> check, string name)
    {
        check(stream.CanRead && stream.CanSeek && !stream.CanWrite && stream.Position == 0 && stream.Length == expected.Length, name + "StreamContract");
        var buffer = new byte[64 * 1024]; var position = 0;
        while (true)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0) break;
            DemoReceipt.Require(count <= buffer.Length && position <= expected.Length - count && buffer.AsSpan(0, count).SequenceEqual(expected.AsSpan(position, count)), name + " content mismatch.");
            position += count;
        }
        check(position == expected.Length, name);
    }
}
