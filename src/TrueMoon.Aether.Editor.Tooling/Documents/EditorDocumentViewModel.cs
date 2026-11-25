using TrueMoon.Argentis.Bindings;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Importing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tooling.Documents;



/// <summary>Owner-thread presentation of document commands, paths, decisions and diagnostics; no controls or native handles.</summary>
public sealed class EditorDocumentViewModel : ViewModel, IDisposable, IEditorDocumentViewModel
{
    private readonly EditorDocumentCommands _commands;
    private readonly EditorScenePlayback? _playback;
    private readonly EditorInspectorViewModel? _inspector;
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
    private bool _canOpenBuild, _canImportModel;
    private string _buildPath = "", _modelPath = "";
    private string? _activeBuildId;
    private EditorDocumentCommandState? _buildTarget;
    private SessionIdentity? _buildRuntime;
    private ProjectSession? _importTarget;

    /// <summary>Creates a model borrowing the App-owned coordinator. The first owner operation establishes the UI thread.</summary>
    /// <param name="commands">Tracked document actions.</param>
    public EditorDocumentViewModel(EditorDocumentCommands commands) : this(commands, null, null) { }

    internal EditorDocumentViewModel(EditorDocumentCommands commands, EditorScenePlayback? playback, EditorInspectorViewModel? inspector)
    { ArgumentNullException.ThrowIfNull(commands); _commands = commands; _playback = playback; _inspector = inspector; }

