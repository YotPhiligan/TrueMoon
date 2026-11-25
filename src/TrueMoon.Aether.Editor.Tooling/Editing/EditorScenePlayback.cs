using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Documents;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Tooling.Editing;

/// <summary>Runs the current authoring scene as an independent game copy, preserving its document and history.</summary>
/// <remarks>App-owned coordinator borrowing document, preview and engine services. Each start verifies game inputs;
/// Save is optional. Uncommitted inspector fields are handled by the presentation model before admission.</remarks>
public sealed class EditorScenePlayback
{
    private readonly Lock _gate = new();
    private readonly EditorDocumentCommands _documents;
    private readonly EditorPreview _preview;
    private readonly Func<SessionStatus> _runtime;
    private readonly Func<GameEditorStatus> _editor;
    private readonly Action<GameEditorContext> _setContext;
    private readonly Func<EditorCommand, CancellationToken, Task> _dispatch;
    private readonly Func<long, CancellationToken, Task> _start;
    private readonly Func<long, CancellationToken, Task> _replace;
    private readonly Func<GameBuildArtifact, long, CancellationToken, Task>? _selectBuild;
    private readonly Func<string, GameBuildArtifact>? _openBuild;
    private readonly AetherEditorOptions? _startup;
    private readonly CancellationToken _lifetime;
    private readonly Task _ready;
    private CancellationTokenSource? _cancellation;
    private Task _pending = Task.CompletedTask;
    private Task? _interrupt;
    private Exception? _error;
    private bool _ownsRuntime;
    private bool _runningSelectedBuild;
    private ProjectSession? _runningProject;
    private SceneAuthoringState? _runningContent;
    private SessionIdentity? _runningIdentity;
    private long _revision;

    /// <summary>Creates scene playback over the App-owned engine and shared viewport readiness.</summary>
    /// <param name="documents">Shared document/exit admission.</param><param name="preview">Borrowed edit preview owner.</param>
    /// <param name="editor">Borrowed engine commands.</param><param name="runtime">Fresh runtime observations.</param>
    /// <param name="ready">Viewport transport readiness.</param><param name="lifetime">App cancellation.</param>
    public EditorScenePlayback(EditorDocumentCommands documents, EditorPreview preview, GameEditor editor,
        Func<SessionStatus> runtime, Task ready, CancellationToken lifetime = default)
        : this(documents, preview, runtime, () => editor.Status, (editor ?? throw new ArgumentNullException(nameof(editor))).SetContext, EditorCommands.Dispatch(editor, ready),
            (version, token) => editor.StartBuildAsync(960, 540, version, token), lifetime, ready,
            (version, token) => editor.ReplaceBuildAsync(version, token),
            (build, version, token) => editor.SelectBuildForContextAsync(build, version, token), editor.OpenPublishedBuild) { }

    internal EditorScenePlayback(EditorDocumentCommands documents, EditorPreview preview, GameEditor editor,
        Func<SessionStatus> runtime, Task ready, CancellationToken lifetime, AetherEditorOptions startup)
        : this(documents, preview, editor, runtime, ready, lifetime) => _startup = startup;

    internal EditorScenePlayback(EditorDocumentCommands documents, EditorPreview preview, Func<SessionStatus> runtime,
        Func<GameEditorStatus> editor, Action<GameEditorContext> setContext,
        Func<EditorCommand, CancellationToken, Task> dispatch, Func<long, CancellationToken, Task> start, CancellationToken lifetime = default, Task? ready = null,
        Func<long, CancellationToken, Task>? replace = null, Func<GameBuildArtifact, long, CancellationToken, Task>? selectBuild = null,
        Func<string, GameBuildArtifact>? openBuild = null)
    {
        ArgumentNullException.ThrowIfNull(documents); ArgumentNullException.ThrowIfNull(preview); ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(setContext); ArgumentNullException.ThrowIfNull(dispatch); ArgumentNullException.ThrowIfNull(start);
        _documents = documents; _preview = preview; _runtime = runtime; _editor = editor; _setContext = setContext;
        _dispatch = dispatch; _start = start; _lifetime = lifetime;
        _ready = ready ?? Task.CompletedTask;
        _replace = replace ?? ((_, token) => dispatch(EditorCommand.Replace, token));
        _selectBuild = selectBuild;
        _openBuild = openBuild;
    }

    /// <summary>Actual completion of scene work or an interrupt, including compiler/runtime drain.</summary>
    public Task Pending { get { lock (_gate) return _interrupt is { IsCompleted: false } ? _interrupt : _pending; } }
    /// <summary>Last non-cancellation command/cleanup failure.</summary>
    public Exception? Error { get { lock (_gate) return _error; } }
    /// <summary>Whether this coordinator has accepted a game startup requiring Stop/recovery.</summary>
    public bool OwnsRuntime { get { lock (_gate) return _ownsRuntime; } }

