using System.ComponentModel;
using System.Linq.Expressions;
using TrueMoon.Argentis;
using TrueMoon.Argentis.Bindings;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;

namespace TrueMoon.Aether.Editor.UI.Editing;

public sealed partial class EditorInspectorView
{
    private const string MaterialPrefix = "editor.materials";
    private readonly MaterialContext _materialContext = new();
    private readonly List<InspectorRenderAssetChoice> _materialRows = [];
    private readonly List<InspectorRenderAssetChoice> _textureRows = [];
    private IReadOnlyList<InspectorRenderAssetChoice>? _shownMaterialRows, _shownTextureRows;
    private Guid? _shownMaterialId, _focusMaterialId;
    private IEditorMaterialsModel? _focusMaterialModel;
    private bool _requestMaterialFocus;
    private MaterialFieldHistory[] _materialFields = [];
    private MaterialFieldHistory? _materialInputField;
    private bool _clearMaterialInputQueued;

    internal static ViewStyleRole<RichTextEditor> MaterialStatusRole { get; } = new("InspectorMaterialStatus");
    internal static ViewStyleRole<RichTextEditor> MaterialNameRole { get; } = new("InspectorMaterialName");
    internal static ViewStyleRole<ScrollViewer> MaterialListRole { get; } = new("InspectorMaterialList");
    private static readonly RichTextEditorOptions MaterialNameEditable = new() { WordWrap = true };
    private static readonly RichTextEditorOptions MaterialNameReadOnly = new() { WordWrap = true, IsReadOnly = true };

    private Border MaterialsPart => GetRequiredPart<Border>("MaterialsPanel");
    private ComponentHeader MaterialHeader => GetRequiredPart<ComponentHeader>("MaterialsHeader");
    private RichTextEditor MaterialName => GetRequiredPart<RichTextEditor>("MaterialName");

    private Element BuildMaterials(IUiBuildContext<IEditorInspectorViewModel> ui) => ui.Border(ui.VStack(
        ui.Create(MaterialHeaderFactory.Instance).Named("MaterialsHeader")
            .WithRole(HeaderRole).SemanticId(MaterialPrefix).Value("Материалы")
            .OnClick(() => { if (CurrentMaterials is { } model) model.IsOpen = !model.IsOpen; }),
        ui.RichTextEditor(new() { IsReadOnly = true, WordWrap = true }).Named("MaterialsStatus")
            .WithRole(MaterialStatusRole).SemanticId(MaterialPrefix + ".status")
            .Configure(status => { status.MinWidth = 0; status.MinHeight = 0; })
            .Bind(RichTextEditor.TextProperty, _materialContext.Strings, text => text.StatusText),
        ui.VStack(
            ui.HStack(
                MaterialCommand(ui, "Создать материал", "new", model => model.CanCreate, model =>
                {
                    model.New();
                    if (model.HasDocument && model.CanEditFields) RequestMaterialFocus(model);
                }).Fill(),
                MaterialCommand(ui, "Обновить материалы", "reload", model => model.CanReload, model => model.Reload()).Fill())
                .WithRole(RowRole),
            ui.Text("Нет авторских материалов").Named("MaterialsEmpty").WithRole(MetadataRole),
            ui.ScrollViewer(ui.VStack().Named("MaterialsItems").WithRole(StackRole))
                .Named("MaterialsList").WithRole(MaterialListRole).Configure(ConfigureMaterialList),
            ui.VStack(
                ui.Text("Имя").WithRole(MetadataRole),
                ui.RichTextEditor(MaterialNameReadOnly).Named("MaterialName").WithRole(MaterialNameRole).SemanticId(MaterialPrefix + ".name")
                    .Configure(input => { input.AccessibleName = "Имя материала"; input.ColorRole = EditorColorRole.Text; input.MinWidth = 0; input.MinHeight = 0; })
                    .BindTwoWay(RichTextEditor.TextProperty, _materialContext.Strings, text => text.Name),
                ui.Text("Модель: Unlit · цвет sRGB (0–1)").WithRole(MetadataRole),
                ui.HStack(
                    MaterialNumber(ui, "R", "MaterialRed", "r", model => model.Red).Fill(),
                    MaterialNumber(ui, "G", "MaterialGreen", "g", model => model.Green).Fill(),
                    MaterialNumber(ui, "B", "MaterialBlue", "b", model => model.Blue).Fill()).WithRole(RowRole),
                ui.Text("Текстура").WithRole(MetadataRole),
                ui.Button().Named("MaterialTexture").WithRole(ActionRole).SemanticId(MaterialPrefix + ".texture")
                    .Bind(UiProperties.Enabled, _materialContext, model => model.CanEditFields).OnClick(ToggleMaterialTextures),
                ui.ScrollViewer(ui.VStack(
                    ui.Button("Без текстуры").WithRole(ActionRole).SemanticId(MaterialPrefix + ".texture.none")
                        .OnClick(() => SelectMaterialTexture(null)),
                    ui.VStack().Named("MaterialTextureItems").WithRole(StackRole)).WithRole(StackRole))
                    .Named("MaterialTextureList").WithRole(MaterialListRole)
                    .Configure(list => { ConfigureMaterialList(list); list.IsVisible = false; }),
                MaterialCommand(ui, "Сохранить материал", "save", model => model.CanSave, model => model.Save()),
                MaterialCommand(ui, "Отменить правку материала", "cancel", model => model.CanCancel, model => model.Cancel()),
                MaterialCommand(ui, "Отменить изменение материала", "undo", model => model.CanUndo, model => model.Undo()),
                MaterialCommand(ui, "Повторить изменение материала", "redo", model => model.CanRedo, model => model.Redo()))
                .Named("MaterialEditor").WithRole(StackRole)
                .Bind(UiProperties.Visible, _materialContext, model => model.HasDocument))
            .Named("MaterialsBody").WithRole(StackRole).Bind(UiProperties.Visible, _materialContext, model => model.IsOpen))
        .WithRole(StackRole)).Named("MaterialsPanel").WithRole(SectionRole).Configure(panel => panel.IsVisible = false);

