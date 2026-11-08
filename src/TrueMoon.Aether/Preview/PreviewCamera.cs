using System.Numerics;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Preview;

/// <summary>Immutable transient camera, independent of authoring, World, history and demo checkpoints.</summary>
public sealed record PreviewCamera
{
    /// <summary>Creates a complete valid camera. Extent-dependent representability is checked by <see cref="CreateProjection"/>.</summary>
    /// <param name="world">Proper rigid world pose; no Euler round trip.</param><param name="projection">Perspective settings.</param>
    /// <param name="pivot">Finite world orbit pivot.</param><param name="distance">Finite positive navigation distance.</param>
    /// <param name="revision">Positive exact revision within the owning view context.</param>
    public PreviewCamera(Matrix4x4 world, PerspectiveProjection projection, Vector3 pivot, float distance, ulong revision)
    {
        PerspectiveProjection.ValidatePose(world); projection.Validate();
        if (!ContentValidation.Finite(pivot)) throw new ArgumentOutOfRangeException(nameof(pivot));
        if (!float.IsFinite(distance) || distance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
        if (revision == 0) throw new ArgumentOutOfRangeException(nameof(revision));
        World = world; Projection = projection; Pivot = pivot; Distance = distance; Revision = revision;
    }

    /// <summary>Gets the original preview view at revision one.</summary>
    public static PreviewCamera Default { get; } = new(Matrix4x4.CreateTranslation(0, 1, 6), PerspectiveProjection.Default, new(0, 1, 0), 6, 1);
    /// <summary>Gets the frozen world pose.</summary>
    public Matrix4x4 World { get; }
    /// <summary>Gets perspective settings.</summary>
    public PerspectiveProjection Projection { get; }
    /// <summary>Gets the navigation pivot; it need not be a scene entity.</summary>
    public Vector3 Pivot { get; }
    /// <summary>Gets positive navigation distance, independent of clipping distances.</summary>
    public float Distance { get; }
    /// <summary>Gets the exact view-local revision. Context epochs are owned by the caller.</summary>
    public ulong Revision { get; }

    /// <summary>Returns this value for an exact no-op, otherwise a new revision; overflow fails without wrapping.</summary>
    /// <param name="world">Candidate pose.</param><param name="projection">Candidate perspective.</param>
    /// <param name="pivot">Candidate navigation pivot.</param><param name="distance">Candidate navigation distance.</param>
    /// <returns>A whole validated camera. Invalid candidates never mutate this instance.</returns>
    public PreviewCamera WithView(Matrix4x4 world, PerspectiveProjection projection, Vector3 pivot, float distance)
        => World == world && Projection == projection && Pivot == pivot && Distance == distance ? this :
            new(world, projection, pivot, distance, checked(Revision + 1));

    /// <summary>Freezes projection, inverse and physical extent for a render/pick capture.</summary>
    /// <param name="width">Physical width.</param><param name="height">Physical height.</param><returns>Detached immutable projection.</returns>
    public CameraProjection CreateProjection(int width, int height) => new(Projection, World, width, height);
}