    internal bool Matches(ProjectSession? project, SceneDocument? document)
    {
        var state = _documents.Read();
        return state.ProjectReady && !state.Closed && !state.Exiting && ReferenceEquals(project, state.Project) &&
            document != null && ReferenceEquals(document, state.Document?.Content?.Document);
    }

    /// <summary>Checks current admission; stale presentation never authorizes an action.</summary>
    /// <param name="command">Game command.</param><returns>Whether the command can be accepted now.</returns>
    public bool CanExecute(EditorCommand command)
        => CanExecute(command, selectedBuild: false);

    private bool CanExecute(EditorCommand command, bool selectedBuild)
    {
        if (!Enum.IsDefined(command)) throw new ArgumentOutOfRangeException(nameof(command));
        lock (_gate)
        {
            if (_interrupt is { IsCompleted: false }) return false;
            if (command == EditorCommand.Stop) return _ownsRuntime || !_pending.IsCompleted;
            if (command == EditorCommand.Cancel) return !_pending.IsCompleted;
            if (_lifetime.IsCancellationRequested || !_pending.IsCompleted) return false;
            var document = _documents.Read(); var runtime = _runtime(); var editor = _editor();
            if (document.Closed || document.Exiting || document.Busy || !document.ProjectReady || document.Document?.Content == null ||
                document.Document.IsClosed || document.Document.Operation != DocumentOperation.None || editor.Closed || editor.Operation != GameEditorOperation.Idle) return false;
            var canStart = !_ownsRuntime && (runtime.State == SessionState.Stopped || _preview.Read().Active);
            var sameGame = _ownsRuntime && _runningIdentity != null && runtime.Identity == _runningIdentity &&
                ReferenceEquals(document.Project, _runningProject) && ReferenceEquals(document.Document.Content, _runningContent);
            return command switch
            {
                EditorCommand.Start => canStart,
                EditorCommand.Build => canStart || sameGame && runtime.State == SessionState.Playing,
                EditorCommand.Play => _ownsRuntime && runtime.State == SessionState.Paused,
                EditorCommand.Pause => _ownsRuntime && runtime.State == SessionState.Playing,
                EditorCommand.Step => _ownsRuntime && runtime.State == SessionState.Paused,
                EditorCommand.Replace => sameGame && (selectedBuild || !_runningSelectedBuild) &&
                    runtime.State is SessionState.Paused or SessionState.Playing,
                _ => false
            };
        }
    }

    /// <summary>Starts a scene command. Start creates a paused game; RunSimulation starts and plays in one tracked gesture.</summary>
    /// <param name="command">Requested game command.</param><returns>True only if admitted.</returns>
    public bool Execute(EditorCommand command) => Execute(command, false);
    /// <summary>Builds/starts/plays the current scene, or resumes the owned paused game.</summary>
    /// <returns>True only if admitted.</returns>
    public bool RunSimulation() => CanExecute(EditorCommand.Play) ? Execute(EditorCommand.Play) : Execute(EditorCommand.Start, true);

    /// <summary>Starts and plays the current full scene on a specifically selected immutable build.</summary>
    /// <remarks>Uses current authoring revision and the ordinary Start/Stop admission. Stop before selecting another build.</remarks>
    /// <param name="build">Published build for the active project and this Editor's runtime host.</param>
    /// <returns>True only if the command was admitted. Inspect Pending and Error for the result.</returns>
    public bool RunSelectedBuild(GameBuildArtifact build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return Execute(EditorCommand.Start, true, build);
    }

    /// <summary>Replaces the selected build of the running full scene and preserves its checkpoint and playback mode.</summary>
    /// <param name="build">Published candidate for the current project, scene and host.</param>
    /// <param name="token">Cancellation of the forward operation. The Editor owns any required recovery.</param>
    /// <returns>True if admitted. Await Pending and inspect Error for the result.</returns>
    public bool ReplaceSelectedBuild(GameBuildArtifact build, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(build);
        return Execute(EditorCommand.Replace, false, build, token);
    }

    internal SessionIdentity? PublishedBuildTarget { get { lock (_gate) return _runningIdentity; } }
    internal string? PreparedBuildId => _editor().Candidate?.Build.Artifact.BuildId;

    internal string? ActivePublishedBuildId
    {
        get
        {
            lock (_gate)
            {
                var runtime = _runtime();
                return _ownsRuntime && _runningSelectedBuild && runtime.State is SessionState.Playing or SessionState.Paused &&
                    runtime.Identity == _runningIdentity ? runtime.Identity?.GameBuildId : null;
            }
        }
    }

    internal bool CanUsePublishedBuild(SessionIdentity? expected)
    {
        lock (_gate)
            return _openBuild != null && _runningIdentity == expected &&
                CanExecute(expected == null ? EditorCommand.Start : EditorCommand.Replace, selectedBuild: true);
    }

