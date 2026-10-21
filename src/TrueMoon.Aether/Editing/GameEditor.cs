using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Frames;

namespace TrueMoon.Aether.Editing;

/// <summary>App-owned, UI-independent Editor commands. Borrowed services remain owned by AetherHost.</summary>
public sealed class GameEditor
{
    private readonly Lock _gate = new();
    private readonly SessionSupervisor _supervisor;
    private readonly FrameSession _frames;
    private readonly GameBuildCompiler _builds;
    private readonly GameReplacement _replacement;
    private readonly string _hostId;
    private GameEditorStatus _status = new(0, null, GameEditorOperation.Idle, null, null, false);
    private Command? _active;
    private Task? _stop;
    private int _candidateFence;

    internal GameEditor(SessionSupervisor supervisor, FrameSession frames, GameBuildCompiler builds, GameReplacement replacement, string hostId)
    { _supervisor = supervisor; _frames = frames; _builds = builds; _replacement = replacement; _hostId = hostId; }

    /// <summary>Gets a snapshot; candidates invalidated by low-level session commands are hidden.</summary>
    public GameEditorStatus Status
    {
        get
        {
            lock (_gate)
            {
                if (_status.Candidate is { } candidate && !Matches(candidate.Origin, _candidateFence))
                    _status = _status with { Candidate = null };
                return _status;
            }
        }
    }

