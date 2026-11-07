using System.Numerics;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

public sealed class PrimitiveSimulationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_IsLazyPreservesInjectedFactoriesAndCreatesCallerOwnedGraph(bool di)
    {
        var calls = new List<string>(); TransformSystem? transforms = null; MovementSystem? movement = null;
        PrimitiveExtractionSystem? extraction = null;
        var transformFactory = new Factory<TransformSystem, World>(world =>
        { calls.Add("transform"); Assert.Equal(3, world.ComponentTypeCount); return transforms = new(world); });
        var movementFactory = new Factory<MovementSystem, World>(world =>
        { calls.Add("movement"); return movement = new(world); });
        var extractionFactory = new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(args =>
        { calls.Add("extraction"); Assert.Equal(3, args.World.ComponentTypeCount); return extraction = new(args); });
        var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<TransformSystem, World>>(_ => transformFactory);
                services.Singleton<IFactory<MovementSystem, World>>(_ => movementFactory);
                services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ => extractionFactory);
            });
            Assert.Same(context, context.UseAetherPrimitiveEcs());
        }).Build();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(velocity: true));
        PrimitiveSimulation? simulation = null;
        try
        {
            Assert.Empty(calls); await app.StartAsync(); Assert.Empty(calls);
            Assert.Same(transformFactory, Assert.Single(app.Services.ResolveAll<IFactory<TransformSystem, World>>()));
            Assert.Same(movementFactory, Assert.Single(app.Services.ResolveAll<IFactory<MovementSystem, World>>()));
            Assert.Same(extractionFactory, Assert.Single(app.Services.ResolveAll<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>()));
            Assert.Empty(app.Services.ResolveAll<World>());
            Assert.Empty(app.Services.ResolveAll<PrimitiveSimulation>());
            var factory = Assert.Single(app.Services.ResolveAll<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>());
            Assert.Empty(calls);
            simulation = factory.Create(new(scene));
            Assert.Equal(new[] { "movement", "transform", "extraction" }, calls);
            Assert.Equal((scene.World.Id, 0UL), (simulation.WorldId, simulation.TickNumber));
            Assert.Equal((scene.Scene.Id, scene.Scene.Revision, 0UL), (simulation.Snapshot.SceneId, simulation.Snapshot.Revision, simulation.Snapshot.Tick));
            Assert.Equal(0UL, transforms!.LastTick);
            Assert.Throws<InvalidOperationException>(() => scene.Dispose());
            await app.DisposeAsync();
            simulation.Tick();
            var step = (float)TimeSpan.FromSeconds(1d / 60).TotalSeconds;
            PrimitivePipelineFixture.Near(new(6 * step, 0, 0), Assert.Single(simulation.Snapshot.Items).Primitive.World.Translation);
            Assert.Equal((1UL, 1UL, 1UL), (simulation.TickNumber, simulation.Snapshot.Tick, transforms.LastTick!.Value));
            simulation.Dispose(); simulation.Dispose();
            Assert.Null(extraction!.Snapshot);
            Assert.Throws<ObjectDisposedException>(() => transforms.Update(2));
            Assert.Throws<ObjectDisposedException>(() => movement!.Advance(TimeSpan.FromSeconds(1)));
            Assert.False(scene.World.IsDisposed);
            Assert.True(scene.IsReady);
            scene.Dispose(); Assert.True(scene.World.IsDisposed);
        }
        finally { simulation?.Dispose(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Prime_InitializesGroupsAndPreservesParentShearThroughMovementTransformExtraction(bool di)
    {
        await using var app = Builder(di).Setup(context => context.UseAetherPrimitiveEcs(options => options.FixedStep = TimeSpan.FromSeconds(.5))).Build();
        await app.StartAsync();
        var group = new EntityDefinitionId(PrimitivePipelineFixture.Id(8));
        var parent = new EntityDefinitionId(PrimitivePipelineFixture.Id(9));
        var source = PrimitivePipelineFixture.Document(velocity: true,
            transform: [PrimitivePipelineFixture.Number("x", 1), PrimitivePipelineFixture.Number("yaw", Math.PI / 4)]);
        source = new(source.Id, source.Revision,
            [new(PrimitivePipelineFixture.Entity(3), PrimitivePipelineFixture.Definition, parentId: PrimitivePipelineFixture.Entity(2)),
             new(PrimitivePipelineFixture.Entity(2), parent, parentId: PrimitivePipelineFixture.Entity(1)),
             new(PrimitivePipelineFixture.Entity(1), group)],
            source.Definitions.Concat([new(group, new()), new(parent, new([new(PrimitivePipelineFixture.Transform, BuiltInComponents.TransformId, 1,
                [PrimitivePipelineFixture.Number("sx", 2), PrimitivePipelineFixture.Number("x", 10)])]))]), source.Schemas);
        using var scene = PrimitivePipelineFixture.Loader().Load(source);
        using var simulation = Factory(app).Create(new(scene));
        var root = scene.Entities[PrimitivePipelineFixture.Entity(1)];
        var parentEntity = scene.Entities[PrimitivePipelineFixture.Entity(2)];
        var child = scene.Entities[PrimitivePipelineFixture.Entity(3)];
        Assert.Equal(LocalTransform.Identity, scene.World.GetComponent<LocalTransform>(root));
        Assert.Equal(default, scene.World.GetComponent<TransformParent>(root).Parent);
        Assert.Equal(root, scene.World.GetComponent<TransformParent>(parentEntity).Parent);
        Assert.Equal(parentEntity, scene.World.GetComponent<TransformParent>(child).Parent);
        Assert.All(scene.Entities.Values, entity => Assert.Equal(0UL, scene.World.GetComponent<WorldTransform>(entity).Tick));
        var initial = simulation.Snapshot;
        Assert.Equal(new Vector3(12, 0, 0), Assert.Single(initial.Items).Primitive.World.Translation);
        simulation.Tick();
        var current = Assert.Single(simulation.Snapshot.Items).Primitive;
        Assert.Equal(new Vector3(4, 0, 0), scene.World.GetComponent<LocalTransform>(child).Position);
        Assert.Equal(new Vector3(18, 0, 0), current.World.Translation);
        var c = MathF.Sqrt(.5f);
        PrimitivePipelineFixture.Near(new(2 * c, 0, -c), Vector3.TransformNormal(Vector3.UnitX, current.World));
        PrimitivePipelineFixture.Near(new(2 * c, 0, c), Vector3.TransformNormal(Vector3.UnitZ, current.World));
        Assert.InRange(MathF.Abs(Vector3.Dot(Vector3.TransformNormal(Vector3.UnitX, current.World), Vector3.TransformNormal(Vector3.UnitZ, current.World))), 1.49f, 1.51f);
        Assert.Equal(new Vector3(12, 0, 0), Assert.Single(initial.Items).Primitive.World.Translation);
        Assert.Equal(0UL, initial.Tick);
        Assert.Equal(1UL, simulation.Snapshot.Tick);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_SnapshotsMutableOptionsBeforeLazyFactoryResolution(bool di)
    {
        PrimitiveSimulationOptions captured = null!;
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherPrimitiveEcs(options =>
            {
                captured = options; options.FixedStep = TimeSpan.FromSeconds(.25);
                options.MaximumTransformEntities = 2; options.MaximumPrimitives = 1;
                options.Camera = new(4, 5, 6, 0, 0, 0);
            });
            captured.FixedStep = TimeSpan.Zero; captured.MaximumTransformEntities = 0;
            captured.MaximumPrimitives = 0; captured.Camera = default;
        }).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(velocity: true));
        using var simulation = Factory(app).Create(new(scene));
        Assert.Equal(new TrueMoon.Aether.Checkpoints.CameraTransform(4, 5, 6, 0, 0, 0), simulation.Snapshot.Scene.Camera);
        simulation.Tick();
        Assert.Equal(new Vector3(1.5f, 0, 0), Assert.Single(simulation.Snapshot.Items).Primitive.World.Translation);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Tick_LateInvalidAppearanceFaultsWorldAfterMovementAndPreservesCompleteSnapshot(bool di)
    {
        await using var app = Builder(di).Setup(context => context.UseAetherPrimitiveEcs(options => options.FixedStep = TimeSpan.FromSeconds(.5))).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(2, velocity: true));
        using var simulation = Factory(app).Create(new(scene));
        simulation.Tick(); var previous = simulation.Snapshot;
        var last = scene.Entities[PrimitivePipelineFixture.Entity(2)];
        scene.World.SetComponent(last, new PrimitiveAppearance(new(float.NaN, 0, 0), false));
        Assert.Throws<InvalidDataException>(() => simulation.Tick());
        Assert.True(scene.World.IsFaulted);
        Assert.Equal(1UL, simulation.TickNumber);
        Assert.Same(previous, simulation.Snapshot);
        Assert.Equal(new[] { new Vector3(3, 0, 0), new Vector3(3, 0, 0) }, previous.Items.Select(item => item.Primitive.World.Translation));
        Assert.All(scene.Entities.Values, entity =>
        {
            Assert.Equal(new Vector3(6, 0, 0), scene.World.GetComponent<LocalTransform>(entity).Position);
            Assert.Equal(2UL, scene.World.GetComponent<WorldTransform>(entity).Tick);
        });
        Assert.Throws<InvalidOperationException>(() => simulation.Tick());
        Assert.Same(previous, simulation.Snapshot);
        using var independentExtractor = new PrimitiveExtractionSystem(new(scene.World, scene.Scene.Id, scene.Scene.Revision));
        Assert.Throws<InvalidOperationException>(() => independentExtractor.Capture(2));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreCancellation_DoesNotActivateSystemsOrMutateSceneAndTickCancellationAllowsRetry(bool di)
    {
        var calls = 0;
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<MovementSystem, World>>(_ => new Factory<MovementSystem, World>(world => { calls++; return new(world); })));
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(velocity: true));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var factory = Factory(app);
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => factory.Create(new(scene, canceled.Token))).CancellationToken);
        Assert.Equal(0, calls);
        Assert.False(scene.World.HasComponent<WorldTransform>(Assert.Single(scene.Entities.Values)));
        using var simulation = factory.Create(new(scene)); var previous = simulation.Snapshot;
        Assert.Throws<OperationCanceledException>(() => simulation.Tick(canceled.Token));
        Assert.Same(previous, simulation.Snapshot);
        Assert.Equal(0UL, simulation.TickNumber);
        Assert.False(scene.World.IsFaulted);
        simulation.Tick(); Assert.Equal(1UL, simulation.TickNumber);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Factory_FailedSystemCreationDisposesCreatedSystemsAndReleasesBorrowedWorld(bool di)
    {
        var expected = new IOException("extraction creation"); TransformSystem? transform = null;
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<TransformSystem, World>>(_ => new Factory<TransformSystem, World>(world => transform = new(world)));
                services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ => new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(arguments => throw expected));
            });
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document());
        Assert.Same(expected, Assert.Throws<IOException>(() => Factory(app).Create(new(scene))));
        Assert.Throws<ObjectDisposedException>(() => transform!.Update(0));
        Assert.True(scene.IsReady); Assert.False(scene.World.IsDisposed);
        using (var scheduler = EcsScheduler.Create(scene.World, [])) Assert.True(scheduler.Tick(TimeSpan.FromSeconds(.1)));
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Factory_RejectsForeignSystemAndWrongSourceExtractionBeforePublishing(bool di)
    {
        using var foreign = new World();
        var mismatch = false;
        await using var app = Builder(di).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<MovementSystem, World>>(_ => new Factory<MovementSystem, World>(world => new(mismatch ? world : foreign)));
                services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ =>
                    new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(args => new(args with { Revision = args.Revision + 1 })));
            });
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync();
        using var first = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document());
        Assert.Throws<InvalidOperationException>(() => Factory(app).Create(new(first)));
        Assert.False(first.World.HasComponent<WorldTransform>(Assert.Single(first.Entities.Values)));
        mismatch = true;
        using var second = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document());
        Assert.Throws<InvalidDataException>(() => Factory(app).Create(new(second)));
        Assert.Equal(0UL, second.World.GetComponent<WorldTransform>(Assert.Single(second.Entities.Values)).Tick);
        Assert.False(first.World.IsDisposed); Assert.False(second.World.IsDisposed); Assert.False(foreign.IsDisposed);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeDefaults_PreserveExplicitPrimitiveProfileAndCustomFactory(bool di)
    {
        var custom = new Factory<PrimitiveSimulation, PrimitiveSimulationRequest>(_ => throw new IOException("caller factory"));
        await using var app = RuntimeApp(di, context => context.Services(services =>
            services.Singleton<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>(_ => custom)));
        await app.StartAsync();
        Assert.Same(custom, Assert.Single(app.Services.ResolveAll<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()));
        Assert.Empty(app.Services.ResolveAll<IFactory<TransformSystem, World>>());
        Assert.Empty(app.Services.ResolveAll<IFactory<MovementSystem, World>>());
        Assert.Empty(app.Services.ResolveAll<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>());
        await using var configured = RuntimeApp(di, context => context.UseAetherPrimitiveEcs(options => options.FixedStep = TimeSpan.FromSeconds(.5)));
        await configured.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(velocity: true));
        using var simulation = Factory(configured).Create(new(scene));
        simulation.Tick();
        Assert.Equal(new Vector3(3, 0, 0), Assert.Single(simulation.Snapshot.Items).Primitive.World.Translation);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeRegistration_AddsLazyDefaultPrimitiveFactoriesWhenAbsent(bool di)
    {
        await using var app = RuntimeApp(di); await app.StartAsync();
        Assert.Single(app.Services.ResolveAll<IFactory<TransformSystem, World>>());
        Assert.Single(app.Services.ResolveAll<IFactory<MovementSystem, World>>());
        Assert.Single(app.Services.ResolveAll<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>());
        Assert.Single(app.Services.ResolveAll<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>());
        Assert.Empty(app.Services.ResolveAll<World>()); Assert.Empty(app.Services.ResolveAll<PrimitiveSimulation>());
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document());
        using var simulation = Factory(app).Create(new(scene));
        Assert.Single(simulation.Snapshot.Items);
    }

    [Theory]
    [InlineData("transforms")] [InlineData("drawables")]
    public async Task Prime_ConfiguredLimitsRejectWholeCandidateAndReleaseCreatedGraph(string limit)
    {
        await using var app = Builder(false).Setup(context => context.UseAetherPrimitiveEcs(options =>
        {
            options.MaximumTransformEntities = limit == "transforms" ? 1 : 4;
            options.MaximumPrimitives = 1;
        })).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document(2));
        if (limit == "transforms")
            Assert.Equal(TransformEvaluationError.LimitExceeded, Assert.Throws<TransformEvaluationException>(() => Factory(app).Create(new(scene))).Error);
        else
            Assert.Throws<InvalidDataException>(() => Factory(app).Create(new(scene)));
        Assert.False(scene.World.IsDisposed);
        Assert.True(scene.IsReady);
        using (var graph = EcsScheduler.Create(scene.World, [])) Assert.True(graph.Tick(TimeSpan.FromSeconds(.1)));
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Fact]
    public async Task Factory_RejectsNullRequestNullSceneAndDisposedSceneWithoutTakingOwnership()
    {
        await using var app = Builder(false).Setup(context => context.UseAetherPrimitiveEcs()).Build();
        await app.StartAsync(); var factory = Factory(app);
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => factory.Create(new(null!)));
        var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document()); scene.Dispose();
        Assert.Throws<InvalidOperationException>(() => factory.Create(new(scene)));
        Assert.True(scene.IsDisposed);
    }

    [Fact]
    public async Task Factory_CancellationFromLastSystemFactoryReleasesEveryCreatedSystemBeforeInitialization()
    {
        using var canceled = new CancellationTokenSource();
        TransformSystem? transform = null; MovementSystem? movement = null; PrimitiveExtractionSystem? extraction = null;
        await using var app = Builder(false).Setup(context =>
        {
            context.Services(services =>
            {
                services.Singleton<IFactory<TransformSystem, World>>(_ => new Factory<TransformSystem, World>(world => transform = new(world)));
                services.Singleton<IFactory<MovementSystem, World>>(_ => new Factory<MovementSystem, World>(world => movement = new(world)));
                services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ =>
                    new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(args =>
                    { extraction = new(args); canceled.Cancel(); return extraction; }));
            });
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync();
        using var scene = PrimitivePipelineFixture.Loader().Load(PrimitivePipelineFixture.Document());
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => Factory(app).Create(new(scene, canceled.Token))).CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => transform!.Update(0));
        Assert.Throws<ObjectDisposedException>(() => movement!.Advance(TimeSpan.FromSeconds(.1)));
        Assert.Throws<ObjectDisposedException>(() => extraction!.Capture(0));
        Assert.False(scene.World.HasComponent<WorldTransform>(Assert.Single(scene.Entities.Values)));
        Assert.True(scene.IsReady); Assert.False(scene.World.IsDisposed);
        scene.Dispose(); Assert.True(scene.IsDisposed);
    }

    [Fact]
    public void Registration_RejectsDuplicateAndInvalidOptionsAtComposition()
    {
        Assert.Throws<ArgumentNullException>(() => PrimitiveEcsConfigurationExtensions.UseAetherPrimitiveEcs(null!));
        Assert.Throws<InvalidOperationException>(() => Builder(false).Setup(context =>
        { context.UseAetherPrimitiveEcs(); context.UseAetherPrimitiveEcs(); }).Build());
        Assert.Throws<ArgumentOutOfRangeException>(() => Builder(false).Setup(context => context.UseAetherPrimitiveEcs(options => options.MaximumPrimitives = 65)).Build());
    }

    internal static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
    internal static IFactory<PrimitiveSimulation, PrimitiveSimulationRequest> Factory(IApp app) => app.Services.Resolve<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>()!;
    private static IApp RuntimeApp(bool di, Action<IAppConfigurationContext>? configure = null)
    {
        const string hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        return Builder(di).Setup(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = hash; options.GameBuildId = hash;
                options.ReportDirectory = "primitive-profile-tests"; options.RuntimeFactory = _ => new NoRuntime();
            });
            configure?.Invoke(context);
            context.UseAetherRuntime(new(new(Guid.NewGuid(), 1, hash, hash, hash), "engine-" + Guid.NewGuid().ToString("N"),
                "primitive-profile-tests", Build: new GameBuildReference(Path.GetFullPath("unused-primitive-build"), hash)), int.MaxValue);
        }).Build();
    }
    private sealed class NoRuntime : IRuntimeFactory
    { public IRuntimeLease Create(SessionLaunch launch) => throw new InvalidOperationException("Registration must not launch a child."); }
}
