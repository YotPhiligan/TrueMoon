using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.Linq.Expressions;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Documents;

/// <summary>Retained document path/actions and an inline Save/Discard/Cancel decision, borrowing model/style.</summary>
public sealed class EditorDocumentView : View<IEditorDocumentViewModel>, IUiInputObserver
{
    private readonly IEditorDocumentViewModel _model;
    private bool _shown;
    private Element? _previousFocus;
    private bool _toolsShown, _pathShown;
    /// <summary>Whether idle tools collapse into the window header instead of reserving a document toolbar.</summary>
    public bool CompactHeader { get; }

    /// <summary>Creates the view on its UI owner thread. Parent/window owns the product.</summary>
    /// <param name="model">Borrowed UI model.</param><param name="style">Borrowed appearance declaration.</param>
    /// <param name="compactHeader">Hides standalone actions when the window provides their shared menu commands.</param>
    public EditorDocumentView(IEditorDocumentViewModel model, IViewStyle<EditorDocumentView> style, bool compactHeader = false)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style);
        _model = model;
        CompactHeader = compactHeader;
        try
        {
            model.Refresh();
            if (compactHeader)
            {
                IsVisible = model.IsToolsVisible;
                this.Bind(UiProperties.Visible, model, x => x.IsToolsVisible);
            }

            Content(model, ui =>
            {
                var path = ui.TextBox().Named("PathInput").SemanticId("editor.document.path")
                    .BindValueTwoWay(model, x => x.RequestedPath)
                    .Bind(UiProperties.Enabled, model, x => x.CanEditPath)
                    .Bind(UiProperties.AccessibleName, model, x => x.PathInputLabel);
                var actions = (compactHeader
                    ? ui.HStack(path,
                        ui.Button("Продолжить").Named("ConfirmPath").SemanticId("editor.document.confirm-path")
                            .Bind(UiProperties.Enabled, model, x => x.CanConfirmPath).OnClick(model.ConfirmPath),
                        ui.Button("Отмена").Named("CancelPath").OnClick(model.CancelPath))
                        .Bind(UiProperties.Visible, model, x => x.IsPathPromptOpen)
                    : ui.HStack(ui.Text("Сцена:"), path,
                        Command(ui, DocumentCommand.New, "Новая", x => x.CanNew),
                        Command(ui, DocumentCommand.Open, "Открыть", x => x.CanOpen),
                        Command(ui, DocumentCommand.Save, "Сохранить", x => x.CanSave),
                        Command(ui, DocumentCommand.SaveAs, "Сохранить как", x => x.CanSaveAs),
                        Command(ui, DocumentCommand.Close, "Закрыть", x => x.CanClose),
                        ui.Button("Отмена").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel)))
                    .Named("Actions");
                var title = ui.Text().Named("Title")
                    .Bind(Text.ValueProperty, model, compactHeader ? x => x.PathPromptText : x => x.Title);
                var content = ui.VStack(
                    compactHeader ? title.Element : actions.Element,
                    compactHeader ? actions.Element : title.Element,
                    ui.Text().WithRole(EditorDocumentViewRoles.ScenesRoot).Bind(Text.ValueProperty, model, x => x.PathHint),
                    ui.Text().Named("Error").Bind(Text.ValueProperty, model, x => x.ErrorText),
                    ui.Border().Named("Decision").Content(ui.VStack(
                        ui.Text().Bind(Text.ValueProperty, model, x => x.DecisionText),
                        ui.HStack(ui.Text("Путь сохранения:"),
                            ui.TextBox().Named("SavePathInput").BindValueTwoWay(model, x => x.SavePathDraft)
                                .SetValue(UiProperties.AccessibleName, "Сохранить новую сцену как"))
                            .Bind(UiProperties.Visible, model, x => x.NeedsSavePath),
                        ui.HStack(
                            ui.Button("Сохранить").Named("SaveChoice").OnClick(() => model.Choose(EditorDocumentCloseChoice.Save)),
                            ui.Button("Не сохранять").Named("DiscardChoice").OnClick(() => model.Choose(EditorDocumentCloseChoice.Discard)),
                            ui.Button("Отмена").Named("CancelChoice").OnClick(() => model.Choose(EditorDocumentCloseChoice.Cancel)))
                            .WithRole(EditorDocumentViewRoles.Choices)))
                        .Bind(UiProperties.Visible, model, x => x.AwaitingDecision));
                if (compactHeader)
                    content.WithChildren(ui.Button("Закрыть").Named("DismissTools")
                        .Bind(UiProperties.Visible, model, x => x.CanDismissTools).OnClick(model.CancelPath));
                return content;
            });
            this.WithViewStyle(style);
        }
        catch (Exception error)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            throw;
        }

        Button Command(IUiBuildContext<IEditorDocumentViewModel> ui, DocumentCommand command, string label,
            Expression<Func<IEditorDocumentViewModel, bool>> enabled)
            => ui.Button(label).SemanticId($"editor.document.command.{command}")
                .Bind(UiProperties.Enabled, model, enabled).OnClick(() => model.Execute(command));
    }

    /// <summary>Routes document shortcuts through the same model actions as buttons.</summary>
    /// <param name="input">Normalized owner-thread input.</param><returns>Whether a document shortcut was consumed.</returns>
    public bool HandleShortcut(UiInput input)
    {
        VerifyAccess();
        if (input.Kind != InputKind.KeyDown) return false;
        if (CompactHeader && _model.IsPathPromptOpen && input.Key == UiKey.Enter && !input.Control)
        { _model.ConfirmPath(); return true; }
        if (input.Key == UiKey.Escape)
        {
            if (_model.AwaitingDecision) { _model.Choose(EditorDocumentCloseChoice.Cancel); return true; }
            if (CompactHeader && _model.IsToolsVisible) { _model.CancelPath(); return true; }
        }
        if (!input.Control) return false;
        if (input.Key == UiKey.S)
        {
            if (_model.AwaitingDecision) _model.Choose(EditorDocumentCloseChoice.Save);
            else if (CompactHeader) _model.Request(input.Shift ? DocumentCommand.SaveAs : DocumentCommand.Save);
            else _model.Execute(input.Shift ? DocumentCommand.SaveAs : DocumentCommand.Save);
            return true;
        }
        if (input.Key == UiKey.W) { if (!_model.AwaitingDecision) _model.Execute(DocumentCommand.Close); return true; }
        return false;
    }

    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => HandleShortcut(input);
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (!CompactHeader || !_model.IsToolsVisible || !IsVisible) return false;
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) return HandleShortcut(input);
        if (input.Kind == InputKind.PointerDown && !Bounds.Contains(input.X, input.Y))
        {
            if (!_model.AwaitingDecision) _model.CancelPath();
            return true;
        }
        return false;
    }

    /// <summary>Moves focus to safe Cancel after bindings show a decision and restores live prior focus on completion.</summary>
    /// <param name="context">Borrowed current session's focus service, used only for this call.</param>
    public void SynchronizeFocus(IInputContext context)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(context);
        if (!IsAttached) return;
        if (CompactHeader)
        {
            if (IsVisible != _model.IsToolsVisible) return;
            if (_model.IsToolsVisible && !_toolsShown) { _previousFocus = context.FocusedElement; _toolsShown = true; }
            if (_model.AwaitingDecision && !_shown && context.TryFocus(this.CancelChoice))
            { _shown = true; _pathShown = false; }
            else if (_model.IsPathPromptOpen && !_pathShown && context.TryFocus(this.PathInput))
            { _pathShown = true; _shown = false; }
            else if (!_model.IsToolsVisible && _toolsShown)
            {
                var target = _previousFocus; _previousFocus = null; _toolsShown = _pathShown = _shown = false;
                if (target != null) context.TryFocus(target);
            }
            return;
        }
        if (this.Decision.IsVisible != _model.AwaitingDecision) return; // Wait for queued visibility binding.
        if (_model.AwaitingDecision && !_shown) _shown = context.TryFocus(this.CancelChoice);
        else if (!_model.AwaitingDecision && _shown)
        {
            if (!(_previousFocus is { } target && context.TryFocus(target)) && !context.TryFocus(this.PathInput)) return;
            _shown = false;
            _previousFocus = null;
        }
        if (!_shown) _previousFocus = context.FocusedElement ?? _previousFocus;
    }
}
