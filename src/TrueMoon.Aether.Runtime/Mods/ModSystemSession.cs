using TrueMoon.Aether.Runtime.Ecs;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Host-owned system generations borrowing one caller-owned stable-component World.</summary>
/// <remarks>Do not mutate/dispose the borrowed World concurrently, or reenter the session from module callbacks.
/// Dispose/stop the session successfully before disposing World. No World migration or automatic state reset occurs.</remarks>
public sealed class ModSystemSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _operation = new(1, 1), _boundary = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly IFactory<ModGroup, ModLoadRequest> _groups;
    private readonly World _world;
    private readonly List<ModGroup> _owners = [];
    private ModGroup? _current;
    private EcsScheduler? _scheduler;
    private long _generation;
    private int _closed;
    private bool _resumeRequired;
    internal ModSystemSession(World world, IFactory<ModGroup, ModLoadRequest> groups)
    { ArgumentNullException.ThrowIfNull(world); world.CheckPlayback(); _world = world; _groups = groups; WorldId = world.Id; }
    /// <summary>Unchanged borrowed World identity.</summary>
    public Guid WorldId { get; }
    /// <summary>Published generation, monotonically increasing after successful commit.</summary>
    public long Generation => Interlocked.Read(ref _generation);
    /// <summary>Whether all generation owners have been released.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Whether an abandoned replacement requires explicit rollback recovery before further ticks.</summary>
    public bool ResumeRequired => Volatile.Read(ref _resumeRequired);
    /// <summary>Weak current context observation; contains no collectible Types.</summary>
    public ModUnloadTicket? CurrentUnloadTicket => Volatile.Read(ref _current)?.UnloadTicket;
    internal int OwnedGenerationCount { get { lock (_owners) return _owners.Count; } }
    /// <summary>Runs a fixed step on the current graph, serialized against capture/commit. Preparation outside the boundary permits old ticks.</summary>
    /// <param name="deltaTime">Positive fixed step.</param><param name="cancellationToken">Cooperative execution cancellation.</param><returns>Complete tick.</returns>
    public bool Tick(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        if (deltaTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOpen(); _boundary.Wait();
        try
        {
            EnsureOpen();
            if (_resumeRequired || _scheduler == null) throw new ModOperationException(new(ModLoadError.AdmissionClosed, "No ready generation; recover rollback or load an initial group."));
            try { return _scheduler.Tick(deltaTime, cancellationToken); }
            catch { _world.SanitizeModuleFailure(); RequestStop(); throw new ModOperationException(new(ModLoadError.ActivationFailed, "System execution failed; stop the session.")); }
        }
        finally { _boundary.Release(); }
    }
    /// <summary>Prepares an independent group, quiesces/captures the old graph, restores private systems and commits once between ticks.</summary>
    /// <param name="request">Complete candidate closure with matching root/reload-group identity.</param><param name="cancellationToken">Observed only before commit.</param>
    /// <returns>Separate commit/preparation and cleanup outcomes. Retired failures never roll back a committed generation.</returns>
    public async Task<ModReloadResult> ReplaceAsync(ModLoadRequest request, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _closed) != 0) return Result(false, ModLoadError.AdmissionClosed, "Session is closed.");
        try { await _operation.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Result(false, ModLoadError.Cancelled, "Replacement admission cancelled."); }
        ModGroup? candidate = null; var boundary = false; var paused = false; var committed = false;
        ModUnloadTicket? retiredTicket = null; var phase = ModLoadError.ActivationFailed;
        try
        {
            if (Volatile.Read(ref _closed) != 0 || _resumeRequired) return Result(false, ModLoadError.AdmissionClosed, "Session is closed or requires rollback recovery.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            linked.Token.ThrowIfCancellationRequested();
            candidate = _groups.Create(request) ?? throw new ModFailure(ModLoadError.ActivationFailed, "Group factory returned null."); lock (_owners) _owners.Add(candidate);
            var activation = await candidate.ActivateAsync(linked.Token, true).ConfigureAwait(false);
            if (activation.Error != ModLoadError.None) throw new ModFailure(activation.Error, activation.Message);
            if (_current != null && (_current.RootId != candidate.RootId || _current.ReloadGroup != candidate.ReloadGroup))
                throw new ModFailure(ModLoadError.DependencyConflict, "Candidate root/reload-group differs from the current generation.");
            await _boundary.WaitAsync(linked.Token).ConfigureAwait(false); boundary = true;
            linked.Token.ThrowIfCancellationRequested();
            var scheduler = candidate.PrepareSystems(_world, _scheduler); scheduler.RejectAliases(_scheduler);
            if (_current != null)
            {
                paused = true; await _current.QuiesceReloadAsync(linked.Token).ConfigureAwait(false);
                phase = ModLoadError.StateRejected;
                var state = _scheduler!.CaptureReloadState();
                scheduler.RestoreReloadState(state);
            }
            linked.Token.ThrowIfCancellationRequested();
            var retired = _current; retiredTicket = retired?.UnloadTicket;
            if (Generation == long.MaxValue) throw new ModFailure(ModLoadError.LimitExceeded, "Generation counter is exhausted.");
            scheduler.PublishReplacing(_scheduler);
            _scheduler = scheduler; Volatile.Write(ref _current, candidate); candidate = null; Interlocked.Increment(ref _generation); committed = true;
            retired?.RetireGeneration(); if (Volatile.Read(ref _closed) != 0) _current.CloseGeneration(); _current.PublishGeneration(); // No module code at commit.
            _boundary.Release(); boundary = false; paused = false;
            var cleanup = await CleanupOwnersAsync().ConfigureAwait(false);
            return new(true, Generation, new(ModLoadError.None, "Generation committed."), cleanup, retiredTicket);
        }
        catch (Exception error)
        {
            if (committed)
            {
                if (boundary) { _boundary.Release(); boundary = false; }
                return new(true, Generation, new(ModLoadError.None, "Generation committed."), new(ModLoadError.CleanupFailed, "Post-commit retirement requires retry."), retiredTicket);
            }
            var diagnostic = error is ModFailure failure ? new ModDiagnostic(failure.Error, failure.Message) :
                error is OperationCanceledException && (cancellationToken.IsCancellationRequested || _stop.IsCancellationRequested) ?
                new(ModLoadError.Cancelled, "Replacement cancelled before commit.") : new(phase, "Candidate preparation or state transfer failed before commit.");
            if (paused && _current != null && Volatile.Read(ref _closed) == 0)
            {
                try { await _current.ResumeReloadAsync().ConfigureAwait(false); }
                catch { _resumeRequired = true; diagnostic = new(ModLoadError.RollbackFailed, $"Original outcome: {diagnostic.Error}; old generation resume requires retry."); }
            }
            if (boundary) { _boundary.Release(); boundary = false; }
            var ticket = candidate?.UnloadTicket;
            var cleanup = await CleanupOwnersAsync().ConfigureAwait(false);
            return new(false, Generation, diagnostic, cleanup, ticket);
        }
        finally { if (boundary) _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Retries a failed old-generation resume without recreating systems or losing state.</summary><returns>Explicit recovery status.</returns>
    public async Task<ModDiagnostic> ResumeAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        await _boundary.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _closed) != 0) return new(ModLoadError.AdmissionClosed, "Session is closed.");
            if (!_resumeRequired) return new(ModLoadError.None, "No rollback recovery required.");
            try { await _current!.ResumeReloadAsync().ConfigureAwait(false); _resumeRequired = false; return new(ModLoadError.None, "Old generation resumed."); }
            catch { return new(ModLoadError.RollbackFailed, "Old generation resume still requires retry."); }
        }
        finally { _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Retries failed candidate/retired cleanup while leaving the current graph intact.</summary><returns>Cleanup outcome.</returns>
    public async Task<ModDiagnostic> RetryCleanupAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try { return await CleanupOwnersAsync().ConfigureAwait(false); }
        finally { _operation.Release(); }
    }
    private async Task<ModDiagnostic> CleanupOwnersAsync(bool all = false)
    {
        ModGroup[] owners; lock (_owners) owners = _owners.ToArray();
        var result = new ModDiagnostic(ModLoadError.None, "All eligible owners released.");
        foreach (var owner in owners)
        {
            if (!all && ReferenceEquals(owner, _current)) continue;
            ModDiagnostic status;
            try { status = await owner.UnloadAsync().ConfigureAwait(false); }
            catch { status = new(ModLoadError.CleanupFailed, "Generation cleanup requires retry."); }
            if (status.Error == ModLoadError.None) { lock (_owners) _owners.Remove(owner); }
            else result = status;
        }
        return result;
    }
    internal void RequestStop() { Interlocked.Exchange(ref _closed, 1); Volatile.Read(ref _current)?.CloseGeneration(); try { _stop.Cancel(); } catch { } }
    /// <summary>Closes admission, drains active replacement/tick/callbacks, releases all generations and leaves World caller-owned.</summary><returns>Retryable cleanup outcome.</returns>
    public async Task<ModDiagnostic> StopAsync()
    {
        RequestStop(); await _operation.WaitAsync().ConfigureAwait(false); await _boundary.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await CleanupOwnersAsync(true).ConfigureAwait(false);
            if (result.Error == ModLoadError.None) { _current = null; _scheduler = null; IsDisposed = true; }
            return result;
        }
        finally { _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Completes cleanup or reports the retained failed owner.</summary>
    public async ValueTask DisposeAsync() { var status = await StopAsync().ConfigureAwait(false); if (status.Error != ModLoadError.None) throw new ModOperationException(status); }
    private void EnsureOpen() { if (Volatile.Read(ref _closed) != 0) throw new ModOperationException(new(ModLoadError.AdmissionClosed, "Session is closed.")); }
    private ModReloadResult Result(bool committed, ModLoadError error, string message) => new(committed, Generation, new(error, message), new(ModLoadError.None, "No cleanup required."), null);
}
