using System.Numerics;

namespace TrueMoon.Aether.Ecs.Transforms;

internal static class TransformMath
{
    internal static void ValidateVector(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Vector must be finite.");
    }
    internal static void ValidateMatrix(Matrix4x4 value)
    {
        ReadOnlySpan<float> values = [value.M11, value.M12, value.M13, value.M14, value.M21, value.M22, value.M23, value.M24,
            value.M31, value.M32, value.M33, value.M34, value.M41, value.M42, value.M43, value.M44];
        foreach (var item in values) if (!float.IsFinite(item)) throw new InvalidDataException("World matrix must be finite.");
        var determinant = value.GetDeterminant();
        if (value.M14 != 0 || value.M24 != 0 || value.M34 != 0 || value.M44 != 1 || !float.IsFinite(determinant) || determinant == 0)
            throw new InvalidDataException("World matrix must be a nonsingular affine transform.");
    }
    internal static float Displace(float position, float velocity, double seconds)
    {
        var result = (double)position + velocity * seconds;
        if (!double.IsFinite(result) || result < -float.MaxValue || result > float.MaxValue)
            throw new InvalidDataException("Movement displacement is not representable.");
        return (float)result;
    }
}
