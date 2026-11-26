using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.ComponentModel;
using System.Reflection;
using SkiaSharp;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Diagnostics;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

public sealed class EditorDiagnosticsViewTests
{
    [Fact]
    public void RefreshFilterAndTheme_PreserveControlsFocusSelectionAndPausedScroll()
    {
        using var source = new DiagnosticConsoleFixture(); source.Add(Enumerable.Range(1, 100).Select(i => DiagnosticConsoleFixture.Record(i, "event " + i)).ToArray());
        using var model = source.Model(new() { RecordsPerRefresh = 128 }); model.IsOpen = true; model.FollowTail = false; model.Refresh();
        using var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var ui = new UiSession(view, new Surface(), new(1280, 360)); ui.Update();
        var tree = view.Events; var detail = view.Details; view.Events.Select("2"); ui.Update(); detail.Select(2, 8); view.Scroll.Offset = 100; ui.Update();
        Assert.Equal(new Thickness(4, 2, 4, 2), view.Search.Padding); Assert.Equal(12f, tree.FontSize);
        ui.Focus(view.Search); var oldText = detail.SelectedText;
        source.Add(DiagnosticConsoleFixture.Record(101, "new row")); model.Refresh(); ui.Update();
        Assert.Same(tree, view.Events); Assert.Same(detail, view.Details); Assert.Equal("2", model.SelectedId);
        Assert.Equal(oldText, detail.SelectedText); Assert.Equal(100, view.Scroll.Offset); Assert.True(view.Search.IsFocused);
        model.Search = "absent"; ui.Update(); Assert.Empty(tree.Items); Assert.Equal("2", model.SelectedId); Assert.Equal(oldText, detail.SelectedText);
        model.Search = ""; ui.Update(); Assert.Equal("2", tree.SelectedId);
        var offset = view.Scroll.Offset; view.ApplyTheme(Theme.Light); ui.Update();
        Assert.Equal(oldText, detail.SelectedText); Assert.Equal(offset, view.Scroll.Offset); Assert.True(view.Search.IsFocused);
    }

