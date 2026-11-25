using System.Numerics;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderBoundsTests
{
    [Fact]
    public void Transform_PreservesFullShearExtremaFromAllEightCorners()
    {
        var bounds = new RenderBounds(new(-2, -1, -3), new(3, 4, 5));
        var world = new Matrix4x4(0, 2, 0, 0, -3, 1, 0, 0, 1, 0, .5f, 0, 10, -4, 2, 1);

        var result = bounds.Transform(world);

        Assert.Equal(new Vector3(-5, -9, .5f), result.Minimum);
        Assert.Equal(new Vector3(18, 6, 4.5f), result.Maximum);
        Assert.Equal(new Vector3(-2, -1, -3), bounds.Minimum);
        Assert.Equal(new Vector3(3, 4, 5), bounds.Maximum);
    }

    [Fact]
    public void Transform_AcceptsPointAndFlatBoundsIncludingDefaultValue()
    {
        default(RenderBounds).Validate();
        Assert.Equal(new RenderBounds(Vector3.Zero, Vector3.Zero), default(RenderBounds).Transform(Matrix4x4.Identity));
        var point = new RenderBounds(new(2, 3, 4), new(2, 3, 4));
        Assert.Equal(new RenderBounds(new(-2, 6, 6), new(-2, 6, 6)),
            point.Transform(Matrix4x4.CreateScale(-2, 3, .5f) * Matrix4x4.CreateTranslation(2, -3, 4)));
        var flat = new RenderBounds(new(-2, 0, -3), new(3, 0, 5));
        Assert.Equal(new RenderBounds(new(-2, 0, -3), new(3, 0, 5)), flat.Transform(Matrix4x4.Identity));
    }

    [Fact]
    public void Transform_UsesDoubleIntermediatesWhenLargeFiniteProductsCancel()
    {
        var bounds = new RenderBounds(new(1e10f, 1e10f, 4), new(1e10f, 1e10f, 4));
        var world = new Matrix4x4(1e30f, 0, 0, 0, -1e30f, 1e-30f, 0, 0, 0, 0, 1, 0, 7, 0, 0, 1);

        var result = bounds.Transform(world);

        Assert.Equal(7, result.Minimum.X);
        Assert.Equal(4, result.Minimum.Z);
        Assert.InRange(result.Minimum.Y, 0.9999998e-20f, 1.0000002e-20f);
        Assert.Equal(result.Minimum, result.Maximum);
    }

    [Theory]
    [InlineData("minimum-x")] [InlineData("minimum-y")] [InlineData("minimum-z")]
    [InlineData("maximum-x")] [InlineData("maximum-y")] [InlineData("maximum-z")]
    [InlineData("nan-minimum")] [InlineData("infinite-maximum")]
    public void Validate_RejectsEachUnorderedAxisAndNonfiniteEndpoint(string kind)
    {
        var minimum = Vector3.Zero; var maximum = Vector3.One;
        switch (kind)
        {
            case "minimum-x": minimum.X = 2; break;
            case "minimum-y": minimum.Y = 2; break;
            case "minimum-z": minimum.Z = 2; break;
            case "maximum-x": maximum.X = -1; break;
            case "maximum-y": maximum.Y = -1; break;
            case "maximum-z": maximum.Z = -1; break;
            case "nan-minimum": minimum.Z = float.NaN; break;
            default: maximum.Y = float.PositiveInfinity; break;
        }
        var bounds = new RenderBounds(minimum, maximum);

        Assert.Equal(RenderContentError.InvalidBounds, Assert.Throws<RenderContentException>(() => bounds.Validate()).Error);
        Assert.Equal(RenderContentError.InvalidBounds, Assert.Throws<RenderContentException>(() => bounds.Transform(Matrix4x4.Identity)).Error);
    }

    [Theory]
    [InlineData("default")] [InlineData("singular")] [InlineData("perspective-x")]
    [InlineData("perspective-y")] [InlineData("perspective-z")] [InlineData("w-zero")]
    [InlineData("w-two")] [InlineData("nan")] [InlineData("infinity")] [InlineData("determinant-overflow")]
    public void Transform_RejectsNonfiniteSingularOrNonaffineMatrices(string kind)
    {
        var world = Matrix4x4.Identity;
        switch (kind)
        {
            case "default": world = default; break;
            case "singular": world.M11 = 0; break;
            case "perspective-x": world.M14 = .1f; break;
            case "perspective-y": world.M24 = .1f; break;
            case "perspective-z": world.M34 = .1f; break;
            case "w-zero": world.M44 = 0; break;
            case "w-two": world.M44 = 2; break;
            case "nan": world.M23 = float.NaN; break;
            case "infinity": world.M41 = float.NegativeInfinity; break;
            default: world.M11 = world.M22 = world.M33 = float.MaxValue; break;
        }

        Assert.Equal(RenderContentError.InvalidBounds,
            Assert.Throws<RenderContentException>(() => new RenderBounds(Vector3.Zero, Vector3.One).Transform(world)).Error);
    }

    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public void Transform_RejectsUnrepresentableFinalCornerEvenWithFiniteNonsingularMatrix(int sign)
    {
        var point = new Vector3(sign * float.MaxValue, 0, 0);
        var bounds = new RenderBounds(point, point);
        Assert.Equal(RenderContentError.InvalidBounds,
            Assert.Throws<RenderContentException>(() => bounds.Transform(Matrix4x4.CreateScale(2, 1, 1))).Error);
    }
}
