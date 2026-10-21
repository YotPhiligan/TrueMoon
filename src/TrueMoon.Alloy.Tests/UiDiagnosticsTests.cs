using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class UiDiagnosticsTests
{
    private static UiSession Create(Element? root = null, Surface? surface = null, int capacity = 4, int budget = 4) =>
        new(root ?? new Panel(), surface ?? new Surface(), new UiViewport(100, 100), null,
            new UiDispatchOptions { Capacity = capacity, ActionsPerUpdate = budget });

    [Fact]
    public void OptIn_PreservesIdentityAndStartsFromCurrentBacklogWithoutInventingHistory()
    {
        using var ui = Create(); using var other = Create();
        ui.Update(); ui.Post(() => ui.Root.Width = 40);
        Assert.Null(ui.Diagnostics); Assert.NotEqual(ui.Id, other.Id);
        var diagnostics = ui.EnableDiagnostics(51);
        Assert.Same(diagnostics, ui.EnableDiagnostics());
        var before = diagnostics.Snapshot;
        Assert.Equal(ui.Id, before.SessionId); Assert.Equal(51, before.WindowId);
        Assert.Equal(0, before.Update.Attempts); Assert.Equal(1, before.Queue.InitialPending);
        Assert.Equal(1, before.Queue.Pending); Assert.Equal(1, before.Queue.HighWater); Assert.Equal(0, before.Queue.Accepted);
        ui.Update(); var after = diagnostics.Snapshot;
        Assert.Equal(1, after.Queue.Started); Assert.Equal(1, after.Queue.Completed); Assert.Equal(0, after.Queue.Pending);
        Assert.Equal(1, before.Queue.Pending); // A previous value snapshot never changes.
    }

    [Fact]
    public void Phases_SeparateLayoutRenderCleanUpdateAndHostPresentation()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics();
        Assert.True(ui.Update()); Assert.False(ui.Update());
        ui.Root.Background = new Color(1, 0, 0); Assert.True(ui.Update());
        var snapshot = diagnostics.Snapshot;
        Assert.Equal(3, snapshot.Update.Completed); Assert.Equal(1, snapshot.Measure.Completed);
        Assert.Equal(1, snapshot.Arrange.Completed); Assert.Equal(2, snapshot.Render.Completed);
        Assert.Equal(0, snapshot.Present.Attempts);
        Assert.Equal(UiSessionState.Active, snapshot.State);
        foreach (var phase in new[] { snapshot.Update, snapshot.Measure, snapshot.Arrange, snapshot.Render })
        {
            Assert.Equal(phase.Attempts, phase.Completed); Assert.Equal(0, phase.Failed);
            Assert.True(phase.Total >= phase.Maximum); Assert.True(phase.Maximum >= phase.Last); Assert.True(phase.Last >= TimeSpan.Zero);
        }
    }

    [Fact]
    public void Suspended_StillDrainsCommandsButDoesNotClaimLayoutRenderOrPresentation()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics();
        ui.Resize(new UiViewport(0, 100)); ui.Post(() => ui.Root.Width = 60);
        Assert.False(ui.Update()); var suspended = diagnostics.Snapshot;
        Assert.Equal(UiSessionState.Suspended, suspended.State); Assert.Equal(new UiViewport(0, 100), suspended.Viewport);
        Assert.Equal(1, suspended.Queue.Completed); Assert.Equal(1, suspended.Update.Completed);
        Assert.Equal(0, suspended.Measure.Attempts); Assert.Equal(0, suspended.Render.Attempts); Assert.Equal(0, suspended.Present.Attempts);
        ui.Resize(new UiViewport(200, 150, 2)); Assert.True(ui.Update());
        Assert.Equal(UiSessionState.Active, diagnostics.Snapshot.State); Assert.Equal(60, ui.Root.Bounds.Width);
        Assert.Equal(new UiViewport(200, 150, 2), diagnostics.Snapshot.Viewport);
    }

    [Fact]
    public void Queue_AccountsForCapacityCancellationCallbackErrorsAndCloseWithoutChangingSemantics()
    {
        using var ui = Create(capacity: 4, budget: 2); var diagnostics = ui.EnableDiagnostics();
        using var cancellation = new CancellationTokenSource(); var ran = 0; var error = new Exception("callback");
        ui.Post(() => ran++);
        var canceled = Worker(() => ui.InvokeAsync(() => ran += 100, cancellation.Token));
        var failed = Worker(() => ui.InvokeAsync(() => throw error));
        ui.Post(() => ran += 10);
        Assert.Throws<InvalidOperationException>(() => ui.Post(() => ran += 1000));
        cancellation.Cancel(); ui.Update();
        Assert.True(canceled.IsCanceled); Assert.Same(error, Assert.Throws<Exception>(() => failed.GetAwaiter().GetResult()));
        var queue = diagnostics.Snapshot.Queue;
        Assert.Equal(4, queue.Accepted); Assert.Equal(4, queue.HighWater); Assert.Equal(1, queue.Pending);
        Assert.Equal(2, queue.Started); Assert.Equal(1, queue.Completed); Assert.Equal(1, queue.Failed);
        Assert.Equal(1, queue.Canceled); Assert.Equal(1, queue.Rejected); Assert.Equal(1, ran);
        Assert.Equal(1, diagnostics.Snapshot.Update.Completed); // Awaitable callback failure did not fail Update.
        ui.Dispose(); Assert.Throws<ObjectDisposedException>(() => ui.Post(() => ran++));
        queue = diagnostics.Snapshot.Queue;
        Assert.Equal(0, queue.Pending); Assert.Equal(1, queue.Discarded); Assert.Equal(2, queue.Rejected);
        Assert.Equal(UiSessionState.Disposed, diagnostics.Snapshot.State); Assert.Equal(1, ran);
    }

    [Fact]
    public void PostFailure_IsCountedAndRethrownWhileRemainingWorkCanBeRetried()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics(); var error = new Exception("post"); var ran = false;
        ui.Post(() => throw error); ui.Post(() => ran = true);
        Assert.Same(error, Assert.Throws<Exception>(() => ui.Update()));
        var snapshot = diagnostics.Snapshot;
        Assert.Equal(1, snapshot.Queue.Failed); Assert.Equal(1, snapshot.Queue.Pending);
        Assert.Equal(1, snapshot.Update.Failed); Assert.Equal(0, snapshot.Render.Attempts);
        ui.Update(); Assert.True(ran); Assert.Equal(1, diagnostics.Snapshot.Update.Completed);
    }

    [Fact]
    public void EnableInsideCallback_DoesNotCountTheAlreadyRunningCallbackOrPartialUpdate()
    {
        using var ui = Create(); ui.Post(() => ui.EnableDiagnostics()); ui.Update();
        var snapshot = ui.Diagnostics!.Snapshot;
        Assert.Equal(0, snapshot.Queue.Started); Assert.Equal(0, snapshot.Queue.Completed);
        Assert.Equal(0, snapshot.Update.Attempts); Assert.Equal(0, snapshot.Render.Attempts);
        ui.Post(() => ui.Root.Width = 20); ui.Update();
        Assert.Equal(1, ui.Diagnostics.Snapshot.Queue.Completed); Assert.Equal(1, ui.Diagnostics.Snapshot.Render.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayoutFailure_ReportsTheFailingPhaseAndPreservesRetry(bool arrange)
    {
        var root = new FailingElement { FailMeasure = !arrange, FailArrange = arrange };
        using var ui = Create(root); var diagnostics = ui.EnableDiagnostics();
        Assert.Same(root.Error, Assert.Throws<Exception>(() => ui.Update()));
        var snapshot = diagnostics.Snapshot;
        Assert.Equal(1, snapshot.Update.Failed); Assert.Equal(arrange ? 0 : 1, snapshot.Measure.Failed);
        Assert.Equal(arrange ? 1 : 0, snapshot.Arrange.Failed); Assert.Equal(0, snapshot.Render.Attempts);
        Assert.Equal(UiSessionState.Active, snapshot.State); Assert.True(ui.NeedsUpdate);
        root.FailMeasure = root.FailArrange = false; Assert.True(ui.Update());
        Assert.Equal(1, diagnostics.Snapshot.Render.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawFailure_DistinguishesRetryableErrorFromTerminalGraphicsFailure(bool terminal)
    {
        var surface = new Surface(); using var ui = Create(surface: surface); var diagnostics = ui.EnableDiagnostics();
        surface.Error = terminal ? new UiRenderingException("fake", "Render", UiRenderingFailureKind.ContextLost, new Exception()) : new Exception("draw");
        Assert.Same(surface.Error, Record.Exception(() => ui.Update()));
        var snapshot = diagnostics.Snapshot;
        Assert.Equal(1, snapshot.Render.Failed); Assert.Equal(1, snapshot.Update.Failed);
        Assert.Equal(terminal ? UiSessionState.Faulted : UiSessionState.Active, snapshot.State);
        Assert.Equal(terminal ? UiRenderingFailureKind.ContextLost : (UiRenderingFailureKind?)null, snapshot.RenderingFailure);
        if (!terminal) { surface.Error = null; Assert.True(ui.Update()); Assert.Equal(1, diagnostics.Snapshot.Render.Completed); }
        ui.Dispose(); Assert.Equal(UiSessionState.Disposed, diagnostics.Snapshot.State);
        Assert.Equal(snapshot.RenderingFailure, diagnostics.Snapshot.RenderingFailure);
    }

    [Fact]
    public void Disposal_GuardFailureDoesNotPretendCleanupStartedAndObserversSeeDisposingThenDisposed()
    {
        var root = new Panel(); var surface = new Surface(); using var ui = Create(root, surface); var diagnostics = ui.EnableDiagnostics();
        surface.Borrowed = true; Assert.Throws<InvalidOperationException>(ui.Dispose);
        Assert.Equal(UiSessionState.Active, diagnostics.Snapshot.State);
        root.Own(new Cleanup(() => Assert.Equal(UiSessionState.Disposing, diagnostics.Snapshot.State)));
        ui.Disposed += _ => Assert.Equal(UiSessionState.Disposed, diagnostics.Snapshot.State);
        surface.Borrowed = false; ui.Dispose(); Assert.Equal(UiSessionState.Disposed, diagnostics.Snapshot.State);
    }

    [Fact]
    public void Presentation_UsesExactHostReportedDurationsAndValidatesInputWithoutChangingCounters()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics();
        diagnostics.RecordPresentation(TimeSpan.FromMilliseconds(2), UiPresentationOutcome.Presented);
        diagnostics.RecordPresentation(TimeSpan.FromMilliseconds(3), UiPresentationOutcome.Skipped);
        diagnostics.RecordPresentation(TimeSpan.FromMilliseconds(1), UiPresentationOutcome.Failed);
        Assert.Equal(new UiPhaseSnapshot(3, 1, 1, 1, TimeSpan.FromMilliseconds(6), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(3)), diagnostics.Snapshot.Present);
        var snapshot = diagnostics.Snapshot;
        Assert.Throws<ArgumentOutOfRangeException>(() => diagnostics.RecordPresentation(TimeSpan.FromTicks(-1), UiPresentationOutcome.Presented));
        Assert.Throws<ArgumentOutOfRangeException>(() => diagnostics.RecordPresentation(TimeSpan.Zero, (UiPresentationOutcome)9));
        Assert.Equal(snapshot, diagnostics.Snapshot); Assert.Equal(0, snapshot.Update.Attempts);
    }

    [Fact]
    public void ConcurrentSnapshots_PreserveQueueAccountingWhileTheOwnerDrainsWork()
    {
        using var ui = Create(capacity: 1024); var diagnostics = ui.EnableDiagnostics();
        using var start = new ManualResetEventSlim(); Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                start.Wait();
                for (var i = 0; i < 500; i++)
                {
                    ui.Post(() => { }); var snapshot = diagnostics.Snapshot;
                    Assert.Equal(ui.Id, snapshot.SessionId);
                    var queue = snapshot.Queue;
                    Assert.Equal(queue.InitialPending + queue.Accepted, queue.Pending + queue.Started + queue.Canceled + queue.Discarded);
                    Assert.True(queue.Completed + queue.Failed <= queue.Started);
                    Assert.InRange(queue.Pending, 0, queue.HighWater);
                }
            }
            catch (Exception caught) { error = caught; }
        }) { IsBackground = true };
        thread.Start(); start.Set();
        for (var i = 0; i < 200; i++) ui.Update();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.Null(error);
        while (ui.NeedsUpdate) ui.Update();
        Assert.Equal(500, diagnostics.Snapshot.Queue.Completed); Assert.Equal(0, diagnostics.Snapshot.Queue.Pending);
    }

    [Fact]
    public void Enable_RespectsOwnerLifetimeAndRejectsInvalidWindowIdentityWithoutPartialObservation()
    {
        using var ui = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => ui.EnableDiagnostics(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ui.EnableDiagnostics(-1));
        Assert.IsType<InvalidOperationException>(Worker(() => Record.Exception(() => ui.EnableDiagnostics())));
        Assert.Null(ui.Diagnostics);
        var diagnostics = ui.EnableDiagnostics(); ui.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ui.EnableDiagnostics());
        Assert.Equal(UiSessionState.Disposed, Worker(() => diagnostics.Snapshot).State);
    }

    [Fact]
    public void ExternalFault_DiscardsPendingWorkBeforeNotificationAndRetainsClassificationAfterDisposal()
    {
        using var ui = Create(); var diagnostics = ui.EnableDiagnostics(); var ran = false;
        ui.Post(() => ran = true);
        var failure = new UiRenderingException("fake", "present", UiRenderingFailureKind.DeviceLost, new Exception());
        ui.RenderingFailed += _ =>
        {
            Assert.Equal(UiSessionState.Faulted, diagnostics.Snapshot.State);
            Assert.Equal(1, diagnostics.Snapshot.Queue.Discarded); Assert.Equal(0, diagnostics.Snapshot.Queue.Pending);
        };
        ui.ReportRenderingFailure(failure); ui.Dispose();
        Assert.Equal(UiRenderingFailureKind.DeviceLost, diagnostics.Snapshot.RenderingFailure);
        Assert.Equal(UiSessionState.Disposed, diagnostics.Snapshot.State); Assert.False(ran);
    }

    [Fact]
    public void LateEnable_AfterTerminalFailureReportsStateWithoutClaimingEarlierPhaseSamples()
    {
        using var ui = Create();
        ui.ReportRenderingFailure(new UiRenderingException("fake", "present", UiRenderingFailureKind.BackendFailure, new Exception()));
        var diagnostics = ui.EnableDiagnostics();
        Assert.Equal(UiSessionState.Faulted, diagnostics.Snapshot.State);
        Assert.Equal(UiRenderingFailureKind.BackendFailure, diagnostics.Snapshot.RenderingFailure);
        Assert.Equal(0, diagnostics.Snapshot.Update.Attempts); Assert.Equal(0, diagnostics.Snapshot.Present.Attempts);
    }

    private static T Worker<T>(Func<T> action)
    {
        T result = default!; Exception? error = null;
        var thread = new Thread(() => { try { result = action(); } catch (Exception caught) { error = caught; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.Null(error); return result;
    }

    private sealed class Cleanup(Action action) : IDisposable { public void Dispose() => action(); }
    private sealed class FailingElement : Element
    {
        public bool FailMeasure, FailArrange;
        public Exception Error { get; } = new("layout");
        protected override Size MeasureCore(Size available, ITextLayoutService text) => FailMeasure ? throw Error : new Size(20, 20);
        protected override void ArrangeCore(Rect contentBounds) { if (FailArrange) throw Error; }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public bool Borrowed;
        public Exception? Error;
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { if (Borrowed) throw new InvalidOperationException("borrowed"); }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { if (Error != null) throw Error; }
        public void Dispose() { }
    }
}
