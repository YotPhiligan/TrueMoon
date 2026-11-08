using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;

/// <summary>Bounded immutable cube geometry from the exact projected render payload; no World/UI/resource ownership.</summary>
public sealed class PreviewPickSnapshot
{
    /// <summary>Exact geometry profile. Unknown visible occluders disable the entire query.</summary>
    public const string CubeGeometryProfile = "editor.opaque-cubes/1";
    private readonly ImmutableArray<Matrix4x4> _inverses;
    private readonly PreviewPickUnavailableReason _unavailable;

    private PreviewPickSnapshot(SceneSnapshot source, string geometryProfile)
    {
        if (!Guid.TryParse(source.Id, out var scene) || scene == Guid.Empty)
            throw new ArgumentException("Picking requires a persistent authoring scene ID.", nameof(source));
        Scene = new(scene); Revision = source.Revision;
        GeometryProfile = geometryProfile;
        var cubes = ImmutableArray.CreateBuilder<PreviewPickPrimitive>();
        var inverses = ImmutableArray.CreateBuilder<Matrix4x4>();
        var ids = new HashSet<EntityId>(); var order = 0;
        foreach (var entity in source.CopyEntities())
        {
            if (!Guid.TryParse(entity.Id, out var value) || value == Guid.Empty || !ids.Add(new(value)))
                throw new ArgumentException("Picking requires distinct persistent authoring entity IDs.", nameof(source));
            if (entity.Primitive is { } geometry)
            {
                var world = geometry.ToPrimitive().World;
                cubes.Add(new(new(value), order, world));
                if (!ContentValidation.TryInvertFinite(world, out var inverse))
                    _unavailable = PreviewPickUnavailableReason.InvalidMath;
                inverses.Add(inverse);
            }
            order++;
        }
        Primitives = cubes.ToImmutable(); _inverses = inverses.ToImmutable();
        if (!string.Equals(geometryProfile, CubeGeometryProfile, StringComparison.Ordinal))
            _unavailable = PreviewPickUnavailableReason.UnsupportedGeometry;
    }

    /// <summary>Captures the same deep-owned projected SceneSnapshot supplied to rendering; does not reproject authoring.</summary>
    /// <param name="source">Complete projected scene, up to64 entities. All IDs must be nonempty GUIDs.</param>
    /// <param name="geometryProfile">Explicit exact profile of all visible geometry, not only candidate cubes.</param>
    /// <returns>Frozen IDs/order/matrices/inverses. Unknown profiles or unrepresentable inverses produce only Unavailable.</returns>
    public static PreviewPickSnapshot Capture(SceneSnapshot source, string geometryProfile)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(geometryProfile);
        return new(source, geometryProfile);
    }

    /// <summary>Gets the persistent scene ID. Open-document epochs are added by presentation admission later.</summary>
    public SceneId Scene { get; }
    /// <summary>Gets the exact projected content revision.</summary>
    public long Revision { get; }
    /// <summary>Gets the explicit geometry profile, including unsupported values.</summary>
    public string GeometryProfile { get; }
    /// <summary>Gets detached primitives in draw order. Non-rendering groups are omitted; Interaction does not filter cubes.</summary>
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

    /// <summary>Queries a bounded world ray; closest surface wins, exact ties preserve projected draw order.</summary>
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
        return new(this, entity == null ? PreviewPickKind.Miss : PreviewPickKind.Hit, entity, nearest, PreviewPickUnavailableReason.None);
    }

    private PreviewPickResult Unavailable(PreviewPickUnavailableReason reason) => new(this, PreviewPickKind.Unavailable, null, null, reason);
}
