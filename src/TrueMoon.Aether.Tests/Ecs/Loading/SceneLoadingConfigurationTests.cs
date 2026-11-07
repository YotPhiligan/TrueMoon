using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

public sealed class SceneLoadingConfigurationTests
{
    private static readonly ComponentTypeId TypeId = new(Id(1));
    private static readonly ComponentId ComponentId = new(Id(2));
    private static readonly EntityDefinitionId DefinitionId = new(Id(3));
    private static readonly ComponentSchema Schema = new(TypeId, 1,
        [new("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Registration_IsLazyFreezesOptionsAndRoundTripsIndependentCallerOwnedWorlds(bool di)
    {
        var registryCalls = 0; var imports = 0; var dependencyActivations = 0;
        var dependency = new Dependency(2);
        var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<Dependency>(_ => { dependencyActivations++; return dependency; }));
            SceneWorldLoadOptions captured = null!;
            Assert.Same(context, context.UseAetherSceneLoading(resolver =>
            {
                registryCalls++;
                var borrowed = resolver.Resolve<Dependency>()!;
                return new ComponentRegistryBuilder().Register(Schema, data =>
                { imports++; return new CountValue(data.GetInt32("value") * borrowed.Factor); }).Build();
            }, options => { captured = options; options.MaximumEntities = 3; }));
            captured.MaximumEntities = 1; captured.MaximumPreparedResources = -1;
        }).Build();
        SceneWorld? first = null, second = null;
        try
        {
            await app.StartAsync();
            Assert.Equal((0, 0, 0), (registryCalls, imports, dependencyActivations));
            Assert.Empty(app.Services.ResolveAll<World>());
            Assert.Empty(app.Services.ResolveAll<SceneWorld>());
            Assert.Empty(app.Services.ResolveAll<AetherHost>());
            var codec = Assert.Single(app.Services.ResolveAll<SceneWorldCodec>());
            Assert.Same(codec, app.Services.Resolve<SceneWorldCodec>());
            Assert.Empty(app.Services.ResolveAll<SceneFileCodec>());
            var source = Document(2);
            var decoded = codec.Decode(codec.Encode(new(source)));
            Assert.Equal((0, 0, 0), (registryCalls, imports, dependencyActivations));

            var loader = Assert.Single(app.Services.ResolveAll<SceneWorldLoader>());
            var factory = Assert.Single(app.Services.ResolveAll<IFactory<SceneWorld, SceneWorldLoadRequest>>());
            Assert.Same(loader, factory);
            Assert.Same(loader, app.Services.Resolve<SceneWorldLoader>());
            Assert.Same(app.Services.Resolve<ComponentRegistry>(), Assert.Single(app.Services.ResolveAll<ComponentRegistry>()));
            Assert.Equal((1, 0, 1), (registryCalls, imports, dependencyActivations));
            first = factory.Create(new(decoded)); second = loader.Load(decoded.Scene);
            Assert.NotEqual(first.World.Id, second.World.Id);
            Assert.Equal((1, 4, 1), (registryCalls, imports, dependencyActivations));
            Assert.Equal((2, 2), (first.World.EntityCount, second.World.EntityCount));
            foreach (var entity in source.Entities)
            {
                var expected = checked((int)entity.Changes.PropertyOverrides.Single().Value.GetInteger() * 2);
                var firstHandle = first.Entities[entity.Id]; var secondHandle = second.Entities[entity.Id];
                Assert.Equal(new CountValue(expected), first.World.GetComponent<CountValue>(firstHandle));
                Assert.Equal(new CountValue(expected), second.World.GetComponent<CountValue>(secondHandle));
                Assert.False(first.World.IsAlive(secondHandle));
                Assert.Equal(entity.Name, first.Scene.Entities[entity.Id].Name);
            }
            Assert.Equal((source.Id, source.Revision), (first.Scene.Id, first.Scene.Revision));
            first.Dispose();
            Assert.True(first.World.IsDisposed);
            Assert.True(second.IsReady);
            Assert.False(second.World.IsDisposed);
            Assert.Equal(0, dependency.Disposals);
            second.Dispose();
            await app.DisposeAsync();
            Assert.Equal(1, dependency.Disposals);
        }
        finally { second?.Dispose(); first?.Dispose(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppDisposal_DoesNotOwnSceneProductsWithoutBorrowedResources(bool di)
    {
        var app = Builder(di).Setup(context =>
        { context.UseAetherEcs(); context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Build()); }).Build();
        SceneWorld? scene = null;
        try
        {
            await app.StartAsync();
            scene = app.Services.Resolve<SceneWorldLoader>()!.Load(new SceneDocument(new(Id(20)), 0, [], [], []));
            var world = scene.World;
            await app.DisposeAsync();
            Assert.True(scene.IsReady);
            Assert.False(scene.IsDisposed);
            Assert.False(world.IsDisposed);
            Assert.True(world.IsAlive(world.CreateEntity()));
            scene.Dispose(); scene.Dispose();
            Assert.True(scene.IsDisposed);
            Assert.True(world.IsDisposed);
        }
        finally { scene?.Dispose(); await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MissingEcsRegistration_FailsBeforeRegistryActivation(bool di)
    {
        var calls = 0;
        var builder = Builder(di).Setup(context => context.UseAetherSceneLoading(_ =>
        { calls++; return new ComponentRegistryBuilder().Build(); }));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, "module")] [InlineData(true, "module")]
    [InlineData(false, "registry")] [InlineData(true, "registry")]
    [InlineData(false, "loader")] [InlineData(true, "loader")]
    [InlineData(false, "factory")] [InlineData(true, "factory")]
    [InlineData(false, "codec")] [InlineData(true, "codec")]
    public void DuplicateOrCollidingRegistrations_AreRejectedWithoutActivatingOverrides(bool di, string collision)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            if (collision == "module") context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Build());
            else context.Services(services =>
            {
                switch (collision)
                {
                    case "registry": services.Singleton<ComponentRegistry>(_ => { activations++; throw new IOException("inactive registry"); }); break;
                    case "loader": services.Singleton<SceneWorldLoader>(_ => { activations++; throw new IOException("inactive loader"); }); break;
                    case "factory": services.Singleton<IFactory<SceneWorld, SceneWorldLoadRequest>>(_ => { activations++; throw new IOException("inactive factory"); }); break;
                    case "codec": services.Singleton<SceneWorldCodec>(_ => { activations++; throw new IOException("inactive codec"); }); break;
                }
            });
            context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Build());
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, activations);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InvalidOptionsAndConfigureFailure_AreReportedBeforeRegistryActivation(bool di)
    {
        var calls = 0;
        var invalid = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherSceneLoading(_ => { calls++; return new ComponentRegistryBuilder().Build(); }, o => o.MaximumEntities = 0);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => invalid.Build());
        var expected = new IOException("configuration fixture");
        var failing = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherSceneLoading(_ => { calls++; return new ComponentRegistryBuilder().Build(); }, _ => throw expected);
        });
        Assert.Same(expected, Assert.Throws<IOException>(() => failing.Build()));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task RegistryFactoryFailure_IsLazyAndPreservesOriginalCause(bool di, bool returnsNull)
    {
        var calls = 0; var expected = new IOException("registry fixture");
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherSceneLoading(_ => { calls++; return returnsNull ? null! : throw expected; });
        }).Build();
        await app.StartAsync();
        Assert.Equal(0, calls);
        if (returnsNull) Assert.Throws<InvalidOperationException>(() => app.Services.Resolve<ComponentRegistry>());
        else Assert.Same(expected, Assert.Throws<IOException>(() => app.Services.Resolve<ComponentRegistry>()));
        Assert.Equal(1, calls);
        Assert.Empty(app.Services.ResolveAll<SceneWorld>());
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task Codec_UsesExistingSceneCodecOrAuthoringFactoryWithoutReplacingIt(bool di, bool existingCodec)
    {
        var validators = new ValidationFactory(new SceneAuthoringSessionFactory(Resolution(), new SceneAuthoringOptions()));
        var sceneCodec = new SceneFileCodec(validators);
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services =>
            {
                if (existingCodec) services.Singleton<SceneFileCodec>(_ => sceneCodec);
                else services.Singleton<IFactory<ISceneAuthoringSession, SceneDocument>>(_ => validators);
            });
            context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Build());
        }).Build();
        await app.StartAsync();
        var codec = Assert.IsType<SceneWorldCodec>(app.Services.Resolve<SceneWorldCodec>());
        Assert.Equal(0, validators.Calls);
        if (existingCodec) Assert.Same(sceneCodec, Assert.Single(app.Services.ResolveAll<SceneFileCodec>()));
        else Assert.Same(validators, Assert.Single(app.Services.ResolveAll<IFactory<ISceneAuthoringSession, SceneDocument>>()));
        var source = Document(1);
        var encoded = codec.Encode(new(source));
        var roundtrip = codec.Decode(encoded);
        Assert.Equal(2, validators.Calls);
        Assert.Equal((source.Id, source.Revision), (roundtrip.Scene.Id, roundtrip.Scene.Revision));
        Assert.Equal(source.Entities.Single().Name, roundtrip.Scene.Entities.Single().Name);
        Assert.Equal(41L, roundtrip.Scene.Entities.Single().Changes.PropertyOverrides.Single().Value.GetInteger());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IndependentApps_KeepRegistryBindingsFactoriesAndLifetimeSeparate(bool di)
    {
        var firstImports = 0; var secondImports = 0;
        await using var firstApp = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Register(Schema,
                data => { firstImports++; return new CountValue(data.GetInt32("value") + 1); }).Build());
        }).Build();
        await using var secondApp = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherSceneLoading(_ => new ComponentRegistryBuilder().Register(Schema,
                data => { secondImports++; return new CountValue(data.GetInt32("value") + 2); }).Build());
        }).Build();
        await firstApp.StartAsync(); await secondApp.StartAsync();
        var firstRegistry = Assert.IsType<ComponentRegistry>(firstApp.Services.Resolve<ComponentRegistry>());
        var secondRegistry = Assert.IsType<ComponentRegistry>(secondApp.Services.Resolve<ComponentRegistry>());
        Assert.NotEqual(firstRegistry.Id, secondRegistry.Id);
        var firstFactory = firstApp.Services.Resolve<IFactory<SceneWorld, SceneWorldLoadRequest>>()!;
        var secondFactory = secondApp.Services.Resolve<IFactory<SceneWorld, SceneWorldLoadRequest>>()!;
        Assert.NotSame(firstFactory, secondFactory);
        var source = Document(1); var id = source.Entities.Single().Id;
        using var first = firstFactory.Create(new(new(source)));
        using var second = secondFactory.Create(new(new(source)));
        Assert.Equal(new CountValue(42), first.World.GetComponent<CountValue>(first.Entities[id]));
        Assert.Equal(new CountValue(43), second.World.GetComponent<CountValue>(second.Entities[id]));
        Assert.Equal((1, 1), (firstImports, secondImports));
        first.Dispose(); await firstApp.DisposeAsync();
        Assert.True(second.IsReady);
        Assert.False(second.World.IsDisposed);
        Assert.Equal(new CountValue(43), second.World.GetComponent<CountValue>(second.Entities[id]));
    }

    [Fact]
    public void NullContextAndRegistryFactory_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => EcsSceneLoadingConfigurationExtensions.UseAetherSceneLoading(null!, _ => new ComponentRegistryBuilder().Build()));
        var builder = Builder(false).Setup(context => { context.UseAetherEcs(); context.UseAetherSceneLoading(null!); });
        Assert.Throws<ArgumentNullException>(() => builder.Build());
    }

    private static SceneDocument Document(int count)
    {
        var definition = new EntityDefinition(DefinitionId, new([new ComponentData(ComponentId, TypeId, 1)]));
        var entities = Enumerable.Range(0, count).Select(i => new EntityInstance(new(Id(100 + i)), DefinitionId,
            new(propertyOverrides: [new(ComponentId, "value", PropertyValue.From(41L + i))]), null, "Сущность " + i, i));
        return new(new(Id(4)), 7, entities, [definition], [Schema]);
    }
    private static SceneResolutionService Resolution() => new(new Factory<EntityResolver, SceneDocument>(scene => new(scene.Definitions, scene.Schemas)));
    private static Guid Id(int value) => new($"20cf4b28-e47a-4f3a-9b62-{value:000000000000}");
    private static IAppBuilder Builder(bool di) => App.Builder(configuration => { if (di) configuration.UseDI(); });
    private readonly record struct CountValue(int Value);
    private sealed class Dependency(int factor) : IDisposable
    {
        internal int Factor { get; } = factor;
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
    private sealed class ValidationFactory(IFactory<ISceneAuthoringSession, SceneDocument> inner) : IFactory<ISceneAuthoringSession, SceneDocument>
    {
        internal int Calls;
        public ISceneAuthoringSession Create(SceneDocument scene) { Calls++; return inner.Create(scene); }
    }
}
