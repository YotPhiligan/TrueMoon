using System.Numerics;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Kton.Contacts;

/// <summary>Gameplay pair transition, reduced after backend workers have joined.</summary>
public enum PhysicsContactKind
{
    /// <summary>New pair, including after a teleport.</summary>
    Begin,
    /// <summary>Existing awake pair. Sleeping pairs retain identity without per-tick Stay.</summary>
    Stay,
    /// <summary>Pair ended; reason is explicit.</summary>
    End
}

/// <summary>Why a contact identity ended.</summary>
public enum PhysicsContactEndReason
{
    /// <summary>Not an End event.</summary>
    None,
    /// <summary>Shapes no longer intersect.</summary>
    Separated,
    /// <summary>Body removed before this tick.</summary>
    BodyRemoved,
    /// <summary>Explicit pose discontinuity.</summary>
    Teleported
}

/// <summary>Owned value payload. Pair A/B and subshapes are canonical, normal points A to B. No borrowed manifolds.</summary>
/// <param name="Tick">Publishing successful tick.</param><param name="Kind">Transition.</param><param name="BodyA">Canonical first body.</param>
/// <param name="BodyB">Canonical second body.</param><param name="SubshapeA">Triangle index or -1.</param><param name="SubshapeB">Triangle index or -1.</param>
/// <param name="IsSensor">No solver response for this pair.</param><param name="HasContact">Whether Point/Normal/Depth are present; false for End.</param>
/// <param name="Point">Representative world contact.</param><param name="Normal">A-to-B unit normal when HasContact.</param>
/// <param name="Depth">Nonnegative physical penetration, excludes speculative-only contacts.</param><param name="EndReason">Reason for End.</param>
public readonly record struct PhysicsContactEvent(ulong Tick, PhysicsContactKind Kind, PhysicsBodyId BodyA, PhysicsBodyId BodyB,
    int SubshapeA, int SubshapeB, bool IsSensor, bool HasContact, Vector3 Point, Vector3 Normal, float Depth, PhysicsContactEndReason EndReason);
