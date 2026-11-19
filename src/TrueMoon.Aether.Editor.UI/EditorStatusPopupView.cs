using System.ComponentModel;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Chooses the popup heading; both popups expose full details when compact segments overflow.</summary>
public sealed record EditorStatusPopupArguments(bool Preview);

/// <summary>Overlay-owned live details and preview actions over borrowed status presentation.</summary>
public sealed class EditorStatusPopupView : View<IEditorStatusBarViewModel>
{
    private readonly IEditorStatusBarViewModel _model;
    private readonly Dictionary<(Guid Opening, Guid Source, string Name), (Button Button, EditorModelReimportAction Target)> _reimportButtons = [];

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
    /// <summary>Gets the selected model import statuses.</summary>
    public Text ModelImportDetails { get; }
    /// <summary>Actions for the selected models configured for reimport.</summary>
    public VStack ReimportActions { get; } = new();
    /// <summary>Creates a transient view. OverlayHost owns it; the model/style stay App-owned.</summary>
    public EditorStatusPopupView(IEditorStatusBarViewModel model, IViewStyle<EditorStatusPopupView> style, EditorStatusPopupArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(arguments);
        _model = model;
        try
        {
            Title = new(arguments.Preview ? "Edit Preview" : "Сведения о редакторе");
            Error = new(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true });
            Error.Bind(RichTextEditor.TextProperty, model, x => x.ErrorDetails).Bind(UiProperties.Visible, model, x => x.HasError);
            StartPreview = Button.Simple("Запустить / обновить Preview").Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview);
            StopPreview = Button.Simple("Остановить Preview").Bind(UiProperties.Enabled, model, x => x.CanStopPreview)
                .OnClick(() => { _ = Observe(model.StopPreviewAsync()); });
            PreviewDetails = Text.Plain().BindText(model, x => x.PreviewDetails);
            ModelImportDetails = Row(x => x.ModelImportDetails).Bind(UiProperties.Visible, model, x => x.HasModelImports);
            PreviewActions.WithChildren(StartPreview, StopPreview);
            Body.WithChildren(Title, Row(x => x.Summary),
                Row(x => x.RuntimeDetails), Row(x => x.OperationDetails),
                Row(x => x.SourceSummary), Row(x => x.SourceDetails),
                ModelImportDetails, ReimportActions,
                Row(x => x.EntityText), Row(x => x.PreviewSummary),
                PreviewDetails, PreviewActions, Error);
            MaxHeight = 420;
            Content(model, _ => new ScrollViewer().Content(Body)); this.WithViewStyle(style);
            SynchronizeReimport();
            model.PropertyChanged += ModelChanged;
            Text Row(System.Linq.Expressions.Expression<Func<IEditorStatusBarViewModel, string>> value) =>
                Text.Plain().WithRole(EditorStatusPopupViewStyle.Row).BindText(model, value);
        }
        catch (Exception original)
        {
            try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IEditorStatusBarViewModel.ModelReimports))
            Dispatch(() => { if (!IsDisposed) SynchronizeReimport(); });
    }

    private void SynchronizeReimport()
    {
        var targets = _model.ModelReimports;
        foreach (var key in _reimportButtons.Keys.Where(key => !targets.Any(action => Key(action) == key)).ToArray())
        {
            var button = _reimportButtons[key].Button;
            ReimportActions.Items.Remove(button);
            _reimportButtons.Remove(key);
            button.Dispose();
        }
        foreach (var target in targets)
        {
            var key = Key(target);
            if (!_reimportButtons.TryGetValue(key, out var entry))
            {
                var button = Button.Simple($"Переимпортировать «{target.Name}»").WithRole(EditorStatusPopupViewStyle.Reimport);
                button.OnClick(() => _model.ReimportModel(_reimportButtons[key].Target));
                ReimportActions.Items.Add(button);
                entry = (button, target);
            }
            _reimportButtons[key] = (entry.Button, target);
            entry.Button.IsEnabled = target.Enabled;
        }
        ReimportActions.IsVisible = targets.Count > 0;

        static (Guid Opening, Guid Source, string Name) Key(EditorModelReimportAction action) =>
            (action.OpeningId, action.SourceId, action.Name);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _model.PropertyChanged -= ModelChanged;
        base.Dispose();
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
