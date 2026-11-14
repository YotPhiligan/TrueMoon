using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Tests.Runtime;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;

public sealed class RequiredMeshPresentationTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    [Fact]
    public async Task FirstPresentation_AndLaterCameraChange_OpenOnlyExactMeshFence_ResizeRejectsOldTicket()
    {
        var runtime = new Factory(); var transport = new Transport(runtime);
        await using var supervisor = new SessionSupervisor(runtime, Hash, Hash, "mesh-ticket", new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        await using var frames = new FrameSession(supervisor, transport);
        var start = frames.StartAsync(new("mesh", 7, []), 320, 180); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(10));
        var ticket = frames.Presentation!.Issue(runtime.Lease!.Metadata()); Assert.False(start.IsCompleted);
        Assert.False((await supervisor.GetStatusAsync()).RenderViewReady);
        Assert.True(frames.Presented(ticket)); await start;
        Assert.True((await supervisor.GetStatusAsync()).RenderViewReady); Assert.Same(ticket, frames.Presentation.FirstPresentedFrame);
        await supervisor.SetPlayingAsync(true); runtime.Lease.Game.CameraRevision++;
        runtime.Lease.Protocol.FixedTick(); var paused = await supervisor.GetStatusAsync();
        Assert.Equal("Paused", paused.Phase); Assert.False(paused.RenderViewReady); Assert.Equal(1UL, supervisor.Status.Tick);
        runtime.Lease.Publish(frames.Presentation.Surface.Generation);
        var next = frames.Presentation.Issue(runtime.Lease.Metadata());
        Assert.True(await frames.PresentedAsync(next)); Assert.Equal(SessionState.Playing, supervisor.Status.State);
        await supervisor.SetPlayingAsync(false);
        var resize = frames.ResizeAsync(384, 216); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(await frames.PresentedAsync(next)); Assert.False(resize.IsCompleted);
        Assert.True(frames.Presented(frames.Presentation!.Issue(runtime.Lease.Metadata()))); await resize;
        Assert.True((await supervisor.GetStatusAsync()).RenderViewReady);
        await frames.StopAsync(); Assert.True(runtime.Lease.ResourcesReleased); Assert.True(transport.Disposed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.AcknowledgeRenderViewAsync(next.Metadata.RenderView!));
    }
    [Fact]
    public async Task MissingMeshStamp_FailsFirstPresentation_AndDrainsConnectionBeforeRuntimeStop()
    {
        var runtime = new Factory(); var transport = new Transport(runtime);
        await using var supervisor = new SessionSupervisor(runtime, Hash, Hash, "mesh-ticket"); await using var frames = new FrameSession(supervisor, transport);
        var start = frames.StartAsync(new("mesh", 7, []), 320, 180); await transport.Configured.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(frames.Presented(frames.Presentation!.Issue(new(0, 1, 0, 0, 1))));
        await Assert.ThrowsAsync<InvalidDataException>(() => start);
        Assert.True(transport.Disposed); Assert.True(runtime.Lease!.ResourcesReleased); Assert.Equal(0, frames.MappingCount);
        Assert.NotEqual(SessionState.Playing, supervisor.Status.State);
    }
    private sealed class Factory : IRuntimeFactory
    {
        internal Lease? Lease;
        public IRuntimeLease Create(SessionLaunch launch) => Lease = new(launch);
    }
    private sealed class Lease(SessionLaunch launch) : IRuntimeLease
    {
        internal readonly RequiredMeshProtocolTests.MeshGame Game = new();
        internal RuntimeProtocol Protocol = null!;
        private RenderSnapshotStamp _stamp = null!;
        private ulong _frame;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProcessId => 123;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token) { Protocol = new(launch.Identity, Game, ProcessId); return Task.CompletedTask; }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token) => Protocol.SendAsync(request, token);
        internal void Publish(ulong surface)
        {
            _frame++;
            _stamp = Game.Stamp() with { Tick = Game.TickNumber, SurfaceGeneration = surface };
            Protocol.SurfaceProduced(new([], Metadata()));
        }
        internal FrameMetadata Metadata() => new(0, _frame, Game.TickNumber, 0, 1) { RenderView = _stamp };
        public Task StopAsync(CancellationToken gracefulDeadline = default) { _exit.TrySetResult(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Protocol.Dispose(); Game.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
    private sealed class Transport(Factory runtime) : IGameViewportTransport, IGameViewportConnection
    {
        internal readonly SemaphoreSlim Configured = new(0); private readonly TaskCompletionSource _work = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Disposed;
        public ViewportTransportKind Kind => ViewportTransportKind.Gpu;
        public Task Completion => _work.Task;
        public Task<IGameViewportConnection> ConnectAsync(IRuntimeLease lease, CancellationToken token) => Task.FromResult<IGameViewportConnection>(this);
        public Task ConfigureAsync(ViewportPresentation presentation, CancellationToken token)
        { runtime.Lease!.Protocol.DetachRenderSurface(); runtime.Lease.Publish(presentation.Surface.Generation); Configured.Release(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; _work.TrySetCanceled(); return ValueTask.CompletedTask; }
    }
}
