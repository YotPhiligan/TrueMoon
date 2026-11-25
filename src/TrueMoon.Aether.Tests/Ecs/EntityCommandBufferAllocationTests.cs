using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Runtime.Ecs;
using Xunit;

namespace TrueMoon.Aether.Tests.Ecs;

[Collection(nameof(EntityCommandBufferAllocationCollection))]
public sealed class EntityCommandBufferAllocationTests
{
    [Fact]
    public void MixedPayloads_CopyUnalignedValuesAndSurviveRejectedOverflow()
    {
        using var world = new World();
        using var commands = new EntityCommandBuffer(world, 10);
        var entity = commands.CreateEntity();
        var packed = new PackedValue(0x71, -0x112233445566L, BitConverter.Int32BitsToSingle(0x7fc01234));
        commands.AddComponent(entity, (byte)0x19);
        commands.AddComponent(entity, 0x0123456789abcdefUL);
        commands.AddComponent(entity, new PackedValue(0x51, 17, 2));
        commands.AddComponent(entity, default(Tag));
        commands.SetComponent(entity, (byte)0xe1);
        commands.SetComponent(entity, packed);
        commands.RemoveComponent<ulong>(entity);
        commands.AddComponent(entity, 0xfedcba9876543210UL);
        commands.AddComponent(entity, true);
        packed = new(0xff, 999, -7);

        Assert.Equal(10, commands.Count);
        Assert.Throws<InvalidOperationException>(() => commands.SetComponent(entity, packed));
        Assert.Equal((10, 0, 0), (commands.Count, world.EntityCount, world.ComponentTypeCount));
        var result = commands.Playback();
        var live = result.Resolve(entity);
        Assert.Equal((byte)0xe1, world.GetComponent<byte>(live));
        Assert.Equal(0xfedcba9876543210UL, world.GetComponent<ulong>(live));
        var actual = world.GetComponent<PackedValue>(live);
        Assert.Equal((byte)0x71, actual.Lead);
        Assert.Equal(-0x112233445566L, actual.Counter);
        Assert.Equal(0x7fc01234, BitConverter.SingleToInt32Bits(actual.Number));
        Assert.True(world.HasComponent<Tag>(live));
        Assert.True(world.GetComponent<bool>(live));
        Assert.Equal((0, 10, 5), (commands.Count, commands.AppliedCommandCount, world.ComponentTypeCount));

        commands.SetComponent(live, new PackedValue(0x99, long.MinValue, -0f));
        commands.Reset();
        commands.SetComponent(live, new PackedValue(0x25, long.MaxValue, 1.25f));
        commands.SetComponent(live, false);
        commands.Playback();
        Assert.Equal(new PackedValue(0x25, long.MaxValue, 1.25f), world.GetComponent<PackedValue>(live));
        Assert.False(world.GetComponent<bool>(live));
        Assert.Equal(2, commands.AppliedCommandCount);
        Assert.Equal(live, result.Resolve(entity));
    }

