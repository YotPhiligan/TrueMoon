using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Owner-thread presentation of document commands, paths, decisions and diagnostics; no controls or native handles.</summary>
public sealed class EditorDocumentViewModel : ViewModel, IDisposable
{
    private readonly EditorDocumentCommands _commands;
    private int _owner;
    private bool _disposed;
    private string _path = "Main.aetherscene", _savePath = "Untitled.aetherscene", _projectPath = "";
    private EditorDocumentCommandState? _state;
    private string _title = "Сцена не открыта", _root = "Проект не открыт", _error = "", _decision = "";
    private bool _canNew, _canOpen, _canSave, _canSaveAs, _canClose, _canCancel, _awaiting, _needsSavePath;
    private DocumentCommand? _pathRequest;
    private string? _dismissedError;
    private bool _canConfirmPath;
    private bool _canOpenProject, _canCloseProject;

    /// <summary>Creates a model borrowing the App-owned coordinator. The first owner operation establishes the UI thread.</summary>
    /// <param name="commands">Tracked document actions.</param>
    public EditorDocumentViewModel(EditorDocumentCommands commands)
    { ArgumentNullException.ThrowIfNull(commands); _commands = commands; }

    /// <summary>Gets/sets the input used by Open, SaveAs and first Save. Completion never replaces this draft.</summary>
    public string PathDraft { get => _path; set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); if (_path == value) return; SetProperty(ref _path, value, nameof(PathDraft)); if (_pathRequest != DocumentCommand.OpenProject) OnPropertyChanged(nameof(RequestedPath)); } }
    /// <summary>Retained project manifest draft, separate from scene and first-save paths.</summary>
    public string ProjectPathDraft { get => _projectPath; set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); if (_projectPath == value) return; SetProperty(ref _projectPath, value, nameof(ProjectPathDraft)); if (_pathRequest == DocumentCommand.OpenProject) OnPropertyChanged(nameof(RequestedPath)); } }
    /// <summary>Path edited by the shared retained prompt.</summary>
    public string RequestedPath { get => _pathRequest == DocumentCommand.OpenProject ? ProjectPathDraft : PathDraft; set { if (_pathRequest == DocumentCommand.OpenProject) ProjectPathDraft = value; else PathDraft = value; } }
    /// <summary>Accessible path input label.</summary>
    public string PathInputLabel => _pathRequest == DocumentCommand.OpenProject ? "Путь проекта" : "Путь сцены";
    /// <summary>Prompt hint without changing the canonical scene-root contract.</summary>
    public string PathHint => _pathRequest == DocumentCommand.OpenProject ? "Полный путь к файлу .aetherproj" : ScenesRoot;
    /// <summary>Actual active project snapshot; owned by the project coordinator.</summary>
    public Projects.ProjectSession? Project => _state?.Project;
    /// <summary>Whether this host supplies dynamic project/runtime rebinding.</summary>
    public bool ProjectSwitchEnabled => _state?.ProjectSwitchEnabled == true;
    /// <summary>Current portable project name for the window caption.</summary>
    public string ProjectName => ProjectSwitchEnabled ? Project?.Project.Description.Name ?? "Проект не открыт" : "";
    /// <summary>Open/switch project admission.</summary>
    public bool CanOpenProject => _canOpenProject;
    /// <summary>Close/retry project cleanup admission.</summary>
    public bool CanCloseProject => _canCloseProject;
    /// <summary>Gets/sets a separate untitled-save path while deciding a transition.</summary>
    public string SavePathDraft { get => _savePath; set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); SetProperty(ref _savePath, value, nameof(SavePathDraft)); } }
    /// <summary>Gets the latest document snapshot copied by Refresh.</summary>
    public SceneDocumentState? Document => _state?.Document;
    /// <summary>Gets file identity, dirty marker and current activity.</summary>
    public string Title => _title;
    /// <summary>Gets the canonical scene root used to interpret relative input.</summary>
    public string ScenesRoot => _root;
    /// <summary>Gets an actionable error without dropping path drafts or document content.</summary>
    public string ErrorText => _error;
    /// <summary>Action awaiting an explicit Open/Save path confirmation; no IO has started.</summary>
    public DocumentCommand? PathRequest => _pathRequest;
    /// <summary>Whether the retained path editor is requested.</summary>
    public bool IsPathPromptOpen => _pathRequest != null;
    /// <summary>Whether contextual document tools need to be shown above the scene.</summary>
    public bool IsToolsVisible => IsPathPromptOpen || AwaitingDecision || (_error.Length != 0 && _dismissedError != _error);
    /// <summary>Heading of the requested path operation.</summary>
    public string PathPromptText => _pathRequest is DocumentCommand.Open ? "Открыть сцену" :
        _pathRequest == DocumentCommand.OpenProject ? "Открыть проект (.aetherproj)" :
        _pathRequest != null ? "Сохранить сцену как" : AwaitingDecision ? "Несохранённые изменения" : _error.Length != 0 ? "Ошибка операции" : Title;
    /// <summary>Fresh admission for the requested path action.</summary>
    public bool CanConfirmPath => _canConfirmPath;
    /// <summary>Whether the tools can be dismissed without answering an unsaved-changes decision.</summary>
    public bool CanDismissTools => !AwaitingDecision && !IsPathPromptOpen;
    /// <summary>Gets the explanation of the pending transition.</summary>
    public string DecisionText => _decision;
    /// <summary>Gets whether an explicit Save/Discard/Cancel decision is pending.</summary>
    public bool AwaitingDecision => _awaiting;
    /// <summary>Gets whether the pending decision needs a first-save path.</summary>
    public bool NeedsSavePath => _needsSavePath;
    /// <summary>Gets whether the path draft can be edited.</summary>
    public bool CanEditPath => _pathRequest == DocumentCommand.OpenProject ? CanOpenProject : CanOpen;
    /// <summary>Gets New availability.</summary>
    public bool CanNew => _canNew;
    /// <summary>Gets Open availability.</summary>
    public bool CanOpen => _canOpen;
    /// <summary>Gets Save availability.</summary>
    public bool CanSave => _canSave;
    /// <summary>Gets SaveAs availability.</summary>
    public bool CanSaveAs => _canSaveAs;
    /// <summary>Gets Close availability.</summary>
    public bool CanClose => _canClose;
    /// <summary>Gets whether cancellable work is pending.</summary>
    public bool CanCancel => _canCancel;
    /// <summary>Whether a document operation or close decision is pending.</summary>
    public bool IsBusy => _state?.Busy == true;
    /// <summary>Current document action, if any.</summary>
    public DocumentCommand? Operation => _state?.Operation;
    /// <summary>Scene game activity sharing document admission.</summary>
    public TrueMoon.Aether.Editing.GameEditorOperation RuntimeOperation => _state?.RuntimeOperation ?? TrueMoon.Aether.Editing.GameEditorOperation.Idle;
    /// <summary>Reads fresh admission state before a game action, including work accepted since the last UI refresh.</summary>
    public bool BlocksRuntime
    {
        get { var state = _commands.Read(); return IsPathPromptOpen || state.Busy || state.Exiting || state.Closed || !state.ProjectReady ||
            state.ProjectSwitchEnabled && !ReferenceEquals(_state?.Project, state.Project) || state.Document?.Content != null; }
    }

    /// <summary>Copies a thread-safe coordinator snapshot and notifies bindings on the UI owner thread.</summary>
    public void Refresh()
    {
        VerifyOwner();
        var toolsVisible = IsToolsVisible; var confirm = CanConfirmPath; var dismiss = CanDismissTools; var promptText = PathPromptText;
        var next = _commands.Read(); var previous = _state; _state = next;
        if (!ReferenceEquals(previous?.Project, next.Project)) { OnPropertyChanged(nameof(Project)); OnPropertyChanged(nameof(ProjectName)); }
        if (previous?.ProjectSwitchEnabled != next.ProjectSwitchEnabled) { OnPropertyChanged(nameof(ProjectSwitchEnabled)); OnPropertyChanged(nameof(ProjectName)); }
        if (previous?.Busy != next.Busy) OnPropertyChanged(nameof(IsBusy));
        if (previous?.Operation != next.Operation) OnPropertyChanged(nameof(Operation));
        if (previous?.RuntimeOperation != next.RuntimeOperation) OnPropertyChanged(nameof(RuntimeOperation));
        if (previous?.Document != next.Document) OnPropertyChanged(nameof(Document));
        SetProperty(ref _canNew, _commands.CanExecute(DocumentCommand.New), nameof(CanNew));
        var wasEditable = CanEditPath;
        SetProperty(ref _canOpenProject, _commands.CanExecute(DocumentCommand.OpenProject), nameof(CanOpenProject));
        SetProperty(ref _canCloseProject, _commands.CanExecute(DocumentCommand.CloseProject), nameof(CanCloseProject));
        SetProperty(ref _canOpen, _commands.CanExecute(DocumentCommand.Open), nameof(CanOpen));
        if (wasEditable != CanEditPath) OnPropertyChanged(nameof(CanEditPath));
        SetProperty(ref _canSave, _commands.CanExecute(DocumentCommand.Save), nameof(CanSave));
        SetProperty(ref _canSaveAs, _commands.CanExecute(DocumentCommand.SaveAs), nameof(CanSaveAs));
        SetProperty(ref _canClose, _commands.CanExecute(DocumentCommand.Close), nameof(CanClose));
        SetProperty(ref _canCancel, next.Busy && !next.Exiting, nameof(CanCancel));
        SetProperty(ref _awaiting, next.AwaitingDecision, nameof(AwaitingDecision));
        SetProperty(ref _needsSavePath, next.AwaitingDecision && next.Document?.Path == null, nameof(NeedsSavePath));
        var file = next.Document?.Content == null ? "Сцена не открыта" :
            next.Document.Path == null ? "Новая сцена" : Path.GetFileName(next.Document.Path);
        var activity = next.Busy ? next.AwaitingDecision ? " · Выберите действие" :
            next.Operation is DocumentCommand.Save or DocumentCommand.SaveAs ? " · Сохранение…" :
            next.Operation == DocumentCommand.Open ? " · Загрузка…" : " · Выполняется…" : "";
        SetProperty(ref _title, file + (next.Document?.IsDirty == true ? " *" : "") + activity, nameof(Title));
        var previousRoot = _root;
        SetProperty(ref _root, next.ScenesDirectory ?? "Проект не открыт", nameof(ScenesRoot));
        if (previousRoot != _root) OnPropertyChanged(nameof(PathHint));
        if (next.Error == null) _dismissedError = null;
        SetProperty(ref _error, next.Error == null ? "" : "Ошибка: " + next.Error.Message, nameof(ErrorText));
        var target = next.Operation switch { DocumentCommand.OpenProject => "открытием другого проекта", DocumentCommand.CloseProject => "закрытием проекта", DocumentCommand.Open => "открытием другой сцены", DocumentCommand.New => "созданием сцены", DocumentCommand.Exit => "выходом", _ => "закрытием сцены" };
        SetProperty(ref _decision, "Сохранить изменения перед " + target + "?", nameof(DecisionText));
        if (toolsVisible != IsToolsVisible) OnPropertyChanged(nameof(IsToolsVisible));
        _canConfirmPath = _pathRequest is { } pathCommand && _commands.CanExecute(pathCommand);
        if (confirm != CanConfirmPath) OnPropertyChanged(nameof(CanConfirmPath));
        if (dismiss != CanDismissTools) OnPropertyChanged(nameof(CanDismissTools));
        if (promptText != PathPromptText) OnPropertyChanged(nameof(PathPromptText));
    }

    /// <summary>Requests a UI action, collecting paths before Open, SaveAs or a first Save.</summary>
    /// <param name="command">New/Open/Save/SaveAs/Close action.</param>
    public void Request(DocumentCommand command)
    {
        VerifyOwner();
        if (command is not (DocumentCommand.New or DocumentCommand.Open or DocumentCommand.Save or DocumentCommand.SaveAs or DocumentCommand.Close or DocumentCommand.OpenProject or DocumentCommand.CloseProject))
            throw new ArgumentOutOfRangeException(nameof(command));
        if (!_commands.CanExecute(command)) return;
        if (command is DocumentCommand.Open or DocumentCommand.OpenProject or DocumentCommand.SaveAs || command == DocumentCommand.Save && Document?.Path == null)
        {
            _dismissedError = null; SetPathRequest(command); return;
        }
        Execute(command);
    }
    /// <summary>Starts the requested operation using the retained path draft and fresh admission.</summary>
    public void ConfirmPath()
    {
        VerifyOwner();
        if (_pathRequest is not { } command || !_commands.CanExecute(command)) return;
        SetPathRequest(null); Execute(command);
    }
    /// <summary>Dismisses path tools or a displayed error without changing the document or its history.</summary>
    public void CancelPath()
    {
        VerifyOwner(); _dismissedError = _error; SetPathRequest(null);
    }
    private void SetPathRequest(DocumentCommand? command)
    {
        var previous = _pathRequest; _pathRequest = command;
        _canConfirmPath = command is { } requested && _commands.CanExecute(requested);
        if (previous != command)
        {
            OnPropertyChanged(nameof(PathRequest)); OnPropertyChanged(nameof(IsPathPromptOpen)); OnPropertyChanged(nameof(PathPromptText)); OnPropertyChanged(nameof(CanConfirmPath));
            OnPropertyChanged(nameof(CanDismissTools));
            OnPropertyChanged(nameof(RequestedPath)); OnPropertyChanged(nameof(PathInputLabel)); OnPropertyChanged(nameof(PathHint)); OnPropertyChanged(nameof(CanEditPath));
        }
        OnPropertyChanged(nameof(IsToolsVisible));
    }

    /// <summary>Invokes the shared action using the current path draft.</summary>
    /// <param name="command">Document action.</param>
    public void Execute(DocumentCommand command) { VerifyOwner(); _commands.Execute(command, command == DocumentCommand.OpenProject ? ProjectPathDraft : PathDraft); Refresh(); }
    /// <summary>Resolves the pending transition using the separate first-save draft.</summary>
    /// <param name="choice">Save/Discard/Cancel.</param>
    public void Choose(DocumentCloseChoice choice) { VerifyOwner(); _commands.Choose(choice, SavePathDraft); Refresh(); }
    /// <summary>Cancels pending file work or a transition decision.</summary>
    public void Cancel() { VerifyOwner(); _commands.Cancel(); Refresh(); }

    private void VerifyOwner()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var thread = Environment.CurrentManagedThreadId;
        if (Interlocked.CompareExchange(ref _owner, thread, 0) is var owner && owner != 0 && owner != thread)
            throw new InvalidOperationException("Document presentation must be updated on its UI owner thread.");
    }

    /// <summary>Disconnects UI notifications. Borrowed commands, projects and documents remain App-owned.</summary>
    public void Dispose() { if (_disposed) return; _disposed = true; ClearPropertyChangedSubscribers(); }
}
