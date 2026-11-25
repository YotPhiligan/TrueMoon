using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace RichTextEditorTest;

/// <summary>Consumer-owned toolbar composition around a reusable RichTextEditor.</summary>
public sealed class EditorView : View
{
    private int _smokeStep;
    /// <summary>The editor exercised by the sample.</summary>
    public RichTextEditor Editor { get; } = new(new() { ShowSelectionWhenUnfocused = true, AcceptsTab = true })
    {
        Height = 330,
        Document = new([
            new("RichTextEditor", new() { Bold = true, FontSize = 28, Foreground = new(80, 180, 255) }),
            new("\nОбычный текст, "), new("курсив", new() { Italic = true }),
            new(" и "), new("подчёркивание", new() { Underline = true }),
            new(".\nВыделите текст и примените форматирование.\nUnicode: e\u0301, 😀, 🧑‍💻.\nEnter — абзац; Tab — четыре пробела.\nCtrl+Z/Y — undo/redo.\n"),
            new("Цвет и размер могут отличаться внутри одного абзаца.", new() { Foreground = new(230, 150, 70), FontSize = 19 })
        ])
    };

    /// <summary>Creates the demo with independent toolbar controls.</summary>
    public EditorView()
    {
        var wrap = new CheckBox("Word wrap"); wrap.IsChecked = true;
        wrap.OnClick(() => Editor.Options = Editor.Options with { WordWrap = wrap.IsChecked });
        var readOnly = new CheckBox("Read only");
        readOnly.OnClick(() => Editor.Options = Editor.Options with { IsReadOnly = readOnly.IsChecked });
        SetContent(new VStack().Padding(16).WithChildren(
            new Text("Alloy — RichTextKit editor"),
            new HStack().WithChildren(
                new Button("Bold").OnClick(() => Editor.ApplyStyle(new() { Bold = !(Editor.SelectionStyle.Bold ?? false) })),
                new Button("Italic").OnClick(() => Editor.ApplyStyle(new() { Italic = !(Editor.SelectionStyle.Italic ?? false) })),
                new Button("Underline").OnClick(() => Editor.ApplyStyle(new() { Underline = !(Editor.SelectionStyle.Underline ?? false) })),
                new Button("Red").OnClick(() => Editor.ApplyStyle(new() { Foreground = new(240, 70, 70) })),
                new Button("Undo").OnClick(() => Editor.Undo()),
                new Button("Redo").OnClick(() => Editor.Redo())),
            new HStack().WithChildren(wrap, readOnly),
            Editor,
            new Text("Выделение сохраняется при фокусе на toolbar. Clipboard — обычный текст.")));
    }

    /// <summary>Runs a deterministic edit/format/history/option sequence after actual presented frames.</summary>
    public void VerifyFrame()
    {
        switch (++_smokeStep)
        {
            case 1:
                Editor.Text = "alpha\nβeta 🧑‍💻"; Editor.Select(0, 5); break;
            case 2:
                Editor.ApplyStyle(new() { Bold = true, Foreground = new(240, 20, 30) });
                Require(Editor.Document.Runs[0].Text == "alpha" && Editor.Document.Runs[0].Style.Bold == true, "Formatting did not reach the document.");
                break;
            case 3:
                Editor.InsertText("changed"); Require(Editor.Text == "changed\nβeta 🧑‍💻", "Replacement failed."); break;
            case 4:
                Require(Editor.Undo() && Editor.SelectedText == "alpha", "Undo did not restore rich content/selection.");
                Require(Editor.Document.Runs[0].Style.Bold == true, "Undo lost formatting."); break;
            case 5:
                Require(Editor.Redo() && Editor.Text.StartsWith("changed"), "Redo failed.");
                Editor.Options = Editor.Options with { IsReadOnly = true }; break;
            case 6:
                Require(!Editor.InsertText("forbidden") && !Editor.ApplyStyle(new() { Italic = true }), "Read-only editor accepted edits.");
                Editor.Options = Editor.Options with { IsReadOnly = false, WordWrap = false }; break;
            case 7:
                Editor.SelectAll(); Editor.InsertText(string.Join("\n", Enumerable.Repeat("RichTextKit render/update", 24))); break;
            case 8:
                Editor.Options = Editor.Options with { WordWrap = true }; break;
            case 9:
                Require(Editor.VerticalOffset > 0, "Caret did not scroll into view.");
                Editor.ScrollTo(0, 0); break;
            case 10:
                Require(Editor.VerticalOffset == 0 && Editor.Document.Text == Editor.Text, "Scroll or document synchronization failed."); break;
        }
    }
    /// <summary>Checks final owner cleanup.</summary>
    public void VerifyClosed()
    { Require(_smokeStep >= 10 && IsDisposed && Editor.IsDisposed && !Editor.IsAttached, "Demo did not complete or release its tree."); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
