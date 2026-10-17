using System.ComponentModel;
using SkiaSharp;
using Topten.RichTextKit;
using TrueMoon.Argentis;
using TrueMoon.Alloy.Rendering.Skia;
using Xunit;
using Color = TrueMoon.Argentis.Color;

namespace TrueMoon.Alloy.Tests;

public sealed class RichTextEditorTests
{
    private static string Font(string name = "NotoSans-Regular.ttf") => Path.Combine(AppContext.BaseDirectory, "Assets/Fonts", name);
    private static SkiaFontCatalog Catalog() => SkiaFontCatalog.Load("Fixed", new Dictionary<string, string> { ["Fixed"] = Font() });
    private static UiSession Session(Element root, int width = 260, int height = 130, IUiClipboard? clipboard = null)
        => UiSession.Create(root, new SkiaRasterRenderBackend(Catalog()), new RasterUiTarget(), new(width, height), clipboard);
    private static void Key(UiSession ui, UiKey key, bool control = false, bool shift = false)
        => ui.HandleInput(new(InputKind.KeyDown, Key: key, Control: control, Shift: shift));

    [Fact]
    public void Document_SnapshotsRunsAndCoalescesWithoutLosingStyles()
    {
        var style = new RichTextStyle { Bold = true };
        var source = new List<RichTextRun> { new("ab", style), new("cd", style), new("", style), new("e") };
        var document = new RichTextDocument(source); source.Clear();
        Assert.Equal("abcde", document.Text);
        Assert.Equal(new[] { "abcd", "e" }, document.Runs.Select(run => run.Text));
        Assert.True(document.Runs[0].Style.Bold);
        Assert.Throws<NotSupportedException>(() => ((IList<RichTextRun>)document.Runs).Clear());
    }

