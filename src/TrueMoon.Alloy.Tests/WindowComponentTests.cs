using System.ComponentModel;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowComponentTests
{
    [Fact]
    public void Defaults_AreDetachedAndClientSizeIsIndependentFromElementLayout()
    {
        using var window = new Window();
        Assert.Equal("TrueMoon UI", window.Title);
        Assert.Equal(new Size(960, 540), window.ClientSize);
        Assert.Equal(WindowPresentationState.Normal, window.State);
        Assert.Equal(default, window.ActualClientSize);
        Assert.False(window.IsActive); Assert.False(window.IsHosted);
        Assert.Null(window.GetContent()); Assert.Empty(window.Children);
        Assert.True(float.IsNaN(window.Width)); Assert.True(float.IsNaN(window.Height));

        window.Width = 123; window.Height = 45;
        Assert.Equal(new Size(960, 540), window.ClientSize);
        window.ClientSize = new(1200, 700);
        Assert.Equal(123, window.Width); Assert.Equal(45, window.Height);
        Assert.Empty(window.Children);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedContent_UsesOptionalDataOnceAndBorrowsObservableModel(bool withData)
    {
        var model = new ScreenModel(); var window = new ScreenWindow(withData ? model : null);
        IView<ScreenModel> typed = window;
        Assert.Same(withData ? model : null, typed.DataContext);
        Assert.Same(window.Body, typed.GetContent());
        Assert.Equal(1, window.Builds); Assert.Equal(0, model.Subscribers);
        using var ui = Create(window); ui.Update();
        Assert.Equal(withData ? "initial" : "unbound", window.Label.Value);
        Assert.Equal(withData ? 3 : 0, model.Subscribers);
        if (withData)
        {
            model.Text = "model edit"; ui.Update();
            Assert.Equal("model edit", window.Editor.Value); Assert.Equal("model edit", window.Label.Value);
            model.Caption = "new caption"; ui.Update(); Assert.Equal("new caption", window.Title);
        }
        ui.Dispose();
        Assert.True(window.IsDisposed); Assert.True(window.Body.IsDisposed);
        Assert.Equal(0, model.Subscribers); Assert.Equal(0, model.Disposals); Assert.Equal(1, window.Builds);
        model.Text = "still borrowed"; Assert.Equal("still borrowed", model.Text);
    }

    [Fact]
    public void TypedContent_AcceptsPlainDataAndRejectsInvalidFactoryResultsWithoutReplacingContent()
    {
        using var window = new Window<string>(); var calls = 0;
        var content = new Text();
        window.Content("plain", data => { calls++; content.Value = data!; return content; });
        Assert.Equal("plain", window.DataContext); Assert.Equal("plain", content.Value);
        Assert.Same(content, window.GetContent()); Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => window.Content("other", null!));
        Assert.Throws<ArgumentException>(() => window.Content("other", _ => new object()));
        var failure = new IOException("compose");
        Assert.Same(failure, Assert.Throws<IOException>(() => window.Content("other", _ => throw failure)));
        Assert.Same(content, window.GetContent()); Assert.Same(window, content.Parent); Assert.False(content.IsDisposed);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 32768)]
    [InlineData(32768, 1)]
    [InlineData(32768, 32768)]
    [InlineData(1024, 768)]
    public void ClientSize_AcceptsExactBoundsAndRepresentativeInterior(int width, int height)
    {
        using var window = new Window(); var notifications = new List<object>();
        window.PropertyChanged += (_, property) => notifications.Add(property);
        window.ClientSize = new(width, height);
        Assert.Equal(new Size(width, height), window.ClientSize);
        Assert.Same(Window.ClientSizeProperty, Assert.Single(notifications));
        window.Set(Window.ClientSizeProperty, new(width, height));
        Assert.Single(notifications); Assert.Equal(default, window.ActualClientSize);
    }

    public static IEnumerable<object[]> InvalidClientSizes()
    {
        float[] invalid = [0, -1, 0.5f, 1.5f, 32768.5f, 32769, 32770, float.NaN, float.PositiveInfinity, float.NegativeInfinity];
        foreach (var value in invalid)
        {
            yield return [value, 200f];
            yield return [200f, value];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidClientSizes))]
    public void ClientSize_InvalidPartitionsRejectBeforeChangingValueOrNotifying(float width, float height)
    {
        using var window = new Window { ClientSize = new(600, 400) }; var notifications = 0;
        window.PropertyChanged += (_, _) => notifications++;
        Assert.Throws<ArgumentOutOfRangeException>(() => window.ClientSize = new(width, height));
        Assert.Equal(new Size(600, 400), window.ClientSize); Assert.Equal(0, notifications);
    }

    [Fact]
    public void TypedPropertyAndStyleValidation_CannotBypassTitleOrClientSizeGuards()
    {
        using var window = new Window { Title = "valid", ClientSize = new(800, 600) };
        var notifications = 0; window.PropertyChanged += (_, _) => notifications++;
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Title = null!);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Set(Window.TitleProperty, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(Window.TitleProperty, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.Set(Window.ClientSizeProperty, new(5.5f, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(Window.ClientSizeProperty, new(100, 0)));
        Assert.Equal("valid", window.Title); Assert.Equal(new Size(800, 600), window.ClientSize); Assert.Equal(0, notifications);
        window.Title = ""; Assert.Equal("", window.Title); Assert.Equal(1, notifications);
    }

    [Fact]
    public void FluentWindowConfiguration_PreservesDerivedIdentityAndUsesTheSameValidation()
    {
        using var window = new ScreenWindow(null);
        ScreenWindow configured = window.WithTitle("fluent").WithClientSize(1200, 800);
        Assert.Same(window, configured); Assert.Equal("fluent", configured.Title); Assert.Equal(new Size(1200, 800), configured.ClientSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => window.WithTitle(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => window.WithClientSize(1200.5f, 800));
        Assert.Equal("fluent", window.Title); Assert.Equal(new Size(1200, 800), window.ClientSize);
        Assert.Throws<ArgumentNullException>(() => ((Window)null!).WithTitle("title"));
        Assert.Throws<ArgumentNullException>(() => ((Window)null!).WithClientSize(1, 1));
        Assert.Equal(1, window.Builds);
    }

    [Fact]
    public async Task Commands_ForwardOnceAndPreserveHostTaskTokenErrorsAndObservedState()
    {
        using var window = new Window(); var controller = new Controller();
        using var connection = window.AttachHost(controller);
        using var cancellation = new CancellationTokenSource();
        window.Minimize(); window.Maximize(); window.Restore();
        var close = window.RequestCloseAsync(cancellation.Token);
        Assert.Same(controller.Close.Task, close); Assert.Equal(cancellation.Token, controller.Token);
        Assert.Equal(["minimize", "maximize", "restore", "close"], controller.Calls);
        Assert.False(close.IsCompleted); Assert.Equal(WindowPresentationState.Normal, window.State);
        Assert.Equal(default, window.ActualClientSize); Assert.False(window.IsActive);
        controller.Close.SetResult(false); Assert.False(await close);
        controller.Failure = new NotSupportedException("fixed window");
        Assert.Same(controller.Failure, Assert.Throws<NotSupportedException>(window.Maximize));
        Assert.Equal(WindowPresentationState.Normal, window.State); Assert.Equal(0, controller.Disposals);
        var count = controller.Calls.Count;
        Assert.Throws<OperationCanceledException>(() => { _ = window.RequestCloseAsync(new CancellationToken(true)); });
        Assert.Equal(count, controller.Calls.Count);
    }

    [Theory]
    [InlineData("minimize")]
    [InlineData("maximize")]
    [InlineData("restore")]
    [InlineData("close")]
    public void Commands_DetachedDisconnectedAndDisposedNeverReachController(string command)
    {
        using var window = new Window(); var controller = new Controller(); var action = Command(window, command);
        Assert.Throws<InvalidOperationException>(action);
        var connection = window.AttachHost(controller); connection.Dispose();
        Assert.Throws<InvalidOperationException>(action); Assert.Empty(controller.Calls); Assert.Equal(0, controller.Disposals);
        window.Dispose(); Assert.Throws<ObjectDisposedException>(action);
        connection.Dispose(); Assert.Empty(controller.Calls); Assert.Equal(0, controller.Disposals);
    }

    [Fact]
    public void AttachHost_RejectsNullParentedAttachedDisposedAndAlreadyHostedRootsWithoutTakingOwnership()
    {
        var controller = new Controller(); using var window = new Window();
        Assert.Throws<ArgumentNullException>(() => window.AttachHost(null!)); Assert.False(window.IsHosted);
        using var owner = new Panel(); owner.Add(window);
        Assert.Throws<InvalidOperationException>(() => window.AttachHost(controller));
        Assert.Same(owner, window.Parent); Assert.False(window.IsDisposed); Assert.False(window.IsHosted);
        owner.Items.Remove(window);
        using (var ui = Create(window))
        {
            Assert.Throws<InvalidOperationException>(() => window.AttachHost(controller));
            Assert.True(window.IsAttached); Assert.False(window.IsHosted);
        }
        Assert.Throws<ObjectDisposedException>(() => window.AttachHost(controller));
        using var fresh = new Window(); using var connection = fresh.AttachHost(controller);
        Assert.Throws<InvalidOperationException>(() => fresh.AttachHost(new Controller()));
        Assert.True(fresh.IsHosted); fresh.Minimize(); Assert.Equal(["minimize"], controller.Calls);
        Assert.Equal(0, controller.Disposals);
    }

    [Fact]
    public void ConnectionDetach_IsIdempotentAndStaleLeaseCannotChangeRehostedWindow()
    {
        using var window = new Window(); var oldController = new Controller(); var newController = new Controller();
        var first = window.AttachHost(oldController); var notifications = 0;
        first.Synchronize(WindowPresentationState.Maximized, true, new(1250.5f, 700.25f));
        window.HostStateChanged += current =>
        {
            Assert.Same(window, current); notifications++;
            if (!current.IsHosted) Assert.False(current.IsActive);
        };
        first.Dispose(); first.Dispose();
        Assert.False(window.IsHosted); Assert.False(window.IsActive); Assert.Equal(1, notifications);
        Assert.Equal(WindowPresentationState.Maximized, window.State); Assert.Equal(new Size(1250.5f, 700.25f), window.ActualClientSize);
        using var second = window.AttachHost(newController);
        Assert.Throws<ObjectDisposedException>(first.VerifyAccess); second.VerifyAccess();
        Assert.Throws<ObjectDisposedException>(() => first.Synchronize(WindowPresentationState.Minimized, false, default));
        Assert.Throws<ObjectDisposedException>(() => first.SnapshotClosingHandlers());
        first.Dispose(); Assert.True(window.IsHosted); Assert.Equal(1, notifications);
        second.Synchronize(WindowPresentationState.Normal, true, new(400, 300)); window.Restore();
        Assert.Equal(2, notifications); Assert.Empty(oldController.Calls); Assert.Equal(["restore"], newController.Calls);
        Assert.Equal(0, oldController.Disposals); Assert.Equal(0, newController.Disposals);
    }

    [Fact]
    public void HostObservation_CommitsAllFieldsBeforeNotificationAndSkipsIdenticalObservation()
    {
        using var window = new Window(); using var connection = window.AttachHost(new Controller());
        var observed = new List<(WindowPresentationState, bool, Size)>(); var owner = Environment.CurrentManagedThreadId;
        window.HostStateChanged += current =>
        {
            Assert.Equal(owner, Environment.CurrentManagedThreadId); Assert.Same(window, current);
            observed.Add((current.State, current.IsActive, current.ActualClientSize));
        };
        connection.Synchronize(WindowPresentationState.Normal, false, default); Assert.Empty(observed);
        connection.Synchronize(WindowPresentationState.Maximized, true, new(1024.5f, 700.25f));
        Assert.Equal((WindowPresentationState.Maximized, true, new Size(1024.5f, 700.25f)), Assert.Single(observed));
        connection.Synchronize(WindowPresentationState.Maximized, true, new(1024.5f, 700.25f)); Assert.Single(observed);
        connection.Synchronize(WindowPresentationState.Minimized, false, default);
        Assert.Equal((WindowPresentationState.Minimized, false, default(Size)), observed[1]);
        Assert.Equal(new Size(960, 540), window.ClientSize);
        connection.Dispose();
        Assert.Equal(3, observed.Count); Assert.False(window.IsHosted); Assert.False(window.IsActive);
    }

    public static IEnumerable<object[]> InvalidObservations()
    {
        yield return [(WindowPresentationState)(-1), new Size(400, 300)];
        yield return [(WindowPresentationState)3, new Size(400, 300)];
        foreach (var value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            yield return [WindowPresentationState.Minimized, new Size(value, 300)];
            yield return [WindowPresentationState.Minimized, new Size(400, value)];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidObservations))]
    public void HostObservation_InvalidValuesRejectAtomically(WindowPresentationState state, Size size)
    {
        using var window = new Window(); using var connection = window.AttachHost(new Controller());
        connection.Synchronize(WindowPresentationState.Maximized, true, new(800, 600));
        var notifications = 0; window.HostStateChanged += _ => notifications++;
        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Synchronize(state, false, size));
        Assert.Equal(WindowPresentationState.Maximized, window.State); Assert.True(window.IsActive);
        Assert.Equal(new Size(800, 600), window.ActualClientSize); Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(0.5f, 32768.5f)]
    public void ActualClientSize_AcceptsSuspensionAndFractionalValuesIndependentlyOfRequestLimits(float width, float height)
    {
        using var window = new Window(); using var connection = window.AttachHost(new Controller());
        connection.Synchronize(WindowPresentationState.Minimized, false, new(width, height));
        Assert.Equal(new Size(width, height), window.ActualClientSize); Assert.Equal(new Size(960, 540), window.ClientSize);
        Assert.Equal(WindowPresentationState.Minimized, window.State);
    }

    [Fact]
    public void HostStateObserverFailure_PropagatesAfterCommitWithoutForcingDuplicateNotificationOnRetry()
    {
        using var window = new Window(); using var connection = window.AttachHost(new Controller());
        var failure = new IOException("observer");
        Action<Window> throwing = _ => throw failure; window.HostStateChanged += throwing;
        Assert.Same(failure, Assert.Throws<IOException>(() => connection.Synchronize(WindowPresentationState.Maximized, true, new(1000, 700))));
        Assert.Equal(WindowPresentationState.Maximized, window.State); Assert.True(window.IsActive);
        Assert.Equal(new Size(1000, 700), window.ActualClientSize); Assert.True(window.IsHosted);
        window.HostStateChanged -= throwing; var calls = 0; window.HostStateChanged += _ => calls++;
        connection.Synchronize(WindowPresentationState.Maximized, true, new(1000, 700)); Assert.Equal(0, calls);
        connection.Dispose(); Assert.Equal(1, calls); Assert.False(window.IsHosted); Assert.False(window.IsActive);
    }

    [Fact]
    public void AttachedCommandsPropertiesSnapshotsAndDisconnectRejectWorkerAccessWithoutPublishing()
    {
        var window = new Window(); var controller = new Controller(); using var connection = window.AttachHost(controller);
        using var ui = Create(window); ui.Update(); var notifications = 0; var owner = Environment.CurrentManagedThreadId;
        window.HostStateChanged += _ => { Assert.Equal(owner, Environment.CurrentManagedThreadId); notifications++; };
        Action[] operations = [window.Minimize, window.Maximize, window.Restore, () => window.RequestCloseAsync(),
            () => window.Title = "worker", () => window.ClientSize = new(200, 100),
            () => connection.Synchronize(WindowPresentationState.Maximized, true, new(200, 100)),
            () => connection.SnapshotClosingHandlers(), connection.VerifyAccess, connection.Dispose];
        Exception?[] errors = new Exception?[operations.Length];
        var worker = new Thread(() => { for (var i = 0; i < operations.Length; i++) errors[i] = Record.Exception(operations[i]); });
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.All(errors, error => Assert.IsType<InvalidOperationException>(error));
        Assert.Empty(controller.Calls); Assert.Equal(0, notifications); Assert.True(window.IsHosted);
        Assert.Equal("TrueMoon UI", window.Title); Assert.Equal(new Size(960, 540), window.ClientSize);
        connection.Synchronize(WindowPresentationState.Maximized, true, new(200, 100));
        Assert.Equal(1, notifications); connection.Dispose(); Assert.Equal(2, notifications);
        Assert.Equal(0, controller.Disposals);
    }

    [Fact]
    public async Task ClosingSnapshot_CopiesDelegatesAndPreservesOrderReasonsTaskAndFailure()
    {
        using var window = new Window(); using var connection = window.AttachHost(new Controller());
        Assert.Empty(connection.SnapshotClosingHandlers());
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<(string, WindowCloseReason)>(); var failure = new IOException("save");
        Func<WindowCloseReason, Task<bool>> first = reason => { calls.Add(("first", reason)); return gate.Task; };
        Func<WindowCloseReason, Task<bool>> second = reason => { calls.Add(("second", reason)); return Task.FromException<bool>(failure); };
        window.Closing += first; window.Closing += second;
        var snapshot = connection.SnapshotClosingHandlers();
        Assert.Equal(2, snapshot.Length); Assert.Same(first, snapshot[0]); Assert.Same(second, snapshot[1]);
        window.Closing -= second; window.Closing += first;
        var next = connection.SnapshotClosingHandlers(); Assert.NotSame(snapshot, next);
        Assert.Equal([first, first], next); Assert.Equal([first, second], snapshot);
        next[0] = second; Assert.Same(first, connection.SnapshotClosingHandlers()[0]);
        var task = snapshot[0](WindowCloseReason.Application); Assert.Same(gate.Task, task); Assert.False(task.IsCompleted);
        gate.SetResult(false); Assert.False(await task);
        Assert.Same(failure, await Record.ExceptionAsync(() => snapshot[1](WindowCloseReason.Shutdown)));
        Assert.Equal([("first", WindowCloseReason.Application), ("second", WindowCloseReason.Shutdown)], calls);
    }

    [Fact]
    public void TypedWindowStyleAndHostUpdates_PreserveContentFocusDraftSelectionScrollAndBindings()
    {
        var model = new ScreenModel(); var window = new ScreenWindow(model); var controller = new Controller();
        using var connection = window.AttachHost(controller);
        window.Nested.WithViewStyle(new Declaration<NestedView>(style => style.Role(EditorRole).FontSize(27)));
        window.WithViewStyle(new Declaration<ScreenWindow>(style =>
        {
            style.Root().Set(Window.TitleProperty, "styled caption").Set(Window.ClientSizeProperty, theme =>
                ReferenceEquals(theme, Theme.Light) ? new Size(900, 600) : new Size(800, 500));
            style.Role(EditorRole).FontSize(22);
        }));
        var content = window.Child; var scrollContent = window.Scroll.Child; var attachments = 0;
        window.Editor.AttachmentChanged += () => attachments++;
        using var ui = Create(window); ui.Update(); ui.Focus(window.Editor);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.HandleInput(new(InputKind.Text, Text: "retained draft")); ui.Update();
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        window.Scroll.Offset = 60; ui.Update();
        var caret = window.Editor.CaretIndex; var selection = window.Editor.SelectionLength;
        Assert.Equal("retained draft", model.Text); Assert.Equal(3, model.Subscribers); Assert.Equal(1, attachments);
        Assert.Equal("caption", window.Title); Assert.Equal(new Size(800, 500), window.ClientSize);
        Assert.Equal(22, window.Editor.FontSize); Assert.Equal(27, window.Nested.Editor.FontSize);
        connection.Synchronize(WindowPresentationState.Maximized, true, new(1000.5f, 800));
        ui.SetTheme(Theme.Light); ui.Update();
        Assert.Equal(new Size(900, 600), window.ClientSize); Assert.Equal(new Size(1000.5f, 800), window.ActualClientSize);
        window.WithViewStyle(new Declaration<ScreenWindow>(style => style.Root().Set(Window.TitleProperty, "replacement")));
        window.WithViewStyle<ScreenWindow>(null); ui.Update();
        Assert.Equal(new Size(960, 540), window.ClientSize); Assert.Equal("caption", window.Title);
        Assert.Same(content, window.Child); Assert.Same(scrollContent, window.Scroll.Child); Assert.Same(window.Editor, window.Body.Items[1]);
        Assert.True(window.Editor.IsFocused); Assert.Equal("retained draft", window.Editor.Value); Assert.Equal("retained draft", window.Label.Value);
        Assert.Equal(caret, window.Editor.CaretIndex); Assert.Equal(selection, window.Editor.SelectionLength); Assert.Equal(60, window.Scroll.Offset);
        Assert.Equal(1, window.Builds); Assert.Equal(1, attachments); Assert.Equal(3, model.Adds); Assert.Equal(0, model.Removes);
        Assert.DoesNotContain(window.Children, element => element is WindowFrame); Assert.Empty(controller.Calls);
        connection.Dispose(); ui.Dispose();
        Assert.Equal(0, model.Subscribers); Assert.Equal(3, model.Removes); Assert.Equal(0, model.Disposals); Assert.Equal(0, controller.Disposals);
    }

    [Fact]
    public void Disposal_ReleasesOwnedTreeOnceAndClearsConnectionWithoutDisposingBorrowedResources()
    {
        var model = new ScreenModel(); var window = new ScreenWindow(model); var controller = new Controller();
        var owned = new Counter(); window.Body.Own(owned);
        var connection = window.AttachHost(controller); var hostEvents = 0;
        window.HostStateChanged += _ => hostEvents++;
        window.Closing += _ => Task.FromResult(true);
        using var ui = Create(window); ui.Update();
        ui.Dispose(); window.Dispose(); connection.Dispose();
        Assert.True(window.IsDisposed); Assert.True(window.Body.IsDisposed); Assert.True(window.Editor.IsDisposed);
        Assert.False(window.IsHosted); Assert.Equal(0, hostEvents); Assert.Equal(1, owned.Disposals);
        Assert.Equal(0, controller.Disposals); Assert.Equal(0, model.Disposals); Assert.Equal(0, model.Subscribers);
        Assert.Throws<ObjectDisposedException>(() => connection.SnapshotClosingHandlers());
        Assert.Throws<ObjectDisposedException>(() => connection.Synchronize(WindowPresentationState.Normal, true, new(1, 1)));
    }

    private static UiSession Create(Element root) => new(root, new Surface(), new UiViewport(480, 400));
    private static Action Command(Window window, string command) => command switch
    {
        "minimize" => window.Minimize, "maximize" => window.Maximize, "restore" => window.Restore,
        "close" => () => window.RequestCloseAsync(), _ => throw new ArgumentOutOfRangeException(nameof(command))
    };
    private static readonly ViewStyleRole<TextBox> EditorRole = new("window-editor");
    private sealed class Declaration<TView>(Action<IViewStyleBuilder<TView>> configure) : IViewStyle<TView> where TView : View
    { public void Configure(IViewStyleBuilder<TView> styles) => configure(styles); }
    private sealed class NestedView : View
    {
        public TextBox Editor { get; } = new TextBox().WithRole(EditorRole);
        public NestedView() => Content(() => Editor);
    }
    private sealed class ScreenWindow : Window<ScreenModel>
    {
        public Text Label { get; } = new("unbound");
        public TextBox Editor { get; } = new TextBox().WithRole(EditorRole);
        public ScrollViewer Scroll { get; } = new ScrollViewer().Height(80).Content(new Panel { Width = 100, Height = 500 });
        public NestedView Nested { get; } = new();
        public VStack Body { get; private set; } = null!;
        public int Builds { get; private set; }
        public ScreenWindow(ScreenModel? model) => Content(model, data =>
        {
            Builds++;
            if (data != null)
            {
                Label.BindText(data, item => item.Text); Editor.BindTwoWay(TextBox.ValueProperty, data, item => item.Text);
                this.Bind(TitleProperty, data, item => item.Caption);
            }
            return Body = new VStack().WithChildren(Label, Editor, Scroll, Nested);
        });
    }
    private sealed class ScreenModel : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? _changed;
        private string _text = "initial", _caption = "caption";
        public int Adds, Removes, Disposals;
        public int Subscribers => Adds - Removes;
        public string Text { get => _text; set { _text = value; _changed?.Invoke(this, new(nameof(Text))); } }
        public string Caption { get => _caption; set { _caption = value; _changed?.Invoke(this, new(nameof(Caption))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Adds++; _changed += value; } remove { Removes++; _changed -= value; } }
        public void Dispose() => Disposals++;
    }
    private sealed class Controller : IWindowController, IDisposable
    {
        public List<string> Calls { get; } = [];
        public TaskCompletionSource<bool> Close { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token;
        public NotSupportedException? Failure;
        public int Disposals;
        public void Minimize() => Calls.Add("minimize");
        public void Maximize() { if (Failure != null) throw Failure; Calls.Add("maximize"); }
        public void Restore() => Calls.Add("restore");
        public Task<bool> RequestCloseAsync(CancellationToken cancellationToken = default) { Calls.Add("close"); Token = cancellationToken; return Close.Task; }
        public void Dispose() => Disposals++;
    }
    private sealed class Counter : IDisposable { public int Disposals; public void Dispose() => Disposals++; }
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
