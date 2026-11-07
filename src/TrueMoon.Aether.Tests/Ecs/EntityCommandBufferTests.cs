using TrueMoon.Aether.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

public sealed class EntityCommandBufferTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1048577)]
    public void Constructor_RejectsInvalidCommandLimits(int maximum)
    {
        using var world = new World();
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityCommandBuffer(world, maximum));
        Assert.Equal((0, 0), (world.EntityCount, world.ComponentTypeCount));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(1048576)]
    public void Constructor_AcceptsInclusiveCommandLimitBoundaries(int maximum)
    {
        using var world = new World();
        using var commands = new EntityCommandBuffer(world, maximum);
        var token = commands.CreateEntity();
        Assert.Equal(1, commands.Count);
        Assert.Equal(0, world.EntityCount);
        var result = commands.Playback();
        Assert.True(world.IsAlive(result.Resolve(token)));
        Assert.Equal(1, commands.AppliedCommandCount);
    }

    [Fact]
    public void Constructor_RejectsNullAndDisposedWorld()
    {
        Assert.Throws<ArgumentNullException>(() => new EntityCommandBuffer(null!));
        var world = new World();
        world.Dispose();
        Assert.Throws<ObjectDisposedException>(() => new EntityCommandBuffer(world));
    }

    [Fact]
    public void CommandLimit_RejectsOneBeyondCapacityAndRenewsAfterPlayback()
    {
        using var world = Limited(4);
        using var commands = new EntityCommandBuffer(world, 2);
        var first = commands.CreateEntity();
        var second = commands.CreateEntity();
        Assert.Throws<InvalidOperationException>(() => commands.CreateEntity());
        Assert.Equal(2, commands.Count);
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        Assert.Equal(0, world.EntityCount);

        var result = commands.Playback();
        Assert.Equal(2, result.CreatedEntityCount);
        Assert.True(world.IsAlive(result.Resolve(first)));
        Assert.True(world.IsAlive(result.Resolve(second)));
        Assert.Equal((0, 2), (commands.Count, commands.AppliedCommandCount));
        var next = commands.CreateEntity();
        Assert.Equal(0, next.Index);
        Assert.Equal(first.BufferId, next.BufferId);
        Assert.NotEqual(first.BatchId, next.BatchId);
        Assert.Equal(1, commands.Count);
        Assert.Equal(2, world.EntityCount);
    }

    [Fact]
    public void Playback_AppliesFifoCommandsAndResolvesDestroyedReservations()
    {
        using var world = Limited(5);
        var existing = world.CreateEntity();
        world.AddComponent(existing, 5);
        using var commands = new EntityCommandBuffer(world);
        var first = commands.CreateEntity();
        commands.AddComponent(first, 11);
        commands.SetComponent(first, 17);
        commands.RemoveComponent<int>(first);
        commands.RemoveComponent<int>(first);
        commands.AddComponent(first, 23);
        commands.SetComponent(existing, 31);
        var second = commands.CreateEntity();
        commands.AddComponent(second, 41L);
        commands.DestroyEntity(first);
        var third = commands.CreateEntity();
        commands.AddComponent(third, 43);
        var recorded = commands.Count;

        Assert.Equal(12, recorded);
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal(5, world.GetComponent<int>(existing));
        var result = commands.Playback();
        var removed = result.Resolve(first);
        var surviving = result.Resolve(second);
        var reused = result.Resolve(third);

        Assert.Equal(3, result.CreatedEntityCount);
        Assert.False(world.IsAlive(removed));
        Assert.Equal(removed.Index, reused.Index);
        Assert.Equal(removed.Generation + 1, reused.Generation);
        Assert.True(world.IsAlive(surviving));
        Assert.True(world.IsAlive(reused));
        Assert.Equal(31, world.GetComponent<int>(existing));
        Assert.Equal(41L, world.GetComponent<long>(surviving));
        Assert.Equal(43, world.GetComponent<int>(reused));
        Assert.False(world.HasComponent<int>(surviving));
        Assert.False(world.HasComponent<long>(reused));
        Assert.Equal((3, 2), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal((0, recorded), (commands.Count, commands.AppliedCommandCount));
        Assert.False(commands.IsFaulted);
        Assert.False(world.IsFaulted);
        Assert.Null(commands.Failure);
        commands.Dispose();
        Assert.Equal(reused, result.Resolve(third));
    }

    [Theory]
    [InlineData("default-target")]
    [InlineData("default-handle")]
    [InlineData("foreign-handle")]
    [InlineData("stale-handle")]
    [InlineData("default-token")]
    [InlineData("foreign-token")]
    [InlineData("reset-token")]
    [InlineData("played-token")]
    [InlineData("future-token")]
    [InlineData("negative-token")]
    [InlineData("wrong-batch")]
    public void Recording_InvalidTargetsDoNotChangeQueueOrWorld(string kind)
    {
        using var world = Limited(8);
        using var otherWorld = Limited(3);
        using var commands = new EntityCommandBuffer(world);
        using var otherCommands = new EntityCommandBuffer(world);
        var live = world.CreateEntity();
        var stale = world.CreateEntity();
        world.DestroyEntity(stale);
        world.AddComponent(live, 47);
        var own = commands.CreateEntity();
        CommandTarget invalid;
        switch (kind)
        {
            case "default-target": invalid = default; break;
            case "default-handle": invalid = default(EntityHandle); break;
            case "foreign-handle": invalid = otherWorld.CreateEntity(); break;
            case "stale-handle": invalid = stale; break;
            case "default-token": invalid = default(DeferredEntity); break;
            case "foreign-token": invalid = otherCommands.CreateEntity(); break;
            case "reset-token":
                commands.Reset(); commands.CreateEntity(); invalid = own; break;
            case "played-token":
                commands.Playback(); commands.CreateEntity(); invalid = own; break;
            case "future-token": invalid = new DeferredEntity(own.BufferId, own.BatchId, own.Index + 1); break;
            case "negative-token": invalid = new DeferredEntity(own.BufferId, own.BatchId, -1); break;
            case "wrong-batch": invalid = new DeferredEntity(own.BufferId, Guid.NewGuid(), own.Index); break;
            default: throw new ArgumentException(nameof(kind));
        }
        var count = commands.Count;
        var entities = world.EntityCount;

        Assert.Throws<ArgumentException>(() => commands.DestroyEntity(invalid));
        Assert.Throws<ArgumentException>(() => commands.AddComponent(invalid, 53));
        Assert.Throws<ArgumentException>(() => commands.SetComponent(invalid, 59));
        Assert.Throws<ArgumentException>(() => commands.RemoveComponent<int>(invalid));
        Assert.Equal(count, commands.Count);
        Assert.Equal(entities, world.EntityCount);
        Assert.Equal(47, world.GetComponent<int>(live));
        Assert.False(commands.IsFaulted);
        Assert.False(world.IsFaulted);
    }

    [Fact]
    public void SuccessfulPlayback_InvalidatesTokensButPreservesIndependentResultMapping()
    {
        using var world = Limited(4);
        using var commands = new EntityCommandBuffer(world);
        var first = commands.CreateEntity();
        var firstResult = commands.Playback();
        var firstHandle = firstResult.Resolve(first);
        Assert.Throws<ArgumentException>(() => commands.DestroyEntity(first));
        commands.Reset();
        var second = commands.CreateEntity();
        var secondResult = commands.Playback();

        Assert.Equal(first.BufferId, second.BufferId);
        Assert.NotEqual(first.BatchId, second.BatchId);
        Assert.Equal(0, second.Index);
        Assert.Equal(firstHandle, firstResult.Resolve(first));
        Assert.NotEqual(firstHandle, secondResult.Resolve(second));
        Assert.Throws<ArgumentException>(() => firstResult.Resolve(second));
        Assert.Throws<ArgumentException>(() => secondResult.Resolve(first));
        Assert.Throws<ArgumentException>(() => firstResult.Resolve(default));
        Assert.Throws<ArgumentException>(() => firstResult.Resolve(new(first.BufferId, first.BatchId, -1)));
        Assert.Throws<ArgumentException>(() => firstResult.Resolve(new(first.BufferId, first.BatchId, 1)));
        Assert.Throws<ArgumentException>(() => firstResult.Resolve(new(Guid.NewGuid(), first.BatchId, 0)));
        Assert.Equal(2, world.EntityCount);
    }

    [Theory]
    [InlineData("duplicate-add")]
    [InlineData("missing-set")]
    [InlineData("double-destroy")]
    [InlineData("after-destroy")]
    [InlineData("type-limit")]
    [InlineData("entity-limit")]
    [InlineData("new-type-then-failure")]
    public void Preflight_RejectsInvalidSequenceWithoutApplyingPrefixOrRegisteringTypes(string kind)
    {
        using var world = new World(new()
        {
            InitialEntityCapacity = 0, MaximumEntityCount = 3,
            MaximumComponentTypes = kind == "new-type-then-failure" ? 2 : 1
        });
        var first = world.CreateEntity();
        var neighbor = world.CreateEntity();
        world.AddComponent(first, 61);
        using var commands = new EntityCommandBuffer(world);
        commands.SetComponent(first, 67);
        switch (kind)
        {
            case "duplicate-add": commands.AddComponent(first, 71); break;
            case "missing-set": commands.SetComponent(neighbor, 73); break;
            case "double-destroy": commands.DestroyEntity(first); commands.DestroyEntity(first); break;
            case "after-destroy": commands.DestroyEntity(first); commands.SetComponent(first, 79); break;
            case "type-limit": commands.AddComponent(neighbor, 83L); break;
            case "entity-limit": commands.CreateEntity(); commands.CreateEntity(); break;
            case "new-type-then-failure": commands.AddComponent(neighbor, 89L); commands.SetComponent(neighbor, 97d); break;
            default: throw new ArgumentException(nameof(kind));
        }
        var count = commands.Count;
        Exception error = kind switch
        {
            "missing-set" or "new-type-then-failure" => Assert.Throws<KeyNotFoundException>(() => commands.Playback()),
            "double-destroy" or "after-destroy" => Assert.Throws<ArgumentException>(() => commands.Playback()),
            _ => Assert.Throws<InvalidOperationException>(() => commands.Playback())
        };
        Assert.Same(error, commands.Failure);
        Assert.True(commands.IsFaulted);
        Assert.Equal((count, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.False(world.IsFaulted);
        Assert.Equal((2, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.True(world.IsAlive(first));
        Assert.True(world.IsAlive(neighbor));
        Assert.Equal(61, world.GetComponent<int>(first));
        Assert.False(world.HasComponent<long>(neighbor));
        Assert.False(world.HasComponent<double>(neighbor));
        Assert.Throws<InvalidOperationException>(() => commands.CreateEntity());
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
        var unreserved = world.CreateEntity();
        Assert.Equal((2, 1u), (unreserved.Index, unreserved.Generation));
        if (kind == "new-type-then-failure")
        {
            world.AddComponent(neighbor, (short)101);
            Assert.Equal((short)101, world.GetComponent<short>(neighbor));
            Assert.Equal(2, world.ComponentTypeCount);
        }
    }

    [Fact]
    public void Preflight_RejectsHandleDestroyedAfterRecordingWithoutChangingReplacement()
    {
        using var world = Limited(3);
        var target = world.CreateEntity();
        var neighbor = world.CreateEntity();
        world.AddComponent(target, 103);
        world.AddComponent(neighbor, 107);
        using var commands = new EntityCommandBuffer(world);
        commands.SetComponent(neighbor, 109);
        commands.SetComponent(target, 113);
        world.DestroyEntity(target);
        var replacement = world.CreateEntity();
        world.AddComponent(replacement, 127);

        Assert.Throws<ArgumentException>(() => commands.Playback());
        Assert.Equal((2, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.True(commands.IsFaulted);
        Assert.False(world.IsFaulted);
        Assert.Equal(107, world.GetComponent<int>(neighbor));
        Assert.Equal(127, world.GetComponent<int>(replacement));
        Assert.False(world.IsAlive(target));
        Assert.Equal(2, world.EntityCount);
    }

    [Theory]
    [InlineData(1u, 3, 4, 1u)]
    [InlineData(2u, 0, 1, 2u)]
    public void Preflight_ModelsFreedStackPriorityAndGenerationRetirement(uint limit, int firstIndex, int secondIndex, uint generation)
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 5 }, limit);
        var destroyedInBatch = world.CreateEntity();
        var alreadyDestroyed = world.CreateEntity();
        var neighbor = world.CreateEntity();
        world.AddComponent(destroyedInBatch, 131);
        world.AddComponent(alreadyDestroyed, 137);
        world.AddComponent(neighbor, 139);
        world.DestroyEntity(alreadyDestroyed);
        using var commands = new EntityCommandBuffer(world);
        commands.DestroyEntity(destroyedInBatch);
        var first = commands.CreateEntity();
        var second = commands.CreateEntity();
        commands.AddComponent(first, 149);
        commands.AddComponent(second, 151);

        var result = commands.Playback();
        var firstHandle = result.Resolve(first);
        var secondHandle = result.Resolve(second);
        Assert.Equal((firstIndex, generation), (firstHandle.Index, firstHandle.Generation));
        Assert.Equal((secondIndex, generation), (secondHandle.Index, secondHandle.Generation));
        Assert.Equal(149, world.GetComponent<int>(firstHandle));
        Assert.Equal(151, world.GetComponent<int>(secondHandle));
        Assert.Equal(139, world.GetComponent<int>(neighbor));
        Assert.False(world.IsAlive(destroyedInBatch));
        Assert.False(world.IsAlive(alreadyDestroyed));
        Assert.Equal((3, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal(5, commands.AppliedCommandCount);
    }

    [Fact]
    public void Preflight_CannotEvictEmptyRetainedTypesOrReuseRetiredSlots()
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 1, MaximumComponentTypes = 1 }, 1);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 157);
        world.RemoveComponent<int>(entity);
        using var commands = new EntityCommandBuffer(world);
        commands.AddComponent(entity, 163L);
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.False(world.HasComponent<long>(entity));
        commands.Reset();
        commands.DestroyEntity(entity);
        commands.CreateEntity();
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
        Assert.True(world.IsAlive(entity));
        Assert.Equal((1, 1), (world.EntityCount, world.ComponentTypeCount));
        Assert.Equal(0, commands.AppliedCommandCount);
        Assert.False(world.IsFaulted);
    }

    [Fact]
    public void Cancellation_PreservesFreshBatchAndTokensForRetryAfterPreviousSuccess()
    {
        using var world = Limited(4);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 167);
        using var commands = new EntityCommandBuffer(world);
        commands.SetComponent(entity, 173);
        commands.Playback();
        Assert.Equal(1, commands.AppliedCommandCount);
        var deferred = commands.CreateEntity();
        commands.AddComponent(deferred, 179);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => commands.Playback(cancellation.Token));
        Assert.Equal((2, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        Assert.False(world.IsFaulted);
        Assert.Equal(1, world.EntityCount);
        Assert.Equal(173, world.GetComponent<int>(entity));
        var result = commands.Playback();
        Assert.Equal(179, world.GetComponent<int>(result.Resolve(deferred)));
        Assert.Equal((0, 2), (commands.Count, commands.AppliedCommandCount));
        Assert.Equal(2, world.EntityCount);
    }

    [Fact]
    public void CancellationAfterCommitBoundary_DoesNotInterruptValidatedBatch()
    {
        using var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 181);
        using var cancellation = new CancellationTokenSource();
        var observed = new List<int>();
        using var commands = new EntityCommandBuffer(world, 8, index =>
        {
            observed.Add(index);
            if (index == 0) cancellation.Cancel();
        });
        commands.SetComponent(entity, 191);
        var deferred = commands.CreateEntity();
        commands.AddComponent(deferred, 193);

        var result = commands.Playback(cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(new[] { 0, 1, 2 }, observed);
        Assert.Equal(191, world.GetComponent<int>(entity));
        Assert.Equal(193, world.GetComponent<int>(result.Resolve(deferred)));
        Assert.Equal((0, 3), (commands.Count, commands.AppliedCommandCount));
        Assert.False(commands.IsFaulted);
        Assert.False(world.IsFaulted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CommitFailure_ReportsCompletedPrefixAndBlocksWorldMutations(int failAt)
    {
        using var world = Limited(4);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 197);
        var expected = new IOException("deterministic commit failure");
        using var commands = new EntityCommandBuffer(world, 8, index => { if (index == failAt) throw expected; });
        commands.SetComponent(entity, 199);
        var deferred = commands.CreateEntity();
        commands.AddComponent(deferred, 211);

        Assert.Same(expected, Assert.Throws<IOException>(() => commands.Playback()));
        Assert.Same(expected, commands.Failure);
        Assert.True(commands.IsFaulted);
        Assert.True(world.IsFaulted);
        Assert.Equal((3, failAt), (commands.Count, commands.AppliedCommandCount));
        Assert.Equal(failAt == 0 ? 197 : 199, world.GetComponent<int>(entity));
        Assert.Equal(failAt == 2 ? 2 : 1, world.EntityCount);
        var inspected = new List<int>();
        world.Query<int>().ForEach((EntityHandle _, scoped in int value) => inspected.Add(value));
        Assert.Equal(new[] { failAt == 0 ? 197 : 199 }, inspected);
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
        Assert.Throws<InvalidOperationException>(() => world.DestroyEntity(entity));
        Assert.Throws<InvalidOperationException>(() => world.AddComponent(entity, 223L));
        Assert.Throws<InvalidOperationException>(() => world.SetComponent(entity, 227));
        Assert.Throws<InvalidOperationException>(() => world.RemoveComponent<int>(entity));
        Assert.Throws<InvalidOperationException>(() => world.Query<int>().ForEachWrite((EntityHandle _, scoped ref int value) => value++));
        Assert.Throws<InvalidOperationException>(() => new EntityCommandBuffer(world));
        commands.Reset();
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        Assert.Equal((0, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.True(world.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => commands.CreateEntity());
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
    }

    [Fact]
    public void Reset_DiscardsPendingBatchAndRecoversValidationFailure()
    {
        using var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 229);
        using var commands = new EntityCommandBuffer(world);
        var discarded = commands.CreateEntity();
        commands.AddComponent(entity, 233);
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
        commands.Reset();
        Assert.Equal((0, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        Assert.Equal(1, world.EntityCount);
        var replacement = commands.CreateEntity();
        Assert.NotEqual(discarded.BatchId, replacement.BatchId);
        Assert.Equal(0, replacement.Index);
        Assert.Throws<ArgumentException>(() => commands.AddComponent(discarded, 239));
        commands.AddComponent(replacement, 241);
        var result = commands.Playback();
        Assert.Equal(241, world.GetComponent<int>(result.Resolve(replacement)));
        Assert.Equal(229, world.GetComponent<int>(entity));
        Assert.Equal(2, world.EntityCount);
    }

    [Fact]
    public void Recording_RejectsManagedPayloadsWithoutConsumingCommandBudget()
    {
        using var world = Limited(3);
        var entity = world.CreateEntity();
        using var commands = new EntityCommandBuffer(world, 1);
        Assert.Throws<NotSupportedException>(() => commands.AddComponent(entity, new ManagedValue("managed")));
        Assert.Throws<NotSupportedException>(() => commands.SetComponent(entity, new ManagedValue("managed")));
        Assert.Throws<NotSupportedException>(() => commands.RemoveComponent<ManagedValue>(entity));
        Assert.Equal((0, 0), (commands.Count, world.ComponentTypeCount));
        commands.AddComponent(entity, 251);
        commands.Playback();
        Assert.Equal(251, world.GetComponent<int>(entity));
        Assert.Equal((0, 1), (commands.Count, commands.AppliedCommandCount));
    }

    [Fact]
    public void PendingValues_AreCopiedAndNeverDisposedByResetOrBufferDisposal()
    {
        DisposableValue.Disposals = 0;
        using var world = Limited(3);
        var entity = world.CreateEntity();
        var commands = new EntityCommandBuffer(world);
        var value = new DisposableValue { Id = 257 };
        commands.AddComponent(entity, value);
        value.Id = -1;
        commands.Playback();
        Assert.Equal(257, world.GetComponent<DisposableValue>(entity).Id);
        commands.SetComponent(entity, new DisposableValue { Id = 263 });
        commands.Reset();
        Assert.Equal(257, world.GetComponent<DisposableValue>(entity).Id);
        commands.SetComponent(entity, new DisposableValue { Id = 269 });
        commands.Dispose();
        commands.Dispose();
        Assert.Equal(0, DisposableValue.Disposals);
        Assert.False(world.IsDisposed);
        Assert.Equal(257, world.GetComponent<DisposableValue>(entity).Id);
        Assert.Equal(1, world.EntityCount);
    }

    [Fact]
    public void Playback_DuringQueryIsRejectedWithoutFaultingAndCanRunAfterTraversal()
    {
        using var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 271);
        using var commands = new EntityCommandBuffer(world);
        world.Query<int>().ForEach((EntityHandle current, scoped in int value) =>
        {
            commands.SetComponent(current, 277);
            Assert.Throws<InvalidOperationException>(() => commands.Playback());
            Assert.Equal(271, value);
            Assert.Equal(1, commands.Count);
            Assert.False(commands.IsFaulted);
        });
        commands.Playback();
        Assert.Equal(277, world.GetComponent<int>(entity));
        Assert.Equal((0, 1), (commands.Count, commands.AppliedCommandCount));
        Assert.False(world.IsFaulted);
    }

    [Fact]
    public void Playback_RejectsReentrantRecordingResetAndDisposeUntilCommitCompletes()
    {
        using var world = Limited(3);
        EntityCommandBuffer commands = null!;
        using (commands = new EntityCommandBuffer(world, 4, _ =>
        {
            Assert.Throws<InvalidOperationException>(() => commands.CreateEntity());
            Assert.Throws<InvalidOperationException>(() => commands.Playback());
            Assert.Throws<InvalidOperationException>(() => commands.Reset());
            Assert.Throws<InvalidOperationException>(() => commands.Dispose());
            Assert.False(commands.IsDisposed);
        }))
        {
            var first = commands.CreateEntity();
            var second = commands.CreateEntity();
            var result = commands.Playback();
            Assert.True(world.IsAlive(result.Resolve(first)));
            Assert.True(world.IsAlive(result.Resolve(second)));
            Assert.Equal((0, 2), (commands.Count, commands.AppliedCommandCount));
            Assert.False(commands.IsFaulted);
            commands.Reset();
            Assert.Equal(0, commands.AppliedCommandCount);
        }
    }

    [Fact]
    public void EmptyPlaybackAndDisposedBuffer_HaveExplicitOutcomesWithoutOwningWorld()
    {
        using var world = Limited(3);
        var commands = new EntityCommandBuffer(world);
        var first = commands.Playback();
        var second = commands.Playback();
        Assert.Same(first, second);
        Assert.Equal(0, first.CreatedEntityCount);
        Assert.Throws<ArgumentException>(() => first.Resolve(default));
        var live = world.CreateEntity();
        world.AddComponent(live, 281);
        commands.SetComponent(live, 283);
        Assert.Same(first, commands.Playback());
        Assert.Equal(283, world.GetComponent<int>(live));
        Assert.Equal(1, commands.AppliedCommandCount);
        commands.Dispose();
        commands.Dispose();
        Assert.True(commands.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => commands.Count);
        Assert.Throws<ObjectDisposedException>(() => commands.CreateEntity());
        Assert.Throws<ObjectDisposedException>(() => commands.DestroyEntity(live));
        Assert.Throws<ObjectDisposedException>(() => commands.AddComponent(live, 293));
        Assert.Throws<ObjectDisposedException>(() => commands.SetComponent(live, 307));
        Assert.Throws<ObjectDisposedException>(() => commands.RemoveComponent<int>(live));
        Assert.Throws<ObjectDisposedException>(() => commands.Playback());
        Assert.Throws<ObjectDisposedException>(() => commands.Reset());
        Assert.False(world.IsDisposed);
        Assert.True(world.IsAlive(live));
        Assert.Equal(283, world.GetComponent<int>(live));
    }

    [Fact]
    public void DisposedBorrowedWorld_RejectsRecordingAndPlaybackButBufferCanDiscardItsQueue()
    {
        var world = Limited(3);
        var entity = world.CreateEntity();
        world.AddComponent(entity, 311);
        using var commands = new EntityCommandBuffer(world);
        commands.SetComponent(entity, 313);
        world.Dispose();

        Assert.Throws<ObjectDisposedException>(() => commands.CreateEntity());
        Assert.Throws<ObjectDisposedException>(() => commands.DestroyEntity(entity));
        Assert.Throws<ObjectDisposedException>(() => commands.AddComponent(entity, 317L));
        Assert.Throws<ObjectDisposedException>(() => commands.SetComponent(entity, 331));
        Assert.Throws<ObjectDisposedException>(() => commands.RemoveComponent<int>(entity));
        Assert.Throws<ObjectDisposedException>(() => commands.Playback());
        Assert.Equal((1, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.False(commands.IsDisposed);
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        commands.Reset();
        Assert.Equal((0, 0), (commands.Count, commands.AppliedCommandCount));
        commands.Dispose();
        commands.Dispose();
        Assert.True(commands.IsDisposed);
        Assert.True(world.IsDisposed);
    }

    private static World Limited(int maximum) => new(new WorldOptions { InitialEntityCapacity = 0, MaximumEntityCount = maximum });
    private readonly record struct ManagedValue(string Value);
    private struct DisposableValue : IDisposable
    {
        internal static int Disposals;
        internal int Id;
        public void Dispose() => Disposals++;
    }
}
