using System.Globalization;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Editor.Hierarchy;

namespace TrueMoon.Aether.Editor;

/// <summary>UI-owned scene selection and immutable runtime presentation; mutations still go through Aether commands.</summary>
public sealed class EditorWorkspace
{
    private readonly SceneEntity[] _entities;
    private readonly SceneSnapshot _initialScene;
    private SceneAuthoringState? _document;
    private string? _selected;
    private IReadOnlyList<HierarchyItem> _hierarchy = [];
    private IReadOnlyList<string> _entityIds = [];
    public EditorWorkspace(SceneSnapshot scene, bool hasProject = true)
    {
        ArgumentNullException.ThrowIfNull(scene); Scene = _initialScene = scene; HasProject = hasProject; _entities = scene.CopyEntities().ToArray();
        UpdateHierarchy(); _selected = _entityIds.FirstOrDefault();
    }
    public SceneSnapshot Scene { get; private set; }
    /// <summary>Gets whether build/game actions have an explicitly selected project.</summary>
    public bool HasProject { get; }
    public int EntityCount => _document?.Document.Entities.Length ?? _entities.Length;
    public IReadOnlyList<string> EntityIds => _entityIds;
    /// <summary>Gets stable immutable parent/name/role data for the current scene revision.</summary>
    public IReadOnlyList<HierarchyItem> HierarchyItems => _hierarchy;
    public string? SelectedId => _selected;
    public SessionState State { get; private set; } = SessionState.Stopped;
    public GameEditorOperation Operation { get; private set; }
    public bool HasCandidate { get; private set; }
    public bool Closed { get; private set; }
    public string? Error { get; private set; }
    public ulong Tick { get; private set; }
    public int? ProcessId { get; private set; }
    public event Action? SelectionChanged;
    /// <summary>Raised on the UI owner when the authoring document identity/content changes.</summary>
    public event Action? SceneChanged;
    /// <summary>Displays immutable authoring content without projecting into the legacy 64-entity runtime format.</summary>
    /// <param name="document">Current project-owned state, or null to return to the original workspace.</param>
    public void SetDocument(SceneAuthoringState? document)
    {
        if (ReferenceEquals(_document?.Document, document?.Document)) return;
        var previousScene = Scene.Id;
        _document = document;
        Scene = document == null ? _initialScene : new(document.Document.Id.Value.ToString("D"), document.Document.Revision, []);
        UpdateHierarchy();
        if (previousScene != Scene.Id || _selected != null && !EntityIds.Contains(_selected)) _selected = EntityIds.FirstOrDefault();
        SceneChanged?.Invoke(); SelectionChanged?.Invoke();
    }
    private void UpdateHierarchy()
    {
        _hierarchy = Array.AsReadOnly(_document == null ? _entities.Select(entity => new HierarchyItem(entity.Id, null, EditorScene.Name(entity.Id),
            entity.Interactive ? HierarchyItemKind.Interactive : entity.Primitive != null ? HierarchyItemKind.Renderable : HierarchyItemKind.Entity)).ToArray() :
            _document.Resolved.HierarchyOrder.Select(id =>
            {
                var entity = _document.Resolved.Entities[id]; var key = id.Value.ToString("D");
                var kind = entity.Components.Values.Any(c => c.TypeId == BuiltInComponents.InteractionId) ? HierarchyItemKind.Interactive :
                    entity.Components.Values.Any(c => c.TypeId == BuiltInComponents.AppearanceId) ? HierarchyItemKind.Renderable : HierarchyItemKind.Entity;
                return new HierarchyItem(key, entity.ParentId?.Value.ToString("D"), entity.Name ?? EditorScene.Name(key), kind);
            }).ToArray());
        _entityIds = Array.AsReadOnly(_hierarchy.Select(item => item.Id).ToArray());
    }
    public void Select(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!EntityIds.Contains(id)) throw new ArgumentException("Entity is not in this scene.", nameof(id));
        if (_selected == id) return; _selected = id; SelectionChanged?.Invoke();
    }
    /// <summary>Clears inspection while retaining the scene and all entities.</summary>
    public void ClearSelection()
    {
        if (_selected == null) return;
        _selected = null; SelectionChanged?.Invoke();
    }
    public void Refresh(SessionStatus runtime, GameEditorStatus editor, Exception? commandError = null)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(editor);
        State = runtime.State; Operation = editor.Operation; HasCandidate = editor.Candidate != null; Closed = editor.Closed;
        Tick = runtime.Tick; ProcessId = runtime.ProcessId;
        Error = Visible(commandError)?.Message ?? Visible(editor.Error)?.Message ?? Visible(runtime.Failure?.Cause)?.Message;
        static Exception? Visible(Exception? error) => error is OperationCanceledException ? null : error;
    }
    public bool CanExecute(EditorCommand command)
    {
        if (!Enum.IsDefined(command)) throw new ArgumentOutOfRangeException(nameof(command));
        if (Closed || !HasProject) return false;
        var idle = Operation == GameEditorOperation.Idle;
        return command switch
        {
            EditorCommand.Stop => State != SessionState.Stopped || !idle,
            EditorCommand.Cancel => !idle && Operation != GameEditorOperation.Stop,
            EditorCommand.Build => idle && State is not (SessionState.Starting or SessionState.Stopping or SessionState.CleanupFailed),
            EditorCommand.Start => idle && HasCandidate && State is SessionState.Stopped or SessionState.Faulted,
            EditorCommand.Play => idle && State == SessionState.Paused,
            EditorCommand.Pause => idle && State == SessionState.Playing,
            EditorCommand.Step => idle && State == SessionState.Paused,
            EditorCommand.Replace => idle && HasCandidate && State is SessionState.Paused or SessionState.Playing,
            _ => false
        };
    }
    public IReadOnlyList<KeyValuePair<string, string>> Properties()
    {
        if (_document != null)
        {
            if (!Guid.TryParse(_selected, out var selected) || !_document.Resolved.Entities.TryGetValue(new(selected), out var item)) return [];
            var values = new List<KeyValuePair<string, string>>
            {
                new("ID", item.Id.Value.ToString("D")), new("Родитель", item.ParentId?.Value.ToString("D") ?? "—"),
                new("Определение", item.DefinitionId.Value.ToString("D"))
            };
            foreach (var component in item.Components.Values.OrderBy(x => x.TypeId.Value).ThenBy(x => x.Id.Value))
            {
                var role = component.TypeId == BuiltInComponents.TransformId ? "Transform" :
                    component.TypeId == BuiltInComponents.AppearanceId ? "Appearance" :
                    component.TypeId == BuiltInComponents.InteractionId ? "Interaction" : component.TypeId.Value.ToString("D");
                values.Add(new(role, component.Id.Value.ToString("D")));
                foreach (var property in component.Properties.OrderBy(x => x.Key, StringComparer.Ordinal))
                    values.Add(new(property.Key, Format(property.Value.Value)));
            }
            return values.AsReadOnly();
        }
        var entity = _entities.FirstOrDefault(x => x.Id == _selected); if (entity == null) return [];
        static string N(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);
        var rows = new List<KeyValuePair<string, string>> { new("ID", entity.Id), new("X", N(entity.X)), new("Y", N(entity.Y)), new("Z", N(entity.Z)),
            new("Интерактивен", entity.Interactive ? "Да" : "Нет"), new("Геометрия", entity.Primitive == null ? "Группа" : "Cube") };
        if (entity.Primitive is { } p) { rows.Add(new("Красный", N(p.Red))); rows.Add(new("Зелёный", N(p.Green))); rows.Add(new("Синий", N(p.Blue))); }
        return rows.AsReadOnly();
    }
    private static string Format(PropertyValue value) => value.Kind switch
    {
        PropertyValueKind.Null => "null", PropertyValueKind.Boolean => value.GetBoolean() ? "true" : "false",
        PropertyValueKind.Integer => value.GetInteger().ToString(CultureInfo.InvariantCulture),
        PropertyValueKind.Number => value.GetNumber().ToString("R", CultureInfo.InvariantCulture),
        _ => "\"" + System.Text.Json.JsonEncodedText.Encode(value.GetString(),
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() + "\""
    };
}
