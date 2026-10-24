using System.ComponentModel;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Schema-driven inspector drafts over one shared document authoring history; borrows App-owned dependencies.</summary>
public class EditorInspectorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EditorDocumentCommands _commands;
    private readonly EditorPreview _preview;
    private SceneDocument? _baseline;
    private EntityId _entity;
    private ComponentId _component;
    private string? _selection;
    private readonly Dictionary<(ComponentId, string), InspectorProperty> _properties = [];
    private IReadOnlyList<InspectorComponent> _components = [];
    private IReadOnlyList<InspectorComponentType> _addable = [];
    private IReadOnlyList<InspectorRemovedComponent> _removed = [];
    private InspectorProperty[] _active = [];
    private bool _canEditProperties, _canChangeComponents;
    private int _owner;
    private bool _loading, _disposed, _available, _conflict;
    private bool _canEdit, _canApply, _canCancel, _canUndo, _canRedo, _canPreview, _canStopPreview;
    private string _error = "", _previewText = "Preview выключен · до 64 объектов";
    private EditorPreviewState _previewState = new(false, false, null, null);

    /// <summary>Creates presentation over borrowed App-owned services.</summary>
    /// <param name="commands">Shared document admission and operations.</param><param name="preview">Bounded preview coordinator.</param>
    public EditorInspectorViewModel(EditorDocumentCommands commands, EditorPreview preview)
    {
        ArgumentNullException.ThrowIfNull(commands); ArgumentNullException.ThrowIfNull(preview);
        _commands = commands; _preview = preview;
        Fields = new[] { ("x", "X"), ("y", "Y"), ("z", "Z"), ("pitch", "Pitch, рад"), ("yaw", "Yaw, рад"), ("roll", "Roll, рад"), ("sx", "Scale X"), ("sy", "Scale Y"), ("sz", "Scale Z") }
            .Select(x => new TransformField(x.Item1, x.Item2, VerifyOwner)).ToArray();
        foreach (var field in Fields) field.Changed += DraftChanged;
    }
    /// <summary>Gets the nine stable numeric fields for legacy Transform form consumers. Dynamic UI uses Components.</summary>
    public IReadOnlyList<TransformField> Fields { get; }
    /// <summary>Gets whether the selected occurrence can be edited.</summary>
    public bool CanEdit => _canEdit;
    /// <summary>Gets valid, changed, nonconflicting draft availability.</summary>
    public bool CanApply => _canApply;
    /// <summary>Gets whether draft cancellation/reload is available.</summary>
    public bool CanCancel => _canCancel;
    /// <summary>Gets engine Undo availability; uncommitted drafts must first be applied/cancelled.</summary>
    public bool CanUndo => _canUndo;
    /// <summary>Gets engine Redo availability.</summary>
    public bool CanRedo => _canRedo;
    /// <summary>Gets explicit preview start/retry availability.</summary>
    public bool CanPreview => _canPreview;
    /// <summary>Gets preview stop availability.</summary>
    public bool CanStopPreview => _canStopPreview;
    /// <summary>Gets whether text differs from the captured baseline.</summary>
    public bool HasDraft => _active.Any(item => item.HasDraft);
    /// <summary>Gets field-independent mutation/conflict diagnostics.</summary>
    public string ErrorText => _error.ReplaceLineEndings("\n");
    /// <summary>Gets whether a complete mutation/conflict diagnostic is available.</summary>
    public bool HasError => _error.Length != 0;
    /// <summary>Gets actual presented and requested revision information.</summary>
    public string PreviewText => _previewText;
    /// <summary>Actual preview state for structured status presentation.</summary>
    public EditorPreviewState PreviewState => _previewState;
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Copies committed state on the UI thread. Selection changes discard the previous selection's draft;
    /// external revision changes preserve a draft and require explicit cancellation.</summary>
    /// <param name="selectedId">Stable entity GUID, or null.</param>
    public void Refresh(string? selectedId)
    {
        VerifyOwner(); var state = _commands.Read(); var content = state.Document?.Content;
        var changedSelection = selectedId != _selection || _baseline?.Id != content?.Document.Id;
        _selection = selectedId;
        if (changedSelection || !HasDraft && !ReferenceEquals(_baseline, content?.Document)) Load(content);
        else if (!ReferenceEquals(_baseline, content?.Document)) _conflict = true;
        _available = state.RuntimeIdle && !state.Busy && !state.Exiting && !state.Closed && state.Document?.Operation == DocumentOperation.None;
        if (content != null) _preview.Synchronize(content.Resolved);
        UpdateState(content);
    }
    /// <summary>Gets only effective components of the selected authoring entity.</summary>
    public IReadOnlyList<InspectorComponent> Components => _components;
    /// <summary>Gets exact schemas and current dependency/multiplicity admission for addition.</summary>
    public IReadOnlyList<InspectorComponentType> AddableTypes => _addable;
    /// <summary>Gets hidden inherited occurrences, preserving their identities.</summary>
    public IReadOnlyList<InspectorRemovedComponent> RemovedComponents => _removed;
    /// <summary>Gets whether the selection has an authoring representation.</summary>
    public bool HasSelection => _entity != default;
    /// <summary>Gets whether empty-selection guidance should be shown.</summary>
    public bool NoSelection => !HasSelection;
    /// <summary>Gets a selection or empty-document description.</summary>
    public string SelectionText => _entity != default ? EditorScene.Name(_entity.Value.ToString("D")) :
        _baseline == null ? "Нет открытой authoring-сцены" : "Сущность не выбрана";
    /// <summary>Gets editing admission for any component, including entities without Transform.</summary>
    public bool CanEditProperties => _canEditProperties;
    /// <summary>Gets presence editing admission; complete or cancel property drafts first.</summary>
    public bool CanChangeComponents => _canChangeComponents;

    private void Load(SceneAuthoringState? content)
    {
        _baseline = content?.Document; _component = default; _entity = default; _conflict = false;
        Set(ref _error, "", nameof(ErrorText)); _loading = true;
        try
        {
            var groups = new List<InspectorComponent>(); var hidden = new List<InspectorRemovedComponent>();
            ResolvedEntity? entity = null;
            if (content != null && Guid.TryParse(_selection, out var id)) content.Resolved.Entities.TryGetValue(new(id), out entity);
            var wanted = new HashSet<(ComponentId, string)>();
            if (entity != null)
            {
                _entity = entity.Id;
                foreach (var component in entity.Components.Values.OrderBy(c => c.TypeId == BuiltInComponents.TransformId ? 0 : 1).ThenBy(c => c.Id.Value))
                {
                    var schema = content!.Document.Schemas.Single(s => s.TypeId == component.TypeId && s.Version == component.SchemaVersion);
                    var fields = new List<InspectorProperty>();
                    var ordered = schema.Properties.Values.OrderBy(p => component.TypeId == BuiltInComponents.TransformId ?
                        Array.FindIndex(Fields.ToArray(), f => f.Key == p.Id) : 0).ThenBy(p => p.Id, StringComparer.Ordinal);
                    foreach (var property in ordered)
                    {
                        var address = (component.Id, property.Id); wanted.Add(address);
                        InspectorProperty field;
                        if (component.TypeId == BuiltInComponents.TransformId)
                        {
                            field = Fields.Single(f => f.Key == property.Id); field.ComponentId = component.Id; _component = component.Id;
                        }
                        else if (!_properties.TryGetValue(address, out field!) || field.Schema.Kind != property.Kind ||
                            field.Schema.IsNullable != property.IsNullable || field.Schema.DefaultValue != property.DefaultValue)
                        {
                            if (field != null) field.Changed -= DraftChanged;
                            field = new(component.Id, property, property.Id, VerifyOwner);
                            field.Changed += DraftChanged;
                        }
                        _properties[address] = field;
                        var value = component.Properties[property.Id];
                        field.Load(value.Value, Source(value.Source, value.IsSchemaDefault),
                            value.Source.EntityId == entity.Id && !value.IsSchemaDefault);
                        fields.Add(field);
                    }
                    groups.Add(new(component.Id, component.TypeId, component.SchemaVersion, ComponentName(component.TypeId),
                        Source(component.PresenceSource, false), fields.AsReadOnly()));
                }
                var instance = content!.Document.Entities.Single(e => e.Id == entity.Id);
                var known = new Dictionary<ComponentId, ComponentData>();
                foreach (var definition in entity.DefinitionChain.Select(key => content.Document.Definitions.Single(d => d.Id == key)))
                    foreach (var data in definition.Changes.AddedComponents) known[data.Id] = data;
                foreach (var data in instance.Changes.AddedComponents) known[data.Id] = data;
                foreach (var data in known.Values.Where(c => !entity.Components.ContainsKey(c.Id)).OrderBy(c => c.Id.Value))
                    hidden.Add(new(data.Id, ComponentName(data.TypeId), instance.Changes.RemovedComponents.Contains(data.Id)));
            }
            foreach (var address in _properties.Keys.Where(key => !wanted.Contains(key)).ToArray())
            {
                var field = _properties[address];
                if (field is not TransformField) field.Changed -= DraftChanged;
                _properties.Remove(address);
            }
            if (_component == default) foreach (var field in Fields) field.Load(PropertyValue.Null, "", false);
            _components = groups.AsReadOnly(); _active = groups.SelectMany(c => c.Properties).ToArray(); _removed = hidden.AsReadOnly();
            var types = entity?.Components.Values.Select(c => c.TypeId).ToHashSet() ?? [];
            _addable = content == null || entity == null ? [] : content.Document.Schemas.OrderBy(s => s.TypeId.Value).ThenBy(s => s.Version)
                .Select(schema => new InspectorComponentType(schema, ComponentName(schema.TypeId) + " v" + schema.Version,
                    (schema.AllowMultiple || !types.Contains(schema.TypeId)) && schema.RequiredTypes.All(types.Contains))).ToArray();
            foreach (var name in new[] { nameof(Components), nameof(AddableTypes), nameof(RemovedComponents), nameof(HasSelection), nameof(NoSelection), nameof(SelectionText), nameof(HasDraft) }) Notify(name);
        }
        finally { _loading = false; }
    }
    /// <summary>Gets a built-in name or a stable fallback for a schema-defined component.</summary>
    /// <param name="type">Persistent component type.</param><returns>A presentation name, never used for addressing.</returns>
    public static string ComponentName(ComponentTypeId type) => type == BuiltInComponents.TransformId ? "Transform" :
        type == BuiltInComponents.AppearanceId ? "Appearance" : type == BuiltInComponents.InteractionId ? "Interaction" :
        "Component " + type.Value.ToString("D")[..8];
    private static string Source(EntityValueSource source, bool schemaDefault) => schemaDefault ? "По умолчанию из схемы" :
        source.EntityId != null ? "Локальное значение" :
        "Наследуется · " + source.DefinitionId?.Value.ToString("D")[..8] + " r" + source.DefinitionRevision;

    private void DraftChanged()
    {
        if (_loading) return;
        Set(ref _error, "", nameof(ErrorText)); UpdateState(_commands.Read().Document?.Content); Notify(nameof(HasDraft));
    }
    private bool Validate()
    {
        var valid = true;
        foreach (var field in _active) valid &= field.TryValue(out _);
        return valid;
    }
    private void UpdateState(SceneAuthoringState? content)
    {
        var valid = Validate(); var draft = HasDraft;
        Set(ref _canEdit, _available && _component != default, nameof(CanEdit));
        Set(ref _canEditProperties, _available && _entity != default, nameof(CanEditProperties));
        Set(ref _canChangeComponents, CanEditProperties && !draft && !_conflict, nameof(CanChangeComponents));
        foreach (var field in _active) field.Enable(CanEditProperties);
        Set(ref _canApply, CanEditProperties && draft && valid && !_conflict, nameof(CanApply));
        Set(ref _canCancel, draft || _conflict, nameof(CanCancel));
        Set(ref _canUndo, _available && !draft && !_conflict && content?.UndoCount > 0, nameof(CanUndo));
        Set(ref _canRedo, _available && !draft && !_conflict && content?.RedoCount > 0, nameof(CanRedo));
        var preview = _preview.Read();
        Set(ref _previewState, preview, nameof(PreviewState));
        Set(ref _canPreview, _available && content != null && !preview.Busy, nameof(CanPreview));
        Set(ref _canStopPreview, preview.Active || preview.Busy || preview.Error != null, nameof(CanStopPreview));
        var shown = preview.Presented == null ? "нет кадра" : $"кадр r{preview.Presented.Revision}";
        var text = preview.Error != null ? "Preview: " + preview.Error.Message :
            preview.Active ? $"Preview: {shown} / документ r{content?.Document.Revision} {(preview.Busy ? "· обновление…" : "")}" : "Preview выключен · до 64 объектов";
        Set(ref _previewText, text, nameof(PreviewText));
        if (_conflict) Set(ref _error, "Сцена изменилась. Отмените черновик и повторите правку.", nameof(ErrorText));
    }
    /// <summary>Commits changed properties/resets as one shared authoring transaction.</summary>
    public void Apply()
    {
        VerifyOwner(); Refresh(_selection); if (!CanApply || !Validate()) return;
        var replaceTransform = ReplaceWholeTransform && Fields.Any(field => field.HasDraft);
        var edits = _active.Where(field => field.HasDraft || replaceTransform && field is TransformField).Select(field =>
        {
            field.TryValue(out var value);
            return new PropertyEdit(_entity, field.ComponentId, field.Key, value, field.IsReset);
        }).ToArray();
        Mutate(() => _commands.EditComponents(_baseline!, new(Operation(), edits)));
    }
    /// <summary>Compatibility policy for consumers that requested a full nine-value Transform replacement.</summary>
    protected virtual bool ReplaceWholeTransform => false;
    /// <summary>Stages an inheritance/schema reset in the current property draft.</summary>
    /// <param name="property">Current retained field model.</param>
    public void ResetProperty(InspectorProperty property)
    {
        ArgumentNullException.ThrowIfNull(property); VerifyOwner(); Refresh(_selection);
        if (CanEditProperties && !_conflict && _active.Contains(property)) property.Reset();
    }
    /// <summary>Adds a new component occurrence using schema defaults.</summary>
    /// <param name="type">Exact type.</param><param name="version">Exact version.</param>
    public void AddComponent(ComponentTypeId type, int version) => ChangeComponent(() =>
    {
        var schema = _addable.SingleOrDefault(s => s.Schema.TypeId == type && s.Schema.Version == version);
        if (schema?.CanAdd != true) throw new InvalidOperationException("Нарушена кратность или отсутствует обязательный компонент.");
        var id = new ComponentId(Guid.NewGuid());
        return new(_entity, id, ComponentChangeKind.Add, new(id, type, version));
    });
    /// <summary>Removes a local occurrence or masks an inherited occurrence.</summary>
    /// <param name="component">Current occurrence identity.</param>
    public void RemoveComponent(ComponentId component) => ChangeComponent(() => new(_entity, component, ComponentChangeKind.Remove));
    /// <summary>Restores an occurrence hidden by this instance or an ancestor.</summary>
    /// <param name="component">Hidden occurrence identity.</param>
    public void RestoreComponent(ComponentId component) => ChangeComponent(() => new(_entity, component, ComponentChangeKind.Restore));
    /// <summary>Clears an instance removal mask while preserving ancestor masks.</summary>
    /// <param name="component">Locally masked occurrence.</param>
    public void ResetRemoval(ComponentId component) => ChangeComponent(() => new(_entity, component, ComponentChangeKind.ResetRemoval));
    private void ChangeComponent(Func<ComponentEdit> change)
    {
        VerifyOwner(); var expected = _baseline; Refresh(_selection);
        if (!ReferenceEquals(expected, _baseline)) { Set(ref _error, "Сцена изменилась. Повторите действие.", nameof(ErrorText)); return; }
        if (!CanChangeComponents) return;
        Mutate(() => _commands.EditComponents(_baseline!, new(Operation(), [], [change()])));
    }
    private AuthoringOperation Operation() => new(Guid.NewGuid(), _baseline!.Revision, "editor");
    /// <summary>Discards uncommitted text and reloads the latest selected occurrence.</summary>
    public void Cancel() { VerifyOwner(); Load(_commands.Read().Document?.Content); Refresh(_selection); Notify(nameof(HasDraft)); }
    /// <summary>Uses the document's existing engine Undo history.</summary>
    public void Undo() { VerifyOwner(); Refresh(_selection); if (CanUndo) Mutate(() => _commands.Undo(_baseline!, new(Guid.NewGuid(), _baseline!.Revision, "editor"))); }
    /// <summary>Uses the document's existing engine Redo history.</summary>
    public void Redo() { VerifyOwner(); Refresh(_selection); if (CanRedo) Mutate(() => _commands.Redo(_baseline!, new(Guid.NewGuid(), _baseline!.Revision, "editor"))); }
    private void Mutate(Func<SceneAuthoringResult> action)
    {
        try
        {
            var result = action();
            if (result.Succeeded) Load(_commands.Read().Document?.Content);
            else Set(ref _error, result.Error + ": " + result.Message, nameof(ErrorText));
        }
        catch (Exception error) { Set(ref _error, error.Message, nameof(ErrorText)); }
        var failure = _error; Refresh(_selection); if (failure.Length != 0) Set(ref _error, failure, nameof(ErrorText));
        Notify(nameof(HasDraft));
    }
    /// <summary>Enables committed-revision preview or retries a failed revision.</summary>
    public void ShowPreview() { VerifyOwner(); Refresh(_selection); if (CanPreview && _commands.Read().Document?.Content is { } content) _preview.Show(content.Resolved); Refresh(_selection); }
    /// <summary>Fences and drains preview, preserving document/history and uncommitted input.</summary>
    /// <returns>Actual cleanup completion.</returns>
    public Task StopPreviewAsync() { VerifyOwner(); return _preview.StopAsync(); }
    private void VerifyOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed, this); var thread = Environment.CurrentManagedThreadId;
        if (Interlocked.CompareExchange(ref _owner, thread, 0) is var owner && owner != 0 && owner != thread)
            throw new InvalidOperationException("Transform presentation must be updated on its UI owner thread.");
    }
    private void Notify(string property)
    {
        PropertyChanged?.Invoke(this, new(property));
        if (property == nameof(ErrorText)) PropertyChanged?.Invoke(this, new(nameof(HasError)));
    }
    private void Set<T>(ref T field, T value, string property) { if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; Notify(property); } }
    /// <summary>Disconnects field callbacks; borrowed document and preview owners remain alive.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        foreach (var field in Fields.Cast<InspectorProperty>().Concat(_properties.Values).Distinct()) field.Changed -= DraftChanged;
        PropertyChanged = null;
    }
}