    [Theory]
    [InlineData(0xd800, false)]
    [InlineData(0xdc00, false)]
    [InlineData(0xd800, true)]
    public void InvalidUnicode_IsRejectedBeforeEditorOrHistoryChanges(int surrogate, bool surround)
    {
        var invalid = surround ? "x" + (char)surrogate + "y" : new string((char)surrogate, 1);
        using var editor = new RichTextEditor { Text = "valid" };
        editor.SelectAll(); editor.InsertText("edited");
        var document = editor.Document;
        Assert.Throws<ArgumentException>(() => editor.InsertText(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Text = invalid);
        Assert.Same(document, editor.Document); Assert.Equal("edited", editor.Text);
        Assert.True(editor.Undo()); Assert.Equal("valid", editor.Text);
    }

    [Fact]
    public void Text_NormalizesLineEndingsAndPropertyNotificationsSeeConsistentDocument()
    {
        using var editor = new RichTextEditor();
        editor.PropertyChanged += (_, _) => Assert.Equal(editor.Document.Text, editor.Text);
        editor.Text = "a\r\nb\rc\n";
        Assert.Equal("a\nb\nc\n", editor.Text);
        editor.SelectAll(); editor.InsertText("replacement\r\n");
        Assert.Equal("replacement\n", editor.Document.Text);
        Assert.Equal(editor.Text.Length, editor.CaretIndex);
    }

    [Fact]
    public void Formatting_ChangesOnlySelectionAndUndoRestoresRunsAndSelection()
    {
        using var editor = new RichTextEditor { Text = "hello world" };
        editor.Select(0, 5); editor.ApplyStyle(new() { Bold = true, Foreground = new(220, 10, 20) });
        Assert.Equal("hello world", editor.Text);
        Assert.Equal(new[] { "hello", " world" }, editor.Document.Runs.Select(run => run.Text));
        Assert.True(editor.Document.Runs[0].Style.Bold); Assert.Null(editor.Document.Runs[1].Style.Bold);
        editor.ApplyStyle(new() { Italic = true });
        Assert.True(editor.Document.Runs[0].Style.Bold); Assert.True(editor.Document.Runs[0].Style.Italic);
        Assert.True(editor.Undo()); Assert.Null(editor.Document.Runs[0].Style.Italic);
        Assert.Equal(0, editor.SelectionStart); Assert.Equal(5, editor.SelectionLength);
        Assert.True(editor.Undo()); Assert.Single(editor.Document.Runs); Assert.Null(editor.Document.Runs[0].Style.Bold);
        Assert.True(editor.Redo()); Assert.True(editor.Document.Runs[0].Style.Bold);
    }

    [Fact]
    public void CollapsedFormatting_AffectsNewTypingAndPreservesNeighbors()
    {
        using var editor = new RichTextEditor { Text = "ac" };
        editor.Select(1, 0); editor.ApplyStyle(new() { Underline = true, FontSize = 22 });
        editor.InsertText("b");
        Assert.Equal("abc", editor.Text);
        Assert.Equal(new[] { "a", "b", "c" }, editor.Document.Runs.Select(run => run.Text));
        Assert.True(editor.Document.Runs[1].Style.Underline);
        Assert.Equal(22, editor.Document.Runs[1].Style.FontSize); Assert.Null(editor.Document.Runs[2].Style.Underline);
        Assert.True(editor.Undo()); Assert.Equal("ac", editor.Text); Assert.Equal(1, editor.CaretIndex);
    }

    [Theory]
    [InlineData("a😀b", 1, 2)]
    [InlineData("ae\u0301b", 1, 2)]
    [InlineData("a🧑‍💻b", 1, 5)]
    public void NavigationAndDelete_RespectWholeGraphemes(string text, int start, int length)
    {
        var editor = new RichTextEditor { Text = text }; using var ui = Session(editor); ui.Update(); ui.Focus(editor);
        editor.Select(start + length, 0); Key(ui, UiKey.Left, shift: true);
        Assert.Equal(start, editor.SelectionStart); Assert.Equal(length, editor.SelectionLength);
        Key(ui, UiKey.Backspace);
        Assert.Equal("ab", editor.Text); Assert.Equal(start, editor.CaretIndex);
        Key(ui, UiKey.Z, control: true);
        Assert.Equal(text, editor.Text); Assert.Equal(length, editor.SelectionLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadOnlyAndDisabled_RejectEditsAndFormattingButProgrammaticUpdatesWork(bool disabled)
    {
        var editor = new RichTextEditor { Text = "original", Options = new() { IsReadOnly = !disabled }, IsEnabled = !disabled };
        using var ui = Session(editor); ui.Update();
        editor.SelectAll();
        Assert.False(editor.InsertText("no")); Assert.False(editor.ApplyStyle(new() { Bold = true }));
        Assert.False(editor.Undo()); Assert.False(editor.Redo());
        ui.HandleInput(new(InputKind.Text, Text: "x")); Key(ui, UiKey.Delete); Key(ui, UiKey.Enter);
        Assert.Equal("original", editor.Text);
        editor.Text = "external"; Assert.Equal("external", editor.Document.Text); Assert.False(editor.CanUndo);
    }

    [Fact]
    public void Clipboard_CutCopyPasteAndFailurePreserveSelectionUntilSuccess()
    {
        var clipboard = new Clipboard();
        var editor = new RichTextEditor { Text = "one\ntwo" }; using var ui = Session(editor, clipboard: clipboard); ui.Update(); ui.Focus(editor);
        editor.Select(0, 3); Key(ui, UiKey.C, control: true); Assert.Equal("one", clipboard.Text);
        clipboard.Failure = new UiClipboardException(UiClipboardOperation.Write, new IOException("clipboard busy"));
        Key(ui, UiKey.X, control: true);
        Assert.Equal("one\ntwo", editor.Text); Assert.Equal("one", editor.SelectedText); Assert.False(editor.CanUndo);
        clipboard.Failure = null; Key(ui, UiKey.X, control: true);
        Assert.Equal("\ntwo", editor.Text); Assert.Equal("one", clipboard.Text);
        clipboard.Text = "first\r\nsecond"; Key(ui, UiKey.V, control: true);
        Assert.Equal("first\nsecond\ntwo", editor.Text); Assert.True(editor.CanUndo);
    }

    [Fact]
    public void UndoLimit_RedoBranchAndExternalReplacementAreBounded()
    {
        using var editor = new RichTextEditor(new() { UndoLimit = 2 });
        editor.InsertText("a"); editor.InsertText("b"); editor.InsertText("c");
        Assert.True(editor.Undo()); Assert.Equal("ab", editor.Text);
        Assert.True(editor.Undo()); Assert.Equal("a", editor.Text); Assert.False(editor.Undo());
        Assert.True(editor.Redo()); editor.InsertText("x"); Assert.Equal("abx", editor.Text); Assert.False(editor.CanRedo);
        editor.Options = editor.Options with { UndoLimit = 0 };
        Assert.False(editor.CanUndo);
        editor.Text = "external"; Assert.Equal("external", editor.Document.Text); Assert.False(editor.CanRedo);
    }

    [Fact]
    public void DocumentBinding_PropagatesFormattingAndReleasesModelOnDispose()
    {
        var model = new Model();
        var editor = new RichTextEditor().BindTwoWay(RichTextEditor.DocumentProperty, model, value => value.Document);
        using (var ui = Session(editor))
        {
            ui.Update(); Assert.Equal("model", editor.Text); Assert.Equal(1, model.Subscriptions);
            editor.SelectAll(); editor.ApplyStyle(new() { Bold = true });
            Assert.Same(editor.Document, model.Document); Assert.True(model.Document.Runs[0].Style.Bold);
            model.Document = RichTextDocument.FromText("external\nvalue");
            ui.Update();
            Assert.Equal("external\nvalue", editor.Text); Assert.False(editor.CanUndo);
        }
        Assert.Equal(0, model.Subscriptions); Assert.True(editor.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => editor.InsertText("late"));
    }

    [Fact]
    public void TabOption_InsertsSpacesButShiftTabStillMovesFocus()
    {
        var editor = new RichTextEditor(new() { AcceptsTab = true });
        var button = new Button("next"); var root = new VStack().WithChildren(editor, button);
        using var ui = Session(root, height: 220); ui.Update(); ui.Focus(editor);
        Key(ui, UiKey.Tab); Assert.Equal("    ", editor.Text); Assert.True(editor.IsFocused);
        Key(ui, UiKey.Tab, shift: true); Assert.True(button.IsFocused);
        ui.Focus(editor); editor.Options = editor.Options with { AcceptsTab = false };
        Key(ui, UiKey.Tab); Assert.True(button.IsFocused); Assert.Equal("    ", editor.Text);
    }

    [Fact]
    public void Options_EnterAndKeyboardFormattingUseTheSameDocumentHistory()
    {
        var editor = new RichTextEditor(new() { AcceptsReturn = false });
        using var ui = Session(editor); ui.Update(); ui.Focus(editor);
        Key(ui, UiKey.Enter); Assert.Equal("", editor.Text);
        editor.Options = editor.Options with { AcceptsReturn = true };
        Key(ui, UiKey.B, control: true); ui.HandleInput(new(InputKind.Text, Text: "bold"));
        Key(ui, UiKey.Enter); ui.HandleInput(new(InputKind.Text, Text: "next"));
        Assert.Equal("bold\nnext", editor.Text); Assert.True(editor.Document.Runs[0].Style.Bold);
        Key(ui, UiKey.Z, control: true); Assert.Equal("bold\n", editor.Text);
        Key(ui, UiKey.Y, control: true); Assert.Equal("bold\nnext", editor.Text);
    }

    [Fact]
    public void Layout_MapsUtf16AcrossEmptyParagraphsAndSurrogatePairs()
    {
        var backend = new SkiaRasterRenderBackend(Catalog());
        using var surface = backend.CreateSurface(new RasterUiTarget(), new(400, 200));
        var service = Assert.IsAssignableFrom<IRichTextLayoutService>(surface.TextLayout);
        using var layout = service.CreateRichTextLayout(RichTextDocument.FromText("a😀\n\nend\n"),
            new() { FontFamily = "Fixed", FontSize = 18 }, 350, RichTextAlignment.Left);
        var first = layout.GetCaretBounds(0); var empty = layout.GetCaretBounds(4); var last = layout.GetCaretBounds(9);
        Assert.True(empty.Y > first.Y); Assert.True(last.Y > empty.Y); Assert.True(last.Height > 0);
        Assert.Equal(4, layout.HitTest(0, empty.Y + empty.Height / 2));
        Assert.Equal(0, layout.GetLineBoundary(3, false)); Assert.Equal(3, layout.GetLineBoundary(0, true));
        Assert.Equal(5, layout.MoveVertical(4, 1, 0));
    }

    [Fact]
    public void WordWrapAndResize_ChangeVisualLinesWithoutChangingDocumentOrSelection()
    {
        var editor = new RichTextEditor { Text = string.Join(" ", Enumerable.Repeat("wrapped text", 20)) };
        using var ui = Session(editor); ui.Update(); editor.Select(20, 5); var document = editor.Document;
        editor.Select(editor.Text.Length, 0); ui.Update(); Assert.True(editor.VerticalOffset > 0);
        editor.Options = editor.Options with { WordWrap = false }; ui.Update();
        Assert.True(editor.HorizontalOffset > 0); Assert.Equal(0, editor.VerticalOffset);
        ui.Resize(new(500, 150)); ui.Update(); Assert.Same(document, editor.Document); Assert.Equal(editor.Text.Length, editor.CaretIndex);
    }

    [Fact]
    public void WheelScrolling_DoesNotSnapBackToCaretOnDraw()
    {
        var editor = new RichTextEditor { Text = string.Join("\n", Enumerable.Range(0, 30).Select(value => $"line {value}")) };
        using var ui = Session(editor); ui.Update();
        ui.HandleInput(new(InputKind.Wheel, X: 20, Y: 30, WheelDelta: -3));
        var offset = editor.VerticalOffset; Assert.True(offset > 0);
        ui.Update(); Assert.Equal(offset, editor.VerticalOffset); Assert.Equal(0, editor.CaretIndex);
    }

    [Fact]
    public void NativePainting_UsesRichColorsAndHasNoStaticRedraw()
    {
        var editor = new RichTextEditor
        {
            Document = new([new("RED", new() { Foreground = new(240, 20, 30), FontSize = 28 }),
                new(" BLUE", new() { Foreground = new(20, 30, 240), Underline = true })])
        };
        var mapper = FontMapper.Default;
        using var ui = Session(editor); Assert.True(ui.Update()); Assert.False(ui.Update());
        using var image = ui.SnapshotRasterImage(); using var bitmap = SKBitmap.FromImage(image);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Red > 150 && pixel.Blue < 80);
        Assert.Contains(bitmap.Pixels, pixel => pixel.Blue > 150 && pixel.Red < 80);
        Assert.Same(mapper, FontMapper.Default);
    }

    [Fact]
    public void Layout_CannotCrossSurfaceOrThreadAndRejectsUseAfterDisposal()
    {
        using var resources = new SkiaDrawingResources(Catalog());
        using var otherResources = new SkiaDrawingResources(Catalog());
        using var bitmap = new SKBitmap(80, 80); using var canvas = new SKCanvas(bitmap);
        var context = new SkiaDrawingContext(canvas, resources);
        var other = new SkiaDrawingContext(canvas, otherResources);
        var layout = context.CreateRichTextLayout(RichTextDocument.FromText("text"), new() { FontSize = 16 }, 60, RichTextAlignment.Left);
        Assert.Throws<ArgumentException>(() => other.RichText(layout, 0, 0, 0, 0, new(1, 2, 3)));
        Exception? failure = null; var thread = new Thread(() => { try { layout.GetCaretBounds(0); } catch (Exception error) { failure = error; } });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.IsType<InvalidOperationException>(failure);
        layout.Dispose(); layout.Dispose();
        Assert.Throws<ObjectDisposedException>(() => layout.HitTest(0, 0));
    }

    [Fact]
    public void ExplicitFaceVariants_SelectRegisteredWeightAndSlant()
    {
        var catalog = SkiaFontCatalog.LoadFaces("Fixed", [
            new("Fixed", Font()), new("Fixed", Font("NotoSansMono-Regular.ttf"), 700),
            new("Fixed", Font("NotoSansSymbols-Regular.ttf"), 400, true)]);
        using var normal = catalog.CreateTypeface("Fixed", 400, false);
        using var bold = catalog.CreateTypeface("Fixed", 700, false);
        using var italic = catalog.CreateTypeface("Fixed", 400, true);
        Assert.Equal("Noto Sans", normal.FamilyName); Assert.Equal("Noto Sans Mono", bold.FamilyName); Assert.Contains("Symbols", italic.FamilyName);
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.LoadFaces("Fixed", [new("Fixed", Font(), 700)]));
        Assert.Throws<ArgumentException>(() => SkiaFontCatalog.LoadFaces("Fixed", [new("Fixed", Font()), new("Fixed", Font())]));
    }

    [Fact]
    public void RichFontMapper_ReusesProcessShapingFaceAcrossCatalogsAndSurfaceDisposal()
    {
        var style = new Topten.RichTextKit.Style { FontFamily = "Fixed", FontSize = 16 };
        SKTypeface face;
        using (var first = new SkiaDrawingResources(Catalog())) face = first.RichFonts.TypefaceFromStyle(style, false);
        using var next = new SkiaDrawingResources(Catalog());
        Assert.Same(face, next.RichFonts.TypefaceFromStyle(style, false));
        Assert.NotEqual(IntPtr.Zero, face.Handle);
        Assert.Throws<ObjectDisposedException>(() => { var disposed = new SkiaDrawingResources(Catalog()); disposed.Dispose(); _ = disposed.RichFonts; });
    }

    [Fact]
    public void InvalidOptionsAndSelection_PreserveContent()
    {
        using var editor = new RichTextEditor { Text = "abc" };
        Assert.Throws<ArgumentOutOfRangeException>(() => new RichTextEditorOptions { UndoLimit = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RichTextStyle { FontSize = float.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Options = new() { Alignment = (RichTextAlignment)99 });
        Assert.Equal("abc", editor.Text); Assert.Equal(0, editor.CaretIndex); Assert.False(editor.CanUndo);
    }

    [Fact]
    public void PointerDrag_FocusLossReleasesCaptureAndStopsSelectionChanges()
    {
        var editor = new RichTextEditor { Text = "abcdefghij" };
        using var ui = Session(editor); ui.Update();
        var down = ui.HandleInput(new(InputKind.PointerDown, X: 10, Y: 12));
        Assert.True(down.PointerCaptured); Assert.True(editor.IsFocused);
        ui.HandleInput(new(InputKind.PointerMove, X: 70, Y: 12));
        Assert.True(editor.SelectionLength > 0);
        var selection = editor.SelectedText;
        Assert.False(ui.HandleInput(new(InputKind.FocusLost)).PointerCaptured);
        ui.HandleInput(new(InputKind.PointerMove, X: 120, Y: 12));
        Assert.Equal(selection, editor.SelectedText); Assert.False(editor.IsFocused);
    }

    [Fact]
    public void VisualLineKeys_MoveBetweenParagraphsAndRespectControlHomeEnd()
    {
        var editor = new RichTextEditor { Text = "abc\nlonger\nz" };
        using var ui = Session(editor); ui.Update(); ui.Focus(editor);
        editor.Select(4, 0); Key(ui, UiKey.Up); Assert.Equal(0, editor.CaretIndex);
        Key(ui, UiKey.Down); Assert.Equal(4, editor.CaretIndex);
        Key(ui, UiKey.End); Assert.Equal(10, editor.CaretIndex);
        Key(ui, UiKey.Home); Assert.Equal(4, editor.CaretIndex);
        Key(ui, UiKey.End, control: true); Assert.Equal(12, editor.CaretIndex);
        Key(ui, UiKey.Home, control: true, shift: true); Assert.Equal(editor.Text, editor.SelectedText);
    }

    [Fact]
    public void FormattingFalse_PreservesOtherAttributesAndReplacementUsesInsertionStyle()
    {
        using var editor = new RichTextEditor
        { Document = new([new("formatted", new() { Bold = true, Italic = true, FontSize = 24 })]) };
        editor.SelectAll(); editor.ApplyStyle(new() { Bold = false, Underline = true });
        var style = Assert.Single(editor.Document.Runs).Style;
        Assert.False(style.Bold); Assert.True(style.Italic); Assert.True(style.Underline); Assert.Equal(24, style.FontSize);
        editor.InsertText("replacement");
        Assert.Equal("replacement", editor.Text); Assert.Equal(style, Assert.Single(editor.Document.Runs).Style);
        editor.Undo(); Assert.Equal("formatted", editor.SelectedText);
    }

    [Fact]
    public void DetachedEditor_ReattachesToAnotherSurfaceWithoutLosingDocument()
    {
        var editor = new RichTextEditor { Text = "retained" }; var root = new VStack().WithChildren(editor);
        using (var ui = Session(root))
        {
            ui.Update(); root.Items.Remove(editor); Assert.False(editor.IsAttached);
        }
        Assert.False(editor.IsDisposed);
        using var next = Session(editor); next.Update(); editor.Select(8, 0); editor.InsertText("!"); next.Update();
        Assert.Equal("retained!", editor.Text); Assert.True(editor.IsAttached);
    }

    [Fact]
    public void PropertyObserverFailure_PreservesCommittedDocumentTextAndUndo()
    {
        using var editor = new RichTextEditor { Text = "old" }; editor.SelectAll();
        void Throw(Element _, object property) { if (ReferenceEquals(property, RichTextEditor.DocumentProperty)) throw new InvalidOperationException("observer"); }
        editor.PropertyChanged += Throw;
        Assert.Throws<InvalidOperationException>(() => editor.InsertText("new"));
        Assert.Equal("new", editor.Document.Text); Assert.Equal(editor.Text, editor.Document.Text); Assert.Equal(3, editor.CaretIndex);
        editor.PropertyChanged -= Throw; Assert.True(editor.Undo()); Assert.Equal("old", editor.SelectedText);
    }

    [Fact]
    public void UnsupportedTextBackend_IsExplicitAndDoesNotDisposeTheEditor()
    {
        using var editor = new RichTextEditor();
        Assert.Throws<NotSupportedException>(() => editor.Measure(new(200, 100), new FixedTextLayout()));
        Assert.False(editor.IsDisposed); editor.Text = "retained"; Assert.Equal("retained", editor.Document.Text);
    }

    private sealed class Clipboard : IUiClipboard
    {
        internal string? Text;
        internal Exception? Failure;
        public string? GetText() { if (Failure != null) throw Failure; return Text; }
        public void SetText(string text) { if (Failure != null) throw Failure; Text = text; }
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private RichTextDocument _document = RichTextDocument.FromText("model");
        private PropertyChangedEventHandler? _changed;
        internal int Subscriptions => _changed?.GetInvocationList().Length ?? 0;
        public RichTextDocument Document { get => _document; set { _document = value; _changed?.Invoke(this, new(nameof(Document))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
}
