using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests;

public sealed class WindowComponentBindingTests
{
    [Fact]
    public void Constructor_RejectsMissingBorrowedDependenciesWithoutConnectingOrCallingNative()
    {
        using var window = new Window(); var controller = new Controller(); var calls = 0;
        Assert.Throws<ArgumentNullException>(() => new WindowComponentBinding(null!, controller, _ => calls++, _ => calls++));
        Assert.Throws<ArgumentNullException>(() => new WindowComponentBinding(window, controller, null!, _ => calls++));
        Assert.Throws<ArgumentNullException>(() => new WindowComponentBinding(window, controller, _ => calls++, null!));
        Assert.Throws<ArgumentNullException>(() => new WindowComponentBinding(window, null!, _ => calls++, _ => calls++));
        Assert.False(window.IsHosted); Assert.Equal(0, calls); Assert.Equal(0, controller.Disposals);
    }

    [Fact]
    public void Configure_UsesEffectiveComponentValuesAndChecksChromeBeforeAnyHostConnection()
    {
        using var window = new Window();
        window.WithViewStyle(new Declaration<Window>(style => style.Root()
            .Set(Window.TitleProperty, "styled caption").Set(Window.ClientSizeProperty, new Size(800, 600))));
        var options = new SilkUiWindowOptions { Title = "legacy", Width = 320, Height = 240 };
        WindowComponentBinding.ValidateRoot(window); WindowComponentBinding.Configure(window, options);
        Assert.Equal("styled caption", options.Title); Assert.Equal(800, options.Width); Assert.Equal(600, options.Height);
        Assert.False(window.IsHosted); Assert.Null(window.Parent); Assert.False(window.IsAttached);
        options.Chrome = new WindowChromeOptions { MinimumSize = new(900, 700) };
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowComponentBinding.Configure(window, options));
        Assert.False(window.IsHosted); Assert.False(window.IsDisposed);
    }

    [Fact]
    public void ValidateRoot_RejectsForeignOwnershipAndNestedOrAlreadyHostedWindowWithoutChangingIt()
    {
        using var component = new Window(); using var owner = new Panel(); owner.Add(component);
        Assert.Throws<InvalidOperationException>(() => WindowComponentBinding.ValidateRoot(component));
        Assert.Throws<InvalidOperationException>(() => WindowComponentBinding.ValidateRoot(owner));
        Assert.Same(owner, component.Parent); Assert.False(component.IsDisposed); Assert.False(owner.IsDisposed);
        owner.Items.Remove(component); WindowComponentBinding.ValidateRoot(component);
        using (var connection = component.AttachHost(new Controller()))
        {
            Assert.Throws<InvalidOperationException>(() => WindowComponentBinding.ValidateRoot(component));
            Assert.True(component.IsHosted);
        }
        using var ui = Create(component);
        Assert.Throws<InvalidOperationException>(() => WindowComponentBinding.ValidateRoot(component)); Assert.True(component.IsAttached);
        ui.Dispose(); Assert.Throws<InvalidOperationException>(() => WindowComponentBinding.ValidateRoot(component));
        using var ordinary = new Panel(); WindowComponentBinding.ValidateRoot(ordinary); Assert.False(ordinary.IsDisposed);
    }

    [Theory]
    [InlineData("null", false)]
    [InlineData("disposed", false)]
    [InlineData("parented", false)]
    [InlineData("hosted", false)]
    [InlineData("nested", true)]
    [InlineData("chrome-size", true)]
    public async Task StartupValidation_RejectsBeforeSessionOrNativeAndDisposesOnlyAdoptedFreshRoot(string kind, bool adopted)
    {
        var component = new Window { ClientSize = new(400, 300) }; var owned = new Counter(); component.Own(owned);
        using var parent = new Panel(); WindowHostConnection? connection = null;
        Element? root = component;
        if (kind == "null") root = null;
        else if (kind == "disposed") component.Dispose();
        else if (kind == "parented") parent.Add(component);
        else if (kind == "hosted") connection = component.AttachHost(new Controller());
        else if (kind == "nested") root = new Panel().WithChildren(component);
        var options = new SilkUiWindowOptions { Width = 1000, Height = 800, Appearance = new WindowAppearance { Decorated = false } };
        if (kind == "chrome-size") options.Chrome = new WindowChromeOptions { MinimumSize = new(800, 600) };
        var sessions = new NoSessions(); var roots = 0;
        await using var dispatcher = new HostedUiDispatcher();
        var hosted = new HostedUiWindow(options, sessions, () => { roots++; return root!; }, null, dispatcher);
        var failure = await Record.ExceptionAsync(() => hosted.StartAsync());
        Assert.NotNull(failure);
        if (kind == "chrome-size") Assert.IsType<ArgumentOutOfRangeException>(failure);
        else Assert.IsType<InvalidOperationException>(failure);
        Assert.Same(failure, await Record.ExceptionAsync(() => hosted.Completion));
        Assert.False(hosted.IsClosed); Assert.Equal(1, roots); Assert.Equal(0, sessions.Creates);
        Assert.Equal(adopted || kind == "disposed", component.IsDisposed);
        Assert.Equal(adopted || kind == "disposed" ? 1 : 0, owned.Disposals);
        if (kind == "parented") Assert.Same(parent, component.Parent);
        if (kind == "hosted") Assert.True(component.IsHosted);
        connection?.Dispose();
        if (component.Parent == null) component.Dispose();
        Assert.Same(failure, await Record.ExceptionAsync(() => hosted.StopAsync()));
    }

    [Fact]
    public void Synchronize_PollsEffectiveLocalBindingAndStyleValuesWithoutRepeatingNativeWritesOrResizeFeedback()
    {
        var model = new CaptionModel(); var component = new Window(); var writes = new List<string>(); var sizes = new List<Size>();
        using var binding = new WindowComponentBinding(component, new Controller(), writes.Add, sizes.Add);
        using var ui = Create(component); ui.Update();
        binding.Synchronize(WindowPresentationState.Normal, true, new(900.5f, 700));
        binding.Synchronize(WindowPresentationState.Normal, true, new(901.25f, 700));
        Assert.Empty(writes); Assert.Empty(sizes); Assert.Equal(new Size(960, 540), component.ClientSize);
        component.Style = new Style().With(Window.TitleProperty, "direct").With(Window.ClientSizeProperty, new Size(800, 600));
        binding.Synchronize(WindowPresentationState.Normal, true, new(800, 600));
        Assert.Equal(["direct"], writes); Assert.Equal([new Size(800, 600)], sizes);
        component.Title = "local"; component.ClientSize = new(1000, 600);
        binding.Synchronize(WindowPresentationState.Normal, true, new(1000, 600));
        component.Clear(Window.TitleProperty); component.Clear(Window.ClientSizeProperty);
        binding.Synchronize(WindowPresentationState.Normal, true, new(800, 600));
        Assert.Equal(["direct", "local", "direct"], writes);
        Assert.Equal([new Size(800, 600), new Size(1000, 600), new Size(800, 600)], sizes);
        component.Style = null;
        component.WithViewStyle(new Declaration<Window>(style => style.Root()
            .Set(Window.TitleProperty, theme => ReferenceEquals(theme, Theme.Light) ? "light" : "dark")
            .Set(Window.ClientSizeProperty, new Size(700, 500))));
        binding.Synchronize(WindowPresentationState.Normal, true, new(700, 500));
        ui.SetTheme(Theme.Light); binding.Synchronize(WindowPresentationState.Normal, true, new(700, 500));
        Assert.Equal("light", writes[^1]); Assert.Equal(4, sizes.Count);
        component.Bind(Window.TitleProperty, model, value => value.Caption);
        binding.Synchronize(WindowPresentationState.Normal, true, new(700, 500)); Assert.Equal("bound", writes[^1]);
        model.Caption = "changed binding"; ui.Update();
        binding.Synchronize(WindowPresentationState.Normal, true, new(701, 501));
        Assert.Equal("changed binding", writes[^1]); Assert.Equal(4, sizes.Count); Assert.Equal(new Size(701, 501), component.ActualClientSize);
        var count = writes.Count; binding.Synchronize(WindowPresentationState.Normal, true, new(701, 501)); Assert.Equal(count, writes.Count);
        binding.Dispose(); ui.Dispose(); Assert.Equal(0, model.Subscribers); Assert.Equal(0, model.Disposals);
    }

    [Fact]
    public void Synchronize_RejectsWorkerAndStaleConnectionBeforeNativeSideEffects()
    {
        var component = new Window(); var titles = new List<string>(); var sizes = new List<Size>(); var controller = new Controller();
        var binding = new WindowComponentBinding(component, controller, titles.Add, sizes.Add);
        using var ui = Create(component); ui.Update();
        component.Title = "pending caption"; component.ClientSize = new(400, 300); var notifications = 0;
        component.HostStateChanged += _ => notifications++;
        Exception? error = null;
        var worker = new Thread(() => error = Record.Exception(() => binding.Synchronize(WindowPresentationState.Maximized, true, new(400, 300))));
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidOperationException>(error); Assert.Empty(titles); Assert.Empty(sizes); Assert.Equal(0, notifications);
        Assert.Equal(WindowPresentationState.Normal, component.State); Assert.False(component.IsActive);
        binding.Synchronize(WindowPresentationState.Maximized, true, new(400, 300));
        Assert.Equal(["pending caption"], titles); Assert.Equal([new Size(400, 300)], sizes); Assert.Equal(1, notifications);
        binding.Dispose(); component.Title = "after detach"; component.ClientSize = new(500, 300);
        Assert.Throws<ObjectDisposedException>(() => binding.Synchronize(WindowPresentationState.Normal, false, new(500, 300)));
        Assert.Single(titles); Assert.Single(sizes); Assert.Equal(2, notifications); Assert.Equal(0, controller.Disposals);
        Assert.Throws<InvalidOperationException>(() => new WindowComponentBinding(component, controller, titles.Add, sizes.Add));
        Assert.False(component.IsHosted); Assert.Single(titles); Assert.Single(sizes);
    }

    [Fact]
    public void DisposedBinding_CannotPublishNativeValuesIntoANewDetachedConnection()
    {
        using var component = new Window(); var writes = 0;
        var old = new WindowComponentBinding(component, new Controller(), _ => writes++, _ => writes++);
        old.Dispose();
        using var current = new WindowComponentBinding(component, new Controller(), _ => writes++, _ => writes++);
        component.Title = "belongs to current host"; component.ClientSize = new(800, 600);
        Assert.Throws<ObjectDisposedException>(() => old.Synchronize(WindowPresentationState.Minimized, false, default));
        Assert.Throws<ObjectDisposedException>(() => old.SnapshotClosingHandlers());
        old.Dispose(); Assert.True(component.IsHosted); Assert.Equal(0, writes);
        current.Synchronize(WindowPresentationState.Maximized, true, new(800, 600));
        Assert.Equal(2, writes); Assert.Equal(WindowPresentationState.Maximized, component.State); Assert.True(component.IsActive);
    }

    [Fact]
    public void CaptionCallbackDisconnect_PreventsSubsequentResizeAndObservedStatePublication()
    {
        using var component = new Window(); WindowComponentBinding? binding = null;
        var titles = new List<string>(); var sizes = new List<Size>(); var notifications = 0;
        binding = new(component, new Controller(), title => { titles.Add(title); binding!.Dispose(); }, sizes.Add);
        using (binding)
        {
            component.Title = "disconnect caption"; component.ClientSize = new(800, 600);
            component.HostStateChanged += _ => notifications++;
            Assert.Throws<ObjectDisposedException>(() => binding.Synchronize(WindowPresentationState.Maximized, true, new(800, 600)));
            Assert.Equal(["disconnect caption"], titles); Assert.Empty(sizes); Assert.Equal(1, notifications);
            Assert.False(component.IsHosted); Assert.False(component.IsActive); Assert.Equal(WindowPresentationState.Normal, component.State);
            Assert.Equal(default, component.ActualClientSize); Assert.False(component.IsDisposed);
        }
    }

    [Fact]
    public void HostStateObserverDisconnect_HappensAfterNativeWritesAndLeavesNoLaterSideEffects()
    {
        using var component = new Window(); var calls = new List<string>();
        using var binding = new WindowComponentBinding(component, new Controller(), _ => calls.Add("title"), _ => calls.Add("resize"));
        component.Title = "new caption"; component.ClientSize = new(800, 600);
        component.HostStateChanged += current =>
        {
            if (current.IsHosted)
            {
                Assert.Equal(["title", "resize"], calls);
                Assert.Equal(WindowPresentationState.Maximized, current.State); Assert.Equal(new Size(800, 600), current.ActualClientSize);
                calls.Add("observed"); binding.Dispose();
            }
            else calls.Add("detached");
        };
        binding.Synchronize(WindowPresentationState.Maximized, true, new(800, 600));
        Assert.Equal(["title", "resize", "observed", "detached"], calls);
        Assert.False(component.IsHosted); Assert.False(component.IsActive); Assert.False(component.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => binding.Synchronize(WindowPresentationState.Normal, false, default));
        Assert.Equal(["title", "resize", "observed", "detached"], calls);
    }

    [Theory]
    [InlineData(-1, 400, 300)]
    [InlineData(3, 400, 300)]
    [InlineData(0, -1, 300)]
    [InlineData(0, 400, -1)]
    [InlineData(0, float.NaN, 300)]
    [InlineData(0, 400, float.NaN)]
    [InlineData(0, float.PositiveInfinity, 300)]
    [InlineData(0, 400, float.PositiveInfinity)]
    [InlineData(0, float.NegativeInfinity, 300)]
    [InlineData(0, 400, float.NegativeInfinity)]
    public void InvalidObservation_RejectsBeforePendingCaptionOrResizeSideEffects(int state, float width, float height)
    {
        using var component = new Window(); var writes = 0;
        using var binding = new WindowComponentBinding(component, new Controller(), _ => writes++, _ => writes++);
        component.Title = "pending"; component.ClientSize = new(800, 600);
        Assert.Throws<ArgumentOutOfRangeException>(() => binding.Synchronize((WindowPresentationState)state, true, new(width, height)));
        Assert.Equal(0, writes); Assert.Equal(WindowPresentationState.Normal, component.State); Assert.False(component.IsActive);
        Assert.Equal(default, component.ActualClientSize); Assert.True(component.IsHosted);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("size")]
    public void NativeWriteFailure_IsObservableAndPendingEffectiveValueRetriesOnceThenSettles(string failing)
    {
        using var component = new Window(); var error = new IOException("native write"); var fail = true;
        var titleCalls = 0; var sizeCalls = 0;
        using var binding = new WindowComponentBinding(component, new Controller(),
            _ => { titleCalls++; if (failing == "title" && fail) throw error; },
            _ => { sizeCalls++; if (failing == "size" && fail) throw error; });
        component.Title = "new"; component.ClientSize = new(800, 600);
        Assert.Same(error, Assert.Throws<IOException>(() => binding.Synchronize(WindowPresentationState.Normal, false, default)));
        Assert.Equal(1, titleCalls); Assert.Equal(failing == "title" ? 0 : 1, sizeCalls);
        Assert.True(component.IsHosted); Assert.False(component.IsDisposed);
        fail = false; binding.Synchronize(WindowPresentationState.Normal, false, default);
        Assert.Equal(failing == "title" ? 2 : 1, titleCalls); Assert.Equal(failing == "title" ? 1 : 2, sizeCalls);
        binding.Synchronize(WindowPresentationState.Normal, false, default);
        Assert.Equal(failing == "title" ? 2 : 1, titleCalls); Assert.Equal(failing == "title" ? 1 : 2, sizeCalls);
    }

    [Theory]
    [InlineData(UiCloseReason.Application, WindowCloseReason.Application)]
    [InlineData(UiCloseReason.Native, WindowCloseReason.Native)]
    [InlineData(UiCloseReason.Shutdown, WindowCloseReason.Shutdown)]
    public async Task ClosingSnapshot_MapsReasonsPreservesTaskAndCopiesSubscriptionSet(UiCloseReason reason, WindowCloseReason expected)
    {
        using var component = new Window(); using var binding = new WindowComponentBinding(component, new Controller(), _ => { }, _ => { });
        var seen = new List<WindowCloseReason>(); var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<WindowCloseReason, Task<bool>> first = value => { seen.Add(value); return gate.Task; };
        component.Closing += first; var snapshot = binding.SnapshotClosingHandlers();
        component.Closing -= first; component.Closing += _ => throw new Exception("next attempt only");
        var task = Assert.Single(snapshot)(reason); Assert.Same(gate.Task, task); Assert.Equal([expected], seen);
        gate.SetResult(false); Assert.False(await task);
        Assert.Throws<Exception>(() => { _ = Assert.Single(binding.SnapshotClosingHandlers())(reason); });
        binding.Dispose(); Assert.Throws<ObjectDisposedException>(() => binding.SnapshotClosingHandlers());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void ClosingSnapshot_RejectsUnknownHostReasonWithoutCallingComponent(int reason)
    {
        using var component = new Window(); using var binding = new WindowComponentBinding(component, new Controller(), _ => { }, _ => { });
        var calls = 0; component.Closing += _ => { calls++; return Task.FromResult(true); };
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Assert.Single(binding.SnapshotClosingHandlers())((UiCloseReason)reason); });
        Assert.Equal(0, calls); Assert.True(component.IsHosted);
    }

    [Fact]
    public async Task ComponentClose_VetoKeepsTreeAliveAndRetryWaitsForHandlerAndActualCleanup()
    {
        using var component = new Window(); var child = new Text("draft"); component.SetContent(child);
        using var controller = new CoordinatedController(component); var reasons = new List<WindowCloseReason>();
        var allow = false; var drain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        component.Closing += reason => { reasons.Add(reason); return allow ? drain.Task : Task.FromResult(false); };
        var vetoed = component.RequestCloseAsync(); Assert.False(controller.Poll()); Assert.False(await vetoed);
        Assert.False(controller.CleanupStarted); Assert.True(component.IsHosted); Assert.False(child.IsDisposed);
        allow = true; var accepted = component.RequestCloseAsync(); Assert.False(controller.Poll()); Assert.False(accepted.IsCompleted);
        Assert.False(controller.CleanupStarted); Assert.True(component.IsHosted); Assert.Equal("draft", child.Value);
        drain.SetResult(true); Assert.True(controller.Poll()); Assert.True(controller.CleanupStarted);
        Assert.False(accepted.IsCompleted); Assert.False(child.IsDisposed); Assert.True(component.IsHosted);
        controller.CompleteCleanup(); Assert.True(await accepted);
        Assert.True(component.IsDisposed); Assert.True(child.IsDisposed); Assert.False(component.IsHosted);
        Assert.Equal([WindowCloseReason.Application, WindowCloseReason.Application], reasons); Assert.Equal(0, controller.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeClose_SaveFailureKeepsRootAliveAndFreshAttemptCanRecover(bool synchronous)
    {
        using var component = new Window(); using var controller = new CoordinatedController(component);
        var failure = new IOException("save"); var fail = true; var reasons = new List<WindowCloseReason>();
        component.Closing += reason =>
        {
            reasons.Add(reason);
            if (!fail) return Task.FromResult(true);
            return synchronous ? throw failure : Task.FromException<bool>(failure);
        };
        var first = controller.Request(UiCloseReason.Native); Assert.False(controller.Poll());
        Assert.Same(failure, await Record.ExceptionAsync(() => first)); Assert.Same(failure, controller.LastError);
        Assert.True(component.IsHosted); Assert.False(component.IsDisposed); Assert.False(controller.CleanupStarted);
        fail = false; var second = controller.Request(UiCloseReason.Native); Assert.Null(controller.LastError);
        Assert.True(controller.Poll()); Assert.False(second.IsCompleted);
        controller.CompleteCleanup(); Assert.True(await second);
        Assert.Equal([WindowCloseReason.Native, WindowCloseReason.Native], reasons);
    }

    [Fact]
    public async Task ForcedShutdown_UpgradesPendingAttemptAndDrainsEveryHandlerBeforeCleanupPreservingErrors()
    {
        using var component = new Window(); using var controller = new CoordinatedController(component);
        var first = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var last = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new IOException("drain"); var calls = new List<(string, WindowCloseReason)>();
        component.Closing += reason => { calls.Add(("first", reason)); return first.Task; };
        component.Closing += reason => { calls.Add(("error", reason)); throw failure; };
        component.Closing += reason => { calls.Add(("last", reason)); return last.Task; };
        var request = component.RequestCloseAsync(); Assert.False(controller.Poll());
        var forced = controller.Request(UiCloseReason.Shutdown); Assert.False(controller.Poll()); Assert.Single(calls);
        first.SetResult(false); Assert.False(controller.Poll());
        Assert.Equal([("first", WindowCloseReason.Application), ("error", WindowCloseReason.Shutdown), ("last", WindowCloseReason.Shutdown)], calls);
        Assert.False(controller.CleanupStarted); Assert.False(component.IsDisposed); Assert.False(request.IsCompleted); Assert.False(forced.IsCompleted);
        last.SetResult(false); Assert.Same(failure, Assert.Throws<IOException>(() => controller.Poll()));
        Assert.Same(failure, controller.LastError); Assert.False(component.IsDisposed);
        controller.CompleteCleanup(failure);
        Assert.Same(failure, await Record.ExceptionAsync(() => request)); Assert.Same(failure, await Record.ExceptionAsync(() => forced));
        Assert.True(component.IsDisposed); Assert.False(component.IsHosted); Assert.Equal(3, calls.Count);
    }

    [Fact]
    public async Task CloseWaitCancellation_DoesNotCancelAcceptedDrainAndCleanupFailureCannotReportSuccess()
    {
        using var component = new Window(); using var controller = new CoordinatedController(component);
        var drain = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        component.Closing += _ => { calls++; return drain.Task; };
        using var cancellation = new CancellationTokenSource();
        var canceledWait = component.RequestCloseAsync(cancellation.Token); Assert.False(controller.Poll());
        var joined = component.RequestCloseAsync(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWait);
        Assert.False(joined.IsCompleted); Assert.True(component.IsHosted); Assert.False(controller.CleanupStarted);
        drain.SetResult(true); Assert.True(controller.Poll()); Assert.Equal(1, calls);
        var failure = new IOException("cleanup"); controller.CompleteCleanup(failure);
        Assert.Same(failure, await Record.ExceptionAsync(() => joined)); Assert.True(component.IsDisposed);
    }

    [Fact]
    public void FrameBinding_RetainsOneFrameContentAndUiStateAcrossCaptionThemeResizeAndDetach()
    {
        var component = new Window(); var editor = new TextBox { Value = "retained draft" };
        var scroll = new ScrollViewer().Height(70).Content(new Panel { Height = 500 });
        var body = new VStack().WithChildren(editor, scroll); component.SetContent(body);
        var controller = new Controller(); var titleWrites = new List<string>(); var sizeWrites = new List<Size>();
        using var binding = new WindowComponentBinding(component, controller, titleWrites.Add, sizeWrites.Add);
        var frame = WindowFrame.CreateDefault(component.Title, component); var attachments = 0;
        editor.AttachmentChanged += () => attachments++;
        using var ui = Create(frame); ui.Update(); ui.Focus(editor);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true)); scroll.Offset = 50; ui.Update();
        var caret = editor.CaretIndex; var selection = editor.SelectionLength; var owner = Environment.CurrentManagedThreadId;
        component.HostStateChanged += _ => Assert.Equal(owner, Environment.CurrentManagedThreadId);
        component.Title = "renamed"; component.ClientSize = new(800, 600);
        binding.Synchronize(WindowPresentationState.Maximized, true, new(800.5f, 600.5f));
        ui.SetTheme(Theme.Light); ui.Resize(new UiViewport(500, 400)); ui.Update();
        Assert.Same(frame, ui.Root); Assert.Same(component, frame.Content); Assert.Same(body, component.Child);
        Assert.Single(Descendants(frame).OfType<WindowFrame>()); Assert.Same(editor, body.Items[0]);
        Assert.Equal("retained draft", editor.Value); Assert.True(editor.IsFocused); Assert.Equal(caret, editor.CaretIndex);
        Assert.Equal(selection, editor.SelectionLength); Assert.Equal(50, scroll.Offset); Assert.Equal(1, attachments);
        Assert.Equal(["renamed"], titleWrites); Assert.Equal([new Size(800, 600)], sizeWrites);
        var disconnectedDuringDisposal = false;
        editor.Own(new Callback(() => { Assert.False(component.IsHosted); disconnectedDuringDisposal = true; }));
        binding.Dispose(); Assert.False(component.IsHosted); Assert.False(component.IsDisposed); Assert.Equal(0, controller.Disposals);
        ui.Dispose(); Assert.True(disconnectedDuringDisposal); Assert.True(component.IsDisposed);
    }

    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        foreach (var child in element.Children) foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static UiSession Create(Element root) => new(root, new Surface(), new UiViewport(480, 400));
    private sealed class Declaration<TView>(Action<IViewStyleBuilder<TView>> configure) : IViewStyle<TView> where TView : View
    { public void Configure(IViewStyleBuilder<TView> styles) => configure(styles); }
    private sealed class Controller : IWindowController, IDisposable
    {
        public int Disposals;
        public void Minimize() { }
        public void Maximize() { }
        public void Restore() { }
        public Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default) => throw new Exception("No command expected");
        public void Dispose() => Disposals++;
    }
    // Exercises the real coordinator and adapter without claiming native HostedUiWindow integration.
    private sealed class CoordinatedController : IWindowController, IDisposable
    {
        private readonly Window _component;
        private readonly WindowComponentBinding _binding;
        private readonly UiCloseCoordinator _close = new(() => { });
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CleanupStarted;
        public int Disposals;
        public Exception? LastError => _close.LastError;
        public CoordinatedController(Window component)
        {
            _component = component; _binding = new(component, this, _ => { }, _ => { });
        }
        public Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); return Request(UiCloseReason.Application).WaitAsync(cancellationToken);
        }
        public Task<bool> Request(UiCloseReason reason) => AwaitCleanup(_close.Request(reason));
        private async Task<bool> AwaitCleanup(Task<bool> attempt)
        {
            if (!await attempt.ConfigureAwait(false)) return false;
            await _completion.Task.ConfigureAwait(false); return true;
        }
        public bool Poll() { var ready = _close.Poll(_binding.SnapshotClosingHandlers); if (ready) CleanupStarted = true; return ready; }
        public void CompleteCleanup(Exception? error = null)
        {
            _binding.Dispose(); _component.Dispose(); _close.Finish();
            if (error == null) _completion.TrySetResult(); else _completion.TrySetException(error);
        }
        public void Minimize() => throw new Exception("No command expected");
        public void Maximize() => throw new Exception("No command expected");
        public void Restore() => throw new Exception("No command expected");
        public void Dispose() { Disposals++; _binding.Dispose(); }
    }
    private sealed class NoSessions : IUiWindowSessionFactory
    {
        public int Creates;
        public bool SupportsWindowGraphics(UiWindowGraphicsApi graphicsApi) => true;
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
        { Creates++; throw new Exception("Session must not be allocated"); }
    }
    private sealed class CaptionModel : INotifyPropertyChanged, IDisposable
    {
        private string _caption = "bound";
        private PropertyChangedEventHandler? _changed;
        public int Subscribers, Disposals;
        public string Caption { get => _caption; set { _caption = value; _changed?.Invoke(this, new(nameof(Caption))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } }
        public void Dispose() => Disposals++;
    }
    private sealed class Counter : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
    private sealed class Callback(Action action) : IDisposable { public void Dispose() => action(); }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new Layout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class Layout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
}
