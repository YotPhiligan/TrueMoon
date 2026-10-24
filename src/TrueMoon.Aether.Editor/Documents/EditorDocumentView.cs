using System.Linq.Expressions;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Retained document path/actions and an inline Save/Discard/Cancel decision, borrowing model/style.</summary>
public sealed class EditorDocumentView : View<EditorDocumentViewModel>
{
    private readonly EditorDocumentViewModel _model;
    private readonly Dictionary<DocumentCommand, Button> _commands = [];
    private bool _shown;
    private Element? _previousFocus;
    /// <summary>Gets the path/action row.</summary>
    public HStack Actions { get; } = HStack.Create();
    /// <summary>Gets the Open/SaveAs path editor.</summary>
    public TextBox PathInput { get; }
    /// <summary>Gets the independent first-save path editor.</summary>
    public TextBox SavePathInput { get; }
    /// <summary>Gets the persistent dirty/activity line.</summary>
    public Text Title { get; }
    /// <summary>Gets the project scene-root line.</summary>
    public Text ScenesRoot { get; }
    /// <summary>Gets operation diagnostics.</summary>
    public Text Error { get; }
    /// <summary>Gets the inline transition decision container.</summary>
    public Border Decision { get; }
    /// <summary>Gets decision action buttons.</summary>
    public HStack Choices { get; } = HStack.Create();
    /// <summary>Gets the safe default decision button.</summary>
    public Button CancelChoice { get; }
    /// <summary>Gets the non-undoable discard decision button.</summary>
    public Button DiscardChoice { get; }
    /// <summary>Gets the save decision button.</summary>
    public Button SaveChoice { get; }
    /// <summary>Gets action buttons by the same command identity used by keyboard input.</summary>
    public IReadOnlyDictionary<DocumentCommand, Button> Commands => _commands;

    /// <summary>Creates the view on its UI owner thread. Parent/window owns the product.</summary>
    /// <param name="model">Borrowed UI model.</param><param name="style">Borrowed appearance declaration.</param>
    /// <param name="compactHeader">Hides standalone actions when the window provides their shared menu commands.</param>
    public EditorDocumentView(EditorDocumentViewModel model, IViewStyle<EditorDocumentView> style, bool compactHeader = false)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style);
        _model = model;
        try
        {
            model.Refresh(); DataContext = model;
            PathInput = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, x => x.PathDraft)
                .Bind(UiProperties.Enabled, model, x => x.CanEditPath).SetValue(UiProperties.AccessibleName, "Путь сцены");
            SavePathInput = TextBox.Create().BindTwoWay(TextBox.ValueProperty, model, x => x.SavePathDraft)
                .SetValue(UiProperties.AccessibleName, "Сохранить новую сцену как");
            Title = Text.Plain().BindText(model, x => x.Title);
            ScenesRoot = Text.Plain().BindText(model, x => x.ScenesRoot);
            Error = Text.Plain().BindText(model, x => x.ErrorText);
            Actions.Add(Text.Plain("Сцена:")); Actions.Add(PathInput);
            Add(DocumentCommand.New, "Новая", x => x.CanNew);
            Add(DocumentCommand.Open, "Открыть", x => x.CanOpen);
            Add(DocumentCommand.Save, "Сохранить", x => x.CanSave);
            Add(DocumentCommand.SaveAs, "Сохранить как", x => x.CanSaveAs);
            Add(DocumentCommand.Close, "Закрыть", x => x.CanClose);
            Actions.Add(Button.Simple("Отмена").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel));
            if (compactHeader) foreach (var button in Actions.Children.OfType<Button>()) button.IsVisible = false;
            SaveChoice = Button.Simple("Сохранить").OnClick(() => model.Choose(DocumentCloseChoice.Save));
            DiscardChoice = Button.Simple("Не сохранять").OnClick(() => model.Choose(DocumentCloseChoice.Discard));
            CancelChoice = Button.Simple("Отмена").OnClick(() => model.Choose(DocumentCloseChoice.Cancel));
            Choices.Add(SaveChoice); Choices.Add(DiscardChoice); Choices.Add(CancelChoice);
            var firstSave = HStack.Create().WithChildren(Text.Plain("Путь сохранения:"), SavePathInput)
                .Bind(UiProperties.Visible, model, x => x.NeedsSavePath);
            Decision = Border.Create().Bind(UiProperties.Visible, model, x => x.AwaitingDecision).Content(
                VStack.Create().WithChildren(Text.Plain().BindText(model, x => x.DecisionText), firstSave, Choices));
            Content(model, _ => VStack.Create().WithChildren(Actions, Title, ScenesRoot, Error, Decision));
            this.WithViewStyle(style);
            void Add(DocumentCommand command, string label, Expression<Func<EditorDocumentViewModel, bool>> enabled)
            {
                var button = Button.Simple(label).Bind(UiProperties.Enabled, model, enabled).OnClick(() => model.Execute(command));
                _commands.Add(command, button); Actions.Add(button);
            }
        }
        catch (Exception error)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }
    }

    /// <summary>Routes document shortcuts through the same model actions as buttons.</summary>
    /// <param name="input">Normalized owner-thread input.</param><returns>Whether a document shortcut was consumed.</returns>
    public bool HandleShortcut(UiInput input)
    {
        VerifyAccess();
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape && _model.AwaitingDecision) { _model.Choose(DocumentCloseChoice.Cancel); return true; }
        if (!input.Control) return false;
        if (input.Key == UiKey.S)
        {
            if (_model.AwaitingDecision) _model.Choose(DocumentCloseChoice.Save);
            else _model.Execute(input.Shift ? DocumentCommand.SaveAs : DocumentCommand.Save);
            return true;
        }
        if (input.Key == UiKey.W) { if (!_model.AwaitingDecision) _model.Execute(DocumentCommand.Close); return true; }
        return false;
    }

    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => HandleShortcut(input);

    /// <summary>Moves focus to safe Cancel after bindings show a decision and restores live prior focus on completion.</summary>
    /// <param name="context">Borrowed current session's focus service, used only for this call.</param>
    public void SynchronizeFocus(IInputContext context)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(context);
        if (Decision.IsVisible != _model.AwaitingDecision) return; // Wait for queued bindings; never focus hidden controls.
        if (_model.AwaitingDecision && !_shown) { _shown = true; context.Focus(CancelChoice); }
        else if (!_model.AwaitingDecision && _shown)
        {
            var target = _previousFocus is { IsDisposed: false, IsAttached: true, IsEffectivelyEnabled: true, IsVisible: true } ? _previousFocus : PathInput;
            if (!target.IsEffectivelyEnabled || !target.IsVisible) return; // Accepted IO may still be draining.
            context.Focus(target); _shown = false;
            _previousFocus = null;
        }
        if (!_shown)
        {
            Element root = this; while (root.Parent != null) root = root.Parent;
            _previousFocus = FindFocus(root) ?? _previousFocus;
        }
    }
    private static Element? FindFocus(Element element)
    {
        if (element.IsFocused) return element;
        foreach (var child in element.Children) if (FindFocus(child) is { } focused) return focused;
        return null;
    }
}
