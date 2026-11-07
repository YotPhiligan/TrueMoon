using TrueMoon.Aether.Ecs;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EcsSystemsConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyGraph_RegistersOnlyCallerOwnedSchedulerFactory(bool di)
    {
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            Assert.Same(context, context.UseAetherEcsSystems(_ => { }));
        }).Build();
        await app.StartAsync();
        Assert.Empty(app.Services.ResolveAll<EcsScheduler>());
        Assert.Empty(app.Services.ResolveAll<IEcsSystem>());
        Assert.Empty(app.Services.ResolveAll<World>());
        var factory = Assert.Single(app.Services.ResolveAll<IFactory<EcsScheduler, World>>());
        Assert.Same(factory, app.Services.Resolve<IFactory<EcsScheduler, World>>());
        using var world = Assert.IsType<World>(app.Services.Resolve<IFactory<World>>()!.Create());
        var scheduler = factory.Create(world);
        try
        {
            Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
            Assert.Equal(1ul, scheduler.TickNumber);
            await app.DisposeAsync();
            Assert.Equal(EcsSchedulerState.Ready, scheduler.State);
            Assert.False(world.IsDisposed);
        }
        finally { scheduler.Dispose(); }
        Assert.Equal(EcsSchedulerState.Disposed, scheduler.State);
        Assert.True(world.IsAlive(world.CreateEntity()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_ResolvesFactoriesOnceAndCreatesIndependentGraphs(bool di)
    {
        var activations = 0;
        var systems = new FactoryProbe();
        var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ =>
            {
                activations++;
                return systems;
            }));
            context.UseAetherEcsSystems(builder => builder.Add<ProbeSystem>(new("probe")));
        }).Build();
        World? firstWorld = null, secondWorld = null;
        EcsScheduler? first = null, second = null;
        try
        {
            await app.StartAsync();
            Assert.Equal((0, 0), (activations, systems.Creations));
            var factory = Assert.IsAssignableFrom<IFactory<EcsScheduler, World>>(app.Services.Resolve<IFactory<EcsScheduler, World>>());
            Assert.Equal((1, 0), (activations, systems.Creations));
            Assert.Same(factory, app.Services.Resolve<IFactory<EcsScheduler, World>>());
            var worlds = Assert.IsAssignableFrom<IFactory<World>>(app.Services.Resolve<IFactory<World>>());
            firstWorld = Assert.IsType<World>(worlds.Create());
            secondWorld = Assert.IsType<World>(worlds.Create());
            first = factory.Create(firstWorld);
            second = factory.Create(secondWorld);
            Assert.NotSame(first, second);
            Assert.Equal(2, systems.Creations);
            var firstSystem = systems.Systems[0];
            var secondSystem = systems.Systems[1];
            Assert.NotSame(firstSystem, secondSystem);
            foreach (var _ in Enumerable.Range(0, 3)) Assert.True(first.Tick(TimeSpan.FromMilliseconds(10)));
            foreach (var _ in Enumerable.Range(0, 2)) Assert.True(second.Tick(TimeSpan.FromMilliseconds(10)));
            Assert.Equal((3, 2), (firstSystem.Executions, secondSystem.Executions));
            Assert.Equal((3ul, 2ul), (first.TickNumber, second.TickNumber));
            Assert.Equal((1, 2), (activations, systems.Creations));

            first.Dispose();
            first.Dispose();
            Assert.Equal((1, 1), (firstSystem.Stops, firstSystem.Disposals));
            Assert.Equal((0, 0), (secondSystem.Stops, secondSystem.Disposals));
            Assert.Equal(0, systems.Disposals);
            Assert.False(firstWorld.IsDisposed);
            await app.DisposeAsync();
            Assert.Equal(1, systems.Disposals);
            Assert.Equal(EcsSchedulerState.Ready, second.State);
            Assert.Equal((0, 0), (secondSystem.Stops, secondSystem.Disposals));
            second.Dispose();
            Assert.Equal((1, 1), (secondSystem.Stops, secondSystem.Disposals));
            Assert.False(secondWorld.IsDisposed);
        }
        finally
        {
            second?.Dispose(); first?.Dispose();
            secondWorld?.Dispose(); firstWorld?.Dispose();
            await app.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_FreezesDescriptionsBuilderAndOptions(bool di)
    {
        var systems = new FactoryProbe(world => context =>
        {
            var entity = new List<EntityHandle>();
            context.World.Query<int>().ForEach((EntityHandle current, scoped in int value) =>
            {
                Assert.Equal(251, value);
                entity.Add(current);
            });
            var existing = Assert.Single(entity);
            Assert.Throws<InvalidOperationException>(() => context.World.SetComponent(existing, -1L));
            context.Commands.CreateEntity();
            Assert.Throws<InvalidOperationException>(() => context.Commands.CreateEntity());
        });
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ => systems));
            var description = new EcsSystemDescription("frozen").Read<int>();
            EcsSystemsBuilder retained = null!;
            EcsSchedulerOptions options = null!;
            context.UseAetherEcsSystems(builder =>
            {
                retained = builder;
                Assert.Same(builder, builder.Add<ProbeSystem>(description));
            }, configure =>
            {
                options = configure;
                configure.MaximumSystems = 1;
                configure.MaximumCommandsPerPhase = 1;
            });
            description.Write<long>().After("not-registered");
            retained.Add<ProbeSystem>(new("late-registration"));
            options.MaximumSystems = 0;
            options.MaximumCommandsPerPhase = 0;
        }).Build();
        await app.StartAsync();
        using var world = Assert.IsType<World>(app.Services.Resolve<IFactory<World>>()!.Create());
        var entity = world.CreateEntity();
        world.AddComponent(entity, 251);
        world.AddComponent(entity, 257L);
        var factory = Assert.IsAssignableFrom<IFactory<EcsScheduler, World>>(app.Services.Resolve<IFactory<EcsScheduler, World>>());
        using var scheduler = factory.Create(world);

        Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(20)));
        Assert.Equal(1, systems.Creations);
        Assert.Equal(1, Assert.Single(systems.Systems).Executions);
        Assert.Equal(2, world.EntityCount);
        Assert.Equal(251, world.GetComponent<int>(entity));
        Assert.Equal(257L, world.GetComponent<long>(entity));
        Assert.Equal(1ul, scheduler.TickNumber);
        Assert.False(world.IsFaulted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingEcsRegistration_FailsWithoutActivatingSystemFactories(bool di)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ =>
            {
                activations++;
                return new FactoryProbe();
            }));
            context.UseAetherEcsSystems(systems => systems.Add<ProbeSystem>(new("probe")));
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, activations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSystemFactory_FailsBeforeAnySystemIsCreated(bool di)
    {
        var activations = 0;
        var systems = new FactoryProbe();
        await using var app = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ =>
            {
                activations++;
                return systems;
            }));
            context.UseAetherEcsSystems(builder => builder.Add<ProbeSystem>(new("available")).Add<MissingSystem>(new("missing")));
        }).Build();
        await app.StartAsync();
        Assert.Equal(0, activations);

        var error = Assert.Throws<InvalidOperationException>(() => app.Services.Resolve<IFactory<EcsScheduler, World>>());

        Assert.Contains(nameof(MissingSystem), error.Message);
        Assert.Contains("IFactory", error.Message);
        Assert.Equal((1, 0), (activations, systems.Creations));
        Assert.Empty(systems.Systems);
        Assert.Empty(app.Services.ResolveAll<EcsScheduler>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DuplicateSchedulerRegistration_IsRejectedBeforeActivation(bool di, bool suppliedFactory)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            if (suppliedFactory)
                context.Services(services => services.Singleton<IFactory<EcsScheduler, World>>(_ =>
                {
                    activations++;
                    throw new IOException("A duplicate factory must stay inactive.");
                }));
            else context.UseAetherEcsSystems(_ => { });
            context.UseAetherEcsSystems(_ => { });
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, activations);
    }

    [Theory]
    [InlineData(false, "duplicate-id")]
    [InlineData(true, "duplicate-id")]
    [InlineData(false, "missing-dependency")]
    [InlineData(true, "missing-dependency")]
    [InlineData(false, "system-limit")]
    [InlineData(true, "system-limit")]
    public void InvalidComposition_FailsBeforeResolvingSystemFactories(bool di, string kind)
    {
        var activations = 0;
        var systems = new FactoryProbe();
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ =>
            {
                activations++;
                return systems;
            }));
            context.UseAetherEcsSystems(graph =>
            {
                var first = new EcsSystemDescription("first");
                if (kind == "missing-dependency") first.After("missing");
                graph.Add<ProbeSystem>(first);
                if (kind != "missing-dependency") graph.Add<ProbeSystem>(new(kind == "duplicate-id" ? "first" : "second"));
            }, options => options.MaximumSystems = 1);
        });

        Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Equal((0, 0), (activations, systems.Creations));
        Assert.Empty(systems.Systems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOptions_AreRejectedBeforeSystemFactoriesActivate(bool di)
    {
        var activations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ =>
            {
                activations++;
                return new FactoryProbe();
            }));
            context.UseAetherEcsSystems(graph => graph.Add<ProbeSystem>(new("probe")), options => options.MaximumCommandsPerPhase = 0);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
        Assert.Equal(0, activations);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConfigureFailure_PreservesOriginalException(bool di, bool optionsCallback)
    {
        var expected = new IOException("ECS systems configuration fixture");
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            if (optionsCallback) context.UseAetherEcsSystems(_ => { }, _ => throw expected);
            else context.UseAetherEcsSystems(_ => throw expected);
        });
        Assert.Same(expected, Assert.Throws<IOException>(() => builder.Build()));
    }

    [Fact]
    public void NullConfiguration_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => EcsConfigurationExtensions.UseAetherEcsSystems(null!, _ => { }));
        var builder = Builder(false).Setup(context => context.UseAetherEcsSystems(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Build());
        Assert.Throws<ArgumentNullException>(() => new EcsSystemsBuilder().Add<ProbeSystem>(null!));
    }

    [Fact]
    public void Builder_AdmitsMaximumRegistrationsAndRejectsOneBeyond()
    {
        var builder = new EcsSystemsBuilder();
        for (var i = 0; i < 1024; i++) Assert.Same(builder, builder.Add<ProbeSystem>(new("system-" + i)));
        Assert.Throws<InvalidOperationException>(() => builder.Add<ProbeSystem>(new("one-too-many")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentApps_HaveIndependentFactoriesAndSystems(bool di)
    {
        var firstSystems = new FactoryProbe();
        var secondSystems = new FactoryProbe();
        await using var firstApp = ConfiguredApp(di, firstSystems);
        await using var secondApp = ConfiguredApp(di, secondSystems);
        await firstApp.StartAsync();
        await secondApp.StartAsync();
        var firstFactory = Assert.IsAssignableFrom<IFactory<EcsScheduler, World>>(firstApp.Services.Resolve<IFactory<EcsScheduler, World>>());
        var secondFactory = Assert.IsAssignableFrom<IFactory<EcsScheduler, World>>(secondApp.Services.Resolve<IFactory<EcsScheduler, World>>());
        Assert.NotSame(firstFactory, secondFactory);
        using var firstWorld = Assert.IsType<World>(firstApp.Services.Resolve<IFactory<World>>()!.Create());
        using var secondWorld = Assert.IsType<World>(secondApp.Services.Resolve<IFactory<World>>()!.Create());
        using var first = firstFactory.Create(firstWorld);
        using var second = secondFactory.Create(secondWorld);
        Assert.True(first.Tick(TimeSpan.FromMilliseconds(10)));
        Assert.True(second.Tick(TimeSpan.FromMilliseconds(10)));
        first.Dispose();
        await firstApp.DisposeAsync();
        Assert.True(second.Tick(TimeSpan.FromMilliseconds(10)));
        Assert.Equal((1, 2), (Assert.Single(firstSystems.Systems).Executions, Assert.Single(secondSystems.Systems).Executions));
        Assert.Equal((1, 0), (firstSystems.Disposals, secondSystems.Disposals));
        Assert.Equal(EcsSchedulerState.Ready, second.State);
        Assert.False(firstWorld.IsDisposed);
        Assert.False(secondWorld.IsDisposed);
    }

    private static IAppBuilder Builder(bool di) => App.Builder(context => { if (di) context.UseDI(); });
    private static IApp ConfiguredApp(bool di, FactoryProbe systems) => Builder(di).Setup(context =>
    {
        context.UseAetherEcs();
        context.Services(services => services.Singleton<IFactory<ProbeSystem, World>>(_ => systems));
        context.UseAetherEcsSystems(builder => builder.Add<ProbeSystem>(new("probe")));
    }).Build();

    private sealed class FactoryProbe(Func<World, Action<EcsSystemContext>?>? execute = null) : IFactory<ProbeSystem, World>, IDisposable
    {
        internal int Creations, Disposals;
        internal List<ProbeSystem> Systems { get; } = [];
        public ProbeSystem Create(World world)
        {
            Creations++;
            var result = new ProbeSystem(world, execute?.Invoke(world));
            Systems.Add(result);
            return result;
        }
        public void Dispose() => Disposals++;
    }
    private sealed class ProbeSystem(World expectedWorld, Action<EcsSystemContext>? execute) : IEcsSystem
    {
        internal int Executions, Stops, Disposals;
        public void Execute(EcsSystemContext context)
        {
            Assert.Same(expectedWorld, context.World);
            Executions++;
            execute?.Invoke(context);
        }
        public void Stop() => Stops++;
        public void Dispose() => Disposals++;
    }
    private sealed class MissingSystem : IEcsSystem
    {
        public void Execute(EcsSystemContext context) => throw new InvalidOperationException("An unregistered system must not be created.");
        public void Dispose() { }
    }
}
