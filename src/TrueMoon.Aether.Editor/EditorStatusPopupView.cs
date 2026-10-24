using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor;

/// <summary>Chooses the popup heading; both popups expose full details when compact segments overflow.</summary>
public sealed record EditorStatusPopupArguments(bool Preview);

/// <summary>Overlay-owned live details and preview actions over borrowed status presentation.</summary>
public sealed class EditorStatusPopupView : View<EditorStatusBarViewModel>
{
    /// <summary>Popup content.</summary>
    public VStack Body { get; } = new();
    /// <summary>Popup heading.</summary>
    public Text Title { get; }
    /// <summary>Selectable, read-only full diagnostics.</summary>
    public RichTextEditor Error { get; }
    /// <summary>Existing preview start/retry action.</summary>
    public Button StartPreview { get; }
    /// <summary>Existing preview stop action.</summary>
    public Button StopPreview { get; }
    /// <summary>Preview action row.</summary>
    public HStack PreviewActions { get; } = new();
    /// <summary>Gets actual preview revisions.</summary>
    public Text PreviewDetails { get; }
    /// <summary>Creates a transient view. OverlayHost owns it; the model/style stay App-owned.</summary>
    public EditorStatusPopupView(EditorStatusBarViewModel model, IViewStyle<EditorStatusPopupView> style, EditorStatusPopupArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            Title = new(arguments.Preview ? "Edit Preview" : "Сведения о редакторе");
            Error = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
            Error.Bind(RichTextEditor.TextProperty, model, x => x.ErrorDetails).Bind(UiProperties.Visible, model, x => x.HasError);
            StartPreview = Button.Simple("Запустить / обновить Preview").Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview);
            StopPreview = Button.Simple("Остановить Preview").Bind(UiProperties.Enabled, model, x => x.CanStopPreview)
                .OnClick(() => { _ = Observe(model.StopPreviewAsync()); });
            PreviewDetails = Text.Plain().BindText(model, x => x.PreviewDetails);
            PreviewActions.WithChildren(StartPreview, StopPreview);
            Body.WithChildren(Title, Row(x => x.Summary),
                Row(x => x.RuntimeDetails), Row(x => x.OperationDetails),
                Row(x => x.EntityText), Row(x => x.PreviewSummary),
                PreviewDetails, PreviewActions, Error);
            MaxHeight = 420;
            Content(model, _ => new ScrollViewer().Content(Body)); this.WithViewStyle(style);
            Text Row(System.Linq.Expressions.Expression<Func<EditorStatusBarViewModel, string>> value) =>
                Text.Plain().WithRole(EditorStatusPopupViewStyle.Row).BindText(model, value);
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    /// <summary>Focuses a safe initial target without executing an action.</summary>
    public void FocusFirst(IInputContext context)
    {
        // Details remain keyboard reachable even when both preview commands are unavailable.
        if (StartPreview.IsEffectivelyEnabled) context.Focus(StartPreview);
        else if (StopPreview.IsEffectivelyEnabled) context.Focus(StopPreview);
    }
    /// <inheritdoc />
    protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
    /// <inheritdoc />
    protected override Color ThemeBackground => Backdrop == null ? Theme.Surface : Color.Transparent;
    private static async Task Observe(Task pending) { try { await pending.ConfigureAwait(false); } catch { /* The coordinator retains the diagnostic. */ } }
}
