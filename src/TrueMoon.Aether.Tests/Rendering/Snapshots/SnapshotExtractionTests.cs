using System.Numerics;
using System.Runtime.CompilerServices;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Resources;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering.Snapshots;

public sealed class SnapshotExtractionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExplicitCamera_DetachesOrderedExactRevisionsAffineBoundsAndLinearTint(bool external)
    {
        var content = ResourceTestFixture.ChangedContent(ResourceTestFixture.Content(), .5f);
        await using var t = await SnapshotFixture.CreateAsync(content: content);
        var shear = Matrix4x4.Identity; shear.M21 = 1.5f;
        t.World.SetComponent(t.First, new WorldTransform(shear, 0));
        var renderer = t.World.GetComponent<MeshRenderer>(t.First);
        t.World.SetComponent(t.First, renderer with { TintEnabled = true, TintLinear = new(.5f, 1, 0) });
        // Dense pool order is deliberately reversed; extraction order is entity/slot order.
        var second = t.World.GetComponent<MeshRenderer>(t.Second); t.World.RemoveComponent<MeshRenderer>(t.First); t.World.AddComponent(t.First, renderer with { TintEnabled = true, TintLinear = new(.5f, 1, 0) });
        var configuration = external ? t.Configuration with { Camera = RenderCameraSource.External(Matrix4x4.CreateTranslation(0, 0, 6), PerspectiveCamera.Default) } : t.Configuration;
        var result = await t.CaptureAsync(configuration: configuration);
        Assert.Equal(new[] { t.First, t.Second }, result.Draws.Select(draw => draw.Entity));
        var first = result.Draws[0]; Assert.Equal(shear, first.World);
        Assert.Equal(new Vector3(-1.25f, -.5f, -.5f), first.Bounds.Minimum); Assert.Equal(new Vector3(1.25f, .5f, .5f), first.Bounds.Maximum);
        Assert.InRange(first.ColorLinear.X, .10702f, .10703f); Assert.InRange(first.ColorLinear.Y, .21404f, .21405f); Assert.Equal(0, first.ColorLinear.Z);
        Assert.InRange(result.Draws[1].ColorLinear.X, .21404f, .21405f); Assert.Equal(second, t.World.GetComponent<MeshRenderer>(t.Second));
        Assert.Equal(renderer.Mesh, first.Mesh); Assert.Equal(renderer.Bindings, first.Bindings);
        Assert.Equal(t.Device.Identity, result.Identity); Assert.Equal(t.Content.Digest, result.ManifestDigest);
        Assert.Equal(7, result.SourceRevision); Assert.Equal(0UL, result.Tick); Assert.Equal(1UL, result.BindingRevision); Assert.Equal(1UL, result.Sequence);
        Assert.Equal(t.Device.Generation, result.DeviceGeneration); Assert.Equal(t.View.Id, result.ViewId);
        Assert.Equal(Matrix4x4.CreateTranslation(0, 0, 6), result.CameraWorld);
        Assert.Equal(PerspectiveCamera.Default.CreateViewProjection(result.CameraWorld, 640, 360), result.ViewProjection);
        Assert.Equal(PerspectiveCamera.Default.ClearColorSrgb, result.ClearColorSrgb);
        Assert.Equal(256L + 2L * Unsafe.SizeOf<RenderDrawRange>(), result.PayloadBytes);
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RenderDrawRange>());
        Assert.Equal(result.PayloadBytes, t.View.Counts.PayloadBytes); Assert.Equal(1, t.View.Counts.Snapshots);
        Assert.Equal(3, t.Host.Content.Counts.LeaseCount); // root, ready binding, one shared current/pending envelope
        t.World.DestroyEntity(t.First); Assert.Equal(shear, result.Draws[0].World); Assert.Equal(renderer.Mesh, result.Draws[0].Mesh);
    }

    [Theory]
    [InlineData(false, 0U, 0U, 0)] [InlineData(true, 0U, uint.MaxValue, 0)]
    [InlineData(false, uint.MaxValue, uint.MaxValue, 0)] [InlineData(true, uint.MaxValue, uint.MaxValue, 1)]
    [InlineData(true, 2U, 1U, 0)] [InlineData(true, 3U, 1U, 1)]
    public async Task EnabledAndMasks_SelectDrawsWithoutChangingWorld(bool enabled, uint layers, uint cameraLayers, int expected)
    {
        await using var t = await SnapshotFixture.CreateAsync(); t.World.DestroyEntity(t.Second);
        var value = t.World.GetComponent<MeshRenderer>(t.First) with { Enabled = enabled, Layers = layers };
        t.World.SetComponent(t.First, value);
        t.World.SetComponent(t.Camera, PerspectiveCamera.Default with { Layers = cameraLayers });
        var result = await t.CaptureAsync(); Assert.Equal(expected, result.Draws.Length); Assert.Equal(value, t.World.GetComponent<MeshRenderer>(t.First));
        Assert.Equal(256L + expected * (long)Unsafe.SizeOf<RenderDrawRange>(), result.PayloadBytes);
    }

    [Theory]
    [InlineData("key")] [InlineData("binding")] [InlineData("tint")] [InlineData("missing-transform")]
    [InlineData("tick")] [InlineData("nan-matrix")] [InlineData("singular")]
    [InlineData("projective")] [InlineData("bounds-overflow")] [InlineData("appearance")]
    public async Task InvalidHiddenRenderer_IsRejectedBeforeVisibilityAndPreservesCurrent(string change)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var previous = await t.CaptureAsync(); t.Advance(1);
        var renderer = t.World.GetComponent<MeshRenderer>(t.First) with { Enabled = false, Layers = 0 };
        switch (change)
        {
            case "key": renderer = renderer with { Mesh = default }; break;
            case "binding": renderer = renderer with { Bindings = renderer.Mesh }; break;
            case "tint": renderer = renderer with { TintLinear = new(float.NaN) }; break;
            case "missing-transform": t.World.RemoveComponent<WorldTransform>(t.First); break;
            case "tick": t.World.SetComponent(t.First, new WorldTransform(Matrix4x4.Identity, 0)); break;
            case "nan-matrix": var nan = Matrix4x4.Identity; nan.M11 = float.NaN; t.World.SetComponent(t.First, new WorldTransform(nan, 1)); break;
            case "singular": t.World.SetComponent(t.First, new WorldTransform(Matrix4x4.CreateScale(0), 1)); break;
            case "projective": var projective = Matrix4x4.Identity; projective.M14 = .1f; t.World.SetComponent(t.First, new WorldTransform(projective, 1)); break;
            case "bounds-overflow": var huge = Matrix4x4.Identity; huge.M41 = float.MaxValue; huge.M11 = float.MaxValue; t.World.SetComponent(t.First, new WorldTransform(huge, 1)); break;
            case "appearance": t.World.AddComponent(t.First, new PrimitiveAppearance(Vector3.One, true)); break;
        }
        t.World.SetComponent(t.First, renderer);
        await Assert.ThrowsAsync<RenderContentException>(() => t.CaptureAsync(1));
        Assert.Same(previous, t.View.Current); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(previous.PayloadBytes, t.View.Counts.PayloadBytes);
        Assert.Equal(3, t.Host.Content.Counts.LeaseCount); Assert.False(t.World.IsFaulted);
    }

    [Theory]
    [InlineData("missing-camera")] [InlineData("foreign")] [InlineData("stale")] [InlineData("missing-transform")]
    [InlineData("tick")] [InlineData("scale")] [InlineData("shear")] [InlineData("reflection")]
    [InlineData("nonfinite")] [InlineData("settings")] [InlineData("ambiguous")]
    public async Task ExplicitCamera_InvalidPartitionsNeverSelectAnotherCamera(string change)
    {
        await using var t = await SnapshotFixture.CreateAsync(); var previous = await t.CaptureAsync(); t.Advance(1);
        var configuration = t.Configuration;
        switch (change)
        {
            case "missing-camera": t.World.RemoveComponent<PerspectiveCamera>(t.Camera); break;
            case "foreign": configuration = configuration with { Camera = RenderCameraSource.FromEntity(new(Guid.NewGuid(), t.Camera.Index, t.Camera.Generation)) }; break;
            case "stale": t.World.DestroyEntity(t.Camera); break;
            case "missing-transform": t.World.RemoveComponent<WorldTransform>(t.Camera); break;
            case "tick": t.World.SetComponent(t.Camera, new WorldTransform(Matrix4x4.Identity, 0)); break;
            case "scale": t.World.SetComponent(t.Camera, new WorldTransform(Matrix4x4.CreateScale(2), 1)); break;
            case "reflection": t.World.SetComponent(t.Camera, new WorldTransform(Matrix4x4.CreateScale(-1, 1, 1), 1)); break;
            case "shear": var shear = Matrix4x4.Identity; shear.M21 = .1f; t.World.SetComponent(t.Camera, new WorldTransform(shear, 1)); break;
            case "nonfinite": var invalid = Matrix4x4.Identity; invalid.M41 = float.PositiveInfinity; t.World.SetComponent(t.Camera, new WorldTransform(invalid, 1)); break;
            case "settings": t.World.SetComponent(t.Camera, PerspectiveCamera.Default with { Near = 0 }); break;
            case "ambiguous": configuration = configuration with { Camera = new(t.Camera, Matrix4x4.Identity, PerspectiveCamera.Default) }; break;
        }
        configuration = configuration with { CameraRevision = 2 };
        var expected = change == "missing-camera" ? typeof(KeyNotFoundException) : change is "foreign" or "stale" or "ambiguous" ? typeof(RenderResourceException) : typeof(RenderContentException);
        await Assert.ThrowsAsync(expected, () => t.CaptureAsync(1, configuration));
        Assert.Same(previous, t.View.Current); Assert.Equal(1, t.View.Counts.Snapshots); Assert.Equal(3, t.Host.Content.Counts.LeaseCount);
    }

    [Theory]
    [InlineData(0, 2, false)] [InlineData(1, 1, true)] [InlineData(1, 2, false)] [InlineData(2, 2, true)]
    public async Task RangeLimit_BeforeAtAndAfterBoundaryNeverTruncates(int limit, int entities, bool succeeds)
    {
        await using var t = await SnapshotFixture.CreateAsync(new() { MaximumDrawRanges = limit });
        if (entities == 1) t.World.DestroyEntity(t.Second);
        if (succeeds) { var data = await t.CaptureAsync(); Assert.Equal(entities, data.Draws.Length); }
        else { var error = await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync()); Assert.Equal(RenderResourceError.LimitExceeded, error.Error); Assert.Null(t.View.Current); Assert.Equal(0, t.View.Counts.PayloadBytes); Assert.Equal(2, t.Host.Content.Counts.LeaseCount); }
    }

    [Theory]
    [InlineData(-1, false)] [InlineData(0, true)] [InlineData(1, true)]
    public async Task PayloadLimit_ExactElementBytesBoundaryIsCheckedBeforePublication(int offset, bool succeeds)
    {
        var expected = 256L + 2L * Unsafe.SizeOf<RenderDrawRange>();
        await using var t = await SnapshotFixture.CreateAsync(new() { MaximumPayloadBytes = expected + offset, MaximumRetainedBytes = expected + offset });
        if (succeeds) Assert.Equal(expected, (await t.CaptureAsync()).PayloadBytes);
        else { await Assert.ThrowsAsync<RenderResourceException>(() => t.CaptureAsync()); Assert.Null(t.View.Current); Assert.Equal(0, t.View.Counts.PayloadBytes); Assert.Equal(2, t.Host.Content.Counts.LeaseCount); }
    }
}
