using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Control v3 wire representation of one opaque cube. SceneSnapshot owns a validated deep copy.</summary>
public sealed class ScenePrimitive
{
    /// <summary>Exactly 16 row-major world matrix elements, including the affine last column.</summary>
    public float[] Transform { get; set; } = [];
    /// <summary>Stored-sRGB red in [0,1].</summary>
    public float Red { get; set; }
    /// <summary>Stored-sRGB green in [0,1].</summary>
    public float Green { get; set; }
    /// <summary>Stored-sRGB blue in [0,1].</summary>
    public float Blue { get; set; }

    /// <summary>Validates and decodes the mutable wire payload into an immutable draw value.</summary>
    public RenderPrimitive ToPrimitive()
    {
        if (Transform is not { Length: 16 }) throw new InvalidDataException("A primitive requires 16 matrix elements.");
        var m = Transform;
        var result = new RenderPrimitive(new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11], m[12], m[13], m[14], m[15]), new(Red, Green, Blue));
        result.Validate(); return result;
    }

    /// <summary>Encodes a validated draw value into a new independently owned wire payload.</summary>
    /// <param name="primitive">World-space cube and material.</param>
    public static ScenePrimitive FromPrimitive(RenderPrimitive primitive)
    {
        primitive.Validate(); var m = primitive.World;
        return new() { Transform = [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44],
            Red = primitive.Color.X, Green = primitive.Color.Y, Blue = primitive.Color.Z };
    }
}
