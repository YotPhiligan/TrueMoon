using TrueMoon.Aether;
using System.Buffers;
using TrueMoon.Aether.Contracts;
using Game.Logic;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Enerit.IO;
using TrueMoon.Generated.TrueMoon.Aether;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class SessionSupervisorTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static SceneSnapshot Scene() => new("room", 7, [new() { Id = "switch", X = 1, Interactive = true }]);
    private static SessionSupervisor Create(Factory factory, SessionDeadlines? deadlines = null, Action<SessionStatus>? trace = null)
        => new(factory, Hash, Hash, "not-used-by-fakes", deadlines ?? new() { HeartbeatInterval = TimeSpan.FromSeconds(30) }, trace);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Start_WaitsForHelloAndAppliedSceneBeforeReady()
    {
        var helloEntered = Signal(); var hello = Signal(); var sceneEntered = Signal(); var scene = Signal();
        var factory = new Factory { Configure = lease => lease.BeforeSend = async request =>
        {
            if (request.Kind == (int)CommandKind.Hello) { helloEntered.SetResult(); await hello.Task; }
            if (request.Kind == (int)CommandKind.LoadScene) { sceneEntered.SetResult(); await scene.Task; }
        } };
        await using var supervisor = Create(factory);
        var starting = supervisor.StartAsync(Scene());
        await helloEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(SessionState.Starting, supervisor.Status.State);
        Assert.Equal(101, supervisor.Status.ProcessId); Assert.False(supervisor.Status.HelloAccepted);
        hello.SetResult(); await sceneEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(supervisor.Status.HelloAccepted); Assert.False(supervisor.Status.SceneLoaded);
        scene.SetResult(); await starting;
        Assert.Equal(SessionState.Paused, supervisor.Status.State); Assert.True(supervisor.Status.SceneLoaded);
        Assert.Equal(7, supervisor.Status.SceneRevision); Assert.Equal(0UL, supervisor.Status.Tick);
        Assert.Equal(new[] { CommandKind.Hello, CommandKind.LoadScene, CommandKind.SetMode }, factory.Leases[0].Commands);
        await supervisor.StopAsync(); Assert.True(factory.Leases[0].ResourcesReleased);
        Assert.Equal(new[] { "Stop", "Dispose" }, factory.Leases[0].Cleanup);
    }

    [Fact]
    public async Task RepeatedStartStop_UsesFreshLeasesGenerationsAndNonces()
    {
        var factory = new Factory(); await using var supervisor = Create(factory);
        await supervisor.StartAsync(Scene()); var first = supervisor.Status.Identity!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartAsync(Scene()));
        Assert.Single(factory.Leases); Assert.Equal(SessionState.Paused, supervisor.Status.State);
        await supervisor.StopAsync(); await supervisor.StartAsync(Scene()); var second = supervisor.Status.Identity!;
        Assert.Equal(first.SessionId, second.SessionId); Assert.Equal(1UL, first.Generation); Assert.Equal(2UL, second.Generation);
        Assert.NotEqual(first.Nonce, second.Nonce); Assert.NotEqual(factory.Launches[0].Endpoint, factory.Launches[1].Endpoint);
        Assert.True(factory.Leases[0].ResourcesReleased); Assert.False(factory.Leases[1].ResourcesReleased);
    }

    [Theory]
    [InlineData("generation")][InlineData("session")][InlineData("host")][InlineData("game")]
    [InlineData("nonce")][InlineData("control")][InlineData("frame")][InlineData("snapshot")]
    [InlineData("sequence")][InlineData("operation")][InlineData("pid")]
    public async Task MismatchedHello_IsRejectedBeforeSceneAndConsumesGeneration(string field)
    {
        var factory = new Factory { Configure = lease => lease.Filter = (request, reply) =>
        {
            switch (field)
            {
                case "generation": reply.Stamp.Generation++; break;
                case "session": reply.Stamp.SessionId = Guid.NewGuid().ToString(); break;
                case "host": reply.Stamp.HostContractId = new string('B', 64); break;
                case "game": reply.Stamp.GameBuildId = new string('B', 64); break;
                case "nonce": reply.Stamp.Nonce = new string('B', 64); break;
                case "control": reply.Stamp.ControlVersion++; break;
                case "frame": reply.Stamp.FrameVersion++; break;
                case "snapshot": reply.Stamp.SnapshotVersion++; break;
                case "sequence": reply.Stamp.Sequence++; break;
                case "operation": reply.Stamp.Kind++; break;
                case "pid": reply.ProcessId++; break;
            }
            return reply;
        } };
        await using var supervisor = Create(factory);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartAsync(Scene()));
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Equal("Hello", supervisor.Status.Failure!.Phase);
        Assert.Same(error, supervisor.Status.Failure.Cause); Assert.False(supervisor.Status.SceneLoaded);
        Assert.Equal(new[] { CommandKind.Hello }, factory.Leases[0].Commands); Assert.True(factory.Leases[0].ResourcesReleased);
        factory.Configure = null; await supervisor.StartAsync(Scene()); Assert.Equal(2UL, supervisor.Status.Identity!.Generation);
    }

    [Fact]
    public async Task CleanupFailure_PreservesPrimaryErrorAndBlocksStartUntilExplicitRetry()
    {
        var factory = new Factory { Configure = lease =>
        {
            lease.StopFailures = 1;
            lease.Filter = (_, reply) => { reply.Stamp.Generation--; return reply; };
        } };
        await using var supervisor = Create(factory);
        var primary = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartAsync(Scene()));
        Assert.Equal(SessionState.CleanupFailed, supervisor.Status.State); Assert.Same(primary, supervisor.Status.Failure!.Cause);
        Assert.IsType<IOException>(supervisor.Status.Failure.CleanupError); Assert.False(factory.Leases[0].ResourcesReleased);
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartAsync(Scene())); Assert.Single(factory.Leases);
        await supervisor.StopAsync(); Assert.True(factory.Leases[0].ResourcesReleased);
        factory.Configure = null; await supervisor.StartAsync(Scene()); Assert.Equal(2UL, supervisor.Status.Identity!.Generation);
    }

    [Fact]
    public async Task Stop_FencesPendingAndQueuedCommandsAndDiscardsLateReply()
    {
        var entered = Signal(); var release = Signal();
        var factory = new Factory { Configure = lease => lease.BeforeSend = async request =>
        { if (request.Kind == (int)CommandKind.GetStatus) { entered.SetResult(); await release.Task; } } };
        await using var supervisor = Create(factory); await supervisor.StartAsync(Scene());
        var oldIdentity = supervisor.Status.Identity;
        var pending = supervisor.GetStatusAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var queued = supervisor.StepAsync(); var stopping = supervisor.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued); await stopping;
        Assert.DoesNotContain(CommandKind.Step, factory.Leases[0].Commands); Assert.True(factory.Leases[0].ResourcesReleased);
        factory.Configure = null; await supervisor.StartAsync(Scene());
        var current = supervisor.Status; release.SetResult();
        await factory.Leases[0].LastSend!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(current, supervisor.Status); Assert.NotEqual(oldIdentity!.Generation, current.Identity!.Generation);
    }

    [Fact]
    public async Task StartupCancellation_CleansWithoutPublishingReadyAndAllowsNextGeneration()
    {
        var entered = Signal(); var release = Signal(); using var cancellation = new CancellationTokenSource();
        var factory = new Factory { Configure = lease => lease.BeforeSend = async _ => { entered.TrySetResult(); await release.Task; } };
        await using var supervisor = Create(factory); var start = supervisor.StartAsync(Scene(), token: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.False(supervisor.Status.SceneLoaded);
        Assert.True(factory.Leases[0].ResourcesReleased);
        factory.Configure = null; await supervisor.StartAsync(Scene());
        release.SetResult(); await factory.Leases[0].LastSend!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2UL, supervisor.Status.Identity!.Generation); Assert.Equal(SessionState.Paused, supervisor.Status.State);
    }

    [Fact]
    public async Task LaunchFailure_IsReportedWithOriginalCauseAndCleanup()
    {
        var original = new IOException("launch fixture");
        var factory = new Factory { Configure = lease => lease.LaunchError = original };
        await using var supervisor = Create(factory);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => supervisor.StartAsync(Scene())));
        Assert.Equal("Launch", supervisor.Status.Failure!.Phase); Assert.Null(supervisor.Status.ProcessId);
        Assert.Empty(factory.Leases[0].Commands); Assert.True(factory.Leases[0].ResourcesReleased);
    }

    [Fact]
    public async Task UnexpectedExit_FaultsAndCleansWithoutCallerPolling()
    {
        var factory = new Factory(); await using var supervisor = Create(factory); await supervisor.StartAsync(Scene());
        factory.Leases[0].Exit.TrySetResult();
        await TestWait.UntilAsync(() => factory.Leases[0].ResourcesReleased, TimeSpan.FromSeconds(3));
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Equal("ProcessExit", supervisor.Status.Failure!.Phase);
        Assert.IsType<IOException>(supervisor.Status.Failure.Cause);
        await supervisor.StartAsync(Scene()); Assert.Equal(2UL, supervisor.Status.Identity!.Generation);
    }

    [Fact]
    public async Task ModeAndStep_ApplyOnAcknowledgementAndRejectStepWhilePlayingLocally()
    {
        var factory = new Factory(); await using var supervisor = Create(factory); await supervisor.StartAsync(Scene());
        await supervisor.SetPlayingAsync(true); Assert.Equal(SessionState.Playing, supervisor.Status.State);
        var count = factory.Leases[0].Commands.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StepAsync());
        Assert.Equal(count, factory.Leases[0].Commands.Count); Assert.Null(supervisor.Status.Failure);
        await supervisor.SetPlayingAsync(false); var step = await supervisor.StepAsync();
        Assert.Equal(1, step.Tick); Assert.Equal(1UL, supervisor.Status.Tick);
        Assert.Equal("Paused", (await supervisor.GetStatusAsync()).Phase);
    }

    [Theory]
    [InlineData("mode")][InlineData("tick")][InlineData("revision")]
    public async Task InvalidApplicationAcknowledgement_FaultsInsteadOfCommittingState(string field)
    {
        var factory = new Factory(); await using var supervisor = Create(factory); await supervisor.StartAsync(Scene());
        factory.Leases[0].Filter = (_, reply) =>
        {
            if (field == "mode") reply.Phase = "Paused";
            if (field == "tick") reply.Tick++;
            if (field == "revision") reply.Revision++;
            return reply;
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => field == "mode" ? supervisor.SetPlayingAsync(true) : field == "tick" ? supervisor.StepAsync() : supervisor.GetStatusAsync());
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Equal(0UL, supervisor.Status.Tick);
        Assert.True(factory.Leases[0].ResourcesReleased);
    }

    [Fact]
    public async Task ThrowingDiagnosticSink_DoesNotPreventReadinessOrCleanupAndDisposeIsRepeatable()
    {
        var factory = new Factory(); var supervisor = Create(factory, trace: _ => throw new IOException("sink"));
        await supervisor.StartAsync(Scene()); await supervisor.DisposeAsync(); await supervisor.DisposeAsync(); await supervisor.StopAsync();
        Assert.True(factory.Leases.Single().ResourcesReleased); Assert.Equal(SessionState.Stopped, supervisor.Status.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => supervisor.StartAsync(Scene()));
    }

    [Fact]
    public async Task GeneratedWire_RoundTripsAllUnsignedBitsThroughBoundedTransport()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), ulong.MaxValue, Hash, Hash, Hash);
        var request = ControlRequest.Create(identity, ulong.MaxValue, CommandKind.GetStatus);
        var inner = new RecordingClient { Response = new ControlReply { Stamp = request, Tick = -1, ProcessId = 123, Phase = "Playing" } };
        var service = new EngineControlGeneratedImplementation(new BoundedControlClient(inner));
        var reply = await service.SendAsync(request);
        SessionSupervisor.ValidateReply(identity, ulong.MaxValue, CommandKind.GetStatus, 123, reply);
        Assert.Equal(ulong.MaxValue, unchecked((ulong)reply.Tick)); Assert.True(inner.LastPayloadLength > 0);
        var decoded = inner.LastRequest!; Assert.Equal(identity, decoded.Identity()); Assert.Equal(-1, decoded.Sequence);
    }

    [Theory]
    [InlineData(0)][InlineData(65536)]
    public void PayloadLimit_AcceptsExactBoundaries(int length) => BoundedControlClient.ValidateLength(length);
    [Theory]
    [InlineData(-1)][InlineData(65537)]
    public void PayloadLimit_RejectsInvalidBoundaries(int length)
        => Assert.Throws<InvalidDataException>(() => BoundedControlClient.ValidateLength(length));

    [Fact]
    public async Task OversizedGeneratedPayload_IsRejectedBeforeTransportInvocation()
    {
        var inner = new RecordingClient(); var service = new EngineControlGeneratedImplementation(new BoundedControlClient(inner));
        var request = ControlRequest.Create(new(Guid.NewGuid(), 1, Hash, Hash, Hash), 1, CommandKind.Hello);
        request.SceneId = new string('x', 65536);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.SendAsync(request)); Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task RuntimeProtocol_RejectsOrderAndSceneErrorsBeforeMutation()
    {
        var identity = new SessionIdentity(Guid.NewGuid(), 1, Hash, Hash, Hash); using var game = new ProbeGame();
        var protocol = new RuntimeProtocol(identity, game, 123);
        await Assert.ThrowsAsync<InvalidOperationException>(() => protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Step)));
        await protocol.SendAsync(ControlRequest.Create(identity, 1, CommandKind.Hello));
        var load = ControlRequest.Create(identity, 2, CommandKind.LoadScene); load.SceneId = "room"; load.Revision = 7;
        load.Entities = [new() { Id = "same" }, new() { Id = "same" }];
        await Assert.ThrowsAsync<ArgumentException>(() => protocol.SendAsync(load)); Assert.Equal(-1, game.Revision);
        load.Entities = [new() { Id = "different" }]; await protocol.SendAsync(load); Assert.Equal(7, game.Revision);
        await Assert.ThrowsAsync<InvalidDataException>(() => protocol.SendAsync(ControlRequest.Create(identity, 4, CommandKind.Step)));
        Assert.Equal(0UL, game.TickNumber);
        await protocol.SendAsync(ControlRequest.Create(identity, 3, CommandKind.Step)); Assert.Equal(1UL, game.TickNumber);
    }

    [Fact]
    public void SceneSnapshot_EnforcesLimitsAndOwnsCopies()
    {
        var entities = Enumerable.Range(0, 64).Select(i => new SceneEntity { Id = i.ToString(), X = i }).ToList();
        var snapshot = new SceneSnapshot("room", 7, entities); entities[0].X = 1000; snapshot.CopyEntities()[1].X = 2000;
        Assert.Equal(0, snapshot.CopyEntities()[0].X); Assert.Equal(1, snapshot.CopyEntities()[1].X);
        entities.Add(new() { Id = "extra" }); Assert.Throws<ArgumentException>(() => new SceneSnapshot("room", 7, entities));
        Assert.Throws<ArgumentException>(() => new SceneSnapshot("room", 7, [new() { Id = "bad", X = float.NaN }]));
        Assert.Empty(new SceneSnapshot("empty", 0, []).CopyEntities());
    }

    private sealed class Factory : IRuntimeFactory
    {
        public Action<Lease>? Configure;
        public List<Lease> Leases { get; } = [];
        public List<SessionLaunch> Launches { get; } = [];
        public IRuntimeLease Create(SessionLaunch launch)
        {
            Launches.Add(launch); var lease = new Lease(launch, Leases.Count + 101); Configure?.Invoke(lease); Leases.Add(lease); return lease;
        }
    }
    private sealed class Lease(SessionLaunch launch, int pid) : IRuntimeLease
    {
        private readonly ProbeGame _game = new();
        private RuntimeProtocol? _protocol;
        public Func<ControlRequest, Task>? BeforeSend;
        public Func<ControlRequest, ControlReply, ControlReply>? Filter;
        public Exception? LaunchError;
        public int StopFailures;
        public List<CommandKind> Commands { get; } = [];
        public List<string> Cleanup { get; } = [];
        public TaskCompletionSource Exit { get; } = Signal();
        public Task? LastSend;
        public int ProcessId => pid;
        public Task Exited => Exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        {
            if (LaunchError != null) throw LaunchError;
            _protocol = new(launch.Identity, _game, pid); return Task.CompletedTask;
        }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        {
            var task = SendCoreAsync(request); LastSend = task; return task;
        }
        private async Task<ControlReply> SendCoreAsync(ControlRequest request)
        {
            Commands.Add((CommandKind)request.Kind);
            if (BeforeSend != null) await BeforeSend(request);
            // Deliberately ignores cancellation: verifies supervisor fencing even for late replies.
            var reply = await _protocol!.SendAsync(request);
            return Filter?.Invoke(request, reply) ?? reply;
        }
        public Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Cleanup.Add("Stop");
            if (StopFailures-- > 0) throw new IOException("reap fixture");
            Exit.TrySetResult(); return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() { Cleanup.Add("Dispose"); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
    private sealed class RecordingClient : IInvocationClient<IEngineControl>
    {
        public ControlReply Response = new();
        public int Calls, LastPayloadLength;
        public ControlRequest? LastRequest;
        public Task<TResult> InvokeAsync<TResult>(byte methodCode, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func, CancellationToken cancellationToken = default)
        {
            Calls++; var writer = new ArrayBufferWriter<byte>(); action?.Invoke(writer); LastPayloadLength = writer.WrittenCount;
            var offset = 0; LastRequest = new ControlRequest().Deserialize(writer.WrittenSpan, ref offset);
            var reply = new ArrayBufferWriter<byte>(); Response.Serialize(reply); return Task.FromResult(func(reply.WrittenMemory));
        }
        public Task InvokeAsync(byte code, Action<IBufferWriter<byte>>? action, CancellationToken token = default) => throw new NotSupportedException();
        public TResult Invoke<TResult>(byte code, Action<IBufferWriter<byte>>? action, Func<ReadOnlyMemory<byte>, TResult> func) => throw new NotSupportedException();
        public void Invoke(byte code, Action<IBufferWriter<byte>>? action) => throw new NotSupportedException();
    }
}
