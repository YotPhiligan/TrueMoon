using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Runtime.Rendering.Resources;

// Exclusive internal retain for a bounded snapshot envelope; never an unrestricted public clone.
internal sealed class RenderSnapshotResources(RenderDeviceOwner device, RenderManifestLease content,
    List<RenderDeviceOwner.EntryLease> entries, ulong bindingRevision) : IAsyncDisposable
{
    private readonly SemaphoreSlim _cleanup = new(1, 1);
    internal RenderDeviceOwner Device { get; } = device;
    internal RenderContentManifest Manifest => content.Manifest;
    internal ulong BindingRevision { get; } = bindingRevision;
    internal bool HasBaseColorTextures { get; } = entries.Exists(static entry => entry.Value.Value.Definition is BaseColorTextureDefinition);
    internal bool IsReleased { get; private set; }
    internal RenderResourceHandle Handle(RenderAssetKey key)
    {
        ObjectDisposedException.ThrowIf(IsReleased, this);
        var entry = entries.FirstOrDefault(item => item.Value.Value.Key == key);
        ResourceGuard.Require(entry != null, RenderResourceError.StaleHandle, "GPU resource is not retained by this snapshot.");
        Device.ValidateHandle(entry!.Value.Handle); return entry.Value.Handle;
    }
    public async ValueTask DisposeAsync()
    {
        await _cleanup.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReleased) return;
            var errors = new List<Exception>();
            for (var i = entries.Count - 1; i >= 0; i--)
            { try { await entries[i].ReleaseAsync().ConfigureAwait(false); entries.RemoveAt(i); } catch (Exception error) { errors.Add(error); } }
            if (errors.Count != 0) throw new AggregateException("Snapshot GPU resources remain retained.", errors);
            content.Dispose(); IsReleased = true;
        }
        finally { _cleanup.Release(); }
    }
}
