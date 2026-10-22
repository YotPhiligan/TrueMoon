using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Interactions;

/// <summary>An opaque world-space cube; ineligible bodies still occlude interaction rays.</summary>
/// <param name="EntityId">Stable scene identity.</param><param name="Primitive">Complete affine geometry.</param>
/// <param name="Interactive">Whether a closest hit is eligible for interaction.</param>
public readonly record struct InteractionBody(string EntityId, RenderPrimitive Primitive, bool Interactive);

/// <summary>The nearest opaque surface, including an ineligible blocker.</summary>
/// <param name="EntityId">Hit scene identity.</param><param name="Distance">World-space distance, zero when inside/on the body.</param>
/// <param name="Interactive">Whether the hit can receive gameplay interaction.</param>
public readonly record struct InteractionHit(string EntityId, float Distance, bool Interactive);

/// <summary>Bounded deterministic ray query against actual affine cubes, preserving nonuniform scale and shear.</summary>
public static class PrimitiveRaycast
{
    /// <summary>Largest accepted query reach in world units.</summary>
    public const float MaximumDistance = 1000;
    /// <summary>Finds the nearest surface on the camera's forward ray, including the exact reach boundary.</summary>
    /// <param name="bodies">At most 64 distinct opaque bodies. Groups without geometry are omitted.</param>
    /// <param name="camera">Finite right-handed camera, forward -Z.</param>
    /// <param name="maximumDistance">Finite reach in (0,1000].</param>
    /// <returns>Nearest hit or null. Equal distances prefer an ineligible blocker, then ordinal entity ID.</returns>
    public static InteractionHit? Cast(IEnumerable<InteractionBody> bodies, CameraTransform camera, float maximumDistance)
    {
        ArgumentNullException.ThrowIfNull(bodies); camera.Validate();
        if (!float.IsFinite(maximumDistance) || maximumDistance <= 0 || maximumDistance > MaximumDistance)
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        var values = bodies.Take(GameObjectStates.MaximumCount + 1).ToArray();
        if (values.Length > GameObjectStates.MaximumCount) throw new InvalidDataException("Too many interaction bodies.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var origin = new Vector3(camera.X, camera.Y, camera.Z);
        var forward = Vector3.Normalize(Vector3.Transform(-Vector3.UnitZ, Quaternion.CreateFromYawPitchRoll(camera.Yaw, camera.Pitch, camera.Roll)));
        InteractionHit? nearest = null; double nearestDistance = maximumDistance;
        foreach (var body in values)
        {
            GameObjectStates.ValidateId(body.EntityId);
            if (!ids.Add(body.EntityId)) throw new InvalidDataException("Duplicate interaction body ID.");
            body.Primitive.Validate();
            if (!Matrix4x4.Invert(body.Primitive.World, out var inverse)) throw new InvalidDataException("Noninvertible interaction body.");
            var localOrigin = Vector3.Transform(origin, inverse);
            // Do not normalize: parameter t must remain world-space distance after the inverse transform.
            var localDirection = Vector3.TransformNormal(forward, inverse);
            if (!Finite(localOrigin) || !Finite(localDirection)) throw new InvalidDataException("Unrepresentable interaction ray.");
            double enter = 0, exit = maximumDistance;
            if (!Slab(localOrigin.X, localDirection.X, ref enter, ref exit) ||
                !Slab(localOrigin.Y, localDirection.Y, ref enter, ref exit) || !Slab(localOrigin.Z, localDirection.Z, ref enter, ref exit)) continue;
            if (enter < nearestDistance || enter == nearestDistance && (nearest == null ||
                nearest.Value.Interactive && !body.Interactive || nearest.Value.Interactive == body.Interactive &&
                StringComparer.Ordinal.Compare(body.EntityId, nearest.Value.EntityId) < 0))
            { nearestDistance = enter; nearest = new(body.EntityId, (float)enter, body.Interactive); }
        }
        return nearest;
    }
    private static bool Slab(float origin, float direction, ref double enter, ref double exit)
    {
        if (direction == 0) return origin is >= -.5f and <= .5f;
        var first = (-.5 - origin) / direction; var second = (.5 - origin) / direction;
        if (first > second) (first, second) = (second, first);
        enter = Math.Max(enter, first); exit = Math.Min(exit, second); return enter <= exit;
    }
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
