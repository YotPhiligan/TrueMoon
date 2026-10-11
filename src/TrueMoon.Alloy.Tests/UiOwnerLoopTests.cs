using System.Runtime.CompilerServices;
using TrueMoon.Alloy.Hosting;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class UiOwnerLoopTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Bounded(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    [Fact]
    public async Task SiblingContinuesAcrossCloseAndRecreationOnSameOwner()
    {
        var loop = new UiOwnerLoop();
        var threads = new List<int>();
        var siblingReady = Signal(); var siblingDone = Signal(); var siblingContinued = Signal();
        var closeSibling = 0; var firstClosed = 0;
        try
        {
            loop.Schedule(() => threads.Add(Environment.CurrentManagedThreadId), () =>
            {
                if (Volatile.Read(ref firstClosed) != 0) siblingContinued.TrySetResult();
                return false;
            }, () => threads.Add(Environment.CurrentManagedThreadId), () => Volatile.Read(ref closeSibling) != 0, siblingReady, siblingDone);
            await Bounded(siblingReady.Task);
            var firstReady = Signal(); var firstDone = Signal();
            loop.Schedule(() => threads.Add(Environment.CurrentManagedThreadId), () =>
            {
                threads.Add(Environment.CurrentManagedThreadId); return true;
            }, () => { threads.Add(Environment.CurrentManagedThreadId); Volatile.Write(ref firstClosed, 1); }, () => false, firstReady, firstDone);
            await Bounded(firstDone.Task);
            await Bounded(siblingContinued.Task);
            Assert.False(siblingDone.Task.IsCompleted);
            var replacementReady = Signal(); var replacementDone = Signal();
            loop.Schedule(() => threads.Add(Environment.CurrentManagedThreadId), () => true,
                () => threads.Add(Environment.CurrentManagedThreadId), () => false, replacementReady, replacementDone);
            await Bounded(replacementDone.Task);
            Volatile.Write(ref closeSibling, 1); loop.Wake();
            await Bounded(siblingDone.Task);
            Assert.Equal(7, threads.Count);
            Assert.Single(threads.Distinct());
            Assert.NotEqual(Environment.CurrentManagedThreadId, threads[0]);
        }
        finally { await Bounded(loop.StopAsync()); }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task ParticipantFaultPreservesCauseAndCleanupWithoutStoppingSibling(bool startup, bool cleanupFails)
    {
        var loop = new UiOwnerLoop();
        var first = new ArgumentException("first cause"); var cleanup = new InvalidOperationException("cleanup cause");
        var ready = Signal(); var done = Signal(); var cleanupCalls = 0;
        var siblingReady = Signal(); var siblingDone = Signal();
        try
        {
            loop.Schedule(() => { if (startup) throw first; }, () => throw first,
                () => { cleanupCalls++; if (cleanupFails) throw cleanup; }, () => false, ready, done);
            var error = await Record.ExceptionAsync(() => Bounded(done.Task));
            if (cleanupFails)
            {
                var errors = Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;
                Assert.Collection(errors, cause => Assert.Same(first, cause), cause => Assert.Same(cleanup, cause));
            }
            else Assert.Same(first, error);
            if (startup) Assert.Same(error, await Record.ExceptionAsync(() => ready.Task));
            else await Bounded(ready.Task);
            Assert.Equal(1, cleanupCalls);
            loop.Schedule(() => { }, () => true, () => { }, () => false, siblingReady, siblingDone);
            await Bounded(siblingDone.Task); await Bounded(siblingReady.Task);
        }
        finally { await Bounded(loop.StopAsync()); }
    }

    [Fact]
    public async Task CloseBeforeStartupSkipsAllocationAndCancelsReadiness()
    {
        var loop = new UiOwnerLoop(); var ready = Signal(); var done = Signal();
        var initialized = 0; var ticks = 0; var cleaned = 0;
        try
        {
            loop.Schedule(() => initialized++, () => { ticks++; return false; }, () => cleaned++, () => true, ready, done);
            await Bounded(done.Task);
            Assert.True(ready.Task.IsCanceled);
            Assert.Equal(0, initialized); Assert.Equal(0, ticks); Assert.Equal(1, cleaned);
        }
        finally { await Bounded(loop.StopAsync()); }
    }

    [Fact]
    public async Task StopDrainsAcceptedWorkAcrossStartupBatchesAndRejectsNewWork()
    {
        var loop = new UiOwnerLoop(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var tasks = new List<Task>(); var cleanups = 0; var allocations = 0;
        var firstReady = Signal(); var firstDone = Signal(); tasks.Add(firstDone.Task);
        try
        {
            loop.Schedule(() => { entered.Set(); Assert.True(release.Wait(TimeSpan.FromSeconds(10))); }, () => false,
                () => cleanups++, () => false, firstReady, firstDone);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            for (var i = 0; i < 80; i++)
            {
                var ready = Signal(); var done = Signal(); tasks.Add(done.Task);
                loop.Schedule(() => allocations++, () => false, () => cleanups++, () => false, ready, done);
            }
            var stop = loop.StopAsync();
            Assert.Throws<ObjectDisposedException>(() => loop.Schedule(() => { }, () => true, () => { }, () => false, Signal(), Signal()));
            release.Set();
            await Bounded(stop); await Bounded(Task.WhenAll(tasks));
            Assert.Equal(81, cleanups); Assert.Equal(0, allocations);
            Assert.Same(stop, loop.StopAsync());
        }
        finally { release.Set(); await Bounded(loop.StopAsync()); }
    }

    [Fact]
    public async Task IdleLoopDoesNotRetainClosedCallbackTargets()
    {
        var loop = new UiOwnerLoop();
        try
        {
            var (reference, completion) = ScheduleTarget(loop);
            await Bounded(completion);
            // Keep this as the last participant: replacing the queue item could
            // hide a target retained by the permanently idle owner frame.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                await Task.Delay(20);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                if (!reference.IsAlive) break;
            }
            Assert.False(reference.IsAlive);
        }
        finally { await Bounded(loop.StopAsync()); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference, Task) ScheduleTarget(UiOwnerLoop loop)
    {
        var target = new Target(); var ready = Signal(); var done = Signal();
        loop.Schedule(target.Initialize, target.Tick, target.Cleanup, target.Closing, ready, done);
        return (new WeakReference(target), done.Task);
    }
    private sealed class Target
    {
        public void Initialize() { }
        public bool Tick() => true;
        public void Cleanup() { }
        public bool Closing() => false;
    }
}