    /// <summary>Gets/sets the input used by Open, SaveAs and first Save. Completion never replaces this draft.</summary>
    public string PathDraft { get => _path; set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); if (_path == value) return; SetProperty(ref _path, value, nameof(PathDraft)); if (_pathRequest is not (DocumentCommand.OpenProject or DocumentCommand.OpenBuild or DocumentCommand.ImportModel)) OnPropertyChanged(nameof(RequestedPath)); } }
    /// <summary>Retained project manifest draft, separate from scene and first-save paths.</summary>
    public string ProjectPathDraft { get => _projectPath; set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); if (_projectPath == value) return; SetProperty(ref _projectPath, value, nameof(ProjectPathDraft)); if (_pathRequest == DocumentCommand.OpenProject) OnPropertyChanged(nameof(RequestedPath)); } }
    /// <summary>Path edited by the shared retained prompt.</summary>
    public string RequestedPath
    {
        get => _pathRequest switch { DocumentCommand.OpenProject => ProjectPathDraft, DocumentCommand.OpenBuild => _buildPath,
            DocumentCommand.ImportModel => _modelPath, _ => PathDraft };
        set
        {
            if (_pathRequest == DocumentCommand.OpenProject) ProjectPathDraft = value;
            else if (_pathRequest == DocumentCommand.OpenBuild)
            {
                VerifyOwner();
                ArgumentNullException.ThrowIfNull(value);
                SetProperty(ref _buildPath, value, nameof(RequestedPath));
                RefreshPathAdmission();
            }
            else if (_pathRequest == DocumentCommand.ImportModel)
            {
                VerifyOwner();
                ArgumentNullException.ThrowIfNull(value);
                SetProperty(ref _modelPath, value, nameof(RequestedPath));
                RefreshPathAdmission();
            }
            else PathDraft = value;
        }
    }
    /// <summary>Accessible path input label.</summary>
    public string PathInputLabel => _pathRequest switch
    {
        DocumentCommand.OpenProject => "Путь проекта",
        DocumentCommand.OpenBuild => "BuildId или папка сборки",
        DocumentCommand.ImportModel => "Путь модели",
        _ => "Путь сцены"
    };
    /// <summary>Prompt hint without changing the canonical scene-root contract.</summary>
    public string PathHint => _pathRequest switch
    {
        DocumentCommand.OpenProject => "Полный путь к файлу .aetherproj",
        DocumentCommand.OpenBuild => "BuildId из каталога сборок или полный путь к папке артефакта",
        DocumentCommand.ImportModel => "Путь к .gltf или .glb относительно корня проекта",
        _ => ScenesRoot
    };
    /// <inheritdoc />
    public bool SupportsPublishedBuilds => _playback != null;
    /// <inheritdoc />
    public bool CanOpenBuild => _canOpenBuild;
    /// <inheritdoc />
    public bool CanImportModel => _canImportModel;
    /// <summary>Exact BuildId of the selected artifact currently playing or paused.</summary>
    public string? ActiveBuildId => _activeBuildId;
    /// <summary>Actual active project snapshot; owned by the project coordinator.</summary>
    public ProjectSession? Project => _state?.Project;
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
    /// <summary>Formats the native/header caption from the same copied project/document state.</summary>
    /// <param name="baseTitle">Host-selected base caption.</param><returns>Caption including project, scene, dirty and activity markers.</returns>
    public string GetWindowCaption(string baseTitle)
    {
        ArgumentNullException.ThrowIfNull(baseTitle);
        return baseTitle + (ProjectName.Length == 0 ? "" : " · " + ProjectName) +
            (Document?.Content == null ? "" : " · " + Title) +
            (ActiveBuildId is { } build ? " · Build " + build[..12] : "");
    }
    /// <summary>Formats the shared accessible caption and full document/root path.</summary>
    /// <param name="baseTitle">Host-selected base caption.</param><returns>Caption followed by its path description.</returns>
    public string GetWindowCaptionDescription(string baseTitle) => GetWindowCaption(baseTitle) + "\n" + (Document?.Path ?? ScenesRoot) +
        (ActiveBuildId is { } build ? "\nBuildId " + build : "");
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
        _pathRequest == DocumentCommand.OpenBuild ? _buildRuntime == null ? "Запустить опубликованную сборку" : "Заменить текущую сборку" :
        _pathRequest == DocumentCommand.ImportModel ? "Импортировать модель" :
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
    public bool CanEditPath => _pathRequest switch
    {
        DocumentCommand.OpenProject => CanOpenProject,
        DocumentCommand.OpenBuild => CanOpenBuild,
        DocumentCommand.ImportModel => CanImportModel,
        _ => CanOpen
    };
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
    public GameEditorOperation RuntimeOperation => _state?.RuntimeOperation ?? GameEditorOperation.Idle;
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
        if (!ReferenceEquals(previous?.ModelCatalog, next.ModelCatalog)) OnPropertyChanged(nameof(ModelCatalog));
        SetProperty(ref _canNew, _commands.CanExecute(DocumentCommand.New), nameof(CanNew));
        var wasEditable = CanEditPath;
        SetProperty(ref _canOpenBuild, _inspector?.CanCancel != true && _playback?.CanUsePublishedBuild(_playback.PublishedBuildTarget) == true, nameof(CanOpenBuild));
        SetProperty(ref _canImportModel, _commands.CanExecute(DocumentCommand.ImportModel), nameof(CanImportModel));
        SetProperty(ref _activeBuildId, _playback?.ActivePublishedBuildId, nameof(ActiveBuildId));
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
        var errorText = next.Error switch
        {
            null => "",
            GameReplacementException { RollbackSucceeded: true, InnerException: OperationCanceledException } =>
                "Замена отменена. Предыдущая сборка восстановлена.",
            GameReplacementException { RollbackSucceeded: true } error =>
                "Предыдущая сборка восстановлена. " + error.Message,
            _ => "Ошибка: " + next.Error.Message
        };
        SetProperty(ref _error, errorText, nameof(ErrorText));
        var target = next.Operation switch { DocumentCommand.OpenProject => "открытием другого проекта", DocumentCommand.CloseProject => "закрытием проекта", DocumentCommand.Open => "открытием другой сцены", DocumentCommand.New => "созданием сцены", DocumentCommand.Exit => "выходом", _ => "закрытием сцены" };
        SetProperty(ref _decision, "Сохранить изменения перед " + target + "?", nameof(DecisionText));
        if (_pathRequest == DocumentCommand.OpenBuild && (!MatchesBuildTarget(next) || _playback!.PublishedBuildTarget != _buildRuntime))
            SetPathRequest(null);
        if (_pathRequest == DocumentCommand.ImportModel && !ReferenceEquals(_importTarget, next.Project))
            SetPathRequest(null);
        if (toolsVisible != IsToolsVisible) OnPropertyChanged(nameof(IsToolsVisible));
        _canConfirmPath = CanSubmitPath();
        if (confirm != CanConfirmPath) OnPropertyChanged(nameof(CanConfirmPath));
        if (dismiss != CanDismissTools) OnPropertyChanged(nameof(CanDismissTools));
        if (promptText != PathPromptText) OnPropertyChanged(nameof(PathPromptText));
    }

    /// <summary>Requests a UI action, collecting paths before Open, SaveAs or a first Save.</summary>
    /// <param name="command">New/Open/Save/SaveAs/Close action.</param>
    public void Request(DocumentCommand command)
    {
        VerifyOwner();
        if (command == DocumentCommand.ImportModel)
        {
            if (!_commands.CanExecute(command)) return;
            _importTarget = _commands.Read().Project;
            _dismissedError = null;
            SetPathRequest(command);
            return;
        }
        if (command == DocumentCommand.OpenBuild)
        {
            var state = _commands.Read();
            var runtime = _playback?.PublishedBuildTarget;
            if (_inspector?.CanCancel == true || _playback?.CanUsePublishedBuild(runtime) != true) return;
            _buildTarget = state;
            _buildRuntime = runtime;
            if (runtime != null && _playback.PreparedBuildId is { } prepared) _buildPath = prepared;
            _dismissedError = null;
            SetPathRequest(command);
            return;
        }
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
        if (_pathRequest == DocumentCommand.OpenBuild)
        {
            if (CanSubmitPath() && _playback!.UsePublishedBuild(_buildPath, _buildTarget!, _buildRuntime))
            {
                SetPathRequest(null);
                Refresh();
            }
            return;
        }
        if (_pathRequest == DocumentCommand.ImportModel)
        {
            if (CanSubmitPath() && _commands.ImportModel(_modelPath, _importTarget!))
            {
                SetPathRequest(null);
                Refresh();
            }
            return;
        }
        if (_pathRequest is not { } command || !CanSubmitPath()) return;
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
        if (command != DocumentCommand.OpenBuild) { _buildTarget = null; _buildRuntime = null; }
        if (command != DocumentCommand.ImportModel) _importTarget = null;
        _canConfirmPath = CanSubmitPath();
        if (previous != command)
        {
            OnPropertyChanged(nameof(PathRequest)); OnPropertyChanged(nameof(IsPathPromptOpen)); OnPropertyChanged(nameof(PathPromptText)); OnPropertyChanged(nameof(CanConfirmPath));
            OnPropertyChanged(nameof(CanDismissTools));
            OnPropertyChanged(nameof(RequestedPath)); OnPropertyChanged(nameof(PathInputLabel)); OnPropertyChanged(nameof(PathHint)); OnPropertyChanged(nameof(CanEditPath));
        }
        OnPropertyChanged(nameof(IsToolsVisible));
    }

    private void RefreshPathAdmission() => SetProperty(ref _canConfirmPath, CanSubmitPath(), nameof(CanConfirmPath));

    private bool CanSubmitPath()
    {
        if (_pathRequest == DocumentCommand.ImportModel)
            return !string.IsNullOrWhiteSpace(_modelPath) && _importTarget != null && ReferenceEquals(_importTarget, _commands.Read().Project) &&
                _commands.CanExecute(DocumentCommand.ImportModel);
        if (_pathRequest != DocumentCommand.OpenBuild)
            return _pathRequest is { } requested && _commands.CanExecute(requested);
        var state = _commands.Read();
        return MatchesBuildTarget(state) && !string.IsNullOrWhiteSpace(_buildPath) && _inspector?.CanCancel != true &&
            _playback?.CanUsePublishedBuild(_buildRuntime) == true;
    }

    private bool MatchesBuildTarget(EditorDocumentCommandState state) => _buildTarget is { } target &&
        ReferenceEquals(target.Project, state.Project) && ReferenceEquals(target.Document?.Content, state.Document?.Content) &&
        target.Document?.OpeningId == state.Document?.OpeningId;

    /// <summary>Invokes the shared action using the current path draft.</summary>
    /// <param name="command">Document action.</param>
    public void Execute(DocumentCommand command)
    {
        VerifyOwner();
        var path = command switch { DocumentCommand.OpenProject => ProjectPathDraft, DocumentCommand.ImportModel => _modelPath, _ => PathDraft };
        _commands.Execute(command, path);
        Refresh();
    }

    internal bool CanReimportModel => _inspector?.CanCancel != true && _commands.CanExecute(DocumentCommand.ReimportModel);
    internal bool CanChangeModelCatalog => CanSave && _inspector?.CanCancel != true;

    internal IReadOnlyList<ProjectModelCatalogItem>? ModelCatalog => _state?.ModelCatalog;
    internal bool HasModelCatalog => Project?.Documents?.HasModelCatalog == true;
    internal bool CanReadModelCatalog => HasModelCatalog && _commands.CanExecute(DocumentCommand.ReadModelCatalog);

    internal void ReadModelCatalog()
    {
        VerifyOwner();
        if (_commands.Execute(DocumentCommand.ReadModelCatalog)) Refresh();
    }

    internal void SelectModel(EditorProjectModelRow row)
    {
        VerifyOwner();
        if (CanChangeModelCatalog && _commands.SelectModel(row)) Refresh();
    }

    internal void PlaceModel(EditorProjectModelRow row)
    {
        VerifyOwner();
        if (CanChangeModelCatalog && _commands.PlaceModel(row)) Refresh();
    }

    internal void ReimportModel(EditorModelReimportAction action)
    {
        VerifyOwner();
        if (CanReimportModel && _commands.ReimportModel(action)) Refresh();
    }
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
    void IEditorDocumentViewModel.Choose(TrueMoon.Aether.Editor.UI.Documents.EditorDocumentCloseChoice choice)
    {
        if (!Enum.IsDefined(choice)) throw new ArgumentOutOfRangeException(nameof(choice));
        Choose(Enum.Parse<DocumentCloseChoice>(choice.ToString()));
    }
}
