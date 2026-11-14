namespace TrueMoon.Kton;

/// <summary>Runtime identity of one world incarnation. Default is invalid; never persist as an asset ID.</summary>
/// <param name="Value">Nonempty value issued once by the provider for a new world.</param>
public readonly record struct PhysicsWorldId(Guid Value)
{
    /// <summary>Whether the value is structurally valid; does not prove that its world is alive.</summary>
    public bool IsValid => Value != Guid.Empty;
}
