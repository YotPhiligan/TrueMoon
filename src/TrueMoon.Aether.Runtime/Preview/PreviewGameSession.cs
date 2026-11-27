using System.Collections.Immutable;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Preview;

/// <summary>Built-in static scene consumer for an isolated Edit preview child; never loads game code.</summary>
public sealed class PreviewGameSession : IRenderGameSession
{
    private readonly IFactory<World>? _worlds;
    private RenderSceneSource? _render;
    private PreviewViewState? _renderView;
    private PrimitiveScene? _scene;
    private bool _disposed;

    /// <summary>Creates a primitive-only preview session.</summary>
    public PreviewGameSession() { }

    internal PreviewGameSession(IFactory<World> worlds) => _worlds = worlds;

    internal bool CanLoadRender => _worlds != null;
    internal bool IsRenderMode => _render != null;
    internal ImmutableArray<RenderPrimitive> PrimitiveOverlay { get; private set; }
    /// <summary>Gets the fixed preview camera. Navigation is a subsequent editor tool.</summary>
    public static CameraTransform Camera => new(0, 1, 6, 0, 0, 0);
    /// <summary>Gets zero: preview has no gameplay simulation.</summary>
    public ulong TickNumber => 0;
    /// <summary>Gets the loaded authoring revision.</summary>
    public long Revision { get; private set; }
    /// <summary>Gets a stable fallback pattern; real preview uses primitive GPU rendering.</summary>
    public uint PatternColor => 0xff2080c0;

    /// <summary>Validates and copies all projected primitives before replacing the current static scene.</summary>
    /// <param name="scene">Authoring projection delivered over the existing bounded control channel.</param>
    public void Load(SceneSnapshot scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(scene);
        if (IsRenderMode) throw new InvalidOperationException("A render preview belongs to this child until retirement.");
        var candidate = new PrimitiveScene(Camera, scene.CopyEntities().Where(e => e.Primitive != null).Select(e => e.Primitive!.ToPrimitive()));
        _scene = candidate; Revision = scene.Revision;
    }
    /// <summary>Installs the initial explicit view atomically with the projected scene before frame readiness.</summary>
    /// <param name="scene">Exact projected payload.</param><param name="view">Opening/content identity and external camera.</param>
    public void Load(SceneSnapshot scene, PreviewViewState view)
    {
        ArgumentNullException.ThrowIfNull(scene); ArgumentNullException.ThrowIfNull(view);
        ValidateView(scene, view);
        Load(scene); _scene = _scene! with { PreviewView = view };
    }
    /// <summary>Gets the current explicit view; legacy preview remains untagged.</summary>
    public PreviewViewState? View => _renderView ?? _scene?.PreviewView;
    /// <summary>Applies a whole newer view at the runtime's serialized command boundary.</summary>
    /// <param name="view">Same context, monotonically newer camera, or an exact no-op.</param>
    public void SetView(PreviewViewState view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(view);
        view.Camera.CreateProjection(16, 16);
        var old = View ?? throw new InvalidOperationException("Explicit preview view was not negotiated at LoadScene.");
        if (view.Context != old.Context || view.Camera.Revision < old.Camera.Revision ||
            view.Camera.Revision == old.Camera.Revision && view != old) throw new InvalidDataException("Stale/foreign preview view.");
        if (view == old) return;
        if (_render is { } render)
        {
            _render = render with { Camera = RenderCamera(view.Camera), CameraRevision = view.Camera.Revision };
            _renderView = view;
            return;
        }
        _scene = _scene! with { PreviewView = view };
    }
    private static void ValidateView(SceneSnapshot scene, PreviewViewState view)
    {
        view.Camera.CreateProjection(16, 16);
        if (!Guid.TryParse(scene.Id, out var id) || id != view.Context.SceneId || scene.Revision != view.Context.SceneRevision)
            throw new InvalidDataException("Preview view does not describe the loaded scene.");
    }

    internal void LoadRender(PreviewRenderPayload payload, PreviewViewState view, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(view);
        if (_worlds == null || _scene != null || IsRenderMode)
            throw new InvalidOperationException("Render preview requires a fresh World-capable session.");
        if (view.Context.SceneId != payload.SceneId.Value || view.Context.SceneRevision != payload.Revision)
            throw new InvalidDataException("Preview view does not describe the loaded scene.");
        view.Camera.CreateProjection(16, 16);
        token.ThrowIfCancellationRequested();
        var manifest = payload.CreateManifest();
        var overlayCount = 0;
        foreach (var row in payload.Rows) if (!row.IsMesh) overlayCount++;
        var overlay = ImmutableArray.CreateBuilder<RenderPrimitive>(overlayCount);
        var world = _worlds.Create();
        try
        {
            foreach (var row in payload.Rows)
            {
                token.ThrowIfCancellationRequested();
                if (!row.IsMesh)
                {
                    overlay.Add(new(row.World, row.Color));
                    continue;
                }
                var entity = world.CreateEntity();
                var mesh = manifest.Resolve(row.MeshId, RenderContentProfile.MeshKindId).Key;
                var bindings = manifest.Resolve(row.BindingId, RenderContentProfile.BindingSetKindId).Key;
                world.AddComponent(entity, new MeshRenderer(mesh, bindings, row.Enabled, row.Layers, row.TintEnabled, row.Color));
                world.AddComponent(entity, new WorldTransform(row.World, 0));
            }
            var source = new RenderSceneSource(world, manifest, payload.SceneId, payload.Revision, RenderCamera(view.Camera), view.Camera.Revision);
            token.ThrowIfCancellationRequested();
            PrimitiveOverlay = overlay.MoveToImmutable();
            Revision = payload.Revision;
            _renderView = view;
            _render = source;
        }
        catch
        {
            world.Dispose();
            throw;
        }
    }

    internal RenderSceneSource ReadRenderScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _render ?? throw new InvalidOperationException("Load a render preview first.");
    }

    private static RenderCameraSource RenderCamera(PreviewCamera camera)
        => RenderCameraSource.External(camera.World, new PerspectiveCamera(camera.Projection.FieldOfView,
            camera.Projection.Near, camera.Projection.Far, uint.MaxValue, new(.04f, .06f, .10f)));
    /// <summary>Does not advance gameplay or modify authoring data.</summary>
    public void Tick() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>Returns the immutable render scene at tick zero.</summary>
    /// <returns>Loaded static primitives and fixed camera.</returns>
    public PrimitiveScene CaptureRenderScene()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scene ?? throw new InvalidOperationException("Load a preview scene first.");
    }
    /// <summary>Releases this consumer's references; previously returned immutable render scenes stay valid.</summary>
    public void Dispose()
    {
        _render?.World.Dispose();
        _disposed = true;
        _render = null;
        _renderView = null;
        PrimitiveOverlay = default;
        _scene = null;
    }
}
