using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

public sealed class SnapshotBoundaryTests
{
    [Theory]
    [InlineData(0, 16)] [InlineData(16, 0)] [InlineData(-1, -1)] [InlineData(15, 16)]
    [InlineData(16, 15)] [InlineData(1921, 1080)] [InlineData(1920, 1081)]
    public async Task InvalidExtentRejectsWithoutChangingThePreviouslyPublishedFrame(int width, int height)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var old = await t.CaptureAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => t.CaptureAsync(1, t.Configuration with { Width = width, Height = height, SurfaceGeneration = 2 }));
        Assert.Same(old, t.View.Current); Assert.Equal(old.PayloadBytes, t.View.Counts.PayloadBytes); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
    }
    [Theory]
    [InlineData(16, 16)] [InlineData(1920, 1080)]
    public async Task InclusiveExtentBoundariesProduceFiniteProjection(int width, int height)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var result = await t.CaptureAsync(configuration: t.Configuration with { Width = width, Height = height });
        Assert.True(float.IsFinite(result.ViewProjection.M11)); Assert.True(float.IsFinite(result.ViewProjection.M22));
        Assert.Equal(width, result.Configuration.Width); Assert.Equal(height, result.Configuration.Height); Assert.Equal(2, result.Draws.Length);
    }
    [Theory]
    [InlineData("camera-unversioned")] [InlineData("extent-unversioned")]
    [InlineData("camera-zero")] [InlineData("surface-zero")]
    public async Task ConfigurationChangesRequireExplicitNonzeroGeneration(string change)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var old = await t.CaptureAsync(); t.Advance(1);
        var configuration = change switch
        {
            "camera-unversioned" => t.Configuration with { Camera = RenderCameraSource.External(Matrix4x4.CreateTranslation(1, 0, 6), PerspectiveCamera.Default) },
            "extent-unversioned" => t.Configuration with { Width = 800 },
            "camera-zero" => t.Configuration with { CameraRevision = 0 },
            _ => t.Configuration with { SurfaceGeneration = 0 }
        };
        var exception = change.EndsWith("zero", StringComparison.Ordinal) ? typeof(ArgumentOutOfRangeException) : typeof(RenderResourceException);
        await Assert.ThrowsAsync(exception, () => t.CaptureAsync(1, configuration));
        Assert.Same(old, t.View.Current); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
    }
    [Fact]
    public async Task TickAndConfigurationRevisionsNeverRegressAndFailedCandidateDoesNotConsumePublicationSequence()
    {
        await using var t = await SnapshotFixture.CreateAsync(); t.Advance(2);
        var settings = t.Configuration with { CameraRevision = 3, SurfaceGeneration = 3 };
        var old = await t.CaptureAsync(2, settings);
        foreach (var invalid in new[] { settings with { CameraRevision = 2 }, settings with { SurfaceGeneration = 2 } })
            await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(3, invalid));
        await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1, settings));
        await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(2, settings));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t.View.CaptureAsync(3, settings, cancellation.Token));
        Assert.Same(old, t.View.Current); Assert.Equal(1UL, old.Sequence);
        t.Advance(3); var next = await t.CaptureAsync(3, settings); Assert.Equal(2UL, next.Sequence); Assert.Equal(3UL, next.Tick);
    }
    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public async Task RetainedBudgetCountsOldAndCandidateUntilAtomicReplacement(int offset, bool succeeds)
    {
        var bytes = 256L + 2L * Unsafe.SizeOf<RenderDrawRange>();
        await using var t = await SnapshotFixture.CreateAsync(new() { MaximumRetainedBytes = 2 * bytes + offset });
        var old = await t.CaptureAsync(); t.Advance(1);
        if (succeeds) { var next = await t.CaptureAsync(1); Assert.NotSame(old, next); Assert.Equal(2UL, next.Sequence); }
        else
        {
            var error = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1)); Assert.Equal(RenderResourceError.LimitExceeded, error.Error);
            Assert.Same(old, t.View.Current); Assert.Equal(1UL, old.Sequence);
        }
        Assert.Equal(bytes, t.View.Counts.PayloadBytes); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
    }
    [Fact]
    public async Task OptionsAreCopiedAndHardDrawRangeLimitCountsAllSlotsRatherThanEntities()
    {
        var source = ResourceTestFixture.Content(); var cube = (MeshDefinition)source.Resolve(RenderContentProfile.CubeAssetId, RenderContentProfile.MeshKindId).Definition;
        var material = source.Resolve(RenderContentProfile.WhiteMaterialAssetId, RenderContentProfile.MaterialKindId);
        var slots = Enumerable.Range(0, 8).Select(i => new MeshSlot(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f" + (1500 + i).ToString("x4")), (uint)(i * 3), (uint)(i == 7 ? 15 : 3))).ToArray();
        var mesh = new MeshDefinition(cube.Vertices, cube.Indices, slots, cube.Bounds); var meshKey = mesh.CreateKey(source.ProjectId, RenderContentProfile.CubeAssetId);
        var bindings = new MaterialBindingSetDefinition(meshKey, slots.Select(slot => new MaterialSlotBinding(slot.SlotId, material.Key)).ToArray());
        var content = new RenderContentManifest(source.ProjectId, source.BuildId, source.Generation,
            [new(meshKey, mesh), material, new(bindings.CreateKey(source.ProjectId, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
        var options = new RenderSnapshotOptions(); await using var t = await SnapshotFixture.CreateAsync(options, content);
        options.MaximumDrawRanges = 0; options.MaximumPayloadBytes = 0;
        for (var i = 2; i < 512; i++) t.AddRenderer(Matrix4x4.Identity);
        var complete = await t.CaptureAsync(); Assert.Equal(4096, complete.Draws.Length);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => (uint)(i * 3)), complete.Draws.Take(8).Select(draw => draw.Slot.FirstIndex));
        Assert.All(complete.Draws.Take(8), draw => Assert.Equal(t.First, draw.Entity)); Assert.Equal(t.Second, complete.Draws[8].Entity);
        Assert.InRange(complete.PayloadBytes, 1, 2 * 1024 * 1024); Assert.Equal(1, t.Backend.CreateCalls);
        t.AddRenderer(Matrix4x4.Identity); t.Advance(1);
        var failure = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1)); Assert.Equal(RenderResourceError.LimitExceeded, failure.Error);
        Assert.Same(complete, t.View.Current); Assert.Equal(complete.PayloadBytes, t.View.Counts.PayloadBytes);
    }
    [Fact]
    public async Task LogicalEntityCeilingIsInclusiveAndRejectsTheNextWithoutLosingCurrent()
    {
        await using var t = await SnapshotFixture.CreateAsync();
        for (var i = t.World.EntityCount; i < 10000; i++) t.World.CreateEntity();
        var data = await t.CaptureAsync(); Assert.Equal(2, data.Draws.Length);
        t.World.CreateEntity(); t.Advance(1);
        var failure = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync(1)); Assert.Equal(RenderResourceError.LimitExceeded, failure.Error);
        Assert.Equal(10001, t.World.EntityCount); Assert.Same(data, t.View.Current); Assert.Equal(1, t.View.Counts.Snapshots);
    }
}
