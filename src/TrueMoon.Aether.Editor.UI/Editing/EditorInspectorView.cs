using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.ComponentModel;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Schema-driven retained inspector. Borrows one model/style and owns only its control tree and subscriptions.</summary>
public sealed class EditorInspectorView : View<IEditorInspectorViewModel>, IUiInputObserver, IUiFrameParticipant
{
    /// <summary>Component section appearance.</summary>
    public static ViewStyleRole<Border> SectionRole { get; } = new("InspectorSection");
    /// <summary>Stack spacing inside dynamic sections.</summary>
    public static ViewStyleRole<VStack> StackRole { get; } = new("InspectorStack");
    /// <summary>Compact action rows.</summary>
    public static ViewStyleRole<HStack> RowRole { get; } = new("InspectorRow");
    internal static ViewStyleRole<HStack> TransformGroupRole { get; } = new("InspectorTransformGroup");
    /// <summary>Component disclosure buttons.</summary>
    public static ViewStyleRole<Button> HeaderRole { get; } = new("InspectorHeader");
    /// <summary>Compact inspector actions, including dynamic schema and presence buttons.</summary>
    public static ViewStyleRole<Button> ActionRole { get; } = new("InspectorAction");
    /// <summary>Square icon-only actions, separate from labeled schema choices.</summary>
    public static ViewStyleRole<IconButton> IconRole { get; } = new("InspectorIcon");
    /// <summary>Scalar property labels.</summary>
    public static ViewStyleRole<Text> LabelRole { get; } = new("InspectorLabel");
    /// <summary>Numeric scalar inputs.</summary>
    public static ViewStyleRole<TextBox> InputRole { get; } = new("InspectorInput");
    /// <summary>Responsive three-column numeric rows.</summary>
    public static ViewStyleRole<Vector3Editor> VectorRole { get; } = new("InspectorVector");
    /// <summary>Vector labels and axis prefixes without the scalar label's fixed width.</summary>
    public static ViewStyleRole<Text> VectorLabelRole { get; } = new("InspectorVectorLabel");
    /// <summary>Responsive rows for all schema-defined scalar editors.</summary>
    public static ViewStyleRole<PropertyEditorRow> FieldRole { get; } = new("InspectorField");
    /// <summary>Compact explicit-null presentation.</summary>
    public static ViewStyleRole<Text> EmptyValueRole { get; } = new("InspectorEmptyValue");
    /// <summary>Opt-in numeric pointer and keyboard adjustment configuration.</summary>
    public static ViewStyleRole<NumberBox> NumberRole { get; } = new("InspectorNumber");
    /// <summary>Boolean scalar inputs.</summary>
    public static ViewStyleRole<CheckBox> BooleanRole { get; } = new("InspectorBoolean");
    /// <summary>Nullable switches.</summary>
    public static ViewStyleRole<CheckBox> NullRole { get; } = new("InspectorNull");
    /// <summary>Multiline string inputs.</summary>
    public static ViewStyleRole<RichTextEditor> StringRole { get; } = new("InspectorString");
    /// <summary>Reset action.</summary>
    public static ViewStyleRole<Button> ResetRole { get; } = new("InspectorReset");
    /// <summary>Metadata and provenance.</summary>
    public static ViewStyleRole<Text> MetadataRole { get; } = new("InspectorMetadata");
    /// <summary>Field validation diagnostics.</summary>
    public static ViewStyleRole<Text> ErrorRole { get; } = new("InspectorError");

