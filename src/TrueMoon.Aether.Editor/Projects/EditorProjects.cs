namespace TrueMoon.Aether.Editor.Projects;

/// <summary>App-owned project coordinator. Candidates become active only after complete file/SDK validation.</summary>
/// <remarks>One Open/Create at a time, with fail-fast admission. Close fences and drains accepted work.
/// Sessions hold metadata only; documents/runtime work must be coordinated before UI project switching is added.</remarks>
public sealed class EditorProjects : IDisposable, IAsyncDisposable
{
    private readonly IProjectStore _store;
    private readonly IProjectToolchain _toolchain;
    private readonly IFactory<ProjectSession, ProjectSessionArguments> _sessions;
    private readonly Lock _gate = new();
    private ProjectSession? _active;
    private readonly List<ProjectSession> _retired = [];
    private CancellationTokenSource? _pending;
    private Task _work = Task.CompletedTask;
    private Task _close = Task.CompletedTask;
    private bool _closed, _draining;
    private Exception? _error;

    /// <summary>Creates a coordinator borrowing App-owned persistence, preflight and product factory services.</summary>
    /// <param name="store">Project persistence.</param><param name="toolchain">Selected SDK preflight.</param>
    /// <param name="sessions">Factory whose products are owned only by this coordinator.</param>
    public EditorProjects(IProjectStore store, IProjectToolchain toolchain,
        IFactory<ProjectSession, ProjectSessionArguments> sessions)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(toolchain); ArgumentNullException.ThrowIfNull(sessions);
        _store = store; _toolchain = toolchain; _sessions = sessions;
    }
    /// <summary>Gets the borrowed active session, or null. A failed candidate leaves it unchanged.</summary>
    public ProjectSession? Active { get { lock (_gate) return _active; } }
    /// <summary>Gets whether terminal App shutdown has closed admission.</summary>
    public bool IsClosed { get { lock (_gate) return _closed; } }
    /// <summary>Gets the last operation error, cleared by a new operation or successful close.</summary>
    public Exception? Error { get { lock (_gate) return _error; } }

    /// <summary>Prepares and atomically selects an existing project. Never starts a compiler or game runtime.</summary>
    /// <param name="manifestPath">.aetherproj path.</param><param name="cancellationToken">Candidate cancellation.</param>
    /// <returns>The borrowed committed session.</returns>
    public Task<ProjectSession> OpenAsync(string manifestPath, CancellationToken cancellationToken = default)
        => Begin(manifestPath, null, cancellationToken);

    /// <summary>Validates an existing game project and publishes a new manifest before selecting its session.</summary>
    /// <remarks>If cancellation/Close wins after file publication, the valid new manifest remains on disk
    /// and the candidate is not selected. Caller cancellation preserves the previous session;
    /// explicit Close still closes it. Existing files are never overwritten.</remarks>
    /// <param name="manifestPath">New manifest in an existing project directory.</param>
    /// <param name="description">Versioned portable metadata.</param><param name="cancellationToken">Candidate cancellation.</param>
    /// <returns>The borrowed committed session.</returns>
    public Task<ProjectSession> CreateAsync(string manifestPath, ProjectDescription description, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(description);
        return Begin(manifestPath, description, cancellationToken);
    }

    private Task<ProjectSession> Begin(string path, ProjectDescription? description, CancellationToken caller)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        TaskCompletionSource<ProjectSession> completion;
        CancellationTokenSource pending;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            caller.ThrowIfCancellationRequested();
            if (_pending != null || _draining || _retired.Count != 0 || _active?.IsClosed == true)
                throw new InvalidOperationException("A project transition/cleanup is already active.");
            pending = CancellationTokenSource.CreateLinkedTokenSource(caller);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = pending; _work = completion.Task; _error = null;
        }
        _ = CompleteAsync(path, description, pending, completion);
        return completion.Task;
    }

    private async Task CompleteAsync(string path, ProjectDescription? description, CancellationTokenSource pending,
        TaskCompletionSource<ProjectSession> completion)
    {
        ProjectSession? candidate = null;
        ProjectSession? result = null;
        Exception? failure = null;
        var token = pending.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            var project = description == null
                ? await _store.ReadAsync(path, token).ConfigureAwait(false)
                : _store.Resolve(path, description);
            token.ThrowIfCancellationRequested();
            var sdk = await _toolchain.ValidateAsync(project.GameProjectPath, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            candidate = _sessions.Create(new(project, sdk)) ?? throw new InvalidOperationException("Project session factory returned null.");
            if (description != null)
            {
                token.ThrowIfCancellationRequested();
                await _store.WriteNewAsync(project, token).ConfigureAwait(false);
            }
            ProjectSession? previous;
            lock (_gate)
            {
                token.ThrowIfCancellationRequested();
                if (_closed || _draining) throw new OperationCanceledException(token);
                if (_active?.Documents is { } documents && !documents.TryFenceClean())
                    throw new InvalidOperationException("Save/discard the current scene and finish document IO before switching projects.");
                previous = _active; _active = result = candidate; candidate = null;
            }
            if (previous != null) await ReleaseAsync(previous).ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (candidate != null)
            {
                try { await ReleaseAsync(candidate).ConfigureAwait(false); }
                catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            }
            lock (_gate)
            {
                _pending = null;
                _error = failure;
                pending.Dispose();
            }
        }
        if (failure is OperationCanceledException) completion.TrySetCanceled(token);
        else if (failure != null) completion.TrySetException(failure);
        else completion.TrySetResult(result!);
    }

    /// <summary>Cancels/drains accepted preparation, then closes the active session. The coordinator can be reused.</summary>
    /// <returns>Actual drain completion; cancellation does not detach owned work.</returns>
    public Task CloseAsync()
    {
        TaskCompletionSource completion;
        Task work;
        Exception? cancellationFailure = null;
        lock (_gate)
        {
            if (_draining) return _close;
            if (!_closed && _active?.Documents?.Read().IsDirty == true)
                return Task.FromException(new InvalidOperationException("Save or explicitly discard the current scene before closing the project."));
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _close = completion.Task; _draining = true; work = _work;
            try { _pending?.Cancel(); } catch (Exception error) { cancellationFailure = error; }
        }
        _ = CompleteCloseAsync(work, cancellationFailure, completion);
        return completion.Task;
    }

    private async Task CompleteCloseAsync(Task work, Exception? cancellationFailure, TaskCompletionSource completion)
    {
        Exception? failure = cancellationFailure;
        try
        {
            try { await work.ConfigureAwait(false); } catch { /* Open/Create caller owns preparation failure. */ }
            ProjectSession[] retired;
            lock (_gate) retired = _retired.ToArray();
            foreach (var product in retired)
            {
                await product.DisposeAsync().ConfigureAwait(false);
                lock (_gate) _retired.Remove(product);
            }
            ProjectSession? previous;
            lock (_gate)
            {
                previous = _active;
                if (!_closed && previous?.Documents is { } documents && !documents.TryFenceClean())
                    throw new InvalidOperationException("Save/discard the current scene and finish document IO before closing the project.");
            }
            if (previous != null) await previous.DisposeAsync().ConfigureAwait(false);
            lock (_gate) _active = null;
        }
        catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
        finally { lock (_gate) { _draining = false; _error = failure; } }
        if (failure == null) completion.TrySetResult(); else completion.TrySetException(failure);
    }

    /// <summary>Closes admission permanently and drains work before releasing the active product.</summary>
    /// <returns>Shared asynchronous cleanup completion.</returns>
    public ValueTask DisposeAsync()
    {
        lock (_gate) _closed = true;
        return new(CloseAsync());
    }
    /// <summary>Completes the same owned cleanup synchronously. Prefer DisposeAsync during application shutdown.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    private async Task ReleaseAsync(ProjectSession product)
    {
        try { await product.DisposeAsync().ConfigureAwait(false); }
        catch { lock (_gate) _retired.Add(product); throw; }
    }
}
