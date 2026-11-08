using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Preview;

/// <summary>Geometry query outcome; Unavailable must never clear selection.</summary>
public enum PreviewPickKind
{
    /// <summary>No admissible query, including the default value.</summary>
    Unavailable,
    /// <summary>Valid query with no surface in the clipped segment.</summary>
    Miss,
    /// <summary>Nearest supported surface.</summary>
    Hit
}

/// <summary>Why a geometry query cannot produce Hit/Miss. Runtime/presentation admission is a separate stage.</summary>
public enum PreviewPickUnavailableReason
{
    /// <summary>Used only by Hit/Miss.</summary>
    None,
    /// <summary>Invalid/default query data or unrepresentable geometry math.</summary>
    InvalidMath,
    /// <summary>Logical point or bounds are outside/unavailable.</summary>
    OutsideImage,
    /// <summary>The complete visible frame is not an exact supported cube profile.</summary>
    UnsupportedGeometry
}

/// <summary>Immutable geometry result carrying its exact frozen source; it does not authorize a selection mutation.</summary>
public readonly record struct PreviewPickResult
{
    internal PreviewPickResult(PreviewPickSnapshot snapshot, PreviewPickKind kind, EntityId? entity, double? distance,
        PreviewPickUnavailableReason reason)
    { Snapshot = snapshot; Kind = kind; Entity = entity; Distance = distance; _reason = reason; }
    private readonly PreviewPickUnavailableReason _reason;
    /// <summary>Gets Hit, Miss or Unavailable.</summary>
    public PreviewPickKind Kind { get; }
    /// <summary>Gets the source retained by this query. Default results have no source.</summary>
    public PreviewPickSnapshot? Snapshot { get; }
    /// <summary>Gets a persistent ID only for Hit.</summary>
    public EntityId? Entity { get; }
    /// <summary>Gets world distance from the near origin only for Hit.</summary>
    public double? Distance { get; }
    /// <summary>Gets an unavailable reason, including InvalidMath for default results.</summary>
    public PreviewPickUnavailableReason UnavailableReason => Kind == PreviewPickKind.Unavailable && _reason == PreviewPickUnavailableReason.None
        ? PreviewPickUnavailableReason.InvalidMath : _reason;
}
