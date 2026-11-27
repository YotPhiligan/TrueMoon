using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using System.ComponentModel;
using TrueMoon.Aether.Scenes;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Schema-driven retained inspector. Borrows one model/style and owns only its control tree and subscriptions.</summary>
public sealed partial class EditorInspectorView : View<IEditorInspectorViewModel>, IUiInputObserver, IUiFrameParticipant
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
    /// <summary>Stable mirror the component binding reads: a section survives while its occurrence id is presented.</summary>
    private readonly List<InspectorComponent> _componentRows = [];
    private InspectorRenderAssetSelection? _shownRenderAssets;
    private Guid? _openRenderSlot;
    private int _renderSnapshotVersion;
    internal static ViewStyleRole<ScrollViewer> RenderChoicesRole { get; } = new("InspectorRenderChoices");
    /// <summary>Stable mirrors the keyed bindings of the choice and restoration containers read.</summary>
    private readonly List<InspectorComponentType> _addableTypes = [];
    private readonly List<InspectorRemovedComponent> _removedComponents = [];
    private readonly HashSet<ComponentId> _collapsed = [];
    private readonly HashSet<ComponentId> _details = [];
    /// <summary>Generic numeric, Boolean and string rows projected from the field rows the bindings retain.</summary>
    public IReadOnlyDictionary<(ComponentId, string), PropertyEditorRow> GenericEditors =>
        PropertyRows().ToDictionary(row => row.Address, row => row.Editor);
    private InspectorUpdate _pendingUpdate;
    private long _contextVersion;
    private long _presentationGeneration;
    private bool _dynamicInputBlocked;
    /// <summary>Retained per-component details disclosures for provenance and inheritance resets.</summary>
    public IReadOnlyDictionary<ComponentId, IconButton> DetailsButtons =>
        Sections().ToDictionary(section => section.Address, section => section.Details);
    /// <summary>Specialized controls selected by exact built-in component type and schema.</summary>
    public IReadOnlyDictionary<ComponentId, TransformEditor> TransformEditors =>
        Sections().Where(section => section.Transform != null)
            .ToDictionary(section => section.Address, section => section.Transform!);
    private DelayedHoverHint _hint;
    private OverlayHost? _hintHost;
    private readonly List<Element> _hintAncestors = [];
    /// <summary>Gets retained component parts.</summary>
    public IReadOnlyDictionary<ComponentId, InspectorComponentControls> Components =>
        Sections().ToDictionary(section => section.Address, section => section.Controls);
    /// <summary>Gets property editors using stable addresses.</summary>
    public IReadOnlyDictionary<(ComponentId, string), InspectorPropertyControls> Properties =>
        AllFields().ToDictionary(controls => (controls.Property.ComponentId, controls.Property.Key));
    /// <summary>Gets addition choices projected from the rows the binding currently retains.</summary>
    public IReadOnlyDictionary<(ComponentTypeId, int), Button> AddButtons => IsDisposed
        ? new Dictionary<(ComponentTypeId, int), Button>()
        : this.AddChoicesPart.Items.OfType<AddChoiceButton>().ToDictionary(button => button.Address, button => (Button)button);
    /// <summary>Gets hidden occurrence restoration actions projected from the rows the binding currently retains.</summary>
    public IReadOnlyDictionary<ComponentId, Button> RestoreButtons => IsDisposed
        ? new Dictionary<ComponentId, Button>()
        : this.RemovedPart.Items.OfType<RestoreRow>().ToDictionary(row => row.Address, row => (Button)row.Restore);
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
    public bool NeedsFrame => _requestMaterialFocus || _hint.IsPending || _hint.Target != null && !HintIsCurrent();

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
                BuildMaterials(ui),
                ui.VStack().Named("ComponentListPart").WithRole(StackRole),
                ui.VStack().Named("RemovedPart").WithRole(StackRole))
                .Named("FormPart").WithRole(StackRole)));
    }

    /// <inheritdoc />
    protected override void OnContentPrepared()
    {
        _parts = new(this.ScrollPart, this.FormPart, this.EmptyPart, this.ActionsPart, this.ApplyPart, this.CancelPart,
            this.UndoPart, this.RedoPart, this.AddComponentPart, this.AddChoicesPart, this.ErrorPart, this.ComponentListPart, this.RemovedPart);
        // Section keys carry the presentation generation: every draft record and captured closure belongs to one view model,
        // so a replaced context rebuilds the sections, while a model refresh inside one context reuses them.
        this.ComponentListPart.BindItems(_componentRows, CreateComponentSection,
            component => (component.Id, _presentationGeneration),
            (element, component) =>
            {
                var section = (ComponentSection)element;
                section.Present(component);
                PresentSection(section);
            });
        this.AddChoicesPart.BindItems(_addableTypes, CreateAddChoice, static choice => (choice.Schema.TypeId, choice.Schema.Version), PresentAddChoice);
        this.RemovedPart.BindItems(_removedComponents, CreateRestoreRow, static removed => removed.Id, PresentRestoreRow);
        PrepareMaterials();
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
            nameof(IEditorInspectorViewModel.Materials) => InspectorUpdate.Materials,
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
        MaterialsPart.IsInputBlocked = blocked;
    }

    private void Synchronize(InspectorUpdate update)
    {
        VerifyAccess();
        if ((update & InspectorUpdate.CloseHint) != 0) CloseHint();

        var model = DataContext;
        var replaced = !ReferenceEquals(_presentedContext, model);
        if (replaced)
        {
            _presentationGeneration++;
            update |= InspectorUpdate.All;
            foreach (var box in NumericEditors()) box.CancelGesture();
            this.AddChoicesPart.IsVisible = false;
        }
        if ((update & InspectorUpdate.RenderAssets) != 0)
            SynchronizeRenderAssets(model?.RenderAssets);
        if ((update & InspectorUpdate.Structure) != 0)
        {
            SynchronizeComponents(model);
            SynchronizeComponentChoices(model);
        }
        if ((update & InspectorUpdate.Materials) != 0) _materialContext.DataContext = model?.Materials;
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

    /// <summary>
    /// Hands the component list to its keyed binding. Sections keep their subtree, disclosure state and focus across a
    /// model refresh and across detach/reattach; a replaced view model advances the generation and rebuilds them.
    /// </summary>
    private void SynchronizeComponents(IEditorInspectorViewModel? model)
    {
        _componentRows.Clear();
        if (model != null) _componentRows.AddRange(model.Components);
        var presented = _componentRows.Select(component => component.Id).ToHashSet();
        _collapsed.RemoveWhere(id => !presented.Contains(id));
        _details.RemoveWhere(id => !presented.Contains(id));
        this.ComponentListPart.RefreshItems();
    }

    /// <summary>Builds one section and gives its field container a second keyed binding, so rows never overlap.</summary>
    private Element CreateComponentSection(InspectorComponent component)
    {
        var section = new ComponentSection(component);
        section.Header.SemanticId($"editor.inspector.component.{component.Id.Value:N}");
        section.Header.OnClick(() => ToggleComponent(section));
        section.Remove.OnClick(() => DataContext?.RemoveComponent(section.Address));
        section.Details.OnClick(() => ToggleDetails(section));
        // A specialized Transform section keeps its fields inside the compact editor instead of the bound body.
        if (!IsSpecializedTransform(component)) section.Body.BindItems(section.Fields,
            property => CreatePropertyRow(section, property),
            property => (property.Key, property, _presentationGeneration));
        section.Present(component);
        PresentSection(section);
        return section;
    }

    /// <summary>Brings one section to the live model: availability, disclosures, the specialized editor and the fields.</summary>
    private void PresentSection(ComponentSection section)
    {
        var id = section.Address;
        section.Remove.IsEnabled = DataContext?.CanChangeComponents ?? false;
        section.Header.Expanded = section.Body.IsVisible = !_collapsed.Contains(id);
        var visible = _details.Contains(id);
        section.Metadata.IsVisible = visible;
        section.Details.Tone = visible ? ControlTone.Accent : ControlTone.Neutral;
        if (section.TransformPanel != null) section.TransformPanel.IsVisible = visible;
        if (IsSpecializedTransform(section.Component))
        {
            if (DataContext is { } model && section.Transform == null) AddTransform(section, model);
            return;
        }

        section.Fields.Clear();
        section.Fields.AddRange(section.Component.Properties);
        section.Body.RefreshItems();
    }

    private static bool IsSpecializedTransform(InspectorComponent component) =>
        component.TypeId == BuiltInComponents.TransformId && component.SchemaVersion == 1;

    private FieldRow CreatePropertyRow(ComponentSection section, InspectorProperty property)
    {
        var model = DataContext ?? throw new InvalidOperationException("A property row requires a presented inspector model.");
        var parts = InspectorPropertyEditorFactory.Create(model, property, section.Component.Title,
            _details.Contains(section.Address),
            (host, choicesSlot) => ToggleRenderChoices(property, host, choicesSlot, null),
            () =>
            {
                if (DataContext is { } current && current.RenderAssets is { CanRestoreMeshMaterial: true } selection &&
                    selection.Component == property.ComponentId)
                    current.RestoreMeshMaterial(selection);
            },
            (state, host) => CreateRenderChoices(property, state, host));
        // A row created inside a reconcile must arrive fully presented: the later editor passes only refresh live rows.
        ApplyEditorState(parts.Controls);
        ApplyGenericState(parts.Row);
        ApplyRenderEditor(parts.Row);
        return parts.Row;
    }

    private void ToggleComponent(ComponentSection section)
    {
        if (!_collapsed.Add(section.Address)) _collapsed.Remove(section.Address);
        section.Header.Expanded = section.Body.IsVisible = !_collapsed.Contains(section.Address);
    }

    /// <summary>
    /// Hands both choice lists to their keyed bindings. Rows of surviving keys keep their instance, so focus, hover
    /// and click state survive a model refresh or a replaced view model; only new keys build a row and removed keys
    /// release one. The containers reject manual child edits while the bindings own them.
    /// </summary>
    private void SynchronizeComponentChoices(IEditorInspectorViewModel? model)
    {
        _addableTypes.Clear();
        _removedComponents.Clear();
        if (model != null)
        {
            _addableTypes.AddRange(model.AddableTypes);
            _removedComponents.AddRange(model.RemovedComponents);
        }
        this.AddChoicesPart.RefreshItems();
        this.RemovedPart.RefreshItems();
    }

    private Element CreateAddChoice(InspectorComponentType choice)
    {
        var button = new AddChoiceButton(choice).WithRole(ActionRole);
        // Click reads the row's current choice and the live model, so a reused row never keeps a stale capture.
        button.OnClick(() =>
        {
            if (DataContext is { } model)
            {
                model.AddComponent(button.Choice.Schema.TypeId, button.Choice.Schema.Version);
                this.AddChoicesPart.IsVisible = false;
            }
        });
        return button;
    }

    private void PresentAddChoice(Element element, InspectorComponentType choice)
    {
        var model = DataContext;
        ((AddChoiceButton)element).Present(choice, model != null && model.CanChangeComponents && choice.CanAdd);
    }

    private Element CreateRestoreRow(InspectorRemovedComponent removed)
    {
        var row = new RestoreRow(removed);
        row.Restore.OnClick(() => DataContext?.RestoreComponent(row.Address));
        return row;
    }

    private void PresentRestoreRow(Element element, InspectorRemovedComponent removed)
    {
        var model = DataContext;
        ((RestoreRow)element).Present(removed, model != null && model.CanChangeComponents);
    }

    private void SynchronizeComponentAvailability(IEditorInspectorViewModel model)
    {
        var canChange = model.CanChangeComponents;
        foreach (var section in Sections()) section.Remove.IsEnabled = canChange;
        foreach (var button in this.AddChoicesPart.Items.OfType<AddChoiceButton>())
            button.IsEnabled = canChange && button.Choice.CanAdd;
        foreach (var row in this.RemovedPart.Items.OfType<RestoreRow>())
            row.Restore.IsEnabled = canChange;
    }

    private void SynchronizeRenderAssets(InspectorRenderAssetSelection? assets)
    {
        if (ReferenceEquals(_shownRenderAssets, assets)) return;
        foreach (var list in RenderLists()) CloseRenderChoices(list);
        _shownRenderAssets = assets;
        _renderSnapshotVersion++;
        _openRenderSlot = null;
    }

    private void SynchronizeRenderEditors()
    {
        foreach (var row in PropertyRows()) ApplyRenderEditor(row);
    }

    private void ApplyRenderEditor(FieldRow row)
    {
        if (row.Choices == null) return;
        var controls = row.Controls;
        var picker = (Button)controls.Input;
        var choices = RenderChoices(controls.Property);
        picker.Value = choices.Items.FirstOrDefault(item => item.Id == choices.Selected)?.Name ?? "Выбрать";
        picker.IsEnabled = _shownRenderAssets?.Enabled == true && choices.Items.Count != 0;
        if (controls.Property.Key == "bindings")
        {
            controls.Reset.IsEnabled = _shownRenderAssets is { CanRestoreMeshMaterial: true } selection &&
                selection.Component == controls.Property.ComponentId;
            RefreshSlotPickers(row);
        }
    }

    private void SynchronizeEditors()
    {
        // Coalesce presentation updates instead of adding two dispatcher entries per field on every model reload.
        foreach (var section in Sections())
        {
            foreach (var row in section.Body.Items.OfType<FieldRow>())
            {
                ApplyEditorState(row.Controls);
                ApplyGenericState(row);
            }

            foreach (var field in section.TransformFields) ApplyEditorState(field.Controls);
        }
    }

    private static void ApplyEditorState(InspectorPropertyControls controls)
    {
        controls.Input.HasError = controls.Property.HasError;
        controls.Input.IsModified = controls.Property.HasDraft;
        controls.Input.ErrorMessage = controls.Property.Error;
        if (controls.Input is CheckBox boolean) boolean.Value = controls.Property.BooleanValue ? "Да" : "Нет";
    }

    private static void ApplyGenericState(FieldRow row)
    {
        row.Editor.ShowEmpty(row.Controls.Property.IsNull);
        row.Editor.Empty.IsModified = row.Controls.Property.HasDraft;
        row.Editor.Empty.HasError = row.Controls.Property.HasError;
        if (row.Controls.Null != null) row.Controls.Null.IsModified = row.Controls.Property.HasDraft;
    }

    private void AddTransform(ComponentSection section, IEditorInspectorViewModel model)
    {
        var parts = InspectorTransformEditorFactory.Create(this, model, section.Component);
        section.Transform = parts.Editor;
        section.TransformPanel = parts.Details;
        section.TransformFields.AddRange(parts.Fields);
        section.Body.Add(parts.Editor);
        section.Body.Add(parts.Details);
        parts.Details.IsVisible = _details.Contains(section.Address);
    }

    private (IReadOnlyList<InspectorRenderAssetChoice> Items, Guid? Selected) RenderChoices(InspectorProperty property)
    {
        if (_shownRenderAssets is not { } selection || selection.Component != property.ComponentId) return ([], null);
        return property.Key == "mesh" ? (selection.Meshes, selection.Mesh) : (selection.Materials, selection.Material);
    }

    private void RefreshSlotPickers(FieldRow row)
    {
        var controls = row.Controls;
        var property = controls.Property;
        if (controls.SlotPickers is not { } pickers || controls.RenderChoices is not { } choices) return;
        var selection = _shownRenderAssets;
        var slots = selection?.Component == property.ComponentId ? selection.Slots : [];
        controls.Input.SemanticId = slots.Count == 0 && selection?.Component == property.ComponentId &&
            selection.SingleSlotId is { } id ? $"editor.inspector.material.slot.{id:N}" : null;
        row.Editor.IsVisible = slots.Count == 0;
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
                    .OnClick(() => ToggleRenderChoices(property, choices.Host, choices.Slot, slot.Id));
                button.SemanticId = $"editor.inspector.material.slot.{slot.Id:N}";
                pickers.Buttons.Add(slot.Id, button);
                pickers.Panel.Add(button);
            }
            var materials = slot.Materials ?? selection!.Materials;
            var material = materials.FirstOrDefault(item => item.Id == slot.Material);
            button.Value = slot.Name + " · " + (material?.Name ?? "Выбрать");
            button.AccessibleName = "Материал слота " + slot.Name;
            button.IsEnabled = slot.CanSelect && materials.Count != 0;
            Reorder(pickers.Panel, button, i);
        }
    }

    private void ToggleRenderChoices(InspectorProperty property, ScrollViewer host, ConditionalContent<RenderChoicesState?> slot, Guid? slotId)
    {
        if (DataContext is not { } model || !ReferenceEquals(model, _presentedContext)) return;
        var open = !host.IsVisible || _openRenderSlot != slotId;
        foreach (var list in RenderLists()) if (!ReferenceEquals(list, host)) CloseRenderChoices(list);
        if (!open) { CloseRenderChoices(host); return; }
        var state = new RenderChoicesState(property.ComponentId, property.Key, slotId, _renderSnapshotVersion);
        if (RenderChoicesRequest(property, state).Selection is null) { CloseRenderChoices(host); return; }
        host.IsVisible = true;
        _openRenderSlot = slotId;
        // One state value for the whole list: clearing it releases the rows, so a settled reopen builds them again.
        slot.State = state;
    }

    /// <summary>Whether one choice request still addresses the presented model, the current snapshot and an open slot.</summary>
    private (InspectorRenderAssetSelection? Selection, InspectorRenderSlot? Slot) RenderChoicesRequest(
        InspectorProperty property, RenderChoicesState? state)
    {
        if (state is null || !ReferenceEquals(DataContext, _presentedContext) ||
            state.Component != property.ComponentId || state.PropertyKey != property.Key ||
            state.SnapshotVersion != _renderSnapshotVersion || _shownRenderAssets is not { Enabled: true } assets ||
            assets.Component != state.Component) return (null, null);
        var slot = state.SlotId.HasValue ? assets.Slots.FirstOrDefault(item => item.Id == state.SlotId) : null;
        if (state.SlotId.HasValue && slot is not { CanSelect: true }) return (null, null);
        return (assets, slot);
    }

    /// <summary>Builds the choice rows of one requested state; null releases the branch when the request is no longer current.</summary>
    private Element? CreateRenderChoices(InspectorProperty property, RenderChoicesState? state, ScrollViewer host)
    {
        var (selection, slot) = RenderChoicesRequest(property, state);
        if (selection is null || state is null) return null;
        var model = DataContext!;
        (IReadOnlyList<InspectorRenderAssetChoice> Items, Guid? Selected) choices =
            slot == null ? RenderChoices(property) : (slot.Materials ?? selection.Materials, slot.Material);
        var label = slot == null ? property.Label : "Материал слота " + slot.Name;
        var rows = new VStack().WithRole(StackRole);
        var materialSlotId = property.Key == "bindings" ? slot?.Id ?? selection.SingleSlotId : null;
        if (materialSlotId is { } currentSlot && choices.Selected is { } selectedMaterial)
            AddMaterialSlotActions(rows, model, selection, currentSlot, selectedMaterial, host);
        var authored = materialSlotId.HasValue && model.Materials is { Items.Count: > 0 } panel
            ? panel.Items.Select(static item => item.Id).ToHashSet()
            : null;
        foreach (var item in choices.Items)
        {
            var name = authored?.Contains(item.Id) == true ? MaterialDisplayName(item) : item.Name;
            var button = new Button(item.CanSelect ? name : $"{name} · недоступен").WithRole(ActionRole).OnClick(() =>
            {
                CloseRenderChoices(host);
                if (!item.CanSelect || !ReferenceEquals(DataContext, model)) return;
                if (slot == null) model.SelectRenderAsset(selection, property, item.Id);
                else model.SelectRenderSlotMaterial(selection, slot.Id, item.Id);
            });
            button.AccessibleName = label + " · " + button.Value;
            button.IsEnabled = item.CanSelect;
            if (materialSlotId is { } id)
                button.SemanticId = $"editor.inspector.material.choice.{id:N}.{item.Id:N}";
            button.Tone = item.Id == choices.Selected ? ControlTone.Accent : ControlTone.Neutral;
            rows.Add(button);
            if (slot == null && property.Key == "mesh" && selection.MeshesWithMaterialTransfer.Contains(item.Id))
            {
                var transfer = new Button("Выбрать с переносом материалов").WithRole(ActionRole).OnClick(() =>
                {
                    CloseRenderChoices(host);
                    if (ReferenceEquals(DataContext, model)) model.SelectMeshWithMaterialOverrides(selection, item.Id);
                });
                transfer.AccessibleName = "Выбрать с переносом материалов · " + item.Name;
                rows.Add(transfer);
            }
        }
        return rows;
    }

    private void CloseRenderChoices(ScrollViewer host)
    {
        if (!host.IsVisible) return;
        host.IsVisible = false;
        // The slot owns the branch: asking for no state releases its rows and their handlers.
        if (host.Child is ConditionalContent<RenderChoicesState?> slot) slot.State = null;
    }
    private IEnumerable<NumberBox> NumericEditors() => PropertyRows().Select(row => row.Number)
        .Concat(Sections().SelectMany(section => section.TransformFields.Select(field => field.Number))).OfType<NumberBox>();
    private IEnumerable<ComponentSection> Sections() =>
        IsDisposed ? [] : this.ComponentListPart.Items.OfType<ComponentSection>();
    private IEnumerable<FieldRow> PropertyRows() => Sections().SelectMany(section => section.Body.Items.OfType<FieldRow>());
    private IEnumerable<InspectorPropertyControls> AllFields() => PropertyRows().Select(row => row.Controls)
        .Concat(Sections().SelectMany(section => section.TransformFields.Select(field => field.Controls)));
    private IEnumerable<ScrollViewer> RenderLists() => PropertyRows().Select(row => row.Choices).OfType<ScrollViewer>();
    private static IEnumerable<InspectorPropertyControls> FieldsOf(ComponentSection section) =>
        section.Body.Items.OfType<FieldRow>().Select(row => row.Controls)
            .Concat(section.TransformFields.Select(field => field.Controls));

    private void ToggleDetails(ComponentSection section)
    {
        if (!_details.Add(section.Address)) _details.Remove(section.Address);
        var visible = _details.Contains(section.Address);
        section.Metadata.IsVisible = visible;
        section.Details.Tone = visible ? ControlTone.Accent : ControlTone.Neutral;
        if (section.TransformPanel != null) section.TransformPanel.IsVisible = visible;
        foreach (var controls in FieldsOf(section))
        {
            if (!controls.Property.IsRenderReference) controls.Reset.IsVisible = visible;
            controls.Source.IsVisible = visible;
            controls.Source.Parent!.IsVisible = visible;
        }
    }
    private static void Reorder(VStack parent, Element child, int index)
    {
        var items = parent.Items;
        if (!ReferenceEquals(items[index], child)) items.Move(items.IndexOf(child), index);
    }

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
        if (MaterialTextHasFocus() || AllFields().Any(controls => controls.Input is TextBox or RichTextEditor && controls.Input.IsFocused)) return false;
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
            if (!ReferenceEquals(hint, _hint.Target))
            {
                CloseHint();
                if (hint != null && FindOverlay() is { Popup: null } host)
                {
                    _hint.Start(hint, input.X, input.Y, TimeSpan.FromMilliseconds(500));
                    _hintHost = host;
                    host.Closed += HintClosed;
                    for (Element? node = this; node != null; node = node.Parent)
                    { _hintAncestors.Add(node); node.PropertyChanged += HintAncestorChanged; }
                }
            }
        }
        else if (input.Kind is InputKind.PointerDown or InputKind.KeyDown or InputKind.FocusLost or InputKind.Wheel) CloseHint();
        if (PreviewMaterialTextInput(input, context)) return true;
        if (input.Kind != InputKind.KeyDown || !(input.Control && input.Key == UiKey.Enter || input.Key == UiKey.Escape))
            return false;
        for (var focused = context.FocusedElement; focused != null; focused = focused.Parent)
            if (ReferenceEquals(this, focused)) return HandleShortcut(input);
        return false;
    }
    /// <inheritdoc />
    public void AdvanceFrame(TimeSpan elapsed, IInputContext context)
    {
        FocusRequestedMaterial(context);
        if (_hint.Target is not { } target) return;
        var layoutPending = context.HasPendingLayout;
        if (!layoutPending && !HintIsCurrent()) { CloseHint(); return; }
        if (!_hint.Advance(elapsed, layoutPending)) return;
        if (_hintHost!.Popup != null) { CloseHint(); return; }
        var popup = new HintSurface { Style = EditorInspectorViewStyle.HintBorder }.Content(
            new Text(target.AccessibleName + (target.IsEffectivelyEnabled ? "" : " · сейчас недоступно")) { Style = EditorInspectorViewStyle.HintText });
        try { _hintHost.Open(this, popup, target.Bounds, context, tooltip: true); }
        catch { if (popup.Parent == null && !popup.IsDisposed) popup.Dispose(); throw; }
    }
    private bool HintIsCurrent() => ReferenceEquals(FindOverlay(), _hintHost) &&
        _hint.IsCurrent(IconAt(this, _hint.X, _hint.Y));
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
        _hintAncestors.Clear(); _hint.Reset(); _hintHost = null;
    }
    private void Attachment()
    {
        if (IsAttached) return;
        _requestMaterialFocus = false;
        _focusMaterialModel = null;
        _materialInputField = null;
        _clearMaterialInputQueued = false;
        ForgetHint();
    }
    internal static IconButton Action(UiSymbol symbol, string name)
        => ConfigureAction(new IconButton(symbol, name));
    internal static IconButton ConfigureAction(IconButton button)
    {
        button.Clear(UiProperties.Width); button.Clear(UiProperties.Height); button.Clear(UiProperties.Padding);
        return button.WithRole(ActionRole).WithRole(IconRole);
    }
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
        Materials = 16,
        All = Structure | EditorState | RenderAssets | Materials
    }

    // Keep the existing public part handles inspectable after disposal.
    /// <summary>Addition choice row kept by its type and schema key; the label and availability follow the current record.</summary>
    private sealed class AddChoiceButton : Button
    {
        public AddChoiceButton(InspectorComponentType choice) : base(choice.Title) => Choice = choice;
        internal InspectorComponentType Choice { get; private set; }
        internal (ComponentTypeId, int) Address => (Choice.Schema.TypeId, Choice.Schema.Version);
        internal void Present(InspectorComponentType choice, bool enabled)
        {
            Choice = choice;
            Value = choice.Title;
            IsEnabled = enabled;
        }
    }

    /// <summary>Restoration row kept by the hidden occurrence key; action name and label follow the current record.</summary>
    private sealed class RestoreRow : HStack
    {
        public RestoreRow(InspectorRemovedComponent removed)
        {
            this.WithRole(RowRole);
            Add(Restore = Action(UiSymbol.Redo, RestoreName(removed)));
            Add(Title = new Text(removed.Title).WithRole(MetadataRole));
            Removed = removed;
        }
        internal IconButton Restore { get; }
        internal Text Title { get; }
        internal InspectorRemovedComponent Removed { get; private set; }
        internal ComponentId Address => Removed.Id;
        internal void Present(InspectorRemovedComponent removed, bool enabled)
        {
            Removed = removed;
            Restore.AccessibleName = RestoreName(removed);
            Title.Value = removed.Title;
            Restore.IsEnabled = enabled;
        }
        private static string RestoreName(InspectorRemovedComponent removed) => "Восстановить компонент " + removed.Title;
    }

    /// <summary>
    /// Component section kept by its occurrence id inside one presentation context: a model refresh reuses the section,
    /// its focus and its disclosure state, while a replaced context builds a new one because every draft record and
    /// captured action belongs to the previous view model. Handlers read the component the section currently presents.
    /// </summary>
    private sealed class ComponentSection : Border
    {
        public ComponentSection(InspectorComponent component)
        {
            Component = component;
            Header = new ComponentHeader().WithRole(HeaderRole);
            Details = Action(UiSymbol.More, DetailsName(component));
            Remove = Action(UiSymbol.Delete, RemoveName(component));
            Metadata = new Text().WithRole(MetadataRole);
            Body = new VStack().WithRole(StackRole);
            this.WithRole(SectionRole).Content(new VStack().WithRole(StackRole).WithChildren(
                new ComponentLine(Header, Details, Remove), Metadata, Body));
            Controls = new InspectorComponentControls(this, Header, Body, Remove, Metadata);
        }

        internal InspectorComponent Component { get; private set; }
        internal ComponentId Address => Component.Id;
        internal ComponentHeader Header { get; }
        internal IconButton Details { get; }
        internal Button Remove { get; }
        internal Text Metadata { get; }
        internal VStack Body { get; }
        internal InspectorComponentControls Controls { get; }
        /// <summary>Source mirror of the field binding; a specialized Transform section keeps its body unbound.</summary>
        internal readonly List<InspectorProperty> Fields = [];
        internal TransformEditor? Transform { get; set; }
        internal VStack? TransformPanel { get; set; }
        internal readonly List<InspectorTransformEditorFactory.Field> TransformFields = [];
        internal void Present(InspectorComponent component)
        {
            Component = component;
            Header.Value = component.Title;
            Header.AccessibleName = component.Title + " · свернуть или раскрыть";
            Metadata.Value = component.SourceText + " · v" + component.SchemaVersion + " · " + component.Id.Value.ToString("D")[..8];
            Metadata.AccessibleName = $"Type {component.TypeId.Value:D}; component {component.Id.Value:D}; schema {component.SchemaVersion}";
            Details.AccessibleName = DetailsName(component);
            Remove.AccessibleName = RemoveName(component);
        }
        private static string DetailsName(InspectorComponent component) => "Сведения и сброс значений · " + component.Title;
        private static string RemoveName(InspectorComponent component) => "Удалить компонент " + component.Title;
    }

    /// <summary>One schema field subtree kept by its exact draft record, so the binding owns create and dispose.</summary>
    internal sealed class FieldRow : VStack
    {
        internal InspectorPropertyControls Controls { get; private set; } = null!;
        internal PropertyEditorRow Editor { get; private set; } = null!;
        internal NumberBox? Number { get; private set; }
        internal ScrollViewer? Choices { get; private set; }
        internal (ComponentId, string) Address => (Controls.Property.ComponentId, Controls.Property.Key);
        internal void Initialize(InspectorPropertyControls controls, PropertyEditorRow editor, NumberBox? number, ScrollViewer? choices)
        {
            Controls = controls;
            Editor = editor;
            Number = number;
            Choices = choices;
        }
    }

    /// <summary>One requested choice list: the field and slot it belongs to plus the asset snapshot it was opened against.</summary>
    internal sealed record RenderChoicesState(ComponentId Component, string PropertyKey, Guid? SlotId, int SnapshotVersion);

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
