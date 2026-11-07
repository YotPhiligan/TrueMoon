using System.Numerics;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class PrimitiveExtractionTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(64)]
    public void Capture_DrawableBoundariesAndManyLogicalNodes_PublishesEveryDrawable(int draws)
    {
        using var world = new World();
        for (var index = 0; index < 129; index++) world.CreateEntity();
        var expected = Enumerable.Range(0, draws).Select(index => Add(world, new(index, 2, -3), 0)).ToArray();
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 23));
        Assert.Null(extraction.Snapshot);
        var frame = extraction.Capture(0);
        Assert.Same(frame, extraction.Snapshot);
        Assert.Equal(expected, frame.Items.Select(item => item.Entity));
        Assert.Equal(draws, frame.Scene.Primitives.Length);
        Assert.Equal((world.Id, PrimitivePipelineFixture.SceneId, 23L, 0UL), (frame.WorldId, frame.SceneId, frame.Revision, frame.Tick));
        Assert.Equal(new CameraTransform(0, 1, 6, 0, 0, 0), frame.Scene.Camera);
        Assert.Equal((MathF.PI / 3, .1f, 100f), (frame.Scene.FieldOfView, frame.Scene.Near, frame.Scene.Far));
        Assert.Equal(frame.Items.Select(item => item.Primitive), frame.Scene.Primitives);
        for (var index = 0; index < draws; index++) Assert.Equal(new Vector3(index, 2, -3), frame.Items[index].Primitive.World.Translation);
    }

    [Fact]
    public void Capture_RecycledSlotsSortByCurrentIdentityAndDetachedFrameSurvivesMutationAndDisposal()
    {
        var world = new World();
        var removed = Add(world, new(99, 0, 0), 7);
        var second = Add(world, new(2, 0, 0), 7);
        world.DestroyEntity(removed);
        var recycled = Add(world, new(1, 0, 0), 7);
        var options = new PrimitiveSimulationOptions { Camera = new(3, 4, 9, .2f, .3f, .4f), FieldOfView = .8f, Near = .2f, Far = 80 };
        var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 11, options));
        options.Camera = default; options.MaximumPrimitives = 0; options.FieldOfView = 1.7f;
        var frame = extraction.Capture(7);
        Assert.Equal(removed.Index, recycled.Index);
        Assert.NotEqual(removed.Generation, recycled.Generation);
        Assert.Equal(new[] { recycled, second }, frame.Items.Select(item => item.Entity));
        Assert.Equal(new CameraTransform(3, 4, 9, .2f, .3f, .4f), frame.Scene.Camera);
        Assert.Equal((.8f, .2f, 80f), (frame.Scene.FieldOfView, frame.Scene.Near, frame.Scene.Far));
        world.SetComponent(recycled, new PrimitiveAppearance(new(0, 0, 0)));
        world.SetComponent(recycled, new WorldTransform(Matrix4x4.CreateTranslation(20, 0, 0), 8));
        extraction.Dispose(); extraction.Dispose(); world.Dispose();
        Assert.Null(extraction.Snapshot);
        Assert.Equal(new Vector3(1, 0, 0), frame.Items[0].Primitive.World.Translation);
        Assert.Equal(Vector3.One, frame.Items[0].Primitive.Color);
        Assert.Equal(frame.Items.Select(item => item.Primitive), frame.Scene.Primitives);
        frame.Scene.Validate();
        Assert.Throws<ObjectDisposedException>(() => extraction.Capture(8));
    }

    [Fact]
    public void Capture_SixtyFifthDrawableRejectsWholeFrameAndRetryDoesNotTruncate()
    {
        using var world = new World();
        var handles = Enumerable.Range(0, 65).Select(index => Add(world, new(index, 0, 0), 0)).ToArray();
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0));
        Assert.Throws<InvalidDataException>(() => extraction.Capture(0));
        Assert.Null(extraction.Snapshot);
        world.SetComponent(handles[^1], new PrimitiveAppearance(Vector3.One, false));
        var frame = extraction.Capture(0);
        Assert.Equal(handles.Take(64), frame.Items.Select(item => item.Entity));
        Assert.Equal(64, frame.Scene.Primitives.Length);
    }

    [Theory]
    [InlineData("missing")] [InlineData("tick")] [InlineData("color")] [InlineData("nan")]
    [InlineData("singular")] [InlineData("projective")] [InlineData("disabled-color")] [InlineData("disabled-matrix")]
    public void Capture_LateInvalidOccurrencePreservesPriorCompleteFrame(string invalid)
    {
        using var world = new World();
        var first = Add(world, new(1, 0, 0), 3); var last = Add(world, new(2, 0, 0), 3);
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 5));
        var previous = extraction.Capture(3);
        world.SetComponent(first, new WorldTransform(Matrix4x4.CreateTranslation(10, 0, 0), 4));
        var matrix = Matrix4x4.CreateTranslation(20, 0, 0);
        var color = Vector3.One; var enabled = true;
        if (invalid is "nan" or "disabled-matrix") matrix.M41 = float.NaN;
        if (invalid == "singular") matrix.M11 = 0;
        if (invalid == "projective") matrix.M14 = .5f;
        if (invalid is "color" or "disabled-color") color.Z = 1.01f;
        if (invalid.StartsWith("disabled", StringComparison.Ordinal)) enabled = false;
        world.SetComponent(last, new WorldTransform(matrix, invalid == "tick" ? 3UL : 4UL));
        world.SetComponent(last, new PrimitiveAppearance(color, enabled));
        if (invalid == "missing") world.RemoveComponent<WorldTransform>(last);
        Assert.Throws<InvalidDataException>(() => extraction.Capture(4));
        Assert.Same(previous, extraction.Snapshot);
        Assert.Equal(3UL, previous.Tick);
        Assert.Equal(new[] { new Vector3(1, 0, 0), new Vector3(2, 0, 0) }, previous.Items.Select(item => item.Primitive.World.Translation));
        Assert.All(previous.Items, item => Assert.Equal(Vector3.One, item.Primitive.Color));
    }

    [Fact]
    public void Capture_DisabledOccurrencesDoNotConsumeBudgetButStillRequireMatchingTransform()
    {
        using var world = new World();
        var disabled = Add(world, Vector3.Zero, 0, false);
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0, new() { MaximumPrimitives = 0 }));
        Assert.Empty(extraction.Capture(0).Items);
        world.SetComponent(disabled, new WorldTransform(Matrix4x4.Identity, 1));
        world.SetComponent(disabled, new PrimitiveAppearance(Vector3.One));
        Assert.Throws<InvalidDataException>(() => extraction.Capture(1));
        Assert.Equal(0UL, extraction.Snapshot!.Tick);
        world.SetComponent(disabled, new PrimitiveAppearance(Vector3.One, false));
        world.RemoveComponent<WorldTransform>(disabled);
        Assert.Throws<InvalidDataException>(() => extraction.Capture(1));
    }

    [Fact]
    public void Capture_CancellationNonIncreasingTicksAndDisposedWorldLeaveLastSnapshot()
    {
        var world = new World(); Add(world, Vector3.Zero, 2);
        using var extraction = new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, 0));
        var previous = extraction.Capture(2);
        Assert.Throws<InvalidOperationException>(() => extraction.Capture(2));
        Assert.Throws<InvalidOperationException>(() => extraction.Capture(1));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => extraction.Capture(3, cancellation.Token)).CancellationToken);
        Assert.Same(previous, extraction.Snapshot);
        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => extraction.Capture(3));
        Assert.Same(previous, extraction.Snapshot);
    }

    [Theory]
    [InlineData(-.0001f)] [InlineData(1.0001f)] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void Appearance_RejectsEachInvalidColorChannelEvenDisabled(float invalid)
    {
        foreach (var color in new[] { new Vector3(invalid, 0, 1), new Vector3(0, invalid, 1), new Vector3(0, 1, invalid) })
            Assert.Throws<InvalidDataException>(() => new PrimitiveAppearance(color, false).Validate());
        new PrimitiveAppearance(new(0, .5f, 1), false).Validate();
    }

    [Fact]
    public void Constructor_RejectsMissingSourceIdentityAndNegativeRevision()
    {
        using var world = new World();
        Assert.Throws<ArgumentNullException>(() => new PrimitiveExtractionSystem(null!));
        Assert.Throws<ArgumentNullException>(() => new PrimitiveExtractionSystem(new(null!, PrimitivePipelineFixture.SceneId, 0)));
        Assert.Throws<ArgumentException>(() => new PrimitiveExtractionSystem(new(world, default, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PrimitiveExtractionSystem(new(world, PrimitivePipelineFixture.SceneId, -1)));
    }

    internal static EntityHandle Add(World world, Vector3 position, ulong tick, bool enabled = true)
    {
        var entity = world.CreateEntity();
        world.AddComponent(entity, new PrimitiveAppearance(Vector3.One, enabled));
        world.AddComponent(entity, new WorldTransform(Matrix4x4.CreateTranslation(position), tick));
        return entity;
    }
}
