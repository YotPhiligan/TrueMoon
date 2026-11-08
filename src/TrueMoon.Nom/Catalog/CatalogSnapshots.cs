using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    private readonly HashSet<AssetSnapshot> snapshots = [];
    private readonly Dictionary<ResourceReadStream,string> snapshotReaders = [];
    internal Task<AssetSnapshotManifest> CreateSnapshotAsync(IReadOnlyList<AssetResourceVersion> roots,int maximumMembers=4096,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(roots); var captured = NomRetentionCodec.CaptureVersions(roots,256);
        Require(captured.Length > 0 && maximumMembers is > 0 and <= 4096,"Snapshot root/member budget.");
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); owner.RequireNoGarbage();
            lock(owner.admission) Require(owner.activeResourceWrite == null,"Finish resource reservation before snapshot creation.");
            var members = owner.Catalog.Index.Closure(captured,maximumMembers);
            var manifest = new AssetSnapshotManifest(NomRetentionCodec.ManifestId(owner.Header.ProjectId,owner.Header.StoreId,owner.Header.Generation,members),owner.Header.Generation,members);
            if (owner.Catalog.Manifest(manifest.Id) != null) return manifest;
            owner.Begin(); try { owner.Catalog.PutManifest(manifest); token.ThrowIfCancellationRequested(); owner.Commit(); return manifest; }
            catch(Exception error) { owner.Rollback(error); throw; }
        },captured.Length*104,token);
    }
    internal Task<IAssetSnapshot> OpenSnapshotAsync(string id,CancellationToken token=default)
    {
        ResourceCodec.Digest(id);
        return Enqueue<IAssetSnapshot>(owner =>
        {
            owner.RequireResourceReady(); var manifest = owner.Catalog.Manifest(id) ?? throw new KeyNotFoundException("Unknown snapshot.");
            lock(owner.admission)
            {
                ObjectDisposedException.ThrowIf(owner.stopping,owner); Require(owner.snapshots.Count < 64,"Snapshot lease budget.");
                var result = new AssetSnapshot(owner,manifest); owner.snapshots.Add(result); owner.resourceUsers++; return result;
            }
        },token:token);
    }
    internal void ReleaseSnapshot(AssetSnapshot snapshot)
    {
        lock(admission)
        { snapshot.Closed = true; if (snapshots.Remove(snapshot)) resourceUsers--; CompleteAfterResourceDrain(); }
    }
    internal Task RemoveSnapshotAsync(string id,CancellationToken token=default)
    {
        ResourceCodec.Digest(id);
        return Enqueue(owner =>
        {
            owner.RequireResourceReady();
            lock(owner.admission) Require(owner.activeResourceWrite == null && !owner.snapshots.Any(s => s.Manifest.Id == id) && !owner.snapshotReaders.Values.Contains(id),"Snapshot has active handles/streams or reservation.");
            Require(!owner.Catalog.Pins().Any(p => p.Manifest == id),"Snapshot has durable pins.");
            var manifest = owner.Catalog.Manifest(id); if (manifest == null) return true;
            owner.Begin(); try { owner.Catalog.RemoveManifest(manifest); token.ThrowIfCancellationRequested(); owner.Commit(); return true; }
            catch(Exception error) { owner.Rollback(error); throw; }
        },token:token);
    }
    internal Task PinSnapshotAsync(string id,Guid pin,CancellationToken token=default)
    {
        ResourceCodec.Digest(id); Require(pin != Guid.Empty,"Empty pin identity.");
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); lock(owner.admission) Require(owner.activeResourceWrite == null,"Resource reservation active.");
            Require(owner.Catalog.Manifest(id) != null,"Unknown pinned snapshot.");
            if (owner.Catalog.Pins().Any(p => p.Pin == pin && p.Manifest == id)) return true;
            owner.Begin(); try { owner.Catalog.PutPin(pin,id); token.ThrowIfCancellationRequested(); owner.Commit(); return true; }
            catch(Exception error) { owner.Rollback(error); throw; }
        },token:token);
    }
    internal Task ReleaseSnapshotPinAsync(Guid pin,CancellationToken token=default)
    {
        Require(pin != Guid.Empty,"Empty pin identity.");
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); lock(owner.admission) Require(owner.activeResourceWrite == null,"Resource reservation active.");
            if (!owner.Catalog.Pins().Any(p => p.Pin == pin)) return true;
            owner.Begin(); try { owner.Catalog.DeletePin(pin); token.ThrowIfCancellationRequested(); owner.Commit(); return true; }
            catch(Exception error) { owner.Rollback(error); throw; }
        },token:token);
    }
}
