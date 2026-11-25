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
    /// <summary>Gets field-local validation labels.</summary>
    public static ViewStyleRole<Text> ErrorRole { get; } = new("TransformError");
    /// <summary>Gets the stable inputs by property key.</summary>
    public IReadOnlyDictionary<string, TextBox> Fields => _fields;
    /// <summary>Gets the main form stack.</summary>
    public VStack Form { get; } = new VStack();
    /// <summary>Gets the draft action row.</summary>
    public HStack Actions { get; } = new HStack();
    /// <summary>Gets the shared history action row.</summary>
    public HStack History { get; } = new HStack();
    /// <summary>Gets preview controls.</summary>
    public HStack PreviewActions { get; } = new HStack();
    /// <summary>Gets the explicit commit button.</summary>
    public Button Apply { get; }
    /// <summary>Gets the draft cancellation button.</summary>
    public Button Cancel { get; }
    /// <summary>Gets Undo.</summary>
    public Button Undo { get; }
    /// <summary>Gets Redo.</summary>
    public Button Redo { get; }
    /// <summary>Gets preview start/retry.</summary>
    public Button ShowPreview { get; }
    /// <summary>Gets preview stop.</summary>
    public Button StopPreview { get; }
    /// <summary>Gets mutation diagnostics.</summary>
    public Text Error { get; }
    /// <summary>Gets actual preview revision/activity.</summary>
    public Text PreviewStatus { get; }
    /// <summary>Builds the stable form on the UI owner thread.</summary>
    /// <param name="model">Borrowed model.</param><param name="style">Borrowed scoped style.</param>
    public EditorTransformView(IEditorInspectorViewModel model, IViewStyle<EditorTransformView> style)
        : this(model, style, true) { }

    /// <summary>Creates the form with optional legacy inline preview controls.</summary>
    public EditorTransformView(IEditorInspectorViewModel model, IViewStyle<EditorTransformView> style, bool showPreviewControls)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        try
        {
            DataContext = model;
            Form.Add(new Text("Transform · десятичная точка"));
            foreach (var field in model.Fields)
            {
                var input = new TextBox().WithRole(InputRole).BindTwoWay(TextBox.ValueProperty, field, x => x.DisplayValue)
                    .Bind(UiProperties.Enabled, model, x => x.CanEdit).Bind(UiProperties.AccessibleName, field, x => x.InputAccessibleName)
                    .Bind(UiProperties.HasError, field, x => x.HasError).Bind(UiProperties.ErrorMessage, field, x => x.Error);
                _fields.Add(field.Key, input);
                Form.Add(new HStack().WithRole(FieldRole).WithChildren(new Text().WithRole(LabelRole).BindText(field, x => x.DisplayLabel), input));
            }
            Apply = new Button("Применить").Bind(UiProperties.Enabled, model, x => x.CanApply).OnClick(model.Apply);
            Cancel = new Button("Отменить").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel);
            Undo = new Button("Undo").Bind(UiProperties.Enabled, model, x => x.CanUndo).OnClick(model.Undo);
            Redo = new Button("Redo").Bind(UiProperties.Enabled, model, x => x.CanRedo).OnClick(model.Redo);
            ShowPreview = new Button("Preview").Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview);
            StopPreview = new Button("Стоп").Bind(UiProperties.Enabled, model, x => x.CanStopPreview).OnClick(() => { _ = Observe(model.StopPreviewAsync()); });
            Error = new Text().BindText(model, x => x.ErrorText);
            PreviewStatus = new Text().BindText(model, x => x.PreviewText);
            PreviewActions.IsVisible = showPreviewControls; PreviewStatus.IsVisible = showPreviewControls;
            Actions.WithChildren(Apply, Cancel); History.WithChildren(Undo, Redo); PreviewActions.WithChildren(ShowPreview, StopPreview);
            Form.WithChildren(Actions, History, Error, PreviewActions, PreviewStatus);
            SetContent(Form); this.WithViewStyle(style);
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
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
