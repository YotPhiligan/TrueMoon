using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Retained numeric Transform form; borrows its UI model/style and never owns authoring sessions.</summary>
public sealed class EditorTransformView : View<IEditorInspectorViewModel>
{
    private readonly IEditorInspectorViewModel _model;
    private readonly Dictionary<string, TextBox> _fields = [];
    /// <summary>Gets numeric field containers for scoped styling.</summary>
    public static ViewStyleRole<HStack> FieldRole { get; } = new("TransformField");
    /// <summary>Gets field labels.</summary>
    public static ViewStyleRole<Text> LabelRole { get; } = new("TransformLabel");
    /// <summary>Gets numeric text boxes.</summary>
    public static ViewStyleRole<TextBox> InputRole { get; } = new("TransformInput");
    /// <summary>Gets retained numeric inputs by property key after the first attachment.</summary>
    public IReadOnlyDictionary<string, TextBox> Fields => _fields;
    /// <summary>Declares the retained form with inline preview controls.</summary>
    /// <param name="model">Borrowed model.</param><param name="style">Borrowed scoped style.</param>
    public EditorTransformView(IEditorInspectorViewModel model, IViewStyle<EditorTransformView> style)
        : this(model, style, true) { }

    /// <summary>Declares the retained form and its optional inline preview controls.</summary>
    /// <param name="model">Borrowed model.</param>
    /// <param name="style">Borrowed scoped style applied on first attachment.</param>
    /// <param name="showPreviewControls">Whether this form, rather than the host, exposes preview actions and status.</param>
    public EditorTransformView(IEditorInspectorViewModel model, IViewStyle<EditorTransformView> style, bool showPreviewControls)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        Content(model, ui =>
        {
            var form = ui.VStack(ui.Text("Transform · десятичная точка")).WithRole(EditorTransformViewRoles.Form);
            foreach (var field in model.Fields)
            {
                var input = ui.TextBox().WithRole(InputRole).BindValueTwoWay(field, x => x.DisplayValue)
                    .Bind(UiProperties.Enabled, model, x => x.CanEdit)
                    .Bind(UiProperties.AccessibleName, field, x => x.InputAccessibleName)
                    .Bind(UiProperties.HasError, field, x => x.HasError)
                    .Bind(UiProperties.ErrorMessage, field, x => x.Error);
                _fields.Add(field.Key, input.Element);
                form.Element.Add(ui.HStack(
                    ui.Text().WithRole(LabelRole).Bind(Text.ValueProperty, field, x => x.DisplayLabel),
                    input).WithRole(FieldRole));
            }

            form.WithChildren(
                ui.HStack(
                    ui.Button("Применить").Named("Apply").Bind(UiProperties.Enabled, model, x => x.CanApply).OnClick(model.Apply),
                    ui.Button("Отменить").Named("Cancel").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel))
                    .WithRole(EditorTransformViewRoles.ActionRow),
                ui.HStack(
                    ui.Button("Undo").Named("Undo").Bind(UiProperties.Enabled, model, x => x.CanUndo).OnClick(model.Undo),
                    ui.Button("Redo").Named("Redo").Bind(UiProperties.Enabled, model, x => x.CanRedo).OnClick(model.Redo))
                    .WithRole(EditorTransformViewRoles.ActionRow),
                ui.Text().Named("Error").Bind(Text.ValueProperty, model, x => x.ErrorText));
            if (showPreviewControls)
                form.WithChildren(
                    ui.HStack(
                        ui.Button("Preview").Named("ShowPreview").Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview),
                        ui.Button("Стоп").Named("StopPreview").Bind(UiProperties.Enabled, model, x => x.CanStopPreview)
                            .OnClick(() => { _ = Observe(model.StopPreviewAsync()); }))
                        .Named("PreviewActions").WithRole(EditorTransformViewRoles.ActionRow),
                    ui.Text().Named("PreviewStatus").WithRole(EditorTransformViewRoles.PreviewStatus)
                        .Bind(Text.ValueProperty, model, x => x.PreviewText));
            return form;
        });
        this.WithViewStyle(style);
    }
    private static async Task Observe(Task work) { try { await work.ConfigureAwait(false); } catch { /* Error is retained by the preview coordinator. */ } }
    /// <summary>Routes explicit Transform/history shortcuts. Plain Ctrl+Z in a TextBox is left to the focused control.</summary>
    /// <param name="input">Owner-thread normalized input.</param><returns>Whether consumed.</returns>
    public bool HandleShortcut(UiInput input)
    {
        VerifyAccess();
        if (input.Kind != InputKind.KeyDown) return false;
        if (input.Key == UiKey.Escape && _model.CanCancel) { _model.Cancel(); return true; }
        if (!input.Control) return false;
        if (input.Key == UiKey.Enter) { _model.Apply(); return true; }
        if (_fields.Values.Any(f => f.IsFocused)) return false;
        if (input.Key == UiKey.Z) { if (input.Shift) _model.Redo(); else _model.Undo(); return true; }
        if (input.Key == UiKey.Y) { _model.Redo(); return true; }
        return false;
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => HandleShortcut(input);
}
