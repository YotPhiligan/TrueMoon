using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class PreviewCameraTests
{
    [Fact]
    public void WithView_FreezesWholeCandidateAndUsesExactMonotonicRevisions()
    {
        var original = PreviewCamera.Default;
        Assert.Equal(1ul, original.Revision);
        Assert.Same(original, original.WithView(original.World, original.Projection, original.Pivot, original.Distance));
        var pose = Matrix4x4.CreateFromYawPitchRoll(.6f, -.2f, .1f) * Matrix4x4.CreateTranslation(2, 3, 8);
        var changed = original.WithView(pose, new(MathF.PI / 2, 1, 11), new(1, 2, 3), 8);
        pose.M41 = 200;
        var rollback = changed.WithView(original.World, original.Projection, original.Pivot, original.Distance);
        Assert.Equal(2ul, changed.Revision); Assert.Equal(3ul, rollback.Revision);
        Assert.Equal(2f, changed.World.M41);
        Assert.Equal(new Vector3(0, 1, 6), original.World.Translation);
        Assert.Equal(new Vector3(0, 1, 0), original.Pivot); Assert.Equal(6f, original.Distance);
        Assert.Equal(original.World, rollback.World);
        Assert.Equal(new PerspectiveProjection(MathF.PI / 2, 1, 11), changed.Projection);
        Assert.Equal(new Vector3(1, 2, 3), changed.Pivot); Assert.Equal(8f, changed.Distance);
        Assert.NotEqual(original.Revision, rollback.Revision);
    }

    [Theory]
    [InlineData("world")] [InlineData("projection")] [InlineData("pivot")] [InlineData("distance")]
    public void WithView_EachChangedValueAdvancesRevision(string part)
    {
        var old = PreviewCamera.Default;
        var next = old.WithView(part == "world" ? Matrix4x4.Identity : old.World,
            part == "projection" ? old.Projection with { Near = 1 } : old.Projection,
            part == "pivot" ? Vector3.Zero : old.Pivot, part == "distance" ? 10 : old.Distance);
        Assert.Equal(2ul, next.Revision); Assert.Equal(1ul, old.Revision);
    }

    [Fact]
    public void WithView_InvalidCandidateAndOverflowPreserveLastValidValue()
    {
        var camera = PreviewCamera.Default;
        Assert.Throws<RenderContentException>(() => camera.WithView(Matrix4x4.CreateScale(2), camera.Projection, camera.Pivot, 6));
        Assert.Equal(Matrix4x4.CreateTranslation(0, 1, 6), camera.World);
        Assert.Equal(1ul, camera.Revision);
        var exhausted = new PreviewCamera(camera.World, camera.Projection, camera.Pivot, 6, ulong.MaxValue);
        Assert.Same(exhausted, exhausted.WithView(camera.World, camera.Projection, camera.Pivot, 6));
        Assert.Throws<OverflowException>(() => exhausted.WithView(Matrix4x4.Identity, camera.Projection, camera.Pivot, 6));
        Assert.Equal(ulong.MaxValue, exhausted.Revision);
    }

    [Fact]
    public void Constructor_RejectsInvalidNavigationAndRevisionValues()
    {
        var c = PreviewCamera.Default;
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCamera(c.World, c.Projection, c.Pivot, 6, 0));
        foreach (var distance in new[] { 0f, -1, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCamera(c.World, c.Projection, c.Pivot, distance, 1));
        foreach (var pivot in new[] { new Vector3(float.NaN, 0, 0), new(0, float.PositiveInfinity, 0), new(0, 0, float.NegativeInfinity) })
            Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCamera(c.World, c.Projection, pivot, 6, 1));
        Assert.Throws<RenderContentException>(() => new PreviewCamera(c.World, default, c.Pivot, 6, 1));
    }

    [Fact]
    public void CreateProjection_PreservesOriginalPrimitiveViewWithoutEulerConversion()
    {
        var expected = new PrimitiveScene(PreviewGameSession.Camera, 0, Vector3.One).CreateViewProjection(640, 360);
        var actual = PreviewCamera.Default.CreateProjection(640, 360);
        Assert.Equal(expected, actual.ViewProjection);
        Assert.Equal(640, actual.Width); Assert.Equal(360, actual.Height);
        Assert.Equal(1ul, PreviewCamera.Default.Revision);
    }
}
