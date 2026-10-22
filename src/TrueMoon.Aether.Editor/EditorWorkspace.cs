using System.Globalization;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor;

/// <summary>UI-owned scene selection and immutable runtime presentation; mutations still go through Aether commands.</summary>
public sealed class EditorWorkspace
{
    private readonly SceneEntity[] _entities;
    private string? _selected;
    public EditorWorkspace(SceneSnapshot scene)
    {
        ArgumentNullException.ThrowIfNull(scene); Scene = scene; _entities = scene.CopyEntities().ToArray(); _selected = _entities.FirstOrDefault()?.Id;
    }
    public SceneSnapshot Scene { get; }
    public int EntityCount => _entities.Length;
    public IReadOnlyList<string> EntityIds => Array.AsReadOnly(_entities.Select(x => x.Id).ToArray());
    public string? SelectedId => _selected;
    public SessionState State { get; private set; } = SessionState.Stopped;
    public GameEditorOperation Operation { get; private set; }
    public bool HasCandidate { get; private set; }
    public bool Closed { get; private set; }
    public string? Error { get; private set; }
    public ulong Tick { get; private set; }
    public int? ProcessId { get; private set; }
    public event Action? SelectionChanged;
    public void Select(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (!_entities.Any(x => x.Id == id)) throw new ArgumentException("Entity is not in this scene.", nameof(id));
        if (_selected == id) return; _selected = id; SelectionChanged?.Invoke();
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
        if (Closed) return false;
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
        var entity = _entities.FirstOrDefault(x => x.Id == _selected); if (entity == null) return [];
        static string N(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);
        var rows = new List<KeyValuePair<string, string>> { new("ID", entity.Id), new("X", N(entity.X)), new("Y", N(entity.Y)), new("Z", N(entity.Z)),
            new("Интерактивен", entity.Interactive ? "Да" : "Нет"), new("Геометрия", entity.Primitive == null ? "Группа" : "Cube") };
        if (entity.Primitive is { } p) { rows.Add(new("Красный", N(p.Red))); rows.Add(new("Зелёный", N(p.Green))); rows.Add(new("Синий", N(p.Blue))); }
        return rows.AsReadOnly();
    }
}
