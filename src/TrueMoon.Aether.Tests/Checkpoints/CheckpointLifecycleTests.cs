using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Runtime;
using Xunit;

namespace TrueMoon.Aether.Tests.Checkpoints;

public sealed class CheckpointLifecycleTests
{
    private static SceneSnapshot Scene() => CheckpointCodecTests.Scene();
    [Fact]
    public async Task Capture_IsObservationalPausedAndAcceptedSequenceRemainsUsable()
    {
        var factory = new CheckpointRuntimeFixture(); await using var supervisor = factory.Supervisor();
        await supervisor.StartAsync(Scene()); for (var i = 0; i < 5; i++) await supervisor.StepAsync();
        var identity = supervisor.Status.Identity; var checkpoint = await supervisor.CaptureCheckpointAsync();
        Assert.Equal(5ul, checkpoint.State.Tick); Assert.Equal(0.625f, checkpoint.State.Camera.X); Assert.True(checkpoint.State.Interaction);
        Assert.Equal(identity!.GameBuildId, checkpoint.SourceGameBuildId); Assert.Equal(identity.HostContractId, checkpoint.HostContractId);
        Assert.Equal(identity, supervisor.Status.Identity); Assert.Equal(5ul, supervisor.Status.Tick); Assert.Null(supervisor.Status.Failure);
        Assert.Equal(5, (await supervisor.GetStatusAsync()).Tick); Assert.Equal(6, (await supervisor.StepAsync()).Tick);
        await supervisor.SetPlayingAsync(true);
        var sent = factory.Leases[0].Commands.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.CaptureCheckpointAsync());
        Assert.Equal(sent, factory.Leases[0].Commands.Count); Assert.Equal(SessionState.Playing, supervisor.Status.State);
    }
    [Theory]
    [InlineData("unsupported", CheckpointOutcome.Unsupported)]
    [InlineData("rejected", CheckpointOutcome.CaptureRejected)]
    [InlineData("invalid", CheckpointOutcome.CaptureRejected)]
    public async Task AcknowledgedRejection_PreservesPausedGameHeartbeatSequenceAndResume(string mode, CheckpointOutcome outcome)
    {
        var factory = new CheckpointRuntimeFixture { Capture = mode };
        await using var supervisor = factory.Supervisor(new() { HeartbeatInterval = TimeSpan.FromMilliseconds(10) });
        await supervisor.StartAsync(Scene()); await supervisor.StepAsync(); var identity = supervisor.Status.Identity;
        var error = await Assert.ThrowsAsync<CheckpointRejectedException>(() => supervisor.CaptureCheckpointAsync());
        Assert.Equal(outcome, error.Outcome); Assert.Equal(SessionState.Paused, supervisor.Status.State); Assert.Null(supervisor.Status.Failure);
        var lease = Assert.Single(factory.Leases); Assert.False(lease.ResourcesReleased); Assert.Equal(0, lease.Stops);
        await TestWait.UntilAsync(() => lease.Commands.Count(c => c == CommandKind.GetStatus) >= 2, TimeSpan.FromSeconds(3));
        Assert.Equal(identity, supervisor.Status.Identity); Assert.Equal(1ul, supervisor.Status.Tick);
        await supervisor.SetPlayingAsync(true); lease.Protocol.FixedTick(); Assert.Equal(2, (await supervisor.GetStatusAsync()).Tick);
        await supervisor.SetPlayingAsync(false); Assert.Equal(3, (await supervisor.StepAsync()).Tick);
        await supervisor.StopAsync(); Assert.True(lease.ResourcesReleased);
    }
    [Theory]
    [InlineData("timeout")] [InlineData("disconnect")] [InlineData("unacknowledged-rejection")]
    [InlineData("unknown-code")] [InlineData("corrupt-payload")] [InlineData("rejection-payload")]
    [InlineData("changed-tick")] [InlineData("mutating-game")]
    public async Task UnconfirmedOrMalformedCaptureOutcome_FaultsAndReapsInsteadOfPretendingRecovery(string invalid)
    {
        var factory = new CheckpointRuntimeFixture();
        if (invalid == "mutating-game") factory.Capture = "mutates";
        factory.BeforeSend = async (request, token) =>
        {
            if (request.Kind != (int)CommandKind.CaptureCheckpoint) return;
            if (invalid == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, token);
            if (invalid == "disconnect") throw new IOException("fixture transport failure");
            if (invalid == "unacknowledged-rejection") throw new CheckpointRejectedException();
        };
        factory.Filter = (request, reply) =>
        {
            if (request.Kind == (int)CommandKind.CaptureCheckpoint)
            {
                if (invalid == "unknown-code") reply.CheckpointOutcome = 99;
                if (invalid == "corrupt-payload") reply.Checkpoint![^1] ^= 1;
                if (invalid == "rejection-payload") reply.CheckpointOutcome = (int)CheckpointOutcome.CaptureRejected;
                if (invalid == "changed-tick") reply.Tick++;
            }
            return reply;
        };
        await using var supervisor = factory.Supervisor(new() { Command = TimeSpan.FromMilliseconds(100), HeartbeatInterval = TimeSpan.FromSeconds(30) });
        await supervisor.StartAsync(Scene());
        if (invalid == "timeout") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => supervisor.CaptureCheckpointAsync());
        else if (invalid == "disconnect") await Assert.ThrowsAsync<IOException>(() => supervisor.CaptureCheckpointAsync());
        else if (invalid == "unacknowledged-rejection") await Assert.ThrowsAsync<CheckpointRejectedException>(() => supervisor.CaptureCheckpointAsync());
        else await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.CaptureCheckpointAsync());
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Equal("CaptureCheckpoint", supervisor.Status.Failure!.Phase);
        Assert.True(Assert.Single(factory.Leases).ResourcesReleased);
    }
    [Fact]
    public async Task RuntimePendingRestore_RejectsModeStepAndSurfaceWithoutAdvancingSequence()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, CheckpointCodecTests.Hash, CheckpointCodecTests.Hash, CheckpointCodecTests.Hash);
        using var game = new Game.Logic.ProbeGame(); var configures = 0;
        var protocol = new RuntimeProtocol(identity, game, 301, _ => configures++, requireRestore: true);
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene);
        load.SceneId = "room"; load.Revision = 7; load.Entities = Scene().CopyEntities(); await protocol.SendAsync(load);
        foreach (var kind in new[] { CommandKind.SetMode, CommandKind.Step, CommandKind.ConfigureSurface, CommandKind.CaptureCheckpoint })
        {
            var request = ControlRequest.Create(identity, 3, kind); request.Playing = true;
            request.Surface = FrameSurface.Create(identity, 1, 32, 16).ToWire();
            await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(request));
        }
        protocol.FixedTick(); Assert.Equal(0ul, game.TickNumber); Assert.Equal(0, configures);
        Assert.False(protocol.TryCaptureFrame(out _));
        var restore = ControlRequest.Create(identity, 3, CommandKind.RestoreCheckpoint);
        restore.Checkpoint = GameCheckpointCodec.Encode(CheckpointCodecTests.Snapshot(37));
        var restored = await protocol.SendAsync(restore);
        Assert.Equal((int)CheckpointOutcome.Restored, restored.CheckpointOutcome);
        Assert.Equal(CheckpointCodecTests.Snapshot(37).State, game.CaptureCheckpoint());
        Assert.True(protocol.TryCaptureFrame(out var frame)); Assert.Equal(37ul, frame.Tick);
        var step = await protocol.SendAsync(ControlRequest.Create(identity, 4, CommandKind.Step));
        Assert.Equal(38, step.Tick);
        var configure = ControlRequest.Create(identity, 5, CommandKind.ConfigureSurface);
        configure.Surface = FrameSurface.Create(identity, 1, 32, 16).ToWire(); await protocol.SendAsync(configure);
        Assert.Equal(1, configures);
    }
    [Fact]
    public async Task StartupRestore_GatesSurfaceAndFirstFrameUntilCompleteStateIsApplied()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new CheckpointRuntimeFixture { Produce = true, BeforeSend = async (request, token) =>
        { if (request.Kind == (int)CommandKind.RestoreCheckpoint) { entered.TrySetResult(); await release.Task.WaitAsync(token); } } };
        await using var supervisor = factory.Supervisor(); await using var frames = new FrameSession(supervisor);
        var checkpoint = CheckpointCodecTests.Snapshot(37);
        var starting = frames.StartAsync(Scene(), checkpoint, 32, 16); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var lease = Assert.Single(factory.Leases);
        Assert.Equal(SessionState.Starting, supervisor.Status.State); Assert.Null(frames.Display); Assert.Equal(0, frames.MappingCount); Assert.Equal(0, lease.Configures);
        Assert.False(lease.Protocol.TryCaptureFrame(out _)); Assert.Throws<InvalidOperationException>(() => lease.Protocol.CaptureFrame());
        release.SetResult(); await TestWait.UntilAsync(() => frames.Display?.Receiver.FirstFrame.IsCompletedSuccessfully == true, TimeSpan.FromSeconds(3));
        var display = frames.Display!; using (var frame = Assert.IsType<FrameLease>(display.Receiver.TakeLatest()))
        { Assert.Equal(37ul, frame.Metadata.Tick); Assert.False(starting.IsCompleted); frames.Presented(display, frame.Metadata); }
        await starting;
        Assert.Equal(checkpoint.State, lease.Game.CaptureCheckpoint()); Assert.Equal(37ul, supervisor.Status.Tick);
        Assert.Equal(1, lease.Configures); Assert.Equal(1, frames.MappingCount);
        Assert.Equal(new[] { CommandKind.Hello, CommandKind.LoadScene, CommandKind.RestoreCheckpoint, CommandKind.SetMode, CommandKind.ConfigureSurface }, lease.Commands);
        var captured = await supervisor.CaptureCheckpointAsync(); Assert.Equal(checkpoint.State, captured.State);
        Assert.NotEqual(checkpoint.SourceGameBuildId, captured.SourceGameBuildId); // Compatible cross-build restore.
        await frames.StopAsync(); Assert.Equal(0, frames.MappingCount); Assert.True(lease.ResourcesReleased); Assert.True(lease.Worker.IsCompleted);
    }
    [Theory]
    [InlineData("unsupported")] [InlineData("rejected")] [InlineData("no-apply")] [InlineData("partial")] [InlineData("corrupted")]
    [InlineData("bad-ack")]
    public async Task FailedStartupRestore_NeverConfiguresSurfaceAndReapsCandidate(string invalid)
    {
        var factory = new CheckpointRuntimeFixture { Produce = true };
        if (invalid == "unsupported") factory.Capture = "unsupported";
        if (invalid is "rejected" or "no-apply" or "partial") factory.Restore = invalid;
        if (invalid == "corrupted") factory.BeforeSend = (request, _) =>
        { if (request.Kind == (int)CommandKind.RestoreCheckpoint) request.Checkpoint![^1] ^= 1; return Task.CompletedTask; };
        if (invalid == "bad-ack") factory.Filter = (request, reply) => { if (request.Kind == (int)CommandKind.RestoreCheckpoint) reply.Tick--; return reply; };
        await using var supervisor = factory.Supervisor(); await using var frames = new FrameSession(supervisor);
        if (invalid is "unsupported" or "rejected" or "corrupted")
            await Assert.ThrowsAsync<CheckpointRejectedException>(() => frames.StartAsync(Scene(), CheckpointCodecTests.Snapshot(), 32, 16));
        else await Assert.ThrowsAsync<InvalidDataException>(() => frames.StartAsync(Scene(), CheckpointCodecTests.Snapshot(), 32, 16));
        var lease = Assert.Single(factory.Leases); Assert.True(lease.ResourcesReleased); Assert.Equal(0, lease.Configures);
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Equal("RestoreCheckpoint", supervisor.Status.Failure!.Phase);
        Assert.Null(frames.Display); Assert.Equal(0, frames.MappingCount);
    }
    [Theory]
    [InlineData("host")] [InlineData("scene")] [InlineData("revision")]
    public async Task IncompatibleCheckpoint_IsRejectedBeforeOldSessionOrGenerationChanges(string field)
    {
        var checkpoint = CheckpointCodecTests.Snapshot();
        var invalid = new GameCheckpoint(field == "host" ? new('C', 64) : checkpoint.HostContractId, checkpoint.SourceGameBuildId,
            field == "scene" ? "other" : checkpoint.SceneId, field == "revision" ? 8 : checkpoint.SceneRevision, checkpoint.State);
        var factory = new CheckpointRuntimeFixture(); await using var supervisor = factory.Supervisor(); await supervisor.StartAsync(Scene());
        var identity = supervisor.Status.Identity;
        await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartAsync(Scene(), invalid));
        Assert.Equal(identity, supervisor.Status.Identity); Assert.False(Assert.Single(factory.Leases).ResourcesReleased);
        Assert.Equal(0, (await supervisor.GetStatusAsync()).Tick);
        await supervisor.StopAsync(); await supervisor.StartAsync(Scene(), checkpoint);
        Assert.Equal(identity!.Generation + 1, supervisor.Status.Identity!.Generation); Assert.Equal(checkpoint.State.Tick, supervisor.Status.Tick);
    }
    [Fact]
    public async Task StopDuringRestore_CancelsPendingStartupWithoutPublishingOrLeakingFrames()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new CheckpointRuntimeFixture { Produce = true, BeforeSend = async (request, token) =>
        { if (request.Kind == (int)CommandKind.RestoreCheckpoint) { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); } } };
        await using var supervisor = factory.Supervisor(); await using var frames = new FrameSession(supervisor);
        var starting = frames.StartAsync(Scene(), CheckpointCodecTests.Snapshot(), 32, 16); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await frames.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        var lease = Assert.Single(factory.Leases); Assert.True(lease.ResourcesReleased); Assert.True(lease.Worker.IsCompleted);
        Assert.Equal(SessionState.Stopped, supervisor.Status.State); Assert.Equal(0, lease.Configures); Assert.Null(frames.Display); Assert.Equal(0, frames.MappingCount);
    }
}
