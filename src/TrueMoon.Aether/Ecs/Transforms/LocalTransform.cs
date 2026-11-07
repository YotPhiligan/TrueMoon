using System.Numerics;

namespace TrueMoon.Aether.Ecs.Transforms;

/// <summary>Local row-vector S*R*T pose. Translation is in parent coordinates; scale must be positive.</summary>
/// <param name="Position">Finite parent-space position.</param><param name="Rotation">Finite unit quaternion, without implicit normalization.</param><param name="Scale">Finite positive scale.</param>
public readonly record struct LocalTransform(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    /// <summary>Gets the explicit identity pose. The default struct is invalid.</summary>
    public static LocalTransform Identity => new(Vector3.Zero, Quaternion.Identity, Vector3.One);
    /// <summary>Maximum accepted error in quaternion squared length.</summary>
    public const float QuaternionTolerance = 0.0001f;
    /// <summary>Rejects malformed pose components without coercion or normalization.</summary>
    public void Validate()
    {
        TransformMath.ValidateVector(Position); TransformMath.ValidateVector(Scale);
        var length = Rotation.LengthSquared();
        if (Scale.X <= 0 || Scale.Y <= 0 || Scale.Z <= 0 || !float.IsFinite(length) || MathF.Abs(length - 1) > QuaternionTolerance)
            throw new InvalidDataException("Transform requires positive scale and a finite unit quaternion.");
    }
    /// <summary>Creates a finite nonsingular affine matrix, preserving row-vector convention.</summary><returns>Scale*rotation*translation.</returns>
    public Matrix4x4 CreateMatrix()
    {
        Validate();
        var matrix = Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position);
        TransformMath.ValidateMatrix(matrix); return matrix;
    }
}
