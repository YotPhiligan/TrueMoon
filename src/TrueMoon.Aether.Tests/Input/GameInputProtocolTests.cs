using System.Buffers;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests.Input;

public sealed class GameInputProtocolTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SessionIdentity Identity => new(Guid.NewGuid(), 1, Hash, Hash, Hash);
    private static SceneSnapshot Scene => new("room", 7, [new() { Id = "switch", Interactive = true }]);
    private static ControlRequest Input(SessionIdentity id, ulong sequence, GameInputState value)
    {
        var request = ControlRequest.Create(id, sequence, CommandKind.SetInput);
        request.InputCaptured = value.Captured; request.InputButtons = (int)value.Buttons; request.InputYaw = value.Yaw; request.InputPitch = value.Pitch; request.InputInteract = value.InteractPressed; return request;
    }
    private static async Task Load(RuntimeProtocol protocol, SessionIdentity identity)
    {
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene); load.SceneId = Scene.Id; load.Revision = Scene.Revision; load.Entities = Scene.CopyEntities();
        await protocol.SendAsync(load);
    }

    [Fact]
    public async Task Input_RequiresHelloAndLoadedScene()
    {
        var id = Identity; using var game = new CameraGame(); var p = new RuntimeProtocol(id, game, 11);
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SendAsync(Input(id, 1, new(true))));
        await p.SendAsync(ControlRequest.Create(id, 1, CommandKind.Hello));
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SendAsync(Input(id, 2, new(true))));
        Assert.Equal(0UL, game.TickNumber); Assert.Equal(-1, game.Revision);
    }
    [Fact]
    public async Task HostReset_DropsHeldLookAndActionWithoutChangingCheckpointState()
    {
        var id = Identity; using var game = new CameraGame(); var p = new RuntimeProtocol(id, game, 11); await Load(p, id);
        var before = game.CaptureCheckpoint(); await p.SendAsync(Input(id, 3, new(true, GameButtons.Forward, .4f, .3f, true)));
        p.ResetInput(); Assert.Equal(before, game.CaptureCheckpoint());
        await p.SendAsync(ControlRequest.Create(id, 4, CommandKind.Step));
        Assert.Equal(1UL, game.TickNumber); Assert.Equal(before.Camera, game.CaptureCheckpoint().Camera); Assert.False(game.CaptureCheckpoint().Interaction);
    }
    [Fact]
    public async Task PlayingLease_ExpiresWithoutRefreshAndFreshPacketResumesMovement()
    {
        var id = Identity; using var game = new CameraGame(); var p = new RuntimeProtocol(id, game, 11); await Load(p, id);
        var play = ControlRequest.Create(id, 3, CommandKind.SetMode); play.Playing = true; await p.SendAsync(play);
        await p.SendAsync(Input(id, 4, new(true, GameButtons.Forward)));
        for (var i = 0; i < 12; i++) p.FixedTick(); var active = game.CaptureCheckpoint();
        Assert.Equal(12UL, active.Tick); Assert.Equal(2.4f, active.Camera.Z, 4);
        p.FixedTick(); Assert.Equal(active.Camera, game.CaptureCheckpoint().Camera); Assert.Equal(13UL, game.TickNumber);
        await p.SendAsync(Input(id, 5, new(true, GameButtons.Forward))); p.FixedTick();
        Assert.Equal(2.35f, game.CaptureCheckpoint().Camera.Z, 4);
    }
    [Fact]
    public async Task Input_StagesWhilePausedAndAppliesOnceAtStepThenReleaseClears()
    {
        var identity = Identity; using var game = new CameraGame(); var protocol = new RuntimeProtocol(identity, game, 11); await Load(protocol, identity);
        var before = game.CaptureCheckpoint();
        var ack = await protocol.SendAsync(Input(identity, 3, new(true, GameButtons.Forward, .1f, .2f, true)));
        Assert.Equal((int)GameInputOutcome.Accepted, ack.InputOutcome); Assert.Equal(0, ack.Tick); Assert.Equal("Paused", ack.Phase); Assert.Equal(before, game.CaptureCheckpoint());
        protocol.FixedTick(); Assert.Equal(before, game.CaptureCheckpoint());
        await protocol.SendAsync(ControlRequest.Create(identity, 4, CommandKind.Step)); var first = game.CaptureCheckpoint();
        Assert.Equal(1UL, first.Tick); Assert.Equal(.1f, first.Camera.Yaw); Assert.Equal(.2f, first.Camera.Pitch); Assert.True(first.Camera.Z < 3); Assert.True(first.Interaction);
        await protocol.SendAsync(ControlRequest.Create(identity, 5, CommandKind.Step)); var second = game.CaptureCheckpoint();
        Assert.True(second.Camera.Z < first.Camera.Z); Assert.Equal(first.Camera.Yaw, second.Camera.Yaw); Assert.Equal(first.Camera.Pitch, second.Camera.Pitch); Assert.True(second.Interaction);
        await protocol.SendAsync(Input(identity, 6, default)); await protocol.SendAsync(ControlRequest.Create(identity, 7, CommandKind.Step));
        Assert.Equal(second.Camera, game.CaptureCheckpoint().Camera); Assert.True(game.CaptureCheckpoint().Interaction);
    }
    [Fact]
    public async Task ModeChangeAndStop_ClearInputBeforeFutureTicks()
    {
        var id = Identity; using var game = new CameraGame(); var p = new RuntimeProtocol(id, game, 11); await Load(p, id);
        await p.SendAsync(Input(id, 3, new(true, GameButtons.Right, .3f, .2f, true)));
        var play = ControlRequest.Create(id, 4, CommandKind.SetMode); play.Playing = true; await p.SendAsync(play); p.FixedTick();
        var neutral = game.CaptureCheckpoint(); Assert.Equal(1UL, neutral.Tick); Assert.Equal(new CameraTransform(0, 1, 3, 0, 0, 0), neutral.Camera); Assert.False(neutral.Interaction);
        await p.SendAsync(Input(id, 5, new(true, GameButtons.Forward))); p.FixedTick(); Assert.True(game.CaptureCheckpoint().Camera.Z < 3);
        await p.SendAsync(ControlRequest.Create(id, 6, CommandKind.SetMode)); var paused = game.CaptureCheckpoint(); p.FixedTick(); Assert.Equal(paused, game.CaptureCheckpoint());
        await p.SendAsync(ControlRequest.Create(id, 7, CommandKind.Step)); Assert.Equal(paused.Camera, game.CaptureCheckpoint().Camera);
        await p.SendAsync(Input(id, 8, new(true, GameButtons.Right))); await p.SendAsync(ControlRequest.Create(id, 9, CommandKind.Stop));
        var stopped = game.CaptureCheckpoint(); p.FixedTick(); Assert.Equal(stopped, game.CaptureCheckpoint());
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SendAsync(Input(id, 10, default)));
    }
    [Fact]
    public async Task RestoreReadinessAndOldSequence_RejectInputWithoutMutatingGame()
    {
        var id = Identity; using var game = new CameraGame(); var p = new RuntimeProtocol(id, game, 11, requireRestore: true);
        await Load(p, id); var original = game.CaptureCheckpoint();
        await Assert.ThrowsAsync<InvalidOperationException>(() => p.SendAsync(Input(id, 3, new(true, GameButtons.Right))));
        Assert.Equal(original, game.CaptureCheckpoint());
        var state = new GameCheckpointState(17, new(1, 1, 3, .1f, .2f, 0), true);
        var restore = ControlRequest.Create(id, 3, CommandKind.RestoreCheckpoint); restore.Checkpoint = GameCheckpointCodec.Encode(new(Hash, Hash, Scene.Id, Scene.Revision, state));
        await p.SendAsync(restore);
        await p.SendAsync(ControlRequest.Create(id, 4, CommandKind.Step)); Assert.Equal(state.Camera, game.CaptureCheckpoint().Camera); Assert.True(game.CaptureCheckpoint().Interaction);
        await Assert.ThrowsAsync<InvalidDataException>(() => p.SendAsync(Input(id, 4, new(true, GameButtons.Right))));
        Assert.Equal(18UL, game.TickNumber);
    }
    [Fact]
    public async Task UnsupportedInput_IsExplicitAndLegacyGameContinues()
    {
        var id = Identity; using var game = new ProbeGame(); var p = new RuntimeProtocol(id, game, 11); await Load(p, id);
        var reply = await p.SendAsync(Input(id, 3, new(true, GameButtons.Right, .2f, .3f, true)));
        Assert.Equal((int)GameInputOutcome.Unsupported, reply.InputOutcome); Assert.Equal(0UL, game.TickNumber);
        await p.SendAsync(ControlRequest.Create(id, 4, CommandKind.Step)); Assert.Equal(1UL, game.TickNumber); Assert.Equal(.125f, game.CaptureCheckpoint().Camera.X);
    }
    [Fact]
    public void ControlV2_RoundTripsInputFlagsLookActionAndOutcomeAndRejectsV1()
    {
        var id = Identity; id.Validate(); Assert.Equal(2, id.ControlVersion);
        Assert.Throws<InvalidDataException>(() => (id with { ControlVersion = 1 }).Validate());
        var request = Input(id, 17, new(true, GameButtons.Forward | GameButtons.Left, -.25f, .5f, true));
        var bytes = new ArrayBufferWriter<byte>(); request.Serialize(bytes); var offset = 0; var decoded = new ControlRequest().Deserialize(bytes.WrittenSpan, ref offset);
        Assert.Equal(bytes.WrittenCount, offset); Assert.True(bytes.WrittenCount < 4096); Assert.Equal(id, decoded.Identity());
        Assert.True(decoded.InputCaptured); Assert.Equal(5, decoded.InputButtons); Assert.Equal(-.25f, decoded.InputYaw); Assert.Equal(.5f, decoded.InputPitch); Assert.True(decoded.InputInteract);
        bytes.Clear(); new ControlReply { Stamp = request, Phase = "Paused", Tick = 37, InputOutcome = (int)GameInputOutcome.Accepted }.Serialize(bytes);
        offset = 0; var reply = new ControlReply().Deserialize(bytes.WrittenSpan, ref offset);
        Assert.Equal(bytes.WrittenCount, offset); Assert.Equal(37, reply.Tick); Assert.Equal((int)GameInputOutcome.Accepted, reply.InputOutcome);
        Assert.Throws<InvalidDataException>(() => SessionSupervisor.ValidateReply(id, 18, CommandKind.Hello, 11,
            new() { Stamp = ControlRequest.Create(id, 18, CommandKind.Hello), ProcessId = 11, InputOutcome = (int)GameInputOutcome.Accepted }));
    }
    [Fact]
    public async Task Supervisor_FencesInputQueuedDuringNewRuntimeStartupWithoutFaultingIt()
    {
        var factory = new Factory(); await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "unused", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        await supervisor.StartAsync(Scene); var old = supervisor.Status.Identity!; await supervisor.StopAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Before = async request => { if (request.Kind == (int)CommandKind.Hello) { entered.TrySetResult(); await release.Task; } };
        var starting = supervisor.StartAsync(Scene); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stale = supervisor.SendInputAsync(old, new(true, GameButtons.Forward));
        release.TrySetResult(); await starting; await Assert.ThrowsAsync<OperationCanceledException>(() => stale);
        Assert.Equal(SessionState.Paused, supervisor.Status.State); Assert.Equal(2UL, supervisor.Status.Identity!.Generation);
        Assert.DoesNotContain(CommandKind.SetInput, factory.Leases[1].Commands); Assert.False(factory.Leases[1].ResourcesReleased);
        var good = await supervisor.SendInputAsync(supervisor.Status.Identity!, new(true, GameButtons.Right)); Assert.Equal((int)GameInputOutcome.Accepted, good.InputOutcome);
        await supervisor.StepAsync(); Assert.Equal(.05f, factory.Leases[1].Game.CaptureCheckpoint().Camera.X, 5);
    }
    [Fact]
    public async Task Supervisor_InvalidLocalInputAndPreCancelledWaitDoNotTouchRuntime()
    {
        var factory = new Factory(); await using var s = new SessionSupervisor(factory, Hash, Hash, "unused", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        await s.StartAsync(Scene); var count = factory.Leases[0].Commands.Count;
        await Assert.ThrowsAsync<InvalidDataException>(() => s.SendInputAsync(s.Status.Identity!, new(true, Yaw: float.NaN)));
        using var end = new CancellationTokenSource(); end.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.SendInputAsync(s.Status.Identity!, new(true), end.Token));
        Assert.Equal(count, factory.Leases[0].Commands.Count); Assert.Equal(SessionState.Paused, s.Status.State);
    }
    [Theory]
    [InlineData("missing")] [InlineData("unknown")] [InlineData("mode")] [InlineData("tick")]
    public async Task Supervisor_RejectsMalformedInputAcknowledgementAndCleansOwnedRuntime(string invalid)
    {
        var factory = new Factory { Filter = reply => { if (reply.Stamp.Kind == (int)CommandKind.SetInput) { if (invalid == "missing") reply.InputOutcome = 0; if (invalid == "unknown") reply.InputOutcome = 999; if (invalid == "mode") reply.Phase = "Playing"; if (invalid == "tick") reply.Tick++; } return reply; } };
        await using var s = new SessionSupervisor(factory, Hash, Hash, "unused", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) }); await s.StartAsync(Scene);
        await Assert.ThrowsAsync<InvalidDataException>(() => s.SendInputAsync(s.Status.Identity!, new(true)));
        Assert.Equal(SessionState.Faulted, s.Status.State); Assert.True(factory.Leases[0].ResourcesReleased); Assert.Equal("SetInput", s.Status.Failure!.Phase);
    }
    [Fact]
    public async Task Stop_PreemptsPendingInputAndNoOldRequestReachesRestartedRuntime()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new Factory { Before = async request => { if (request.Kind == (int)CommandKind.SetInput) { entered.TrySetResult(); await release.Task; } } };
        await using var s = new SessionSupervisor(factory, Hash, Hash, "unused", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) }); await s.StartAsync(Scene);
        try
        {
            var pending = s.SendInputAsync(s.Status.Identity!, new(true, GameButtons.Forward)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await s.StopAsync().WaitAsync(TimeSpan.FromSeconds(3)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(SessionState.Stopped, s.Status.State); Assert.True(factory.Leases[0].ResourcesReleased);
            await s.StartAsync(Scene); Assert.Equal(2UL, s.Status.Identity!.Generation);
            Assert.DoesNotContain(CommandKind.SetInput, factory.Leases[1].Commands); Assert.Equal(0UL, factory.Leases[1].Game.TickNumber);
        }
        finally { release.TrySetResult(); }
    }
    private sealed class Factory : IRuntimeFactory
    {
        internal Func<ControlRequest, Task>? Before;
        internal Func<ControlReply, ControlReply>? Filter;
        internal List<Lease> Leases { get; } = [];
        public IRuntimeLease Create(SessionLaunch launch) { var lease = new Lease(this, launch); Leases.Add(lease); return lease; }
    }
    private sealed class Lease(Factory factory, SessionLaunch launch) : IRuntimeLease
    {
        internal CameraGame Game { get; } = new(); internal List<CommandKind> Commands { get; } = [];
        private RuntimeProtocol _protocol = null!; private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 101; public Task Exited => _exit.Task; public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token) { _protocol = new(launch.Identity, Game, ProcessId); return Task.CompletedTask; }
        public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        { Commands.Add((CommandKind)request.Kind); if (factory.Before != null) await factory.Before(request); var reply = await _protocol.SendAsync(request, token); return factory.Filter?.Invoke(reply) ?? reply; }
        public Task StopAsync(CancellationToken gracefulDeadline = default) { _exit.TrySetResult(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Game.Dispose(); ResourcesReleased = true; _exit.TrySetResult(); return ValueTask.CompletedTask; }
    }
}
