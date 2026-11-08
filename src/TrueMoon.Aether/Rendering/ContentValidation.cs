using System.Collections.Immutable;
using System.Numerics;

namespace TrueMoon.Aether.Rendering;

internal static class ContentValidation
{
    internal static void Require(bool condition, RenderContentError error, string message)
    { if (!condition) throw new RenderContentException(error, message); }
    internal static void Version(int version) => Require(version == 1, RenderContentError.UnsupportedVersion, "Only exact asset schema version one is supported.");
    internal static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    internal static bool UnitRange(Vector3 value) => Finite(value) && value.X is >= 0 and <= 1 && value.Y is >= 0 and <= 1 && value.Z is >= 0 and <= 1;
    internal static ImmutableArray<T> Copy<T>(IReadOnlyList<T> source, int maximum, string name)
    {
        ArgumentNullException.ThrowIfNull(source, name);
        var count = source.Count;
        Require(count >= 0 && count <= maximum, RenderContentError.LimitExceeded, "Content collection admission exceeded.");
        var builder = ImmutableArray.CreateBuilder<T>(count);
        for (var i = 0; i < count; i++) builder.Add(source[i]);
        return builder.MoveToImmutable();
    }
    internal static bool Finite(Matrix4x4 matrix)
    {
        ReadOnlySpan<float> values = [matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44];
        foreach (var value in values) if (!float.IsFinite(value)) return false;
        return true;
    }
    internal static bool Affine(Matrix4x4 matrix)
    {
        var determinant = matrix.GetDeterminant();
        return Finite(matrix) && matrix.M14 == 0 && matrix.M24 == 0 && matrix.M34 == 0 && matrix.M44 == 1 && float.IsFinite(determinant) && determinant != 0;
    }

    // Matrix4x4.Invert can report success with finite zeros after intermediate overflow.
    // Require a usable Single inverse, checking both products with double accumulation.
    internal static bool TryInvertFinite(Matrix4x4 matrix, out Matrix4x4 inverse)
    {
        inverse = default;
        if (!Finite(matrix) || !Matrix4x4.Invert(matrix, out inverse) || !Finite(inverse)) return false;
        ReadOnlySpan<float> a = [matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44];
        ReadOnlySpan<float> b = [inverse.M11, inverse.M12, inverse.M13, inverse.M14, inverse.M21, inverse.M22, inverse.M23, inverse.M24,
            inverse.M31, inverse.M32, inverse.M33, inverse.M34, inverse.M41, inverse.M42, inverse.M43, inverse.M44];
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
        {
            double forward = 0, backward = 0;
            for (var k = 0; k < 4; k++)
            { forward += (double)a[row * 4 + k] * b[k * 4 + column]; backward += (double)b[row * 4 + k] * a[k * 4 + column]; }
            var expected = row == column ? 1 : 0;
            if (Math.Abs(forward - expected) > .001 || Math.Abs(backward - expected) > .001) return false;
        }
        return true;
    }
}
