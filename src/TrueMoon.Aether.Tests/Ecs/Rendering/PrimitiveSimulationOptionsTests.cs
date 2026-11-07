using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class PrimitiveSimulationOptionsTests
{
    [Fact]
    public void Defaults_AreExplicitFixedStepAndIndependentTransformAndDrawLimits()
    {
        var options = new PrimitiveSimulationOptions();
        Assert.Equal((65536, 64, TimeSpan.FromSeconds(1d / 60)), (options.MaximumTransformEntities, options.MaximumPrimitives, options.FixedStep));
        Assert.Equal(new TrueMoon.Aether.Checkpoints.CameraTransform(0, 1, 6, 0, 0, 0), options.Camera);
        Assert.Equal((MathF.PI / 3, .1f, 100f), (options.FieldOfView, options.Near, options.Far));
    }

    [Theory]
    [InlineData(1, 0, 1L)] [InlineData(1048576, 64, 10000000L)]
    public void Constructor_AcceptsInclusiveOptionBoundaries(int transforms, int draws, long ticks)
    {
        using var world = new World();
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0,
            new() { MaximumTransformEntities = transforms, MaximumPrimitives = draws, FixedStep = TimeSpan.FromTicks(ticks) }));
        Assert.Empty(extraction.Capture(0).Items);
    }

    [Theory]
    [InlineData("transforms-zero")] [InlineData("transforms-large")] [InlineData("draws-negative")]
    [InlineData("draws-large")] [InlineData("step-zero")] [InlineData("step-negative")] [InlineData("step-large")]
    public void Constructor_RejectsImmediatelyOutsideLimitBoundaries(string invalid)
    {
        using var world = new World(); var options = new PrimitiveSimulationOptions();
        switch (invalid)
        {
            case "transforms-zero": options.MaximumTransformEntities = 0; break;
            case "transforms-large": options.MaximumTransformEntities = 1048577; break;
            case "draws-negative": options.MaximumPrimitives = -1; break;
            case "draws-large": options.MaximumPrimitives = 65; break;
            case "step-zero": options.FixedStep = TimeSpan.Zero; break;
            case "step-negative": options.FixedStep = TimeSpan.FromTicks(-1); break;
            case "step-large": options.FixedStep = TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1); break;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0, options)));
    }

    [Theory]
    [InlineData("fov-zero")] [InlineData("fov-pi")] [InlineData("fov-nan")]
    [InlineData("near-zero")] [InlineData("near-nan")] [InlineData("far-equal")]
    [InlineData("far-less")] [InlineData("far-infinity")] [InlineData("camera-nan")]
    public void Constructor_RejectsInvalidCameraAndProjection(string invalid)
    {
        using var world = new World(); var options = new PrimitiveSimulationOptions();
        switch (invalid)
        {
            case "fov-zero": options.FieldOfView = 0; break;
            case "fov-pi": options.FieldOfView = MathF.PI; break;
            case "fov-nan": options.FieldOfView = float.NaN; break;
            case "near-zero": options.Near = 0; break;
            case "near-nan": options.Near = float.NaN; break;
            case "far-equal": options.Far = options.Near; break;
            case "far-less": options.Far = options.Near / 2; break;
            case "far-infinity": options.Far = float.PositiveInfinity; break;
            case "camera-nan": options.Camera = new(float.NaN, 0, 0, 0, 0, 0); break;
        }
        Assert.Throws<InvalidDataException>(() => new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0, options)));
    }
}
