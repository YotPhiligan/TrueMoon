using System.ComponentModel;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;

namespace AlloyTest;

/// <summary>Verifies a real hosted window with a controlled clipboard service; never accesses the system clipboard.</summary>
internal sealed class ClipboardFailureSmoke
{
    private readonly Clipboard _clipboard = new();
    private bool _verified;
    private UiSession? _session;
    internal sealed class Sessions(HostedUiSessionFactory inner, ClipboardFailureSmoke smoke) : IUiSessionFactory
    {
        public UiSession Create(Element root, UiRenderTarget target, UiViewport viewport, IUiClipboard? clipboard = null)
        {
            var session = inner.Create(root, target, viewport, smoke._clipboard);
            smoke._session = session;
            return session;
        }
    }
    internal void Frame(UiSession ui)
    {
        if (_verified) return;
        var editor = ((View1)ui.Root).NameEditor;
        Require(editor.IsFocused && editor.SelectionLength > 0, "Expected selected editor from interaction stage0.");
        var value = editor.Value; var start = editor.SelectionStart; var length = editor.SelectionLength;
        var failures = new List<UiClipboardException>();
        ui.ClipboardFailed += failures.Add;
        try
        {
            foreach (var key in new[] { UiKey.C, UiKey.X, UiKey.V })
            {
                var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key, Control: true));
                Require(result.Handled && result.KeyboardFocused, "Clipboard failure escaped input routing.");
                Require(editor.IsFocused && editor.Value == value && editor.SelectionStart == start && editor.SelectionLength == length,
                    "Clipboard failure changed editor value, selection or focus.");
                Require(!ui.IsDisposed, "Clipboard failure disposed session.");
            }
            Require(failures.Select(error => error.Operation).SequenceEqual(new[] { UiClipboardOperation.Write, UiClipboardOperation.Write, UiClipboardOperation.Read }),
                "Expected one failure notification for each copy/cut/paste operation.");
            Require(failures.All(error => error.InnerException is Win32Exception { NativeErrorCode: 5 }), "Unexpected failure cause.");
            _clipboard.Failing = false;
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.C, Control: true));
            Require(_clipboard.Text == value.Substring(start, length), "Copy did not recover.");
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.X, Control: true));
            Require(editor.Value == value.Remove(start, length), "Cut did not recover.");
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.V, Control: true));
            Require(editor.Value == value, "Paste did not recover.");
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
            Require(editor.SelectionStart == start && editor.SelectionLength == length && editor.IsFocused, "Retry lost selection or focus.");
            Require(failures.Count == 3, "Successful retries emitted failure notifications.");
            _verified = true;
        }
        finally { ui.ClipboardFailed -= failures.Add; }
    }
    internal void Verify(HostedUiSessionFactory registry)
    {
        Require(_verified && _session is { IsDisposed: true }, "Clipboard recovery smoke did not complete and dispose.");
        Require(registry.ActiveSessionCount == 0, "Clipboard recovery retained a registered session.");
    }
    private sealed class Clipboard : IUiClipboard
    {
        internal bool Failing = true;
        internal string? Text;
        public string? GetText() { if (Failing) throw Failure(UiClipboardOperation.Read); return Text; }
        public void SetText(string text) { if (Failing) throw Failure(UiClipboardOperation.Write); Text = text; }
        private static UiClipboardException Failure(UiClipboardOperation operation) => new(operation, new Win32Exception(5));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
