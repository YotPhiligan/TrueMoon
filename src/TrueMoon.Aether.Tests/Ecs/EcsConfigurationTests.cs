using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EcsConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registration_SnapshotsOptionsAndCreatesIndependentCallerOwnedWorlds(bool di)
    {
        var app = Builder(di).Setup(context =>
        {
            WorldOptions captured = null!;
            Assert.Same(context, context.UseAetherEcs(options =>
            {
                captured = options;
                options.InitialEntityCapacity = 0;
                options.MaximumEntityCount = 3;
                options.MaximumComponentTypes = 1;
            }));
            captured.MaximumEntityCount = 1;
            captured.MaximumComponentTypes = 2;
            captured.InitialEntityCapacity = -1;
        }).Build();
        try
        {
            await app.StartAsync();
            Assert.Empty(app.Services.ResolveAll<World>());
            Assert.Empty(app.Services.ResolveAll<AetherHost>());
            var factory = Assert.Single(app.Services.ResolveAll<IFactory<World>>());
            Assert.Same(factory, app.Services.Resolve<IFactory<World>>());
            using var first = Assert.IsType<World>(factory.Create());
            using var second = Assert.IsType<World>(factory.Create());
            Assert.NotSame(first, second);
            Assert.NotEqual(first.Id, second.Id);
            var entity = first.CreateEntity();
            first.CreateEntity();
            first.CreateEntity();
            Assert.Throws<InvalidOperationException>(() => first.CreateEntity());
            first.AddComponent(entity, 619);
            Assert.Throws<InvalidOperationException>(() => first.AddComponent(entity, 631L));
            Assert.Throws<ArgumentException>(() => factory.Create(7));
            Assert.False(second.IsAlive(entity));
            Assert.Equal((0, 0), (second.EntityCount, second.ComponentTypeCount));

            await app.StopAsync();
            await app.DisposeAsync();
            Assert.False(first.IsDisposed);
            Assert.False(second.IsDisposed);
            first.SetComponent(entity, 641);
            Assert.Equal(641, first.GetComponent<int>(entity));
            Assert.Equal((3, 1), (first.EntityCount, first.ComponentTypeCount));
            var other = second.CreateEntity();
            Assert.True(second.IsAlive(other));
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentApps_HaveIndependentFactoryConfiguration(bool di)
    {
        await using var firstApp = Builder(di).Setup(c => c.UseAetherEcs(o =>
        {
            o.InitialEntityCapacity = 0;
            o.MaximumEntityCount = 2;
        })).Build();
        await using var secondApp = Builder(di).Setup(c => c.UseAetherEcs(o =>
        {
            o.InitialEntityCapacity = 0;
            o.MaximumEntityCount = 3;
        })).Build();
        await firstApp.StartAsync();
        await secondApp.StartAsync();
        var firstFactory = Assert.IsAssignableFrom<IFactory<World>>(firstApp.Services.Resolve<IFactory<World>>());
        var secondFactory = Assert.IsAssignableFrom<IFactory<World>>(secondApp.Services.Resolve<IFactory<World>>());
        Assert.NotSame(firstFactory, secondFactory);
        using var first = Assert.IsType<World>(firstFactory.Create());
        using var second = Assert.IsType<World>(secondFactory.Create());
        first.CreateEntity(); first.CreateEntity();
        var replaced = second.CreateEntity(); second.CreateEntity(); second.CreateEntity();
        Assert.Throws<InvalidOperationException>(() => first.CreateEntity());
        Assert.Throws<InvalidOperationException>(() => second.CreateEntity());
        Assert.Equal((2, 3), (first.EntityCount, second.EntityCount));
        await firstApp.DisposeAsync();
        Assert.False(first.IsDisposed);
        Assert.False(second.IsDisposed);
        second.DestroyEntity(replaced);
        var entity = second.CreateEntity();
        Assert.True(second.IsAlive(entity));
        Assert.False(second.IsAlive(replaced));
        Assert.Equal(replaced.Generation + 1, entity.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateRegistration_IsRejectedForBothContainers(bool di)
    {
        var builder = Builder(di).Setup(context =>
        {
            context.UseAetherEcs();
            context.UseAetherEcs();
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingFactory_IsRejectedWithoutActivatingIt(bool di)
    {
        var creations = 0;
        var builder = Builder(di).Setup(context =>
        {
            context.Services(services => services.Singleton<IFactory<World>>(_ => new Factory<World>(() =>
            {
                creations++;
                return new World();
            })));
            context.UseAetherEcs();
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, creations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidConfiguration_FailsBeforeFactoryRegistration(bool di)
    {
        var builder = Builder(di).Setup(context => context.UseAetherEcs(options => options.InitialEntityCapacity = -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureFailure_PreservesOriginalException(bool di)
    {
        var expected = new IOException("ECS options fixture");
        var builder = Builder(di).Setup(context => context.UseAetherEcs(_ => throw expected));
        Assert.Same(expected, Assert.Throws<IOException>(() => builder.Build()));
    }

    [Fact]
    public void NullContext_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => EcsConfigurationExtensions.UseAetherEcs(null!));

    private static IAppBuilder Builder(bool di) => App.Builder(c => { if (di) c.UseDI(); });
}
