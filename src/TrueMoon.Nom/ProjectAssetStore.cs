using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom;

internal sealed class ProjectAssetStore(CatalogOwner owner) : IProjectAssetStore
{
    public string Root => owner.Root;
    public Guid ProjectId => owner.Header.ProjectId;
    public Guid StoreId => owner.Header.StoreId;
    public long ContentGeneration => owner.Header.Generation;
    public bool IsClosed => owner.IsClosed;
    public Task<AssetRecord[]> ReadAsync() => owner.ReadAsync();
    public Task<AssetWriteTicket> ReserveResourcesAsync(long expectedGeneration, IReadOnlyList<AssetResourceWrite> specs,
        long maximumTotalBytes = ResourceCodec.MaximumBatch, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(specs);
        return owner.ReserveResourcesAsync(expectedGeneration, specs, maximumTotalBytes, token);
    }
    public Task<AssetWriteReceipt> PublishResourcesAsync(AssetWriteTicket ticket, IReadOnlyDictionary<Guid, Stream> sources, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(sources);
        // The owner captures stream references at submission.
        return owner.PublishResourcesAsync(ticket, sources, token);
    }
    public Task<AssetWriteOutcome> LookupResourceOperationAsync(AssetWriteTicket ticket)
    { ArgumentNullException.ThrowIfNull(ticket); return owner.LookupResourceOperationAsync(ticket); }
    public Task AbortResourceOperationAsync(AssetWriteTicket ticket)
    { ArgumentNullException.ThrowIfNull(ticket); return owner.AbortResourceOperationAsync(ticket); }
    public Task PruneResourceOperationsAsync(long throughSequence) => owner.PruneResourceOperationsAsync(throughSequence);
    public Task<AssetResourceVersion?> FindResourceAsync(Guid assetId) => owner.FindResourceAsync(assetId);
    public Task<Stream> OpenResourceAsync(AssetResourceVersion exact, CancellationToken token = default)
    { ArgumentNullException.ThrowIfNull(exact); return owner.OpenResourceAsync(exact, token); }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => new(owner.DisposeAsync().AsTask());
    public Task<NomCompactionReceipt> CompactAsync(CancellationToken token = default) => owner.CompactAsync(token);
    public Task<AssetPage> QueryAssetsAsync(AssetQuery query, AssetPageCursor? cursor = null, int pageSize = 64, CancellationToken token = default) => owner.QueryAssetsAsync(query,cursor,pageSize,token);
    public Task<AssetResourceVersion[]> ReadDependenciesAsync(AssetResourceVersion exact,bool reverse=false,int maximum=256,CancellationToken token=default) => owner.ReadDependenciesAsync(exact,reverse,maximum,token);
    public Task<AssetResourceVersion[]> GetDependencyClosureAsync(IReadOnlyList<AssetResourceVersion> roots,int maximumMembers=4096,CancellationToken token=default) => owner.GetDependencyClosureAsync(roots,maximumMembers,token);
    public Task<long> DeleteResourcesAsync(long expectedGeneration,IReadOnlyList<Guid> assetIds,CancellationToken token=default) => owner.DeleteResourcesAsync(expectedGeneration,assetIds,token);
    public Task<AssetSnapshotManifest> CreateSnapshotAsync(IReadOnlyList<AssetResourceVersion> roots,int maximumMembers=4096,CancellationToken token=default) => owner.CreateSnapshotAsync(roots,maximumMembers,token);
    public Task<IAssetSnapshot> OpenSnapshotAsync(string id,CancellationToken token=default) => owner.OpenSnapshotAsync(id,token);
    public Task RemoveSnapshotAsync(string id,CancellationToken token=default) => owner.RemoveSnapshotAsync(id,token);
    public Task PinSnapshotAsync(string snapshotId,Guid pinId,CancellationToken token=default) => owner.PinSnapshotAsync(snapshotId,pinId,token);
    public Task ReleaseSnapshotPinAsync(Guid pinId,CancellationToken token=default) => owner.ReleaseSnapshotPinAsync(pinId,token);
    public Task<NomGarbageCollectionPlan> PlanGarbageCollectionAsync(CancellationToken token=default) => owner.PlanGarbageCollectionAsync(token);
    public Task<NomGarbageCollectionReceipt> CollectGarbageAsync(NomGarbageCollectionPlan? plan=null,CancellationToken token=default) => owner.CollectGarbageAsync(plan,token);
    public Task<NomBackupReceipt> ExportBackupAsync(string destinationRoot,NomBackupOptions? options=null,CancellationToken token=default) => owner.ExportBackupAsync(destinationRoot,options,token);
}