    private static void ConfigureMaterialList(ScrollViewer list) => list.MaxHeight = EditorInspectorViewStyle.MaterialListMaxHeight;

    private UiControl<Button, IEditorInspectorViewModel> MaterialCommand(IUiBuildContext<IEditorInspectorViewModel> ui,
        string label, string suffix, Expression<Func<IEditorMaterialsModel, bool>> enabled, Action<IEditorMaterialsModel> action) =>
        ui.Button(label).WithRole(ActionRole).SemanticId(MaterialPrefix + "." + suffix)
            .Bind(UiProperties.Enabled, _materialContext, enabled)
            .OnClick(() => { if (CurrentMaterials is { } model) action(model); });

    private UiControl<NumberBox, IEditorInspectorViewModel> MaterialNumber(IUiBuildContext<IEditorInspectorViewModel> ui,
        string prefix, string name, string suffix, Expression<Func<IEditorMaterialsModel, string>> value) =>
        ui.Create(MaterialNumberFactory.Instance, prefix).Named(name).Configure(box =>
        {
            box.Editor.WithRole(InputRole).BindTwoWay(TextBox.ValueProperty, _materialContext, value);
            box.Editor.SemanticId = MaterialPrefix + "." + suffix;
            box.Editor.AccessibleName = $"Материал · цвет sRGB · {prefix}";
            box.Prefix.WithRole(VectorLabelRole);
        });

    private IEditorMaterialsModel? CurrentMaterials =>
        ReferenceEquals(DataContext, _presentedContext) && _materialContext.DataContext is { IsAvailable: true } model &&
        ReferenceEquals(DataContext?.Materials, model) ? model : null;

