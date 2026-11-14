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
