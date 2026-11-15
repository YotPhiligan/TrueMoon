using System.Numerics;
using TrueMoon.Aether.Abstractions.Ecs.Motion;
using TrueMoon.Aether.Abstractions.Ecs.Transforms;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Tests.Ecs.Rendering;
using TrueMoon.Dependencies;
using Xunit;
using A = TrueMoon.Aether.Abstractions.Ecs;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneSubsystemSessionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Session_PreparesLazilyCombinesSystemsAndReleasesBeforeBorrowedWorld(bool di)
    {
        var log = new List<string>(); var instances = new List<Probe>();
        await using var app = Build(di, args => { var probe = new Probe(args, log); instances.Add(probe); return probe; });
        await app.StartAsync(); Assert.Empty(instances);
        var factory = PrimitiveSimulationTests.Factory(app); Assert.Empty(instances);
        using var scene = Scene();
        using var simulation = factory.Create(new(scene));
        Assert.Single(instances); Assert.Equal(TimeSpan.FromSeconds(.5), instances[0].Step);
        Assert.Equal(0UL, simulation.TickNumber); Assert.Equal(Vector3.Zero, simulation.Snapshot.Items[0].Primitive.World.Translation);
        simulation.SetPlaying(true); simulation.SetPlaying(true); simulation.SetPlaying(false);
        Assert.Equal(new[] { "prepare", "paused", "playing", "paused" }, log);
        simulation.Tick();
        var marked = scene.Entities[PrimitivePipelineFixture.Entity(1)]; var ordinary = scene.Entities[PrimitivePipelineFixture.Entity(2)];
        Assert.Equal(new Vector3(10, 0, 0), scene.World.GetComponent<LocalTransform>(marked).Position);
        Assert.Equal(new Vector3(3, 0, 0), scene.World.GetComponent<LocalTransform>(ordinary).Position);
        Assert.Equal(1UL, simulation.Snapshot.Tick);
        Assert.Contains(simulation.Snapshot.Items, item => item.Primitive.World.Translation.X == 10);
        simulation.Dispose(); simulation.Dispose();
        Assert.Equal(new[] { "prepare", "paused", "playing", "paused", "execute:1", "stop", "system-dispose", "paused", "resource-dispose" }, log);
        Assert.True(scene.IsReady); Assert.False(scene.World.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => simulation.SetPlaying(true));
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Sessions_UseIndependentResourcesAndGameForwardsPlayback(bool di)
    {
        var created = new List<Probe>();
        await using var app = Build(di, args => { var probe = new Probe(args, []); created.Add(probe); return probe; });
        await app.StartAsync();
        using var first = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), PrimitiveSimulationTests.Factory(app));
        using var second = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), PrimitiveSimulationTests.Factory(app));
        first.LoadSceneWorld(new(PrimitivePipelineFixture.Document(2, velocity: true)));
        second.LoadSceneWorld(new(PrimitivePipelineFixture.Document(2, velocity: true)));
        Assert.Equal(2, created.Count); Assert.NotEqual(first.ActiveScene!.World.Id, second.ActiveScene!.World.Id);
        first.SetPlaying(true); first.Tick();
        Assert.True(created[0].Playing); Assert.False(created[1].Playing); Assert.Equal(1UL, first.TickNumber); Assert.Equal(0UL, second.TickNumber);
        first.Dispose(); Assert.True(created[0].Disposed); Assert.False(created[1].Disposed);
        second.Tick(); Assert.Equal(1UL, second.TickNumber); Assert.False(second.IsFaulted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreparationCancellation_ReleasesResourcesWithoutPublishingOrDisposingBorrowedScene(bool di)
    {
        using var end = new CancellationTokenSource(); Probe? probe = null;
        await using var app = Build(di, args => { probe = new(args, []); end.Cancel(); return probe; });
        await app.StartAsync(); using var scene = Scene();
        Assert.Equal(end.Token, Assert.Throws<OperationCanceledException>(() => PrimitiveSimulationTests.Factory(app).Create(new(scene, end.Token))).CancellationToken);
        Assert.True(probe!.Disposed); Assert.Equal(0, probe.SystemCreates); Assert.False(scene.World.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CleanupFailure_RetainsOwnerAndRetriesWithoutRepeatingSystemDisposal(bool di)
    {
        Probe? probe = null;
        await using var app = Build(di, args => probe = new(args, []) { CleanupFailures = 1 });
        await app.StartAsync(); using var scene = Scene();
        var simulation = PrimitiveSimulationTests.Factory(app).Create(new(scene));
        Assert.Throws<IOException>(() => simulation.Dispose());
        Assert.Equal(1, probe!.SystemDisposals); Assert.False(probe.Disposed); Assert.False(scene.World.IsDisposed);
        Assert.Throws<InvalidOperationException>(() => scene.Dispose());
        Assert.Throws<InvalidOperationException>(() => simulation.Tick());
        simulation.Dispose(); Assert.True(probe.Disposed); Assert.Equal(1, probe.SystemDisposals); Assert.Equal(2, probe.DisposeAttempts);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FactoryFailureWithRetainedCleanup_PreservesEarlierOwnersUntilReverseCleanupSucceeds(bool di)
    {
        Probe? first = null; var retained = new FailedOwner();
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context =>
        {
            context.UseAetherPrimitiveEcs();
            context.Services(services =>
            {
                services.Singleton<IFactory<Probe, SceneSubsystemArguments>>(_ => new Factory<Probe, SceneSubsystemArguments>(args => first = new(args, [])));
                services.Singleton<IFactory<Failure, SceneSubsystemArguments>>(_ => new Factory<Failure, SceneSubsystemArguments>(_ => throw new SceneWorldPreparationException(new IOException("prepare"), retained)));
            });
            context.UseAetherSceneSubsystems(builder => builder.Add<Probe>("first").Add<Failure>("second"));
        }).Build();
        await app.StartAsync(); using var scene = Scene();
        var error = Assert.Throws<SceneWorldPreparationException>(() => PrimitiveSimulationTests.Factory(app).Create(new(scene)));
        Assert.False(first!.Disposed); Assert.Equal(0, first.SystemCreates); Assert.Equal(2, retained.Attempts);
        Assert.Throws<InvalidOperationException>(() => scene.Dispose());
        retained.Fail = false; error.CleanupOwner.Dispose(); error.CleanupOwner.Dispose();
        Assert.True(first.Disposed); Assert.Equal(3, retained.Attempts); Assert.False(scene.World.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidSystemGraph_ReleasesPreparedResourcesAndNeverCreatesSystems(bool di)
    {
        Probe? probe = null;
        await using var app = Build(di, args => probe = new(args, [], systemId: "builtin.movement"));
        await app.StartAsync(); using var scene = Scene();
        Assert.Throws<ArgumentException>(() => PrimitiveSimulationTests.Factory(app).Create(new(scene)));
        Assert.True(probe!.Disposed); Assert.Equal(0, probe.SystemCreates); Assert.False(scene.World.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TickFailure_PausesResourcesFaultsWorldAndPreservesLastCompletedFrame(bool di)
    {
        Probe? probe = null;
        await using var app = Build(di, args => probe = new(args, []) { FailTick = true });
        await app.StartAsync(); using var scene = Scene(); using var simulation = PrimitiveSimulationTests.Factory(app).Create(new(scene));
        var initial = simulation.Snapshot; simulation.SetPlaying(true);
        Assert.Throws<IOException>(() => simulation.Tick());
        Assert.False(probe!.Playing); Assert.True(scene.World.IsFaulted); Assert.Same(initial, simulation.Snapshot); Assert.Equal(0UL, simulation.TickNumber);
        Assert.Throws<InvalidOperationException>(() => simulation.SetPlaying(true));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PlaybackFailure_PausesAllResourcesAndForbidsResume(bool di)
    {
        Probe? probe = null;
        await using var app = Build(di, args => probe = new(args, []) { FailPlay = true });
        await app.StartAsync(); using var scene = Scene(); using var simulation = PrimitiveSimulationTests.Factory(app).Create(new(scene));
        Assert.Throws<IOException>(() => simulation.SetPlaying(true));
        Assert.False(probe!.Playing); Assert.True(scene.World.IsFaulted); Assert.Equal(0UL, simulation.TickNumber);
        Assert.Throws<InvalidOperationException>(() => simulation.SetPlaying(true));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DuplicateSubsystemInstance_IsReleasedExactlyOnce(bool di)
    {
        Probe? probe = null;
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context =>
        {
            context.UseAetherPrimitiveEcs();
            context.Services(services => services.Singleton<IFactory<Probe, SceneSubsystemArguments>>(_ =>
                new Factory<Probe, SceneSubsystemArguments>(args => probe ??= new(args, []))));
            context.UseAetherSceneSubsystems(builder => builder.Add<Probe>("one").Add<Probe>("two"));
        }).Build();
        await app.StartAsync(); using var scene = Scene();
        Assert.Throws<InvalidOperationException>(() => PrimitiveSimulationTests.Factory(app).Create(new(scene)));
        Assert.Equal(1, probe!.DisposeAttempts); Assert.True(probe.Disposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SceneLease_RejectsRepreparationAndEarlyDependencyDisposal(bool di)
    {
        var created = new List<Probe>();
        await using var app = Build(di, args => { var owner = new Probe(args, []); created.Add(owner); return owner; });
        await app.StartAsync(); using var scene = Scene();
        var factory = app.Services.Resolve<IFactory<SceneSubsystemSession, SceneSubsystemArguments>>()!;
        using var subsystems = factory.Create(new(scene, TimeSpan.FromSeconds(.5)))!;
        Assert.Throws<InvalidOperationException>(() => factory.Create(new(scene, TimeSpan.FromSeconds(.5))));
        Assert.Single(created); Assert.Throws<InvalidOperationException>(() => scene.Dispose());
        using var scheduler = EcsScheduler.Create(scene.World, [new(Aether.Runtime.Ecs.Motion.MovementSystem.DescribeContract(),
            new Factory<A.ISystem, World>(world => new Aether.Runtime.Ecs.Motion.MovementSystem(world))), .. subsystems.Systems]);
        Assert.Throws<InvalidOperationException>(() => subsystems.Dispose()); Assert.False(created[0].Disposed);
        scheduler.Dispose(); subsystems.Dispose(); Assert.True(created[0].Disposed); Assert.Equal(1, created[0].SystemDisposals);
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Selection_IsFrozenAndIndependentOfPrimitiveRegistrationOrder(bool di)
    {
        Probe? probe = null;
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context =>
        {
            SceneSubsystemsBuilder captured = null!;
            context.UseAetherSceneSubsystems(builder => { captured = builder; builder.Add<Probe>("selected"); });
            captured.Add<Failure>("late-unregistered-factory");
            context.Services(services => services.Singleton<IFactory<Probe, SceneSubsystemArguments>>(_ =>
                new Factory<Probe, SceneSubsystemArguments>(args => probe = new(args, []))));
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync(); using var scene = Scene(); using var simulation = PrimitiveSimulationTests.Factory(app).Create(new(scene));
        simulation.Tick(); Assert.Equal(1, probe!.SystemCreates); Assert.Equal(1UL, simulation.TickNumber);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SystemCreationFailure_ReleasesSubsystemAndPreservesOriginalFailure(bool di)
    {
        Probe? probe = null;
        await using var app = Build(di, args => probe = new(args, []) { FailSystemCreation = true });
        await app.StartAsync(); using var scene = Scene();
        Assert.Equal("system creation", Assert.Throws<IOException>(() => PrimitiveSimulationTests.Factory(app).Create(new(scene))).Message);
        Assert.True(probe!.Disposed); Assert.Equal(0, probe.SystemDisposals); Assert.Equal(1, probe.DisposeAttempts);
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptySelectionAndNullFactoryResult_HaveExplicitOutcomes(bool di)
    {
        await using var empty = PrimitiveSimulationTests.Builder(di).Setup(context =>
        { context.UseAetherPrimitiveEcs(); context.UseAetherSceneSubsystems(_ => { }); }).Build();
        await empty.StartAsync(); using var scene = Scene(); using (var simulation = PrimitiveSimulationTests.Factory(empty).Create(new(scene)))
        { simulation.Tick(); Assert.Equal(1UL, simulation.TickNumber); }
        await using var invalid = Build(di, _ => null!); await invalid.StartAsync();
        using var candidate = Scene();
        Assert.Throws<InvalidOperationException>(() => PrimitiveSimulationTests.Factory(invalid).Create(new(candidate)));
        Assert.False(candidate.World.IsDisposed);
    }

    [Fact]
    public void Registration_RejectsDuplicateInvalidAndOverBudgetSelection()
    {
        var builder = new SceneSubsystemsBuilder();
        Assert.Throws<ArgumentException>(() => builder.Add<Probe>(" "));
        Assert.Throws<ArgumentException>(() => builder.Add<Probe>(new string('x', 129)));
        builder.Add<Probe>("first"); Assert.Throws<ArgumentException>(() => builder.Add<Probe>("first"));
        for (var i = 1; i < 64; i++) builder.Add<Probe>(i.ToString());
        Assert.Throws<InvalidOperationException>(() => builder.Add<Probe>("overflow"));
        Assert.Throws<InvalidOperationException>(() => PrimitiveSimulationTests.Builder(false).Setup(context =>
        { context.UseAetherSceneSubsystems(_ => { }); context.UseAetherSceneSubsystems(_ => { }); }).Build());
    }

    private static SceneWorld Scene() => PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(2, velocity: true));
    private static IApp Build(bool di, Func<SceneSubsystemArguments, Probe> create) => PrimitiveSimulationTests.Builder(di).Setup(context =>
    {
        context.UseAetherPrimitiveEcs(options => options.FixedStep = TimeSpan.FromSeconds(.5));
        context.Services(services => services.Singleton<IFactory<Probe, SceneSubsystemArguments>>(_ => new Factory<Probe, SceneSubsystemArguments>(create)));
        context.UseAetherSceneSubsystems(builder => builder.Add<Probe>("probe"));
    }).Build();

    private sealed class Probe : ISceneSubsystem
    {
        private readonly List<string> _log;
        public TimeSpan Step { get; }
        public bool Playing, Disposed, FailTick, FailPlay, FailSystemCreation;
        public int CleanupFailures, SystemCreates, SystemDisposals, DisposeAttempts;
        public IReadOnlyList<EcsSystemRegistration> Systems { get; }
        public Probe(SceneSubsystemArguments arguments, List<string> log, string systemId = "test.subsystem")
        {
            Step = arguments.FixedStep; _log = log; log.Add("prepare");
            var world = arguments.Scene.World; var entity = arguments.Scene.Entities[PrimitivePipelineFixture.Entity(1)];
            world.AddComponent(entity, new ExternalMotion());
            var description = new A.EcsSystemDescription(systemId, A.EcsPhase.Movement).Write<LocalTransform>();
            if (systemId != "builtin.movement") description.After("builtin.movement");
            Systems = [new(description,
                new Factory<A.ISystem, World>(borrowed => { Assert.Same(world, borrowed); if (FailSystemCreation) throw new IOException("system creation"); SystemCreates++; return new System(this, world, entity); }))];
        }
        public void SetPlaying(bool playing)
        {
            Assert.False(Disposed); Playing = playing; _log.Add(playing ? "playing" : "paused");
            if (playing && FailPlay) throw new IOException("playback");
        }
        public void Dispose()
        {
            DisposeAttempts++; Assert.False(Disposed);
            if (CleanupFailures-- > 0) throw new IOException("cleanup");
            Disposed = true; _log.Add("resource-dispose");
        }
        private sealed class System(Probe owner, World world, A.EntityHandle entity) : A.ISystem
        {
            public void Execute(A.EcsSystemContext context)
            {
                Assert.False(owner.Disposed); owner._log.Add($"execute:{context.TickNumber}");
                if (owner.FailTick) throw new IOException("tick");
                var pose = world.GetComponent<LocalTransform>(entity); world.SetComponent(entity, pose with { Position = pose.Position + new Vector3(10, 0, 0) });
            }
            public void Stop() => owner._log.Add("stop");
            public void Dispose() { Assert.False(owner.Disposed); owner.SystemDisposals++; owner._log.Add("system-dispose"); }
        }
    }
    private sealed class FailedOwner : IDisposable
    {
        public bool Fail = true;
        public int Attempts;
        public void Dispose() { Attempts++; if (Fail) throw new IOException("retained"); }
    }
    private sealed class Failure : ISceneSubsystem
    {
        public IReadOnlyList<EcsSystemRegistration> Systems => [];
        public void SetPlaying(bool playing) { }
        public void Dispose() { }
    }
}
