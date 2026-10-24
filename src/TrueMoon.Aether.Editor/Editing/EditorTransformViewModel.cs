using System.ComponentModel;
using System.Globalization;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Editing;

/// <summary>UI-owner Transform draft; all committed mutations and history belong to the existing document authoring session.</summary>
public sealed class EditorTransformViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EditorDocumentCommands _commands;
    private readonly EditorPreview _preview;
    private SceneDocument? _baseline;
    private EntityId _entity;
    private ComponentId _component;
    private string? _selection;
    private string[] _original = [];
    private int _owner;
    private bool _loading, _disposed, _available, _conflict;
    private bool _canEdit, _canApply, _canCancel, _canUndo, _canRedo, _canPreview, _canStopPreview;
    private string _error = "", _previewText = "Preview выключен · до 64 объектов";
    private EditorPreviewState _previewState = new(false, false, null, null);

    /// <summary>Creates presentation over borrowed App-owned services.</summary>
    /// <param name="commands">Shared document admission and operations.</param><param name="preview">Bounded preview coordinator.</param>
    public EditorTransformViewModel(EditorDocumentCommands commands, EditorPreview preview)
    {
        ArgumentNullException.ThrowIfNull(commands); ArgumentNullException.ThrowIfNull(preview);
        _commands = commands; _preview = preview;
        Fields = new[] { ("x", "X"), ("y", "Y"), ("z", "Z"), ("pitch", "Pitch, рад"), ("yaw", "Yaw, рад"), ("roll", "Roll, рад"), ("sx", "Scale X"), ("sy", "Scale Y"), ("sz", "Scale Z") }
            .Select(x => new TransformField(x.Item1, x.Item2, VerifyOwner)).ToArray();
        foreach (var field in Fields) field.Changed += DraftChanged;
    }
    /// <summary>Gets the nine stable numeric field models.</summary>
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
    public bool HasDraft => _original.Length == Fields.Count && Fields.Where((f, i) => f.Value != _original[i]).Any();
    /// <summary>Gets field-independent mutation/conflict diagnostics.</summary>
    public string ErrorText => _error;
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
    private void Load(SceneAuthoringState? content)
    {
        _baseline = content?.Document; _component = default; _conflict = false; Set(ref _error, "", nameof(ErrorText));
        var values = new string[Fields.Count]; Array.Fill(values, "");
        if (content != null && Guid.TryParse(_selection, out var id) && content.Resolved.Entities.TryGetValue(new(id), out var entity))
        {
            var transform = entity.Components.Values.SingleOrDefault(c => c.TypeId == BuiltInComponents.TransformId);
            if (transform != null)
            {
                _entity = entity.Id; _component = transform.Id;
                for (var i = 0; i < Fields.Count; i++) values[i] = transform.Properties[Fields[i].Key].Value.GetNumber().ToString("R", CultureInfo.InvariantCulture);
            }
        }
        _loading = true;
        try { for (var i = 0; i < Fields.Count; i++) Fields[i].Value = values[i]; _original = values; }
        finally { _loading = false; }
    }
    private void DraftChanged()
    {
        if (_loading) return;
        Set(ref _error, "", nameof(ErrorText)); UpdateState(_commands.Read().Document?.Content); Notify(nameof(HasDraft));
    }
    private bool Parse(out TransformValue transform)
    {
        var values = new double[Fields.Count]; var valid = _component != default;
        for (var i = 0; i < Fields.Count; i++)
        {
            var ok = double.TryParse(Fields[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) && float.IsFinite((float)values[i]);
            if (i >= 6) ok &= (float)values[i] > 0;
            Fields[i].SetError(_component == default || ok ? "" : i >= 6 ? "Масштаб должен быть > 0" : "Введите конечное число с точкой");
            valid &= ok;
        }
        transform = new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]); return valid;
    }
    private void UpdateState(SceneAuthoringState? content)
    {
        var valid = Parse(out _); var draft = HasDraft;
        Set(ref _canEdit, _available && _component != default, nameof(CanEdit));
        Set(ref _canApply, CanEdit && draft && valid && !_conflict, nameof(CanApply));
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
    /// <summary>Atomically commits all nine fields as one engine history transaction.</summary>
    public void Apply()
    {
        VerifyOwner(); Refresh(_selection); if (!CanApply || !Parse(out var value)) return;
        Mutate(() => _commands.Apply(_baseline!, new(new(Guid.NewGuid(), _baseline!.Revision, "editor"), [new(_entity, _component, value)])));
    }
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
    private void Notify(string property) => PropertyChanged?.Invoke(this, new(property));
    private void Set<T>(ref T field, T value, string property) { if (!EqualityComparer<T>.Default.Equals(field, value)) { field = value; Notify(property); } }
    /// <summary>Disconnects field callbacks; borrowed document and preview owners remain alive.</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; foreach (var field in Fields) field.Changed -= DraftChanged; PropertyChanged = null; }
}
