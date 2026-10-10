using TrueMoon.Alloy;
using TrueMoon.Argentis;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AlloyTest;

/// <summary>A bounded interaction probe running on the UI owner thread with any backend.</summary>
internal sealed class SettingsFormSmoke(bool verifyClipboard = false, bool trace = false)
{
    internal const string ClipboardFixture = "Alloy clipboard smoke fixture v1";
    private Element[]? _tree;
    private SettingsModel? _model;
    private TextBox? _editor;
    private CheckBox? _toggle;
    private Slider? _slider;
    private Text? _summary;
    private Button? _example, _reset;
    private int _stage, _selectionStart, _selectionLength;
    private VStack? _list;
    private SettingsRow? _row;
    private Element? _rowNode, _removedNode;
    private TextBox? _rowEditor;
    private int _rowSelectionStart, _rowSelectionLength;
    private readonly HashSet<Element> _generated = new(ReferenceEqualityComparer.Instance);
    private Element[]? _rowsBeforeReset;
    private LoadStatusRegion? _statusRegion;
    private ScrollViewer? _rowScroll;
    private readonly HashSet<Element> _branches = new(ReferenceEqualityComparer.Instance);
    private Element? _previousBranch;
    private TextBox? _statusNeighbor;
    private Element[]? _statusRows;
    private int _statusSelectionStart, _statusSelectionLength;
    private View1? _view;
    private int _frames;
    private readonly ConcurrentQueue<TraceEntry>? _trace = trace ? new() : null;
    private sealed record TraceEntry(int Frame, int Stage, string Event, string ModelName, string EditorValue,
        bool ModelEnabled, bool Focused, int Caret, int SelectionStart, int SelectionLength, UiInput? Input);

    internal void ObserveNativeInput(UiInput input) => Record("native-input-after-routing", input);

