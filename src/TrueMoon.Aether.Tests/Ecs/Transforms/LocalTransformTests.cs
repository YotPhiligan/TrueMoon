using System.Numerics;
using TrueMoon.Aether.Ecs.Transforms;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Transforms;

public sealed class LocalTransformTests
{
    [Fact]
    public void Identity_CreatesIdentityMatrix_WhileDefaultIsInvalid()
    {
        Assert.Equal(Matrix4x4.Identity, LocalTransform.Identity.CreateMatrix());
        Assert.Throws<InvalidDataException>(() => default(LocalTransform).Validate());
        Assert.Throws<InvalidDataException>(() => default(LocalTransform).CreateMatrix());
    }

    [Fact]
    public void CreateMatrix_UsesLiteralRowVectorScaleRotationTranslation()
    {
        var pose = new LocalTransform(new(5, -7, 11), new(.5f, .5f, .5f, .5f), new(2, 3, 4));
        var expected = new Matrix4x4(0, 2, 0, 0, 0, 0, 3, 0, 4, 0, 0, 0, 5, -7, 11, 1);
        Assert.Equal(expected, pose.CreateMatrix());
        Assert.Equal(new Vector3(33, -3, 26), Vector3.Transform(new(2, 5, 7), pose.CreateMatrix()));
        Assert.Equal(expected, (pose with { Rotation = -pose.Rotation }).CreateMatrix());
    }

    public static IEnumerable<object[]> InvalidPoses()
    {
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        for (var axis = 0; axis < 3; axis++)
        {
            var vector = Vector3.Zero; vector[axis] = value;
            yield return [LocalTransform.Identity with { Position = vector }];
            vector = Vector3.One; vector[axis] = value;
            yield return [LocalTransform.Identity with { Scale = vector }];
        }
        for (var axis = 0; axis < 3; axis++)
        foreach (var value in new[] { 0f, -float.Epsilon, -1f })
        {
            var vector = Vector3.One; vector[axis] = value;
            yield return [LocalTransform.Identity with { Scale = vector }];
        }
        foreach (var q in new[] { default(Quaternion), new Quaternion(0, 0, 0, 2),
                     new Quaternion(float.NaN, 0, 0, 1), new Quaternion(0, float.PositiveInfinity, 0, 1),
                     new Quaternion(0, 0, float.NegativeInfinity, 1), new Quaternion(0, 0, 0, float.NaN) })
            yield return [LocalTransform.Identity with { Rotation = q }];
    }

    [Theory]
    [MemberData(nameof(InvalidPoses))]
    public void Validate_RejectsEveryNonfiniteNonpositiveAndNonunitPartition(LocalTransform pose)
    {
        Assert.Throws<InvalidDataException>(() => pose.Validate());
        Assert.Throws<InvalidDataException>(() => pose.CreateMatrix());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void QuaternionTolerance_AcceptsInsideAndRejectsAdjacentOutsideWithoutNormalizing(bool upper)
    {
        var edge = MathF.Sqrt(1 + (upper ? LocalTransform.QuaternionTolerance : -LocalTransform.QuaternionTolerance));
        var inside = upper ? MathF.BitDecrement(edge) : MathF.BitIncrement(edge);
        var outside = upper ? MathF.BitIncrement(edge) : MathF.BitDecrement(edge);
        var pose = LocalTransform.Identity with { Rotation = new Quaternion(inside, 0, 0, 0) };
        pose.Validate();
        var matrix = pose.CreateMatrix();
        Assert.Equal(1 - 2 * inside * inside, matrix.M22);
        Assert.NotEqual(-1f, matrix.M22);
        Assert.Throws<InvalidDataException>(() => (pose with { Rotation = new Quaternion(outside, 0, 0, 0) }).Validate());
    }

    [Fact]
    public void CreateMatrix_RejectsRepresentationalSingularityAndOverflow()
    {
        var tiny = LocalTransform.Identity with { Scale = new Vector3(float.Epsilon) };
        tiny.Validate();
        Assert.Throws<InvalidDataException>(() => tiny.CreateMatrix());
        var huge = LocalTransform.Identity with { Scale = new Vector3(float.MaxValue) };
        huge.Validate();
        Assert.Throws<InvalidDataException>(() => huge.CreateMatrix());
        Assert.Equal(float.Epsilon, (LocalTransform.Identity with { Scale = new(float.Epsilon, 1, 1) }).CreateMatrix().M11);
    }
}
