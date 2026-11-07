using TrueMoon.Aether.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class WorldTests
{
    [Fact]
    public void Create_WorldsHaveIndependentIdentityAndEmptyStorage()
    {
        using var first = new World();
        using var second = new World(null);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal((0, 0), (first.EntityCount, first.ComponentTypeCount));
        Assert.False(first.IsDisposed);
        Assert.False(first.IsAlive(default));

        var entity = first.CreateEntity();
        var other = second.CreateEntity();
        Assert.Equal((first.Id, 0, 1u), (entity.WorldId, entity.Index, entity.Generation));
        Assert.Equal((second.Id, 0, 1u), (other.WorldId, other.Index, other.Generation));
        Assert.Equal(1, first.EntityCount);
        Assert.True(first.IsAlive(entity));
        Assert.False(first.IsAlive(other));
        Assert.False(second.IsAlive(entity));
    }

    [Fact]
    public void Destroy_ReusesSlotWithNewGenerationAndNoComponents()
    {
        using var world = Limited(3);
        var old = world.CreateEntity();
        var neighbor = world.CreateEntity();
        world.AddComponent(old, new Position(13, 17));
        world.AddComponent(old, new Speed(3));
        world.AddComponent(neighbor, new Position(23, 29));

        world.DestroyEntity(old);
        Assert.Equal(1, world.EntityCount);
        Assert.False(world.IsAlive(old));
        Assert.Throws<ArgumentException>(() => world.DestroyEntity(old));
        var replacement = world.CreateEntity();

        Assert.Equal(old.Index, replacement.Index);
        Assert.Equal(old.Generation + 1, replacement.Generation);
        Assert.NotEqual(old, replacement);
        Assert.False(world.IsAlive(old));
        Assert.True(world.IsAlive(replacement));
        Assert.False(world.HasComponent<Position>(replacement));
        Assert.False(world.HasComponent<Speed>(replacement));
        Assert.Equal(new Position(23, 29), world.GetComponent<Position>(neighbor));
        world.AddComponent(replacement, new Position(31, 37));
        Assert.Throws<ArgumentException>(() => world.SetComponent(old, new Position(-1, -1)));
        Assert.Equal(new Position(31, 37), world.GetComponent<Position>(replacement));
        Assert.Equal((2, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("foreign")]
    [InlineData("negative-index")]
    [InlineData("next-index")]
    [InlineData("large-index")]
    [InlineData("zero-generation")]
    [InlineData("wrong-generation")]
    [InlineData("destroyed")]
    public void InvalidHandles_RejectEveryComponentOperationWithoutChangingLiveEntity(string kind)
    {
        using var world = Limited(5);
        using var other = Limited(5);
        var live = world.CreateEntity();
        var destroyed = world.CreateEntity();
        world.DestroyEntity(destroyed);
        world.AddComponent(live, new Position(41, 43));
        var invalid = kind switch
        {
            "default" => default,
            "foreign" => other.CreateEntity(),
            "negative-index" => new EntityHandle(world.Id, -1, 1),
            "next-index" => new EntityHandle(world.Id, 2, 1),
            "large-index" => new EntityHandle(world.Id, int.MaxValue, 1),
            "zero-generation" => new EntityHandle(world.Id, live.Index, 0),
            "wrong-generation" => new EntityHandle(world.Id, live.Index, live.Generation + 1),
            "destroyed" => destroyed,
            _ => throw new ArgumentException(nameof(kind))
        };

        Assert.False(world.IsAlive(invalid));
        Assert.Throws<ArgumentException>(() => world.DestroyEntity(invalid));
        Assert.Throws<ArgumentException>(() => world.AddComponent(invalid, new Position(0, 0)));
        Assert.Throws<ArgumentException>(() => world.SetComponent(invalid, new Position(0, 0)));
        Assert.Throws<ArgumentException>(() => world.GetComponent<Position>(invalid));
        Assert.Throws<ArgumentException>(() => world.TryGetComponent<Position>(invalid, out _));
        Assert.Throws<ArgumentException>(() => world.HasComponent<Position>(invalid));
        Assert.Throws<ArgumentException>(() => world.RemoveComponent<Position>(invalid));
        Assert.Throws<ArgumentException>(() => world.AddComponent(invalid, new ManagedValue("rejected")));
        Assert.Equal(new Position(41, 43), world.GetComponent<Position>(live));
        Assert.True(world.IsAlive(live));
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public void GenerationExhaustion_RetiresSlotsWithoutRevivingOldHandles(uint limit)
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 2 }, limit);
        var stale = new List<EntityHandle>();
        for (var index = 0; index < 2; index++)
        {
            for (uint generation = 1; generation <= limit; generation++)
            {
                var entity = world.CreateEntity();
                Assert.Equal((index, generation), (entity.Index, entity.Generation));
                world.AddComponent(entity, new Position(index, (int)generation));
                foreach (var previous in stale) Assert.False(world.IsAlive(previous));
                world.DestroyEntity(entity);
                stale.Add(entity);
                Assert.False(world.IsAlive(entity));
                Assert.Equal(0, world.EntityCount);
            }
        }

        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
        foreach (var previous in stale) Assert.False(world.IsAlive(previous));
        Assert.Equal((0, 1), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void EntityBudget_RejectsOneBeyondCapacityAndAllowsDestroyedSlotReuse()
    {
        using var world = Limited(3);
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        var third = world.CreateEntity();
        world.AddComponent(second, new Position(47, 53));
        Assert.Equal(3, world.EntityCount);
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
        Assert.Equal(new Position(47, 53), world.GetComponent<Position>(second));

        world.DestroyEntity(first);
        var reused = world.CreateEntity();
        Assert.Equal(first.Index, reused.Index);
        Assert.Equal(3, world.EntityCount);
        Assert.True(world.IsAlive(second));
        Assert.True(world.IsAlive(third));
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
    }

    [Fact]
    public void Components_AreIndependentSingletonValuesAndReadsReturnCopies()
    {
        using var world = Limited(4);
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.AddComponent(first, new Position(59, 61));
        world.AddComponent(first, new Speed(7));
        world.AddComponent(second, new Position(67, 71));

        var copy = world.GetComponent<Position>(first);
        copy.X = -1;
        Assert.True(world.TryGetComponent<Position>(first, out var otherCopy));
        otherCopy.Y = -2;
        Assert.Equal(new Position(59, 61), world.GetComponent<Position>(first));
        world.SetComponent(first, new Position(73, 79));
        Assert.Equal(new Position(73, 79), world.GetComponent<Position>(first));
        Assert.Equal(new Position(67, 71), world.GetComponent<Position>(second));
        Assert.Equal(new Speed(7), world.GetComponent<Speed>(first));
        Assert.False(world.HasComponent<Speed>(second));
        Assert.Equal((2, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void MissingComponents_HaveExplicitOutcomesWithoutRegisteringPools()
    {
        using var world = Limited(8);
        var entities = Enumerable.Range(0, 8).Select(_ => world.CreateEntity()).ToArray();
        var absent = entities[^1];
        Assert.False(world.TryGetComponent<Position>(absent, out var before));
        Assert.Equal(default, before);
        Assert.False(world.HasComponent<Position>(absent));
        Assert.False(world.RemoveComponent<Position>(absent));
        Assert.Throws<KeyNotFoundException>(() => world.GetComponent<Position>(absent));
        Assert.Throws<KeyNotFoundException>(() => world.SetComponent(absent, new Position(1, 2)));
        Assert.Equal(0, world.ComponentTypeCount);

        // The live target is beyond this pool's current sparse array.
        world.AddComponent(entities[0], new Position(83, 89));
        Assert.False(world.TryGetComponent<Position>(absent, out var after));
        Assert.Equal(default, after);
        Assert.False(world.HasComponent<Position>(absent));
        Assert.False(world.RemoveComponent<Position>(absent));
        Assert.Throws<KeyNotFoundException>(() => world.GetComponent<Position>(absent));
        Assert.Throws<KeyNotFoundException>(() => world.SetComponent(absent, new Position(1, 2)));
        Assert.Equal(new Position(83, 89), world.GetComponent<Position>(entities[0]));
        Assert.Equal(1, world.ComponentTypeCount);
    }

    [Fact]
    public void DuplicateAddAndMissingSet_DoNotOverwriteOrRegisterData()
    {
        using var world = Limited(4);
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.AddComponent(first, new Position(97, 101));
        Assert.Throws<InvalidOperationException>(() => world.AddComponent(first, new Position(-1, -1)));
        Assert.Throws<KeyNotFoundException>(() => world.SetComponent(second, new Position(-1, -1)));
        Assert.Throws<KeyNotFoundException>(() => world.SetComponent(first, new Speed(-1)));
        Assert.Equal(new Position(97, 101), world.GetComponent<Position>(first));
        Assert.False(world.HasComponent<Position>(second));
        Assert.False(world.HasComponent<Speed>(first));
        Assert.Equal((2, 1), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Remove_DenseElementsRepairRemainingEntityLookups(int removed)
    {
        using var world = Limited(4);
        var entities = Enumerable.Range(0, 4).Select(_ => world.CreateEntity()).ToArray();
        for (var i = 0; i < entities.Length; i++) world.AddComponent(entities[i], new Position(100 + i, 200 + i));
        world.AddComponent(entities[removed], new Speed(11));

        Assert.True(world.RemoveComponent<Position>(entities[removed]));
        Assert.False(world.RemoveComponent<Position>(entities[removed]));
        Assert.False(world.TryGetComponent<Position>(entities[removed], out var missing));
        Assert.Equal(default, missing);
        Assert.Equal(new Speed(11), world.GetComponent<Speed>(entities[removed]));
        for (var i = 0; i < entities.Length; i++)
        {
            if (i == removed) continue;
            Assert.Equal(new Position(100 + i, 200 + i), world.GetComponent<Position>(entities[i]));
            world.SetComponent(entities[i], new Position(300 + i, 400 + i));
            Assert.Equal(new Position(300 + i, 400 + i), world.GetComponent<Position>(entities[i]));
        }
        world.AddComponent(entities[removed], new Position(503, 509));
        Assert.Equal(new Position(503, 509), world.GetComponent<Position>(entities[removed]));
        Assert.Equal((4, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void Destroy_RemovesEveryPoolAndPreservesOtherEntities()
    {
        using var world = Limited(4);
        var entities = Enumerable.Range(0, 4).Select(_ => world.CreateEntity()).ToArray();
        for (var i = 0; i < entities.Length; i++)
        {
            world.AddComponent(entities[i], new Position(1000 + i, 2000 + i));
            world.AddComponent(entities[i], new Speed(3000 + i));
        }
        world.DestroyEntity(entities[1]);
        foreach (var i in new[] { 0, 2, 3 })
        {
            Assert.Equal(new Position(1000 + i, 2000 + i), world.GetComponent<Position>(entities[i]));
            Assert.Equal(new Speed(3000 + i), world.GetComponent<Speed>(entities[i]));
        }
        var replacement = world.CreateEntity();
        Assert.Equal(entities[1].Index, replacement.Index);
        Assert.False(world.HasComponent<Position>(replacement));
        Assert.False(world.HasComponent<Speed>(replacement));
        Assert.Equal((4, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void GrowthAndReuse_PreserveSparseDenseMappingsForTenThousandEntities()
    {
        const int count = 10000;
        using var world = Limited(count);
        var entities = new EntityHandle[count];
        for (var i = 0; i < count; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent(entities[i], new Position(i + 100, -i));
            if (i % 3 == 0) world.AddComponent(entities[i], new Speed(i * 7 + 13));
        }
        for (var i = 2; i < count; i += 5) Assert.True(world.RemoveComponent<Position>(entities[i]));
        var destroyed = new Dictionary<int, EntityHandle>();
        for (var i = 1; i < count; i += 7)
        {
            destroyed.Add(i, entities[i]);
            world.DestroyEntity(entities[i]);
        }
        Assert.Equal(count - destroyed.Count, world.EntityCount);
        var replacements = new List<EntityHandle>();
        foreach (var _ in destroyed)
        {
            var replacement = world.CreateEntity();
            var old = destroyed[replacement.Index];
            Assert.Equal(old.Generation + 1, replacement.Generation);
            Assert.False(world.HasComponent<Position>(replacement));
            Assert.False(world.HasComponent<Speed>(replacement));
            world.AddComponent(replacement, new Position(-100000 - replacement.Index, 17));
            replacements.Add(replacement);
        }
        for (var i = 0; i < count; i++)
        {
            if (destroyed.ContainsKey(i)) { Assert.False(world.IsAlive(entities[i])); continue; }
            Assert.True(world.IsAlive(entities[i]));
            Assert.Equal(i % 5 != 2, world.TryGetComponent<Position>(entities[i], out var position));
            Assert.Equal(i % 5 == 2 ? default : new Position(i + 100, -i), position);
            Assert.Equal(i % 3 == 0, world.TryGetComponent<Speed>(entities[i], out var speed));
            Assert.Equal(i % 3 == 0 ? new Speed(i * 7 + 13) : default, speed);
        }
        foreach (var replacement in replacements)
            Assert.Equal(new Position(-100000 - replacement.Index, 17), world.GetComponent<Position>(replacement));
        Assert.Equal((count, 2), (world.EntityCount, world.ComponentTypeCount));
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
    }

    [Fact]
    public void ComponentTypeLimit_PreservesExistingDataAndCountsEmptyPools()
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 3, MaximumComponentTypes = 2 });
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(521, 523));
        world.AddComponent(entity, new Speed(19));
        Assert.Throws<InvalidOperationException>(() => world.AddComponent(entity, new Tag()));
        Assert.Equal(new Position(521, 523), world.GetComponent<Position>(entity));
        Assert.Equal(new Speed(19), world.GetComponent<Speed>(entity));
        Assert.False(world.HasComponent<Tag>(entity));
        Assert.True(world.RemoveComponent<Position>(entity));
        Assert.Equal(2, world.ComponentTypeCount);
        Assert.Throws<InvalidOperationException>(() => world.AddComponent(entity, new Tag()));
        world.AddComponent(entity, new Position(541, 547));
        Assert.Equal(new Position(541, 547), world.GetComponent<Position>(entity));
        Assert.Equal(2, world.ComponentTypeCount);
    }

    [Fact]
    public void ManagedValues_AreRejectedByEveryComponentApiWithoutRegisteringPool()
    {
        using var world = new World(new() { MaximumComponentTypes = 1 });
        var entity = world.CreateEntity();
        Reject(new ManagedValue("managed"));
        Reject(new NestedManagedValue(new ManagedValue("nested")));
        Assert.Equal((1, 0), (world.EntityCount, world.ComponentTypeCount));
        world.AddComponent(entity, new Position(557, 563));
        Assert.Equal(new Position(557, 563), world.GetComponent<Position>(entity));
        Assert.Equal(1, world.ComponentTypeCount);

        void Reject<T>(T value) where T : struct
        {
            Assert.Throws<NotSupportedException>(() => world.AddComponent(entity, value));
            Assert.Throws<NotSupportedException>(() => world.SetComponent(entity, value));
            Assert.Throws<NotSupportedException>(() => world.GetComponent<T>(entity));
            Assert.Throws<NotSupportedException>(() => world.TryGetComponent<T>(entity, out _));
            Assert.Throws<NotSupportedException>(() => world.HasComponent<T>(entity));
            Assert.Throws<NotSupportedException>(() => world.RemoveComponent<T>(entity));
        }
    }

    [Fact]
    public void UnmanagedNestedValuesEnumsAndTags_AreSupported()
    {
        using var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, new NestedValue(new Position(569, 571), 23));
        world.AddComponent(entity, Mode.Active);
        world.AddComponent(entity, new Tag());
        Assert.Equal(new NestedValue(new Position(569, 571), 23), world.GetComponent<NestedValue>(entity));
        Assert.Equal(Mode.Active, world.GetComponent<Mode>(entity));
        Assert.True(world.HasComponent<Tag>(entity));
        Assert.True(world.RemoveComponent<Tag>(entity));
        Assert.False(world.HasComponent<Tag>(entity));
        Assert.Throws<KeyNotFoundException>(() => world.GetComponent<Tag>(entity));
        Assert.Equal((1, 3), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void ComponentCopies_DoNotOwnDisposableValues()
    {
        DisposableValue.Disposals = 0;
        var world = Limited(3);
        var removed = world.CreateEntity();
        var destroyed = world.CreateEntity();
        var retained = world.CreateEntity();
        world.AddComponent(removed, new DisposableValue(1));
        world.AddComponent(destroyed, new DisposableValue(2));
        world.AddComponent(retained, new DisposableValue(3));
        var borrowedCopy = world.GetComponent<DisposableValue>(retained);
        world.SetComponent(retained, new DisposableValue(4));
        Assert.True(world.RemoveComponent<DisposableValue>(removed));
        world.DestroyEntity(destroyed);
        world.Dispose();
        world.Dispose();
        Assert.Equal(0, DisposableValue.Disposals);
        Assert.Equal(3, borrowedCopy.Id);
        borrowedCopy.Dispose();
        Assert.Equal(1, DisposableValue.Disposals);
    }

    [Fact]
    public void Dispose_IsIdempotentAndEveryDataOperationRejectsFurtherUse()
    {
        var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(577, 587));
        var id = world.Id;
        world.Dispose();
        world.Dispose();
        Assert.True(world.IsDisposed);
        Assert.Equal(id, world.Id);
        Assert.Throws<ObjectDisposedException>(() => world.EntityCount);
        Assert.Throws<ObjectDisposedException>(() => world.ComponentTypeCount);
        Assert.Throws<ObjectDisposedException>(() => world.CreateEntity());
        Assert.Throws<ObjectDisposedException>(() => world.IsAlive(entity));
        Assert.Throws<ObjectDisposedException>(() => world.IsAlive(default));
        Assert.Throws<ObjectDisposedException>(() => world.DestroyEntity(entity));
        Assert.Throws<ObjectDisposedException>(() => world.AddComponent(entity, new Position(0, 0)));
        Assert.Throws<ObjectDisposedException>(() => world.SetComponent(entity, new Position(0, 0)));
        Assert.Throws<ObjectDisposedException>(() => world.GetComponent<Position>(entity));
        Assert.Throws<ObjectDisposedException>(() => world.TryGetComponent<Position>(entity, out _));
        Assert.Throws<ObjectDisposedException>(() => world.HasComponent<Position>(entity));
        Assert.Throws<ObjectDisposedException>(() => world.RemoveComponent<Position>(entity));
        Assert.Throws<ObjectDisposedException>(() => world.AddComponent(entity, new ManagedValue("disposed")));
    }

    private static World Limited(int maximum) => new(new WorldOptions { InitialEntityCapacity = 0, MaximumEntityCount = maximum });

    private struct Position(int x, int y)
    {
        internal int X = x;
        internal int Y = y;
    }
    private readonly record struct Speed(int Value);
    private readonly record struct ManagedValue(string Value);
    private readonly record struct NestedManagedValue(ManagedValue Value);
    private readonly record struct NestedValue(Position Position, int Layer);
    private readonly struct Tag { }
    private enum Mode { Idle, Active }
    private readonly struct DisposableValue(int id) : IDisposable
    {
        internal static int Disposals;
        internal int Id { get; } = id;
        public void Dispose() => Disposals++;
    }
}
