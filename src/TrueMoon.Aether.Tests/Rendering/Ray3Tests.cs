using System.Numerics;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class Ray3Tests
{
    [Theory]
    [InlineData(1f)] [InlineData(float.Epsilon)] [InlineData(float.MaxValue)]
    public void Constructor_NormalizesDirectionWithoutChangingWorldDistance(float magnitude)
    {
        var ray = new Ray3(new(0, 0, 6), new(0, 0, -magnitude), 20);
        Assert.Equal(-Vector3.UnitZ, ray.Direction);
        Assert.True(ray.TryIntersectUnitCube(Matrix4x4.Identity, out var hit)); Assert.Equal(5.5, hit);
        Assert.Equal(20, ray.MaximumDistance);
    }

    [Theory]
    [InlineData(5.499999, false)] [InlineData(5.5, true)] [InlineData(5.500001, true)]
    public void Intersection_ClipsAtInclusiveFarSurface(double maximum, bool hits)
    {
        var ray = new Ray3(new(0, 0, 6), -Vector3.UnitZ, maximum);
        Assert.True(ray.TryIntersectUnitCube(Matrix4x4.Identity, out var hit));
        Assert.Equal(hits ? 5.5 : (double?)null, hit);
    }

    [Theory]
    [InlineData(0f, .5)] [InlineData(.25f, .75)] [InlineData(.5f, 0)]
    public void Intersection_NearInsideUsesExitAndExactNearSurfaceCanHit(float z, double expected)
    {
        var ray = new Ray3(new(0, 0, z), -Vector3.UnitZ, 10);
        Assert.True(ray.TryIntersectUnitCube(Matrix4x4.Identity, out var hit)); Assert.Equal(expected, hit);
        if (z == 0)
        { Assert.True(new Ray3(Vector3.Zero, -Vector3.UnitZ, .25).TryIntersectUnitCube(Matrix4x4.Identity, out hit)); Assert.Null(hit); }
    }

    [Theory]
    [InlineData(0f, true)] [InlineData(.5f, true)] [InlineData(.50001f, false)]
    public void Intersection_ParallelAxesRespectInclusiveCubeBounds(float x, bool hits)
    {
        Assert.True(new Ray3(new(x, 0, 6), -Vector3.UnitZ, 10).TryIntersectUnitCube(Matrix4x4.Identity, out var hit));
        Assert.Equal(hits ? 5.5 : (double?)null, hit);
    }

    [Fact]
    public void Intersection_RejectsBehindDisjointSlabsAndNearClippedSurfacesAsValidMisses()
    {
        var ray = new Ray3(Vector3.Zero, -Vector3.UnitZ, 10);
        Assert.True(ray.TryIntersectUnitCube(Matrix4x4.CreateTranslation(0, 0, 2), out var hit)); Assert.Null(hit);
        Assert.True(new Ray3(new(2, 0, 6), Vector3.Normalize(new(-1, 0, -1)), 20).TryIntersectUnitCube(Matrix4x4.Identity, out hit)); Assert.Null(hit);
        Assert.True(new Ray3(new(0, 0, -1), -Vector3.UnitZ, 10).TryIntersectUnitCube(Matrix4x4.Identity, out hit)); Assert.Null(hit);
    }

    [Fact]
    public void Intersection_PreservesWorldParameterThroughNonuniformScaleShearAndReflection()
    {
        var ray = new Ray3(new(0, 1, 6), -Vector3.UnitZ, 20);
        var shear = Matrix4x4.Identity; shear.M31 = .4f; shear.M32 = .2f;
        foreach (var sx in new[] { 2f, -2f })
        {
            var world = Matrix4x4.CreateScale(sx, 1, 3) * shear * Matrix4x4.CreateTranslation(0, 1, -1);
            Assert.True(ray.TryIntersectUnitCube(world, out var hit)); Assert.InRange(hit!.Value, 5.499999, 5.500001);
            Assert.Equal(-Vector3.UnitZ, ray.Direction); Assert.Equal(20, ray.MaximumDistance);
        }
    }

    [Fact]
    public void Intersection_HierarchyProductUsesLocalCubeRatherThanWorldAabb()
    {
        var child = Matrix4x4.CreateRotationY(MathF.PI / 4) * Matrix4x4.CreateTranslation(0, 0, -2);
        var parent = Matrix4x4.CreateScale(2, 1, 3);
        var world = child * parent;
        // At x0 the rotated local cube exits at local z=sqrt(.5), transformed by parent Z scale.
        var ray = new Ray3(new(0, 0, 6), -Vector3.UnitZ, 20);
        Assert.True(ray.TryIntersectUnitCube(world, out var hit));
        Assert.InRange(hit!.Value, 12 - 3 * Math.Sqrt(.5) - .00001, 12 - 3 * Math.Sqrt(.5) + .00001);
        var rotated = Matrix4x4.CreateRotationY(MathF.PI / 4);
        Assert.True(new Ray3(new(.7f, .0f, .7f), -Vector3.UnitY, 3).TryIntersectUnitCube(rotated, out hit)); Assert.Null(hit);
    }

    [Fact]
    public void Intersection_MalformedWorldOrInverseIsUnavailable()
    {
        var ray = new Ray3(new(0, 0, 6), -Vector3.UnitZ, 10);
        var perspective = Matrix4x4.Identity; perspective.M14 = .01f;
        var nan = Matrix4x4.Identity; nan.M41 = float.NaN;
        foreach (var world in new[] { default(Matrix4x4), Matrix4x4.CreateScale(0), perspective, nan, Matrix4x4.CreateScale(float.Epsilon, 1, 1) })
        { Assert.False(ray.TryIntersectUnitCube(world, out var hit)); Assert.Null(hit); }
        Assert.False(default(Ray3).TryIntersectUnitCube(Matrix4x4.Identity, out _));
    }

    [Fact]
    public void Constructor_RejectsInvalidOriginDirectionAndDistance()
    {
        foreach (var direction in new[] { Vector3.Zero, new(float.NaN, 0, 0), new(0, float.PositiveInfinity, 0) })
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ray3(Vector3.Zero, direction, 1));
        foreach (var maximum in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ray3(Vector3.Zero, -Vector3.UnitZ, maximum));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Ray3(new(0, 0, float.NaN), -Vector3.UnitZ, 1));
    }
}