    internal void WriteTrace(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(new
        {
            FrameCalls = _frames, Stage = _stage, Completed = _stage >= 31,
            Scope = "Last 128 frame/native-input snapshots; native input is observed after Hosting routing, not raw Win32 messages.",
            Entries = _trace?.ToArray() ?? []
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Record(string name, UiInput? input = null)
    {
        if (_trace == null || _model == null || _editor == null || _editor.IsDisposed) return;
        _trace.Enqueue(new(_frames, _stage, name, _model.Name, _editor.Value, _model.Enabled,
            _editor.IsFocused, _editor.CaretIndex, _editor.SelectionStart, _editor.SelectionLength, input));
        while (_trace.Count > 128) _trace.TryDequeue(out _);
    }

    public void Frame(UiSession ui, View1? settingsView = null)
    {
        if (_tree == null)
        {
            var view = settingsView ?? Descendants(ui.Root).OfType<View1>().Single();
            _view = view;
            _model = view.DataContext ?? throw new InvalidOperationException("Missing settings model.");
            _list = view.RowList;
            _statusRegion = view.StatusRegion;
            _rowScroll = view.RowScroll;
            _tree = FixedTree(view).ToArray();
            _editor = view.NameEditor;
            _toggle = view.NotificationToggle;
            _slider = view.VolumeSlider;
            _summary = ((Border)_tree.Single(element => element is Border)).Child as Text
                ?? throw new InvalidOperationException("Missing summary label.");
            _example = _tree.OfType<Button>().Single(button => button.Value == "Загрузить пример");
            _reset = _tree.OfType<Button>().Single(button => button.Value == "Сбросить");
        }
        _frames++;
        Record("frame-start");
        Require(_tree.SequenceEqual(FixedTree(_view!)), "Form control identities changed.");
        foreach (var element in Descendants(_list!).Skip(1)) _generated.Add(element);
        foreach (var element in Descendants(_statusRegion!).Skip(1)) _branches.Add(element);
        Require(_list!.Items.Count == _model!.Rows.Count, "Collection size mismatch.");
        for (var index = 0; index < _list.Items.Count; index++)
            Require(((TextBox)_list.Items[index].Children[0]).Value == _model.Rows[index].Name, "Row binding/order mismatch.");
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
            case 4:
                VerifyValues("Алексей", true, .4f);
                _row = model.Rows[1];
                _rowNode = _list.Items[1];
                _rowEditor = (TextBox)_rowNode.Children[0];
                ui.Focus(_rowEditor);
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
                Require(ui.HandleInput(new UiInput(InputKind.Text, Text: "Дело 🧑‍💻")).Handled, "Row text edit was not handled.");
                Require(_row.Name == "Дело 🧑‍💻", "Row edit did not write to the model.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                _rowSelectionStart = _rowEditor.SelectionStart;
                _rowSelectionLength = _rowEditor.SelectionLength;
                Require(_rowSelectionLength == "🧑‍💻".Length, "Row selection split a grapheme.");
                _stage++;
                break;
            case 5:
                model.AddRow();
                _stage++;
                break;
            case 6:
                Require(model.Rows.Count == 4, "Add did not create a row.");
                VerifyRetainedRow();
                model.MoveRowUp(_row!);
                _stage++;
                break;
            case 7:
                Require(ReferenceEquals(_list.Items[0], _rowNode), "Move recreated or misplaced the row.");
                VerifyRetainedRow();
                _rowsBeforeReset = _list.Items.ToArray();
                model.ResetRows();
                _stage++;
                break;
            case 8:
                Require(_list.Items.SequenceEqual(_rowsBeforeReset!.Reverse()), "Reset recreated or misplaced surviving rows.");
                VerifyRetainedRow();
                _removedNode = _list.Items[0];
                model.ReplaceRow(model.Rows[0]);
                _stage++;
                break;
            case 9:
                Require(_removedNode!.IsDisposed && !_removedNode.IsAttached, "Replace did not release the old subtree.");
                Require(!ReferenceEquals(_list.Items[0], _removedNode), "Replace reused a different model's row.");
                VerifyRetainedRow();
                model.RemoveRow(_row!);
                _stage++;
                break;
            case 10:
                Require(_rowNode!.IsDisposed && _rowEditor!.IsDisposed && !_rowEditor.IsFocused, "Remove retained the focused row.");
                Require(!ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).KeyboardFocused, "Remove retained session focus.");
                Click(ui, _tree.OfType<Button>().Single(button => button.Value == "Добавить дело"));
                _stage++;
                break;
            case 11:
                Require(model.Rows.Count == 4, "Add button did not update the collection.");
                _removedNode = _list.Items[0];
                Click(ui, _removedNode.Children.OfType<Button>().Single(button => button.Value == "Удалить"));
                _stage++;
                break;
            case 12:
                Require(model.Rows.Count == 3 && _removedNode!.IsDisposed, "Remove button did not release its row.");
                _stage++;
                break;
            case 13:
                for (var index = 0; index < 4; index++) model.AddRow();
                _stage++;
                break;
            case 14:
                _statusRows = _list.Items.ToArray();
                _statusNeighbor = (TextBox)_list.Items[2].Children[0];
                ui.Focus(_statusNeighbor);
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
                Require(ui.HandleInput(new UiInput(InputKind.Text, Text: "Сосед 🧑‍💻")).Handled, "Neighbor text edit was not handled.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                _statusSelectionStart = _statusNeighbor.SelectionStart;
                _statusSelectionLength = _statusNeighbor.SelectionLength;
                Require(_statusSelectionLength == "🧑‍💻".Length, "Neighbor selection split a grapheme.");
                _rowScroll!.Offset = 36;
                _previousBranch = _statusRegion!.Child;
                model.BeginLoading();
                _stage++;
                break;
            case 15:
                VerifyStatusTransition(LoadPhase.Loading, preserveNeighbor: true);
                Require(Descendants(_statusRegion!).OfType<ProgressBar>().Single().Value == .5f, "Loading branch lacks its progress indicator.");
                model.ShowLoadError();
                _stage++;
                break;
            case 16:
                VerifyStatusTransition(LoadPhase.Error, preserveNeighbor: true);
                model.CompleteLoading();
                _stage++;
                break;
            case 17:
                VerifyStatusTransition(LoadPhase.Ready, preserveNeighbor: true);
                Click(ui, _tree.OfType<Button>().Single(button => button.Value == "Ошибка"));
                _stage++;
                break;
            case 18:
                VerifyStatusTransition(LoadPhase.Error, preserveNeighbor: false);
                Click(ui, Descendants(_statusRegion!).OfType<Button>().Single(button => button.Value == "Повторить"));
                _stage++;
                break;
            case 19:
                VerifyStatusTransition(LoadPhase.Loading, preserveNeighbor: false);
                Require(!ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).KeyboardFocused, "Replacing the retry branch retained session focus.");
                Click(ui, _tree.OfType<Button>().Single(button => button.Value == "Готово"));
                _stage++;
                break;
            case 20:
                VerifyStatusTransition(LoadPhase.Ready, preserveNeighbor: false);
                _stage++;
                break;
            case 21:
                ui.Focus(_statusNeighbor!);
                editor = _statusNeighbor!;
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                _statusSelectionStart = editor.SelectionStart;
                _statusSelectionLength = editor.SelectionLength;
                model.UseLight();
                _stage++;
                break;
            case 22:
                VerifyAppearance(light: true, compact: false, preserveNeighbor: true);
                model.IsCompact = true;
                _stage++;
                break;
            case 23:
                VerifyAppearance(light: true, compact: true, preserveNeighbor: true);
                Click(ui, _view!.DarkButton);
                _stage++;
                break;
            case 24:
                VerifyAppearance(light: false, compact: true, preserveNeighbor: false);
                Click(ui, _view!.CompactToggle);
                _stage++;
                break;
            case 25:
                VerifyAppearance(light: false, compact: false, preserveNeighbor: false);
                Click(ui, _view!.EditingToggle);
                _stage++;
                break;
            case 26:
                Require(!model.ControlsEnabled && !_view!.SettingsFields.IsEffectivelyEnabled, "Disabled group remained enabled.");
                var previousName = model.Name;
                var previousEnabled = model.Enabled;
                var previousVolume = model.Volume;
                var disabled = editor.Bounds;
                Require(!ui.HandleInput(new UiInput(InputKind.PointerDown, disabled.X + 10, disabled.Y + 10)).Handled, "Disabled editor consumed pointer input.");
                Require(model.Name == previousName && model.Enabled == previousEnabled && model.Volume == previousVolume, "Disabled input changed the model.");
                Click(ui, _view!.EditingToggle);
                _stage++;
                break;
            case 27:
                Require(model.ControlsEnabled && _view!.SettingsFields.IsEffectivelyEnabled, "Enabled group did not recover.");
                ui.Focus(editor);
                Require(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab)).Handled && toggle.IsFocused, "Tab did not focus check box.");
                Require(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab, Shift: true)).Handled && editor.IsFocused, "Shift+Tab did not restore editor focus.");
                VerifyTextEditing(ui, editor);
                ui.Focus(toggle);
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Space));
                Require(toggle.IsPressed && model.Enabled, "Check box activated before key release.");
                ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space));
                Require(!model.Enabled && !toggle.IsPressed, "Space did not toggle check box once.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Enter));
                ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Enter));
                Require(model.Enabled, "Enter did not restore check box.");
                var sliderBounds = slider.Bounds;
                Require(ui.HandleInput(new UiInput(InputKind.PointerDown, sliderBounds.X + 10, sliderBounds.Y + 10)).PointerCaptured, "Slider did not capture pointer.");
                Require(ui.HandleInput(new UiInput(InputKind.PointerMove, sliderBounds.X + sliderBounds.Width + 20, sliderBounds.Y)).Handled && model.Volume == 1, "Slider did not clamp upper drag boundary.");
                Require(!ui.HandleInput(new UiInput(InputKind.PointerUp, sliderBounds.X + sliderBounds.Width + 20, sliderBounds.Y)).PointerCaptured, "Slider did not release outside.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
                Require(model.Volume == 1, "Slider arrow exceeded maximum.");
                model.Reset();
                var scrollBounds = _rowScroll!.Bounds;
                Require(ui.HandleInput(new UiInput(InputKind.Wheel, scrollBounds.X + 10, scrollBounds.Y + 10, WheelDelta: -1)).Handled, "Wheel did not bubble to row viewport.");
                _stage++;
                break;
            case 28:
                Require(_rowScroll!.Offset == 72, "Wheel did not move by one logical step.");
                Require(Math.Abs(_view!.Meter.Value - model.Volume) < .001f && _view.Logo.Source?.Size == new Size(32, 32), "Meter/image mismatch.");
                ui.Focus((TextBox)_list.Items[^1].Children[0]);
                _stage++;
                break;
            case 29:
                var lastEditor = (TextBox)_list.Items[^1].Children[0];
                Require(lastEditor.Bounds.Y >= _rowScroll!.ChildClipBounds.Y && lastEditor.Bounds.Y + lastEditor.Bounds.Height <= _rowScroll.ChildClipBounds.Y + _rowScroll.ChildClipBounds.Height,
                    "Focus did not scroll the last editor into view.");
                ui.HandleInput(new UiInput(InputKind.FocusLost));
                Require(!lastEditor.IsFocused && !ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.None)).KeyboardFocused, "FocusLost retained input state.");
                _stage++;
                break;
            default:
                VerifyValues("Алексей", true, .4f);
                Require(!ui.NeedsUpdate, "An unchanged form kept invalidating.");
                _stage++;
                break;
        }
        Record("frame-end");
    }

    public void VerifyDisposed()
    {
        Require(_stage >= 31, "The interaction probe did not finish.");
        Require(_tree != null && _tree.All(element => element.IsDisposed && !element.IsAttached), "The form was not released.");
        Require(_generated.Count > 0 && _generated.All(element => element.IsDisposed && !element.IsAttached), "Generated rows were not released.");
        Require(_branches.Count > 0 && _branches.All(element => element.IsDisposed && !element.IsAttached), "Conditional branches were not released.");
    }

    private void VerifyValues(string name, bool enabled, float volume)
    {
        if (_model!.Name != name || _editor!.Value != name)
            throw new InvalidOperationException($"Name binding mismatch at stage {_stage}, frame {_frames}: " +
                $"expected={JsonSerializer.Serialize(name)}, model={JsonSerializer.Serialize(_model.Name)}, " +
                $"editor={JsonSerializer.Serialize(_editor.Value)}, reason={(_model.Name == _editor.Value ? "unexpected-value" : "binding-out-of-sync")}, focused={_editor.IsFocused}, " +
                $"caret={_editor.CaretIndex}, selection={_editor.SelectionStart}+{_editor.SelectionLength}.");
        Require(_model.Enabled == enabled && _toggle!.IsChecked == enabled, "Toggle binding mismatch.");
        Require(Math.Abs(_model.Volume - volume) < .001f && Math.Abs(_slider!.Value - volume) < .001f, "Volume binding mismatch.");
        var expectedSummary = $"{name} · уведомления: {(enabled ? "вкл." : "выкл.")} · громкость: {volume:P0}";
        Require(_model.Summary == expectedSummary && _summary!.Value == expectedSummary, "Summary binding mismatch.");
    }
    private void VerifyAppearance(bool light, bool compact, bool preserveNeighbor)
    {
        Require(_model!.IsLight == light && _model.IsCompact == compact, "Appearance model mismatch.");
        Require(_view!.Theme == _model.Appearance && _view.Background == _view.Theme.Surface, "Appearance did not reach the root.");
        Require(_editor!.Padding == new Thickness(compact ? 4 : 8) && _view.SettingsFields.Spacing == (compact ? 6 : 12), "Theme spacing did not reach controls.");
        Require(Descendants(_list!).All(element => element.Theme == _view.Theme), "Generated rows lost the theme.");
        Require(_list!.Items.SequenceEqual(_statusRows!) && _rowScroll!.Offset == 36, "Appearance changed row identities or scrolling.");
        if (preserveNeighbor)
            Require(_statusNeighbor!.IsFocused && _statusNeighbor.SelectionStart == _statusSelectionStart && _statusNeighbor.SelectionLength == _statusSelectionLength,
                "Appearance lost neighboring focus or selection.");
    }
    private void VerifyTextEditing(UiSession ui, TextBox editor)
    {
        var previousClipboard = verifyClipboard ? ui.GetClipboardText() : null;
        if (verifyClipboard && previousClipboard != ClipboardFixture)
            throw new InvalidOperationException("OS clipboard smoke requires the explicitly prepared fixture: " + ClipboardFixture);
        try
        {
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
            ui.HandleInput(new UiInput(InputKind.Text, Text: "Latin Привет 🧑‍💻"));
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
            if (verifyClipboard)
            {
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.C, Control: true));
                Require(ui.GetClipboardText() == "🧑‍💻", "Clipboard did not copy a whole grapheme.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.X, Control: true));
                Require(_model!.Name == "Latin Привет ", "Cut did not update the model.");
                ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.V, Control: true));
                Require(_model.Name == "Latin Привет 🧑‍💻", "Paste did not restore the grapheme.");
            }
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Delete));
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Backspace));
            Require(editor.Value == "atin Привет " && _model!.Name == editor.Value, "Delete/Backspace split a grapheme or lost the binding.");
        }
        finally { if (verifyClipboard) ui.SetClipboardText(previousClipboard ?? ""); }
    }
    private void VerifyRetainedRow()
    {
        Require(_list!.Items.Any(node => ReferenceEquals(node, _rowNode)), "The surviving row was recreated.");
        Require(_rowEditor!.IsFocused && _rowEditor.SelectionStart == _rowSelectionStart && _rowEditor.SelectionLength == _rowSelectionLength,
            "A collection change lost row focus or selection.");
    }
    private IEnumerable<Element> FixedTree(Element root)
    {
        var generated = new HashSet<Element>(Descendants(_list!).Skip(1), ReferenceEqualityComparer.Instance);
        generated.UnionWith(Descendants(_statusRegion!).Skip(1));
        return Descendants(root).Where(element => !generated.Contains(element));
    }
    private void VerifyStatusTransition(LoadPhase phase, bool preserveNeighbor)
    {
        Require(_statusRegion!.DisplayedStatus == _model!.LoadStatus && _model.LoadStatus.Phase == phase, "Conditional state mismatch.");
        Require(_previousBranch is { IsDisposed: true, IsAttached: false }, "Replaced conditional branch was not released.");
        Require(_statusRegion.Child is { IsAttached: true, IsDisposed: false }, "New conditional branch was not attached.");
        Require(_list!.Items.SequenceEqual(_statusRows!), "Conditional replacement recreated list rows.");
        Require(_rowScroll!.Offset == 36, "Conditional replacement changed list scrolling.");
        if (preserveNeighbor)
            Require(_statusNeighbor!.IsFocused && _statusNeighbor.SelectionStart == _statusSelectionStart && _statusNeighbor.SelectionLength == _statusSelectionLength,
                "Conditional replacement lost neighboring focus or selection.");
        _previousBranch = _statusRegion.Child;
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
