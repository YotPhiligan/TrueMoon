using System.Collections.Immutable;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;

namespace TrueMoon.Aether.Runtime;

// One App-local required view, with early producer cleanup and retained App fallback.
internal sealed class RuntimeMeshRenderHost(IFactory<RenderResourceHost, IFactory<IRenderResourceBackend, RenderDeviceRequest>> roots) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _end = new();
    private RenderResourceHost? _resources;
    private RenderDeviceOwner? _device;
    private RenderWorldBinding? _binding;
    private RenderSnapshotView? _view;
    private RenderSceneSource? _source;
    private IFactory<IRenderResourceBackend, RenderDeviceRequest>? _backends;
    private PreviewViewContext? _previewContext;
    private int _closed;

    internal Task<GpuFrameData> CaptureAsync(RenderSceneSource source, ulong tick, GpuSurfaceRenderRequest request, CancellationToken token)
        => CaptureCoreAsync(source, tick, request, token, null, default);

    internal Task<GpuFrameData> CapturePreviewAsync(RenderSceneSource source, PreviewViewState view, ImmutableArray<RenderPrimitive> overlay,
        GpuSurfaceRenderRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(view);
        return CaptureCoreAsync(source, 0, request, token, view, overlay);
    }

    private async Task<GpuFrameData> CaptureCoreAsync(RenderSceneSource source, ulong tick, GpuSurfaceRenderRequest request,
        CancellationToken token, PreviewViewState? preview, ImmutableArray<RenderPrimitive> overlay)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(source.World); ArgumentNullException.ThrowIfNull(source.Content);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _end.Token); token = linked.Token;
        if (source.World.IsDisposed || source.World.IsFaulted || source.CameraRevision == 0) throw new InvalidDataException("Required mesh source is unavailable.");
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            if (preview == null && _previewContext != null || preview != null && _previewContext == null && _resources != null)
                throw new InvalidDataException("Cannot switch an active mesh host between game and preview workloads.");
            if (preview != null)
            {
                if (_previewContext != null && _previewContext != preview.Context ||
                    preview.Context.SceneId != source.SceneId.Value || preview.Context.SceneRevision != source.SourceRevision ||
                    preview.Camera.Revision != source.CameraRevision || preview.Camera.World != source.Camera.ExternalWorld ||
                    preview.Camera.Projection != new PerspectiveProjection(source.Camera.ExternalSettings.FieldOfView,
                        source.Camera.ExternalSettings.Near, source.Camera.ExternalSettings.Far))
                    throw new InvalidDataException("Preview view does not match the render source.");
                _previewContext = preview.Context;
            }
            if (_resources == null)
            {
                _resources = roots.Create(request.Backends); _backends = request.Backends; _source = source;
                _device = _resources.Create(new RenderDeviceRequest(new(request.Stamp.Identity.SessionId, source.World.Id,
                    source.Content.ProjectId, source.Content.BuildId, source.Content.Generation), request.Stamp.SurfaceGeneration));
                using var content = _resources.Content.Acquire(source.Content);
                _binding = _resources.Create(new RenderWorldBindingRequest(_device, source.World, content));
                await _binding.InitializeAsync(token).ConfigureAwait(false);
                _view = _resources.Create(new RenderSnapshotViewRequest(_binding, source.SceneId, source.SourceRevision));
            }
            if (!ReferenceEquals(_backends, request.Backends) || _source!.World != source.World || _source.SceneId != source.SceneId ||
                _source.SourceRevision != source.SourceRevision || _device!.Generation != request.Stamp.SurfaceGeneration)
                throw new InvalidDataException("Detach the prior required view before changing its context/World/source identity.");
            if (_binding!.Current!.Manifest.Digest != source.Content.Digest)
            {
                if (preview != null) throw new InvalidDataException("Preview content cannot change within one child.");
                using var content = _resources.Content.Acquire(source.Content);
                var result = await _binding.ReloadAsync(content, token).ConfigureAwait(false);
                if (!result.Committed || result.CleanupError != null) throw new RenderResourceException(RenderResourceError.CleanupFailed, "Required content reload did not release its previous owners.", result.CleanupError);
            }
            var configuration = new RenderViewConfiguration(source.Camera, request.Width, request.Height, source.CameraRevision, request.Stamp.SurfaceGeneration);
            var current = _view!.Current;
            if (current == null || current.Tick != tick || current.Configuration != configuration || current.BindingRevision != _binding.Revision)
                await _view.CaptureAsync(tick, configuration, token).ConfigureAwait(false);
            var recording = await _view.RecordCurrentAsync(token).ConfigureAwait(false) ?? throw new InvalidOperationException("Required mesh publication unavailable.");
            try
            {
                return preview == null ? GpuFrameData.FromSnapshot(request.Stamp, recording) :
                    GpuFrameData.FromPreviewSnapshot(request.Stamp, recording, preview, overlay);
            }
            catch { await recording.DisposeAsync().ConfigureAwait(false); throw; }
        }
        finally { _gate.Release(); }
    }
    internal async Task DetachAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_resources != null) await _resources.DisposeAsync().ConfigureAwait(false);
            _resources = null; _device = null; _binding = null; _view = null; _source = null; _backends = null;
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0) _end.Cancel();
        await DetachAsync().ConfigureAwait(false); _end.Dispose();
    }
}
