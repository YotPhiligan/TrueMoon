using Game.Logic;
using System.Collections.Concurrent;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Tests.Checkpoints;

internal sealed class CheckpointRuntimeFixture : IRuntimeFactory
{
    internal string Capture = "ok", Restore = "ok";
    internal bool Produce;
    internal Func<ControlRequest, CancellationToken, Task>? BeforeSend;
    internal Func<ControlRequest, ControlReply, ControlReply>? Filter;
    internal readonly List<Lease> Leases = [];
    public IRuntimeLease Create(SessionLaunch launch) { var lease = new Lease(this, launch); Leases.Add(lease); return lease; }
    internal SessionSupervisor Supervisor(SessionDeadlines? deadlines = null) => new(this, CheckpointCodecTests.Hash, CheckpointCodecTests.Hash,
        "checkpoint-tests", deadlines ?? new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
    internal sealed class Lease(CheckpointRuntimeFixture owner, SessionLaunch launch) : IRuntimeLease
    {
        internal readonly GameState Game = new(owner);
        internal readonly ConcurrentQueue<CommandKind> Commands = [];
        internal RuntimeProtocol Protocol = null!;
        internal Task Worker = Task.CompletedTask;
        internal int Configures, Stops;
        private readonly FrameProducer _producer = new();
        private readonly CancellationTokenSource _end = new();
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 301;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            IGameSession game = owner.Capture == "unsupported" ? new PlainGame(Game) : Game;
            Protocol = new(launch.Identity, game, ProcessId, surface => { Configures++; _producer.Configure(surface); }, requireRestore: launch.RequireRestore);
            if (owner.Produce) Worker = _producer.RunAsync(Protocol, _end.Token);
            return Task.CompletedTask;
        }
        public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            Commands.Enqueue((CommandKind)request.Kind);
            if (owner.BeforeSend != null) await owner.BeforeSend(request, token);
            var reply = await Protocol.SendAsync(request, token); return owner.Filter?.Invoke(request, reply) ?? reply;
        }
        public async Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Stops++; _end.Cancel(); try { await Worker; } catch (OperationCanceledException) { }
            _producer.Dispose(); _exit.TrySetResult();
        }
        public ValueTask DisposeAsync() { Game.Dispose(); _end.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
    internal sealed class GameState(CheckpointRuntimeFixture owner) : ICheckpointGameSession
    {
        private readonly ProbeGame _game = new();
        public ulong TickNumber => _game.TickNumber; public long Revision => _game.Revision; public uint PatternColor => _game.PatternColor;
        public void Load(SceneSnapshot scene) => _game.Load(scene);
        public void Tick() => _game.Tick();
        public GameCheckpointState CaptureCheckpoint()
        {
            if (owner.Capture == "rejected") throw new CheckpointRejectedException();
            if (owner.Capture == "mutates") { _game.Tick(); throw new CheckpointRejectedException(); }
            if (owner.Capture == "invalid") return _game.CaptureCheckpoint() with { Camera = new(float.NaN, 0, 0, 0, 0, 0) };
            return _game.CaptureCheckpoint();
        }
        public void RestoreCheckpoint(GameCheckpointState state)
        {
            if (owner.Restore == "rejected") throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
            if (owner.Restore == "no-apply") return;
            if (owner.Restore == "partial") { _game.RestoreCheckpoint(state with { Camera = default, Interaction = false }); return; }
            _game.RestoreCheckpoint(state);
        }
        public void Dispose() => _game.Dispose();
    }
    private sealed class PlainGame(IGameSession inner) : IGameSession
    {
        public ulong TickNumber => inner.TickNumber; public long Revision => inner.Revision; public uint PatternColor => inner.PatternColor;
        public void Load(SceneSnapshot scene) => inner.Load(scene); public void Tick() => inner.Tick(); public void Dispose() => inner.Dispose();
    }
}