    private void PrepareMaterials()
    {
        _materialFields =
        [
            new(GetRequiredPart<NumberBox>("MaterialRed").Editor),
            new(GetRequiredPart<NumberBox>("MaterialGreen").Editor),
            new(GetRequiredPart<NumberBox>("MaterialBlue").Editor)
        ];
        foreach (var field in _materialFields)
            field.Editor.PropertyChanged += (_, key) =>
            {
                if (!ReferenceEquals(key, TextBox.ValueProperty)) return;
                if (ReferenceEquals(_materialInputField, field))
                {
                    _materialInputField = null;
                    field.Record();
                }
                else if (!field.IsReplaying) field.Clear();
            };
        GetRequiredPart<VStack>("MaterialsItems").BindItems(_materialRows, CreateMaterialRow, static item => item.Id,
            (row, item) => ((MaterialActionRow)row).Present(MaterialDisplayName(item), item.Id == _shownMaterialId));
        GetRequiredPart<VStack>("MaterialTextureItems").BindItems(_textureRows, CreateTextureRow, static item => item.Id,
            static (row, item) => ((MaterialActionRow)row).Present(item.Name, false));
        MaterialsPart.ObservePropertyChanged(_materialContext, (_, change) =>
        {
            if (change.PropertyName is null or "" or nameof(IEditorMaterialsModel.Name) or nameof(IEditorMaterialsModel.StatusText))
                _materialContext.Strings.Notify(change);
            if (change.PropertyName is null or "" or nameof(IEditorMaterialsModel.IsAvailable) or nameof(IEditorMaterialsModel.IsOpen) or
                nameof(IEditorMaterialsModel.StatusText) or nameof(IEditorMaterialsModel.Items) or nameof(IEditorMaterialsModel.Textures) or
                nameof(IEditorMaterialsModel.SelectedMaterialId) or nameof(IEditorMaterialsModel.CanEditFields) or
                nameof(IEditorMaterialsModel.TextureId))
                SynchronizeMaterials();
        }, () =>
        {
            _materialContext.Strings.Notify(new(null));
            SynchronizeMaterials();
        },
            UiSynchronization.AfterTreeChange);
    }

    private static string MaterialDisplayName(InspectorRenderAssetChoice item) =>
        $"{item.Name.ReplaceLineEndings(" ")} · {item.Id.ToString("N")[..8]}";

    private Element CreateMaterialRow(InspectorRenderAssetChoice item) => new MaterialActionRow(MaterialDisplayName(item),
        $"{MaterialPrefix}.item.{item.Id:N}", () => OpenMaterial(item.Id), selected: item.Id == _shownMaterialId);

    private Element CreateTextureRow(InspectorRenderAssetChoice item) => new MaterialActionRow(item.Name,
        $"{MaterialPrefix}.texture.{item.Id:N}", () => SelectMaterialTexture(item.Id));

    private void SynchronizeMaterials()
    {
        var model = _materialContext.DataContext;
        MaterialsPart.IsVisible = model?.IsAvailable == true;
        MaterialHeader.Expanded = model?.IsOpen == true;
        GetRequiredPart<RichTextEditor>("MaterialsStatus").IsVisible = !string.IsNullOrEmpty(model?.StatusText);
        var items = model?.Items;
        var refreshRows = !ReferenceEquals(_shownMaterialRows, items);
        if (refreshRows)
        {
            _shownMaterialRows = items;
            _materialRows.Clear();
            if (items != null) _materialRows.AddRange(items);
        }
        if (_shownMaterialId != model?.SelectedMaterialId)
        {
            _shownMaterialId = model?.SelectedMaterialId;
            foreach (var field in _materialFields) field.Clear();
            refreshRows = true;
        }
        if (refreshRows) GetRequiredPart<VStack>("MaterialsItems").RefreshItems();
        GetRequiredPart<Text>("MaterialsEmpty").IsVisible = _materialRows.Count == 0 && string.IsNullOrEmpty(model?.StatusText);
        GetRequiredPart<ScrollViewer>("MaterialsList").IsVisible = _materialRows.Count != 0;

        var textures = model?.Textures;
        if (!ReferenceEquals(_shownTextureRows, textures))
        {
            _shownTextureRows = textures;
            _textureRows.Clear();
            if (textures != null) _textureRows.AddRange(textures);
            GetRequiredPart<VStack>("MaterialTextureItems").RefreshItems();
        }
        var readOnly = model?.CanEditFields != true;
        MaterialName.Options = readOnly ? MaterialNameReadOnly : MaterialNameEditable;
        foreach (var field in _materialFields) field.Editor.IsReadOnly = readOnly;
        var texture = GetRequiredPart<Button>("MaterialTexture");
        texture.Value = model?.TextureId is { } id
            ? _textureRows.Find(item => item.Id == id)?.Name ?? $"Недоступная текстура · {id:D}"
            : "Без текстуры";
        texture.AccessibleName = "Текстура · " + texture.Value;
        if (readOnly || model?.IsOpen != true) GetRequiredPart<ScrollViewer>("MaterialTextureList").IsVisible = false;
    }

