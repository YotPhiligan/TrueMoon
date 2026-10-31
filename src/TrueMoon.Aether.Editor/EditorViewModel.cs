using TrueMoon.Argentis;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor;

/// <summary>Observable editor state and guarded actions, independent of controls and themes.</summary>
public sealed class EditorViewModel : ViewModel, IDisposable
{
    private readonly EditorWorkspace _workspace;
    private readonly EditorCommands _commands;
    private readonly EditorDocumentViewModel? _documents;
    private readonly EditorInspectorViewModel? _transform;
    private readonly EditorHierarchyViewModel? _hierarchy;
    private readonly EditorScenePlayback? _playback;
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
        EditorInspectorViewModel? transform = null, EditorHierarchyViewModel? hierarchy = null)
        : this(workspace, commands, documents, transform, hierarchy, null) { }

    /// <summary>Creates presentation with optional shared-admission scene playback; all injected services are borrowed.</summary>
    public EditorViewModel(EditorWorkspace workspace, EditorCommands commands, EditorDocumentViewModel? documents,
        EditorInspectorViewModel? transform, EditorHierarchyViewModel? hierarchy, EditorScenePlayback? playback)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(commands);
        _workspace = workspace;
        _commands = commands;
        _documents = documents;
        _transform = transform;
        _hierarchy = hierarchy;
        _playback = playback;
        _workspace.SelectionChanged += SelectionChanged;
        _workspace.SceneChanged += SceneChanged;
        SelectionChanged();
        RefreshState();
    }

    public SceneSnapshot Scene => _workspace.Scene;
    public IReadOnlyList<string> EntityIds => _workspace.EntityIds;
    public int EntityCount => _workspace.EntityCount;
    public string? SelectedId => _selectedId;
    /// <summary>Shows entity properties only while there is a selected entity context.</summary>
    public bool IsInspectorVisible => _selectedId != null;
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

    public void Select(string id)
    {
        VerifyOpen();
        _workspace.Select(id);
        _transform?.Refresh(_workspace.SelectedId);
        _hierarchy?.Refresh();
    }

    /// <summary>Ends entity inspection and discards the uncommitted selection draft.</summary>
    public void ClearSelection()
    {
        VerifyOpen();
        _workspace.ClearSelection();
        _transform?.Refresh(null);
        _hierarchy?.Refresh();
    }

    public void Execute(EditorCommand command)
    {
        VerifyOpen();
        if (!CanExecute(command)) return;
        if (UsesScenePlayback) _playback!.Execute(command); else _commands.Execute(command);
    }
    /// <summary>Starts and plays a prepared runtime, or resumes it, with fresh admission checks.</summary>
    public void RunSimulation()
    {
        VerifyOpen();
        if (UsesScenePlayback)
        {
            if (CanExecute(EditorCommand.Start) || CanExecute(EditorCommand.Play)) _playback!.RunSimulation();
            return;
        }
        if (!RuntimeAllowed) return;
        var start = _workspace.CanExecute(EditorCommand.Start);
        if (start || _workspace.CanExecute(EditorCommand.Play)) _commands.RunSimulation(start);
    }

    /// <summary>Accepts snapshots on the presentation owner thread and publishes changed values.</summary>
    public void Refresh(SessionStatus runtime, GameEditorStatus editor, Exception? commandError = null)
        => Refresh(() => runtime, () => editor, commandError);

    /// <summary>Reads runtime status after the project/document snapshot, preserving publication order during project switches.</summary>
    /// <param name="runtime">Fresh borrowed runtime observation.</param><param name="editor">Fresh borrowed build/context observation.</param>
    /// <param name="commandError">Optional command failure.</param>
    public void Refresh(Func<SessionStatus> runtime, Func<GameEditorStatus> editor, Exception? commandError = null)
    {
        VerifyOpen();
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(editor);
        _documents?.Refresh();
        if (_documents?.ProjectSwitchEnabled == true) _workspace.SetProject(_documents.Project);
        if (_documents != null) _workspace.SetDocument(_documents.Document?.Content);
        _transform?.Refresh(_workspace.SelectedId);
        _workspace.Refresh(runtime(), editor(), UsesScenePlayback ? _playback!.Error ?? commandError : commandError);
        RefreshState();
        _hierarchy?.Refresh();
    }

    /// <summary>Closes command admission and drains accepted asynchronous work.</summary>
    public Task StopAsync()
    {
        VerifyOpen();
        return _commands.StopAsync();
    }

    private void RefreshState()
    {
        SetProperty(ref _runtimeState, _workspace.State, nameof(RuntimeState));
        SetProperty(ref _operation, _workspace.Operation == GameEditorOperation.Idle && UsesScenePlayback ?
            _documents?.RuntimeOperation ?? GameEditorOperation.Idle : _workspace.Operation, nameof(Operation));
        SetProperty(ref _tick, _workspace.Tick, nameof(Tick));
        SetProperty(ref _processId, _workspace.ProcessId, nameof(ProcessId));
        var couldRun = CanRun;
        SetProperty(ref _canBuild, CanExecute(EditorCommand.Build), nameof(CanBuild));
        SetProperty(ref _canStart, CanExecute(EditorCommand.Start), nameof(CanStart));
        SetProperty(ref _canPlay, CanExecute(EditorCommand.Play), nameof(CanPlay));
        if (couldRun != CanRun) OnPropertyChanged(nameof(CanRun));
        SetProperty(ref _canPause, CanExecute(EditorCommand.Pause), nameof(CanPause));
        SetProperty(ref _canStep, CanExecute(EditorCommand.Step), nameof(CanStep));
        SetProperty(ref _canStop, CanExecute(EditorCommand.Stop), nameof(CanStop));
        SetProperty(ref _canReplace, CanExecute(EditorCommand.Replace), nameof(CanReplace));
        SetProperty(ref _canCancel, CanExecute(EditorCommand.Cancel), nameof(CanCancel));
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
        SetProperty(ref _statusText,
            $"  {state}   ·   {operation}   ·   Tick {_workspace.Tick}   ·   Runtime PID {_workspace.ProcessId?.ToString() ?? "—"}   ·   {_workspace.EntityCount} объектов",
            nameof(StatusText));
        SetProperty(ref _errorText, _workspace.Error == null ? "" : "Ошибка: " + _workspace.Error, nameof(ErrorText));
    }

    private void SelectionChanged()
    {
        if (_disposed) return;
        var visible = IsInspectorVisible;
        SetProperty(ref _selectedId, _workspace.SelectedId, nameof(SelectedId));
        if (visible != IsInspectorVisible) OnPropertyChanged(nameof(IsInspectorVisible));
        SetProperty(ref _selectedName,
            _workspace.SelectedId == null ? "Нет объектов" : EditorScene.Name(_workspace.SelectedId),
            nameof(SelectedName));
        _properties = _workspace.Properties();
        OnPropertyChanged(nameof(Properties));
    }

    private void SceneChanged()
    {
        if (_disposed) return;
        OnPropertyChanged(nameof(Scene));
        OnPropertyChanged(nameof(EntityIds));
        OnPropertyChanged(nameof(EntityCount));
        OnPropertyChanged(nameof(HasProject));
    }

    private bool RuntimeAllowed => _documents?.BlocksRuntime != true &&
        (_documents?.ProjectSwitchEnabled != true || ReferenceEquals(_workspace.Project, _documents.Project));

    private bool UsesScenePlayback => _playback != null &&
        (_documents?.Document?.Content != null || _playback.OwnsRuntime || !_playback.Pending.IsCompleted);

    private bool CanExecute(EditorCommand command)
    {
        if (!UsesScenePlayback) return RuntimeAllowed && _workspace.CanExecute(command);
        if (command is EditorCommand.Stop or EditorCommand.Cancel) return _playback!.CanExecute(command);
        return _documents?.IsPathPromptOpen != true && _transform?.CanCancel != true &&
            _playback!.Matches(_workspace.Project, _documents?.Document?.Content?.Document) && _playback.CanExecute(command);
    }


    private void VerifyOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>Disconnects subscriptions; borrowed services remain owned by App.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _workspace.SelectionChanged -= SelectionChanged;
        _workspace.SceneChanged -= SceneChanged;
        ClearPropertyChangedSubscribers();
    }
}
