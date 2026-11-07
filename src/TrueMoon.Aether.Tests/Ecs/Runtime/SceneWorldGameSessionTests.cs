using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Runtime;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneWorldGameSessionTests
{
    private static readonly ComponentTypeId TypeId = new(Guid.Parse("56cd30ed-af4b-4a71-8444-000000000001"));
    private static readonly ComponentId ComponentId = new(Guid.Parse("56cd30ed-af4b-4a71-8444-000000000002"));
    private static readonly EntityDefinitionId DefinitionId = new(Guid.Parse("56cd30ed-af4b-4a71-8444-000000000003"));
    private static readonly ComponentSchema Schema = new(TypeId, 1,
        [new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(7L))]);

    [Fact]
    public void Constructor_ExposesRegistryWithoutCreatingWorldOrLoadingScene()
    {
        var registry = Registry(); var worlds = new WorldFactory();
        using var game = new ProbeGame(Loader(registry, worlds));
        Assert.Equal(registry.Id, game.RegistryId);
        Assert.NotEqual(Guid.Empty, game.RegistryId);
        Assert.Null(game.ActiveScene);
        Assert.Equal((-1L, 0UL, 0, false), (game.Revision, game.TickNumber, game.SceneComponentCount, game.IsFaulted));
        Assert.Equal(0xff123456u, game.PatternColor);
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        Assert.Equal((0, 0), (worlds.Calls, game.Executions));
        Assert.Throws<ArgumentNullException>(() => new ProbeGame(null!));
    }

    [Fact]
    public void LoadSceneWorld_PublishesOnlyCompleteCandidateAndOwnsItsLifetime()
    {
        var worlds = new WorldFactory(); ProbeGame? game = null; var imports = 0;
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        {
            imports++;
            Assert.Null(game!.ActiveScene);
            Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
            var privateWorld = Assert.Single(worlds.Products);
            Assert.Equal(2, privateWorld.EntityCount);
            Assert.Equal(0, privateWorld.ComponentTypeCount);
            return new Value(context.GetInt32("value"));
        }).Build();
        using (game = new ProbeGame(Loader(registry, worlds)))
        {
            var data = Data(2);
            game.LoadSceneWorld(data);
            var scene = Assert.IsType<SceneWorld>(game.ActiveScene);
            Assert.True(scene.IsReady);
            Assert.Equal((13L, 0UL, 2), (game.Revision, game.TickNumber, game.SceneComponentCount));
            Assert.Equal((1, 2), (worlds.Calls, imports));
            Assert.Equal(2, scene.Entities.Count);
            foreach (var handle in scene.Entities.Values) Assert.Equal(new Value(7), scene.World.GetComponent<Value>(handle));
            Assert.Equal(data.Scene.Id, scene.Scene.Id);
            Assert.Throws<InvalidOperationException>(() => scene.World.Dispose());
        }
        Assert.True(Assert.Single(worlds.Products).IsDisposed);
        Assert.Null(game.ActiveScene);
        Assert.Equal(0, worlds.Disposals);
    }

    [Fact]
    public void Tick_ExecutesBorrowedWorldAndPublishesOnlyCompletedTick()
    {
        var worlds = new WorldFactory();
        using var game = new ProbeGame(Loader(Registry(), worlds));
        game.LoadSceneWorld(Data(2));
        var active = game.ActiveScene!; var observations = new List<(ulong Before, ulong Next)>();
        game.OnTick = (scene, next) =>
        {
            Assert.Same(active, scene);
            observations.Add((game.TickNumber, next));
            scene.World.Query<Value>().ForEachWrite((EntityHandle entity, scoped ref Value value) => value = new(value.Number + (int)next));
        };
        game.Tick(); game.Tick();
        Assert.Equal(new[] { (0UL, 1UL), (1UL, 2UL) }, observations);
        Assert.Equal((2UL, 2, 13L), (game.TickNumber, game.Executions, game.Revision));
        foreach (var handle in active.Entities.Values) Assert.Equal(new Value(10), active.World.GetComponent<Value>(handle));
        var spawned = active.World.CreateEntity(); active.World.AddComponent(spawned, new Value(19));
        Assert.Equal((3, 2, 2), (active.World.EntityCount, active.Entities.Count, game.SceneComponentCount));
        Assert.All(active.Scene.Entities.Values, entity =>
            Assert.Equal(7L, entity.Components[ComponentId].Properties["value"].Value.GetInteger()));
        Assert.False(game.IsFaulted);
    }

    [Fact]
    public void LegacyLoad_IsExplicitlyUnsupportedWithoutChangingActivation()
    {
        var worlds = new WorldFactory();
        using var game = new ProbeGame(Loader(Registry(), worlds));
        Assert.Throws<NotSupportedException>(() => game.Load(new SceneSnapshot("legacy", 7, [])));
        Assert.Null(game.ActiveScene);
        Assert.Equal((0, -1L), (worlds.Calls, game.Revision));
        game.LoadSceneWorld(Data());
        var original = game.ActiveScene;
        Assert.Throws<NotSupportedException>(() => game.Load(new SceneSnapshot("legacy", 8, [])));
        Assert.Same(original, game.ActiveScene);
        Assert.Equal((1, 13L), (worlds.Calls, game.Revision));
    }

    [Fact]
    public void RepeatedLoad_IsRejectedWithoutCreatingOrReplacingWorld()
    {
        var worlds = new WorldFactory(); var imports = 0;
        using var game = new ProbeGame(Loader(new ComponentRegistryBuilder().Register(Schema,
            context => { imports++; return new Value(context.GetInt32("value")); }).Build(), worlds));
        game.LoadSceneWorld(Data());
        var active = game.ActiveScene!;
        Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(Data(3)));
        Assert.Same(active, game.ActiveScene);
        Assert.True(active.IsReady);
        Assert.Equal((1, 1, 1, 13L), (worlds.Calls, imports, active.World.EntityCount, game.Revision));
        game.Tick();
        Assert.Equal(1UL, game.TickNumber);
    }

    [Fact]
    public void RejectedSchema_DoesNotPublishOrPoisonCleanInitialLoadRetry()
    {
        var worlds = new WorldFactory();
        using var game = new ProbeGame(Loader(new ComponentRegistryBuilder().Build(), worlds));
        var error = Assert.Throws<SceneWorldLoadException>(() => game.LoadSceneWorld(Data()));
        Assert.Equal(SceneWorldLoadError.UnknownComponent, error.Error);
        Assert.Null(game.ActiveScene);
        Assert.Equal((-1L, 0UL, 0, 0), (game.Revision, game.TickNumber, game.SceneComponentCount, worlds.Calls));
        Assert.False(game.IsFaulted);
        game.LoadSceneWorld(new(new SceneDocument(new(Id(99)), 17, [], [], [])));
        Assert.True(game.ActiveScene!.IsReady);
        Assert.Equal((17L, 0, 1), (game.Revision, game.SceneComponentCount, worlds.Calls));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FailedPreparation_DoesNotPublishAndCleansWorldAndReturnedLease(bool cancel)
    {
        var worlds = new WorldFactory(); var lease = new Lease(); var expected = new IOException("import fixture");
        using var cancellation = new CancellationTokenSource();
        var registry = new ComponentRegistryBuilder().Register<Value>(Schema, context =>
        {
            context.Acquire(_ => lease);
            if (!cancel) throw expected;
            cancellation.Cancel();
            return new Value(23);
        }).Build();
        using var game = new ProbeGame(Loader(registry, worlds));
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(Data(), cancellation.Token)).CancellationToken);
        else
        {
            var error = Assert.Throws<SceneWorldLoadException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal(SceneWorldLoadError.ImportFailed, error.Error);
            Assert.Same(expected, error.InnerException);
        }
        Assert.Null(game.ActiveScene);
        Assert.Equal((-1L, 0UL, 0), (game.Revision, game.TickNumber, game.SceneComponentCount));
        Assert.True(Assert.Single(worlds.Products).IsDisposed);
        Assert.Equal(1, lease.Disposals);
        Assert.False(game.IsFaulted);
    }

    [Fact]
    public void PreCancellation_DoesNotCreateWorldAndExplicitRetryCanActivate()
    {
        var worlds = new WorldFactory();
        using var game = new ProbeGame(Loader(Registry(), worlds));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => game.LoadSceneWorld(Data(), cancellation.Token));
        Assert.Equal(0, worlds.Calls);
        Assert.Null(game.ActiveScene);
        game.LoadSceneWorld(Data());
        Assert.True(game.ActiveScene!.IsReady);
        Assert.Equal((1, 13L), (worlds.Calls, game.Revision));
    }

    [Fact]
    public void FailedPreparationCleanup_RemainsOwnedAndRetriesOnlyFailedResources()
    {
        var worlds = new WorldFactory(); var release = false;
        var original = new IOException("import fixture"); var cleanup = new IOException("cleanup fixture");
        var successful = new Lease(); var failing = new Lease(() => { if (!release) throw cleanup; });
        var registry = new ComponentRegistryBuilder().Register<Value>(Schema, context =>
        { context.Acquire(_ => successful); context.Acquire(_ => failing); throw original; }).Build();
        var game = new ProbeGame(Loader(registry, worlds));
        try
        {
            var error = Assert.Throws<SceneWorldLoadException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal(SceneWorldLoadError.CleanupFailed, error.Error);
            var owner = Assert.IsType<SceneWorld>(error.CleanupOwner);
            Assert.True(owner.World.IsDisposed);
            Assert.False(owner.IsDisposed);
            Assert.Null(game.ActiveScene);
            Assert.Equal((1, 1), (successful.Disposals, failing.Disposals));
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal(1, worlds.Calls);
            Assert.Throws<AggregateException>(() => game.Dispose());
            Assert.Equal((1, 2, 1), (successful.Disposals, failing.Disposals, game.CleanupCalls));
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal((0, 1), (game.Executions, worlds.Calls));
            release = true;
            game.Dispose(); game.Dispose();
            Assert.True(owner.IsDisposed);
            Assert.Equal((1, 3, 1), (successful.Disposals, failing.Disposals, game.CleanupCalls));
            Assert.Throws<ObjectDisposedException>(() => game.LoadSceneWorld(Data()));
        }
        finally { release = true; game.Dispose(); }
    }

    [Fact]
    public void ActiveSceneDisposalFailure_RetainsOwnerAndSkipsSuccessfulDerivedCleanupOnRetry()
    {
        var release = false; var expected = new IOException("active cleanup fixture");
        var lease = new Lease(() => { if (!release) throw expected; });
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(29); }).Build();
        var game = new ProbeGame(Loader(registry, new WorldFactory()));
        try
        {
            game.LoadSceneWorld(Data()); var scene = game.ActiveScene!;
            var error = Assert.Throws<AggregateException>(() => game.Dispose());
            Assert.Contains(expected, error.InnerExceptions);
            Assert.Same(scene, game.ActiveScene);
            Assert.True(scene.World.IsDisposed);
            Assert.False(scene.IsReady);
            Assert.Equal((1, 1), (lease.Disposals, game.CleanupCalls));
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal((0, 0UL), (game.Executions, game.TickNumber));
            release = true; game.Dispose(); game.Dispose();
            Assert.Null(game.ActiveScene);
            Assert.True(scene.IsDisposed);
            Assert.Equal((2, 1), (lease.Disposals, game.CleanupCalls));
        }
        finally { release = true; game.Dispose(); }
    }

    [Fact]
    public void DerivedCleanup_ReleasesOwnedSchedulerBeforeSceneAndRunsOnce()
    {
        var trace = new List<string>(); EcsScheduler? scheduler = null; World? world = null;
        var lease = new Lease(() =>
        { Assert.True(world!.IsDisposed); Assert.Equal(EcsSchedulerState.Disposed, scheduler!.State); trace.Add("lease"); });
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(31); }).Build();
        var game = new ProbeGame(Loader(registry, new WorldFactory()));
        game.OnTick = (scene, _) =>
        {
            world = scene.World; scheduler ??= EcsScheduler.Create(world, []);
            Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
        };
        game.OnCleanup = () =>
        { Assert.False(world!.IsDisposed); scheduler!.Dispose(); trace.Add("game"); };
        try
        {
            game.LoadSceneWorld(Data()); game.Tick();
            var active = game.ActiveScene!;
            game.Dispose(); game.Dispose();
            Assert.Equal(new[] { "game", "lease" }, trace);
            Assert.True(active.IsDisposed);
            Assert.Equal((1, 1), (game.CleanupCalls, lease.Disposals));
        }
        finally { scheduler?.Dispose(); game.Dispose(); }
    }

    [Fact]
    public void FailedDerivedCleanup_PreservesWorldAndRetriesHookBeforeSceneRelease()
    {
        var release = false; var expected = new IOException("derived cleanup fixture");
        var lease = new Lease();
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(37); }).Build();
        var game = new ProbeGame(Loader(registry, new WorldFactory()));
        game.OnCleanup = () => { if (!release) throw expected; };
        try
        {
            game.LoadSceneWorld(Data()); var scene = game.ActiveScene!;
            Assert.Same(expected, Assert.Throws<IOException>(() => game.Dispose()));
            Assert.True(scene.IsReady);
            Assert.False(scene.World.IsDisposed);
            Assert.Equal(new Value(37), scene.World.GetComponent<Value>(Assert.Single(scene.Entities.Values)));
            Assert.Equal((1, 0), (game.CleanupCalls, lease.Disposals));
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Throws<InvalidOperationException>(() => game.LoadSceneWorld(Data()));
            Assert.Equal((0, 0UL), (game.Executions, game.TickNumber));
            release = true; game.Dispose(); game.Dispose();
            Assert.True(scene.World.IsDisposed);
            Assert.Equal((2, 1), (game.CleanupCalls, lease.Disposals));
        }
        finally { release = true; game.Dispose(); }
    }

    [Fact]
    public void SessionDisposal_PreservesSceneResourcesUntilBorrowedSchedulerCompletes()
    {
        var lease = new Lease();
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(41); }).Build();
        var game = new ProbeGame(Loader(registry, new WorldFactory()));
        EcsScheduler? scheduler = null;
        try
        {
            game.LoadSceneWorld(Data()); var scene = game.ActiveScene!;
            scheduler = EcsScheduler.Create(scene.World, []);
            Assert.Throws<InvalidOperationException>(() => game.Dispose());
            Assert.True(scene.IsReady);
            Assert.False(scene.World.IsDisposed);
            Assert.Equal((1, 0), (game.CleanupCalls, lease.Disposals));
            Assert.Throws<InvalidOperationException>(() => game.Tick());
            Assert.Equal(0, game.Executions);
            scheduler.Dispose(); game.Dispose();
            Assert.True(scene.IsDisposed);
            Assert.Equal((1, 1), (game.CleanupCalls, lease.Disposals));
        }
        finally { scheduler?.Dispose(); game.Dispose(); }
    }

    [Fact]
    public void ReentrantOperations_AreRejectedDuringPreparationAndTick()
    {
        ProbeGame? game = null; var imports = 0; var worlds = new WorldFactory(); var data = Data();
        void Reject()
        {
            Assert.Throws<InvalidOperationException>(() => game!.LoadSceneWorld(data));
            Assert.Throws<InvalidOperationException>(() => game!.Tick());
            Assert.Throws<InvalidOperationException>(() => game!.Dispose());
        }
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { imports++; Reject(); return new Value(43); }).Build();
        using (game = new ProbeGame(Loader(registry, worlds)))
        {
            game.LoadSceneWorld(data);
            game.OnTick = (_, _) => Reject();
            game.Tick();
            Assert.Equal((1, 1, 1UL, 0), (imports, worlds.Calls, game.TickNumber, game.CleanupCalls));
            Assert.True(game.ActiveScene!.IsReady);
            Assert.False(game.IsFaulted);
        }
        Assert.Equal(1, game.CleanupCalls);
    }

    [Fact]
    public void FailedTick_IsTerminalAndPreservesPartialDataForInspectionWithoutAdvancingTick()
    {
        using var game = new ProbeGame(Loader(Registry(), new WorldFactory()));
        game.LoadSceneWorld(Data()); var scene = game.ActiveScene!;
        var expected = new IOException("tick fixture"); var entity = Assert.Single(scene.Entities.Values);
        game.OnTick = (active, next) =>
        { Assert.Equal(1UL, next); active.World.SetComponent(entity, new Value(47)); throw expected; };
        Assert.Same(expected, Assert.Throws<IOException>(() => game.Tick()));
        Assert.Equal((0UL, 1, true), (game.TickNumber, game.Executions, game.IsFaulted));
        Assert.Equal(new Value(47), scene.World.GetComponent<Value>(entity));
        Assert.True(scene.World.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        Assert.Throws<InvalidOperationException>(() => scene.World.SetComponent(entity, new Value(-1)));
        Assert.Equal(1, game.Executions);
    }

    [Fact]
    public void Dispose_IsIdempotentAndDoesNotDisposeBorrowedFactoryDependencies()
    {
        var worlds = new WorldFactory(); var game = new ProbeGame(Loader(Registry(), worlds));
        Assert.Throws<ArgumentNullException>(() => game.LoadSceneWorld(null!));
        Assert.Null(game.ActiveScene);
        game.LoadSceneWorld(Data()); var scene = game.ActiveScene!; var registry = game.RegistryId;
        game.Dispose(); game.Dispose();
        Assert.True(scene.IsDisposed);
        Assert.Null(game.ActiveScene);
        Assert.Equal((1, 0), (game.CleanupCalls, worlds.Disposals));
        Assert.Equal(registry, game.RegistryId);
        Assert.Throws<ObjectDisposedException>(() => game.Tick());
        Assert.Throws<ObjectDisposedException>(() => game.LoadSceneWorld(Data()));
        worlds.Dispose(); Assert.Equal(1, worlds.Disposals);
    }

    private static ComponentRegistry Registry() => new ComponentRegistryBuilder().Register(Schema,
        context => new Value(context.GetInt32("value"))).Build();
    private static SceneWorldLoader Loader(ComponentRegistry registry, IFactory<World> worlds) => new(registry, worlds,
        new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas))));
    private static SceneWorldData Data(int entities = 1) => new(new SceneDocument(new(Id(99)), 13,
        Enumerable.Range(1, entities).Select(index => new EntityInstance(new(Id(100 + index)), DefinitionId)),
        [new(DefinitionId, new([new ComponentData(ComponentId, TypeId, 1)]))], [Schema]));
    private static Guid Id(int value) => new($"56cd30ed-af4b-4a71-8444-{value:000000000000}");
    private readonly record struct Value(int Number);
    private sealed class Lease(Action? dispose = null) : IDisposable
    {
        internal int Disposals;
        public void Dispose() { Disposals++; dispose?.Invoke(); }
    }
    private sealed class WorldFactory : IFactory<World>, IDisposable
    {
        internal int Calls, Disposals;
        internal List<World> Products { get; } = [];
        public World Create() { Calls++; var world = new World(); Products.Add(world); return world; }
        public World Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException(nameof(data));
        public void Dispose() => Disposals++;
    }
    private sealed class ProbeGame(SceneWorldLoader loader) : SceneWorldGameSession(loader)
    {
        internal int Executions, CleanupCalls;
        internal Action<SceneWorld, ulong>? OnTick;
        internal Action? OnCleanup;
        public override uint PatternColor => 0xff123456;
        protected override void TickWorld(SceneWorld scene, ulong nextTick) { Executions++; OnTick?.Invoke(scene, nextTick); }
        protected override void DisposeGameResources() { CleanupCalls++; OnCleanup?.Invoke(); }
    }
}
