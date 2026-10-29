using TrueMoon.Aether.Editor.Diagnostics;
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
        using (var ui = new UiSession(first, new Surface(), new(1280, 360), clipboard))
        {
            ui.Update(); ui.Update(); Assert.True(first.Scroll.Offset > 0);
            first.Events.Select("2"); ui.Focus(first.Events); ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.C, Control: true)); Assert.Equal(model.Details, clipboard.Text);
            source.Add(DiagnosticConsoleFixture.Record(101, "event 101")); model.Refresh(); ui.Update(); ui.Update();
            Assert.Equal(first.Scroll.Extent - first.Scroll.Bounds.Height, first.Scroll.Offset);
            model.IsOpen = false; ui.Update(); Assert.False(first.IsVisible); model.IsOpen = true; ui.Update(); Assert.True(first.IsVisible); Assert.Equal("2", model.SelectedId);
        }
        Assert.True(first.IsDisposed); Assert.True(first.Events.IsDisposed); Assert.Equal(0, source.Disposals);
        var reads = source.Reads; source.Add(DiagnosticConsoleFixture.Record(102)); model.Refresh(); Assert.Equal(reads + 1, source.Reads);
        using var reopened = new EditorDiagnosticsView(model, new EditorDiagnosticsViewStyle()); using var again = new UiSession(reopened, new Surface(), new(1280, 360)); again.Update();
        Assert.Equal(102, reopened.Events.Items.Count); Assert.Equal("2", reopened.Events.SelectedId); Assert.Equal(0, source.Disposals);
    }

    [Fact]
    public void FailedStyle_DisposesUnpublishedConsoleAndRetainsBorrowedModel()
    {
        using var source = new DiagnosticConsoleFixture(); using var model = source.Model(); EditorDiagnosticsView? created = null;
        var expected = new IOException("style failed");
        var error = Assert.Throws<IOException>(() => new EditorDiagnosticsView(model, new FailingStyle(view => created = view, expected)));
        Assert.Same(expected, error); Assert.NotNull(created); Assert.True(created.IsDisposed); Assert.True(created.Events.IsDisposed);
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
        public void Render(Element root, UiViewport viewport) { }
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
