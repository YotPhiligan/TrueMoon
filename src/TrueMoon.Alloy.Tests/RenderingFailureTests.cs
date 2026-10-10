using AlloyTest;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class RenderingFailureTests
{
    private static readonly UiViewport Viewport = new(900, 1100);
    private static UiRenderingException Failure(UiRenderingFailureKind kind = UiRenderingFailureKind.BackendFailure) =>
        new("controlled", "Render", kind, new InvalidOperationException("original graphics error"));

    [Theory]
    [InlineData(UiRenderingFailureKind.DeviceLost)]
    [InlineData(UiRenderingFailureKind.ContextLost)]
    [InlineData(UiRenderingFailureKind.BackendFailure)]
    public void Exception_PreservesClassificationOperationAndOriginalCause(UiRenderingFailureKind kind)
    {
        var cause = new Exception("original graphics error"); var error = new UiRenderingException("controlled", "Render", kind, cause);
        Assert.Equal("controlled", error.Backend); Assert.Equal("Render", error.Operation);
        Assert.Equal(kind, error.Kind); Assert.Equal("original graphics error", error.InnerException!.Message);
        Assert.Same(cause, error.InnerException);
        Assert.Contains(kind.ToString(), error.Message);
    }

    [Fact]
    public void Exception_RejectsMissingCauseNamesAndUnknownKind()
    {
        Assert.Throws<ArgumentNullException>(() => new UiRenderingException("b", "op", UiRenderingFailureKind.DeviceLost, null!));
        foreach (var name in new[] { null, "", " " })
        {
            Assert.ThrowsAny<ArgumentException>(() => new UiRenderingException(name!, "op", UiRenderingFailureKind.DeviceLost, new Exception()));
            Assert.ThrowsAny<ArgumentException>(() => new UiRenderingException("b", name!, UiRenderingFailureKind.DeviceLost, new Exception()));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiRenderingException("b", "op", (UiRenderingFailureKind)99, new Exception()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TerminalRenderOrResize_ReportsOnceAndBlocksFurtherWorkUntilExplicitDisposal(bool resize)
    {
        var root = new Button("Action"); var surface = new Surface(); using var ui = new UiSession(root, surface, Viewport);
        ui.Update(); ui.HandleInput(new UiInput(InputKind.PointerDown, 20, 20));
        Assert.True(root.IsFocused); Assert.True(root.IsPressed); Assert.True(root.IsHovered);
        var error = Failure(); var reports = new List<UiRenderingException>(); var owner = Environment.CurrentManagedThreadId;
        ui.RenderingFailed += failure => { Assert.Equal(owner, Environment.CurrentManagedThreadId); reports.Add(failure); };
        surface.Error = error; root.Background = new Color(1, 0, 0);
        Assert.Same(error, Assert.Throws<UiRenderingException>(() => { if (resize) ui.Resize(new UiViewport(901, 1100)); else ui.Update(); }));
        Assert.Same(error, ui.RenderingFailure); Assert.True(ui.IsFaulted); Assert.False(ui.IsDisposed);
        Assert.Equal(Viewport, ui.Viewport); Assert.False(root.IsFocused); Assert.False(root.IsPressed); Assert.False(root.IsHovered);
        Assert.False(ui.ReportRenderingFailure(Failure(UiRenderingFailureKind.DeviceLost))); Assert.Single(reports); Assert.Same(error, reports[0]);
        var calls = surface.WorkCalls;
        foreach (Action operation in new Action[] { () => ui.Update(), () => ui.Resize(Viewport), () => ui.HandleInput(new UiInput(InputKind.Text, Text: "x")), () => ui.Post(() => { }), ui.VerifyRendering, () => ui.SetTheme(Theme.Light), () => ui.Focus(root), () => ui.Capture(root), () => ui.GetClipboardText() })
            Assert.Same(error, Assert.Throws<InvalidOperationException>(operation).InnerException);
        Assert.Equal(calls, surface.WorkCalls); ui.VerifyAccess(); ui.Dispose(); ui.Dispose();
        Assert.True(root.IsDisposed); Assert.Equal(1, surface.Disposals); Assert.True(ui.IsDisposed); Assert.Same(error, ui.RenderingFailure);
    }

    [Fact]
    public void ExternalFailure_DiscardsPendingPostsAndNotifiesEveryObserverWhilePreventingReentrantDisposal()
    {
        var surface = new Surface(); using var ui = new UiSession(new Panel(), surface, Viewport);
        var ran = false; ui.Post(() => ran = true); var error = Failure(); var observerError = new Exception("observer"); var observed = 0;
        ui.RenderingFailed += _ => throw observerError;
        ui.RenderingFailed += failure => { observed++; Assert.Same(error, failure); Assert.Throws<InvalidOperationException>(ui.Dispose); };
        var aggregate = Assert.Throws<AggregateException>(() => ui.ReportRenderingFailure(error));
        Assert.Equal(new Exception[] { error, observerError }, aggregate.InnerExceptions); Assert.Equal(1, observed); Assert.False(ran);
        Assert.False(ui.ReportRenderingFailure(Failure())); Assert.False(ran); Assert.Equal(0, surface.WorkCalls);
        ui.Dispose(); Assert.Equal(1, surface.Disposals); Assert.Throws<ObjectDisposedException>(() => ui.Post(() => ran = true));
    }

    [Fact]
    public void OrdinaryDrawError_RemainsRetryableAndDoesNotReportTerminalFailure()
    {
        var root = new Panel(); var surface = new Surface { Error = new InvalidOperationException("ordinary draw error") };
        using var ui = new UiSession(root, surface, Viewport); var notifications = 0; ui.RenderingFailed += _ => notifications++;
        Assert.Same(surface.Error, Assert.Throws<InvalidOperationException>(() => ui.Update())); Assert.False(ui.IsFaulted); Assert.True(ui.NeedsUpdate);
        surface.Error = null; Assert.True(ui.Update()); Assert.False(ui.Update()); Assert.Equal(0, notifications); Assert.Equal(2, surface.WorkCalls);
    }

    [Fact]
    public void FailureReportedByPostedWork_PreventsThePendingDrawAndCancelsRemainingPosts()
    {
        var surface = new Surface(); using var ui = new UiSession(new Panel(), surface, Viewport); var error = Failure(); var second = false;
        ui.Post(() => ui.ReportRenderingFailure(error)); ui.Post(() => second = true);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => ui.Update()).InnerException);
        Assert.Equal(0, surface.WorkCalls); Assert.False(second); Assert.Same(error, ui.RenderingFailure);
    }

    [Fact]
    public void InputCancellationError_DoesNotSkipCapturedElementHoverOrFailureObservers()
    {
        var root = new Panel(); var focused = new CancellingButton { Width = 100 }; var captured = new CancellingButton { Width = 100 };
        root.Add(focused); root.Add(captured); var surface = new Surface(); using var ui = new UiSession(root, surface, Viewport);
        ui.Update(); ui.Focus(focused); ui.Capture(captured); ui.HandleInput(new UiInput(InputKind.PointerMove, 20, 20));
        Assert.True(captured.IsHovered); Assert.Equal(0, captured.Cancellations); focused.ThrowOnCancel = true;
        var error = Failure(); var notified = 0; ui.RenderingFailed += failure => { Assert.Same(error, failure); notified++; };
        var errors = Assert.Throws<AggregateException>(() => ui.ReportRenderingFailure(error));
        Assert.Same(error, errors.InnerExceptions[0]); Assert.Contains(focused.Error, errors.Flatten().InnerExceptions);
        Assert.False(focused.IsFocused); Assert.Equal(1, captured.Cancellations); Assert.False(captured.IsHovered); Assert.Equal(1, notified);
        Assert.Contains(focused.Error, Assert.Throws<AggregateException>(ui.Dispose).Flatten().InnerExceptions);
        Assert.True(ui.IsDisposed); Assert.False(root.IsAttached); Assert.Equal(1, surface.Disposals);
    }

    [Fact]
    public void FailureReportedDuringLayout_PreventsRenderingAfterMeasurementReturns()
    {
        var surface = new Surface(); var root = new LayoutReportingElement(); using var ui = new UiSession(root, surface, Viewport); var error = Failure();
        root.Report = () => ui.ReportRenderingFailure(error);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => ui.Update()).InnerException);
        Assert.Equal(0, surface.WorkCalls); Assert.True(ui.IsFaulted); Assert.True(ui.NeedsUpdate);
    }

    [Fact]
    public void BorrowedSurface_FaultDoesNotTransferOwnershipOrPermitPrematureDisposal()
    {
        var root = new Panel(); var surface = new Surface(); using var ui = new UiSession(root, surface, Viewport);
        ui.Update(); surface.Borrowed = true; ui.ReportRenderingFailure(Failure());
        Assert.Throws<InvalidOperationException>(ui.Dispose); Assert.False(root.IsDisposed); Assert.True(root.IsAttached); Assert.False(ui.IsDisposed); Assert.Equal(0, surface.Disposals);
        surface.Borrowed = false; ui.Dispose(); Assert.True(root.IsDisposed); Assert.Equal(1, surface.Disposals);
    }

    [Fact]
    public void Cleanup_AttemptsEveryActionAndPreservesPrimaryAndCleanupErrorIdentityInOrder()
    {
        var primary = Failure(); var first = new Exception("first cleanup"); var last = new Exception("last cleanup"); var order = new List<int>();
        var error = Assert.Throws<AggregateException>(() => UiCleanup.Complete(primary,
            () => { order.Add(1); throw first; }, () => order.Add(2), () => { order.Add(3); throw last; }));
        Assert.Equal(new[] { 1, 2, 3 }, order); Assert.Equal(new Exception[] { primary, first, last }, error.InnerExceptions);
        Assert.Same(primary, Assert.Throws<UiRenderingException>(() => UiCleanup.Complete(primary, () => order.Add(4))));
        Assert.Equal(new[] { 1, 2, 3, 4 }, order);
    }

    [Fact]
    public void Cleanup_ValidatesAllActionsBeforeStartingAndRethrowsSingleCleanupError()
    {
        var ran = false; Assert.Throws<ArgumentNullException>(() => UiCleanup.Complete(null, null!));
        Assert.Throws<ArgumentException>(() => UiCleanup.Complete(null, () => ran = true, null!)); Assert.False(ran);
        var error = new Exception("cleanup"); Assert.Same(error, Assert.Throws<Exception>(() => UiCleanup.Complete(null, () => throw error)));
        UiCleanup.Complete(null, () => ran = true); Assert.True(ran); UiCleanup.Complete(null);
    }

    [Fact]
    public void SessionDisposal_ReportsAllCleanupErrorsAndStillDetachesTreeAndNotifiesRegistryOnce()
    {
        var treeError = new Exception("tree"); var surfaceError = new Exception("surface"); var observerError = new Exception("disposed observer");
        var root = new Panel(); var owned = new ThrowingResource(treeError); root.Own(owned);
        var surface = new Surface { DisposeError = surfaceError }; var ui = new UiSession(root, surface, Viewport); ui.ReportRenderingFailure(Failure());
        var notified = 0; ui.Disposed += _ => throw observerError; ui.Disposed += _ => notified++;
        var errors = Assert.Throws<AggregateException>(ui.Dispose).Flatten().InnerExceptions;
        Assert.Equal(new[] { treeError, surfaceError, observerError }, errors); Assert.True(root.IsDisposed); Assert.False(root.IsAttached);
        Assert.True(ui.IsDisposed); Assert.Equal(1, owned.Disposals); Assert.Equal(1, surface.Disposals); Assert.Equal(1, notified);
        ui.Dispose(); Assert.Equal(1, owned.Disposals); Assert.Equal(1, surface.Disposals); Assert.Equal(1, notified);
    }

    [Fact]
    public void CreationRollback_KeepsCallerRootAndPreservesAttachmentAndSurfaceCleanupErrors()
    {
        var root = new Panel(); using var first = new UiSession(root, new Surface(), Viewport);
        var cleanupError = new Exception("surface cleanup"); var surface = new Surface { DisposeError = cleanupError };
        var errors = Assert.Throws<AggregateException>(() => UiSession.Create(root, new Backend(surface), new Target(), Viewport)).InnerExceptions;
        Assert.IsType<ArgumentException>(errors[0]); Assert.Same(cleanupError, errors[1]); Assert.Equal(1, surface.Disposals);
        Assert.True(root.IsAttached); Assert.False(root.IsDisposed); Assert.Same(root, first.Root); Assert.True(first.Update());
    }

    [Fact]
    public void ForeignThread_ReportAndDisposalRejectBeforeMutation()
    {
        var surface = new Surface(); using var ui = new UiSession(new Panel(), surface, Viewport);
        Exception? report = null, dispose = null; var thread = new Thread(() =>
        {
            try { ui.ReportRenderingFailure(Failure()); } catch (Exception error) { report = error; }
            try { ui.Dispose(); } catch (Exception error) { dispose = error; }
        });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(report); Assert.IsType<InvalidOperationException>(dispose);
        Assert.False(ui.IsFaulted); Assert.False(ui.IsDisposed); Assert.Equal(0, surface.Disposals); Assert.True(ui.Update());
    }

    [Fact]
    public void ExplicitRecreation_PreservesCallerModelRowsAndCreatesNewTreeWithoutOldSubscriptionsOrFocus()
    {
        var model = new SettingsModel { Name = "Сохранено 👩‍💻" }; model.AddRow(); model.Rows[0].Name = "Row Б"; var rows = model.Rows.ToArray();
        var oldView = new View1(model); var surface = new Surface(); using var oldUi = new UiSession(oldView, surface, Viewport);
        oldUi.Update(); oldUi.Focus(oldView.NameEditor); oldUi.Capture(oldView.NameEditor); surface.Error = Failure(); oldView.Background = new Color(1, 0, 0);
        Assert.Throws<UiRenderingException>(() => oldUi.Update()); Assert.False(oldView.NameEditor.IsFocused); oldUi.Dispose();
        var oldValue = oldView.NameEditor.Value; Assert.Equal("Сохранено 👩‍💻", oldValue);
        model.Name = "Новая модель 🧑‍💻"; model.AddRow(); Assert.Equal(rows.Length + 1, model.Rows.Count);
        Assert.Equal(oldValue, oldView.NameEditor.Value); Assert.True(oldView.RowList.IsDisposed);
        var newView = new View1(model); using var newUi = new UiSession(newView, new Surface(), Viewport); Assert.True(newUi.Update());
        Assert.NotSame(oldView, newView); Assert.NotSame(oldView.NameEditor, newView.NameEditor);
        Assert.Equal("Новая модель 🧑‍💻", model.Name); Assert.Equal("Новая модель 🧑‍💻", newView.NameEditor.Value);
        Assert.Equal(rows.Length + 1, newView.RowList.Items.Count);
        Assert.All(rows.Zip(model.Rows), pair => Assert.Same(pair.First, pair.Second)); Assert.Equal("Row Б", model.Rows[0].Name);
        Assert.False(newView.NameEditor.IsFocused); Assert.False(newUi.IsFaulted); Assert.False(newUi.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).PointerCaptured);
    }

    private sealed class Target : UiRenderTarget;
    private sealed class Backend(Surface surface) : IRenderBackend
    { public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport) => surface; }
    private sealed class ThrowingResource(Exception error) : IDisposable
    { public int Disposals; public void Dispose() { Disposals++; throw error; } }
    private sealed class CancellingButton : Button
    {
        internal bool ThrowOnCancel;
        internal int Cancellations;
        internal readonly Exception Error = new("cancel input");
        public override void OnInputCancelled() { base.OnInputCancelled(); Cancellations++; if (ThrowOnCancel) throw Error; }
    }
    private sealed class LayoutReportingElement : Element
    {
        internal Action? Report;
        protected override Size MeasureCore(Size available, ITextLayoutService text) { Report?.Invoke(); return new Size(10, 10); }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public Exception? Error, DisposeError;
        public bool Borrowed;
        public int WorkCalls, Disposals;
        public void VerifyAvailable() { if (Borrowed) throw new InvalidOperationException("borrowed"); }
        public void Resize(UiViewport viewport) { WorkCalls++; if (Error != null) throw Error; }
        public void Render(Element root, UiViewport viewport) { WorkCalls++; if (Error != null) throw Error; root.Draw(new RecordingDrawingContext()); }
        public void Dispose() { VerifyAvailable(); Disposals++; if (DisposeError != null) throw DisposeError; }
    }
}
