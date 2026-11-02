using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor.Editing;

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
    private readonly CancellationToken _lifetime;
    private readonly Task _ready;
    private CancellationTokenSource? _cancellation;
    private Task _pending = Task.CompletedTask;
    private Task? _interrupt;
    private Exception? _error;
    private bool _ownsRuntime;
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
            (version, token) => editor.ReplaceBuildAsync(version, token)) { }

    internal EditorScenePlayback(EditorDocumentCommands documents, EditorPreview preview, Func<SessionStatus> runtime,
        Func<GameEditorStatus> editor, Action<GameEditorContext> setContext,
        Func<EditorCommand, CancellationToken, Task> dispatch, Func<long, CancellationToken, Task> start, CancellationToken lifetime = default, Task? ready = null,
        Func<long, CancellationToken, Task>? replace = null)
    {
        ArgumentNullException.ThrowIfNull(documents); ArgumentNullException.ThrowIfNull(preview); ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(setContext); ArgumentNullException.ThrowIfNull(dispatch); ArgumentNullException.ThrowIfNull(start);
        _documents = documents; _preview = preview; _runtime = runtime; _editor = editor; _setContext = setContext;
        _dispatch = dispatch; _start = start; _lifetime = lifetime;
        _ready = ready ?? Task.CompletedTask;
        _replace = replace ?? ((_, token) => dispatch(EditorCommand.Replace, token));
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
            return command switch
            {
                EditorCommand.Build or EditorCommand.Start => !_ownsRuntime && (runtime.State == SessionState.Stopped || _preview.Read().Active),
                EditorCommand.Play => _ownsRuntime && runtime.State == SessionState.Paused,
                EditorCommand.Pause => _ownsRuntime && runtime.State == SessionState.Playing,
                EditorCommand.Step => _ownsRuntime && runtime.State == SessionState.Paused,
                EditorCommand.Replace => _ownsRuntime && runtime.State is SessionState.Paused or SessionState.Playing &&
                    runtime.Identity == _runningIdentity && _runningIdentity != null &&
                    ReferenceEquals(document.Project, _runningProject) && ReferenceEquals(document.Document.Content, _runningContent),
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

    private bool Execute(EditorCommand command, bool play)
    {
        lock (_gate)
        {
            if (!CanExecute(command)) return false;
            if (command is EditorCommand.Stop or EditorCommand.Cancel)
            {
                var interrupted = _pending; var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _interrupt = completion.Task;
                _ = InterruptAsync(command, interrupted, completion); return true;
            }
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime);
            Task work;
            try
            {
                work = _documents.RunRuntimeAsync(command switch
                {
                    EditorCommand.Build => GameEditorOperation.Build, EditorCommand.Start => GameEditorOperation.Start,
                    EditorCommand.Replace => GameEditorOperation.Replace,
                    EditorCommand.Play => GameEditorOperation.Play, EditorCommand.Pause => GameEditorOperation.Pause, _ => GameEditorOperation.Step
                }, command is EditorCommand.Build or EditorCommand.Start, RunAsync, cancellation.Token);
            }
            catch { cancellation.Dispose(); throw; }
            _cancellation = cancellation; _error = null;
            _pending = ObserveAsync(work, cancellation); return true;

            async Task RunAsync(ProjectSession project, SceneAuthoringState content, CancellationToken token)
            {
                if (command is not (EditorCommand.Build or EditorCommand.Start or EditorCommand.Replace))
                { await _dispatch(command, token).ConfigureAwait(false); return; }
                var scene = PrimitiveSceneAdapter.Project(content.Resolved, token);
                var replacing = command == EditorCommand.Replace;
                var origin = _runtime().Identity;
                if (!replacing) await _preview.StopAsync().ConfigureAwait(false);
                VerifyCapture(project, content, token);
                if (replacing) VerifyRunning(project, content, origin, token);
                else if (_runtime().State != SessionState.Stopped) throw new InvalidOperationException("Stop the previous runtime before starting a scene game.");
                var revision = Interlocked.Increment(ref _revision);
                _setContext(new(project.CreateBuildRequest(revision) with { TrackInputs = true, CaptureCompilation = true, CapturePreparation = true,
                    CaptureProjectReferences = true, ReuseArtifact = command == EditorCommand.Start }, scene, ""));
                var selected = _editor();
                if (!ReferenceEquals(selected.Context?.Scene, scene) || selected.Context.Build.SourceRevision != revision)
                    throw new OperationCanceledException("Game context was superseded before compilation.", token);
                var version = selected.ContextVersion;
                await _dispatch(EditorCommand.Build, token).ConfigureAwait(false);
                VerifyCapture(project, content, token);
                var built = _editor();
                if (built.ContextVersion == version && ReferenceEquals(built.Context?.Scene, scene) &&
                    built.Candidate is { } candidate && candidate.ContextVersion == version)
                    project.BindSourceBuild(candidate.Build);
                if (command == EditorCommand.Build) return;
                if (replacing)
                {
                    VerifyRunning(project, content, origin, token);
                    try
                    {
                        await _documents.RunCapturedRuntime(project, content, captured => _replace(version, captured), token).ConfigureAwait(false);
                        lock (_gate) _runningIdentity = _runtime().Identity;
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
                    lock (_gate) { _runningProject = project; _runningContent = content; _runningIdentity = _runtime().Identity; }
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
                throw new OperationCanceledException("Replacement no longer targets the owned game/document.", token);
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