    private IEditorInspectorViewModel? _presentedContext;
    private NamedControls _parts = null!;
    private readonly Dictionary<ComponentId, InspectorComponentControls> _components = [];
    private readonly Dictionary<(ComponentId, string), InspectorPropertyControls> _properties = [];
    private readonly Dictionary<(ComponentId, string), ScrollViewer> _renderLists = [];
    private InspectorRenderAssetSelection? _shownRenderAssets;
    private Guid? _openRenderSlot;
    internal static ViewStyleRole<ScrollViewer> RenderChoicesRole { get; } = new("InspectorRenderChoices");
    private readonly Dictionary<(ComponentTypeId, int), Button> _addButtons = [];
    private readonly Dictionary<ComponentId, Button> _restoreButtons = [];
    private readonly HashSet<ComponentId> _collapsed = [];
    private readonly HashSet<ComponentId> _details = [];
    private readonly Dictionary<ComponentId, IconButton> _detailsButtons = [];
    private readonly Dictionary<ComponentId, TransformEditor> _transforms = [];
    private readonly Dictionary<ComponentId, VStack> _transformDetails = [];
    private readonly Dictionary<(ComponentId, string), PropertyEditorRow> _genericEditors = [];
    private readonly Dictionary<(ComponentId, string), NumberBox> _numericEditors = [];
    /// <summary>Retained generic numeric, Boolean and string rows, keyed by exact component/property addresses.</summary>
    public IReadOnlyDictionary<(ComponentId, string), PropertyEditorRow> GenericEditors => _genericEditors;
    private InspectorUpdate _pendingUpdate;
    private long _contextVersion;
    private bool _dynamicInputBlocked;
    /// <summary>Retained per-component details disclosures for provenance and inheritance resets.</summary>
    public IReadOnlyDictionary<ComponentId, IconButton> DetailsButtons => _detailsButtons;
    /// <summary>Specialized controls selected by exact built-in component type and schema.</summary>
    public IReadOnlyDictionary<ComponentId, TransformEditor> TransformEditors => _transforms;
    private IconButton? _hint;
    private OverlayHost? _hintHost;
    private Rect _hintBounds;
    private float _hintX, _hintY;
    private TimeSpan _hintRemaining;
    private readonly List<Element> _hintAncestors = [];
    /// <summary>Gets retained component parts.</summary>
    public IReadOnlyDictionary<ComponentId, InspectorComponentControls> Components => _components;
    /// <summary>Gets property editors using stable addresses.</summary>
    public IReadOnlyDictionary<(ComponentId, string), InspectorPropertyControls> Properties => _properties;
    /// <summary>Gets addition choices.</summary>
    public IReadOnlyDictionary<(ComponentTypeId, int), Button> AddButtons => _addButtons;
    /// <summary>Gets hidden occurrence restoration actions.</summary>
    public IReadOnlyDictionary<ComponentId, Button> RestoreButtons => _restoreButtons;
    /// <summary>Gets the complete inspector layout.</summary>
    public VStack Form => _parts.Form;
    /// <summary>Gets the dynamic component host.</summary>
    public VStack ComponentList => _parts.ComponentList;
    /// <summary>Gets scrolling for the whole inspector.</summary>
    public ScrollViewer Scroll => _parts.Scroll;
    /// <summary>Gets the empty-selection guidance.</summary>
    public Text Empty => _parts.Empty;
    /// <summary>Gets Apply.</summary>
    public Button Apply => _parts.Apply;
    /// <summary>Gets Cancel.</summary>
    public Button Cancel => _parts.Cancel;
    /// <summary>Gets shared Undo.</summary>
    public Button Undo => _parts.Undo;
    /// <summary>Gets shared Redo.</summary>
    public Button Redo => _parts.Redo;
    /// <summary>Gets the schema-choice disclosure.</summary>
    public Button AddComponent => _parts.AddComponent;
    /// <summary>Gets the retained schema choices.</summary>
    public VStack AddChoices => _parts.AddChoices;
    /// <summary>Gets hidden component choices.</summary>
    public VStack Removed => _parts.Removed;
    /// <summary>Gets complete selectable mutation diagnostics.</summary>
    public RichTextEditor Error => _parts.Error;
    /// <summary>Compact primary actions; each icon has a semantic name and delayed hover hint.</summary>
    public HStack Actions => _parts.Actions;
    /// <inheritdoc />
    public bool NeedsFrame => _hintRemaining > TimeSpan.Zero || _hint != null && !HintIsCurrent();

    /// <summary>Builds a parent-owned inspector on the UI owner thread.</summary>
    /// <param name="model">Borrowed presentation.</param><param name="style">Borrowed scoped appearance.</param>
    public EditorInspectorView(IEditorInspectorViewModel model, IViewStyle<EditorInspectorView> style)
        : this(model)
    {
        ArgumentNullException.ThrowIfNull(style);
        ViewCreation.Prepare(this, style);
    }

    /// <summary>Creates and prepares a parent-owned inspector on the UI owner thread.</summary>
    public static EditorInspectorView Create(IEditorInspectorViewModel model, IViewStyle<EditorInspectorView> style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return ViewCreation.Prepare(new EditorInspectorView(model), style);
    }

