using System.Linq.Expressions;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;

namespace TrueMoon.Aether.Editor.UI.Editing;

public sealed partial class EditorInspectorView
{
    private const string MeshPrefix = "editor.meshes";
    private readonly MeshContext _meshContext = new();
    private readonly MeshRows<InspectorMesh> _meshItems = new("item");
    private readonly MeshRows<InspectorMeshChoice> _meshSources = new("source");
    private readonly MeshRows<InspectorMeshChoice> _meshGeometries = new("geometry");
    private IEditorMeshesModel? _shownMeshModel;
    private InspectorMesh? _shownMesh;
    private long _shownMeshGeneration, _meshRowGeneration;
    private Border MeshesPart => GetRequiredPart<Border>("MeshesPanel");
    private RichTextEditor MeshName => GetRequiredPart<RichTextEditor>("MeshName");
    private IEditorMeshesModel? CurrentMeshes => ReferenceEquals(DataContext, _presentedContext) &&
        _meshContext.DataContext is { IsAvailable: true } model && ReferenceEquals(DataContext?.Meshes, model) ? model : null;

    private Element BuildMeshes(IUiBuildContext<IEditorInspectorViewModel> ui) => ui.Border(ui.VStack(
        ui.Create(MaterialHeaderFactory.Instance).Named("MeshesHeader").WithRole(HeaderRole).SemanticId(MeshPrefix)
            .Value("Меши").OnClick(() => { if (CurrentMeshes is { } model) model.IsOpen = !model.IsOpen; }),
        ui.RichTextEditor(TextureReadOnly).Named("MeshesStatus").SemanticId(MeshPrefix + ".status")
            .WithRole(MaterialStatusRole).Configure(ConfigureTextureText),
        ui.Text("Есть несохранённая правка меша").WithRole(MetadataRole).SemanticId(MeshPrefix + ".draft")
            .Bind(UiProperties.Visible, _meshContext, model => model.HasDraft),
        ui.VStack(
            ui.HStack(
                MeshCommand(ui, "Новый меш", "new", model => model.CanCreateDraft, model => model.New()).Fill(),
                MeshCommand(ui, "Обновить меши", "reload", model => model.CanReload, model => model.Reload()).Fill())
                .WithRole(RowRole),
            ui.Text("Нет авторских мешей").Named("MeshesEmpty").WithRole(MetadataRole),
            ui.ScrollViewer(ui.VStack().Named("MeshesItems").WithRole(StackRole))
                .Named("MeshesList").WithRole(MaterialListRole).Configure(ConfigureMaterialList),
            ui.VStack(
                ui.RichTextEditor(TextureReadOnly).Named("MeshMetadata").SemanticId(MeshPrefix + ".metadata")
                    .WithRole(TextureMetadataRole).Configure(ConfigureTextureText),
                ui.Text("Имя").WithRole(MetadataRole),
                ui.RichTextEditor(TextureReadOnly).Named("MeshName").WithRole(MaterialNameRole).SemanticId(MeshPrefix + ".name")
                    .Configure(input => { ConfigureTextureText(input); input.AccessibleName = "Имя меша"; input.ColorRole = EditorColorRole.Text; })
                    .BindTwoWay(RichTextEditor.TextProperty, _meshContext, model => model.Name),
                ui.VStack(
                    ui.Text("Зарегистрированные модели").WithRole(MetadataRole),
                    ui.Text("Добавьте модель через «Импортировать модель»").Named("MeshSourcesEmpty").WithRole(MetadataRole),
                    ui.ScrollViewer(ui.VStack().Named("MeshSources").WithRole(StackRole))
                        .Named("MeshSourcesList").WithRole(MaterialListRole).Configure(ConfigureMaterialList),
                    ui.Text("Геометрия модели").WithRole(MetadataRole),
                    ui.Text("Выберите модель и затем геометрию").Named("MeshGeometriesEmpty").WithRole(MetadataRole),
                    ui.ScrollViewer(ui.VStack().Named("MeshGeometries").WithRole(StackRole))
                        .Named("MeshGeometriesList").WithRole(MaterialListRole).Configure(ConfigureMaterialList))
                    .Named("MeshGeometryEditor").WithRole(StackRole),
                MeshCommand(ui, "Создать меш", "create", model => model.CanCreate, model => model.Create()),
                MeshCommand(ui, "Переименовать меш", "rename", model => model.CanRename, model => model.Rename()),
                MeshCommand(ui, "Заменить геометрию", "replace", model => model.CanReplace, model => model.ReplaceGeometry()),
                MeshCommand(ui, "Отменить правку меша", "cancel", model => model.CanCancel, model => model.Cancel()))
                .WithRole(StackRole).Bind(UiProperties.Visible, _meshContext, model => model.HasEditor))
            .WithRole(StackRole).Bind(UiProperties.Visible, _meshContext, model => model.IsOpen))
        .WithRole(StackRole)).Named("MeshesPanel").WithRole(SectionRole).Configure(panel => panel.IsVisible = false);

