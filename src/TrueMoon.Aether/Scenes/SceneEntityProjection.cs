namespace TrueMoon.Aether.Scenes;

/// <summary>Explicit world-space pose and interaction policy for the existing SceneEntity contract.</summary>
/// <param name="X">Finite world X coordinate.</param>
/// <param name="Y">Finite world Y coordinate.</param>
/// <param name="Z">Finite world Z coordinate.</param>
/// <param name="Interactive">Whether the existing game consumer treats the entity as interactive.</param>
public readonly record struct SceneEntityProjection(float X, float Y, float Z, bool Interactive)
{
    /// <summary>Rejects nonfinite coordinates before a compatibility snapshot is published.</summary>
    public void Validate()
    {
        if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Z))
            throw new InvalidDataException("Projected world coordinates must be finite.");
    }
}
