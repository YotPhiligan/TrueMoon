using System.Security.Cryptography;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneWorldSupervisorTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly ComponentTypeId TypeId = new(Id(1));
    private static readonly ComponentId ComponentId = new(Id(2));
    private static readonly EntityDefinitionId DefinitionId = new(Id(3));
    private static readonly ComponentSchema Schema = new(TypeId, 1,
        [new("value", PropertyValueKind.Integer, PropertyValue.From(3L)), new("note", PropertyValueKind.String, PropertyValue.From("default"))]);

    [Fact]
    public async Task StartWorld_WaitsForActivationThenTransfersExactFullPayloadAndPublishesInitialCounts()
    {
        var entered = Signal(); var release = Signal();
        var factory = new RuntimeFactory { Configure = lease => lease.BeforeSend = async request =>
        { if (request.Kind == (int)CommandKind.ActivateSceneWorld) { entered.SetResult(); await release.Task; } } };
        await using var supervisor = Create(factory);
        var data = Document(100, large: true); var bytes = Codec().Encode(data);
        Assert.True(bytes.Length > BoundedControlClient.PayloadLimit);
        var starting = supervisor.StartWorldAsync(data);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(SessionState.Starting, supervisor.Status.State); Assert.True(supervisor.Status.HelloAccepted);
        Assert.False(supervisor.Status.SceneLoaded); Assert.Null(supervisor.Status.SceneWorldId);
        Assert.Null(factory.Leases[0].Game.ActiveScene);
        release.SetResult(); await starting;

        var lease = Assert.Single(factory.Leases); var status = supervisor.Status;
        Assert.Equal((SessionState.Paused, true, 17L, 0UL), (status.State, status.SceneLoaded, status.SceneRevision, status.Tick));
        Assert.Equal(lease.Game.ActiveScene!.World.Id, status.SceneWorldId);
        Assert.Equal(lease.Game.RegistryId, status.SceneRegistryId);
        Assert.Equal((100, 100), (status.SceneEntityCount, status.SceneComponentCount));
        Assert.Null(supervisor.Configuration);
        var begin = Assert.Single(lease.Requests, request => request.Kind == (int)CommandKind.BeginSceneWorld);
        Assert.Equal((bytes.Length, data.Scene.Id.Value.ToString("D"), 17L, lease.Game.RegistryId.ToString("D")),
            (begin.SceneBytes, begin.SceneId, begin.Revision, begin.SceneRegistryId));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), begin.SceneDigest);
        var chunks = lease.Requests.Where(request => request.Kind == (int)CommandKind.AppendSceneWorld).ToArray();
        Assert.Equal((bytes.Length + 32767) / 32768, chunks.Length);
        Assert.Equal(bytes, chunks.SelectMany(request => request.SceneChunk!).ToArray());
        var offset = 0;
        foreach (var chunk in chunks)
        {
            Assert.Equal((begin.SceneLoadId, begin.SceneRegistryId, offset), (chunk.SceneLoadId, chunk.SceneRegistryId, chunk.SceneOffset));
            Assert.Equal(Math.Min(32768, bytes.Length - offset), chunk.SceneChunk!.Length); offset += chunk.SceneChunk.Length;
        }
        Assert.Equal(new[] { CommandKind.Hello, CommandKind.BeginSceneWorld }, lease.Commands.Take(2));
        Assert.Equal(new[] { CommandKind.ActivateSceneWorld, CommandKind.SetMode }, lease.Commands.TakeLast(2));
        Assert.DoesNotContain(CommandKind.LoadScene, lease.Commands);
        var loaded = lease.Game.ActiveScene;
        var entity = loaded.Entities[new EntityId(Id(100 + 99))];
        Assert.Equal(new Value(102, 1024), loaded.World.GetComponent<Value>(entity));
        Assert.Equal("Entity 99", loaded.Scene.Entities[new EntityId(Id(199))].Name);
        var stepped = await supervisor.StepAsync();
        Assert.Equal((1L, 1UL), (stepped.Tick, supervisor.Status.Tick));
        Assert.Equal(103, loaded.World.GetComponent<Value>(entity).Number);
        await supervisor.StopAsync();
        Assert.True(loaded.IsDisposed); Assert.True(lease.ResourcesReleased);
        Assert.Equal(new[] { "Stop", "Dispose" }, lease.Cleanup);
    }

    [Fact]
    public async Task RepeatedWorldStartStop_UsesFreshWorldRegistryOperationGenerationAndNonce()
    {
        var factory = new RuntimeFactory(); await using var supervisor = Create(factory);
        await supervisor.StartWorldAsync(Document(0)); var first = supervisor.Status;
        Assert.Equal((0, 0), (first.SceneEntityCount, first.SceneComponentCount));
        Assert.NotEqual(Guid.Empty, first.SceneWorldId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartWorldAsync(Document(1)));
        Assert.Single(factory.Leases);
        await supervisor.StopAsync(); await supervisor.StartWorldAsync(Document(1)); var second = supervisor.Status;
        Assert.Equal(first.Identity!.SessionId, second.Identity!.SessionId);
        Assert.Equal((1UL, 2UL), (first.Identity.Generation, second.Identity.Generation));
        Assert.NotEqual(first.Identity.Nonce, second.Identity.Nonce);
        Assert.NotEqual(first.SceneWorldId, second.SceneWorldId); Assert.NotEqual(first.SceneRegistryId, second.SceneRegistryId);
        Assert.NotEqual(factory.Leases[0].Requests[1].SceneLoadId, factory.Leases[1].Requests[1].SceneLoadId);
        Assert.True(factory.Leases[0].Game.ActiveScene is null or { IsDisposed: true });
        Assert.True(factory.Leases[0].ResourcesReleased); Assert.False(factory.Leases[1].ResourcesReleased);
    }

    [Theory]
    [InlineData("version-zero")][InlineData("version-future")][InlineData("registry-empty")]
    [InlineData("registry-whitespace")][InlineData("registry-format")][InlineData("max-smaller")]
    [InlineData("max-larger")][InlineData("chunk-smaller")][InlineData("chunk-larger")]
    [InlineData("operation")][InlineData("offset")][InlineData("world")][InlineData("entities")][InlineData("components")]
    public async Task InvalidCapabilityOrUsedHello_IsRejectedWithoutBeginOrPrimitiveFallback(string field)
    {
        var factory = new RuntimeFactory { Configure = lease => lease.Filter = (request, reply) =>
        {
            if (request.Kind != (int)CommandKind.Hello) return reply;
            switch (field)
            {
                case "version-zero": reply.SceneWorldVersion = 0; break;
                case "version-future": reply.SceneWorldVersion = 2; break;
                case "registry-empty": reply.SceneRegistryId = ""; break;
                case "registry-whitespace": reply.SceneRegistryId = " " + reply.SceneRegistryId; break;
                case "registry-format": reply.SceneRegistryId = Guid.Parse(reply.SceneRegistryId).ToString("N"); break;
                case "max-smaller": reply.SceneMaximumBytes--; break;
                case "max-larger": reply.SceneMaximumBytes++; break;
                case "chunk-smaller": reply.SceneChunkBytes--; break;
                case "chunk-larger": reply.SceneChunkBytes++; break;
                case "operation": reply.SceneLoadId = Guid.NewGuid().ToString("D"); break;
                case "offset": reply.SceneLoadOffset = 1; break;
                case "world": reply.SceneWorldId = Guid.NewGuid().ToString("D"); break;
                case "entities": reply.SceneEntityCount = 1; break;
                case "components": reply.SceneComponentCount = 1; break;
            }
            return reply;
        } };
        await using var supervisor = Create(factory);
        if (field.StartsWith("version", StringComparison.Ordinal))
            await Assert.ThrowsAsync<NotSupportedException>(() => supervisor.StartWorldAsync(Document(1)));
        else await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartWorldAsync(Document(1)));
        var lease = Assert.Single(factory.Leases);
        Assert.Equal(new[] { CommandKind.Hello }, lease.Commands);
        Assert.Null(lease.Game.ActiveScene); Assert.True(lease.ResourcesReleased);
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.False(supervisor.Status.SceneLoaded);
        Assert.Null(supervisor.Status.SceneWorldId);
    }

    [Theory]
    [InlineData("BeginSceneWorld", "registry")][InlineData("AppendSceneWorld", "operation")]
    [InlineData("AppendSceneWorld", "offset")][InlineData("AppendSceneWorld", "version")]
    [InlineData("AppendSceneWorld", "max")][InlineData("AppendSceneWorld", "chunk")]
    [InlineData("AppendSceneWorld", "tick")][InlineData("AppendSceneWorld", "phase")]
    [InlineData("AppendSceneWorld", "revision")][InlineData("AppendSceneWorld", "world")]
    [InlineData("AppendSceneWorld", "entities")][InlineData("AppendSceneWorld", "components")]
    [InlineData("ActivateSceneWorld", "world-empty")][InlineData("ActivateSceneWorld", "world-format")]
    [InlineData("ActivateSceneWorld", "entities")][InlineData("ActivateSceneWorld", "components")]
    [InlineData("ActivateSceneWorld", "revision")]
    public async Task InvalidTransferAcknowledgement_FailsClosedAndReapsWithoutReady(string command, string field)
    {
        var target = Enum.Parse<CommandKind>(command);
        var factory = new RuntimeFactory { Configure = lease => lease.Filter = (request, reply) =>
        { if (request.Kind == (int)target) Mutate(reply, field); return reply; } };
        await using var supervisor = Create(factory);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartWorldAsync(Document(2)));
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.Same(error, supervisor.Status.Failure!.Cause);
        Assert.False(supervisor.Status.SceneLoaded); Assert.Null(supervisor.Status.SceneWorldId);
        var lease = Assert.Single(factory.Leases);
        Assert.True(lease.ResourcesReleased); Assert.DoesNotContain(CommandKind.SetMode, lease.Commands);
        Assert.Equal(target, lease.Commands.Last());
        if (lease.LastScene != null) Assert.True(lease.LastScene.IsDisposed);
    }

    [Theory]
    [InlineData("registry")][InlineData("operation")][InlineData("offset")][InlineData("version")]
    [InlineData("max")][InlineData("chunk")][InlineData("world")][InlineData("entities")][InlineData("components")]
    public async Task ActiveAcknowledgementIdentityDrift_IsRejectedAfterSuccessfulActivation(string field)
    {
        var factory = new RuntimeFactory(); await using var supervisor = Create(factory);
        await supervisor.StartWorldAsync(Document(1));
        var lease = Assert.Single(factory.Leases);
        lease.Filter = (request, reply) => { if (request.Kind == (int)CommandKind.GetStatus) Mutate(reply, field); return reply; };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.GetStatusAsync());
        Assert.Equal(SessionState.Faulted, supervisor.Status.State);
        Assert.Same(error, supervisor.Status.Failure!.Cause); Assert.True(lease.ResourcesReleased);
        Assert.True(lease.LastScene!.IsDisposed);
        Assert.Single(lease.Commands, command => command == CommandKind.GetStatus);
    }

    [Theory]
    [InlineData("BeginSceneWorld")][InlineData("AppendSceneWorld")][InlineData("ActivateSceneWorld")]
    public async Task CancellationDuringTransfer_DiscardsLateAckAndNeverReplaysInNewGeneration(string command)
    {
        var entered = Signal(); var release = Signal(); var target = Enum.Parse<CommandKind>(command);
        var factory = new RuntimeFactory { Configure = lease => lease.AfterReply = async request =>
        { if (request.Kind == (int)target) { entered.TrySetResult(); await release.Task; } } };
        await using var supervisor = Create(factory); using var cancellation = new CancellationTokenSource();
        var start = supervisor.StartWorldAsync(Document(2), token: cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(SessionState.Starting, supervisor.Status.State); Assert.False(supervisor.Status.SceneLoaded);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start);
        var old = Assert.Single(factory.Leases);
        Assert.True(old.ResourcesReleased); Assert.False(supervisor.Status.SceneLoaded);
        Assert.DoesNotContain(CommandKind.SetMode, old.Commands);
        if (old.LastScene != null) Assert.True(old.LastScene.IsDisposed);
        factory.Configure = null; await supervisor.StartWorldAsync(Document(1)); var current = supervisor.Status;
        release.SetResult(); await old.LastSend!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(current, supervisor.Status); Assert.Equal(2UL, current.Identity!.Generation);
        Assert.Equal(1, old.Commands.Count(kind => kind == target));
        Assert.Equal(1, factory.Leases[1].Commands.Count(kind => kind == CommandKind.BeginSceneWorld));
    }

    [Fact]
    public async Task StopDuringActivation_FencesLateReadinessAndAllowsOnlyFreshGeneration()
    {
        var entered = Signal(); var release = Signal();
        var factory = new RuntimeFactory { Configure = lease => lease.AfterReply = async request =>
        { if (request.Kind == (int)CommandKind.ActivateSceneWorld) { entered.SetResult(); await release.Task; } } };
        await using var supervisor = Create(factory);
        var start = supervisor.StartWorldAsync(Document(1)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stop = supervisor.StopAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        Assert.Equal(SessionState.Stopped, supervisor.Status.State); Assert.True(factory.Leases[0].ResourcesReleased);
        Assert.DoesNotContain(CommandKind.SetMode, factory.Leases[0].Commands);
        factory.Configure = null; await supervisor.StartWorldAsync(Document(1)); var current = supervisor.Status;
        release.SetResult(); await factory.Leases[0].LastSend!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(current, supervisor.Status); Assert.Equal(2UL, current.Identity!.Generation);
    }

    [Fact]
    public async Task FailedActivation_CleansCandidateBeforeReapAndDoesNotPublishScene()
    {
        var original = new IOException("import fixture");
        var factory = new RuntimeFactory { Importer = _ => throw original };
        await using var supervisor = Create(factory);
        var error = await Assert.ThrowsAsync<SceneWorldLoadException>(() => supervisor.StartWorldAsync(Document(2)));
        Assert.Same(original, error.InnerException); Assert.Equal(SceneWorldLoadError.ImportFailed, error.Error);
        Assert.Equal(SessionState.Faulted, supervisor.Status.State); Assert.False(supervisor.Status.SceneLoaded);
        Assert.Null(supervisor.Status.SceneWorldId); Assert.Null(factory.Leases[0].Game.ActiveScene);
        Assert.True(factory.Leases[0].ResourcesReleased);
        Assert.Equal(CommandKind.ActivateSceneWorld, factory.Leases[0].Commands.Last());
    }

    [Fact]
    public async Task CleanupFailure_PreservesPrimaryTransferErrorAndRetainsLeaseUntilExplicitRetry()
    {
        var factory = new RuntimeFactory { Configure = lease =>
        { lease.StopFailures = 1; lease.Filter = (_, reply) => { reply.SceneChunkBytes--; return reply; }; } };
        await using var supervisor = Create(factory);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartWorldAsync(Document(1)));
        Assert.Equal(SessionState.CleanupFailed, supervisor.Status.State); Assert.Same(error, supervisor.Status.Failure!.Cause);
        Assert.IsType<IOException>(supervisor.Status.Failure.CleanupError); Assert.False(factory.Leases[0].ResourcesReleased);
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartWorldAsync(Document(1))); Assert.Single(factory.Leases);
        await supervisor.StopAsync(); Assert.True(factory.Leases[0].ResourcesReleased);
        Assert.Equal(new[] { "Stop", "Stop", "Dispose" }, factory.Leases[0].Cleanup);
        factory.Configure = null; await supervisor.StartWorldAsync(Document(1)); Assert.Equal(2UL, supervisor.Status.Identity!.Generation);
    }

    [Theory]
    [InlineData("codec")][InlineData("resolver")]
    public async Task MissingNeutralDependency_IsRejectedBeforeLaunchOrIdentityPublication(string missing)
    {
        var factory = new RuntimeFactory();
        await using var supervisor = new SessionSupervisor(factory, Hash, Hash, "fixture", sceneWorldCodec: missing == "codec" ? null : Codec(),
            sceneResolution: missing == "resolver" ? null : Resolution());
        await Assert.ThrowsAsync<InvalidOperationException>(() => supervisor.StartWorldAsync(Document(1)));
        Assert.Empty(factory.Leases); Assert.Equal(SessionState.Stopped, supervisor.Status.State); Assert.Null(supervisor.Status.Identity);
    }

    [Fact]
    public async Task InvalidOrCancelledSource_IsRejectedBeforeAnyRuntimeAndPreservesFirstGeneration()
    {
        var factory = new RuntimeFactory(); await using var supervisor = Create(factory);
        var data = Document(1);
        await Assert.ThrowsAsync<InvalidDataException>(() => supervisor.StartWorldAsync(new(data.Scene, 2)));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => supervisor.StartWorldAsync(data, token: cancelled.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => supervisor.StartWorldAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => supervisor.StartWorldAsync(data, (TrueMoon.Aether.Builds.GameBuildArtifact)null!));
        Assert.Empty(factory.Leases); Assert.Null(supervisor.Status.Identity);
        await supervisor.StartWorldAsync(data); Assert.Equal(1UL, supervisor.Status.Identity!.Generation);
    }

    private static void Mutate(ControlReply reply, string field)
    {
        switch (field)
        {
            case "registry": reply.SceneRegistryId = Guid.NewGuid().ToString("D"); break;
            case "operation": reply.SceneLoadId = Guid.NewGuid().ToString("D"); break;
            case "offset": reply.SceneLoadOffset++; break;
            case "version": reply.SceneWorldVersion++; break;
            case "max": reply.SceneMaximumBytes++; break;
            case "chunk": reply.SceneChunkBytes++; break;
            case "tick": reply.Tick++; break;
            case "phase": reply.Phase = "Paused"; break;
            case "revision": reply.Revision++; break;
            case "world": reply.SceneWorldId = Guid.NewGuid().ToString("D"); break;
            case "world-empty": reply.SceneWorldId = ""; break;
            case "world-format": reply.SceneWorldId = Guid.Parse(reply.SceneWorldId).ToString("N"); break;
            case "entities": reply.SceneEntityCount++; break;
            case "components": reply.SceneComponentCount++; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }
    private static SessionSupervisor Create(RuntimeFactory factory) => new(factory, Hash, Hash, "fixture",
        new() { HeartbeatInterval = TimeSpan.FromMinutes(2) }, sceneWorldCodec: Codec(), sceneResolution: Resolution());
    private static SceneResolutionService Resolution() => new(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas)));
    private static SceneWorldCodec Codec() => new(new SceneFileCodec(new SceneAuthoringSessionFactory(Resolution(), new SceneAuthoringOptions())));
    private static SceneWorldData Document(int count, bool large = false) => new(new(new(Id(10)), 17,
        Enumerable.Range(0, count).Select(index => new EntityInstance(new(Id(100 + index)), DefinitionId,
            new(propertyOverrides: [new(ComponentId, "value", PropertyValue.From(3L + index)),
                new(ComponentId, "note", PropertyValue.From(large ? new string((char)('a' + index % 26), 1024) : ""))]), null, "Entity " + index, index)),
        [new(DefinitionId, new([new ComponentData(ComponentId, TypeId, 1)]))], [Schema]));
    private static Guid Id(int value) => new($"e24dd4ab-903c-4ddd-a144-{value:000000000000}");
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private record struct Value(int Number, int NoteLength);
    private sealed class Game(SceneWorldLoader loader) : SceneWorldGameSession(loader)
    {
        public override uint PatternColor => 0xff123456;
        protected override void TickWorld(SceneWorld scene, ulong nextTick)
            => scene.World.Query<Value>().ForEachWrite(static (EntityHandle entity, scoped ref Value value) => value.Number++);
    }
    private sealed class RuntimeFactory : IRuntimeFactory
    {
        internal Action<Lease>? Configure;
        internal Func<ComponentLoadContext, Value>? Importer;
        internal List<Lease> Leases { get; } = [];
        public IRuntimeLease Create(SessionLaunch launch)
        {
            var registry = new ComponentRegistryBuilder().Register(Schema, Importer ?? (context => new Value(context.GetInt32("value"), context.GetString("note").Length))).Build();
            var loader = new SceneWorldLoader(registry, new Factory<World>(() => new()), Resolution());
            var lease = new Lease(launch, Leases.Count + 101, new(loader)); Configure?.Invoke(lease); Leases.Add(lease); return lease;
        }
    }
    private sealed class Lease(SessionLaunch launch, int pid, Game game) : IRuntimeLease
    {
        internal Game Game { get; } = game;
        internal RuntimeProtocol? Protocol;
        internal Func<ControlRequest, Task>? BeforeSend, AfterReply;
        internal Func<ControlRequest, ControlReply, ControlReply>? Filter;
        internal List<ControlRequest> Requests { get; } = [];
        internal IEnumerable<CommandKind> Commands => Requests.Select(request => (CommandKind)request.Kind);
        internal List<string> Cleanup { get; } = [];
        internal int StopFailures;
        internal Task? LastSend;
        internal SceneWorld? LastScene;
        private readonly TaskCompletionSource _exit = Signal();
        public int ProcessId => pid;
        public Task Exited => _exit.Task;
        public bool ResourcesReleased { get; private set; }
        public Task LaunchAsync(CancellationToken token)
        { Protocol = new(launch.Identity, Game, pid, sceneWorldCodec: Codec()); return Task.CompletedTask; }
        public Task<ControlReply> SendAsync(ControlRequest request, CancellationToken token)
        { var task = SendCoreAsync(request); LastSend = task; return task; }
        private async Task<ControlReply> SendCoreAsync(ControlRequest request)
        {
            Requests.Add(request); if (BeforeSend != null) await BeforeSend(request);
            // Deliberately ignores the caller's wait token. Supervisor must discard late acknowledgements.
            var reply = await Protocol!.SendAsync(request); LastScene = Game.ActiveScene;
            if (AfterReply != null) await AfterReply(request);
            return Filter?.Invoke(request, reply) ?? reply;
        }
        public Task StopAsync(CancellationToken gracefulDeadline = default)
        {
            Cleanup.Add("Stop");
            if (StopFailures-- > 0) throw new IOException("reap fixture");
            _exit.TrySetResult(); return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        { Cleanup.Add("Dispose"); Protocol?.Dispose(); Game.Dispose(); ResourcesReleased = true; return ValueTask.CompletedTask; }
    }
}