    /// <summary>Snapshots project/dependencies/scene/selection, cancels pending work and clears its candidate.</summary>
    public void SetContext(GameEditorContext context)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(context.Build);
        ArgumentNullException.ThrowIfNull(context.Scene); ArgumentNullException.ThrowIfNull(context.Selection);
        var build = context.Build;
        ArgumentException.ThrowIfNullOrWhiteSpace(build.ProjectPath); ArgumentException.ThrowIfNullOrWhiteSpace(build.EntryType);
        if (build.SourceRevision < 0) throw new ArgumentOutOfRangeException(nameof(context));
        var dependencies = build.Dependencies?.ToArray() ?? [];
        GameBuildArtifact.ValidateNames([build.EntryAssembly, .. dependencies]);
        var snapshot = context with { Build = build with { ProjectPath = Path.GetFullPath(build.ProjectPath), Dependencies = Array.AsReadOnly(dependencies) } };
        lock (_gate)
        {
            EnsureOpen();
            _status = _status with { ContextVersion = checked(_status.ContextVersion + 1), Context = snapshot, Candidate = null, Error = null };
            _active?.Cancellation.Cancel();
        }
    }

    /// <summary>Builds a candidate without changing the running world. Stale completion is rejected.</summary>
    public Task<GameBuildArtifact> BuildAsync(CancellationToken token = default)
        => Run(GameEditorOperation.Build, async command =>
        {
            var build = await _builds.BuildAsync(command.Context.Build, command.Cancellation.Token).ConfigureAwait(false);
            Select(command, build); return build;
        }, token, clearCandidate: true);

    /// <summary>Explicitly selects a previously published build for the current context and runtime.</summary>
    public Task<GameBuildArtifact> SelectBuildAsync(GameBuildArtifact build, CancellationToken token = default)
        => Run(GameEditorOperation.Select, command =>
        {
            ArgumentNullException.ThrowIfNull(build);
            var validated = GameBuildArtifact.Open(build.DirectoryPath, _hostId, build.BuildId);
            Select(command, validated); return Task.FromResult(validated);
        }, token, clearCandidate: true);

    private void Select(Command command, GameBuildArtifact build)
    {
        lock (_gate)
        {
            CheckCurrent(command);
            if (build.HostContractId != _hostId || build.SourceRevision != command.Context.Build.SourceRevision ||
                build.EntryAssembly != command.Context.Build.EntryAssembly || build.EntryType != command.Context.Build.EntryType)
                throw new InvalidDataException("Candidate does not match the selected project contract/source revision/entry point.");
            _candidateFence = command.Fence;
            _status = _status with { Candidate = new(build, command.Version, command.Origin) };
        }
    }

    /// <summary>Explicitly starts/reset the scene using the selected build, or the fixed runtime when none is selected.</summary>
    public Task StartAsync(int width, int height, CancellationToken token = default)
        => Run(GameEditorOperation.Start, async command =>
        {
            var candidate = Candidate(command, required: false);
            if (candidate == null) await _frames.StartAsync(command.Context.Scene, width, height, token: command.Cancellation.Token).ConfigureAwait(false);
            else await _frames.StartAsync(command.Context.Scene, candidate.Build, width, height, token: command.Cancellation.Token).ConfigureAwait(false);
            lock (_gate) if (_status.ContextVersion == command.Version) _status = _status with { Candidate = null };
            return true;
        }, token);

    /// <summary>Applies the selected candidate through the checkpoint/rollback transaction.</summary>
    public Task<GameReplacementResult> ReplaceAsync(CancellationToken token = default)
        => Run(GameEditorOperation.Replace, async command =>
        {
            var candidate = Candidate(command, required: true)!;
            var identity = command.Origin ?? throw new InvalidOperationException("No originating runtime to replace.");
            var result = await _replacement.ReplaceAsync(new(candidate.Build, identity, command.Context.Scene,
                command.Context.Build.SourceRevision), command.Cancellation.Token).ConfigureAwait(false);
            lock (_gate) if (_status.ContextVersion == command.Version) _status = _status with { Candidate = null };
            return result;
        }, token);

    /// <summary>Resumes simulation.</summary>
    public Task PlayAsync(CancellationToken token = default) => Run(GameEditorOperation.Play, c => _supervisor.SetPlayingAsync(true, c.Cancellation.Token), token);
    /// <summary>Pauses simulation at an acknowledged tick boundary.</summary>
    public Task PauseAsync(CancellationToken token = default) => Run(GameEditorOperation.Pause, c => _supervisor.SetPlayingAsync(false, c.Cancellation.Token), token);
    /// <summary>Advances one paused simulation tick.</summary>
    public Task StepAsync(CancellationToken token = default) => Run(GameEditorOperation.Step, c => _supervisor.StepAsync(c.Cancellation.Token), token);

    /// <summary>Cancels and observes the current command. Replace retains its independent rollback policy.</summary>
    public async Task CancelAsync()
    {
        Task? pending;
        lock (_gate) { EnsureOpen(); _active?.Cancellation.Cancel(); pending = _active?.Completion.Task; }
        if (pending != null) await ObserveAsync(pending).ConfigureAwait(false);
    }

    /// <summary>Invalidates candidates, interrupts replacement recovery and drains accepted commands and frame resources.</summary>
    /// <remarks>Concurrent calls share completion; failed cleanup can be retried. Previous command errors stay in Status.</remarks>
    public Task StopAsync()
    {
        TaskCompletionSource completion; Task? pending;
        lock (_gate)
        {
            if (_stop is { IsCompleted: false }) return _stop;
            _supervisor.Admission.Interrupt(); // Fence recovery before caller cancellation can wake the transaction.
            _status = _status with { ContextVersion = checked(_status.ContextVersion + 1), Candidate = null, Operation = GameEditorOperation.Stop };
            _active?.Cancellation.Cancel(); pending = _active?.Completion.Task;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _stop = completion.Task;
        }
        _ = StopCoreAsync(pending, completion); return completion.Task;
    }

    private async Task StopCoreAsync(Task? pending, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try { await _frames.StopAsync().ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        if (pending != null) await ObserveAsync(pending).ConfigureAwait(false);
        lock (_gate)
        {
            _status = _status with { Operation = GameEditorOperation.Idle, Error = failure ?? _status.Error };
            if (failure == null) completion.TrySetResult(); else completion.TrySetException(failure);
        }
    }

    internal void CloseAdmission()
    {
        lock (_gate)
        {
            if (_status.Closed) return;
            _status = _status with { Closed = true, ContextVersion = checked(_status.ContextVersion + 1), Candidate = null };
            _active?.Cancellation.Cancel();
        }
    }

    private GameEditorCandidate? Candidate(Command command, bool required)
    {
        lock (_gate)
        {
            CheckCurrent(command);
            var candidate = _status.Candidate;
            if (candidate != null && (candidate.ContextVersion != command.Version || !Matches(candidate.Origin, _candidateFence)))
                throw new OperationCanceledException("Selected build belongs to an earlier Editor context/runtime.");
            return candidate ?? (required ? throw new InvalidOperationException("Build or select a candidate first.") : null);
        }
    }
    private bool Matches(SessionIdentity? origin, int fence) => _supervisor.Status.Identity == origin && _supervisor.StopVersion == fence;
    private void CheckCurrent(Command command)
    {
        command.Cancellation.Token.ThrowIfCancellationRequested();
        if (_status.Closed || _status.ContextVersion != command.Version || !Matches(command.Origin, command.Fence))
            throw new OperationCanceledException("Command completed for an earlier Editor context/runtime.");
    }
    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_status.Closed, this);
    private Task<T> Run<T>(GameEditorOperation kind, Func<Command, Task<T>> action, CancellationToken token, bool clearCandidate = false)
    {
        Command command; var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            EnsureOpen();
            if (_active != null || _stop is { IsCompleted: false }) throw new InvalidOperationException("An Editor command is already running.");
            var context = _status.Context ?? throw new InvalidOperationException("Select an Editor context first.");
            command = new(context, _status.ContextVersion, _supervisor.Status.Identity, _supervisor.StopVersion,
                CancellationTokenSource.CreateLinkedTokenSource(token));
            _active = command;
            _status = _status with { Operation = kind, Error = null, Candidate = clearCandidate ? null : _status.Candidate };
        }
        _ = ExecuteAsync(); return result.Task;
        async Task ExecuteAsync()
        {
            T value = default!; Exception? failure = null;
            try { command.Cancellation.Token.ThrowIfCancellationRequested(); value = await action(command).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
            lock (_gate)
            {
                _active = null;
                _status = _status with { Operation = _stop is { IsCompleted: false } ? GameEditorOperation.Stop : GameEditorOperation.Idle, Error = failure };
                command.Cancellation.Dispose();
                // Complete only after status and command admission have been updated.
                if (failure is OperationCanceledException canceled) result.TrySetCanceled(canceled.CancellationToken);
                else if (failure != null) result.TrySetException(failure); else result.TrySetResult(value);
                command.Completion.TrySetResult();
            }
        }
    }
    private static async Task ObserveAsync(Task pending)
    { try { await pending.ConfigureAwait(false); } catch (Exception) { /* The originating caller owns the command error; Status retains it. */ } }
    private sealed record Command(GameEditorContext Context, long Version, SessionIdentity? Origin, int Fence, CancellationTokenSource Cancellation)
    { internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
}