    private void OpenMaterial(Guid id)
    {
        if (CurrentMaterials is not { } model) return;
        var wasDirty = model.HasDraft;
        model.Open(id);
        if (!wasDirty && model.SelectedMaterialId == id && model.HasDocument) RequestMaterialFocus(model);
    }

    private void ToggleMaterialTextures()
    {
        if (CurrentMaterials is not { CanEditFields: true }) return;
        var list = GetRequiredPart<ScrollViewer>("MaterialTextureList");
        list.IsVisible = !list.IsVisible;
    }

    private void SelectMaterialTexture(Guid? id)
    {
        if (CurrentMaterials is not { CanEditFields: true } model) return;
        model.TextureId = id;
        GetRequiredPart<ScrollViewer>("MaterialTextureList").IsVisible = false;
    }

    private void AddMaterialSlotActions(VStack rows, IEditorInspectorViewModel inspector, InspectorRenderAssetSelection selection,
        Guid slotId, Guid selectedMaterial, ScrollViewer host)
    {
        if (inspector.Materials is not { IsAvailable: true } materials) return;
        bool IsCurrent() => ReferenceEquals(DataContext, inspector) && ReferenceEquals(inspector.RenderAssets, selection) &&
            ReferenceEquals(CurrentMaterials, materials);
        rows.Add(new MaterialActionRow("Редактировать материал", $"editor.inspector.material.edit.{slotId:N}", () =>
        {
            if (!IsCurrent()) return;
            CloseRenderChoices(host);
            var wasDirty = materials.HasDraft;
            materials.OpenSlot(selection, slotId);
            if (!wasDirty && materials.SelectedMaterialId == selectedMaterial && materials.HasDocument)
                RequestMaterialFocus(materials);
        }));
        rows.Add(new MaterialActionRow("Создать копию и назначить", $"editor.inspector.material.copy.{slotId:N}", () =>
        {
            if (!IsCurrent() || !materials.CanCopySlot(selection, slotId)) return;
            CloseRenderChoices(host);
            materials.CopyForSlot(selection, slotId);
        }, button =>
        {
            void Present() => button.IsEnabled = IsCurrent() && materials.CanCopySlot(selection, slotId);
            Present();
            button.ObserveState(Present, materials, inspector);
        }));
    }

    private void RequestMaterialFocus(IEditorMaterialsModel model)
    {
        _focusMaterialModel = model;
        _focusMaterialId = model.SelectedMaterialId;
        _requestMaterialFocus = true;
        Invalidate(Invalidation.Render);
    }

    private void FocusRequestedMaterial(IInputContext input)
    {
        if (!_requestMaterialFocus || input.HasPendingLayout) return;
        _requestMaterialFocus = false;
        var model = _focusMaterialModel;
        _focusMaterialModel = null;
        if (model == null || !ReferenceEquals(CurrentMaterials, model) || !model.IsOpen || !model.HasDocument ||
            model.SelectedMaterialId != _focusMaterialId) return;
        input.TryFocus(model.IsReadOnly ? MaterialHeader : MaterialName);
    }

    private bool MaterialTextHasFocus()
    {
        if (MaterialsPart.IsVisible && MaterialName.IsFocused) return true;
        foreach (var field in _materialFields)
            if (field.Editor.IsFocused) return true;
        return false;
    }

