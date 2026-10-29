using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class UiFramePumpTests
{
    [Theory]
    [InlineData(UiFrameMode.OnDemand)]
    [InlineData(UiFrameMode.Continuous)]
    public void SelectionReveal_DefersOutputUntilLayoutSettlesAndKeepsFrameCadence(UiFrameMode mode)
    {
        var tree = new TreeView(); tree.SetItems(Enumerable.Range(1, 100).Select(index => new TreeItem(index.ToString(), null, "Row " + index)));
        var scroll = new ScrollViewer(); scroll.SetContent(tree); var clock = new Clock();
        using var ui = new UiSession(scroll, new Surface(), new(200, 100)); var diagnostics = ui.EnableDiagnostics();
        using var pump = new UiFramePump(mode, 100, () => { }, clock); pump.Attach(ui); var outputs = 0;
        bool Output() { Assert.False(ui.NeedsRender); outputs++; return true; }
        Assert.True(pump.Tick(false, Output)); Assert.Equal(1, outputs);
        tree.Select("100"); clock.Advance(10);
        Assert.False(pump.Tick(false, Output)); Assert.True(pump.UpdatedLastTick); Assert.True(ui.NeedsRender); Assert.True(scroll.Offset > 0);
        Assert.Equal(1, outputs); Assert.Equal(1, diagnostics.Snapshot.Present.Attempts);
        var updates = diagnostics.Snapshot.Update.Completed; Assert.False(pump.Tick(false, Output)); Assert.Equal(updates, diagnostics.Snapshot.Update.Completed);
        clock.Advance(10); Assert.True(pump.Tick(false, Output)); Assert.False(ui.NeedsRender); Assert.Equal(2, outputs);
        Assert.Equal("100", tree.SelectedId); Assert.True(tree.GetItemBounds("100")!.Value.Y >= scroll.Bounds.Y);
    }

    [Fact]
    public void VisualInvalidationDuringDraw_DefersOutputWithoutLoopingUpdateOnTheOwner()
    {
        using var fixture = new Fixture(); var draws = 0; var outputs = 0;
        fixture.Surface.Drawing = () => { if (++draws == 1) fixture.Ui.Root.Invalidate(); };
        bool Output() { Assert.False(fixture.Ui.NeedsRender); outputs++; return true; }
        Assert.False(fixture.Pump.Tick(false, Output)); Assert.Equal(1, draws); Assert.Equal(0, outputs);
        Assert.True(fixture.Ui.NeedsRender); Assert.False(fixture.Pump.Tick(false, Output)); Assert.Equal(1, draws);
        fixture.Clock.Advance(10); Assert.True(fixture.Pump.Tick(false, Output)); Assert.Equal(2, draws); Assert.Equal(1, outputs);
        Assert.Equal(0, fixture.Diagnostics.Snapshot.Update.Failed); Assert.Equal(0, fixture.Diagnostics.Snapshot.Present.Failed);
    }

    [Fact]
    public void OnDemand_CleanTreeWithObserversDoesNotUpdateOrPresentUntilRequested()
    {
        using var fixture = new Fixture();
        Assert.True(fixture.Tick()); var initial = fixture.Diagnostics.Snapshot;
        fixture.Clock.Advance(100);
        Assert.False(fixture.Tick()); Assert.Equal(initial, fixture.Diagnostics.Snapshot);
        fixture.Ui.RequestFrame(); Assert.True(fixture.Tick());
        var after = fixture.Diagnostics.Snapshot;
        Assert.Equal(2, after.Render.Completed); Assert.Equal(1, after.Measure.Completed);
        Assert.Equal(2, after.Present.Completed);
        Assert.Equal(0, after.Queue.Accepted);
    }

    [Theory]
    [InlineData(UiFrameMode.Automatic, false, false)]
    [InlineData(UiFrameMode.Automatic, true, true)]
    [InlineData(UiFrameMode.Continuous, false, true)]
    [InlineData(UiFrameMode.OnDemand, true, false)]
    public void Modes_PreserveLegacyObserverPollingAndExplicitContinuousMode(UiFrameMode mode, bool observers, bool repeats)
    {
        using var fixture = new Fixture(mode); Assert.True(fixture.Tick(observers));
        fixture.Clock.Advance(10); Assert.Equal(repeats, fixture.Tick(observers));
        Assert.Equal(repeats ? 2 : 1, fixture.Diagnostics.Snapshot.Update.Completed);
        Assert.Equal(1, fixture.Diagnostics.Snapshot.Render.Completed);
    }

    [Fact]
    public void Automatic_ObserverRemovalReturnsToIdleAndAddingObserverResumesPolling()
    {
        using var fixture = new Fixture(UiFrameMode.Automatic); fixture.Tick(true);
        fixture.Clock.Advance(10); Assert.False(fixture.Tick(false));
        Assert.True(fixture.Tick(true)); Assert.Equal(2, fixture.Diagnostics.Snapshot.Present.Completed);
    }

    [Fact]
    public void WorkSignal_WakesOnceForFloodAndDrainsWithinTheFrameBudget()
    {
        using var fixture = new Fixture(); fixture.Tick(); fixture.Clock.Advance(10);
        var before = fixture.Wakes; var ran = 0;
        Parallel.For(0, 4, _ => fixture.Ui.Post(() => ran++));
        Assert.Equal(before + 1, fixture.Wakes);
        Assert.False(fixture.Tick()); Assert.Equal(2, ran); Assert.True(fixture.Ui.NeedsUpdate);
        Assert.False(fixture.Tick()); Assert.Equal(2, ran);
        fixture.Clock.Advance(10); Assert.False(fixture.Tick()); Assert.Equal(4, ran);
        Assert.Equal(4, fixture.Diagnostics.Snapshot.Queue.Completed);
    }

    [Fact]
    public void FrameRequests_CoalesceOutsideQueueCapacityAndRespectMonotonicCadence()
    {
        using var fixture = new Fixture(); fixture.Tick();
        var before = fixture.Wakes;
        Parallel.For(0, 1000, _ => fixture.Ui.RequestFrame());
        Assert.Equal(before + 1, fixture.Wakes);
        fixture.Clock.Advance(9); Assert.False(fixture.Tick());
        Assert.Equal(1, fixture.Pump.WaitMilliseconds(true, false, true));
        fixture.Clock.Advance(1); Assert.True(fixture.Tick());
        Assert.Equal(2, fixture.Diagnostics.Snapshot.Render.Completed); Assert.Equal(0, fixture.Diagnostics.Snapshot.Queue.Accepted);
    }

    [Fact]
    public void FullQueue_DoesNotRejectAnIndependentFrameRequestOrInvalidateCompletedOutput()
    {
        using var fixture = new Fixture(); fixture.Tick();
        for (var i = 0; i < 4; i++) fixture.Ui.Post(() => { });
        fixture.Ui.RequestFrame(); Assert.True(fixture.Ui.NeedsUpdate); Assert.False(fixture.Ui.NeedsRender);
        fixture.Clock.Advance(10); Assert.True(fixture.Tick());
        Assert.True(fixture.Ui.NeedsUpdate); Assert.False(fixture.Ui.NeedsRender);
        Assert.Equal(2, fixture.Diagnostics.Snapshot.Queue.Pending); Assert.Equal(0, fixture.Diagnostics.Snapshot.Queue.Rejected);
        Assert.Equal(2, fixture.Diagnostics.Snapshot.Present.Completed);
    }

    [Fact]
    public void RequestDuringDrawing_IsRetainedForTheNextFrame()
    {
        using var fixture = new Fixture(); var request = true;
        fixture.Surface.Drawing = () => { if (request) { request = false; fixture.Ui.RequestFrame(); } };
        fixture.Tick(); Assert.True(fixture.Ui.NeedsUpdate);
        fixture.Clock.Advance(10); Assert.True(fixture.Tick()); Assert.False(fixture.Ui.NeedsUpdate);
        fixture.Clock.Advance(10); Assert.False(fixture.Tick()); Assert.Equal(2, fixture.Diagnostics.Snapshot.Render.Completed);
    }

    [Fact]
    public void NativeRefresh_PresentsCachedImageImmediatelyWithoutMeasureOrDraw()
    {
        using var fixture = new Fixture(); fixture.Tick(); var before = fixture.Diagnostics.Snapshot;
        fixture.Pump.RequestPresentation(); Assert.True(fixture.Tick(immediate: true));
        var after = fixture.Diagnostics.Snapshot;
        Assert.Equal(before.Update, after.Update); Assert.Equal(before.Render, after.Render);
        Assert.Equal(2, after.Present.Completed);
    }

    [Fact]
    public void Suspended_DrainsPostedWorkOnceAndResumesWithTheRetainedRedraw()
    {
        using var fixture = new Fixture(); fixture.Ui.Resize(new UiViewport(0, 100));
        Assert.False(fixture.Tick()); var updates = fixture.Diagnostics.Snapshot.Update.Completed;
        fixture.Clock.Advance(100); Assert.False(fixture.Tick());
        Assert.Equal(updates, fixture.Diagnostics.Snapshot.Update.Completed);
        var ran = false; fixture.Ui.Post(() => ran = true); Assert.False(fixture.Tick()); Assert.True(ran);
        Assert.Equal(0, fixture.Diagnostics.Snapshot.Render.Completed);
        Assert.Equal(16, fixture.Pump.WaitMilliseconds(true, true, true));
        fixture.Ui.Resize(new UiViewport(150, 100, 1.5f)); fixture.Clock.Advance(10);
        Assert.True(fixture.Tick()); Assert.Equal(new UiViewport(150, 100, 1.5f), fixture.Diagnostics.Snapshot.Viewport);
    }

    [Fact]
    public void SkippedPresentation_RetriesAtTheNextDeadlineWithoutDrawingAgain()
    {
        using var fixture = new Fixture(); fixture.Output = false; Assert.False(fixture.Tick());
        Assert.Equal(1, fixture.Diagnostics.Snapshot.Present.Skipped);
        fixture.Output = true; Assert.False(fixture.Tick());
        fixture.Clock.Advance(10); Assert.True(fixture.Tick());
        Assert.Equal(1, fixture.Diagnostics.Snapshot.Render.Completed); Assert.Equal(1, fixture.Diagnostics.Snapshot.Present.Completed);
    }

    [Fact]
    public void CallbackFailure_PreservesTheCauseAndPendingWorkForRetry()
    {
        using var fixture = new Fixture(); var cause = new Exception("post"); fixture.Ui.Post(() => throw cause);
        Assert.Same(cause, Assert.Throws<Exception>(() => fixture.Tick()));
        fixture.Clock.Advance(10); Assert.True(fixture.Tick()); Assert.Equal(1, fixture.Diagnostics.Snapshot.Update.Failed);
    }

    [Fact]
    public void SiblingSession_ReceivesFramesWhileTheFirstContinuouslySelfPosts()
    {
        using var first = new Fixture(); using var second = new Fixture(); var count = 0;
        void Again() { count++; first.Ui.Post(Again); }
        first.Ui.Post(Again); first.Tick(); second.Tick();
        for (var i = 0; i < 3; i++)
        {
            first.Clock.Advance(10); second.Clock.Advance(10);
            second.Ui.RequestFrame(); first.Tick(); Assert.True(second.Tick());
        }
        Assert.Equal(8, count); Assert.Equal(4, second.Diagnostics.Snapshot.Present.Completed);
    }

    [Fact]
    public void Detach_RemovesWakeTargetWithoutClosingTheSession()
    {
        using var fixture = new Fixture(); fixture.Tick(); var before = fixture.Wakes;
        fixture.Pump.Dispose(); fixture.Ui.Post(() => { }); fixture.Ui.RequestFrame();
        Assert.Equal(before, fixture.Wakes); Assert.True(fixture.Ui.Update());
        fixture.Ui.Dispose(); Assert.Throws<ObjectDisposedException>(fixture.Ui.RequestFrame);
    }

    [Theory]
    [InlineData((UiFrameMode)99, 60)]
    [InlineData(UiFrameMode.OnDemand, 0)]
    [InlineData(UiFrameMode.OnDemand, 241)]
    public void InvalidPolicy_IsRejectedBeforeAttachingAClockOrSession(UiFrameMode mode, int rate)
    { Assert.Throws<ArgumentOutOfRangeException>(() => new UiFramePump(mode, rate, () => { })); }

    private sealed class Fixture : IDisposable
    {
        internal readonly Clock Clock = new();
        internal readonly Surface Surface = new();
        internal readonly UiSession Ui;
        internal readonly UiFramePump Pump;
        internal readonly UiDiagnostics Diagnostics;
        internal bool Output = true;
        internal int Wakes;
        internal Fixture(UiFrameMode mode = UiFrameMode.OnDemand)
        {
            Ui = new UiSession(new Panel(), Surface, new UiViewport(100, 100), null, new UiDispatchOptions { Capacity = 4, ActionsPerUpdate = 2 });
            Diagnostics = Ui.EnableDiagnostics();
            Pump = new UiFramePump(mode, 100, () => Interlocked.Increment(ref Wakes), Clock); Pump.Attach(Ui);
        }
        internal bool Tick(bool observers = true, bool immediate = false) => Pump.Tick(observers, () => Output, immediate);
        public void Dispose() { Pump.Dispose(); Ui.Dispose(); }
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(int milliseconds) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
    private sealed class Surface : IUiRenderSurface
    {
        internal Action? Drawing;
        public ITextLayoutService TextLayout { get; } = new Layout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => Drawing?.Invoke();
        public void Dispose() { }
    }
    private sealed class Layout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
