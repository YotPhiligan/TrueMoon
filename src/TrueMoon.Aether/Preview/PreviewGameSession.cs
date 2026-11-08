using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;

/// <summary>Built-in static scene consumer for an isolated Edit preview child; never loads game code.</summary>
public sealed class PreviewGameSession : IRenderGameSession
{
    private PrimitiveScene? _scene;
    private bool _disposed;
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
    public PreviewViewState? View => _scene?.PreviewView;
    /// <summary>Applies a whole newer view at the runtime's serialized command boundary.</summary>
    /// <param name="view">Same context, monotonically newer camera, or an exact no-op.</param>
    public void SetView(PreviewViewState view)
    {
        ObjectDisposedException.ThrowIf(_disposed, this); ArgumentNullException.ThrowIfNull(view);
        view.Camera.CreateProjection(16, 16);
        var old = View ?? throw new InvalidOperationException("Explicit preview view was not negotiated at LoadScene.");
        if (view.Context != old.Context || view.Camera.Revision < old.Camera.Revision ||
            view.Camera.Revision == old.Camera.Revision && view != old) throw new InvalidDataException("Stale/foreign preview view.");
        _scene = _scene! with { PreviewView = view };
    }
    private static void ValidateView(SceneSnapshot scene, PreviewViewState view)
    {
        view.Camera.CreateProjection(16, 16);
        if (!Guid.TryParse(scene.Id, out var id) || id != view.Context.SceneId || scene.Revision != view.Context.SceneRevision)
            throw new InvalidDataException("Preview view does not describe the loaded scene.");
    }
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
    public void Dispose() { _disposed = true; _scene = null; }
}
