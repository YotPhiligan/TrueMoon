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
}