    private UiControl<Button, IEditorInspectorViewModel> MeshCommand(IUiBuildContext<IEditorInspectorViewModel> ui,
        string label, string suffix, Expression<Func<IEditorMeshesModel, bool>> enabled, Action<IEditorMeshesModel> action) =>
        ui.Button(label).WithRole(ActionRole).SemanticId(MeshPrefix + "." + suffix)
            .Bind(UiProperties.Enabled, _meshContext, enabled).OnClick(() => { if (CurrentMeshes is { } model) action(model); });

    private void PrepareMeshes()
    {
        _meshItems.Bind(GetRequiredPart<VStack>("MeshesItems"), row => row.Id,
            row => $"{row.Name} · {row.Id.ToString("N")[..8]}", () => _meshRowGeneration,
            row => { if (CurrentMeshes is { CanSelect: true } model) model.Select(row.Id); });
        _meshSources.Bind(GetRequiredPart<VStack>("MeshSources"), row => row.Id, row => row.Label, () => _meshRowGeneration,
            row => { if (CurrentMeshes is { CanEditGeometry: true } model) model.SelectSource(row); });
        _meshGeometries.Bind(GetRequiredPart<VStack>("MeshGeometries"), row => row.Id, row => row.Label, () => _meshRowGeneration,
            row => { if (CurrentMeshes is { CanEditGeometry: true } model) model.SelectGeometry(row); });
        MeshesPart.ObservePropertyChanged(_meshContext, (_, change) =>
        {
            if (change.PropertyName is null or "" or nameof(IEditorMeshesModel.IsAvailable) or nameof(IEditorMeshesModel.IsOpen) or
                nameof(IEditorMeshesModel.Items) or nameof(IEditorMeshesModel.Sources) or nameof(IEditorMeshesModel.Geometries) or
                nameof(IEditorMeshesModel.Selected) or nameof(IEditorMeshesModel.SelectedSource) or nameof(IEditorMeshesModel.SelectedGeometry) or
                nameof(IEditorMeshesModel.StatusText) or nameof(IEditorMeshesModel.CanSelect) or nameof(IEditorMeshesModel.CanEditName) or
                nameof(IEditorMeshesModel.CanEditGeometry))
                SynchronizeMeshes();
        }, SynchronizeMeshes, UiSynchronization.AfterTreeChange);
    }

    private void SynchronizeMeshes()
    {
        var model = _meshContext.DataContext;
        var replaced = _shownMeshGeneration != _presentationGeneration || !ReferenceEquals(_shownMeshModel, model);
        if (replaced) _meshRowGeneration++;
        _shownMeshGeneration = _presentationGeneration;
        _shownMeshModel = model;
        MeshesPart.IsVisible = model?.IsAvailable == true;
        GetRequiredPart<ComponentHeader>("MeshesHeader").Expanded = model?.IsOpen == true;
        var status = GetRequiredPart<RichTextEditor>("MeshesStatus");
        status.Text = model?.StatusText.ReplaceLineEndings("\n") ?? "";
        status.IsVisible = status.Text.Length != 0;
        MeshName.Options = model?.CanEditName == true ? TextureEditable : TextureReadOnly;
        _meshItems.Update(model?.Items, model?.Selected?.Id, model?.CanSelect == true, replaced);
        _meshSources.Update(model?.Sources, model?.SelectedSource, model?.CanEditGeometry == true, replaced);
        _meshGeometries.Update(model?.Geometries, model?.SelectedGeometry, model?.CanEditGeometry == true, replaced);
        GetRequiredPart<VStack>("MeshGeometryEditor").IsVisible = model?.IsGeometryAvailable == true;
        GetRequiredPart<Text>("MeshesEmpty").IsVisible = model?.Items.Count == 0 && !status.IsVisible;
        GetRequiredPart<Text>("MeshSourcesEmpty").IsVisible = model?.Sources.Count == 0 && !status.IsVisible;
        GetRequiredPart<Text>("MeshGeometriesEmpty").IsVisible = model?.Geometries.Count == 0 && !status.IsVisible;
        GetRequiredPart<ScrollViewer>("MeshesList").IsVisible = model?.Items.Count > 0;
        GetRequiredPart<ScrollViewer>("MeshSourcesList").IsVisible = model?.Sources.Count > 0;
        GetRequiredPart<ScrollViewer>("MeshGeometriesList").IsVisible = model?.Geometries.Count > 0;
        var row = model?.Selected;
        var metadata = GetRequiredPart<RichTextEditor>("MeshMetadata");
        metadata.IsVisible = row != null;
        if (!ReferenceEquals(row, _shownMesh))
        {
            _shownMesh = row;
            metadata.Text = row == null ? "" : FormattableString.Invariant(
                $"ID: {row.Id:D}\nИсточник: {row.SourcePath}\nГеометрия: {row.SourceLocator}\nMin: {row.Bounds.Minimum.X:R}, {row.Bounds.Minimum.Y:R}, {row.Bounds.Minimum.Z:R}\nMax: {row.Bounds.Maximum.X:R}, {row.Bounds.Maximum.Y:R}, {row.Bounds.Maximum.Z:R}\nUV0: {(row.HasUv0 ? "да" : "нет")}\n") +
                string.Join("\n", row.Slots.Select(slot => $"{slot.Name} · {slot.Id.ToString("N")[..8]}"));
        }
    }

