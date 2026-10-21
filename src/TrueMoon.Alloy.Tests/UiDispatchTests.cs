using TrueMoon.Alloy.Runtime;
using System.Collections.Concurrent;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class UiDispatchTests
{
    private static UiSession Create(int capacity = 4, int budget = 2, Surface? surface = null, Element? root = null) =>
        new(root ?? new Panel(), surface ?? new Surface(), new UiViewport(240, 140), null,
            new UiDispatchOptions { Capacity = capacity, ActionsPerUpdate = budget });

    [Fact]
    public void SelfPosting_LeavesTimeForRenderingAndAnotherSession()
    {
        var firstSurface = new Surface(); var secondSurface = new Surface();
        using var first = Create(surface: firstSurface); using var second = Create(surface: secondSurface);
        var count = 0;
        void Repeat() { count++; first.Post(Repeat); }
        first.Post(Repeat);
        second.Post(() => second.Root.Width = 75);
        Assert.True(first.Update()); Assert.Equal(2, count); Assert.True(first.NeedsUpdate);
        Assert.True(second.Update()); Assert.Equal(75, second.Root.Bounds.Width);
        Assert.Equal(1, firstSurface.Frames); Assert.Equal(1, secondSurface.Frames);
        Assert.False(first.Update()); Assert.Equal(4, count); Assert.True(first.NeedsUpdate);
    }

    [Fact]
    public void Capacity_RejectsOverflowAndRetainsFifoAcrossUpdates()
    {
        using var ui = Create(capacity: 3); var order = new List<int>();
        for (var i = 0; i < 3; i++) { var value = i; ui.Post(() => order.Add(value)); }
        Assert.Contains("capacity (3)", Assert.Throws<InvalidOperationException>(() => ui.Post(() => order.Add(99))).Message);
        ui.Update(); Assert.Equal(new[] { 0, 1 }, order); Assert.True(ui.NeedsUpdate);
        ui.Post(() => order.Add(3)); ui.Update();
        Assert.Equal(new[] { 0, 1, 2, 3 }, order); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void DefaultBudget_BoundsTheOriginalTwentyThousandActionReproduction()
    {
        using var ui = new UiSession(new Panel(), new Surface(), new UiViewport(10, 10));
        var count = 0;
        void Repeat() { if (++count < 20000) ui.Post(Repeat); }
        ui.Post(Repeat); ui.Update();
        Assert.Equal(64, count); Assert.Equal(1024, ui.DispatchOptions.Capacity); Assert.True(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void InvalidLimits_DoNotTakeRootOrSurfaceOwnership(int capacity, int budget)
    {
        var root = new Panel(); var surface = new Surface();
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(capacity, budget, surface, root));
        Assert.False(root.IsAttached); Assert.False(root.IsDisposed); Assert.Equal(0, surface.Disposals);
        root.Dispose(); surface.Dispose();
    }

    [Fact]
    public void WorkerDispatch_CompletesOnOwnerAndReportsCallbackFailureWithoutSkippingNextCommand()
    {
        using var ui = Create(); var owner = Environment.CurrentManagedThreadId; var executed = new List<int>();
        var error = new Exception("callback");
        var failed = Worker(() => ui.InvokeAsync(() => { executed.Add(Environment.CurrentManagedThreadId); throw error; }));
        var succeeded = Worker(() => ui.InvokeAsync(() => executed.Add(Environment.CurrentManagedThreadId)));
        Assert.False(failed.IsCompleted); Assert.False(succeeded.IsCompleted);
        Assert.True(ui.Update());
        Assert.Same(error, Assert.Throws<Exception>(() => failed.GetAwaiter().GetResult()));
        Assert.True(succeeded.IsCompletedSuccessfully); Assert.Equal(new[] { owner, owner }, executed);
        Assert.False(ui.IsFaulted); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void OwnerDispatch_RunsImmediatelyEvenWithFullQueueAndReturnsErrorsAsTasks()
    {
        using var ui = Create(capacity: 1); var order = new List<int>();
        ui.Post(() => order.Add(2));
        var task = ui.InvokeAsync(() => order.Add(1));
        Assert.True(task.IsCompletedSuccessfully); Assert.Equal(new[] { 1 }, order);
        var error = new Exception("inline"); var failed = ui.InvokeAsync(() => throw error);
        Assert.Same(error, Assert.Throws<Exception>(() => failed.GetAwaiter().GetResult()));
        ui.Update(); Assert.Equal(new[] { 1, 2 }, order);
    }

    [Fact]
    public void Cancellation_RemovesPendingCommandImmediatelyAndReleasesCapacity()
    {
        using var ui = Create(capacity: 1); using var cancellation = new CancellationTokenSource(); var ran = false;
        var task = Worker(() => ui.InvokeAsync(() => ran = true, cancellation.Token));
        cancellation.Cancel(); Assert.True(task.IsCanceled);
        Assert.Equal(cancellation.Token, Assert.ThrowsAny<OperationCanceledException>(() => task.GetAwaiter().GetResult()).CancellationToken);
        ui.Post(() => ui.Root.Width = 80); Assert.True(ui.Update());
        Assert.False(ran); Assert.Equal(80, ui.Root.Bounds.Width); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void PreCanceledDispatch_DoesNotUseCapacityEvenWhenFull()
    {
        using var ui = Create(capacity: 1); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var ran = false; ui.Post(() => ui.Root.Width = 50);
        var task = Worker(() => ui.InvokeAsync(() => ran = true, cancellation.Token));
        Assert.True(task.IsCanceled);
        Assert.True(ui.InvokeAsync(() => ran = true, cancellation.Token).IsCanceled);
        ui.Update(); Assert.False(ran); Assert.Equal(50, ui.Root.Bounds.Width);
    }

    [Fact]
    public void Cancellation_AfterCallbackStartsDoesNotChangeItsResult()
    {
        using var ui = Create(); using var cancellation = new CancellationTokenSource(); var ran = false;
        var task = Worker(() => ui.InvokeAsync(() => { cancellation.Cancel(); ran = true; }, cancellation.Token));
        ui.Update(); Assert.True(task.IsCompletedSuccessfully); Assert.True(ran);
    }

    [Fact]
    public void Cancellation_DoesNotConsumeUpdateBudgetOrReorderRemainingCommands()
    {
        using var ui = Create(capacity: 3, budget: 1); using var cancellation = new CancellationTokenSource();
        var order = new List<int>(); ui.Post(() => order.Add(1));
        var canceled = Worker(() => ui.InvokeAsync(() => order.Add(99), cancellation.Token));
        ui.Post(() => order.Add(2)); cancellation.Cancel();
        ui.Update(); Assert.Equal(new[] { 1 }, order); Assert.True(canceled.IsCanceled);
        ui.Update(); Assert.Equal(new[] { 1, 2 }, order); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void NestedOwnerDispatch_DoesNotWaitForAnotherUpdate()
    {
        using var ui = Create(budget: 1); var order = new List<int>();
        ui.Post(() =>
        {
            var nested = ui.InvokeAsync(() => order.Add(1));
            Assert.True(nested.IsCompletedSuccessfully); order.Add(2);
        });
        ui.Post(() => order.Add(3)); ui.Update(); Assert.Equal(new[] { 1, 2 }, order);
        ui.Update(); Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void AwaitableOverflow_IsObservableAndDoesNotReplaceAcceptedWork()
    {
        using var ui = Create(capacity: 1); var ran = false; ui.Post(() => ui.Root.Width = 40);
        var task = Worker(() => ui.InvokeAsync(() => ran = true));
        Assert.Contains("capacity", Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult()).Message);
        ui.Update(); Assert.False(ran); Assert.Equal(40, ui.Root.Bounds.Width);
    }

    [Fact]
    public void PostError_PropagatesButRemainingCommandsCanBeRetried()
    {
        using var ui = Create(); var error = new Exception("post"); var ran = false;
        ui.Post(() => throw error); ui.Post(() => ran = true);
        Assert.Same(error, Assert.Throws<Exception>(() => ui.Update())); Assert.False(ran); Assert.True(ui.NeedsUpdate);
        Assert.True(ui.Update()); Assert.True(ran); Assert.False(ui.IsFaulted);
    }

    [Fact]
    public void Disposal_ClosesQueueBeforeOwnedCleanupAndSettlesPendingTasks()
    {
        var root = new Panel(); var surface = new Surface(); using var ui = Create(root: root, surface: surface);
        var ran = false; var pending = Worker(() => ui.InvokeAsync(() => ran = true));
        root.Own(new Cleanup(() =>
        {
            Assert.True(pending.IsFaulted);
            Assert.Throws<ObjectDisposedException>(() => ui.Post(() => ran = true));
        }));
        ui.Dispose(); Assert.False(ran); Assert.True(root.IsDisposed); Assert.Equal(1, surface.Disposals);
        Assert.Throws<ObjectDisposedException>(() => pending.GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => Worker(() => ui.InvokeAsync(() => ran = true)).GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => ui.InvokeAsync(() => ran = true).GetAwaiter().GetResult());
    }

    [Fact]
    public void FailedDisposalGuard_DoesNotCloseDispatch()
    {
        var surface = new Surface(); using var ui = Create(surface: surface); var ran = false;
        var task = Worker(() => ui.InvokeAsync(() => ran = true));
        surface.Borrowed = true; Assert.Throws<InvalidOperationException>(ui.Dispose); Assert.False(task.IsCompleted);
        surface.Borrowed = false; ui.Update(); Assert.True(ran); Assert.True(task.IsCompletedSuccessfully);
        ui.Post(() => Assert.Throws<InvalidOperationException>(ui.Dispose));
        ui.Post(() => ui.Root.Width = 30); ui.Update(); Assert.Equal(30, ui.Root.Bounds.Width);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalCallbackFailure_FaultsSessionAndAllPendingTasks(bool inline)
    {
        using var ui = Create(); var failure = new UiRenderingException("fake", "dispatch", UiRenderingFailureKind.DeviceLost, new Exception("device"));
        var ran = false;
        var failing = inline ? null : Worker(() => ui.InvokeAsync(() => throw failure));
        var pending = Worker(() => ui.InvokeAsync(() => ran = true));
        if (inline) failing = ui.InvokeAsync(() => throw failure);
        else Assert.Same(failure, Assert.Throws<UiRenderingException>(() => ui.Update()));
        Assert.Same(failure, Assert.Throws<UiRenderingException>(() => failing!.GetAwaiter().GetResult()));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult()).InnerException);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => ui.Post(() => ran = true)).InnerException);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Worker(() => ui.InvokeAsync(() => ran = true)).GetAwaiter().GetResult()).InnerException);
        Assert.False(ran); Assert.True(ui.IsFaulted);
    }

    [Fact]
    public void ConcurrentProducers_RespectCapacityWithoutLosingAcceptedCommands()
    {
        using var ui = Create(capacity: 32, budget: 7);
        var accepted = new ConcurrentBag<int>(); var executed = new List<int>(); var rejected = 0;
        Parallel.For(0, 400, i =>
        {
            try { ui.Post(() => executed.Add(i)); accepted.Add(i); }
            catch (InvalidOperationException) { Interlocked.Increment(ref rejected); }
        });
        Assert.Equal(32, accepted.Count); Assert.Equal(368, rejected);
        for (var i = 0; i < 5; i++) ui.Update();
        Assert.Equal(accepted.Order(), executed.Order()); Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcurrentClose_SettlesEveryDispatchWithoutExecutingIt(bool fault)
    {
        using var ui = Create(capacity: 1024); using var start = new ManualResetEventSlim();
        var tasks = new ConcurrentBag<Task>(); var ran = 0;
        var thread = new Thread(() =>
        {
            start.Wait();
            for (var i = 0; i < 1000; i++) tasks.Add(ui.InvokeAsync(() => Interlocked.Increment(ref ran)));
        }) { IsBackground = true };
        thread.Start(); start.Set();
        var error = new UiRenderingException("fake", "close", UiRenderingFailureKind.ContextLost, new Exception());
        if (fault) ui.ReportRenderingFailure(error); else ui.Dispose();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        Assert.Equal(1000, tasks.Count); Assert.Equal(0, ran);
        foreach (var task in tasks)
        {
            Assert.True(task.IsFaulted);
            if (fault) Assert.Same(error, Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult()).InnerException);
            else Assert.Throws<ObjectDisposedException>(() => task.GetAwaiter().GetResult());
        }
    }

    private static T Worker<T>(Func<T> action)
    {
        T result = default!; Exception? error = null;
        var thread = new Thread(() => { try { result = action(); } catch (Exception caught) { error = caught; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.Null(error); return result;
    }

    private sealed class Cleanup(Action release) : IDisposable { public void Dispose() => release(); }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public bool Borrowed { get; set; }
        public int Frames { get; private set; }
        public int Disposals { get; private set; }
        public void VerifyAvailable() { if (Borrowed) throw new InvalidOperationException("borrowed"); }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => Frames++;
        public void Dispose() => Disposals++;
    }
}
