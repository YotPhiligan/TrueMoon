namespace TrueMoon.Aether.Kul;

/// <summary>Stable content identity, independent of native voice and ECS entity generations.</summary>
public readonly record struct AudioAssetId(Guid Value)
{
    /// <summary>Whether this is a nonempty identity.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => Value != Guid.Empty;
}
