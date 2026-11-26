using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
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
    private readonly Dictionary<(Guid Opening, Guid Source), (HStack Layout, CheckBox Check, Button Place, EditorProjectModelRow Row)> _catalogRows = [];

    /// <summary>Creates a transient view. OverlayHost owns it; the model/style stay App-owned.</summary>
    public EditorStatusPopupView(IEditorStatusBarViewModel model, IViewStyle<EditorStatusPopupView> style, EditorStatusPopupArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(model); ArgumentNullException.ThrowIfNull(style); ArgumentNullException.ThrowIfNull(arguments);
        _model = model;
        MaxHeight = 420;
        Content(model, ui => ui.ScrollViewer(ui.VStack(
            ui.Text(arguments.Preview ? "Edit Preview" : "Сведения о редакторе").Named("Title"),
            ui.Button("Модели проекта").Named("ReadModelCatalog")
                .Bind(UiProperties.Visible, model, x => x.HasModelCatalog)
                .Bind(UiProperties.Enabled, model, x => x.CanReadModelCatalog).OnClick(model.ReadModelCatalog),
            Row(ui, x => x.ModelCatalogMessage).Named("ModelCatalogMessage"),
            ui.ScrollViewer(ui.VStack().Named("ModelCatalogRows")).Named("ModelCatalogList")
                .Configure(list => list.MaxHeight = 150),
            Row(ui, x => x.Summary), Row(ui, x => x.RuntimeDetails), Row(ui, x => x.OperationDetails),
            Row(ui, x => x.SourceSummary), Row(ui, x => x.SourceDetails),
            Row(ui, x => x.ModelImportDetails).Named("ModelImportDetails").Bind(UiProperties.Visible, model, x => x.HasModelImports),
            ui.VStack().Named("ReimportActions"),
            Row(ui, x => x.EntityText), Row(ui, x => x.PreviewSummary), Row(ui, x => x.PreviewDetails),
            ui.HStack(
                ui.Button("Запустить / обновить Preview").Named("StartPreview")
                    .Bind(UiProperties.Enabled, model, x => x.CanPreview).OnClick(model.ShowPreview),
                ui.Button("Остановить Preview").Named("StopPreview")
                    .Bind(UiProperties.Enabled, model, x => x.CanStopPreview).OnClick(() => { _ = Observe(model.StopPreviewAsync()); }))
                .WithRole(EditorStatusPopupViewRoles.PreviewActions),
            ui.RichTextEditor(new RichTextEditorOptions { IsReadOnly = true, WordWrap = true }).Named("Error")
                .Bind(RichTextEditor.TextProperty, model, x => x.ErrorDetails).Bind(UiProperties.Visible, model, x => x.HasError))
            .WithRole(EditorStatusPopupViewRoles.Body)));
        this.WithViewStyle(style);

        UiControl<Text, IEditorStatusBarViewModel> Row(IUiBuildContext<IEditorStatusBarViewModel> ui,
            System.Linq.Expressions.Expression<Func<IEditorStatusBarViewModel, string>> value) =>
            ui.Text().WithRole(EditorStatusPopupViewStyle.Row).Bind(Text.ValueProperty, model, value);
    }

    /// <inheritdoc />
    protected override void OnContentPrepared() =>
        this.ObservePropertyChanged(_model, ModelChanged, SynchronizeRows, UiSynchronization.AfterTreeChange);

    private void SynchronizeRows()
    {
        SynchronizeReimport();
        SynchronizeCatalog();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)) { SynchronizeRows(); return; }
        if (args.PropertyName == nameof(IEditorStatusBarViewModel.ModelReimports)) SynchronizeReimport();
        if (args.PropertyName is nameof(IEditorStatusBarViewModel.ProjectModels) or nameof(IEditorStatusBarViewModel.ModelCatalogMessage))
            SynchronizeCatalog();
    }

    private void SynchronizeCatalog()
    {
        var catalog = this.ModelCatalogRows;
        var rows = _model.ProjectModels;
        var active = rows.Select(row => (row.OpeningId, row.SourceId)).ToHashSet();
        foreach (var key in _catalogRows.Keys.Where(key => !active.Contains(key)).ToArray())
        {
            var layout = _catalogRows[key].Layout;
            catalog.Items.Remove(layout);
            _catalogRows.Remove(key);
            layout.Dispose();
        }
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var key = (row.OpeningId, row.SourceId);
            if (!_catalogRows.TryGetValue(key, out var entry))
            {
                var check = new CheckBox().WithRole(EditorStatusPopupViewStyle.CatalogChoice);
                check.OnClick(() =>
                {
                    if (!_catalogRows.TryGetValue(key, out var target)) return;
                    check.IsChecked = target.Row.Selected;
                    _model.SelectModel(target.Row);
                });
                var place = new Button("Добавить в сцену").WithRole(EditorStatusPopupViewStyle.CatalogPlace);
                place.OnClick(() =>
                {
                    if (_catalogRows.TryGetValue(key, out var target)) _model.PlaceModel(target.Row);
                });
                var layout = new HStack().WithRole(EditorStatusPopupViewStyle.CatalogRow).WithChildren(place, check);
                entry = (layout, check, place, row);
                catalog.Items.Insert(index, layout);
            }
            else if (!ReferenceEquals(catalog.Children[index], entry.Layout))
                catalog.Items.Move(catalog.Items.IndexOf(entry.Layout), index);
            _catalogRows[key] = (entry.Layout, entry.Check, entry.Place, row);
            entry.Check.Value = row.SourcePath.Length == 0 ? row.Name : row.Name + " · " + row.SourcePath;
            entry.Check.AccessibleName = entry.Check.Value;
            entry.Check.IsChecked = row.Selected;
            entry.Check.IsEnabled = row.CanChange;
            entry.Place.AccessibleName = $"Добавить «{row.Name}» в сцену";
            entry.Place.IsEnabled = row.CanChange && row.SelectedGeneration.HasValue;
        }
        this.ModelCatalogList.IsVisible = rows.Count > 0;
        this.ModelCatalogMessage.IsVisible = _model.ModelCatalogMessage.Length > 0;
    }

    private void SynchronizeReimport()
    {
        var actions = this.ReimportActions;
        var targets = _model.ModelReimports;
        foreach (var key in _reimportButtons.Keys.Where(key => !targets.Any(action => Key(action) == key)).ToArray())
        {
            var button = _reimportButtons[key].Button;
            actions.Items.Remove(button);
            _reimportButtons.Remove(key);
            button.Dispose();
        }
        foreach (var target in targets)
        {
            var key = Key(target);
            if (!_reimportButtons.TryGetValue(key, out var entry))
            {
                var button = new Button($"Переимпортировать «{target.Name}»").WithRole(EditorStatusPopupViewStyle.Reimport);
                button.OnClick(() => _model.ReimportModel(_reimportButtons[key].Target));
                actions.Items.Add(button);
                entry = (button, target);
            }
            _reimportButtons[key] = (entry.Button, target);
            entry.Button.IsEnabled = target.Enabled;
        }
        actions.IsVisible = targets.Count > 0;

        static (Guid Opening, Guid Source, string Name) Key(EditorModelReimportAction action) =>
            (action.OpeningId, action.SourceId, action.Name);
    }

    /// <summary>Focuses a safe initial target without executing an action.</summary>
    public void FocusFirst(IInputContext context)
    {
        // Details remain keyboard reachable even when both preview commands are unavailable.
        if (!context.TryFocus(this.StartPreview)) context.TryFocus(this.StopPreview);
    }
    /// <inheritdoc />
    protected override BackdropMaterial? ThemeBackdrop => Theme.MenuBackdrop;
    /// <inheritdoc />
    protected override Color ThemeBackground => Backdrop == null ? Theme.Surface : Color.Transparent;
    private static async Task Observe(Task pending) { try { await pending.ConfigureAwait(false); } catch { /* The coordinator retains the diagnostic. */ } }
}
