using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Tests.Rendering.Snapshots;
using Xunit;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class RenderViewReadinessTests
{
    [Fact]
    public void Production_IsNotPresentation_AndPausedReproductionDoesNotResetReady()
    {
        var fence = new RenderViewReadiness(); var stamp = RenderSnapshotStampTests.Stamp();
        Assert.False(fence.IsReady); Assert.Throws<InvalidDataException>(() => fence.Presented(stamp));
        fence.Produced(stamp); Assert.False(fence.IsReady);
        fence.Presented(stamp); Assert.True(fence.IsReady);
        fence.Produced(stamp); Assert.True(fence.IsReady);
        fence.Produced(stamp with { Sequence = stamp.Sequence + 1, Tick = stamp.Tick + 1 }); Assert.True(fence.IsReady);
        fence.Reset(); Assert.False(fence.IsReady); Assert.Throws<InvalidDataException>(() => fence.Presented(stamp));
    }
    [Theory]
    [InlineData("surface")] [InlineData("binding")] [InlineData("camera")] [InlineData("world")] [InlineData("manifest")]
    public void ChangedRequiredProvenance_RejectsOldPresentationAndRequiresNewOne(string field)
    {
        var fence = new RenderViewReadiness(); var old = RenderSnapshotStampTests.Stamp(); fence.Produced(old); fence.Presented(old);
        var next = (field switch
        {
            "surface" => old with { SurfaceGeneration = old.SurfaceGeneration + 1 },
            "binding" => old with { BindingRevision = old.BindingRevision + 1 },
            "camera" => old with { CameraRevision = old.CameraRevision + 1 },
            "world" => old with { WorldId = Guid.NewGuid() },
            _ => old with { ManifestHash = new('B', 64) }
        }) with { Sequence = old.Sequence + 1 };
        fence.Produced(next); Assert.False(fence.IsReady);
        Assert.Throws<InvalidDataException>(() => fence.Presented(old)); Assert.False(fence.IsReady);
        fence.Presented(next); Assert.True(fence.IsReady);
    }
    [Fact]
    public void UnproducedAndRegressedSequences_AreRejectedWithoutLosingPreviousReadyFence()
    {
        var fence = new RenderViewReadiness(); var stamp = RenderSnapshotStampTests.Stamp(); fence.Produced(stamp); fence.Presented(stamp);
        Assert.Throws<InvalidDataException>(() => fence.Presented(stamp with { Sequence = stamp.Sequence + 1 }));
        Assert.Throws<InvalidDataException>(() => fence.Presented(stamp with { Tick = stamp.Tick + 1 }));
        Assert.Throws<InvalidDataException>(() => fence.Produced(stamp with { Sequence = stamp.Sequence - 1 })); Assert.True(fence.IsReady);
    }
}
