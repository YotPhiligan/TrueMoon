using System.Numerics;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Tests.Rendering.Snapshots;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuSnapshotFrameTests
{
    [Fact]
    public async Task OwnedMeshFrame_RejectsWrongSessionExtentPayloadAndMetadataWithoutDroppingReservation()
    {
        await using var fixture = await SnapshotFixture.CreateAsync(); var snapshot = await fixture.CaptureAsync();
        await using var recording = (await fixture.View.TakeLatestAsync())!;
        var stamp = new GpuFrameStamp(new(fixture.Device.Identity.SessionId, 1, new('A', 64), new('B', 64), new('C', 64)), 1, 1, 0, 2);
        var data = GpuFrameData.FromSnapshot(stamp, recording); data.Validate(stamp, 640, 360);
        Assert.Empty(data.Pixels); Assert.Null(data.Scene); Assert.Same(recording, data.Snapshot);
        Assert.Equal(snapshot.Tick, data.Metadata.Tick); Assert.Equal(snapshot.ViewId, data.Metadata.RenderView!.ViewId);
        Assert.Throws<InvalidDataException>(() => data.Validate(stamp, 320, 180));
        Assert.Throws<InvalidDataException>(() => (data with { Pixels = [1] }).Validate(stamp, 640, 360));
        Assert.Throws<InvalidDataException>(() => (data with { Metadata = data.Metadata with { RenderView = null } }).Validate(stamp, 640, 360));
        Assert.Throws<InvalidDataException>(() => data.Validate(stamp with { SurfaceGeneration = 2 }, 640, 360));
        Assert.Throws<InvalidDataException>(() => data.Validate(stamp with { Identity = stamp.Identity with { SessionId = Guid.NewGuid() } }, 640, 360));
        Assert.False(recording.IsReleased); Assert.Equal(1, fixture.View.Counts.HasRecording ? 1 : 0);
    }

    [Fact]
    public async Task PreviewReservation_RejectsForeignOpeningContentCameraAndRuntimeStamps()
    {
        await using var fixture = await SnapshotFixture.CreateAsync();
        var snapshot = await fixture.CaptureAsync();
        await using var recording = (await fixture.View.TakeLatestAsync())!;
        var stamp = new GpuFrameStamp(new(fixture.Device.Identity.SessionId, 1, new('A', 64), new('B', 64), new('C', 64)), 1, 1, 0, 2);
        var camera = new PreviewCamera(snapshot.CameraWorld, PerspectiveProjection.Default, Vector3.Zero, 6, snapshot.Configuration.CameraRevision);
        var view = new PreviewViewState(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), snapshot.SceneId.Value, snapshot.SourceRevision), camera);
        var cube = new RenderPrimitive(Matrix4x4.CreateTranslation(2, 0, 0), new(.5f, .25f, 1));
        var frame = GpuFrameData.FromPreviewSnapshot(stamp, recording, view, [cube]);

        frame.Validate(stamp, 640, 360);
        Assert.Equal(view.Stamp, frame.Metadata.PreviewView);
        Assert.Null(frame.Metadata.RenderView);
        Assert.Equal(cube, Assert.Single(frame.PreviewOverlay));
        Assert.Same(recording, frame.Snapshot);
        Assert.Empty(frame.Pixels);
        Assert.Null(frame.Scene);

        foreach (var foreign in new[]
        {
            view.Stamp with { DocumentEpoch = Guid.NewGuid() },
            view.Stamp with { ContentId = Guid.NewGuid() },
            view.Stamp with { CameraRevision = view.Camera.Revision + 1 },
            view.Stamp with { SceneId = Guid.NewGuid() },
            view.Stamp with { SceneRevision = view.Context.SceneRevision + 1 }
        })
            Assert.Throws<InvalidDataException>(() => (frame with { Metadata = frame.Metadata with { PreviewView = foreign } }).Validate(stamp, 640, 360));
        Assert.Throws<InvalidDataException>(() => frame.Validate(stamp with { SurfaceGeneration = 2 }, 640, 360));
        Assert.Throws<InvalidDataException>(() => frame.Validate(stamp with { Identity = stamp.Identity with { Generation = 2 } }, 640, 360));
        Assert.Throws<InvalidDataException>(() => (frame with { Pixels = [1] }).Validate(stamp, 640, 360));
        Assert.Throws<InvalidDataException>(() => (frame with { Metadata = frame.Metadata with { RenderView = RenderSnapshotStamp.FromSnapshot(snapshot) } }).Validate(stamp, 640, 360));
        Assert.Throws<InvalidDataException>(() => GpuFrameData.FromPreviewSnapshot(stamp, recording,
            new(view.Context with { SceneId = Guid.NewGuid() }, camera), [cube]));
        Assert.False(recording.IsReleased);
    }
    [Fact]
    public async Task PausedPublication_ReusesOneEnvelopeAndCollectsCompletedSubmissionsBeforeBudgetAdmission()
    {
        await using var fixture = await SnapshotFixture.CreateAsync(); var data = await fixture.CaptureAsync();
        for (var frame = 0; frame < 8; frame++)
        {
            var recording = (await fixture.View.RecordCurrentAsync(CancellationToken.None))!;
            Assert.Same(data, recording.Snapshot);
            var backend = new ControlledSubmission(); backend.Complete();
            var use = await fixture.View.SubmitAsync(recording, new Factory<IRenderSnapshotSubmission, RenderSnapshotData>(_ => backend));
            await use.Completion;
            Assert.InRange(fixture.View.Counts.Submissions, 0, 1); Assert.Equal(1, fixture.View.Counts.Snapshots);
        }
        Assert.Equal(data.Sequence, fixture.View.Current!.Sequence);
    }
}
