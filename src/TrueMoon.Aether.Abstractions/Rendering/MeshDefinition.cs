using System.Collections.Immutable;
using System.Numerics;

namespace TrueMoon.Aether.Rendering;


/// <summary>Supported exact primitive topology.</summary>
public enum MeshTopology
{
    /// <summary>Independent three-index triangles.</summary>
    TriangleList = 1
}

/// <summary>Supported exact packed vertex layout.</summary>
public enum MeshVertexLayout
{
    /// <summary>Three little-endian Single coordinates, stride twelve.</summary>
    PositionFloat3 = 1,
    /// <summary>Packed positions plus a separate packed float2 UV0 stream.</summary>
    PositionFloat3Uv0Float2 = 2
}

/// <summary>Supported exact index representation.</summary>
public enum MeshIndexFormat
{
    /// <summary>Little-endian unsigned 32-bit indices.</summary>
    UInt32 = 1
}

/// <summary>One stable material slot and its nonempty contiguous index range.</summary>
/// <param name="SlotId">Persistent semantic slot.</param><param name="FirstIndex">First index, divisible by three.</param>
/// <param name="IndexCount">Nonzero index count, divisible by three.</param>
public readonly record struct MeshSlot(Guid SlotId, uint FirstIndex, uint IndexCount);


/// <summary>Validated detached triangle-list geometry; input collections are copied.</summary>
public sealed class MeshDefinition : RenderAssetDefinition
{
    /// <summary>Creates exact v1 geometry without IO, GPU resources or registration.</summary>
    /// <param name="vertices">Finite positions within the profile coordinate range.</param><param name="indices">Indices into vertices.</param>
    /// <param name="slots">Distinct ordered ranges covering every index exactly once.</param><param name="bounds">Inclusive local bounds covering all positions.</param>
    /// <param name="schemaVersion">Exact version one.</param><param name="topology">Triangle list only.</param>
    /// <param name="vertexLayout">Packed position-f32x3 only.</param><param name="indexFormat">UInt32 only.</param>
    public MeshDefinition(IReadOnlyList<Vector3> vertices, IReadOnlyList<uint> indices, IReadOnlyList<MeshSlot> slots, RenderBounds bounds,
        int schemaVersion = 1, MeshTopology topology = MeshTopology.TriangleList, MeshVertexLayout vertexLayout = MeshVertexLayout.PositionFloat3,
        MeshIndexFormat indexFormat = MeshIndexFormat.UInt32)
        : this(vertices, null, indices, slots, bounds, schemaVersion, topology, vertexLayout, indexFormat) { }

    /// <summary>Creates a triangle mesh with one UV0 coordinate per position.</summary>
    /// <param name="vertices">Finite positions within the profile coordinate range.</param>
    /// <param name="uv0">Normalized sampler coordinates. Finite values outside the unit square use edge clamping.</param>
    /// <param name="indices">Indices into both vertex streams.</param>
    /// <param name="slots">Distinct ordered ranges covering all indices.</param>
    /// <param name="bounds">Inclusive local bounds covering all positions.</param>
    public MeshDefinition(IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector2> uv0, IReadOnlyList<uint> indices,
        IReadOnlyList<MeshSlot> slots, RenderBounds bounds)
        : this(vertices, uv0 ?? throw new ArgumentNullException(nameof(uv0)), indices, slots, bounds, 1, MeshTopology.TriangleList,
            MeshVertexLayout.PositionFloat3Uv0Float2, MeshIndexFormat.UInt32) { }

