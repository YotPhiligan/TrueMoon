using System.Runtime.CompilerServices;
using TrueMoon.Argentis;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Schema-driven inspector drafts over one shared document authoring history; borrows App-owned dependencies.</summary>
public class EditorInspectorViewModel : ViewModel, IDisposable
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
    private string _validationText = "";
    private EditorPreviewState _previewState = new(false, false, null, null);
    private bool _scaleLinked, _linkingScale, _restoringGesture, _canLinkScale;
    private double[] _scaleReference = [1, 1, 1];
    private string _scaleLinkError = "";
    private InspectorProperty? _scaleErrorField;
    private NumericDraftSession? _numericGesture;
    private long _nextGestureId;
    private sealed record NumericDraftSession(long Id, InspectorProperty Source, SceneDocument? Document, string? Selection,
        (InspectorProperty Field, InspectorProperty.DraftState Draft)[] Drafts);
    /// <summary>Opt-in proportional scale editing. Enabling it does not change any field or commit data.</summary>
    public bool ScaleLinked
    {
        get => _scaleLinked;
        set
        {
            VerifyOwner(); if (_scaleLinked == value) return;
            FinishNumericGesture(NumericGestureResult.Cancelled, _numericGesture?.Id ?? 0);
            _scaleLinked = value; _scaleLinkError = ""; _scaleErrorField = null; RebaseScale();
            OnPropertyChanged(); UpdateState(_commands.Read().Document?.Content);
        }
    }
    /// <summary>Whether the selected Transform's link preference can be changed.</summary>
    public bool CanLinkScale => _canLinkScale;

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
    /// <summary>Complete copyable mutation and field diagnostics, including messages clipped in a narrow numeric column.</summary>
    public string DiagnosticsText => string.Join("\n", new[] { ErrorText, _validationText }.Where(text => text.Length != 0));
    /// <summary>Whether the inspector should expose its full diagnostics panel.</summary>
    public bool HasDiagnostics => HasError || _validationText.Length != 0;
    /// <summary>Gets actual presented and requested revision information.</summary>
    public string PreviewText => _previewText;
    /// <summary>Actual preview state for structured status presentation.</summary>
    public EditorPreviewState PreviewState => _previewState;

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
        SetProperty(ref _error, "", nameof(ErrorText)); _loading = true;
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
            foreach (var name in new[] { nameof(Components), nameof(AddableTypes), nameof(RemovedComponents), nameof(HasSelection), nameof(NoSelection), nameof(SelectionText), nameof(HasDraft) }) OnPropertyChanged(name);
        }
        finally { _loading = false; _scaleLinkError = ""; _scaleErrorField = null; RebaseScale(); }
    }
    /// <summary>Gets a built-in name or a stable fallback for a schema-defined component.</summary>
    /// <param name="type">Persistent component type.</param><returns>A presentation name, never used for addressing.</returns>
    public static string ComponentName(ComponentTypeId type) => type == BuiltInComponents.TransformId ? "Transform" :
        type == BuiltInComponents.AppearanceId ? "Appearance" : type == BuiltInComponents.InteractionId ? "Interaction" :
        "Component " + type.Value.ToString("D")[..8];
    private static string Source(EntityValueSource source, bool schemaDefault) => schemaDefault ? "По умолчанию из схемы" :
        source.EntityId != null ? "Локальное значение" :
        "Наследуется · " + source.DefinitionId?.Value.ToString("D")[..8] + " r" + source.DefinitionRevision;

    private void DraftChanged(InspectorProperty? changed = null)
    {
        if (_loading || _restoringGesture) return;
        if (!_linkingScale && changed is TransformField && changed.Key is "sx" or "sy" or "sz")
        {
            if (ScaleLinked) PropagateScale(changed); else RebaseScale();
        }
        if (_linkingScale) return;
        SetProperty(ref _error, "", nameof(ErrorText)); UpdateState(_commands.Read().Document?.Content); OnPropertyChanged(nameof(HasDraft));
    }
    private TransformField[] ScaleFields => Fields.Where(item => item.Key is "sx" or "sy" or "sz").ToArray();
    private static bool TryScale(InspectorProperty field, out double value) =>
        double.TryParse(field.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) &&
        value > 0 && float.IsFinite((float)value) && (float)value > 0;
    private void RebaseScale()
    {
        var fields = ScaleFields; var values = new double[3];
        for (var i = 0; i < 3; i++) if (fields[i].IsNull || fields[i].IsReset || !TryScale(fields[i], out values[i])) return;
        _scaleReference = values;
    }
    private void PropagateScale(InspectorProperty source)
    {
        _scaleLinkError = ""; _scaleErrorField = null;
        if (source.IsReset || source.IsNull || !TryScale(source, out var value)) return;
        var fields = ScaleFields; var index = Array.IndexOf(fields, source);
        if (index < 0) return;
        var reference = _scaleReference; NumericDraftSession? captured = null;
        if (_numericGesture is { Drafts.Length: 3 } gesture && IsNumericGestureCurrent(source, gesture.Id))
        {
            var baseline = new double[3]; var validBaseline = true;
            for (var i = 0; i < 3; i++) validBaseline &= double.TryParse(gesture.Drafts[i].Draft.Value,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out baseline[i]) && baseline[i] > 0;
            if (validBaseline) { reference = baseline; captured = gesture; }
        }
        var ratio = value / reference[index]; var values = new double[3];
        for (var i = 0; i < 3; i++)
        {
            values[i] = i == index ? value : ProportionalScale(reference[i], value, reference[index], ratio);
            if (fields[i].IsReset || fields[i].IsNull || i != index && !TryScale(fields[i], out _) ||
                values[i] <= 0 || !float.IsFinite((float)values[i]) || (float)values[i] <= 0)
            {
                _scaleLinkError = "Связанный масштаб выходит за допустимый диапазон или содержит незавершённый черновик/сброс.";
                _scaleErrorField = source; return;
            }
        }
        _linkingScale = true;
        try
        {
            for (var i = 0; i < 3; i++) if (i != index)
                fields[i].Value = captured != null && source.Value == captured.Drafts[index].Draft.Value
                    ? captured.Drafts[i].Draft.Value : values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
        finally { _linkingScale = false; }
        _scaleReference = values;
    }
    private static double ProportionalScale(double target, double value, double source, double ratio)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (decimal.TryParse(target.ToString("R", culture), System.Globalization.NumberStyles.Float, culture, out var a) && (double)a == target &&
            decimal.TryParse(value.ToString("R", culture), System.Globalization.NumberStyles.Float, culture, out var b) && (double)b == value &&
            decimal.TryParse(source.ToString("R", culture), System.Globalization.NumberStyles.Float, culture, out var c) && c != 0 && (double)c == source)
        {
            try { return (double)checked(a * b / c); }
            catch (OverflowException) { }
        }
        return target * ratio;
    }
    internal bool CanAdjustNumeric(InspectorProperty property)
    {
        if (_disposed) return false;
        VerifyOwner(); var state = _commands.Read();
        return _active.Contains(property) && property.CanWrite && !_conflict && state.RuntimeIdle && !state.Busy && !state.Exiting && !state.Closed &&
            state.Document?.Operation == DocumentOperation.None && ReferenceEquals(state.Document?.Content?.Document, _baseline);
    }
    internal bool IsNumericGestureCurrent(InspectorProperty property, long id) => !_disposed && _numericGesture is { } gesture && gesture.Id == id &&
        ReferenceEquals(gesture.Source, property) && ReferenceEquals(gesture.Document, _baseline) && gesture.Selection == _selection &&
        ReferenceEquals(gesture.Document, _commands.Read().Document?.Content?.Document) && _active.Contains(property);
    internal long BeginNumericGesture(InspectorProperty property)
    {
        if (!CanAdjustNumeric(property)) return 0;
        var fields = ScaleLinked && property is TransformField && property.Key is "sx" or "sy" or "sz" ? ScaleFields.Cast<InspectorProperty>().ToArray() : [property];
        var id = unchecked(++_nextGestureId); if (id == 0) id = unchecked(++_nextGestureId);
        _numericGesture = new(id, property, _baseline, _selection, fields.Select(field => (field, field.CaptureDraft())).ToArray());
        return id;
    }
    internal void FinishNumericGesture(NumericGestureResult result, long id)
    {
        var gesture = _numericGesture; if (gesture == null || gesture.Id != id) return; _numericGesture = null;
        if (_disposed || gesture == null || result != NumericGestureResult.Cancelled || !ReferenceEquals(gesture.Document, _baseline) ||
            gesture.Selection != _selection || !ReferenceEquals(gesture.Document, _commands.Read().Document?.Content?.Document) ||
            gesture.Drafts.Any(item => !_active.Contains(item.Field))) return;
        _restoringGesture = true;
        try { foreach (var item in gesture.Drafts) item.Field.RestoreDraft(item.Draft); }
        finally { _restoringGesture = false; }
        _scaleLinkError = ""; _scaleErrorField = null; RebaseScale(); DraftChanged();
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
        if (_scaleLinkError.Length != 0) { valid = false; _scaleErrorField?.SetError(_scaleLinkError); }
        SetProperty(ref _validationText, string.Join("\n", _components.SelectMany(component => component.Properties
            .Where(field => field.HasError).Select(field => component.Title + " · " + field.Label + ": " + field.Error))), nameof(DiagnosticsText));
        SetProperty(ref _canEdit, _available && _component != default, nameof(CanEdit));
        SetProperty(ref _canLinkScale, CanEdit && !_conflict, nameof(CanLinkScale));
        SetProperty(ref _canEditProperties, _available && _entity != default, nameof(CanEditProperties));
        SetProperty(ref _canChangeComponents, CanEditProperties && !draft && !_conflict, nameof(CanChangeComponents));
        foreach (var field in _active) field.Enable(CanEditProperties);
        SetProperty(ref _canApply, CanEditProperties && draft && valid && !_conflict, nameof(CanApply));
        SetProperty(ref _canCancel, draft || _conflict, nameof(CanCancel));
        SetProperty(ref _canUndo, _available && !draft && !_conflict && content?.UndoCount > 0, nameof(CanUndo));
        SetProperty(ref _canRedo, _available && !draft && !_conflict && content?.RedoCount > 0, nameof(CanRedo));
        var preview = _preview.Read();
        SetProperty(ref _previewState, preview, nameof(PreviewState));
        SetProperty(ref _canPreview, _available && content != null && !preview.Busy, nameof(CanPreview));
        SetProperty(ref _canStopPreview, preview.Active || preview.Busy || preview.Error != null, nameof(CanStopPreview));
        var shown = preview.Presented == null ? "нет кадра" : $"кадр r{preview.Presented.Revision}";
        var text = preview.Error != null ? "Preview: " + preview.Error.Message :
            preview.Active ? $"Preview: {shown} / документ r{content?.Document.Revision} {(preview.Busy ? "· обновление…" : "")}" : "Preview выключен · до 64 объектов";
        SetProperty(ref _previewText, text, nameof(PreviewText));
        if (_conflict) SetProperty(ref _error, "Сцена изменилась. Отмените черновик и повторите правку.", nameof(ErrorText));
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
        if (!ReferenceEquals(expected, _baseline)) { SetProperty(ref _error, "Сцена изменилась. Повторите действие.", nameof(ErrorText)); return; }
        if (!CanChangeComponents) return;
        Mutate(() => _commands.EditComponents(_baseline!, new(Operation(), [], [change()])));
    }
    private AuthoringOperation Operation() => new(Guid.NewGuid(), _baseline!.Revision, "editor");
    /// <summary>Discards uncommitted text and reloads the latest selected occurrence.</summary>
    public void Cancel() { VerifyOwner(); Load(_commands.Read().Document?.Content); Refresh(_selection); OnPropertyChanged(nameof(HasDraft)); }
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
            else SetProperty(ref _error, result.Error + ": " + result.Message, nameof(ErrorText));
        }
        catch (Exception error) { SetProperty(ref _error, error.Message, nameof(ErrorText)); }
        var failure = _error; Refresh(_selection); if (failure.Length != 0) SetProperty(ref _error, failure, nameof(ErrorText));
        OnPropertyChanged(nameof(HasDraft));
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
    /// <inheritdoc />
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(ErrorText))
        { base.OnPropertyChanged(nameof(HasError)); base.OnPropertyChanged(nameof(DiagnosticsText)); base.OnPropertyChanged(nameof(HasDiagnostics)); }
        if (propertyName == nameof(DiagnosticsText)) base.OnPropertyChanged(nameof(HasDiagnostics));
    }
    /// <summary>Disconnects field callbacks; borrowed document and preview owners remain alive.</summary>
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _numericGesture = null;
        foreach (var field in Fields.Cast<InspectorProperty>().Concat(_properties.Values).Distinct()) field.Changed -= DraftChanged;
        ClearPropertyChangedSubscribers();
    }
}
