using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;


internal readonly struct PreviewPickMesh
{
    internal PreviewPickMesh(EntityId entity, Matrix4x4 world, MeshDefinition geometry)
    {
        Entity = entity;
        Geometry = geometry;
        Inverse = ContentValidation.Affine(world) && ContentValidation.TryInvertFinite(world, out var inverse) ? inverse : null;
    }

    internal EntityId Entity { get; }
    internal MeshDefinition Geometry { get; }
    internal Matrix4x4? Inverse { get; }
}

/// <summary>Frozen cube instances and exact mesh geometry from the projected render payload; no World, UI or GPU ownership.</summary>
public sealed class PreviewPickSnapshot
{
    /// <summary>Exact profile for opaque legacy cubes.</summary>
    public const string CubeGeometryProfile = "editor.opaque-cubes/1";
    /// <summary>Exact opaque mesh and cube profile; supported only with mesh rows from the frozen render payload.</summary>
    public const string MeshAndCubeGeometryProfile = "editor.meshes-and-cubes/1";
    private readonly ImmutableArray<Matrix4x4> _inverses;
    private readonly ImmutableArray<PreviewPickMesh> _meshes;
    private readonly PreviewPickUnavailableReason _unavailable;

    private PreviewPickSnapshot(SceneId scene, long revision, string geometryProfile, ImmutableArray<PreviewPickPrimitive> primitives,
        ImmutableArray<PreviewPickMesh> meshes)
    {
        Scene = scene; Revision = revision; GeometryProfile = geometryProfile; Primitives = primitives; _meshes = meshes;
        if (meshes.IsEmpty && !string.Equals(geometryProfile, CubeGeometryProfile, StringComparison.Ordinal))
        {
            _unavailable = PreviewPickUnavailableReason.UnsupportedGeometry;
            _inverses = [];
            return;
        }
        var inverses = ImmutableArray.CreateBuilder<Matrix4x4>(primitives.Length);
        foreach (var primitive in primitives)
        {
            Matrix4x4 inverse = default;
            if (!ContentValidation.Affine(primitive.World) || !ContentValidation.TryInvertFinite(primitive.World, out inverse))
                _unavailable = PreviewPickUnavailableReason.InvalidMath;
            inverses.Add(inverse);
        }
        _inverses = inverses.MoveToImmutable();
        foreach (ref readonly var mesh in meshes.AsSpan())
            if (!mesh.Inverse.HasValue) _unavailable = PreviewPickUnavailableReason.InvalidMath;
    }

    internal static PreviewPickSnapshot CaptureRender(SceneId scene, long revision, ImmutableArray<PreviewPickPrimitive> primitives,
        ImmutableArray<PreviewPickMesh> meshes)
        => new(scene, revision, meshes.IsEmpty ? CubeGeometryProfile : MeshAndCubeGeometryProfile, primitives, meshes);

    /// <summary>Captures projected legacy cubes without reprojecting authoring. This path has no mesh geometry.</summary>
    /// <param name="source">Complete projected scene, up to 64 entities. All IDs must be nonempty GUIDs.</param>
    /// <param name="geometryProfile">Exact profile of all visible geometry, not only candidate cubes.</param>
    /// <returns>Frozen cube rows and inverses. Unsupported profiles (including mixed without mesh rows) or invalid inverses yield Unavailable.</returns>
    public static PreviewPickSnapshot Capture(SceneSnapshot source, string geometryProfile)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(geometryProfile);
        if (!Guid.TryParse(source.Id, out var scene) || scene == Guid.Empty)
            throw new ArgumentException("Picking requires a persistent authoring scene ID.", nameof(source));
        var cubes = ImmutableArray.CreateBuilder<PreviewPickPrimitive>();
        var ids = new HashSet<EntityId>(); var order = 0;
        foreach (var entity in source.CopyEntities())
        {
            if (!Guid.TryParse(entity.Id, out var value) || value == Guid.Empty || !ids.Add(new(value)))
                throw new ArgumentException("Picking requires distinct persistent authoring entity IDs.", nameof(source));
            if (entity.Primitive is { } geometry)
                cubes.Add(new(new(value), order, geometry.ToPrimitive().World));
            order++;
        }
        return new(new(scene), source.Revision, geometryProfile, cubes.ToImmutable(), []);
    }

    /// <summary>Gets the persistent scene ID. Open-document epochs are added by presentation admission later.</summary>
    public SceneId Scene { get; }
    /// <summary>Gets the exact projected content revision.</summary>
    public long Revision { get; }
    /// <summary>Gets the explicit geometry profile, including unsupported values.</summary>
    public string GeometryProfile { get; }
    /// <summary>Gets only legacy cube rows in draw order. Non-rendering groups and meshes are omitted; Interaction does not filter cubes.</summary>
    public ImmutableArray<PreviewPickPrimitive> Primitives { get; }

    /// <summary>Queries a pixel-center ray in frozen logical ImageBounds. Presentation/context admission belongs to the caller.</summary>
    /// <param name="projection">Matrices/extent from the same render capture.</param><param name="image">Actually drawn logical image rectangle.</param>
    /// <param name="x">Logical pointer X.</param><param name="y">Logical pointer Y.</param><returns>Hit/Miss/Unavailable retaining this source.</returns>
    public PreviewPickResult Pick(CameraProjection projection, ViewportImageBounds image, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (!image.TryMapPixel(x, y, projection.Width, projection.Height, out var px, out var py))
            return Unavailable(PreviewPickUnavailableReason.OutsideImage);
        return projection.TryCreateRay(px, py, out var ray) ? Pick(ray) : Unavailable(PreviewPickUnavailableReason.InvalidMath);
    }

    /// <summary>Queries a bounded world ray; closest surface wins, exact ties preserve draw order (cubes before meshes).</summary>
    /// <param name="ray">Normalized world-distance segment.</param><returns>Geometry outcome only, without mutating selection.</returns>
    public PreviewPickResult Pick(Ray3 ray)
    {
        if (_unavailable != PreviewPickUnavailableReason.None) return Unavailable(_unavailable);
        // Validate even empty scenes; invalid rays must not become background misses.
        if (ray.MaximumDistance <= 0) return Unavailable(PreviewPickUnavailableReason.InvalidMath);
        EntityId? entity = null; double? nearest = null;
        for (var i = 0; i < Primitives.Length; i++)
        {
            if (!ray.TryIntersectInverse(_inverses[i], out var distance)) return Unavailable(PreviewPickUnavailableReason.InvalidMath);
            if (distance is { } hit && (nearest == null || hit < nearest.Value))
            { nearest = hit; entity = Primitives[i].Entity; }
        }
        foreach (ref readonly var mesh in _meshes.AsSpan())
        {
            if (!ray.TryIntersectMeshInverse(mesh.Inverse!.Value, mesh.Geometry, nearest ?? ray.MaximumDistance, out var distance))
                return Unavailable(PreviewPickUnavailableReason.InvalidMath);
            if (distance is { } hit && (nearest == null || hit < nearest.Value))
            { nearest = hit; entity = mesh.Entity; }
        }
        return new(this, entity == null ? PreviewPickKind.Miss : PreviewPickKind.Hit, entity, nearest, PreviewPickUnavailableReason.None);
    }

    private PreviewPickResult Unavailable(PreviewPickUnavailableReason reason) => new(this, PreviewPickKind.Unavailable, null, null, reason);
}
