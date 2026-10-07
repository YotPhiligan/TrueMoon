using TrueMoon.Alloy;
using TrueMoon.Argentis;

namespace AlloyTest;

/// <summary>A bounded interaction probe running on the real window's UI thread.</summary>
internal sealed class SettingsFormSmoke
{
    private Element[]? _tree;
    private SettingsModel? _model;
    private TextBox? _editor;
    private CheckBox? _toggle;
    private Slider? _slider;
    private Text? _summary;
    private Button? _example, _reset;
    private int _stage, _selectionStart, _selectionLength;

    public void Frame(UiSession ui)
    {
        if (_tree == null)
        {
            var view = (View1)ui.Root;
            _model = view.DataContext ?? throw new InvalidOperationException("Missing settings model.");
            _tree = Descendants(view).ToArray();
            _editor = _tree.OfType<TextBox>().Single();
            _toggle = _tree.OfType<CheckBox>().Single();
            _slider = _tree.OfType<Slider>().Single();
            _summary = ((Border)_tree.Single(element => element is Border)).Child as Text
                ?? throw new InvalidOperationException("Missing summary label.");
            _example = _tree.OfType<Button>().Single(button => button.Value == "Загрузить пример");
            _reset = _tree.OfType<Button>().Single(button => button.Value == "Сбросить");
        }
        Require(_tree.SequenceEqual(Descendants(ui.Root)), "Control identities changed.");
        var model = _model!;
        var editor = _editor!;
        var toggle = _toggle!;
        var slider = _slider!;
        switch (_stage)
        {
            case 0:
                VerifyValues("Алексей", true, .4f);
                ui.Focus(editor);
                Require(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true)).Handled, "Select all was not handled.");
                Require(ui.HandleInput(new UiInput(InputKind.Text, Text: "Ирина 🧑‍💻")).Handled, "Text input was not handled.");
                Click(ui, toggle);
                Click(ui, slider, .75f);
                Require(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right)).Handled, "Slider key was not handled.");
                Require(model.Name == "Ирина 🧑‍💻" && !model.Enabled && Math.Abs(model.Volume - .8f) < .001f, "Input did not update the model.");
                ui.Focus(editor);
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                _selectionStart = editor.SelectionStart;
                _selectionLength = editor.SelectionLength;
                Require(_selectionLength == "🧑‍💻".Length, "Selection split a grapheme.");
                _stage++;
                break;
            case 1:
                VerifyValues("Ирина 🧑‍💻", false, .8f);
                model.Enabled = true;
                _stage++;
                break;
            case 2:
                VerifyValues("Ирина 🧑‍💻", true, .8f);
                Require(editor.IsFocused && editor.SelectionStart == _selectionStart && editor.SelectionLength == _selectionLength,
                    "An unrelated model update lost editor focus or selection.");
                Click(ui, _example!);
                _stage++;
                break;
            case 3:
                VerifyValues("Привет 👋", false, .75f);
                Click(ui, _reset!);
                _stage++;
                break;
            default:
                VerifyValues("Алексей", true, .4f);
                Require(!ui.NeedsUpdate, "An unchanged form kept invalidating.");
                _stage++;
                break;
        }
    }

    public void VerifyDisposed()
    {
        Require(_stage >= 5, "The interaction probe did not finish.");
        Require(_tree != null && _tree.All(element => element.IsDisposed && !element.IsAttached), "The form was not released.");
    }

    private void VerifyValues(string name, bool enabled, float volume)
    {
        Require(_model!.Name == name && _editor!.Value == name, "Name binding mismatch.");
        Require(_model.Enabled == enabled && _toggle!.IsChecked == enabled, "Toggle binding mismatch.");
        Require(Math.Abs(_model.Volume - volume) < .001f && Math.Abs(_slider!.Value - volume) < .001f, "Volume binding mismatch.");
        var expectedSummary = $"{name} · уведомления: {(enabled ? "вкл." : "выкл.")} · громкость: {volume:P0}";
        Require(_model.Summary == expectedSummary && _summary!.Value == expectedSummary, "Summary binding mismatch.");
    }
    private static void Click(UiSession ui, Element control, float fraction = .5f)
    {
        var bounds = control.Bounds;
        Require(bounds.Width > 0 && bounds.Height > 0, "An interactive control has empty bounds.");
        var x = bounds.X + bounds.Width * fraction;
        var y = bounds.Y + bounds.Height / 2;
        Require(ui.HandleInput(new UiInput(InputKind.PointerDown, x, y)).Handled, "Pointer down was not handled.");
        var release = ui.HandleInput(new UiInput(InputKind.PointerUp, x, y));
        Require(release.Handled && !release.PointerCaptured, "Pointer up did not release capture.");
    }
    private static IEnumerable<Element> Descendants(Element root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
