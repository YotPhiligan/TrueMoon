using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime.Sessions;

public sealed partial class SessionSupervisor
{
    private int _renderViewVersion;
    private RenderSnapshotStamp? _confirmedRenderView;
    /// <summary>Required mesh-view capability of the current Hello; zero for legacy/headless/primitive games.</summary>
    public int RequiredRenderViewVersion => Volatile.Read(ref _renderViewVersion);
    /// <summary>Custom-host acknowledgement after actual mesh-frame presentation. Standard hosts use FrameSession.PresentedAsync.</summary>
    /// <returns>False for a frame superseded by the current source/publication; no Playing fence is opened by that frame.</returns>
    public Task<bool> AcknowledgeRenderViewAsync(RenderSnapshotStamp stamp, CancellationToken token = default)
        => ConfirmRenderViewAsync(stamp, token);
    private bool IsRequiredViewPause(ControlReply reply) => RequiredRenderViewVersion == 1 && reply.RenderViewVersion == 1 &&
        Status.State == SessionState.Playing && reply.Phase == "Paused" && !reply.RenderViewReady;
    internal async Task<bool> ConfirmRenderViewAsync(RenderSnapshotStamp stamp, CancellationToken token, ReplacementAdmission.Lease? owner = null,
        SessionIdentity? expectedIdentity = null)
    {
        ObjectDisposedException.ThrowIf(_disposed || Volatile.Read(ref _closing) != 0, this);
        var status = Status;
        if (expectedIdentity != null && (status.Identity != expectedIdentity ||
            status.State is not (SessionState.Paused or SessionState.Playing))) return false;
        if (status.State is not (SessionState.Paused or SessionState.Playing)) throw new InvalidOperationException("Runtime is not ready for presentation.");
        stamp.Validate();
        if (RequiredRenderViewVersion != 1) throw new InvalidDataException("Runtime does not support this mesh presentation profile.");
        var old = Volatile.Read(ref _confirmedRenderView);
        if (old != null && SameRequiredView(old, stamp)) return true;
        var reply = await CommandAsync(CommandKind.PresentRenderView, request => request.RenderViewStamp = stamp.Encode(), token,
            owner: owner, expectedIdentity: expectedIdentity).ConfigureAwait(false);
        if (reply.RenderViewVersion != 1) throw new InvalidDataException("Runtime mesh presentation capability changed.");
        if (!reply.RenderViewPresentationAccepted) return false;
        if (!reply.RenderViewReady) throw new InvalidDataException("Accepted mesh presentation did not open readiness.");
        Volatile.Write(ref _confirmedRenderView, stamp); return true;
    }
    private static bool SameRequiredView(RenderSnapshotStamp a, RenderSnapshotStamp b) => a.ViewId == b.ViewId && a.WorldId == b.WorldId &&
        a.SceneId == b.SceneId && a.SourceRevision == b.SourceRevision && a.BindingRevision == b.BindingRevision && a.CameraRevision == b.CameraRevision &&
        a.SurfaceGeneration == b.SurfaceGeneration && a.ManifestHash == b.ManifestHash;
}
