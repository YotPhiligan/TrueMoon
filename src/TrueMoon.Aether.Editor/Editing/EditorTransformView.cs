using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Retained numeric Transform form; borrows its UI model/style and never owns authoring sessions.</summary>
public sealed class EditorTransformView : View<EditorInspectorViewModel>
{
    private readonly EditorInspectorViewModel _model;
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
    public VStack Form { get; } = VStack.Create();
    /// <summary>Gets the draft action row.</summary>
    public HStack Actions { get; } = HStack.Create();
    /// <summary>Gets the shared history action row.</summary>
    public HStack History { get; } = HStack.Create();
    /// <summary>Gets preview controls.</summary>
    public HStack PreviewActions { get; } = HStack.Create();
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
    public EditorTransformView(EditorInspectorViewModel model, IViewStyle<EditorTransformView> style)
        : this(model, style, true) { }

    /// <summary>Creates the form with optional legacy inline preview controls.</summary>
    public EditorTransformView(EditorInspectorViewModel model, IViewStyle<EditorTransformView> style, bool showPreviewControls)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); _model = model;
        try
        {
            DataContext = model;
            Form.Add(Text.Plain("Transform · десятичная точка"));
            foreach (var field in model.Fields)
            {
                var input = TextBox.Create().WithRole(InputRole).BindTwoWay(TextBox.ValueProperty, field, x => x.Value)
                    .Bind(UiProperties.Enabled, model, x => x.CanEdit).SetValue(UiProperties.AccessibleName, "Transform " + field.Label);
                _fields.Add(field.Key, input);
                Form.Add(HStack.Create().WithRole(FieldRole).WithChildren(Text.Plain(field.Label).WithRole(LabelRole), input));
                Form.Add(Text.Plain().WithRole(ErrorRole).BindText(field, x => x.Error).Bind(UiProperties.Visible, field, x => x.HasError));
            }
            Apply = Button.Simple("Применить").Bind(UiProperties.Enabled, model, x => x.CanApply).OnClick(model.Apply);
            Cancel = Button.Simple("Отменить").Bind(UiProperties.Enabled, model, x => x.CanCancel).OnClick(model.Cancel);
            Undo = Button.Simple("Undo").Bind(UiProperties.Enabled, model, x => x.CanUndo).OnClick(model.Undo);
            Redo = Button.Simple("Redo").Bind(UiProperties.Enabled, model, x => x.CanRedo).OnClick(model.Redo);
            ShowPreview = Button.Simple("Preview").Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview);
            StopPreview = Button.Simple("Стоп").Bind(UiProperties.Enabled, model, x => x.CanStopPreview).OnClick(() => { _ = Observe(model.StopPreviewAsync()); });
            Error = Text.Plain().BindText(model, x => x.ErrorText);
            PreviewStatus = Text.Plain().BindText(model, x => x.PreviewText);
            PreviewActions.IsVisible = showPreviewControls; PreviewStatus.IsVisible = showPreviewControls;
            Actions.WithChildren(Apply, Cancel); History.WithChildren(Undo, Redo); PreviewActions.WithChildren(ShowPreview, StopPreview);
            Form.WithChildren(Actions, History, Error, PreviewActions, PreviewStatus);
            Content(model, _ => Form); this.WithViewStyle(style);
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
