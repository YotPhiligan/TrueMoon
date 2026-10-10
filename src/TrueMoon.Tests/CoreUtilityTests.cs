using System.Buffers;
using System.Reflection;
using TrueMoon.Threading;
using Xunit;

namespace TrueMoon.Tests;

public class CoreUtilityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static Task Schedule(TmTaskScheduler scheduler, Action action) =>
        Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SchedulerRejectsInvalidWorkerCounts(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new TmTaskScheduler("invalid", count));

    [Fact]
    public async Task SchedulerDisposeDrainsAcceptedWorkAndTerminatesWorkers()
    {
        using var scheduler = new TmTaskScheduler("drain", 1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var count = 0;
        var first = Schedule(scheduler, () => { entered.Set(); Assert.True(release.Wait(Timeout)); Interlocked.Increment(ref count); });
        Assert.True(entered.Wait(Timeout));
        var second = Schedule(scheduler, () => Interlocked.Increment(ref count));
        var third = Schedule(scheduler, () => Interlocked.Increment(ref count));
        var disposing = Task.Run(scheduler.Dispose);
        release.Set();
        await Task.WhenAll(first, second, third, disposing).WaitAsync(Timeout);
        Assert.Equal(3, count);
        Assert.True(scheduler.Completion.IsCompletedSuccessfully);
        scheduler.Dispose();
        var error = Assert.Throws<TaskSchedulerException>(() => { _ = Schedule(scheduler, () => { }); });
        Assert.IsType<ObjectDisposedException>(error.InnerException);
    }

    [Fact]
    public async Task SchedulerCancellationDrainsQueueAndRejectsNewTasks()
    {
        using var cts = new CancellationTokenSource();
        using var scheduler = new TmTaskScheduler("cancel", 1, cts.Token);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var ran = false;
        var first = Schedule(scheduler, () => { entered.Set(); Assert.True(release.Wait(Timeout)); });
        Assert.True(entered.Wait(Timeout));
        var second = Schedule(scheduler, () => ran = true);
        try
        {
            cts.Cancel();
            var error = Assert.Throws<TaskSchedulerException>(() => { _ = Schedule(scheduler, () => { }); });
            Assert.IsType<ObjectDisposedException>(error.InnerException);
        }
        finally { release.Set(); }
        await Task.WhenAll(first, second, scheduler.Completion).WaitAsync(Timeout);
        Assert.True(ran);
    }

    [Fact]
    public async Task SchedulerRespectsWorkerConcurrencyAndStaAffinity()
    {
        using var scheduler = new TmTaskScheduler("affinity", 2);
        using var entered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        var workers = new System.Collections.Concurrent.ConcurrentBag<Thread>();
        var tasks = Enumerable.Range(0, 2).Select(_ => Schedule(scheduler, () =>
        {
            workers.Add(Thread.CurrentThread);
            entered.Signal();
            Assert.True(release.Wait(Timeout));
        })).ToArray();
        try
        {
            Assert.True(entered.Wait(Timeout));
            Assert.Equal(2, scheduler.MaximumConcurrencyLevel);
            Assert.Equal(2, workers.Select(t => t.ManagedThreadId).Distinct().Count());
            Assert.All(workers, thread =>
            {
                Assert.StartsWith("affinityThread_", thread.Name);
                if (OperatingSystem.IsWindows()) { Assert.Equal(ApartmentState.STA, thread.GetApartmentState()); }
            });
        }
        finally { release.Set(); }
        await Task.WhenAll(tasks).WaitAsync(Timeout);
    }

    [Fact]
    public async Task SchedulerExposesPendingTasksAndRefusesForeignInlineExecution()
    {
        using var scheduler = new TmTaskScheduler("queued", 1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var first = Schedule(scheduler, () => { entered.Set(); Assert.True(release.Wait(Timeout)); });
        Assert.True(entered.Wait(Timeout));
        var ranOn = 0;
        var pending = Schedule(scheduler, () => ranOn = Environment.CurrentManagedThreadId);
        try
        {
            var snapshot = (IEnumerable<Task>)typeof(TmTaskScheduler).GetMethod("GetScheduledTasks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scheduler, null)!;
            Assert.Same(pending, Assert.Single(snapshot));
            var inlined = (bool)typeof(TmTaskScheduler).GetMethod("TryExecuteTaskInline", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scheduler, [pending, true])!;
            Assert.False(inlined);
            var unqueued = new Task(() => ranOn = Environment.CurrentManagedThreadId);
            var foreignInline = (bool)typeof(TmTaskScheduler).GetMethod("TryExecuteTaskInline", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scheduler, [unqueued, false])!;
            Assert.False(foreignInline);
            Assert.Equal(0, ranOn);
        }
        finally { release.Set(); }
        await Task.WhenAll(first, pending).WaitAsync(Timeout);
        Assert.NotEqual(Environment.CurrentManagedThreadId, ranOn);
    }

    [Fact]
    public async Task SchedulerAllowsWorkerInlineExecutionAndSelfDisposalWithoutDeadlock()
    {
        var scheduler = new TmTaskScheduler("self", 1);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var ranAfterDispose = false;
        var first = Schedule(scheduler, () =>
        {
            entered.Set();
            Assert.True(release.Wait(Timeout));
            var workerId = Environment.CurrentManagedThreadId;
            var nested = new Task<int>(() => Environment.CurrentManagedThreadId);
            nested.RunSynchronously(scheduler);
            Assert.Equal(workerId, nested.Result);
            scheduler.Dispose();
        });
        Assert.True(entered.Wait(Timeout));
        var second = Schedule(scheduler, () => ranAfterDispose = true);
        release.Set();
        await Task.WhenAll(first, second, scheduler.Completion).WaitAsync(Timeout);
        scheduler.Dispose();
        Assert.True(ranAfterDispose);
    }

    [Fact]
    public async Task SchedulerCompletesWhenInitiallyCancelledAndTaskFailuresAreObservable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var cancelled = new TmTaskScheduler("cancelled", 1, cts.Token);
        await cancelled.Completion.WaitAsync(Timeout);
        Assert.Throws<TaskSchedulerException>(() => { _ = Schedule(cancelled, () => { }); });
        using var scheduler = new TmTaskScheduler("errors", 1);
        var expected = new InvalidOperationException("task failure");
        var failed = Schedule(scheduler, () => throw expected);
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => failed.WaitAsync(Timeout)));
        var executed = false;
        await Schedule(scheduler, () => executed = true).WaitAsync(Timeout);
        Assert.True(executed);
    }

    [Fact]
    public void BufferWriterGrowsPreservingOnlyWrittenRegionAndReturnsOldBuffer()
    {
        var pool = new TrackingPool<int>(-10);
        using var writer = new ArrayPoolBufferWriter<int>(4, pool);
        var initial = writer.GetMemory();
        initial.Span[0] = 42;
        initial.Span[1] = 999;
        writer.Advance(1);
        var remaining = writer.GetMemory(5);
        Assert.True(remaining.Length >= 5);
        Assert.Equal(42, Assert.Single(writer.WrittenMemory.ToArray()));
        Assert.Equal(-10, remaining.Span[0]);
        Assert.Equal(1, pool.ReturnCount);
        Assert.Equal(writer.Capacity - 1, writer.FreeCapacity);
        Assert.Equal(1, writer.WrittenCount);
    }

    [Fact]
    public void BufferWriterHandlesCapacityBoundaryClearAndZeroHints()
    {
        var pool = new TrackingPool<int>(0);
        using var writer = new ArrayPoolBufferWriter<int>(4, pool);
        Assert.Equal(4, writer.Capacity);
        Assert.Equal(0, writer.WrittenCount);
        writer.GetSpan().Fill(7);
        writer.Advance(4);
        Assert.Equal(0, writer.FreeCapacity);
        Assert.Equal(new[] { 7, 7, 7, 7 }, writer.WrittenMemory.ToArray());
        Assert.Throws<InvalidOperationException>(() => writer.Advance(1));
        Assert.NotEmpty(writer.GetMemory(0).ToArray());
        Assert.Equal(8, writer.Capacity);
        writer.Advance(0);
        writer.Clear();
        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(writer.Capacity, writer.FreeCapacity);
        Assert.Equal(new[] { 0, 0, 0, 0 }, writer.GetMemory().Span[..4].ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BufferWriterRejectsInvalidInitialCapacity(int capacity) =>
        Assert.Throws<ArgumentException>(() => new ArrayPoolBufferWriter<byte>(capacity));

    [Fact]
    public void BufferWriterRejectsInvalidHintsAdvanceAndOverflowWithoutChangingState()
    {
        using var writer = new ArrayPoolBufferWriter<byte>(4, new TrackingPool<byte>(0));
        Assert.Throws<ArgumentNullException>(() => new ArrayPoolBufferWriter<byte>(1, null!));
        Assert.Throws<ArgumentException>(() => writer.GetMemory(-1));
        Assert.Throws<ArgumentException>(() => writer.GetSpan(-1));
        Assert.Throws<ArgumentException>(() => writer.Advance(-1));
        Assert.Throws<InvalidOperationException>(() => writer.Advance(5));
        writer.Advance(1);
        Assert.Throws<OutOfMemoryException>(() => writer.GetMemory(int.MaxValue));
        Assert.Equal(1, writer.WrittenCount);
        Assert.Equal(4, writer.Capacity);
    }

    [Fact]
    public void BufferWriterDisposeReturnsOnceClearsReferencesAndGuardsEveryMember()
    {
        var pool = new TrackingPool<object>(null!);
        var writer = new ArrayPoolBufferWriter<object>(2, pool);
        writer.GetSpan()[0] = new object();
        writer.Advance(1);
        writer.GetMemory(3);
        Assert.True(pool.LastClearArray);
        Assert.All(pool.Returned[0], item => Assert.Null(item));
        writer.GetSpan()[0] = new object();
        writer.Dispose();
        writer.Dispose();
        Assert.Equal(2, pool.ReturnCount);
        Assert.True(pool.LastClearArray);
        Assert.All(pool.Returned.SelectMany(a => a), item => Assert.Null(item));
        Action[] members = [() => _ = writer.WrittenMemory, () => _ = writer.WrittenSpan.Length,
            () => _ = writer.Buffer, () => _ = writer.WrittenSpanWritable.Length, () => _ = writer.WrittenCount,
            () => _ = writer.Capacity, () => _ = writer.FreeCapacity, writer.Clear, () => writer.Advance(0),
            () => writer.GetMemory(), () => writer.GetSpan()];
        Assert.All(members, member => Assert.Throws<ObjectDisposedException>(member));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(17)]
    public void MemoryRentalHasExactLengthReturnsOnceAndRejectsUseAfterReturn(int length)
    {
        var memory = MemoryPoolUtils.Create(length);
        Assert.Equal(length, memory.Length);
        if (length > 0) { memory.Span[0] = 42; Assert.Equal(42, memory.Span[0]); }
        MemoryPoolUtils.Return((ReadOnlyMemory<byte>)memory);
        Assert.False(memory.TryReturn());
        Assert.Throws<InvalidOperationException>(() => memory.Return());
        Assert.Throws<ObjectDisposedException>(() => _ = memory.Span.Length);
    }

    [Fact]
    public void MemoryRentalRejectsForeignAndSlicedMemoryWithoutReturningWholeRental()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryPoolUtils.Create(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryPoolUtils.Rent<int>(-1));
        Assert.False(default(Memory<byte>).TryReturn());
        Assert.False(default(ReadOnlyMemory<byte>).TryReturn());
        var foreign = new byte[8].AsMemory();
        Assert.False(foreign.TryReturn());
        Assert.Throws<InvalidOperationException>(() => foreign.Return());
        var raw = ArrayPool<byte>.Shared.Rent(8);
        try { Assert.False(raw.AsMemory(0, 8).TryReturn()); }
        finally { ArrayPool<byte>.Shared.Return(raw); }
        var memory = MemoryPoolUtils.Create(8);
        try
        {
            Assert.False(memory[1..].TryReturn());
            Assert.False(memory[..7].TryReturn());
            Assert.Throws<InvalidOperationException>(() => memory[..4].Return());
            memory.Span[0] = 55;
            Assert.Equal(55, memory.Span[0]);
        }
        finally { memory.Return(); }
    }

    [Fact]
    public void MemoryOwnerDisposalAndReturnShareOwnershipAndStaleRentalCannotReturnNewLease()
    {
        var owner = MemoryPoolUtils.Rent<string>(3);
        var original = owner.Memory;
        original.Span[0] = "value";
        Assert.True(original.TryReturn());
        owner.Dispose();
        owner.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = owner.Memory);
        using var next = MemoryPoolUtils.Rent<string>(3);
        next.Memory.Span[0] = "next";
        Assert.False(original.TryReturn());
        Assert.Equal("next", next.Memory.Span[0]);
        next.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = next.Memory);
    }

    [Fact]
    public async Task MemoryConcurrentReturnsHaveExactlyOneWinner()
    {
        var memory = MemoryPoolUtils.Create(16);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => memory.TryReturn())));
        Assert.Equal(1, results.Count(result => result));
        Assert.Throws<ObjectDisposedException>(() => _ = memory.Span.Length);
    }

    [Fact]
    public void MemoryRentalSupportsPinningDuringItsLifetime()
    {
        var memory = MemoryPoolUtils.Create(8);
        try { using var handle = memory.Pin(); memory.Span[0] = 2; Assert.Equal(2, memory.Span[0]); }
        finally { memory.Return(); }
    }

    private sealed class TrackingPool<T>(T initialValue) : ArrayPool<T>
    {
        public int ReturnCount { get; private set; }
        public bool LastClearArray { get; private set; }
        public List<T[]> Returned { get; } = [];
        public override T[] Rent(int minimumLength)
        {
            var buffer = new T[minimumLength];
            Array.Fill(buffer, initialValue);
            return buffer;
        }
        public override void Return(T[] array, bool clearArray = false)
        {
            ReturnCount++;
            LastClearArray = clearArray;
            if (clearArray) { array.AsSpan().Clear(); }
            Returned.Add(array);
        }
    }
}
