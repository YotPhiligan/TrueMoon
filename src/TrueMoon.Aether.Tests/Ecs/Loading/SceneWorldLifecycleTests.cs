using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class SceneWorldLifecycleTests
{
    private static readonly ComponentTypeId TypeId = new(new Guid("2396bcfe-5a38-4a83-96cd-000000000001"));
    private static readonly ComponentId ComponentId = new(new Guid("2396bcfe-5a38-4a83-96cd-000000000002"));
    private static readonly EntityId EntityId = new(new Guid("2396bcfe-5a38-4a83-96cd-000000000003"));
    private static readonly ComponentSchema Schema = new(TypeId, 1, []);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SceneDisposal_PreservesResourcesUntilActiveQueryOrSchedulerCompletes(bool schedulerActive)
    {
        var trace = new List<string>(); World? captured = null;
        var lease = new Lease(() => { Assert.True(captured!.IsDisposed); trace.Add("release"); });
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(11); }).Build();
        using var scene = Loader(registry, () => captured = new World()).Load(Document());
        var world = scene.World; var handle = scene.Entities[EntityId];
        Assert.Throws<InvalidOperationException>(() => world.Dispose());
        Assert.True(scene.IsReady);
        if (schedulerActive)
        {
            using var scheduler = EcsScheduler.Create(world, []);
            Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
            Assert.Throws<InvalidOperationException>(() => scene.Dispose());
            Assert.True(scene.IsReady);
            Assert.False(scene.IsDisposed);
            Assert.False(world.IsDisposed);
            Assert.Equal((1, 0), (scene.PreparedResourceCount, lease.Disposals));
            Assert.Equal(new Value(11), world.GetComponent<Value>(handle));
            scheduler.Dispose();
        }
        else
        {
            var visits = 0;
            world.Query<Value>().ForEach((EntityHandle entity, scoped in Value value) =>
            {
                visits++;
                Assert.Throws<InvalidOperationException>(() => scene.Dispose());
                Assert.True(scene.IsReady);
                Assert.Equal((1, 0), (scene.PreparedResourceCount, lease.Disposals));
                Assert.Equal(11, value.Number);
            });
            Assert.Equal(1, visits);
        }
        Assert.Empty(trace);
        Assert.True(world.IsAlive(handle));
        scene.Dispose(); scene.Dispose();
        Assert.True(scene.IsDisposed);
        Assert.False(scene.IsReady);
        Assert.True(world.IsDisposed);
        Assert.Equal((0, 1), (scene.PreparedResourceCount, lease.Disposals));
        Assert.Equal("release", Assert.Single(trace));
        Assert.Throws<ObjectDisposedException>(() => world.IsAlive(handle));
    }

    [Fact]
    public void PrivateLoadingWorld_RejectsExternalWritesDisposalAndSchedulerBeforePublication()
    {
        World? captured = null; var imports = 0;
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        {
            imports++;
            var world = captured!;
            Assert.Equal(1, world.EntityCount);
            Assert.Equal(0, world.ComponentTypeCount);
            Assert.True(world.IsAlive(context.Entity));
            Assert.False(world.HasComponent<Value>(context.Entity));
            Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
            Assert.Throws<InvalidOperationException>(() => world.DestroyEntity(context.Entity));
            Assert.Throws<InvalidOperationException>(() => world.AddComponent(context.Entity, new Value(-1)));
            Assert.Throws<InvalidOperationException>(() => world.SetComponent(context.Entity, new Value(-1)));
            Assert.Throws<InvalidOperationException>(() => world.RemoveComponent<Value>(context.Entity));
            Assert.Throws<InvalidOperationException>(() => world.Query<Value>().ForEachWrite((EntityHandle e, scoped ref Value value) => value = new(-1)));
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            Assert.Throws<InvalidOperationException>(() => EcsScheduler.Create(world, []));
            return new Value(17);
        }).Build();
        using var scene = Loader(registry, () => captured = new World()).Load(Document());
        var handle = scene.Entities[EntityId];
        Assert.True(scene.IsReady);
        Assert.Equal(1, imports);
        Assert.Equal(new Value(17), scene.World.GetComponent<Value>(handle));
        scene.World.SetComponent(handle, new Value(19));
        Assert.Equal(new Value(19), scene.World.GetComponent<Value>(handle));
        using var scheduler = EcsScheduler.Create(scene.World, []);
        Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public void ResourceDisposal_RejectsReentrancyWithoutDoubleReleaseAndOwnerRemainsIdempotent()
    {
        SceneWorld? scene = null; var attempts = 0;
        var lease = new Lease(() =>
        {
            attempts++;
            Assert.True(scene!.World.IsDisposed);
            Assert.Throws<InvalidOperationException>(() => scene.Dispose());
            Assert.False(scene.IsDisposed);
        });
        var registry = new ComponentRegistryBuilder().Register(Schema, context =>
        { context.Acquire(_ => lease); return new Value(23); }).Build();
        scene = Loader(registry, () => new World()).Load(Document());
        try
        {
            scene.Dispose(); scene.Dispose();
            Assert.Equal((1, 1), (lease.Disposals, attempts));
            Assert.True(scene.IsDisposed);
            Assert.Equal(0, scene.PreparedResourceCount);
        }
        finally { scene.Dispose(); }
    }

    private static SceneWorldLoader Loader(ComponentRegistry registry, Func<World> create) => new(registry,
        new Factory<World>(create), new SceneResolutionService(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas))));
    private static SceneDocument Document()
    {
        var definition = new EntityDefinitionId(new Guid("2396bcfe-5a38-4a83-96cd-000000000004"));
        return new(new(new Guid("2396bcfe-5a38-4a83-96cd-000000000005")), 7,
            [new EntityInstance(EntityId, definition)], [new(definition, new([new ComponentData(ComponentId, TypeId, 1)]))], [Schema]);
    }
    private readonly record struct Value(int Number);
    private sealed class Lease(Action dispose) : IDisposable
    {
        internal int Disposals;
        public void Dispose() { Disposals++; dispose(); }
    }
}