    [Fact]
    public void SegmentedPayloads_CopyAcrossBlocksAndReuseOversizedValuesAfterReset()
    {
        const int count = 300;
        Assert.Equal(512, Unsafe.SizeOf<BlockValue>());
        Assert.Equal(60000, Unsafe.SizeOf<OversizedValue>());
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 512 });
        using var commands = new EntityCommandBuffer(world, 1024);
        var owner = commands.CreateEntity();
        commands.AddComponent(owner, (byte)0x19);
        var tokens = new DeferredEntity[count];
        for (var index = 0; index < count; index++)
        {
            tokens[index] = commands.CreateEntity();
            commands.AddComponent(tokens[index], Medium(index));
            if (index == 129) RecordLarge(commands, owner, add: true, 71, 73, -79);
        }
        commands.SetComponent(tokens[0], Medium(10000));
        commands.RemoveComponent<BlockValue>(tokens[128]);
        commands.AddComponent(tokens[128], Medium(10128));
        commands.SetComponent(tokens[256], Medium(10256));
        commands.SetComponent(owner, (byte)0xe7);

        var first = commands.Playback();
        var liveOwner = first.Resolve(owner);
        var live = tokens.Select(first.Resolve).ToArray();
        Assert.Equal((0, 608, 301, 3), (commands.Count, commands.AppliedCommandCount, world.EntityCount, world.ComponentTypeCount));
        Assert.Equal((byte)0xe7, world.GetComponent<byte>(liveOwner));
        AssertLarge(world, liveOwner, 71, 73, -79);
        for (var index = 0; index < count; index++)
            AssertMedium(live[index], index is 0 or 128 or 256 ? 10000 + index : index);

        // Reuse blocks after success with an oversized value before several ordinary blocks.
        RecordLarge(commands, liveOwner, add: false, 83, 89, -97);
        for (var index = 0; index < count; index++) commands.SetComponent(live[index], Medium(20000 + index));
        commands.RemoveComponent<byte>(liveOwner);
        commands.AddComponent(liveOwner, (byte)0xa5);
        commands.SetComponent(live[0], Medium(30000));
        Assert.Equal(0, commands.Playback().CreatedEntityCount);
        Assert.Equal(304, commands.AppliedCommandCount);
        AssertLarge(world, liveOwner, 83, 89, -97);
        Assert.Equal((byte)0xa5, world.GetComponent<byte>(liveOwner));
        for (var index = 0; index < count; index++) AssertMedium(live[index], index == 0 ? 30000 : 20000 + index);

        RecordLarge(commands, liveOwner, add: false, -101, -103, -107);
        for (var index = 0; index < count; index++) commands.SetComponent(live[index], Medium(-index - 1));
        commands.Reset();
        AssertLarge(world, liveOwner, 83, 89, -97);
        AssertMedium(live[0], 30000);
        commands.SetComponent(live[0], Medium(40000));
        RecordLarge(commands, liveOwner, add: false, 109, 113, -127);
        commands.SetComponent(live[0], Medium(50000));
        commands.Playback();
        AssertMedium(live[0], 50000);
        for (var index = 1; index < count; index++) AssertMedium(live[index], 20000 + index);
        AssertLarge(world, liveOwner, 109, 113, -127);
        Assert.Equal((0, 3, 301), (commands.Count, commands.AppliedCommandCount, world.EntityCount));
        Assert.Equal(liveOwner, first.Resolve(owner));
        Assert.False(world.IsFaulted);

        static BlockValue Medium(int serial) => new() { Serial = serial, Middle = 900000L + serial, Tail = ~serial };
        void AssertMedium(EntityHandle entity, int serial)
        {
            var actual = world.GetComponent<BlockValue>(entity);
            Assert.Equal(serial, actual.Serial);
            Assert.Equal(900000L + serial, actual.Middle);
            Assert.Equal(~serial, actual.Tail);
        }
    }

    // Keep 60 KB value temporaries out of the caller's frame and check the last bytes, not just its header.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RecordLarge(EntityCommandBuffer commands, CommandTarget entity, bool add, long head, int middle, long tail)
    {
        var value = new OversizedValue { Head = head, Middle = middle, Tail = tail };
        if (add) commands.AddComponent(entity, value);
        else commands.SetComponent(entity, value);
        value.Head = -1; value.Middle = -1; value.Tail = -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertLarge(World world, EntityHandle entity, long head, int middle, long tail)
    {
        var actual = world.GetComponent<OversizedValue>(entity);
        Assert.Equal(head, actual.Head); Assert.Equal(middle, actual.Middle); Assert.Equal(tail, actual.Tail);
    }

    [Fact]
    public void PublishedMaps_RemainIndependentAcrossGrowthReuseResetAndDispose()
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 512 });
        var commands = new EntityCommandBuffer(world, 1024);
        var first = commands.CreateEntity();
        commands.AddComponent(first, 11);
        var firstMap = commands.Playback();
        var firstHandle = firstMap.Resolve(first);

        commands.DestroyEntity(firstHandle);
        var many = new DeferredEntity[257];
        for (var index = 0; index < many.Length; index++)
        {
            many[index] = commands.CreateEntity();
            commands.AddComponent(many[index], 1000L + index);
        }
        var largeMap = commands.Playback();
        var reused = largeMap.Resolve(many[0]);
        var last = largeMap.Resolve(many[^1]);
        Assert.Equal(firstHandle.Index, reused.Index);
        Assert.Equal(firstHandle.Generation + 1, reused.Generation);
        Assert.False(world.IsAlive(firstHandle));
        Assert.Equal(1256L, world.GetComponent<long>(last));
        Assert.Equal(257, largeMap.CreatedEntityCount);

        commands.Reset();
        commands.DestroyEntity(reused);
        var final = commands.CreateEntity();
        commands.AddComponent(final, (byte)0x5a);
        var finalMap = commands.Playback();
        var finalHandle = finalMap.Resolve(final);
        commands.Dispose();
        Assert.Equal(firstHandle, firstMap.Resolve(first));
        Assert.Equal(reused, largeMap.Resolve(many[0]));
        Assert.Equal(last, largeMap.Resolve(many[^1]));
        Assert.Equal(reused.Index, finalHandle.Index);
        Assert.Equal(reused.Generation + 1, finalHandle.Generation);
        Assert.Equal((byte)0x5a, world.GetComponent<byte>(finalHandle));
        Assert.Throws<ArgumentException>(() => largeMap.Resolve(final));
        Assert.Throws<ArgumentException>(() => finalMap.Resolve(first));
        Assert.Equal(257, world.EntityCount);
    }

    [Fact]
    public void ReusedValidation_FailedBatchThenResetReadsFreshAllocatorState()
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 8 });
        using var commands = new EntityCommandBuffer(world, 16);
        var warm = commands.CreateEntity();
        commands.AddComponent(warm, 13);
        commands.DestroyEntity(warm);
        var warmMap = commands.Playback();
        var retired = warmMap.Resolve(warm);

        var rejected = commands.CreateEntity();
        commands.AddComponent(rejected, 17);
        commands.AddComponent(rejected, 19);
        Assert.Throws<InvalidOperationException>(() => commands.Playback());
        Assert.Equal((0, 1, 0), (world.EntityCount, world.ComponentTypeCount, commands.AppliedCommandCount));
        Assert.False(world.IsFaulted);
        Assert.True(commands.IsFaulted);

        commands.Reset();
        var outside = world.CreateEntity();
        world.AddComponent(outside, 23);
        commands.DestroyEntity(outside);
        var replacement = commands.CreateEntity();
        commands.AddComponent(replacement, 29L);
        var map = commands.Playback();
        var live = map.Resolve(replacement);
        Assert.Equal(retired.Index, outside.Index);
        Assert.Equal(retired.Generation + 1, outside.Generation);
        Assert.Equal(outside.Index, live.Index);
        Assert.Equal(outside.Generation + 1, live.Generation);
        Assert.Equal(29L, world.GetComponent<long>(live));
        Assert.False(world.HasComponent<int>(live));
        Assert.Equal((1, 2, 3), (world.EntityCount, world.ComponentTypeCount, commands.AppliedCommandCount));
        Assert.False(commands.IsFaulted);
        Assert.Null(commands.Failure);
        Assert.Equal(retired, warmMap.Resolve(warm));
    }

    [Fact]
    public void ReusedValidation_CancellationRetryReadsExternalWorldChanges()
    {
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 8 });
        using var commands = new EntityCommandBuffer(world, 16);
        var warm = commands.CreateEntity();
        commands.AddComponent(warm, 31L);
        var warmHandle = commands.Playback().Resolve(warm);
        world.DestroyEntity(warmHandle);
        var pending = commands.CreateEntity();
        commands.AddComponent(pending, new PackedValue(3, 37, 4.5f));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => commands.Playback(cancellation.Token));
        Assert.Equal((2, 0, 0), (commands.Count, commands.AppliedCommandCount, world.EntityCount));
        Assert.False(commands.IsFaulted);

        var outside = world.CreateEntity();
        world.AddComponent(outside, 41L);
        var result = commands.Playback();
        var live = result.Resolve(pending);
        Assert.Equal(warmHandle.Index, outside.Index);
        Assert.Equal(warmHandle.Generation + 1, outside.Generation);
        Assert.NotEqual(outside.Index, live.Index);
        Assert.Equal(new PackedValue(3, 37, 4.5f), world.GetComponent<PackedValue>(live));
        Assert.Equal(41L, world.GetComponent<long>(outside));
        Assert.Equal((2, 2, 0), (world.EntityCount, commands.AppliedCommandCount, commands.Count));
        Assert.False(world.IsFaulted);
    }

    [Fact]
    public void CommitFailure_AfterPayloadPrefixReportsAppliedCountAndFaultsWorld()
    {
        using var world = new World();
        var first = world.CreateEntity(); var second = world.CreateEntity();
        world.AddComponent(first, (byte)3); world.AddComponent(second, new PackedValue(5, 43, 6));
        var failure = new IOException("fail after the copied byte payload");
        using var commands = new EntityCommandBuffer(world, 8, index => { if (index == 1) throw failure; });
        commands.SetComponent(first, (byte)0xe7);
        commands.SetComponent(second, new PackedValue(7, 47, 8));
        Assert.Same(failure, Assert.Throws<IOException>(() => commands.Playback()));
        Assert.Equal((byte)0xe7, world.GetComponent<byte>(first));
        Assert.Equal(new PackedValue(5, 43, 6), world.GetComponent<PackedValue>(second));
        Assert.Equal((2, 1), (commands.Count, commands.AppliedCommandCount));
        Assert.True(commands.IsFaulted); Assert.True(world.IsFaulted);
        Assert.Same(failure, commands.Failure);
        commands.Reset();
        Assert.Equal((0, 0), (commands.Count, commands.AppliedCommandCount));
        Assert.Null(commands.Failure); Assert.False(commands.IsFaulted);
        Assert.True(world.IsFaulted);
        Assert.Throws<InvalidOperationException>(() => commands.CreateEntity());
    }

    [Fact]
    public void WarmedContactLikeBatches_AllocateOnlyBoundedDetachedMappings()
    {
        const int count = 256, warmup = 32, measured = 32;
        using var world = new World(new() { InitialEntityCapacity = 0, MaximumEntityCount = 1024 });
        using var commands = new EntityCommandBuffer(world, 1024);
        var tokens = new DeferredEntity[count]; var live = new EntityHandle[count];
        for (var batch = 0; batch < warmup; batch++) RecordAndPlay(batch, batch != 0);
        var retiredFirst = live[0];
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var batch = warmup; batch < warmup + measured; batch++) RecordAndPlay(batch, true);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0L, measured * 32768L);
        Assert.Equal(count, world.EntityCount);
        Assert.Equal((0, count * 3), (commands.Count, commands.AppliedCommandCount));
        Assert.False(world.IsAlive(retiredFirst));
        Assert.Equal(warmup + measured - 1, world.GetComponent<ContactValue>(live[0]).Tick);
        Assert.Equal(count - 1, world.GetComponent<ContactValue>(live[^1]).Serial);
        Assert.Equal(new Vector3(count - 1, warmup + measured - 1, -count + 1), world.GetComponent<ContactValue>(live[^1]).Point);
        Assert.False(world.IsFaulted);

        void RecordAndPlay(int batch, bool destroy)
        {
            if (destroy) for (var index = 0; index < count; index++) commands.DestroyEntity(live[index]);
            for (var index = 0; index < count; index++)
            {
                tokens[index] = commands.CreateEntity();
                commands.AddComponent(tokens[index], new ContactValue(index, batch, new(index, batch, -index), new(1, 2, 3, 4), 7.5f));
            }
            var result = commands.Playback();
            for (var index = 0; index < count; index++) live[index] = result.Resolve(tokens[index]);
        }
    }

    [Fact]
    public void HandlerCacheLimit_DoesNotLimitLegalTypeChanges()
    {
        using var world = new World();
        var live = world.CreateEntity();
        using var commands = new EntityCommandBuffer(world, 1);
        commands.RemoveComponent<int>(live); commands.Playback();
        commands.RemoveComponent<long>(live); commands.Playback();
        commands.RemoveComponent<PackedValue>(live); commands.Playback();
        commands.RemoveComponent<Tag>(live); commands.Playback();
        Assert.Equal((0, 1), (commands.Count, commands.AppliedCommandCount));
        Assert.Equal((1, 0), (world.EntityCount, world.ComponentTypeCount));
        Assert.True(world.IsAlive(live));
        Assert.False(commands.IsFaulted);
    }

    [Theory]
    [InlineData("success", false)] [InlineData("success", true)]
    [InlineData("reset", false)] [InlineData("reset", true)]
    [InlineData("cancel-reset", false)] [InlineData("cancel-reset", true)]
    [InlineData("failure-reset", false)] [InlineData("failure-reset", true)]
    [InlineData("dispose", false)] [InlineData("dispose", true)]
    public void CollectibleHandlers_AreReleasedAfterBatchResetOrDispose(string outcome, bool sharedGeneric)
    {
        using var world = new World();
        var live = world.CreateEntity();
        using var commands = new EntityCommandBuffer(world, 16);
        var weak = QueueCollectible(world, commands, live, outcome, sharedGeneric);
        if (outcome == "dispose") Assert.True(commands.IsDisposed);
        else
        {
            commands.AddComponent(live, 53);
            commands.Playback();
            Assert.Equal(53, world.GetComponent<int>(live));
        }
        AssertCollected(weak);
        Assert.False(world.IsDisposed); Assert.True(world.IsAlive(live));
        Assert.Equal(outcome == "dispose" ? 0 : 1, world.ComponentTypeCount);
        GC.KeepAlive(world); GC.KeepAlive(commands);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PendingCollectibleRemoval_RetainsTypeUntilPlaybackThenReleases(bool sharedGeneric)
    {
        using var world = new World();
        var live = world.CreateEntity();
        using var commands = new EntityCommandBuffer(world);
        var weak = QueueCollectible(world, commands, live, "pending", sharedGeneric);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.True(weak.IsAlive);
        Assert.Equal(1, commands.Count);
        commands.Playback();
        AssertCollected(weak);
        Assert.Equal((0, 1, 0), (commands.Count, commands.AppliedCommandCount, world.ComponentTypeCount));
        Assert.True(world.IsAlive(live));
        GC.KeepAlive(world); GC.KeepAlive(commands);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference QueueCollectible(World world, EntityCommandBuffer commands, EntityHandle live, string outcome, bool sharedGeneric)
    {
        var context = new AssemblyLoadContext("command-payload-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        var weak = new WeakReference(context);
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(EntityCommandBufferAllocationTests).Assembly.Location);
            var privateType = assembly.GetType(typeof(CollectibleValue).FullName!, throwOnError: true)!;
            var component = sharedGeneric ? typeof(SharedEnvelope<>).MakeGenericType(privateType) : privateType;
            Assert.True(component.IsCollectible);
            if (sharedGeneric) Assert.False(component.Assembly.IsCollectible);
            CommandTarget target = live;
            if (outcome is "success" or "pending")
            {
                typeof(EntityCommandBuffer).GetMethod(nameof(EntityCommandBuffer.RemoveComponent))!.MakeGenericMethod(component).Invoke(commands, [target]);
                if (outcome == "success") commands.Playback();
            }
            else
            {
                var value = Activator.CreateInstance(component)!;
                typeof(EntityCommandBuffer).GetMethod(nameof(EntityCommandBuffer.AddComponent))!.MakeGenericMethod(component).Invoke(commands, [target, value]);
                switch (outcome)
                {
                    case "reset": commands.Reset(); break;
                    case "cancel-reset":
                        using (var end = new CancellationTokenSource())
                        { end.Cancel(); Assert.Throws<OperationCanceledException>(() => commands.Playback(end.Token)); }
                        Assert.Equal(1, commands.Count); commands.Reset(); break;
                    case "failure-reset":
                        commands.DestroyEntity(live); commands.SetComponent(live, 59);
                        Assert.Throws<ArgumentException>(() => commands.Playback());
                        Assert.True(commands.IsFaulted); Assert.False(world.IsFaulted);
                        Assert.True(world.IsAlive(live)); Assert.Equal(0, world.ComponentTypeCount);
                        commands.Reset(); break;
                    case "dispose": commands.Dispose(); break;
                    default: throw new ArgumentException(nameof(outcome));
                }
            }
            return weak;
        }
        finally { context.Unload(); }
    }

    private static void AssertCollected(WeakReference weak)
    {
        for (var attempt = 0; attempt < 12 && weak.IsAlive; attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(weak.IsAlive);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct PackedValue(byte Lead, long Counter, float Number);
    [StructLayout(LayoutKind.Explicit, Size = 512)]
    private struct BlockValue
    {
        [FieldOffset(0)] public int Serial;
        [FieldOffset(248)] public long Middle;
        [FieldOffset(508)] public int Tail;
    }
    [StructLayout(LayoutKind.Explicit, Size = 60000)]
    private struct OversizedValue
    {
        [FieldOffset(0)] public long Head;
        [FieldOffset(30000)] public int Middle;
        [FieldOffset(59992)] public long Tail;
    }
    private readonly record struct ContactValue(int Serial, int Tick, Vector3 Point, Vector4 Normal, float Impulse);
    private readonly record struct Tag;
    public struct CollectibleValue { public long Value; }
    public struct SharedEnvelope<T> where T : struct { public T Value; }
}