    internal bool UsePublishedBuild(string selection, EditorDocumentCommandState target, SessionIdentity? expected)
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(selection) || !CanUsePublishedBuild(expected)) return false;
            return Execute(expected == null ? EditorCommand.Start : EditorCommand.Replace, expected == null,
                openBuild: (project, content, token) =>
                {
                    if (!ReferenceEquals(project, target.Project) || !ReferenceEquals(content, target.Document?.Content) ||
                        PublishedBuildTarget != expected)
                        throw new OperationCanceledException("Build selection was superseded.", token);
                    return _openBuild!(selection);
                });
        }
    }

    private bool Execute(EditorCommand command, bool play, GameBuildArtifact? build = null, CancellationToken caller = default,
        Func<ProjectSession, SceneAuthoringState, CancellationToken, GameBuildArtifact>? openBuild = null)
    {
        lock (_gate)
        {
            if (caller.IsCancellationRequested || !CanExecute(command, build != null || openBuild != null)) return false;
            if (command is EditorCommand.Stop or EditorCommand.Cancel)
            {
                var interrupted = _pending; var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _interrupt = completion.Task;
                _ = InterruptAsync(command, interrupted, completion); return true;
            }
            var buildingWhilePlaying = command == EditorCommand.Build && _ownsRuntime;
            var runningOrigin = _runningIdentity;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime, caller);
            Task work;
            try
            {
                work = _documents.RunRuntimeAsync(command switch
                {
                    EditorCommand.Build => GameEditorOperation.Build, EditorCommand.Start => GameEditorOperation.Start,
                    EditorCommand.Replace => GameEditorOperation.Replace,
                    EditorCommand.Play => GameEditorOperation.Play, EditorCommand.Pause => GameEditorOperation.Pause, _ => GameEditorOperation.Step
                }, command == EditorCommand.Start || command == EditorCommand.Build && !buildingWhilePlaying, RunAsync, cancellation.Token);
            }
            catch { cancellation.Dispose(); throw; }
            _cancellation = cancellation; _error = null;
            _pending = ObserveAsync(work, cancellation); return true;

            async Task RunAsync(ProjectSession project, SceneAuthoringState content, CancellationToken token)
            {
                if (command is not (EditorCommand.Build or EditorCommand.Start or EditorCommand.Replace))
                { await _dispatch(command, token).ConfigureAwait(false); return; }
                var replacing = command == EditorCommand.Replace;
                var usesRunningGame = replacing || buildingWhilePlaying;
                if (usesRunningGame) VerifyRunning(project, content, runningOrigin, token);
                var selectedBuild = build ?? openBuild?.Invoke(project, content, token);
                token.ThrowIfCancellationRequested();
                var startupProject = _startup?.ProjectPath is { } path && string.Equals(path, project.Project.ManifestPath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                if (_startup?.SingleProject == true && !startupProject)
                    throw new InvalidOperationException("This development host can build and play only its configured game project.");
                GameBuildRenderContent? modelContent = null;
                if (selectedBuild == null)
                {
                    modelContent = startupProject ? _startup!.ProjectRenderContent : null;
                    if (modelContent != null && modelContent.AssetKeys[0].ProjectId != project.Project.Description.Id)
                        throw new InvalidDataException("Development render content belongs to another project.");
                    if (modelContent == null && project.Documents is { } document)
                        modelContent = await document.CheckModelImportsAsync(document.Read().OpeningId, token).ConfigureAwait(false);
                }
                VerifyCapture(project, content, token);
                var fullScene = selectedBuild != null || modelContent != null;
                var scene = fullScene ? null : PrimitiveSceneAdapter.Project(content.Resolved, token);
                var world = fullScene ? new SceneWorldData(content.Document) : null;
                if (selectedBuild?.RenderContent is { } manifest && manifest.ProjectId != project.Project.Description.Id)
                    throw new InvalidDataException("Selected render content belongs to another project.");
                if (!usesRunningGame) await _preview.StopAsync().ConfigureAwait(false);
                VerifyCapture(project, content, token);
                if (usesRunningGame) VerifyRunning(project, content, runningOrigin, token);
                else if (_runtime().State != SessionState.Stopped) throw new InvalidOperationException("Stop the previous runtime before starting a scene game.");
                var revision = selectedBuild?.SourceRevision ?? Interlocked.Increment(ref _revision);
                var request = project.CreateBuildRequest(revision);
                if (selectedBuild == null)
                    request = request with { TrackInputs = true, CaptureCompilation = true, CapturePreparation = true,
                        CaptureProjectReferences = true, ReuseArtifact = command == EditorCommand.Start, RenderContent = modelContent };
                _setContext(new(request, scene, "") { World = world });
                var selected = _editor();
                if (selected.Context is not { } context || !ReferenceEquals(context.Scene, scene) ||
                    !ReferenceEquals(context.World, world) || context.Build.SourceRevision != revision)
                    throw new OperationCanceledException("Game context was superseded during game preparation.", token);
                var version = selected.ContextVersion;
                if (selectedBuild == null)
                    await _dispatch(EditorCommand.Build, token).ConfigureAwait(false);
                else
                    await (_selectBuild ?? throw new InvalidOperationException("Artifact selection is unavailable."))(selectedBuild, version, token).ConfigureAwait(false);
                VerifyCapture(project, content, token);
                if (usesRunningGame) VerifyRunning(project, content, runningOrigin, token);
                var built = _editor();
                if (selectedBuild == null && built.ContextVersion == version && ReferenceEquals(built.Context?.Scene, scene) &&
                    ReferenceEquals(built.Context?.World, world) &&
                    built.Candidate is { } candidate && candidate.ContextVersion == version)
                    project.BindSourceBuild(candidate.Build);
                if (command == EditorCommand.Build) return;
                if (replacing)
                {
                    try
                    {
                        await _documents.RunCapturedRuntime(project, content, captured => _replace(version, captured), token).ConfigureAwait(false);
                        lock (_gate)
                        {
                            _runningIdentity = _runtime().Identity;
                            _runningSelectedBuild = selectedBuild != null;
                        }
                    }
                    catch (GameReplacementException error) when (error.RollbackSucceeded)
                    { lock (_gate) _runningIdentity = _runtime().Identity; throw; }
                    return;
                }
                await _ready.WaitAsync(token).ConfigureAwait(false);
                // The engine atomically requires this version and its candidate; no fixed-runtime fallback is allowed.
                var startup = _documents.RunCapturedRuntime(project, content, captured => _start(version, captured), token);
                lock (_gate) _ownsRuntime = true;
                try
                {
                    await startup.ConfigureAwait(false); token.ThrowIfCancellationRequested();
                    lock (_gate)
                    {
                        _runningProject = project; _runningContent = content; _runningIdentity = _runtime().Identity;
                        _runningSelectedBuild = selectedBuild != null;
                    }
                    if (play) await _dispatch(EditorCommand.Play, token).ConfigureAwait(false);
                }
                catch (Exception failure)
                {
                    try
                    {
                        await _dispatch(EditorCommand.Stop, CancellationToken.None).ConfigureAwait(false);
                        lock (_gate) { _ownsRuntime = false; _runningProject = null; _runningContent = null; _runningIdentity = null; }
                    }
                    catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                    throw;
                }
            }
        }
    }

    private void VerifyRunning(ProjectSession project, SceneAuthoringState content, SessionIdentity? origin, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var runtime = _runtime();
        lock (_gate)
            if (origin == null || runtime.Identity != origin || origin != _runningIdentity ||
                runtime.State is not (SessionState.Paused or SessionState.Playing) ||
                !ReferenceEquals(project, _runningProject) || !ReferenceEquals(content, _runningContent))
                throw new OperationCanceledException("The operation no longer targets the owned game/document.", token);
    }

    private void VerifyCapture(ProjectSession project, SceneAuthoringState content, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var state = _documents.Read(); var actual = state.Document?.Content;
        if (project.IsClosed || !state.ProjectReady || !ReferenceEquals(project, state.Project) ||
            !ReferenceEquals(content, actual))
            throw new OperationCanceledException("The scene/project changed during game preparation.", token);
    }

    private async Task ObserveAsync(Task work, CancellationTokenSource cancellation)
    {
        try { await work.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception error) { lock (_gate) _error = error; }
        finally { lock (_gate) { _cancellation = null; cancellation.Dispose(); } }
    }

    private async Task InterruptAsync(EditorCommand command, Task work, TaskCompletionSource completion)
    {
        Exception? failure = null; Task? dispatch = null;
        // Stop must fence replacement recovery synchronously before cancellation wakes it.
        if (command == EditorCommand.Stop)
            try { dispatch = _dispatch(command, CancellationToken.None); }
            catch (Exception error) { failure = error; }
        try { _cancellation?.Cancel(); } catch (Exception error) { failure = error; }
        try
        {
            if (command != EditorCommand.Stop) dispatch = _dispatch(command, CancellationToken.None);
            if (dispatch != null) await dispatch.ConfigureAwait(false);
        }
        catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
        await work.ConfigureAwait(false);
        lock (_gate)
        {
            if (failure == null && command == EditorCommand.Stop)
            { _ownsRuntime = false; _runningProject = null; _runningContent = null; _runningIdentity = null; }
            if (failure != null && command == EditorCommand.Stop) _ownsRuntime = true; // Preserve retry admission for incomplete cleanup.
            _error = failure ?? _error;
            completion.TrySetResult();
        }
    }
}
