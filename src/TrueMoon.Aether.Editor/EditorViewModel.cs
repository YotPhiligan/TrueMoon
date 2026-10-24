using System.ComponentModel;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor;

/// <summary>Observable editor state and guarded actions, independent of controls and themes.</summary>
public sealed class EditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly EditorWorkspace _workspace;
    private readonly EditorCommands _commands;
    private readonly EditorDocumentViewModel? _documents;
    private readonly EditorTransformViewModel? _transform;
    private bool _disposed;
    private string? _selectedId;
    private string _selectedName = "", _statusText = "", _errorText = "";
    private bool _canBuild, _canStart, _canPlay, _canPause, _canStep, _canStop, _canReplace, _canCancel;
    private IReadOnlyList<KeyValuePair<string, string>> _properties = [];
    private SessionState _runtimeState;
    private GameEditorOperation _operation;
    private ulong _tick;
    private int? _processId;

    /// <summary>Creates a model borrowing App-owned workspace and command services.</summary>
    public EditorViewModel(EditorWorkspace workspace, EditorCommands commands, EditorDocumentViewModel? documents = null,
        EditorTransformViewModel? transform = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(commands);
        _workspace = workspace;
        _commands = commands;
        _documents = documents;
        _transform = transform;
        _workspace.SelectionChanged += SelectionChanged;
        _workspace.SceneChanged += SceneChanged;
        SelectionChanged();
        RefreshState();
    }

    public SceneSnapshot Scene => _workspace.Scene;
    public IReadOnlyList<string> EntityIds => _workspace.EntityIds;
    public int EntityCount => _workspace.EntityCount;
    public string? SelectedId => _selectedId;
    public string SelectedName => _selectedName;
    public string StatusText => _statusText;
    public string ErrorText => _errorText;
    /// <summary>Typed runtime state for presentation without parsing the legacy status string.</summary>
    public SessionState RuntimeState => _runtimeState;
    /// <summary>Current tracked game operation.</summary>
    public GameEditorOperation Operation => _operation;
    /// <summary>Latest runtime tick, used by details.</summary>
    public ulong Tick => _tick;
    /// <summary>Runtime child process identity, when running.</summary>
    public int? ProcessId => _processId;
    /// <summary>Whether an explicit project/game is selected.</summary>
    public bool HasProject => _workspace.HasProject;
    public IReadOnlyList<KeyValuePair<string, string>> Properties => _properties;
    public bool CanBuild => _canBuild;
    public bool CanStart => _canStart;
    public bool CanPlay => _canPlay;
    /// <summary>Gets whether the title-bar Run action can start or resume simulation.</summary>
    public bool CanRun => CanStart || CanPlay;
    public bool CanPause => _canPause;
    public bool CanStep => _canStep;
    public bool CanStop => _canStop;
    public bool CanReplace => _canReplace;
    public bool CanCancel => _canCancel;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Select(string id)
    {
        VerifyOpen();
        _workspace.Select(id);
        _transform?.Refresh(_workspace.SelectedId);
    }

    public void Execute(EditorCommand command)
    {
        VerifyOpen();
        if (_workspace.CanExecute(command) && _documents?.BlocksRuntime != true) _commands.Execute(command);
    }
    /// <summary>Starts and plays a prepared runtime, or resumes it, with fresh admission checks.</summary>
    public void RunSimulation()
    {
        VerifyOpen();
        if (_documents?.BlocksRuntime == true) return;
        var start = _workspace.CanExecute(EditorCommand.Start);
        if (start || _workspace.CanExecute(EditorCommand.Play)) _commands.RunSimulation(start);
    }

    /// <summary>Accepts snapshots on the presentation owner thread and publishes changed values.</summary>
    public void Refresh(SessionStatus runtime, GameEditorStatus editor, Exception? commandError = null)
    {
        VerifyOpen();
        _documents?.Refresh();
        if (_documents != null) _workspace.SetDocument(_documents.Document?.Content);
        _transform?.Refresh(_workspace.SelectedId);
        _workspace.Refresh(runtime, editor, commandError);
        RefreshState();
    }

    /// <summary>Closes command admission and drains accepted asynchronous work.</summary>
    public Task StopAsync()
    {
        VerifyOpen();
        return _commands.StopAsync();
    }

    private void RefreshState()
    {
        Update(ref _runtimeState, _workspace.State, nameof(RuntimeState));
        Update(ref _operation, _workspace.Operation, nameof(Operation));
        Update(ref _tick, _workspace.Tick, nameof(Tick));
        Update(ref _processId, _workspace.ProcessId, nameof(ProcessId));
        var allowed = _documents?.BlocksRuntime != true;
        var couldRun = CanRun;
        Update(ref _canBuild, allowed && _workspace.CanExecute(EditorCommand.Build), nameof(CanBuild));
        Update(ref _canStart, allowed && _workspace.CanExecute(EditorCommand.Start), nameof(CanStart));
        Update(ref _canPlay, allowed && _workspace.CanExecute(EditorCommand.Play), nameof(CanPlay));
        if (couldRun != CanRun) PropertyChanged?.Invoke(this, new(nameof(CanRun)));
        Update(ref _canPause, allowed && _workspace.CanExecute(EditorCommand.Pause), nameof(CanPause));
        Update(ref _canStep, allowed && _workspace.CanExecute(EditorCommand.Step), nameof(CanStep));
        Update(ref _canStop, allowed && _workspace.CanExecute(EditorCommand.Stop), nameof(CanStop));
        Update(ref _canReplace, allowed && _workspace.CanExecute(EditorCommand.Replace), nameof(CanReplace));
        Update(ref _canCancel, allowed && _workspace.CanExecute(EditorCommand.Cancel), nameof(CanCancel));
        var state = _workspace.State switch
        {
            SessionState.Stopped => "Остановлен", SessionState.Starting => "Запуск", SessionState.Paused => "Пауза",
            SessionState.Playing => "Симуляция", SessionState.Stopping => "Остановка", _ => "Ошибка runtime"
        };
        var operation = _workspace.Operation switch
        {
            GameEditorOperation.Idle => "Готово", GameEditorOperation.Build => "Сборка",
            GameEditorOperation.Replace => "Замена сборки", _ => "Выполняется команда"
        };
        Update(ref _statusText,
            $"  {state}   ·   {operation}   ·   Tick {_workspace.Tick}   ·   Runtime PID {_workspace.ProcessId?.ToString() ?? "—"}   ·   {_workspace.EntityCount} объектов",
            nameof(StatusText));
        Update(ref _errorText, _workspace.Error == null ? "" : "Ошибка: " + _workspace.Error, nameof(ErrorText));
    }

    private void SelectionChanged()
    {
        if (_disposed) return;
        Update(ref _selectedId, _workspace.SelectedId, nameof(SelectedId));
        Update(ref _selectedName,
            _workspace.SelectedId == null ? "Нет объектов" : EditorScene.Name(_workspace.SelectedId),
            nameof(SelectedName));
        _properties = _workspace.Properties();
        PropertyChanged?.Invoke(this, new(nameof(Properties)));
    }

    private void SceneChanged()
    {
        if (_disposed) return;
        PropertyChanged?.Invoke(this, new(nameof(Scene)));
        PropertyChanged?.Invoke(this, new(nameof(EntityIds)));
        PropertyChanged?.Invoke(this, new(nameof(EntityCount)));
    }

    private void Update<T>(ref T field, T value, string property)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new(property));
    }

    private void VerifyOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Disconnects subscriptions; borrowed services remain owned by App.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.SelectionChanged -= SelectionChanged;
        _workspace.SceneChanged -= SceneChanged;
        PropertyChanged = null;
    }
}
