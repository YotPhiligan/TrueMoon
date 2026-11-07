using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Explicit bounded sRGB transfer functions; no implicit clamp, alpha or HDR conversion.</summary>
public static class RenderColor
{
    /// <summary>Validates three finite channels in [0,1].</summary><param name="value">Color or linear multiplier.</param>
    public static void Validate(Vector3 value) => ContentValidation.Require(ContentValidation.UnitRange(value), RenderContentError.InvalidMaterial, "Color channels must be finite and in [0,1].");
    /// <summary>Decodes one stored-sRGB channel to linear light.</summary><param name="value">Finite sRGB in [0,1].</param><returns>Linear channel.</returns>
    public static float Decode(float value)
    {
        Validate(new(value));
        return (float)(value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4));
    }
    /// <summary>Encodes one linear-light channel for an sRGB-valued UNORM target.</summary><param name="value">Finite linear value in [0,1].</param><returns>Stored-sRGB channel.</returns>
    public static float Encode(float value)
    {
        Validate(new(value));
        return (float)(value <= 0.0031308 ? 12.92 * value : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055);
    }
    /// <summary>Decodes all three stored-sRGB channels.</summary><param name="value">Stored-sRGB RGB.</param><returns>Linear RGB.</returns>
    public static Vector3 Decode(Vector3 value) => new(Decode(value.X), Decode(value.Y), Decode(value.Z));
    /// <summary>Encodes all three linear channels.</summary><param name="value">Linear RGB.</param><returns>Stored-sRGB RGB.</returns>
    public static Vector3 Encode(Vector3 value) => new(Encode(value.X), Encode(value.Y), Encode(value.Z));
}
