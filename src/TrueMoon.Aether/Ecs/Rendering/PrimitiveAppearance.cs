using System.Numerics;

namespace TrueMoon.Aether.Ecs.Rendering;

/// <summary>Legacy opaque unit-cube material in stored-sRGB. Disabling drawing does not stop movement.</summary>
/// <param name="Color">Finite RGB in [0,1].</param><param name="Enabled">Whether this occurrence draws.</param>
public readonly record struct PrimitiveAppearance(Vector3 Color, bool Enabled = true)
{
    /// <summary>Rejects invalid color even for a disabled occurrence.</summary>
    public void Validate()
    {
        if (!Channel(Color.X) || !Channel(Color.Y) || !Channel(Color.Z)) throw new InvalidDataException("Primitive color must be stored-sRGB in [0,1].");
    }
    private static bool Channel(float value) => float.IsFinite(value) && value is >= 0 and <= 1;
}
