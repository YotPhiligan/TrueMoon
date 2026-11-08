using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Retention;

internal sealed class AssetSnapshot(CatalogOwner owner,AssetSnapshotManifest manifest) : IAssetSnapshot
{
    internal bool Closed;
    public AssetSnapshotManifest Manifest { get; } = manifest;
    public Task<Stream> OpenResourceAsync(AssetResourceVersion exact,CancellationToken token=default)
    { ArgumentNullException.ThrowIfNull(exact); return owner.OpenResourceAsync(exact,token,snapshot:this); }
    public void Dispose() => owner.ReleaseSnapshot(this);
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