    private bool MeshTextHasFocus() => IsAttached && MeshesPart.IsVisible && MeshName.IsFocused;

    private sealed class MeshContext : IDataContext<IEditorMeshesModel>
    {
        private IEditorMeshesModel? _current;
        public IEditorMeshesModel? DataContext
        {
            get => _current;
            set
            {
                if (ReferenceEquals(_current, value)) return;
                _current = value;
                DataContextChanged?.Invoke();
            }
        }
        public event Action? DataContextChanged;
    }

    private sealed class MeshRows<T>(string kind) where T : class
    {
        private VStack? _container;
        private readonly List<T> _rows = [];
        private IReadOnlyList<T>? _shown;
        private Guid? _selected;

        internal void Bind(VStack container, Func<T, Guid> id, Func<T, string> text, Func<long> generation, Action<T> activate)
        {
            _container = container;
            container.BindItems(_rows, Create, item => (id(item), generation()),
                (row, item) => ((MeshActionRow<T>)row).Present(item, text(item), id(item) == _selected));

            Element Create(T item)
            {
                var created = generation();
                return new MeshActionRow<T>(item, id(item), $"{MeshPrefix}.{kind}.{id(item):N}", text(item), id(item) == _selected,
                    row => { if (created == generation()) activate(row); });
            }
        }

        internal void Update(IReadOnlyList<T>? items, Guid? selection, bool enabled, bool replaced)
        {
            if (_container is not { } container) return;
            container.IsEnabled = enabled;
            var changedSelection = _selected != selection;
            _selected = selection;
            if (!ReferenceEquals(_shown, items))
            {
                _shown = items;
                _rows.Clear();
                if (items != null) _rows.AddRange(items);
                replaced = true;
            }
            if (replaced) container.RefreshItems();
            if (changedSelection)
                foreach (var child in container.Items)
                    if (child is MeshActionRow<T> row) row.Highlight(row.Id == selection);
        }
    }

    private sealed class MeshActionRow<T> : View
    {
        private T _item;
        private string _label;
        private bool _selected;
        private Button? _button;
        internal Guid Id { get; }

        internal MeshActionRow(T item, Guid id, string semanticId, string label, bool selected, Action<T> activate)
        {
            _item = item;
            Id = id;
            _label = label;
            _selected = selected;
            Content(ui => ui.Button().SemanticId(semanticId).OnClick(() => activate(_item)).Configure(button =>
            {
                button.Style = EditorInspectorViewStyle.MaterialRowAction;
                _button = button;
                Present(_item, _label, _selected);
            }));
        }

        internal void Present(T item, string label, bool selected)
        {
            _item = item;
            _label = label.ReplaceLineEndings(" ");
            if (_button != null) _button.Value = _button.AccessibleName = _label;
            Highlight(selected);
        }

        internal void Highlight(bool selected)
        {
            _selected = selected;
            if (_button != null) _button.Tone = selected ? ControlTone.Accent : ControlTone.Neutral;
        }
    }
}
