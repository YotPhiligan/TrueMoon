using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Ecs.Loading;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Runtime;

public sealed class SceneWorldRuntimeConfigurationTests
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RuntimeRegistration_IsLazyAndProvidesRegistrySpecificLoaders(bool di)
    {
        var worlds = new WorldFactory(); var runtimes = new RuntimeFactory();
        await using var app = CreateApp(di, worlds, runtimes);
        Assert.Equal((0, 0), (worlds.Calls, runtimes.Calls));
        await app.StartAsync();
        var factory = Assert.Single(app.Services.ResolveAll<IFactory<SceneWorldLoader, ComponentRegistry>>());
        Assert.Same(factory, app.Services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>());
        Assert.Empty(app.Services.ResolveAll<ComponentRegistry>());
        Assert.Empty(app.Services.ResolveAll<SceneWorldLoader>());
        Assert.Empty(app.Services.ResolveAll<SceneWorld>());
        Assert.Empty(app.Services.ResolveAll<World>());
        var registry = new ComponentRegistryBuilder().Build();
        var loader = factory.Create(registry);
        Assert.Equal(registry.Id, loader.RegistryId);
        Assert.NotSame(loader, factory.Create(registry));
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Equal((0, 0), (worlds.Calls, runtimes.Calls));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Factory_UsesCallerRegistryWithoutCreatingGlobalBindings(bool di)
    {
        await using var app = CreateApp(di);
        await app.StartAsync();
        Assert.Single(app.Services.ResolveAll<IFactory<World>>());
        var importsA = 0; var importsB = 0;
        var registryA = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema,
            context => { importsA++; return new LoadFixture.Value(context.GetInt32("value") + 5); }).Build();
        var registryB = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema,
            context => { importsB++; return new LoadFixture.Value(context.GetInt32("value") + 19); }).Build();
        var factory = app.Services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>()!;
        var loaderA = factory.Create(registryA); var loaderB = factory.Create(registryB);
        Assert.Equal((registryA.Id, registryB.Id), (loaderA.RegistryId, loaderB.RegistryId));
        Assert.NotEqual(loaderA.RegistryId, loaderB.RegistryId);
        Assert.Equal((0, 0), (importsA, importsB));
        var source = LoadFixture.Scene(LoadFixture.IntegerSchema);
        using var first = loaderA.Load(source);
        using var second = loaderB.Load(source);
        using var third = loaderA.Load(source);
        Assert.Equal((2, 1), (importsA, importsB));
        Assert.Equal(new LoadFixture.Value(12), first.World.GetComponent<LoadFixture.Value>(first.Entities[LoadFixture.First]));
        Assert.Equal(new LoadFixture.Value(26), second.World.GetComponent<LoadFixture.Value>(second.Entities[LoadFixture.First]));
        Assert.Equal(new LoadFixture.Value(12), third.World.GetComponent<LoadFixture.Value>(third.Entities[LoadFixture.First]));
        Assert.NotEqual(first.World.Id, second.World.Id);
        Assert.NotEqual(first.World.Id, third.World.Id);
        Assert.False(first.World.IsAlive(second.Entities[LoadFixture.First]));
        Assert.Empty(app.Services.ResolveAll<ComponentRegistry>());
        Assert.Empty(app.Services.ResolveAll<SceneWorldLoader>());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WorldFactoryOverride_IsBorrowedAndUsedByLoader(bool di)
    {
        var worlds = new WorldFactory(1);
        await using var app = CreateApp(di, worlds);
        await app.StartAsync();
        Assert.Same(worlds, Assert.Single(app.Services.ResolveAll<IFactory<World>>()));
        var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema,
            context => new LoadFixture.Value(context.GetInt32("value"))).Build();
        var loader = app.Services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>()!.Create(registry);
        var failure = Assert.Throws<SceneWorldLoadException>(() => loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema, count: 2)));
        Assert.Equal(SceneWorldLoadError.LimitExceeded, failure.Error);
        Assert.True(Assert.Single(worlds.Products).IsDisposed);
        Assert.Equal((1, 0), (worlds.Calls, worlds.Disposals));
        using (var scene = loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema)))
        {
            Assert.Same(worlds.Products[1], scene.World);
            Assert.Equal(1, scene.World.EntityCount);
            Assert.Equal(new LoadFixture.Value(7), scene.World.GetComponent<LoadFixture.Value>(scene.Entities[LoadFixture.First]));
            Assert.Equal((2, 0), (worlds.Calls, worlds.Disposals));
        }
        Assert.All(worlds.Products, world => Assert.True(world.IsDisposed));
        Assert.Equal(0, worlds.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppDisposal_DoesNotOwnCallerScenesAndDisposesBorrowedDependenciesOnce(bool di)
    {
        var worlds = new WorldFactory(); var dependency = new Dependency();
        var app = CreateApp(di, worlds, configure: context => context.Services(services => services.Singleton<Dependency>(_ => dependency)));
        SceneWorld? first = null, second = null;
        try
        {
            await app.StartAsync();
            var borrowed = app.Services.Resolve<Dependency>()!;
            var registry = new ComponentRegistryBuilder().Register(LoadFixture.IntegerSchema,
                context => new LoadFixture.Value(context.GetInt32("value") * borrowed.Factor)).Build();
            var loader = app.Services.Resolve<IFactory<SceneWorldLoader, ComponentRegistry>>()!.Create(registry);
            first = loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
            second = loader.Load(LoadFixture.Scene(LoadFixture.IntegerSchema));
            first.Dispose();
            Assert.True(first.World.IsDisposed);
            Assert.Equal((0, 0), (worlds.Disposals, dependency.Disposals));
            await app.DisposeAsync();
            Assert.Equal((1, 1), (worlds.Disposals, dependency.Disposals));
            Assert.True(second.IsReady);
            Assert.False(second.World.IsDisposed);
            Assert.Equal(new LoadFixture.Value(21), second.World.GetComponent<LoadFixture.Value>(second.Entities[LoadFixture.First]));
            second.Dispose();
            Assert.True(second.World.IsDisposed);
            await app.DisposeAsync();
            Assert.Equal((1, 1), (worlds.Disposals, dependency.Disposals));
        }
        finally { second?.Dispose(); first?.Dispose(); await app.DisposeAsync(); }
    }

    private static IApp CreateApp(bool di, WorldFactory? worlds = null, RuntimeFactory? runtimes = null,
        Action<IAppConfigurationContext>? configure = null)
    {
        runtimes ??= new RuntimeFactory();
        return App.Builder(context => { if (di) context.UseDI(); }).Setup(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = Hash; options.GameBuildId = Hash;
                options.ReportDirectory = "aether-runtime-registry-tests";
                options.RuntimeFactory = _ => runtimes;
            });
            if (worlds != null) context.Services(services => services.Singleton<IFactory<World>>(_ => worlds));
            configure?.Invoke(context);
            context.UseAetherRuntime(new(new(Guid.NewGuid(), 1, Hash, Hash, Hash),
                "engine-" + Guid.NewGuid().ToString("N"), "aether-runtime-registry-tests",
                Build: new GameBuildReference(Path.GetFullPath("unused-runtime-registry-build"), Hash)), int.MaxValue);
        }).Build();
    }

    private sealed class RuntimeFactory : IRuntimeFactory
    {
        internal int Calls;
        public IRuntimeLease Create(SessionLaunch launch)
        { Calls++; throw new InvalidOperationException("No runtime process should be created by loader registration."); }
    }
    private sealed class WorldFactory(int maximumEntities = 65536) : IFactory<World>, IDisposable
    {
        internal int Calls, Disposals;
        internal List<World> Products { get; } = [];
        public World Create()
        {
            Calls++;
            var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = maximumEntities });
            Products.Add(world); return world;
        }
        public World Create<TData>(TData? data = default) => data is null ? Create() : throw new ArgumentException(nameof(data));
        public void Dispose() => Disposals++;
    }
    private sealed class Dependency : IDisposable
    {
        internal int Disposals;
        internal int Factor => 3;
        public void Dispose() => Disposals++;
    }
}
