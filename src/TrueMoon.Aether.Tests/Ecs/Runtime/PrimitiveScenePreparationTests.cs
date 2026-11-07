using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tests.Ecs.Loading;
using TrueMoon.Aether.Tests.Ecs.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class PrimitiveScenePreparationTests
{
    [Fact]
    public void PreparationException_PreservesOriginalErrorAndExclusiveOwnerAndRejectsNulls()
    {
        var error = new IOException("prepare"); var owner = new Resource();
        var failure = new SceneWorldPreparationException(error, owner);
        Assert.Same(error, failure.InnerException);
        Assert.Same(owner, failure.CleanupOwner);
        Assert.Throws<ArgumentNullException>(() => new SceneWorldPreparationException(null!, owner));
        Assert.Throws<ArgumentNullException>(() => new SceneWorldPreparationException(error, null!));
        Assert.Equal(0, owner.Calls);
    }

    [Fact]
    public void Prepare_CleanFailurePreservesErrorIdentityDiscardsCandidateAndAllowsFreshRetry()
    {
        var candidates = new List<SceneWorld>(); var expected = new IOException("private preparation");
        using var game = new PreparingGame(PrimitivePipelineFixture.Loader());
        game.Prepare = (scene, _) =>
        {
            candidates.Add(scene);
            Assert.Null(game.ActiveScene);
            Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
            Assert.True(scene.IsReady);
            if (candidates.Count == 1) throw expected;
            return null;
        };
        var data = new SceneWorldData(PrimitivePipelineFixture.Document());
        Assert.Same(expected, Assert.Throws<IOException>(() => game.LoadSceneWorld(data)));
        Assert.True(candidates[0].IsDisposed);
        Assert.Null(game.ActiveScene);
        Assert.False(game.IsFaulted);
        game.LoadSceneWorld(data);
        Assert.Same(candidates[1], game.ActiveScene);
        Assert.NotEqual(candidates[0].World.Id, candidates[1].World.Id);
        Assert.Equal((23L, 2, 2), (game.Revision, game.SceneComponentCount, candidates.Count));
        game.Tick(); Assert.Equal(1UL, game.TickNumber);
    }

    [Fact]
    public void Prepare_ReturnThenCancelTracksResourceBeforeFinalCheckAndReleasesItBeforeWorld()
    {
        using var canceled = new CancellationTokenSource(); SceneWorld? candidate = null;
        var trace = new List<string>();
        var owner = new Resource(() => { Assert.False(candidate!.World.IsDisposed); trace.Add("owner"); });
        using var game = new PreparingGame(PrimitivePipelineFixture.Loader());
        game.Prepare = (scene, token) =>
        {
            Assert.Equal(canceled.Token, token); candidate = scene;
            Assert.Null(game.ActiveScene); canceled.Cancel(); return owner;
        };
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(new(PrimitivePipelineFixture.Document()), canceled.Token)).CancellationToken);
        Assert.Equal(new[] { "owner" }, trace);
        Assert.Equal(1, owner.Calls);
        Assert.True(candidate!.IsDisposed);
        Assert.Null(game.ActiveScene);
        Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
        game.Dispose(); Assert.Equal(1, owner.Calls);
    }

    [Fact]
    public void Prepare_TypedPartialOwnerFailureRetainsPrivateSceneUntilExplicitDisposalRetry()
    {
        var release = false; var trace = new List<string>(); SceneWorld? candidate = null;
        var original = new IOException("prepare"); var cleanup = new IOException("owner cleanup");
        var owner = new Resource(() =>
        {
            Assert.False(candidate!.World.IsDisposed); trace.Add("owner");
            if (!release) throw cleanup;
        });
        var lease = new Resource(() => { Assert.True(candidate!.World.IsDisposed); trace.Add("lease"); });
        var loader = ResourceLoader(lease);
        var game = new PreparingGame(loader) { Cleanup = () => trace.Add("hook") };
        var retained = new SceneWorldPreparationException(original, owner);
        game.Prepare = (scene, _) => { candidate = scene; throw retained; };
        try
        {
            var error = Assert.Throws<AggregateException>(() => game.LoadSceneWorld(new(LoadFixture.Scene(LoadFixture.IntegerSchema))));
            Assert.Same(retained, error.InnerExceptions[0]); Assert.Same(cleanup, error.InnerExceptions[1]);
            Assert.Null(game.ActiveScene); Assert.True(candidate!.IsReady); Assert.False(candidate.World.IsDisposed);
            Assert.Equal((1, 0, 0), (owner.Calls, lease.Calls, game.CleanupCalls));
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(new(LoadFixture.Scene(LoadFixture.IntegerSchema))));
            Assert.Same(cleanup, Assert.Throws<IOException>(() => game.Dispose()));
            Assert.Equal((2, 0, 1), (owner.Calls, lease.Calls, game.CleanupCalls));
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(new(LoadFixture.Scene(LoadFixture.IntegerSchema))));
            release = true; game.Dispose(); game.Dispose();
            Assert.Equal(new[] { "owner", "hook", "owner", "owner", "lease" }, trace);
            Assert.Equal((3, 1, 1), (owner.Calls, lease.Calls, game.CleanupCalls));
            Assert.True(candidate.IsDisposed); Assert.Null(game.ActiveScene);
            Assert.Throws<ObjectDisposedException>(() => game.Tick());
        }
        finally { release = true; game.Dispose(); }
    }

    [Fact]
    public void Prepare_SuccessfulOwnerCleanupIsNotRepeatedWhenCandidateLeaseCleanupFails()
    {
        var release = false; SceneWorld? candidate = null; var expected = new IOException("lease");
        var owner = new Resource(() => Assert.False(candidate!.World.IsDisposed));
        var lease = new Resource(() => { Assert.True(candidate!.World.IsDisposed); if (!release) throw expected; });
        var failure = new IOException("prepare");
        var game = new PreparingGame(ResourceLoader(lease));
        game.Prepare = (scene, _) => { candidate = scene; throw new SceneWorldPreparationException(failure, owner); };
        try
        {
            var error = Assert.Throws<AggregateException>(() => game.LoadSceneWorld(new(LoadFixture.Scene(LoadFixture.IntegerSchema))));
            Assert.Same(failure, Assert.IsType<SceneWorldPreparationException>(error.InnerExceptions[0]).InnerException);
            Assert.Contains(expected, Assert.IsType<AggregateException>(error.InnerExceptions[1]).InnerExceptions);
            Assert.Equal((1, 1), (owner.Calls, lease.Calls));
            Assert.Null(game.ActiveScene); Assert.True(candidate!.World.IsDisposed); Assert.False(candidate.IsDisposed);
            Assert.Throws<AggregateException>(() => game.Dispose());
            Assert.Equal((1, 2, 1), (owner.Calls, lease.Calls, game.CleanupCalls));
            release = true; game.Dispose(); game.Dispose();
            Assert.Equal((1, 3, 1), (owner.Calls, lease.Calls, game.CleanupCalls));
            Assert.True(candidate.IsDisposed);
        }
        finally { release = true; game.Dispose(); }
    }

    [Fact]
    public void Dispose_OrdersLegacyHookPreparedGraphWorldAndLeasesAndRetriesOnlyFailedStage()
    {
        var releaseHook = false; var releaseOwner = false; var trace = new List<string>(); SceneWorld? active = null;
        var hookFailure = new IOException("hook"); var ownerFailure = new IOException("graph");
        var lease = new Resource(() => { Assert.True(active!.World.IsDisposed); trace.Add("lease"); });
        var owner = new Resource(() =>
        {
            Assert.False(active!.World.IsDisposed); trace.Add("owner"); if (!releaseOwner) throw ownerFailure;
        });
        var game = new PreparingGame(ResourceLoader(lease));
        game.Prepare = (_, _) => owner;
        game.Cleanup = () => { trace.Add("hook"); if (!releaseHook) throw hookFailure; };
        try
        {
            game.LoadSceneWorld(new(LoadFixture.Scene(LoadFixture.IntegerSchema))); active = game.ActiveScene!;
            Assert.Same(hookFailure, Assert.Throws<IOException>(() => game.Dispose()));
            Assert.Equal((1, 0, 0), (game.CleanupCalls, owner.Calls, lease.Calls));
            Assert.True(active.IsReady);
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            releaseHook = true;
            Assert.Same(ownerFailure, Assert.Throws<IOException>(() => game.Dispose()));
            Assert.Equal((2, 1, 0), (game.CleanupCalls, owner.Calls, lease.Calls));
            Assert.Same(active, game.ActiveScene); Assert.True(active.IsReady);
            releaseOwner = true; game.Dispose(); game.Dispose();
            Assert.Equal(new[] { "hook", "hook", "owner", "owner", "lease" }, trace);
            Assert.Equal((2, 2, 1), (game.CleanupCalls, owner.Calls, lease.Calls));
            Assert.Null(game.ActiveScene); Assert.True(active.IsDisposed);
        }
        finally { releaseHook = releaseOwner = true; game.Dispose(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimitiveGame_PrimesPrivateSceneAndRendersOnlyCompletedTicksFromActualSimulation(bool di)
    {
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context => context.UseAetherPrimitiveEcs(options => options.FixedStep = TimeSpan.FromSeconds(.5))).Build();
        await app.StartAsync(); var real = PrimitiveSimulationTests.Factory(app);
        PrimitiveGameSession? game = null; PrimitiveSimulation? simulation = null; var creates = 0;
        var factory = new Factory<PrimitiveSimulation, PrimitiveSimulationRequest>(args =>
        {
            creates++; Assert.Null(game!.ActiveScene); Assert.Equal((-1L, 0UL), (game.Revision, game.TickNumber));
            simulation = real.Create(args);
            Assert.Equal(0UL, simulation.Snapshot.Tick);
            Assert.Equal(args.Scene.World.Id, simulation.Snapshot.WorldId);
            Assert.Equal((args.Scene.Scene.Id, 23L), (simulation.Snapshot.SceneId, simulation.Snapshot.Revision));
            return simulation;
        });
        using (game = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), factory))
        {
            Assert.IsAssignableFrom<IRenderGameSession>(game);
            Assert.Equal(0, creates);
            Assert.Throws<InvalidOperationException>(() => game.CaptureRenderScene());
            Assert.Throws<NotSupportedException>(() => game.Load(new SceneSnapshot("legacy", 0, [])));
            game.LoadSceneWorld(new(PrimitivePipelineFixture.Document(velocity: true)));
            var scene = game.ActiveScene!;
            var initial = game.CaptureRenderScene();
            Assert.Same(initial, game.CaptureRenderScene());
            Assert.Equal(Vector3.Zero, Assert.Single(initial.Primitives).World.Translation);
            Assert.Equal((23L, 3, 0UL, 1), (game.Revision, game.SceneComponentCount, game.TickNumber, creates));
            game.Tick();
            var frame = game.CaptureRenderScene();
            Assert.Same(simulation!.Snapshot.Scene, frame);
            Assert.Equal(new Vector3(3, 0, 0), Assert.Single(frame.Primitives).World.Translation);
            Assert.Equal((1UL, 1UL), (game.TickNumber, simulation.TickNumber));
            Assert.Equal(Vector3.Zero, Assert.Single(initial.Primitives).World.Translation);
            Assert.Throws<InvalidOperationException>(() => scene.Dispose());
            game.Dispose();
            Assert.True(scene.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => simulation.Snapshot);
            Assert.Equal(new Vector3(3, 0, 0), Assert.Single(frame.Primitives).World.Translation);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimitiveGame_CancellationAfterFactoryReturnsDisposesGraphBeforeDiscardingPrivateWorld(bool di)
    {
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context => context.UseAetherPrimitiveEcs()).Build();
        await app.StartAsync(); var real = PrimitiveSimulationTests.Factory(app);
        using var canceled = new CancellationTokenSource(); PrimitiveSimulation? simulation = null; SceneWorld? candidate = null;
        using var game = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), new Factory<PrimitiveSimulation, PrimitiveSimulationRequest>(args =>
        { candidate = args.Scene; simulation = real.Create(args); canceled.Cancel(); return simulation; }));
        Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(new(PrimitivePipelineFixture.Document()), canceled.Token));
        Assert.Null(game.ActiveScene);
        Assert.True(candidate!.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => simulation!.Tick());
        Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimitiveGame_FactoryFailureAfterPrivateInitializationDiscardsCandidateAndCleanRetryActivates(bool di)
    {
        var wrong = true; var candidates = new List<SceneWorld>(); PrimitiveExtractionSystem? failedExtraction = null;
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>>(_ =>
                new Factory<PrimitiveExtractionSystem, PrimitiveExtractionArguments>(args =>
                    failedExtraction = new(wrong ? args with { Revision = args.Revision + 1 } : args))));
            context.UseAetherPrimitiveEcs();
        }).Build();
        await app.StartAsync(); var real = PrimitiveSimulationTests.Factory(app);
        using var game = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), new Factory<PrimitiveSimulation, PrimitiveSimulationRequest>(args =>
        { candidates.Add(args.Scene); return real.Create(args); }));
        var source = new SceneWorldData(PrimitivePipelineFixture.Document());
        Assert.Throws<InvalidDataException>(() => game.LoadSceneWorld(source));
        Assert.Null(game.ActiveScene); Assert.True(candidates[0].IsDisposed);
        Assert.Null(failedExtraction!.Snapshot);
        Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
        wrong = false; game.LoadSceneWorld(source);
        Assert.Same(candidates[1], game.ActiveScene);
        Assert.NotEqual(candidates[0].World.Id, candidates[1].World.Id);
        Assert.Single(game.CaptureRenderScene().Primitives);
        game.Tick(); Assert.Equal(1UL, game.TickNumber);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PrimitiveGame_FailedTickFaultsGameAndRejectsRenderWithoutAdvancingItsTick(bool di)
    {
        await using var app = PrimitiveSimulationTests.Builder(di).Setup(context => context.UseAetherPrimitiveEcs()).Build();
        await app.StartAsync();
        using var game = new PrimitiveGameSession(PrimitivePipelineFixture.Loader(), PrimitiveSimulationTests.Factory(app));
        game.LoadSceneWorld(new(PrimitivePipelineFixture.Document()));
        var previous = game.CaptureRenderScene();
        var scene = game.ActiveScene!;
        scene.World.SetComponent(Assert.Single(scene.Entities.Values), new PrimitiveAppearance(new(2, 0, 0)));
        Assert.Throws<InvalidDataException>(() => game.Tick());
        Assert.True(game.IsFaulted); Assert.Equal(0UL, game.TickNumber);
        Assert.Throws<InvalidOperationException>(() => game.CaptureRenderScene());
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        Assert.Equal(Vector3.One, Assert.Single(previous.Primitives).Color);
    }

    private static SceneWorldLoader ResourceLoader(Resource lease)
    {
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema, context =>
        { context.Acquire(_ => lease); return new LoadFixture.Value(context.GetInt32("value")); }).Build();
        return new(registry, LoadFixture.Worlds(), LoadFixture.Resolution());
    }
    private sealed class Resource(Action? dispose = null) : IDisposable
    { internal int Calls; public void Dispose() { Calls++; dispose?.Invoke(); } }
    private sealed class PreparingGame(SceneWorldLoader loader) : SceneWorldGameSession(loader)
    {
        internal Func<SceneWorld, CancellationToken, IDisposable?>? Prepare;
        internal Action? Cleanup;
        internal int CleanupCalls;
        public override uint PatternColor => 0xff123456;
        protected override IDisposable? PrepareSceneWorld(SceneWorld scene, CancellationToken cancellationToken) => Prepare?.Invoke(scene, cancellationToken);
        protected override void TickWorld(SceneWorld scene, ulong nextTick) { }
        protected override void DisposeGameResources() { CleanupCalls++; Cleanup?.Invoke(); }
    }
}
