using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Rendering.Snapshots;

/// <summary>Detached value draw. The handle grants no ownership or use after its recording/submission owner is released.</summary>
/// <param name="Entity">Transient source identity.</param><param name="World">Complete affine matrix.</param><param name="Bounds">Conservative world AABB.</param>
/// <param name="Mesh">Exact mesh revision.</param><param name="Bindings">Exact binding revision.</param><param name="Material">Exact material revision.</param>
/// <param name="Handle">Prepared device-local mesh handle.</param><param name="Slot">Ordered mesh index range.</param><param name="ColorLinear">Decoded base times effective instance tint.</param>
public readonly record struct RenderDrawRange(EntityHandle Entity, Matrix4x4 World, RenderBounds Bounds, RenderAssetKey Mesh,
    RenderAssetKey Bindings, RenderAssetKey Material, RenderResourceHandle Handle, MeshSlot Slot, Vector3 ColorLinear);

/// <summary>Detached immutable inspection data, independent of World and native lifetime. Rendering requires a live recording/use owner.</summary>
public sealed class RenderSnapshotData
{
    internal RenderSnapshotData(Guid viewId, RenderResourceIdentity identity, SceneId scene, long sourceRevision, ulong tick,
        ulong sequence, ulong bindingRevision, ulong deviceGeneration, RenderContentHash manifest, RenderViewConfiguration configuration,
        Matrix4x4 cameraWorld, Matrix4x4 viewProjection, Vector3 clear, ImmutableArray<RenderDrawRange> draws, long payloadBytes)
    { ViewId = viewId; Identity = identity; SceneId = scene; SourceRevision = sourceRevision; Tick = tick; Sequence = sequence;
        BindingRevision = bindingRevision; DeviceGeneration = deviceGeneration; ManifestDigest = manifest; Configuration = configuration;
        CameraWorld = cameraWorld; ViewProjection = viewProjection; ClearColorSrgb = clear; Draws = draws; PayloadBytes = payloadBytes; }
    /// <summary>Unique process-local view identity.</summary>
    public Guid ViewId { get; }
    /// <summary>Exact session/World/project/build/catalog identity.</summary>
    public RenderResourceIdentity Identity { get; }
    /// <summary>Captured persistent source scene.</summary>
    public SceneId SceneId { get; }
    /// <summary>Initial source authoring revision.</summary>
    public long SourceRevision { get; }
    /// <summary>Complete WorldTransform tick shared by every required transform.</summary>
    public ulong Tick { get; }
    /// <summary>Strictly increasing successful publication sequence; no wrap.</summary>
    public ulong Sequence { get; }
    /// <summary>Exact atomic resource binding revision.</summary>
    public ulong BindingRevision { get; }
    /// <summary>Exact device generation.</summary>
    public ulong DeviceGeneration { get; }
    /// <summary>Exact frozen content fingerprint.</summary>
    public RenderContentHash ManifestDigest { get; }
    /// <summary>Frozen camera selection/external values, extent and generations.</summary>
    public RenderViewConfiguration Configuration { get; }
    /// <summary>Validated proper rigid camera pose.</summary>
    public Matrix4x4 CameraWorld { get; }
    /// <summary>RH row-vector Vulkan depth[0,1] view/projection, Y flipped once.</summary>
    public Matrix4x4 ViewProjection { get; }
    /// <summary>Encoded-sRGB background; do not encode again.</summary>
    public Vector3 ClearColorSrgb { get; }
    /// <summary>Copied draw values ordered by entity index then first index.</summary>
    public ImmutableArray<RenderDrawRange> Draws { get; }
    /// <summary>Accounted header plus draw array element bytes. Caller-retained inspection copies do not grant live leases.</summary>
    public long PayloadBytes { get; }
}

/// <summary>View-owned counters, including failed retirement owners.</summary>
/// <param name="Snapshots">Distinct retained envelopes.</param><param name="PayloadBytes">Accounted live buffer bytes.</param>
/// <param name="Submissions">Accepted uncollected uses.</param><param name="HasPending">Latest mailbox entry exists.</param>
/// <param name="HasRecording">A recording is outstanding.</param><param name="Closed">Admission is stopped or fenced.</param>
public sealed record RenderSnapshotCounts(int Snapshots, long PayloadBytes, int Submissions, bool HasPending, bool HasRecording, bool Closed);
