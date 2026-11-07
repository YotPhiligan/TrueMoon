using TrueMoon.Aether.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EntityQueryTests
{
    [Fact]
    public void ReadSingle_MatchesOnlyRequiredMembershipAndReturnsExpectedValues()
    {
        using var world = CreateWorld();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        var absent = world.CreateEntity();
        world.AddComponent(first, new Position(11, 13));
        world.AddComponent(second, new Position(17, 19));
        world.AddComponent(absent, new Velocity(23));
        var found = new Dictionary<EntityHandle, Position>();

        world.Query<Position>().ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity, position));

        Assert.Equal(2, found.Count);
        Assert.Equal(new Position(11, 13), found[first]);
        Assert.Equal(new Position(17, 19), found[second]);
        Assert.False(found.ContainsKey(absent));
        Assert.Equal((3, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadPair_IntersectsRequiredPoolsIndependentlyOfPoolSize(bool firstPoolSmaller)
    {
        using var world = CreateWorld();
        var entities = Enumerable.Range(0, 5).Select(_ => world.CreateEntity()).ToArray();
        world.AddComponent(entities[0], new Position(29, 31));
        world.AddComponent(entities[0], new Velocity(2));
        world.AddComponent(entities[1], new Position(37, 41));
        world.AddComponent(entities[1], new Velocity(3));
        world.AddComponent(entities[2], new Position(43, 47));
        world.AddComponent(entities[3], new Velocity(5));
        if (firstPoolSmaller) world.AddComponent(entities[4], new Velocity(7));
        else world.AddComponent(entities[4], new Position(53, 59));
        var found = new Dictionary<EntityHandle, (Position Position, Velocity Velocity)>();
        var reversed = new Dictionary<EntityHandle, (Position Position, Velocity Velocity)>();

        world.Query<Position, Velocity>().ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) =>
            found.Add(entity, (position, velocity)));
        world.Query<Velocity, Position>().ForEach((EntityHandle entity, scoped in Velocity velocity, scoped in Position position) =>
            reversed.Add(entity, (position, velocity)));

        Assert.Equal(2, found.Count);
        Assert.Equal(2, reversed.Count);
        Assert.Equal((new Position(29, 31), new Velocity(2)), found[entities[0]]);
        Assert.Equal((new Position(37, 41), new Velocity(3)), found[entities[1]]);
        foreach (var pair in found) Assert.Equal(pair.Value, reversed[pair.Key]);
        Assert.False(found.ContainsKey(entities[2]));
        Assert.False(found.ContainsKey(entities[3]));
        Assert.False(found.ContainsKey(entities[4]));
    }

    [Fact]
    public void Exclusions_FilterMembershipAndLeaveOriginalDescriptorUnchanged()
    {
        using var world = CreateWorld();
        var first = world.CreateEntity();
        var excluded = world.CreateEntity();
        foreach (var entity in new[] { first, excluded })
        {
            world.AddComponent(entity, new Position(entity.Index + 61, 67));
            world.AddComponent(entity, new Velocity(11));
        }
        world.AddComponent(excluded, new Tag0());
        var original = world.Query<Position>();
        var filtered = original.Without<Tag0>();
        var pairOriginal = world.Query<Position, Velocity>();
        var pairFiltered = pairOriginal.Without<Tag0>();
        var unfiltered = new HashSet<EntityHandle>();
        var filteredEntities = new HashSet<EntityHandle>();
        var pairUnfiltered = new HashSet<EntityHandle>();
        var pairFilteredEntities = new HashSet<EntityHandle>();

        original.ForEach((EntityHandle entity, scoped in Position position) => unfiltered.Add(entity));
        filtered.ForEach((EntityHandle entity, scoped in Position position) => filteredEntities.Add(entity));
        pairOriginal.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => pairUnfiltered.Add(entity));
        pairFiltered.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => pairFilteredEntities.Add(entity));

        Assert.Equal(2, unfiltered.Count);
        Assert.Equal(2, pairUnfiltered.Count);
        Assert.Contains(first, unfiltered);
        Assert.Contains(excluded, unfiltered);
        Assert.Equal(first, Assert.Single(filteredEntities));
        Assert.Equal(first, Assert.Single(pairFilteredEntities));
        Assert.Equal(3, world.ComponentTypeCount);
    }

    [Fact]
    public void Exclusions_ValidateDistinctLimitAndRequiredContradictions()
    {
        using var world = CreateWorld();
        var visible = world.CreateEntity();
        var excluded = world.CreateEntity();
        world.AddComponent(visible, new Position(71, 73));
        world.AddComponent(excluded, new Position(79, 83));
        world.AddComponent(excluded, new Tag7());
        var query = world.Query<Position>().Without<Tag0>().Without<Tag1>().Without<Tag2>().Without<Tag3>()
            .Without<Tag4>().Without<Tag5>().Without<Tag6>().Without<Tag7>();
        var repeated = query.Without<Tag7>().Without<Tag0>();
        var found = new List<EntityHandle>();

        repeated.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity));

        Assert.Equal(visible, Assert.Single(found));
        Assert.Throws<InvalidOperationException>(() => query.Without<Tag8>());
        Assert.Throws<ArgumentException>(() => query.Without<Position>());
        Assert.Throws<ArgumentException>(() => world.Query<Position, Velocity>().Without<Position>());
        Assert.Throws<ArgumentException>(() => world.Query<Position, Velocity>().Without<Velocity>());
        Assert.Throws<NotSupportedException>(() => query.Without<ManagedValue>());
        Assert.Equal((2, 2), (world.EntityCount, world.ComponentTypeCount));
    }

    [Fact]
    public void EmptyAndRetainedQueries_ObserveLaterMembershipWithoutRegisteringTypes()
    {
        using var world = CreateWorld();
        var query = world.Query<Position>();
        var pair = world.Query<Position, Velocity>();
        var visits = 0;
        query.ForEach((EntityHandle entity, scoped in Position position) => visits++);
        pair.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => visits++);
        Assert.Equal(0, visits);
        Assert.Equal(0, world.ComponentTypeCount);

        var first = world.CreateEntity();
        world.AddComponent(first, new Position(89, 97));
        pair.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => visits++);
        world.Query<Velocity, Position>().ForEach((EntityHandle entity, scoped in Velocity velocity, scoped in Position position) => visits++);
        Assert.Equal(0, visits);
        Assert.Equal(1, world.ComponentTypeCount);
        world.AddComponent(first, new Velocity(13));
        var found = new Dictionary<EntityHandle, Position>();
        pair.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => found.Add(entity, position));
        Assert.Equal(new Position(89, 97), found[first]);
        Assert.Single(found);

        world.DestroyEntity(first);
        var replacement = world.CreateEntity();
        world.AddComponent(replacement, new Position(101, 103));
        found.Clear();
        query.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity, position));
        Assert.Single(found);
        Assert.False(found.ContainsKey(first));
        Assert.Equal(new Position(101, 103), found[replacement]);
        var pairVisits = 0;
        pair.ForEach((EntityHandle entity, scoped in Position position, scoped in Velocity velocity) => pairVisits++);
        Assert.Equal(0, pairVisits);
        Assert.Equal(2, world.ComponentTypeCount);
    }

    [Fact]
    public void RetainedExclusions_ReevaluatePoolsAfterRegistrationRemovalAndReuse()
    {
        using var world = CreateWorld();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        world.AddComponent(first, new Position(211, 223));
        world.AddComponent(second, new Position(227, 229));
        var query = world.Query<Position>().Without<Tag0>();
        var found = new HashSet<EntityHandle>();
        query.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity));
        Assert.Equal(2, found.Count);
        Assert.Equal(1, world.ComponentTypeCount);

        world.AddComponent(first, new Tag0());
        found.Clear();
        query.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity));
        Assert.Equal(second, Assert.Single(found));
        Assert.True(world.RemoveComponent<Tag0>(first));
        found.Clear();
        query.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity));
        Assert.Equal(2, found.Count);
        Assert.Contains(first, found);
        Assert.Contains(second, found);

        world.DestroyEntity(first);
        var replacement = world.CreateEntity();
        world.AddComponent(replacement, new Position(233, 239));
        found.Clear();
        query.ForEach((EntityHandle entity, scoped in Position position) => found.Add(entity));
        Assert.Equal(2, found.Count);
        Assert.Contains(replacement, found);
        Assert.Contains(second, found);
        Assert.DoesNotContain(first, found);
        Assert.Equal(2, world.ComponentTypeCount);
    }

    [Fact]
    public void WriteCallbacks_UpdateOnlyDeclaredMembershipAndComponents()
    {
        using var world = CreateWorld();
        var matching = world.CreateEntity();
        var filtered = world.CreateEntity();
        var unmatched = world.CreateEntity();
        world.AddComponent(matching, new Position(107, 109));
        world.AddComponent(matching, new Velocity(5));
        world.AddComponent(filtered, new Position(113, 127));
        world.AddComponent(filtered, new Velocity(7));
        world.AddComponent(filtered, new Tag0());
        world.AddComponent(unmatched, new Position(131, 137));
        var query = world.Query<Position, Velocity>().Without<Tag0>();

        query.ForEachWrite((EntityHandle entity, scoped ref Position position, scoped in Velocity velocity) =>
        {
            position.X += velocity.Value;
            position.Y -= velocity.Value;
        });
        Assert.Equal(new Position(112, 104), world.GetComponent<Position>(matching));
        Assert.Equal(new Velocity(5), world.GetComponent<Velocity>(matching));
        query.ForEachWriteBoth((EntityHandle entity, scoped ref Position position, scoped ref Velocity velocity) =>
        {
            position.Y += velocity.Value;
            velocity.Value *= 3;
        });
        Assert.Equal(new Position(112, 109), world.GetComponent<Position>(matching));
        Assert.Equal(new Velocity(15), world.GetComponent<Velocity>(matching));
        Assert.Equal(new Position(113, 127), world.GetComponent<Position>(filtered));
        Assert.Equal(new Velocity(7), world.GetComponent<Velocity>(filtered));
        Assert.Equal(new Position(131, 137), world.GetComponent<Position>(unmatched));

        world.Query<Position>().Without<Tag0>().ForEachWrite((EntityHandle entity, scoped ref Position position) => position.X *= 2);
        Assert.Equal(new Position(224, 109), world.GetComponent<Position>(matching));
        Assert.Equal(new Position(262, 137), world.GetComponent<Position>(unmatched));
        Assert.Equal(new Position(113, 127), world.GetComponent<Position>(filtered));
    }

    [Fact]
    public void InvalidQueryDescriptorsAndCallbacks_AreRejectedBeforeTraversal()
    {
        using var world = CreateWorld();
        var query = world.Query<Position>();
        var pair = world.Query<Position, Velocity>();
        Assert.Throws<ArgumentException>(() => world.Query<Position, Position>());
        Assert.Throws<NotSupportedException>(() => world.Query<ManagedValue>());
        Assert.Throws<NotSupportedException>(() => world.Query<ManagedValue, Position>());
        Assert.Throws<NotSupportedException>(() => world.Query<Position, ManagedValue>());
        Assert.Throws<NotSupportedException>(() => pair.Without<ManagedValue>());
        Assert.Throws<ArgumentNullException>(() => query.ForEach(null!));
        Assert.Throws<ArgumentNullException>(() => query.ForEachWrite(null!));
        Assert.Throws<ArgumentNullException>(() => pair.ForEach(null!));
        Assert.Throws<ArgumentNullException>(() => pair.ForEachWrite(null!));
        Assert.Throws<ArgumentNullException>(() => pair.ForEachWriteBoth(null!));
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position>).Without<Tag0>());
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position>).ForEach(Read));
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position>).ForEachWrite(Write));
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position, Velocity>).Without<Tag0>());
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position, Velocity>).ForEach(ReadPair));
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position, Velocity>).ForEachWrite(WritePair));
        Assert.Throws<InvalidOperationException>(() => default(EntityQuery<Position, Velocity>).ForEachWriteBoth(WriteBoth));
        Assert.Equal(0, world.ComponentTypeCount);

        static void Read(EntityHandle entity, scoped in Position value) { }
        static void Write(EntityHandle entity, scoped ref Position value) { }
        static void ReadPair(EntityHandle entity, scoped in Position first, scoped in Velocity second) { }
        static void WritePair(EntityHandle entity, scoped ref Position first, scoped in Velocity second) { }
        static void WriteBoth(EntityHandle entity, scoped ref Position first, scoped ref Velocity second) { }
    }

    [Fact]
    public void DisposedWorld_RejectsNewRetainedAndFilteredQueries()
    {
        var world = CreateWorld();
        var query = world.Query<Position>();
        var pair = world.Query<Position, Velocity>();
        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => world.Query<Position>());
        Assert.Throws<ObjectDisposedException>(() => world.Query<Position, Velocity>());
        Assert.Throws<ObjectDisposedException>(() => query.Without<Tag0>());
        Assert.Throws<ObjectDisposedException>(() => pair.Without<Tag0>());
        Assert.Throws<ObjectDisposedException>(() => query.ForEach((EntityHandle entity, scoped in Position value) => { }));
        Assert.Throws<ObjectDisposedException>(() => query.ForEachWrite((EntityHandle entity, scoped ref Position value) => { }));
        Assert.Throws<ObjectDisposedException>(() => pair.ForEach((EntityHandle entity, scoped in Position first, scoped in Velocity second) => { }));
        Assert.Throws<ObjectDisposedException>(() => pair.ForEachWrite((EntityHandle entity, scoped ref Position first, scoped in Velocity second) => { }));
        Assert.Throws<ObjectDisposedException>(() => pair.ForEachWriteBoth((EntityHandle entity, scoped ref Position first, scoped ref Velocity second) => { }));
        Assert.True(world.IsDisposed);
    }

    [Fact]
    public void Traversal_RejectsStructuralMutationAndDirectWrites()
    {
        using var world = CreateWorld();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(139, 149));
        var visits = 0;
        world.Query<Position>().ForEach((EntityHandle current, scoped in Position value) =>
        {
            visits++;
            Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
            Assert.Throws<InvalidOperationException>(() => world.DestroyEntity(current));
            Assert.Throws<InvalidOperationException>(() => world.AddComponent(current, new Velocity(17)));
            Assert.Throws<InvalidOperationException>(() => world.RemoveComponent<Position>(current));
            Assert.Throws<InvalidOperationException>(() => world.SetComponent(current, new Position(-1, -1)));
            Assert.Throws<InvalidOperationException>(() => world.Dispose());
            Assert.Equal(new Position(139, 149), world.GetComponent<Position>(current));
        });
        Assert.Equal(1, visits);
        Assert.False(world.IsDisposed);
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.False(world.HasComponent<Velocity>(entity));
        world.SetComponent(entity, new Position(151, 157));
        Assert.Equal(new Position(151, 157), world.GetComponent<Position>(entity));
        Assert.True(world.IsAlive(world.CreateEntity()));
    }

    [Fact]
    public void Traversal_AllowsCopyReadsAndRejectsNestedIteration()
    {
        using var world = CreateWorld();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(163, 167));
        world.AddComponent(entity, new Velocity(19));
        var nestedCalls = 0;
        var visits = 0;
        world.Query<Position>().ForEach((EntityHandle current, scoped in Position value) =>
        {
            visits++;
            var copy = world.GetComponent<Position>(current);
            copy.X = -1;
            Assert.True(world.TryGetComponent<Position>(current, out var secondCopy));
            Assert.Equal(new Position(163, 167), secondCopy);
            Assert.True(world.HasComponent<Position>(current));
            Assert.True(world.IsAlive(current));
            Assert.Throws<InvalidOperationException>(() => world.Query<Position>().ForEach(
                (EntityHandle inner, scoped in Position position) => nestedCalls++));
            Assert.Throws<InvalidOperationException>(() => world.Query<Position, Velocity>().ForEachWriteBoth(
                (EntityHandle inner, scoped ref Position position, scoped ref Velocity velocity) => nestedCalls++));
            Assert.Throws<InvalidOperationException>(() => world.Query<Tag8>().ForEach(
                (EntityHandle inner, scoped in Tag8 tag) => nestedCalls++));
        });
        Assert.Equal(1, visits);
        Assert.Equal(0, nestedCalls);
        Assert.Equal(new Position(163, 167), world.GetComponent<Position>(entity));
        Assert.Equal(2, world.ComponentTypeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackFailure_PreservesCompletedWritesAndReleasesTraversalGuard(bool pair)
    {
        using var world = CreateWorld();
        var entities = Enumerable.Range(0, 3).Select(_ => world.CreateEntity()).ToArray();
        foreach (var entity in entities)
        {
            world.AddComponent(entity, new Position(173 + entity.Index, 179));
            world.AddComponent(entity, new Velocity(23));
        }
        var expected = new IOException("query callback fixture");
        EntityHandle changed = default;
        var visits = 0;
        Exception actual;
        if (pair)
            actual = Assert.Throws<IOException>(() => world.Query<Position, Velocity>().ForEachWriteBoth(
                (EntityHandle entity, scoped ref Position position, scoped ref Velocity velocity) =>
                {
                    changed = entity;
                    visits++;
                    position.X += 1000;
                    velocity.Value = 29;
                    throw expected;
                }));
        else
            actual = Assert.Throws<IOException>(() => world.Query<Position>().ForEachWrite(
                (EntityHandle entity, scoped ref Position position) =>
                {
                    changed = entity;
                    visits++;
                    position.X += 1000;
                    throw expected;
                }));

        Assert.Same(expected, actual);
        Assert.Equal(1, visits);
        Assert.False(world.IsFaulted);
        foreach (var entity in entities)
        {
            Assert.Equal(new Position(173 + entity.Index + (entity == changed ? 1000 : 0), 179), world.GetComponent<Position>(entity));
            Assert.Equal(new Velocity(pair && entity == changed ? 29 : 23), world.GetComponent<Velocity>(entity));
        }
        world.SetComponent(changed, new Position(181, 191));
        Assert.Equal(new Position(181, 191), world.GetComponent<Position>(changed));
        Assert.True(world.IsAlive(world.CreateEntity()));
    }

    [Fact]
    public void FaultedWorld_AllowsReadQueriesButRejectsEveryWriteQuery()
    {
        using var world = CreateWorld();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new Position(193, 197));
        world.AddComponent(entity, new Velocity(31));
        var failure = new IOException("unknown commit outcome fixture");
        world.MarkFaulted(failure);
        var singleVisits = 0;
        var pairVisits = 0;
        world.Query<Position>().ForEach((EntityHandle current, scoped in Position position) =>
        {
            singleVisits++;
            Assert.Equal(entity, current);
            Assert.Equal(new Position(193, 197), position);
        });
        world.Query<Position, Velocity>().Without<Tag0>().ForEach((EntityHandle current, scoped in Position position, scoped in Velocity velocity) =>
        {
            pairVisits++;
            Assert.Equal(new Velocity(31), velocity);
        });
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => world.Query<Position>().ForEachWrite(
            (EntityHandle current, scoped ref Position position) => position.X = -1)).InnerException);
        Assert.Throws<InvalidOperationException>(() => world.Query<Position, Velocity>().ForEachWrite(
            (EntityHandle current, scoped ref Position position, scoped in Velocity velocity) => position.X = -1));
        Assert.Throws<InvalidOperationException>(() => world.Query<Position, Velocity>().ForEachWriteBoth(
            (EntityHandle current, scoped ref Position position, scoped ref Velocity velocity) => velocity.Value = -1));
        Assert.Throws<InvalidOperationException>(() => world.Query<Tag8>().ForEachWrite(
            (EntityHandle current, scoped ref Tag8 tag) => { }));
        Assert.Equal((1, 1), (singleVisits, pairVisits));
        Assert.Equal(new Position(193, 197), world.GetComponent<Position>(entity));
        Assert.Equal(new Velocity(31), world.GetComponent<Velocity>(entity));
        Assert.True(world.IsFaulted);
    }

    private static World CreateWorld() => new(new WorldOptions { InitialEntityCapacity = 0, MaximumEntityCount = 32 });
    private struct Position(int x, int y)
    {
        internal int X = x;
        internal int Y = y;
    }
    private struct Velocity(int value) { internal int Value = value; }
    private readonly record struct ManagedValue(string Text);
    private readonly struct Tag0 { }
    private readonly struct Tag1 { }
    private readonly struct Tag2 { }
    private readonly struct Tag3 { }
    private readonly struct Tag4 { }
    private readonly struct Tag5 { }
    private readonly struct Tag6 { }
    private readonly struct Tag7 { }
    private readonly struct Tag8 { }
}
