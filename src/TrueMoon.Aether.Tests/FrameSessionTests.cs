using TrueMoon.Aether;
using TrueMoon.Aether.Contracts;
using Game.Logic;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class FrameSessionTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SceneSnapshot Scene() => new("room", 7, []);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static SessionSupervisor Supervisor(Factory factory) => new(factory, Hash, Hash, "frame-fake",
        new() { HeartbeatInterval = TimeSpan.FromSeconds(30) });

    [Fact]
    public async Task StartAndResize_RequireSurfaceAckThenActualPresentationAndRejectOldDisplay()
    {
        var entered = Signal(); var ack = Signal();
        var factory = new Factory { BeforeSurface = async () => { entered.TrySetResult(); await ack.Task; } };
        await using var supervisor = Supervisor(factory); await using var frames = new FrameSession(supervisor);
        var start = frames.StartAsync(Scene(), 32, 16);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Null(frames.Display); Assert.False(start.IsCompleted);
        ack.SetResult(); var first = await ReadyAsync(frames);
        using var initial = first.Receiver.TakeLatest()!; Assert.NotNull(initial); Assert.False(start.IsCompleted);
        frames.Presented(first, initial.Metadata); await start;
        initial.Dispose();
        var resize = frames.ResizeAsync(64, 32); var next = await ReadyAsync(frames, first);
        frames.Presented(first, initial.Metadata); Assert.False(resize.IsCompleted);
        using (var image = next.Receiver.TakeLatest()) { Assert.NotNull(image); frames.Presented(next, image.Metadata); }
        await resize; Assert.Equal(2, frames.PeakMappings); Assert.Equal(1, frames.MappingCount);
        Assert.True(first.Receiver.Completion.IsCompleted); Assert.NotEqual(first.Surface.Mapping, next.Surface.Mapping);
        Assert.True(next.Surface.Generation > first.Surface.Generation); Assert.Equal(first.Surface.Identity, next.Surface.Identity);
        Assert.Throws<WaitHandleCannotBeOpenedException>(() => SharedFrameRing.Open(first.Surface));
        await frames.StopAsync(); Assert.Equal(0, frames.MappingCount); Assert.True(factory.Leases.All(l => l.ResourcesReleased));
    }
    [Fact]
    public async Task ZeroSize_SuspendsReleasesMappingsKeepsHeartbeatAndResumesFreshSurface()
    {
        var factory = new Factory(); await using var supervisor = Supervisor(factory); await using var frames = new FrameSession(supervisor);
        var starting = frames.StartAsync(Scene(), 32, 16); var first = await PresentAsync(frames); await starting;
        await frames.ResizeAsync(0, 0); Assert.Null(frames.Display); Assert.True(frames.Surface!.Suspended); Assert.Equal(0, frames.MappingCount);
        Assert.Equal("Paused", (await supervisor.GetStatusAsync()).Phase);
        var resume = frames.ResizeAsync(64, 32); var next = await PresentAsync(frames, first); await resume;
        Assert.True(next.Surface.Generation > first.Surface.Generation); Assert.Equal(first.Surface.Identity, next.Surface.Identity);
        await frames.StopAsync(); Assert.Equal(0, frames.MappingCount);
    }
    [Fact]
    public async Task Stop_CancelsMissingFirstFrameDrainsAndAllowsFreshChild()
    {
        var factory = new Factory { Produce = false }; await using var supervisor = Supervisor(factory); await using var frames = new FrameSession(supervisor);
        var start = frames.StartAsync(Scene(), 32, 16); await UntilAsync(() => frames.Display != null);
        var queued = frames.StartAsync(Scene(), 64, 32);
        await frames.StopAsync().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued); Assert.Single(factory.Leases);
        Assert.Equal(SessionState.Stopped, supervisor.Status.State); Assert.Equal(0, frames.MappingCount);
        var firstIdentity = supervisor.Status.Identity; factory.Produce = true;
        var second = frames.StartAsync(Scene(), 32, 16); await PresentAsync(frames); await second;
        Assert.True(supervisor.Status.Identity!.Generation > firstIdentity!.Generation);
        await frames.StopAsync(); Assert.All(factory.Leases, lease => Assert.True(lease.ResourcesReleased));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingSurfaceAck_FaultsAndRetainsMappingOnlyUntilCleanupRetry(bool cleanupFails)
    {
        var factory = new Factory { WrongAck = true, StopFailures = cleanupFails ? 1 : 0 };
        await using var supervisor = Supervisor(factory); await using var frames = new FrameSession(supervisor);
        await Assert.ThrowsAsync<InvalidDataException>(() => frames.StartAsync(Scene(), 32, 16));
        Assert.Equal(cleanupFails ? SessionState.CleanupFailed : SessionState.Faulted, supervisor.Status.State);
        Assert.Equal(cleanupFails ? 1 : 0, frames.MappingCount); Assert.Null(frames.Display);
        Assert.Equal("ConfigureSurface", supervisor.Status.Failure!.Phase);
        await frames.StopAsync(); Assert.Equal(SessionState.Stopped, supervisor.Status.State); Assert.Equal(0, frames.MappingCount);
    }
    [Fact]
    public async Task Producer_EofDetachesOldMappingAndFrameIdsIncreaseAcrossSurfaces()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, Hash, Hash, Hash);
        using var game = new ProbeGame(); using var producer = new FrameProducer();
        var protocol = new RuntimeProtocol(identity, game, 123, producer.Configure); using var cancel = new CancellationTokenSource();
        var worker = producer.RunAsync(protocol, cancel.Token);
        var s1 = FrameSurface.Create(identity, 1, 32, 16); var s2 = FrameSurface.Create(identity, 2, 32, 16);
        using var ring1 = SharedFrameRing.Create(s1); using var ring2 = SharedFrameRing.Create(s2);
        producer.Configure(s1); var bytes = new byte[s1.PixelBytes]; FrameMetadata? first = null;
        await UntilAsync(() => (first = ring1.CopyLatest(bytes, 0)) != null);
        producer.Configure(s2); Assert.Throws<EndOfStreamException>(() => ring1.CopyLatest(bytes, first!.FrameId));
        FrameMetadata? second = null; await UntilAsync(() => (second = ring2.CopyLatest(bytes, first!.FrameId)) != null);
        Assert.True(second!.FrameId > first!.FrameId);
        Assert.Throws<InvalidDataException>(() => producer.Configure(s1));
        cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker);
    }
    private static async Task<FrameDisplay> ReadyAsync(FrameSession session, FrameDisplay? old = null)
    {
        await UntilAsync(() => session.Display is { } d && d != old && d.Receiver.FirstFrame.IsCompletedSuccessfully);
        return session.Display!;
    }
    private static async Task<FrameDisplay> PresentAsync(FrameSession session, FrameDisplay? old = null)
    {
        var display = await ReadyAsync(session, old);
        using var frame = display.Receiver.TakeLatest()!; Assert.NotNull(frame); session.Presented(display, frame.Metadata); return display;
    }
    private static async Task UntilAsync(Func<bool> ready)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!ready()) await Task.Delay(5, limit.Token);
    }
    private sealed class Factory : IRuntimeFactory
    {
        public bool Produce = true, WrongAck;
        public int StopFailures;
        public Func<Task>? BeforeSurface;
        public List<Lease> Leases = [];
        public IRuntimeLease Create(SessionLaunch launch)
        { var lease = new Lease(launch.Identity, this); Leases.Add(lease); return lease; }
    }
    private sealed class Lease(SessionIdentity identity, Factory factory) : IRuntimeLease
    {
        private readonly ProbeGame _game = new(); private readonly FrameProducer _producer = new();
        private readonly CancellationTokenSource _end = new(); private readonly TaskCompletionSource _exit = Signal();
        private RuntimeProtocol? _protocol; private Task _worker = Task.CompletedTask;
        public int ProcessId => 101;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            _protocol = new(identity, _game, ProcessId, _producer.Configure);
            if (factory.Produce) _worker = _producer.RunAsync(_protocol, _end.Token);
            return Task.CompletedTask;
        }
        public async Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            if (request.Kind == (int)CommandKind.ConfigureSurface && factory.BeforeSurface != null) await factory.BeforeSurface();
            var reply = await _protocol!.SendAsync(request, token);
            if (request.Kind == (int)CommandKind.ConfigureSurface && factory.WrongAck) reply.SurfaceGeneration++;
            return reply;
        }
        public async Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            if (factory.StopFailures-- > 0) throw new IOException("Frame fixture reap failure.");
            _end.Cancel(); try { await _worker; } catch (OperationCanceledException) { }
            _producer.Dispose(); _exit.TrySetResult();
        }
        public ValueTask DisposeAsync() { ResourcesReleased = true; _end.Dispose(); _game.Dispose(); return ValueTask.CompletedTask; }
    }
}
