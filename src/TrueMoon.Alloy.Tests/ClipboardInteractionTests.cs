using System.ComponentModel;
using AlloyTest;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ClipboardInteractionTests
{
    [Theory]
    [InlineData(UiKey.C, UiClipboardOperation.Write)]
    [InlineData(UiKey.X, UiClipboardOperation.Write)]
    [InlineData(UiKey.V, UiClipboardOperation.Read)]
    public void ClipboardFailure_ReportsOperationAndPreservesActualDemoEditorUntilRetry(UiKey key, UiClipboardOperation operation)
    {
        var model = new SettingsModel { Name = "Latin Привет 🧑‍💻" };
        var view = new View1(model); var clipboard = new Clipboard { Text = "replacement" };
        var surface = new Surface(); using var ui = new UiSession(view, surface, new UiViewport(900, 1100), clipboard);
        ui.Update(); ui.Focus(view.NameEditor); ui.Capture(view.NameEditor);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        ui.Update();
        var editor = view.NameEditor; var start = editor.SelectionStart; var length = editor.SelectionLength;
        Assert.Equal("🧑‍💻".Length, length);
        var cause = new Win32Exception(5); var failure = new UiClipboardException(operation, cause);
        clipboard.Failure = failure;
        var reported = new List<UiClipboardException>(); ui.ClipboardFailed += reported.Add;
        var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: true));
        Assert.True(result.Handled); Assert.True(result.KeyboardFocused); Assert.True(result.PointerCaptured);
        Assert.Same(failure, Assert.Single(reported)); Assert.Same(cause, reported[0].InnerException);
        Assert.Equal(operation, reported[0].Operation);
        Assert.Equal("Latin Привет 🧑‍💻", model.Name); Assert.Equal(model.Name, editor.Value);
        Assert.Equal(start, editor.SelectionStart); Assert.Equal(length, editor.SelectionLength); Assert.True(editor.IsFocused);
        Assert.Equal("replacement", clipboard.Text); Assert.False(ui.IsDisposed); Assert.False(ui.NeedsUpdate);
        Assert.False(ui.Update());
        clipboard.Failure = null;
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: true)).Handled);
        Assert.Single(reported);
        Assert.Equal(key == UiKey.V ? "Latin Привет replacement" : key == UiKey.X ? "Latin Привет " : "Latin Привет 🧑‍💻", model.Name);
        Assert.Equal(key == UiKey.V ? "replacement" : "🧑‍💻", clipboard.Text);
        Assert.Equal(model.Name, editor.Value); Assert.True(editor.IsFocused);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "!")).Handled);
        Assert.Contains("!", model.Name);
    }

    [Fact]
    public void ClipboardFailure_WithoutSubscriberRemainsHandledAndSessionUsable()
    {
        var editor = new TextBox { Value = "retained" }; var failure = new UiClipboardException(UiClipboardOperation.Read, new Win32Exception(5));
        using var ui = new UiSession(editor, new Surface(), new UiViewport(200, 80), new Clipboard { Failure = failure });
        ui.Update(); ui.Focus(editor);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.V, Control: true)).Handled);
        Assert.Equal("retained", editor.Value); Assert.False(ui.IsDisposed);
        Assert.True(ui.HandleInput(new UiInput(InputKind.Text, Text: "x")).Handled);
        Assert.Contains("x", editor.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectClipboardAccess_ThrowsTypedFailureWithoutInputNotification(bool write)
    {
        var failure = new UiClipboardException(write ? UiClipboardOperation.Write : UiClipboardOperation.Read, new Win32Exception(5));
        using var ui = new UiSession(new TextBox(), new Surface(), new UiViewport(200, 80), new Clipboard { Failure = failure });
        var reports = 0; ui.ClipboardFailed += _ => reports++;
        Assert.Same(failure, Assert.Throws<UiClipboardException>(() => { if (write) ui.SetClipboardText("text"); else _ = ui.GetClipboardText(); }));
        Assert.Equal(0, reports);
    }

    [Fact]
    public void UnexpectedClipboardProviderFailure_IsNotSilenced()
    {
        var failure = new InvalidOperationException("Programming error."); var editor = new TextBox { Value = "retained" };
        using var ui = new UiSession(editor, new Surface(), new UiViewport(200, 80), new Clipboard { Failure = failure });
        ui.Update(); ui.Focus(editor); var reports = 0; ui.ClipboardFailed += _ => reports++;
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.X, Control: true))));
        Assert.Equal("retained", editor.Value);
        Assert.Equal(0, reports);
    }

    [Fact]
    public void ClipboardObserverFailure_PropagatesAndDoesNotModifyEditor()
    {
        var editor = new TextBox { Value = "retained" }; var observer = new InvalidOperationException("Observer failed.");
        using var ui = new UiSession(editor, new Surface(), new UiViewport(200, 80), new Clipboard { Failure = new UiClipboardException(UiClipboardOperation.Read, new Win32Exception(5)) });
        ui.Update(); ui.Focus(editor); ui.ClipboardFailed += _ => throw observer;
        Assert.Same(observer, Assert.Throws<InvalidOperationException>(() => ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.V, Control: true))));
        Assert.Equal("retained", editor.Value);
    }

    [Theory]
    [InlineData(UiKey.C)]
    [InlineData(UiKey.X)]
    public void CopyAndCutWithoutSelection_DoNotAccessClipboard(UiKey key)
    {
        var clipboard = new Clipboard { Failure = new UiClipboardException(UiClipboardOperation.Write, new Win32Exception(5)) };
        var editor = new TextBox { Value = "retained" }; using var ui = new UiSession(editor, new Surface(), new UiViewport(200, 80), clipboard);
        ui.Update(); ui.Focus(editor);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: true)).Handled);
        Assert.Equal(0, clipboard.Accesses); Assert.Equal("retained", editor.Value);
    }

    [Theory]
    [InlineData(UiKey.C)]
    [InlineData(UiKey.X)]
    public void FailureAfterPublication_PreservesEditorWithoutPromisingClipboardRollback(UiKey key)
    {
        var editor = new TextBox { Value = "selected 🧑‍💻" };
        var failure = new UiClipboardException(UiClipboardOperation.Write, new Win32Exception(6));
        var clipboard = new Clipboard { Failure = failure, PublishBeforeFailure = true, Text = "previous" };
        using var ui = new UiSession(editor, new Surface(), new UiViewport(240, 80), clipboard);
        ui.Update(); ui.Focus(editor); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
        UiClipboardException? reported = null; ui.ClipboardFailed += error => reported = error;
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: true)).Handled);
        Assert.Same(failure, reported);
        Assert.Equal("selected 🧑‍💻", editor.Value); Assert.Equal(editor.Value, clipboard.Text);
        Assert.Equal(0, editor.SelectionStart); Assert.Equal(editor.Value.Length, editor.SelectionLength);
        Assert.True(editor.IsFocused);
    }

    [Fact]
    public void ClipboardException_RejectsInvalidOperationAndMissingCause()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiClipboardException((UiClipboardOperation)99, new Win32Exception(5)));
        Assert.Throws<ArgumentNullException>(() => new UiClipboardException(UiClipboardOperation.Read, null!));
    }

    private sealed class Clipboard : IUiClipboard
    {
        internal string? Text;
        internal Exception? Failure;
        internal int Accesses;
        internal bool PublishBeforeFailure;
        public string? GetText() { Accesses++; if (Failure != null) throw Failure; return Text; }
        public void SetText(string text) { Accesses++; if (PublishBeforeFailure) Text = text; if (Failure != null) throw Failure; Text = text; }
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) => root.Draw(new RecordingDrawingContext());
        public void Dispose() { }
    }
}
