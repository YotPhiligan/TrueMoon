using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;

namespace TrueMoon.Aether.Runtime;

/// <summary>Required-view fence: CPU activation/upload/production cannot substitute for actual presentation.</summary>
public sealed class RenderViewReadiness
{
    private readonly Lock _gate = new();
    private RenderSnapshotStamp? _produced;
    private RenderContentHash _manifest;
    private bool _ready;
    /// <summary>Whether a frame of the current required view/binding/camera/surface has been presented.</summary>
    public bool IsReady { get { lock (_gate) return _ready; } }
    internal bool MatchesSource(RenderSceneSource source)
    {
        lock (_gate) return MatchesSourceCore(source);
    }
    private bool MatchesSourceCore(RenderSceneSource source) => _produced is { } stamp && stamp.WorldId == source.World.Id &&
        stamp.SceneId == source.SceneId.Value && stamp.SourceRevision == source.SourceRevision && stamp.CameraRevision == source.CameraRevision &&
        _manifest == source.Content.Digest;
    internal void Invalidate() { lock (_gate) _ready = false; }
    internal bool TryPresented(RenderSnapshotStamp stamp, RenderSceneSource source)
    {
        stamp.Validate();
        lock (_gate)
        {
            // A previously completed frame may reach the UI after a content/camera
            // change or after a newer publication. Reject it without faulting the
            // current view or opening its fence; the next current ticket can retry.
            if (_produced is not { } current || !SameView(current, stamp) || stamp.Sequence > current.Sequence || stamp.Tick > current.Tick)
                return false;
            _ready = MatchesSourceCore(source); return _ready;
        }
    }
    /// <summary>Invalidates the previous surface or retired view without claiming resumed readiness.</summary>
    public void Reset() { lock (_gate) { _produced = null; _manifest = default; _ready = false; } }
    /// <summary>Records only a confirmed GPU-completed frame; changing required provenance resets presentation.</summary>
    public void Produced(RenderSnapshotStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp); stamp.Validate();
        lock (_gate)
        {
            if (_produced is { } old)
            {
                if (old == stamp) return; // Paused views may re-present the same owned publication.
                if (old.ViewId == stamp.ViewId && stamp.Sequence <= old.Sequence) throw new InvalidDataException("Stale mesh publication.");
                if (!SameView(old, stamp)) _ready = false;
            }
            _produced = stamp; _manifest = RenderContentHash.Parse(stamp.ManifestHash);
        }
    }
    /// <summary>Admits an actual presentation from the produced range of the current required provenance.</summary>
    public void Presented(RenderSnapshotStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp); stamp.Validate();
        lock (_gate)
        {
            if (_produced is not { } current || !SameView(current, stamp) || stamp.Sequence > current.Sequence || stamp.Tick > current.Tick)
                throw new InvalidDataException("Stale/foreign/unproduced mesh presentation.");
            _ready = true;
        }
    }
    private static bool SameView(RenderSnapshotStamp a, RenderSnapshotStamp b) => a.ViewId == b.ViewId && a.WorldId == b.WorldId &&
        a.SceneId == b.SceneId && a.SourceRevision == b.SourceRevision && a.BindingRevision == b.BindingRevision &&
        a.CameraRevision == b.CameraRevision && a.SurfaceGeneration == b.SurfaceGeneration && a.ManifestHash == b.ManifestHash;
}
