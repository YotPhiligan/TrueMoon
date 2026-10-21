using Game.Logic;
using System.Collections.Concurrent;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Transport;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Tests.Replacements;

internal sealed class ReplacementRuntimeFixture : IRuntimeFactory
{
    internal readonly List<Lease> Leases = [];
    internal Action<Lease>? Configure;
    internal bool Produce;
    internal int MaxLive;
    public IRuntimeLease Create(SessionLaunch launch)
    {
        var lease = new Lease(this, launch); Leases.Add(lease); Configure?.Invoke(lease);
        MaxLive = Math.Max(MaxLive, Leases.Count(l => !l.ResourcesReleased)); return lease;
    }
    internal sealed class Lease(ReplacementRuntimeFixture owner, SessionLaunch launch) : IRuntimeLease
    {
        internal SessionLaunch Launch => launch;
        internal bool RejectCapture, RejectRestore, NoFrames, FailStop;
        internal Func<ControlRequest, CancellationToken, Task>? BeforeSend;
        internal Action<ControlRequest, ControlReply>? AfterSend;
        internal Func<Task>? BeforeStop;
        internal readonly ConcurrentQueue<CommandKind> Commands = [];
        internal RuntimeProtocol Protocol = null!;
        internal readonly Game Game = new();
        internal Task Worker = Task.CompletedTask;
        internal int Stops, Disposals;
        private readonly FrameProducer _producer = new();
        private readonly CancellationTokenSource _end = new();
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 700 + (int)launch.Identity.Generation;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Game.Lease = this;
            Protocol = new(launch.Identity, Game, ProcessId, s => _producer.Configure(s), requireRestore: launch.RequireRestore);
            if (owner.Produce && !NoFrames) Worker = _producer.RunAsync(Protocol, _end.Token);
            return Task.CompletedTask;
        }
        public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            Commands.Enqueue((CommandKind)request.Kind);
            if (BeforeSend != null) await BeforeSend(request, token);
            var reply = await Protocol.SendAsync(request, token); AfterSend?.Invoke(request, reply); return reply;
        }
        public async Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Stops++; if (BeforeStop != null) await BeforeStop();
            if (FailStop) throw new IOException("fixture reap failure");
            _end.Cancel(); try { await Worker; } catch (OperationCanceledException) { }
            _producer.Dispose(); _exit.TrySetResult();
        }
        public ValueTask DisposeAsync()
        { Disposals++; Game.Dispose(); _end.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
    internal sealed class Game : ICheckpointGameSession
    {
        internal Lease Lease = null!;
        private readonly ProbeGame _inner = new();
        public ulong TickNumber => _inner.TickNumber;
        public long Revision => _inner.Revision;
        public uint PatternColor => _inner.PatternColor;
        public void Load(SceneSnapshot scene) => _inner.Load(scene);
        public void Tick() => _inner.Tick();
        public GameCheckpointState CaptureCheckpoint() => Lease.RejectCapture ? throw new CheckpointRejectedException() : _inner.CaptureCheckpoint();
        public void RestoreCheckpoint(GameCheckpointState state)
        {
            if (Lease.RejectRestore) throw new CheckpointRejectedException(CheckpointOutcome.RestoreRejected);
            _inner.RestoreCheckpoint(state);
        }
        public void Dispose() => _inner.Dispose();
    }
}