    [Fact]
    public void CopyFollowAndReopen_UseRetainedModelAndDisposeOnlyOwnedControls()
    {
        using var source = new DiagnosticConsoleFixture(); source.Add(Enumerable.Range(1, 100).Select(i => DiagnosticConsoleFixture.Record(i, "event " + i)).ToArray());
        using var model = source.Model(new() { RecordsPerRefresh = 128 }); model.IsOpen = true; model.Refresh();
        var clipboard = new Clipboard(); var first = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        TreeView firstEvents;
        using (var ui = new UiSession(first, new Surface(), new(1280, 360), clipboard))
        {
            firstEvents = first.Events;
            ui.Update(); Assert.True(first.Scroll.Offset > 0);
            first.Events.Select("2"); ui.Focus(first.Events); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.C, Control: true)); Assert.Equal(model.Details, clipboard.Text);
            source.Add(DiagnosticConsoleFixture.Record(101, "event 101")); model.Refresh(); ui.Update();
            Assert.Equal(first.Scroll.Extent - first.Scroll.ChildClipBounds.Height, first.Scroll.Offset);
            Assert.False(ui.NeedsUpdate);
            var beforeResize = first.Scroll.Offset;
            ui.Resize(new(1280, 300)); ui.Update();
            Assert.Equal(first.Scroll.Extent - first.Scroll.ChildClipBounds.Height, first.Scroll.Offset);
            Assert.True(first.Scroll.Offset > beforeResize);
            model.IsOpen = false; ui.Update(); Assert.False(first.IsVisible); model.IsOpen = true; ui.Update(); Assert.True(first.IsVisible); Assert.Equal("2", model.SelectedId);
        }
        Assert.True(first.IsDisposed); Assert.True(firstEvents.IsDisposed); Assert.Equal(0, source.Disposals);
        var reads = source.Reads; source.Add(DiagnosticConsoleFixture.Record(102)); model.Refresh(); Assert.Equal(reads + 1, source.Reads);
        using var reopened = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var again = new UiSession(reopened, new Surface(), new(1280, 360)); again.Update();
        Assert.Equal(102, reopened.Events.Items.Count); Assert.Equal("2", reopened.Events.SelectedId); Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public void ModelSubscription_DetachesResynchronizesAndRejectsOldConsoleNotifications()
    {
        using var source = new DiagnosticConsoleFixture();
        source.Add(Enumerable.Range(1, 100).Select(i => DiagnosticConsoleFixture.Record(i, "event " + i)).ToArray());
        using var model = source.Model(new() { RecordsPerRefresh = 128 });
        model.IsOpen = true;
        model.FollowTail = false;
        model.Refresh();
        var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        var firstRoot = new Panel().WithChildren(view);
        using var first = new UiSession(firstRoot, new Surface(), new(1280, 360));
        first.Update();
        var oldHandlers = Subscribers();
        Assert.NotNull(oldHandlers);
        var attachedCount = oldHandlers.GetInvocationList().Length;

        view.Scroll.Offset = 160;
        first.Update();
        Assert.Equal(160, view.Scroll.Offset);
        model.Mode = EditorDiagnosticMode.Health;
        model.Mode = EditorDiagnosticMode.Structured;
        Assert.True(first.NeedsUpdate);
        Assert.True(firstRoot.Items.Remove(view));
        Assert.Null(Subscribers());

        source.Add(DiagnosticConsoleFixture.Record(101, "while detached"));
        model.Refresh();
        model.ExportIncludesText = true;
        Assert.Equal(100, view.Events.Items.Count);
        Assert.Equal("Экспорт без текста", view.Export.Value);

        var secondRoot = new Panel().WithChildren(view);
        using var second = new UiSession(secondRoot, new Surface(), new(1280, 360));
        second.Update();
        Assert.Equal(attachedCount, Subscribers()!.GetInvocationList().Length);
        Assert.Equal(101, view.Events.Items.Count);
        Assert.Equal("Экспорт с текстом", view.Export.Value);
        Assert.Equal(160, view.Scroll.Offset);

        // Mode notifications would reset the paused scroll if an old handler could reach this view.
        oldHandlers(model, new PropertyChangedEventArgs(nameof(model.Mode)));
        first.Update();
        second.Update();
        Assert.Equal(160, view.Scroll.Offset);

        Assert.True(secondRoot.Items.Remove(view));
        Assert.Null(Subscribers());
        secondRoot.Items.Add(view);
        Assert.Equal(attachedCount, Subscribers()!.GetInvocationList().Length);
        model.Mode = EditorDiagnosticMode.Health;
        second.Update();
        Assert.Equal("Health", view.Mode.Value);
        Assert.Equal(0, view.Scroll.Offset);

        second.Dispose();
        Assert.Null(Subscribers());

        PropertyChangedEventHandler? Subscribers() => (PropertyChangedEventHandler?)typeof(Bindable)
            .GetField(nameof(INotifyPropertyChanged.PropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(model);
    }

    [Fact]
    public void DataContextSwap_RetainsFocusedFilterAndClearTargetsCurrentModelAfterReattach()
    {
        using var previousSource = new DiagnosticConsoleFixture();
        using var nextSource = new DiagnosticConsoleFixture();
        previousSource.Add(DiagnosticConsoleFixture.Record(1, "previous"));
        nextSource.Add(DiagnosticConsoleFixture.Record(101, "replacement"));
        using var previous = previousSource.Model();
        using var next = nextSource.Model();
        previous.IsOpen = next.IsOpen = true;
        previous.FollowTail = next.FollowTail = false;
        previous.Refresh();
        next.Refresh();
        var view = new EditorDiagnosticsView(previous, new EditorDiagnosticsViewStyle());
        var root = new Panel().WithChildren(view);
        using var ui = new UiSession(root, new Surface(), new(1280, 360));
        ui.Update();
        var search = view.Search;
        var events = view.Events;
        Assert.Same(search, view.GetRequiredPart<TextBox>("Search"));
        Assert.Same(events, view.GetRequiredPart<TreeView>("Events"));
        Assert.Same(view.Clear, view.GetRequiredPart<Button>("ClearButton"));
        Assert.Equal("1", Assert.Single(events.Items).Id);
        ui.Focus(search);
        Assert.True(search.IsFocused);

        previous.Mode = EditorDiagnosticMode.Health;
        view.DataContext = next;
        ui.Update();
        Assert.True(search.IsFocused);
        Assert.Equal("Structured", view.Mode.Value);
        ui.HandleInput(new(InputKind.Text, Text: "replacement"));
        ui.Update();
        Assert.Equal("", previous.Search);
        Assert.Equal("replacement", next.Search);
        Assert.Equal("101", Assert.Single(events.Items).Id);
        ui.Focus(events);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Down));
        ui.Update();
        Assert.Equal("101", next.SelectedId);
        Assert.Null(previous.SelectedId);

        Assert.True(root.Items.Remove(view));
        next.Mode = EditorDiagnosticMode.Health;
        root.Items.Add(view);
        ui.Update();
        Assert.Same(events, view.Events);
        Assert.False(view.Clear.IsEnabled);
        next.Mode = EditorDiagnosticMode.Structured;
        ui.Update();
        Assert.True(view.Clear.IsEnabled);
        Assert.Single(events.Items);
        ui.Focus(view.Clear);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
        ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter));
        ui.Update();
        Assert.Empty(events.Items);
        Assert.Empty(next.Entries);
        Assert.Single(previous.Entries);
        Assert.Single(nextSource.Records);
    }

    [Theory]
    [InlineData(false, 215)]
    [InlineData(true, 360)]
    public void Console_ResizeKeepsEqualColumnsAndClearUsesModeAdmission(bool raster, int narrowHeight)
    {
        using var source = new DiagnosticConsoleFixture();
        source.Add(DiagnosticConsoleFixture.Record(1, "entry"));
        using var model = source.Model();
        model.IsOpen = true;
        model.FollowTail = false;
        model.Refresh();
        var view = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle());
        using var ui = raster
            ? UiSession.Create(view, new SkiaRasterRenderBackend(), new RasterUiTarget(), new(1280, 360))
            : new UiSession(view, new Surface(), new(1280, 360));
        AssertGeometry(1280, 360);
        AssertGeometry(720, narrowHeight);
        Assert.Single(view.Events.Items);

        ui.Resize(new(1280, 360));
        model.Mode = EditorDiagnosticMode.Health;
        ui.Update();
        Assert.False(view.Clear.IsEnabled);
        Assert.False(ui.HandleInput(new(InputKind.PointerDown, view.Clear.Bounds.X + 1, view.Clear.Bounds.Y + 1)).Handled);
        model.Mode = EditorDiagnosticMode.Structured;
        ui.Update();
        Assert.True(view.Clear.IsEnabled);
        ui.Focus(view.Clear);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Enter));
        ui.HandleInput(new(InputKind.KeyUp, Key: UiKey.Enter));
        ui.Update();
        Assert.Empty(view.Events.Items);
        Assert.Empty(model.Entries);

        void AssertGeometry(int width, int height)
        {
            ui.Resize(new(width, height));
            ui.Update();
            var content = view.Child!.Bounds;
            var left = view.Scroll.Bounds;
            var right = view.Details.Bounds;
            Assert.Equal(content.X, left.X);
            Assert.Equal((content.Width - 8) / 2, left.Width);
            Assert.Equal(left.X + left.Width + 8, right.X);
            Assert.Equal(left.Width, right.Width);
            Assert.Equal(left.Y, right.Y);
            Assert.Equal(left.Height, right.Height);
            Assert.True(left.Height > 0);
            Assert.Equal(view.Summary.Bounds.Y + view.Summary.Bounds.Height, left.Y);
            Assert.Equal(left.Y + left.Height, view.IncludeText.Bounds.Y);

            if (!raster) return;
            using var image = ui.SnapshotRasterImage();
            using var pixels = SKBitmap.FromImage(image);
            var y = (int)(right.Y + right.Height / 2);
            var gap = pixels.GetPixel((int)right.X - 4, y);
            var details = pixels.GetPixel((int)(right.X + right.Width * 0.75f), y);
            Assert.NotEqual(gap, details);
        }
    }

    [Fact]
    public void FailedStyle_DisposesUnpublishedContentAndRetainsBorrowedModel()
    {
        using var source = new DiagnosticConsoleFixture(); using var model = source.Model();
        TreeView? tree = null;
        var expected = new IOException("style failed");
        using var draft = new EditorDiagnosticsView(model, new FailingStyle(view => tree = view.Events, expected));
        using var surface = new Surface();
        var error = Assert.Throws<IOException>(() => new UiSession(draft, surface, new(1280, 800)));
        Assert.Same(expected, error);
        Assert.False(draft.IsDisposed);
        Assert.False(draft.IsAttached);
        Assert.Null(draft.Child);
        Assert.NotNull(tree); Assert.True(tree.IsDisposed);
        source.Add(DiagnosticConsoleFixture.Record(1)); model.Refresh(); Assert.Single(model.Entries); Assert.Equal(0, source.Disposals);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShellConsole_PreservesSceneSurfaceAndReservesSpaceAboveFooter(bool overlay)
    {
        using var source = new DiagnosticConsoleFixture(); using var diagnostics = source.Model();
        using var model = new EditorViewModel(new(EditorScene.Demo()), new((_, _) => Task.CompletedTask, CancellationToken.None));
        using var view = new EditorView(model, new EditorViewStyle(), null, null, overlay, null, null, null,
            new Factory<EditorDiagnosticsView>(() => new(diagnostics, new EditorDiagnosticsViewStyle())));
        using var ui = new UiSession(view, new Surface(), new(1280, 720)); ui.Update();
        var layout = Assert.IsType<EditorLayout>(Assert.IsType<Panel>(view.Overlays.Content).Children.Single()); var original = layout.Viewport.Bounds;
        diagnostics.IsOpen = true; ui.Update(); var console = view.Diagnostics!;
        Assert.True(console.Bounds.Height > 100); Assert.Equal(layout.Status.Bounds.Y, console.Bounds.Y + console.Bounds.Height);
        Assert.True(layout.Hierarchy.Bounds.Y + layout.Hierarchy.Bounds.Height <= console.Bounds.Y);
        if (overlay) Assert.Equal(original, layout.Viewport.Bounds);
        else Assert.True(layout.Viewport.Bounds.Y + layout.Viewport.Bounds.Height <= console.Bounds.Y);
        diagnostics.IsOpen = false; ui.Update(); Assert.Equal(original, layout.Viewport.Bounds); Assert.False(console.IsDisposed);
    }

    private sealed class FailingStyle(Action<EditorDiagnosticsView> capture, Exception error) : IViewStyle<EditorDiagnosticsView>
    {
        public void Configure(IViewStyleBuilder<EditorDiagnosticsView> styles)
        { styles.Part(view => { capture(view); return view.Details; }); throw error; }
    }
    private sealed class Clipboard : IUiClipboard
    { public string? Text; public string? GetText() => Text; public void SetText(string text) => Text = text; }
    internal sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new Layout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport, UiPresenters? presenters = null) { }
        public void Dispose() { }
    }
    private sealed class Layout : ITextLayoutService, IRichTextLayoutService
    {
        public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize);
        public IRichTextLayout CreateRichTextLayout(RichTextDocument document, RichTextStyle defaults, float? wrapWidth, RichTextAlignment alignment) => new RichLayout(document.Text);
    }
    private sealed class RichLayout(string text) : IRichTextLayout
    {
        public Size Size => new(400, Math.Max(1, text.Count(character => character == '\n') + 1) * 16);
        public int HitTest(float x, float y) => Math.Clamp((int)(x / 6), 0, text.Length);
        public Rect GetCaretBounds(int index) => new(index * 6, 0, 1, 16);
        public int MoveVertical(int index, int direction, float preferredX) => index;
        public int GetLineBoundary(int index, bool end) => end ? text.Length : 0;
        public void Dispose() { }
    }
}