    private EditorInspectorView(IEditorInspectorViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        SemanticId = "editor.inspector";
        Content(model, ui => ui.ScrollViewer().Named("ScrollPart").Content(
            ui.VStack(
                ui.Text().Named("EmptyPart")
                    .BindText(model => model.SelectionText)
                    .Bind(UiProperties.Visible, model => model.NoSelection),
                ui.HStack(
                    ui.IconButton(UiSymbol.Check, "Применить изменения · Ctrl+Enter").Configure(button => ConfigureAction(button)).Named("ApplyPart")
                        .SemanticId("editor.inspector.apply")
                        .Bind(UiProperties.Enabled, model => model.CanApply)
                        .OnClick(() => DataContext?.Apply()),
                    ui.IconButton(UiSymbol.Close, "Отменить правку · Esc").Configure(button => ConfigureAction(button)).Named("CancelPart")
                        .SemanticId("editor.inspector.cancel")
                        .Bind(UiProperties.Enabled, model => model.CanCancel)
                        .OnClick(() => DataContext?.Cancel()),
                    ui.IconButton(UiSymbol.Undo, "Отменить изменение сцены · Ctrl+Z вне текстового поля").Configure(button => ConfigureAction(button)).Named("UndoPart")
                        .SemanticId("editor.inspector.undo")
                        .Bind(UiProperties.Enabled, model => model.CanUndo)
                        .OnClick(() => DataContext?.Undo()),
                    ui.IconButton(UiSymbol.Redo, "Повторить изменение сцены · Ctrl+Y вне текстового поля").Configure(button => ConfigureAction(button)).Named("RedoPart")
                        .SemanticId("editor.inspector.redo")
                        .Bind(UiProperties.Enabled, model => model.CanRedo)
                        .OnClick(() => DataContext?.Redo()),
                    ui.IconButton(UiSymbol.Add, "Добавить компонент").Configure(button => ConfigureAction(button)).Named("AddComponentPart")
                        .SemanticId("editor.inspector.add-component")
                        .Bind(UiProperties.Enabled, model => model.CanChangeComponents)
                        .OnClick(() => this.AddChoicesPart.IsVisible = !this.AddChoicesPart.IsVisible))
                    .Named("ActionsPart").WithRole(RowRole),
                ui.VStack().Named("AddChoicesPart").WithRole(StackRole).Configure(choices => choices.IsVisible = false),
                ui.RichTextEditor(new() { IsReadOnly = true, WordWrap = true }).Named("ErrorPart")
                    .Configure(error => { error.MinWidth = 0; error.MinHeight = 0; })
                    .Bind(RichTextEditor.TextProperty, model => model.DiagnosticsText)
                    .Bind(UiProperties.Visible, model => model.HasDiagnostics),
                ui.VStack().Named("ComponentListPart").WithRole(StackRole),
                ui.VStack().Named("RemovedPart").WithRole(StackRole))
                .Named("FormPart").WithRole(StackRole)));
    }

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        _parts = new(this.ScrollPart, this.FormPart, this.EmptyPart, this.ActionsPart, this.ApplyPart, this.CancelPart,
            this.UndoPart, this.RedoPart, this.AddComponentPart, this.AddChoicesPart, this.ErrorPart, this.ComponentListPart, this.RemovedPart);
        AttachmentChanged += Attachment;
        SynchronizeContext();
        this.ObservePropertyChanged(this, (_, change) => ModelChanged(change), SynchronizeContext);
    }

    private void SynchronizeContext()
    {
        _contextVersion++;
        _pendingUpdate = InspectorUpdate.None;
        // Attachment notifications run inside a tree change, so the replacement happens in the UI queue.
        if (IsAttached && !ReferenceEquals(_presentedContext, DataContext))
            BlockDynamicInput(true);
        QueueSynchronization(InspectorUpdate.All | InspectorUpdate.CloseHint);
    }

    private void ModelChanged(PropertyChangedEventArgs change)
    {
        var update = change.PropertyName switch
        {
            null or "" => InspectorUpdate.All | InspectorUpdate.CloseHint,
            nameof(IEditorInspectorViewModel.Components) or nameof(IEditorInspectorViewModel.AddableTypes) or
                nameof(IEditorInspectorViewModel.RemovedComponents) => InspectorUpdate.All,
            nameof(IEditorInspectorViewModel.CanChangeComponents) or nameof(IEditorInspectorViewModel.HasDraft) or
                nameof(IEditorInspectorViewModel.DiagnosticsText) => InspectorUpdate.EditorState,
            nameof(IEditorInspectorViewModel.RenderAssets) => InspectorUpdate.RenderAssets,
            nameof(IEditorInspectorViewModel.HasSelection) => InspectorUpdate.CloseHint,
            _ => InspectorUpdate.None
        };
        if (update != InspectorUpdate.None) QueueSynchronization(update);
    }

    private void QueueSynchronization(InspectorUpdate update)
    {
        var alreadyQueued = _pendingUpdate != InspectorUpdate.None;
        _pendingUpdate |= update;
        if (alreadyQueued) return;

        var contextVersion = _contextVersion;
        Dispatch(() =>
        {
            if (contextVersion != _contextVersion) return;
            var pending = _pendingUpdate;
            _pendingUpdate = InspectorUpdate.None;
            Synchronize(pending);
            if (contextVersion == _contextVersion) BlockDynamicInput(false);
        });
    }

    private void BlockDynamicInput(bool blocked)
    {
        if (_dynamicInputBlocked == blocked) return;
        _dynamicInputBlocked = blocked;
        this.ComponentListPart.IsInputBlocked = blocked;
        this.AddChoicesPart.IsInputBlocked = blocked;
        this.RemovedPart.IsInputBlocked = blocked;
    }

    private void Synchronize(InspectorUpdate update)
    {
        VerifyAccess();
        if ((update & InspectorUpdate.CloseHint) != 0) CloseHint();

        var model = DataContext;
        var replaced = !ReferenceEquals(_presentedContext, model);
        if (replaced)
        {
            update |= InspectorUpdate.All;
            foreach (var box in NumericEditors()) box.CancelGesture();
            this.AddChoicesPart.IsVisible = false;
        }
        if ((update & InspectorUpdate.RenderAssets) != 0)
            SynchronizeRenderAssets(model?.RenderAssets);
        if ((update & InspectorUpdate.Structure) != 0)
        {
            SynchronizeComponents(model, replaced);
            SynchronizeComponentChoices(model, replaced);
        }
        _presentedContext = model;

        if (model == null)
        {
            foreach (var action in this.ActionsPart.Children) action.IsEnabled = false;
            this.EmptyPart.IsVisible = true;
            this.EmptyPart.Value = "Нет выбранного объекта";
            this.ErrorPart.IsVisible = false;
            return;
        }

        if ((update & InspectorUpdate.EditorState) != 0)
        {
            foreach (var box in NumericEditors())
                if (box.IsDragging && !(box.IsGestureCurrent?.Invoke() ?? true)) box.CancelGesture();
            SynchronizeComponentAvailability(model);
            SynchronizeEditors();
        }
        if ((update & InspectorUpdate.RenderAssets) != 0) SynchronizeRenderEditors();
    }

    private void SynchronizeComponents(IEditorInspectorViewModel? model, bool replaced)
    {
        var wanted = (model?.Components ?? []).Select(component => component.Id).ToHashSet();
        foreach (var id in _components.Keys.Where(id => replaced || !wanted.Contains(id)).ToArray())
        {
            var section = _components[id];
            this.ComponentListPart.Items.Remove(section.Section);
            section.Section.Dispose();
            _components.Remove(id);
            foreach (var address in _properties.Keys.Where(key => key.Item1 == id).ToArray())
            {
                _properties.Remove(address);
                _genericEditors.Remove(address);
                _numericEditors.Remove(address);
                _renderLists.Remove(address);
            }
            _collapsed.Remove(id);
            _details.Remove(id);
            _detailsButtons.Remove(id);
            _transforms.Remove(id);
            _transformDetails.Remove(id);
        }
        if (model == null) return;

        for (var index = 0; index < model.Components.Count; index++)
        {
            var component = model.Components[index];
            if (!_components.TryGetValue(component.Id, out var parts))
            {
                var body = new VStack().WithRole(StackRole);
                var header = new ComponentHeader().WithRole(HeaderRole)
                    .SemanticId($"editor.inspector.component.{component.Id.Value:N}");
                header.OnClick(() => ToggleComponent(component.Id, body, header));
                var remove = Action(UiSymbol.Delete, "Удалить компонент " + component.Title).OnClick(() => model.RemoveComponent(component.Id));
                var details = Action(UiSymbol.More, "Сведения и сброс значений · " + component.Title).OnClick(() => ToggleDetails(component.Id));
                _detailsButtons.Add(component.Id, details);
                var metadata = new Text().WithRole(MetadataRole);
                metadata.IsVisible = false;
                var section = new Border().WithRole(SectionRole).Content(new VStack().WithRole(StackRole).WithChildren(
                    new ComponentLine(header, details, remove), metadata, body));
                parts = new(section, header, body, remove, metadata); _components.Add(component.Id, parts); this.ComponentListPart.Add(section);
            }
            parts.Header.Value = component.Title;
            parts.Header.AccessibleName = component.Title + " · свернуть или раскрыть";
            parts.Metadata.Value = component.SourceText + " · v" + component.SchemaVersion + " · " + component.Id.Value.ToString("D")[..8];
            parts.Metadata.AccessibleName = $"Type {component.TypeId.Value:D}; component {component.Id.Value:D}; schema {component.SchemaVersion}";
            if (component.TypeId == BuiltInComponents.TransformId && component.SchemaVersion == 1)
            {
                if (!_transforms.ContainsKey(component.Id)) AddTransform(model, component, parts.Body);
                Reorder(this.ComponentListPart, parts.Section, index);
                continue;
            }
            var addresses = component.Properties.Select(p => (component.Id, p.Key)).ToHashSet();
            foreach (var address in _properties.Keys.Where(key => key.Item1 == component.Id && !addresses.Contains(key)).ToArray()) RemoveProperty(address, parts.Body);
            for (var position = 0; position < component.Properties.Count; position++)
            {
                var property = component.Properties[position]; var address = (component.Id, property.Key);
                if (_properties.TryGetValue(address, out var existing) && !ReferenceEquals(existing.Property, property)) RemoveProperty(address, parts.Body);
                if (!_properties.TryGetValue(address, out var row))
                { row = CreatePropertyEditor(model, property, component.Title); _properties.Add(address, row); parts.Body.Add(row.Row); }
                Reorder(parts.Body, row.Row, position);
            }
            Reorder(this.ComponentListPart, parts.Section, index);
        }
    }

    private void ToggleComponent(ComponentId id, VStack body, ComponentHeader header)
    {
        if (!_collapsed.Add(id)) _collapsed.Remove(id);
        header.Expanded = body.IsVisible = !_collapsed.Contains(id);
    }

    private void SynchronizeComponentChoices(IEditorInspectorViewModel? model, bool replaced)
    {
        var types = (model?.AddableTypes ?? []).Select(type => (type.Schema.TypeId, type.Schema.Version)).ToHashSet();
        foreach (var key in _addButtons.Keys.Where(key => replaced || !types.Contains(key)).ToArray())
        {
            var button = _addButtons[key];
            this.AddChoicesPart.Items.Remove(button);
            button.Dispose();
            _addButtons.Remove(key);
        }
        var hidden = (model?.RemovedComponents ?? []).Select(component => component.Id).ToHashSet();
        foreach (var id in _restoreButtons.Keys.Where(id => replaced || !hidden.Contains(id)).ToArray())
        {
            var row = _restoreButtons[id].Parent!;
            this.RemovedPart.Items.Remove(row);
            row.Dispose();
            _restoreButtons.Remove(id);
        }
        if (model == null) return;

        for (var i = 0; i < model.AddableTypes.Count; i++)
        {
            var choice = model.AddableTypes[i]; var key = (choice.Schema.TypeId, choice.Schema.Version);
            if (!_addButtons.TryGetValue(key, out var button))
            {
                button = new Button(choice.Title).WithRole(ActionRole).OnClick(() => { model.AddComponent(choice.Schema.TypeId, choice.Schema.Version); this.AddChoicesPart.IsVisible = false; });
                _addButtons.Add(key, button); this.AddChoicesPart.Add(button);
            }
            Reorder(this.AddChoicesPart, button, i);
        }
        for (var i = 0; i < model.RemovedComponents.Count; i++)
        {
            var removed = model.RemovedComponents[i];
            if (!_restoreButtons.TryGetValue(removed.Id, out var button))
            {
                button = Action(UiSymbol.Redo, "Восстановить компонент " + removed.Title).OnClick(() => model.RestoreComponent(removed.Id));
                _restoreButtons.Add(removed.Id, button);
                this.RemovedPart.Add(new HStack().WithRole(RowRole).WithChildren(button, new Text(removed.Title).WithRole(MetadataRole)));
            }
            Reorder(this.RemovedPart, button.Parent!, i);
        }
    }

    private void SynchronizeComponentAvailability(IEditorInspectorViewModel model)
    {
        var canChange = model.CanChangeComponents;
        foreach (var section in _components.Values) section.Remove.IsEnabled = canChange;
        foreach (var choice in model.AddableTypes)
            if (_addButtons.TryGetValue((choice.Schema.TypeId, choice.Schema.Version), out var button))
                button.IsEnabled = canChange && choice.CanAdd;
        foreach (var button in _restoreButtons.Values) button.IsEnabled = canChange;
    }

    private void SynchronizeRenderAssets(InspectorRenderAssetSelection? assets)
    {
        if (ReferenceEquals(_shownRenderAssets, assets)) return;
        foreach (var list in _renderLists.Values) CloseRenderChoices(list);
        _shownRenderAssets = assets;
        _openRenderSlot = null;
    }

    private void SynchronizeRenderEditors()
    {
        foreach (var address in _renderLists.Keys)
        {
            var property = _properties[address];
            var picker = (Button)property.Input;
            var choices = RenderChoices(property.Property);
            picker.Value = choices.Items.FirstOrDefault(item => item.Id == choices.Selected)?.Name ?? "Выбрать";
            picker.IsEnabled = _shownRenderAssets?.Enabled == true && choices.Items.Count != 0;
            if (property.Property.Key == "bindings")
            {
                property.Reset.IsEnabled = _shownRenderAssets is { CanRestoreMeshMaterial: true } selection &&
                    selection.Component == property.Property.ComponentId;
                RefreshSlotPickers(property);
            }
        }
    }

    private void SynchronizeEditors()
    {
        // Coalesce presentation updates instead of adding two dispatcher entries per field on every model reload.
        foreach (var property in _properties.Values)
        {
            property.Input.HasError = property.Property.HasError; property.Input.IsModified = property.Property.HasDraft;
            property.Input.ErrorMessage = property.Property.Error;
            if (property.Input is CheckBox boolean) boolean.Value = property.Property.BooleanValue ? "Да" : "Нет";
            if (_genericEditors.TryGetValue((property.Property.ComponentId, property.Property.Key), out var editor))
            {
                editor.ShowEmpty(property.Property.IsNull);
                editor.Empty.IsModified = property.Property.HasDraft;
                editor.Empty.HasError = property.Property.HasError;
                if (property.Null != null) property.Null.IsModified = property.Property.HasDraft;
            }
        }
    }

    private InspectorPropertyControls CreatePropertyEditor(IEditorInspectorViewModel model, InspectorProperty property, string title)
    {
        var address = (property.ComponentId, property.Key);
        var parts = InspectorPropertyEditorFactory.Create(model, property, title, _details.Contains(property.ComponentId),
            choices => ToggleRenderChoices(property, choices), () =>
            {
                if (ReferenceEquals(DataContext, model) &&
                    _shownRenderAssets is { CanRestoreMeshMaterial: true } selection && selection.Component == property.ComponentId)
                    model.RestoreMeshMaterial(selection);
            });
        _genericEditors.Add(address, parts.Editor);
        if (parts.Number != null) _numericEditors.Add(address, parts.Number);
        if (parts.Choices != null) _renderLists.Add(address, parts.Choices);
        return parts.Controls;
    }

    private void AddTransform(IEditorInspectorViewModel model, InspectorComponent component, VStack body)
    {
        var parts = InspectorTransformEditorFactory.Create(this, model, component);
        _transforms.Add(component.Id, parts.Editor);
        _transformDetails.Add(component.Id, parts.Details);
        foreach (var field in parts.Fields)
        {
            var address = (component.Id, field.Controls.Property.Key);
            _properties.Add(address, field.Controls);
            _numericEditors.Add(address, field.Number);
        }
        body.Add(parts.Editor);
        body.Add(parts.Details);
    }

    private (IReadOnlyList<InspectorRenderAssetChoice> Items, Guid? Selected) RenderChoices(InspectorProperty property)
    {
        if (_shownRenderAssets is not { } selection || selection.Component != property.ComponentId) return ([], null);
        return property.Key == "mesh" ? (selection.Meshes, selection.Mesh) : (selection.Materials, selection.Material);
    }

    private void RefreshSlotPickers(InspectorPropertyControls controls)
    {
        var property = controls.Property;
        if (controls.SlotPickers is not { } pickers) return;
        var key = (property.ComponentId, property.Key);
        var selection = _shownRenderAssets;
        var slots = selection?.Component == property.ComponentId ? selection.Slots : [];
        _genericEditors[key].IsVisible = slots.Count == 0;
        pickers.Panel.IsVisible = slots.Count != 0;
        foreach (var stale in pickers.Buttons.Keys.Where(id => slots.All(slot => slot.Id != id)).ToArray())
        {
            var button = pickers.Buttons[stale];
            pickers.Panel.Items.Remove(button);
            button.Dispose();
            pickers.Buttons.Remove(stale);
        }
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!pickers.Buttons.TryGetValue(slot.Id, out var button))
            {
                button = new Button().WithRole(ActionRole)
                    .OnClick(() => ToggleRenderChoices(property, _renderLists[key], slot.Id));
                pickers.Buttons.Add(slot.Id, button);
                pickers.Panel.Add(button);
            }
            var material = selection!.Materials.FirstOrDefault(item => item.Id == slot.Material);
            button.Value = slot.Name + " · " + (material?.Name ?? "Выбрать");
            button.AccessibleName = "Материал слота " + slot.Name;
            button.IsEnabled = slot.CanSelect && selection.Materials.Count != 0;
            Reorder(pickers.Panel, button, i);
        }
    }

    private void ToggleRenderChoices(InspectorProperty property, ScrollViewer scroll) => ToggleRenderChoices(property, scroll, null);

    private void ToggleRenderChoices(InspectorProperty property, ScrollViewer scroll, Guid? slotId)
    {
        if (DataContext is not { } model || !ReferenceEquals(model, _presentedContext)) return;
        var open = !scroll.IsVisible || _openRenderSlot != slotId;
        foreach (var list in _renderLists.Values) CloseRenderChoices(list);
        if (!open || _shownRenderAssets is not { Enabled: true } selection) return;
        var slot = slotId.HasValue ? selection.Slots.FirstOrDefault(item => item.Id == slotId) : null;
        if (slotId.HasValue && slot is not { CanSelect: true }) return;
        (IReadOnlyList<InspectorRenderAssetChoice> Items, Guid? Selected) choices =
            slot == null ? RenderChoices(property) : (selection.Materials, slot.Material);
        var label = slot == null ? property.Label : "Материал слота " + slot.Name;
        var rows = new VStack().WithRole(StackRole);
        foreach (var item in choices.Items)
        {
            var button = new Button(item.Name).WithRole(ActionRole).OnClick(() =>
            {
                CloseRenderChoices(scroll);
                if (!ReferenceEquals(DataContext, model)) return;
                if (slot == null) model.SelectRenderAsset(selection, property, item.Id);
                else model.SelectRenderSlotMaterial(selection, slot.Id, item.Id);
            });
            button.AccessibleName = label + " · " + item.Name;
            button.Tone = item.Id == choices.Selected ? ControlTone.Accent : ControlTone.Neutral;
            rows.Add(button);
            if (slot == null && property.Key == "mesh" && selection.MeshesWithMaterialTransfer.Contains(item.Id))
            {
                var transfer = new Button("Выбрать с переносом материалов").WithRole(ActionRole).OnClick(() =>
                {
                    CloseRenderChoices(scroll);
                    if (ReferenceEquals(DataContext, model)) model.SelectMeshWithMaterialOverrides(selection, item.Id);
                });
                transfer.AccessibleName = "Выбрать с переносом материалов · " + item.Name;
                rows.Add(transfer);
            }
        }
        scroll.Content(rows);
        scroll.IsVisible = true;
        _openRenderSlot = slotId;
    }

    private static void CloseRenderChoices(ScrollViewer scroll)
    {
        if (!scroll.IsVisible) return;
        scroll.IsVisible = false;
        var list = scroll.Child;
        scroll.SetContent(null);
        list?.Dispose();
    }
    private IEnumerable<NumberBox> NumericEditors() => _numericEditors.Values;
    private void ToggleDetails(ComponentId id)
    {
        if (!_details.Add(id)) _details.Remove(id);
        var visible = _details.Contains(id); _components[id].Metadata.IsVisible = visible;
        _detailsButtons[id].Tone = visible ? ControlTone.Accent : ControlTone.Neutral;
        if (_transformDetails.TryGetValue(id, out var panel)) panel.IsVisible = visible;
        foreach (var entry in _properties.Where(p => p.Key.Item1 == id).Select(p => p.Value))
        {
            if (!entry.Property.IsRenderReference) entry.Reset.IsVisible = visible;
            entry.Source.IsVisible = visible;
            entry.Source.Parent!.IsVisible = visible;
        }
    }
    private void RemoveProperty((ComponentId, string) address, VStack body)
    { var old = _properties[address]; body.Items.Remove(old.Row); old.Row.Dispose(); _properties.Remove(address); _genericEditors.Remove(address); _numericEditors.Remove(address); _renderLists.Remove(address); }
    private static void Reorder(VStack parent, Element child, int index)
    { if (ReferenceEquals(parent.Children[index], child)) return; parent.Items.Remove(child); parent.Items.Insert(index, child); }

    /// <summary>Routes shared actions while leaving text-edit undo to the focused scalar editor.</summary>
    /// <param name="input">Normalized input.</param><returns>Whether consumed.</returns>
    public bool HandleShortcut(UiInput input)
    {
        VerifyAccess(); if (input.Kind != InputKind.KeyDown || DataContext is not { } model) return false;
        if (input.Key == UiKey.Escape && NumericEditors().FirstOrDefault(box => box.IsDragging) is { } dragging)
        { dragging.CancelGesture(); return true; }
        if (input.Control && input.Key == UiKey.Enter && ReferenceEquals(model, _presentedContext))
            foreach (var box in NumericEditors()) box.CompleteGesture();
        if (input.Key == UiKey.Escape && model.CanCancel) { model.Cancel(); return true; }
        if (!input.Control) return false;
        if (input.Key == UiKey.Enter) { model.Apply(); return true; }
        if (_properties.Values.Any(p => p.Input is TextBox or RichTextEditor && p.Input.IsFocused)) return false;
        if (input.Key == UiKey.Z) { if (input.Shift) model.Redo(); else model.Undo(); return true; }
        if (input.Key == UiKey.Y) { model.Redo(); return true; }
        return false;
    }
    /// <inheritdoc />
    public override bool HandleInput(UiInput input, IInputContext context) => HandleShortcut(input);
    /// <inheritdoc />
    public bool PreviewInput(UiInput input, IInputContext context)
    {
        if (input.Kind == InputKind.PointerMove && !NumericEditors().Any(box => box.IsDragging))
        {
            var hint = IconAt(this, input.X, input.Y);
            if (!ReferenceEquals(hint, _hint))
            {
                CloseHint();
                if (hint != null && FindOverlay() is { Popup: null } host)
                {
                    _hint = hint; _hintHost = host; _hintBounds = hint.Bounds;
                    _hintX = input.X; _hintY = input.Y; _hintRemaining = TimeSpan.FromMilliseconds(500);
                    host.Closed += HintClosed;
                    for (Element? node = this; node != null; node = node.Parent)
                    { _hintAncestors.Add(node); node.PropertyChanged += HintAncestorChanged; }
                }
            }
        }
        else if (input.Kind is InputKind.PointerDown or InputKind.KeyDown or InputKind.FocusLost or InputKind.Wheel) CloseHint();
        return FocusWithin(this) && input.Kind == InputKind.KeyDown &&
            (input.Control && input.Key == UiKey.Enter || input.Key == UiKey.Escape) && HandleShortcut(input);
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        if (_hint == null) return;
        if (!HintIsCurrent()) { CloseHint(); return; }
        if (_hintRemaining <= TimeSpan.Zero) return;
        _hintRemaining -= elapsed;
        if (_hintRemaining > TimeSpan.Zero) return;
        _hintRemaining = TimeSpan.Zero;
        if (_hintHost!.Popup != null) { CloseHint(); return; }
        var popup = new HintSurface { Style = EditorInspectorViewStyle.HintBorder }.Content(
            new Text(_hint.AccessibleName + (_hint.IsEffectivelyEnabled ? "" : " · сейчас недоступно")) { Style = EditorInspectorViewStyle.HintText });
        try { _hintHost.Open(this, popup, _hint.Bounds, context, tooltip: true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private bool HintIsCurrent()
    {
        if (_hint is not { IsDisposed: false, IsAttached: true } || _hint.Bounds != _hintBounds ||
            !ReferenceEquals(IconAt(this, _hintX, _hintY), _hint) || !ReferenceEquals(FindOverlay(), _hintHost)) return false;
        for (var node = Parent; node != null; node = node.Parent)
            if (!node.IsVisible || !node.ChildClipBounds.Contains(_hintX, _hintY)) return false;
        return true;
    }
    private OverlayHost? FindOverlay()
    { for (var node = Parent; node != null; node = node.Parent) if (node is OverlayHost host) return host; return null; }
    private static IconButton? IconAt(Element node, float x, float y)
    {
        if (!node.IsVisible || node.IsDisposed || !node.Bounds.Contains(x, y)) return null;
        if (node is IconButton icon) return icon;
        if (!node.ChildClipBounds.Contains(x, y)) return null;
        for (var i = node.Children.Count - 1; i >= 0; i--)
            if (IconAt(node.Children[i], x, y) is { } found) return found;
        return null;
    }
    private void CloseHint()
    {
        var host = _hintHost;
        ForgetHint();
        if (host is { IsDisposed: false } && ReferenceEquals(host.PopupOwner, this)) host.Close(false);
    }
    private void HintAncestorChanged(Element sender, object property)
    { if (ReferenceEquals(property, UiProperties.Visible) || ReferenceEquals(property, UiProperties.Enabled)) CloseHint(); }
    private void HintClosed() => ForgetHint();
    private void ForgetHint()
    {
        if (_hintHost != null) _hintHost.Closed -= HintClosed;
        foreach (var node in _hintAncestors) node.PropertyChanged -= HintAncestorChanged;
        _hintAncestors.Clear(); _hint = null; _hintHost = null; _hintRemaining = TimeSpan.Zero;
    }
    private void Attachment()
    { if (!IsAttached) ForgetHint(); }
    internal static IconButton Action(UiSymbol symbol, string name)
        => ConfigureAction(new IconButton(symbol, name));
    internal static IconButton ConfigureAction(IconButton button)
    {
        button.Clear(UiProperties.Width); button.Clear(UiProperties.Height); button.Clear(UiProperties.Padding);
        return button.WithRole(ActionRole).WithRole(IconRole);
    }
    private static bool FocusWithin(Element element) => element.IsFocused || element.Children.Any(FocusWithin);
    /// <inheritdoc />
    public override void Dispose()
    {
        if (IsDisposed) return;
        AttachmentChanged -= Attachment;
        ForgetHint(); base.Dispose();
    }

    [Flags]
    private enum InspectorUpdate
    {
        None = 0,
        Structure = 1,
        EditorState = 2,
        RenderAssets = 4,
        CloseHint = 8,
        All = Structure | EditorState | RenderAssets
    }

    // Keep the existing public part handles inspectable after disposal.
    private sealed record NamedControls(ScrollViewer Scroll, VStack Form, Text Empty, HStack Actions,
        Button Apply, Button Cancel, Button Undo, Button Redo, Button AddComponent, VStack AddChoices,
        RichTextEditor Error, VStack ComponentList, VStack Removed);

    private sealed class ComponentLine : HStack
    {
        public ComponentLine(Element header, Element details, Element remove) { Add(header); Add(details); Add(remove); }
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            foreach (var child in Children) child.Measure(available, text);
            return new(Math.Min(available.Width, Children.Sum(child => child.DesiredSize.Width) + 6), Children.Max(child => child.DesiredSize.Height));
        }
        protected override void ArrangeCore(Rect area)
        {
            var actionWidth = Math.Min(area.Width / 2, Math.Max(Children[1].DesiredSize.Width, Children[2].DesiredSize.Width));
            var headerWidth = Math.Max(0, area.Width - actionWidth * 2 - 6);
            Children[0].Arrange(new(area.X, area.Y, headerWidth, area.Height));
            Children[1].Arrange(new(area.X + headerWidth + 3, area.Y, actionWidth, area.Height));
            Children[2].Arrange(new(area.X + area.Width - actionWidth, area.Y, actionWidth, area.Height));
        }
    }

    private sealed class HintSurface : Border
    {
        protected override Color ThemeBackground => Theme.Control;
    }

    internal sealed class ComponentHeader : Button
    {
        private bool _expanded = true;
        public bool Expanded { get => _expanded; set { if (_expanded == value) return; _expanded = value; Invalidate(Invalidation.Render); } }
        protected override bool CentersLabel => false;
    }

}
