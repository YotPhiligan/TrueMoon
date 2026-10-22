using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>An opaque unit cube centered at the origin, transformed into world space.</summary>
/// <param name="World">Finite, nonsingular affine row-vector world matrix; shear is preserved.</param>
/// <param name="Color">Stored-sRGB RGB in [0,1].</param>
public readonly record struct RenderPrimitive(Matrix4x4 World, Vector3 Color)
{
    /// <summary>Rejects malformed geometry or material before native command recording.</summary>
    public void Validate()
    {
        ValidateMatrix(World);
        var determinant = World.GetDeterminant();
        if (World.M14 != 0 || World.M24 != 0 || World.M34 != 0 || World.M44 != 1 ||
            !float.IsFinite(determinant) || determinant == 0 ||
            !ValidColor(Color.X) || !ValidColor(Color.Y) || !ValidColor(Color.Z))
            throw new InvalidDataException("Invalid primitive world matrix/material.");
    }

    internal static void ValidateMatrix(Matrix4x4 value)
    {
        ReadOnlySpan<float> values = [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];
        foreach (var item in values)
            if (!float.IsFinite(item)) throw new InvalidDataException("Primitive transform is not representable.");
    }
    private static bool ValidColor(float value) => float.IsFinite(value) && value is >= 0 and <= 1;
}
