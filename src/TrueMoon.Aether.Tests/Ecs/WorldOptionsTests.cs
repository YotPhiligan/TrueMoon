using TrueMoon.Aether.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class WorldOptionsTests
{
    [Theory]
    [InlineData(-1, 64, 128)]
    [InlineData(65, 64, 128)]
    [InlineData(0, -1, 128)]
    [InlineData(0, 0, 128)]
    [InlineData(0, 1048577, 128)]
    [InlineData(0, int.MaxValue, 128)]
    [InlineData(0, 64, -1)]
    [InlineData(0, 64, 0)]
    [InlineData(0, 64, 1025)]
    [InlineData(-1, 0, 0)]
    public void InvalidOptions_FailBeforeWorldCreation(int initial, int maximum, int types)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new World(new()
        {
            InitialEntityCapacity = initial,
            MaximumEntityCount = maximum,
            MaximumComponentTypes = types
        }));

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(63, 63, 128)]
    [InlineData(64, 64, 128)]
    [InlineData(0, 1048576, 1024)]
    [InlineData(1048576, 1048576, 1024)]
    public void ValidBoundaryOptions_CreateUsableWorld(int initial, int maximum, int types)
    {
        using var world = new World(new()
        {
            InitialEntityCapacity = initial,
            MaximumEntityCount = maximum,
            MaximumComponentTypes = types
        });
        var entity = world.CreateEntity();
        world.AddComponent(entity, 607);
        Assert.Equal(607, world.GetComponent<int>(entity));
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        world.DestroyEntity(entity);
        Assert.False(world.IsAlive(entity));
        Assert.Equal(0, world.EntityCount);
    }

    [Fact]
    public void World_CopiesOptionsBeforeCallerMutation()
    {
        var options = new WorldOptions { InitialEntityCapacity = 0, MaximumEntityCount = 3, MaximumComponentTypes = 1 };
        using var world = new World(options);
        options.InitialEntityCapacity = -1;
        options.MaximumEntityCount = 1;
        options.MaximumComponentTypes = 2;
        var first = world.CreateEntity();
        world.CreateEntity();
        world.CreateEntity();
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
        world.AddComponent(first, 613);
        Assert.Throws<InvalidOperationException>(() => world.AddComponent(first, 617L));
        Assert.Equal(613, world.GetComponent<int>(first));
        Assert.False(world.HasComponent<long>(first));
        Assert.Equal((3, 1), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void GenerationLimit_RejectsZeroAndNullInternalOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new World(new WorldOptions(), 0));
        Assert.Throws<ArgumentNullException>(() => new World(null!, 1));
    }
}