    // TextBox has no history of its own. Only accept values changed by routed text/cut/paste/delete,
    // not canonical values arriving through the two-way binding after a catalog operation.
    private bool PreviewMaterialTextInput(UiInput input, IInputContext context)
    {
        _materialInputField = null;
        if (input.Kind is not (InputKind.Text or InputKind.KeyDown) ||
            context.FocusedElement is not TextBox editor || CurrentMaterials is not { } model) return false;
        foreach (var field in _materialFields)
        {
            if (!ReferenceEquals(field.Editor, editor)) continue;
            if (input is { Kind: InputKind.KeyDown, Control: true, Key: UiKey.Z or UiKey.Y })
            {
                if (model.CanEditFields) field.Replay(input.Key == UiKey.Y || input.Shift);
                return true;
            }
            if (model.CanEditFields && (input.Kind == InputKind.Text || input.Key is UiKey.Backspace or UiKey.Delete ||
                input.Control && input.Key is (UiKey.X or UiKey.V)))
            {
                field.Arm();
                _materialInputField = field;
                if (!_clearMaterialInputQueued)
                {
                    _clearMaterialInputQueued = true;
                    Dispatch(() => { _clearMaterialInputQueued = false; _materialInputField = null; });
                }
            }
            break;
        }
        return false;
    }

    private sealed class MaterialFieldHistory(TextBox editor)
    {
        private Stack<string>? _undo, _redo;
        private string _before = "";
        public TextBox Editor { get; } = editor;
        public bool IsReplaying { get; private set; }
        public void Arm() => _before = Editor.Value;
        public void Record()
        {
            if (_before == Editor.Value) return;
            (_undo ??= new()).Push(_before);
            _redo?.Clear();
        }
        public void Replay(bool redo)
        {
            var from = redo ? _redo : _undo;
            if (from is null || !from.TryPop(out var next)) return;
            var previous = Editor.Value;
            IsReplaying = true;
            try { Editor.Value = next; }
            finally { IsReplaying = false; }
            if (Editor.Value == previous) { from.Push(next); return; }
            var to = redo ? (_undo ??= new()) : (_redo ??= new());
            to.Push(previous);
        }
        public void Clear()
        {
            _before = "";
            _undo?.Clear();
            _redo?.Clear();
        }
    }

    private sealed class MaterialContext : IDataContext<IEditorMaterialsModel>
    {
        private IEditorMaterialsModel? _current;
        public MaterialContext() => Strings = new(this);
        public MaterialStrings Strings { get; }
        public IEditorMaterialsModel? DataContext
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

    // RichText uses LF internally; reading a catalog name must not rewrite its original line endings.
    private sealed class MaterialStrings(MaterialContext context) : INotifyPropertyChanged
    {
        public string Name
        {
            get => context.DataContext?.Name.ReplaceLineEndings("\n") ?? "";
            set { if (context.DataContext is { } model) model.Name = value; }
        }
        public string StatusText => context.DataContext?.StatusText.ReplaceLineEndings("\n") ?? "";
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Notify(PropertyChangedEventArgs change) => PropertyChanged?.Invoke(this, change);
    }

    // A keyed row gets its button from the active UiSession registry when it is attached.
    private sealed class MaterialActionRow : View
    {
        private string _label;
        private bool _selected;
        private Button? _button;

        internal MaterialActionRow(string label, string id, Action action, Action<Button>? configure = null, bool selected = false)
        {
            _label = label;
            _selected = selected;
            Content(ui => ui.Button().SemanticId(id).OnClick(action).Configure(button =>
            {
                button.Style = EditorInspectorViewStyle.MaterialRowAction;
                _button = button;
                Present(_label, _selected);
                configure?.Invoke(button);
            }));
        }

        internal void Present(string label, bool selected)
        {
            _label = label;
            _selected = selected;
            if (_button == null) return;
            _button.Value = label;
            _button.AccessibleName = label;
            _button.Tone = selected ? ControlTone.Accent : ControlTone.Neutral;
        }
    }

    private static class MaterialHeaderFactory
    {
        internal static readonly IFactory<ComponentHeader> Instance = new Factory<ComponentHeader>(static () => new());
    }

    private sealed class MaterialNumberFactory : IFactory<NumberBox, string>
    {
        internal static readonly MaterialNumberFactory Instance = new();
        public NumberBox Create(string arguments) => new(arguments);
    }
}
