using System.Collections.Immutable;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;

namespace TrueMoon.Aether.Runtime.Gpu.Vulkan;

public sealed partial class VulkanSharedFrame
{
    internal IFactory<IRenderSnapshotSubmission, RenderSnapshotData> SnapshotSubmissions(GpuFrameStamp stamp, RenderSnapshotResources resources,
        ImmutableArray<RenderPrimitive> overlay)
        => new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(data => new SnapshotUse(this, stamp, resources, data, overlay));

    private async Task ProduceSnapshotAsync(GpuFrameStamp stamp, RenderSnapshotData snapshot, RenderSnapshotResources resources,
        ImmutableArray<RenderPrimitive> overlay, CancellationToken token)
    {
        await WaitGatesAsync(token).ConfigureAwait(false);
        try
        {
            EnsureOpen(); Surfaces.GpuSlotLedger.ValidateStamp(stamp, _identity);
            if (!_reusable || _submitted || (_produced && !_retired) || stamp.Slot != Offer.Stamp.Slot ||
                stamp.SurfaceGeneration != Offer.Stamp.SurfaceGeneration || stamp.FrameId != (_produced ? checked(_stamp.FrameId + 1) : 1) ||
                snapshot.Configuration.SurfaceGeneration != stamp.SurfaceGeneration || snapshot.Identity.SessionId != _identity.SessionId)
                throw new InvalidDataException("Snapshot is stale or belongs to another surface/session.");
            if (_meshRenderer == null)
            {
                _meshRenderer = new(_vk!, _physical, _device, _image, _width, _height) { MeshProfile = true };
                // Retain partial renderer ownership before any native creation.
                _meshRenderer.Initialize();
            }
            _stamp = stamp;
            Submit(false, snapshot: snapshot, resources: resources, overlay: overlay);
            await FenceAsync(token).ConfigureAwait(false);
            _produced = true; _retired = false;
        }
        finally { ReleaseGates(); }
    }

    private async Task ConfirmSnapshotCleanupAsync()
    {
        await WaitGatesAsync(CancellationToken.None).ConfigureAwait(false);
        try { if (_submitted) await FenceAsync(CancellationToken.None).ConfigureAwait(false); }
        finally { ReleaseGates(); }
    }

    private sealed class SnapshotUse(VulkanSharedFrame frame, GpuFrameStamp stamp, RenderSnapshotResources resources, RenderSnapshotData data,
        ImmutableArray<RenderPrimitive> overlay)
        : IRenderSnapshotSubmission
    {
        private Task? _work;
        private readonly Lock _workGate = new();
        public bool IsComplete { get; private set; }
        public bool IsReleased { get; private set; }
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            lock (_workGate)
            {
                ObjectDisposedException.ThrowIf(IsReleased, this);
                return _work ??= ExecuteCoreAsync(cancellationToken);
            }
        }
        private async Task ExecuteCoreAsync(CancellationToken token)
        { await frame.ProduceSnapshotAsync(stamp, data, resources, overlay, token).ConfigureAwait(false); IsComplete = true; }
        public async ValueTask DisposeAsync()
        {
            Task? work; lock (_workGate) work = _work;
            if (work != null) { try { await work.ConfigureAwait(false); } catch { /* Original failure belongs to the submission ledger. */ } }
            await frame.ConfirmSnapshotCleanupAsync().ConfigureAwait(false);
            IsReleased = true;
        }
    }
}