    private MeshDefinition(IReadOnlyList<Vector3> vertices, IReadOnlyList<Vector2>? uv0, IReadOnlyList<uint> indices,
        IReadOnlyList<MeshSlot> slots, RenderBounds bounds, int schemaVersion, MeshTopology topology, MeshVertexLayout vertexLayout,
        MeshIndexFormat indexFormat) : base(RenderContentProfile.MeshKindId, schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(vertices); ArgumentNullException.ThrowIfNull(indices); ArgumentNullException.ThrowIfNull(slots);
        ContentValidation.Require(topology == MeshTopology.TriangleList && indexFormat == MeshIndexFormat.UInt32 &&
            vertexLayout == (uv0 == null ? MeshVertexLayout.PositionFloat3 : MeshVertexLayout.PositionFloat3Uv0Float2),
            RenderContentError.UnsupportedLayout, "Unsupported triangle-list vertex streams or index format.");
        ContentValidation.Require(vertices.Count <= RenderContentProfile.MaximumVertices && indices.Count <= RenderContentProfile.MaximumIndices &&
            slots.Count <= RenderContentProfile.MaximumSlots, RenderContentError.LimitExceeded, "Mesh collection admission exceeded.");
        ContentValidation.Require(vertices.Count > 0 && indices.Count > 0 && indices.Count % 3 == 0 && slots.Count > 0,
            RenderContentError.InvalidGeometry, "Mesh geometry and slots must be nonempty complete triangles.");
        bounds.Validate();
        Vertices = ContentValidation.Copy(vertices, RenderContentProfile.MaximumVertices, nameof(vertices));
        if (uv0 != null)
        {
            ContentValidation.Require(uv0.Count == Vertices.Length, RenderContentError.InvalidGeometry, "UV0 must cover every position.");
            Uv0 = ContentValidation.Copy(uv0, RenderContentProfile.MaximumVertices, nameof(uv0));
            foreach (var uv in Uv0)
                ContentValidation.Require(float.IsFinite(uv.X) && float.IsFinite(uv.Y), RenderContentError.InvalidGeometry, "Invalid UV0 coordinate.");
        }
        Indices = ContentValidation.Copy(indices, RenderContentProfile.MaximumIndices, nameof(indices));
        Slots = ContentValidation.Copy(slots, RenderContentProfile.MaximumSlots, nameof(slots));
        Bounds = bounds; Topology = topology; VertexLayout = vertexLayout; IndexFormat = indexFormat;
        foreach (var vertex in Vertices)
        {
            ContentValidation.Require(ContentValidation.Finite(vertex) && MathF.Abs(vertex.X) <= RenderContentProfile.MaximumCoordinate &&
                MathF.Abs(vertex.Y) <= RenderContentProfile.MaximumCoordinate && MathF.Abs(vertex.Z) <= RenderContentProfile.MaximumCoordinate,
                RenderContentError.InvalidGeometry, "Invalid mesh coordinate.");
            ContentValidation.Require(vertex.X >= bounds.Minimum.X && vertex.X <= bounds.Maximum.X && vertex.Y >= bounds.Minimum.Y && vertex.Y <= bounds.Maximum.Y &&
                vertex.Z >= bounds.Minimum.Z && vertex.Z <= bounds.Maximum.Z, RenderContentError.InvalidBounds, "Bounds do not cover every vertex.");
        }
        foreach (var index in Indices) ContentValidation.Require(index < Vertices.Length, RenderContentError.InvalidGeometry, "Index is outside the vertex buffer.");
        var ids = new HashSet<Guid>(); ulong cursor = 0;
        foreach (var slot in Slots)
        {
            ContentValidation.Require(slot.SlotId != Guid.Empty && ids.Add(slot.SlotId) && slot.FirstIndex == cursor && slot.IndexCount > 0 &&
                slot.FirstIndex % 3 == 0 && slot.IndexCount % 3 == 0, RenderContentError.InvalidGeometry, "Invalid, duplicate or noncontiguous mesh slot.");
            cursor += slot.IndexCount;
            ContentValidation.Require(cursor <= (ulong)Indices.Length, RenderContentError.InvalidGeometry, "Mesh range exceeds the index buffer.");
        }
        ContentValidation.Require(cursor == (ulong)Indices.Length, RenderContentError.InvalidGeometry, "Mesh ranges must cover every index.");
        Complete();
    }
    /// <summary>Gets copied immutable positions.</summary>
    public ImmutableArray<Vector3> Vertices { get; }
    /// <summary>Copied UV0 stream, empty for a position-only mesh.</summary>
    public ImmutableArray<Vector2> Uv0 { get; } = [];
    /// <summary>Gets copied immutable indices.</summary>
    public ImmutableArray<uint> Indices { get; }
    /// <summary>Gets stable ranges in increasing first-index order.</summary>
    public ImmutableArray<MeshSlot> Slots { get; }
    /// <summary>Gets validated local bounds.</summary>
    public RenderBounds Bounds { get; }
    /// <summary>Gets the exact supported topology.</summary>
    public MeshTopology Topology { get; }
    /// <summary>Gets the exact packed vertex layout.</summary>
    public MeshVertexLayout VertexLayout { get; }
    /// <summary>Gets the exact index representation.</summary>
    public MeshIndexFormat IndexFormat { get; }
}
