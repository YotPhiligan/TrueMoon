using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Mods;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Owns replaceable Worlds and collectible groups. World references are borrowed until the next commit.</summary>
/// <remarks>Serialize direct World access with session operations. Never dispose the borrowed World or reenter from module callbacks.</remarks>
public sealed class ModWorldMigrationSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _operation = new(1, 1), _boundary = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly IFactory<ModGroup, ModLoadRequest> _groups;
    private readonly IFactory<SceneWorldLoader, ComponentRegistry> _loaders;
    private readonly SceneResolutionService _resolution;
    private readonly ModWorldSnapshotCodec _codec;
    private readonly List<ModGroup> _owners = [];
    private ModGroup? _current;
    private ModMigrationWorld? _world;
    private sealed record Publication(ModGroup? Group, ModMigrationWorld? World, long Number);
    private Publication _published = new(null, null, 0);
    private int _closed;
    private bool _resumeRequired;
    internal ModWorldMigrationSession(IFactory<ModGroup, ModLoadRequest> groups, IFactory<SceneWorldLoader, ComponentRegistry> loaders,
        SceneResolutionService resolution, ModWorldSnapshotCodec codec)
    { _groups = groups; _loaders = loaders; _resolution = resolution; _codec = codec; }
    /// <summary>Borrowed current storage. Its identity changes on every successful commit.</summary>
    public World? CurrentWorld => Volatile.Read(ref _published).World?.Scene?.World;
    /// <summary>Monotonic published generation.</summary>
    public long Generation => Volatile.Read(ref _published).Number;
    /// <summary>Whether all owners have been released.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Whether rollback resume requires an explicit retry before ticks.</summary>
    public bool ResumeRequired => Volatile.Read(ref _resumeRequired);
    /// <summary>Weak current collectible context observation.</summary>
    public ModUnloadTicket? CurrentUnloadTicket => Volatile.Read(ref _published).Group?.UnloadTicket;
    internal int OwnedGenerationCount { get { lock (_owners) return _owners.Count; } }
    /// <summary>Resolves a persistent identity into the current World's handle; old handles never transfer.</summary>
    public EntityHandle Entity(EntityId id) { _boundary.Wait(); try { Ready(); return _world!.Entity(id); } finally { _boundary.Release(); } }
    /// <summary>Admits a runtime-created entity into future snapshots.</summary>
    public void TrackEntity(EntityId id, EntityHandle entity, EntityId? parent = null)
    { _boundary.Wait(); try { Ready(); _world!.TrackEntity(id, entity, parent); } finally { _boundary.Release(); } }
    /// <summary>Admits a runtime-added component occurrence into future snapshots.</summary>
    public void TrackComponent(EntityId entity, ComponentTypeId type, ComponentId component)
    { _boundary.Wait(); try { Ready(); _world!.TrackComponent(entity, type, component); } finally { _boundary.Release(); } }
    /// <summary>Ticks the current scheduler, serialized against capture and publication.</summary>
    public bool Tick(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        if (deltaTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        cancellationToken.ThrowIfCancellationRequested(); _boundary.Wait();
        try
        {
            Ready();
            try { return _world!.Scheduler!.Tick(deltaTime, cancellationToken); }
            catch { _world!.World.SanitizeModuleFailure(); RequestStop(); throw new ModOperationException(new(ModLoadError.ActivationFailed, "System execution failed; stop the session.")); }
        }
        finally { _boundary.Release(); }
    }
    /// <summary>Loads initial data. Replacing an existing World requires ReplaceAsync or explicit ResetAsync.</summary>
    public Task<ModReloadResult> LoadAsync(ModLoadRequest request, ModWorldSnapshot seed, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(seed); return ChangeAsync(request, seed, null, false, cancellationToken); }
    /// <summary>Captures live data, migrates exact schemas, prepares a private World, then commits once.</summary>
    public Task<ModReloadResult> ReplaceAsync(ModLoadRequest request, ModWorldMigrationOptions? options = null, CancellationToken cancellationToken = default)
        => ChangeAsync(request, null, options, false, cancellationToken);
    /// <summary>Explicit destructive state reset. Denied admission never falls back to a reset.</summary>
    public Task<ModReloadResult> ResetAsync(ModLoadRequest request, ModWorldSnapshot seed, ModWorldResetAdmission admission, CancellationToken cancellationToken = default)
    { ArgumentNullException.ThrowIfNull(seed); return admission == ModWorldResetAdmission.Allow ? ChangeAsync(request, seed, null, true, cancellationToken) :
            Task.FromResult(Result(ModLoadError.ResetNotAdmitted, "Reset requires explicit host admission.")); }
    private async Task<ModReloadResult> ChangeAsync(ModLoadRequest request, ModWorldSnapshot? seed, ModWorldMigrationOptions? options, bool reset, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _closed) != 0) return Result(ModLoadError.AdmissionClosed, "Session is closed.");
        try { await _operation.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Result(ModLoadError.Cancelled, "Migration admission cancelled."); }
        ModGroup? candidate = null; var boundary = false; var paused = false; var committed = false;
        ModUnloadTicket? retiredTicket = null; var phase = ModLoadError.InvalidSnapshot;
        try
        {
            if (Volatile.Read(ref _closed) != 0 || _resumeRequired) return Result(ModLoadError.AdmissionClosed, "Session is closed or requires rollback recovery.");
            if (seed != null && _current != null && !reset || seed == null && _current == null || reset && _current == null)
                return Result(ModLoadError.AdmissionClosed, "Initial load, replacement and reset have distinct admission.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (seed != null) seed = _codec.Decode(_codec.Encode(seed)); // Validate bounded neutral data before module code.
            phase = ModLoadError.ActivationFailed;
            candidate = _groups.Create(request) ?? throw new InvalidOperationException("Group factory returned null.");
            lock (_owners) _owners.Add(candidate);
            var activation = await candidate.ActivateAsync(linked.Token, true, true).ConfigureAwait(false);
            if (activation.Error != ModLoadError.None) throw new ModFailure(activation.Error, activation.Message);
            if (_current != null && (_current.RootId != candidate.RootId || _current.ReloadGroup != candidate.ReloadGroup))
                throw new ModFailure(ModLoadError.DependencyConflict, "Candidate root/reload-group differs from the current generation.");
            await _boundary.WaitAsync(linked.Token).ConfigureAwait(false); boundary = true;
            if (_current != null) { paused = true; await _current.QuiesceReloadAsync(linked.Token).ConfigureAwait(false); }
            phase = ModLoadError.InvalidSnapshot;
            var source = seed ?? _codec.Decode(_codec.Encode(_current!.CaptureMigrationWorld(_world!)));
            phase = ModLoadError.MigrationFailed;
            ModMigrationWorld next;
            using (_world?.World.BeginReloadPreparation())
            {
                var migrated = candidate.MigrateSnapshot(source, _resolution, options ?? new());
                migrated = _codec.Decode(_codec.Encode(migrated));
                next = candidate.PrepareMigrationWorld(migrated, _loaders, seed == null, _world?.Scheduler);
                candidate.ReleaseMigrationConverters(); // Disposal failures reject before publication and remain owned for retry.
            }
            linked.Token.ThrowIfCancellationRequested();
            if (Generation == long.MaxValue) throw new ModFailure(ModLoadError.LimitExceeded, "Generation counter is exhausted.");
            var retired = _current; retiredTicket = retired?.UnloadTicket;
            var publication = new Publication(candidate, next, Generation + 1);
            next.Scheduler!.PublishReplacing(null);
            _world = next; Volatile.Write(ref _current, candidate); candidate = null;
            Volatile.Write(ref _published, publication); committed = true;
            retired?.RetireGeneration(); if (Volatile.Read(ref _closed) != 0) _current.CloseGeneration(); _current.PublishGeneration();
            _boundary.Release(); boundary = false; paused = false;
            return new(true, Generation, new(ModLoadError.None, "New World committed."), await CleanupOwnersAsync().ConfigureAwait(false), retiredTicket);
        }
        catch (Exception error)
        {
            if (committed) return new(true, Generation, new(ModLoadError.None, "New World committed."), new(ModLoadError.CleanupFailed, "Retirement requires retry."), retiredTicket);
            var diagnostic = error is ModFailure failure ? new ModDiagnostic(failure.Error, failure.Message) :
                error is OperationCanceledException && (cancellationToken.IsCancellationRequested || _stop.IsCancellationRequested) ?
                new(ModLoadError.Cancelled, "Migration cancelled before commit.") : new(phase, "World preparation rejected before commit.");
            if (paused && _current != null && Volatile.Read(ref _closed) == 0)
            {
                try { await _current.ResumeReloadAsync().ConfigureAwait(false); }
                catch { _resumeRequired = true; diagnostic = new(ModLoadError.RollbackFailed, $"Original outcome: {diagnostic.Error}; old generation resume requires retry."); }
            }
            if (boundary) { _boundary.Release(); boundary = false; }
            return new(false, Generation, diagnostic, await CleanupOwnersAsync().ConfigureAwait(false), candidate?.UnloadTicket);
        }
        finally { if (boundary) _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Captures a neutral bounded snapshot with callbacks quiesced; no World/type references escape.</summary>
    public async Task<ModWorldSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        await _operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var boundary = false; var paused = false;
        try
        {
            await _boundary.WaitAsync(linked.Token).ConfigureAwait(false); boundary = true; Ready();
            paused = true; await _current!.QuiesceReloadAsync(linked.Token).ConfigureAwait(false);
            return _codec.Decode(_codec.Encode(_current.CaptureMigrationWorld(_world!)));
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw new ModOperationException(new(ModLoadError.Cancelled, "World capture cancelled before completion.")); }
        catch (ModOperationException) { throw; }
        catch { throw new ModOperationException(new(ModLoadError.InvalidSnapshot, "Live World cannot be captured without data loss.")); }
        finally
        {
            try { if (paused && Volatile.Read(ref _closed) == 0) { try { await _current!.ResumeReloadAsync().ConfigureAwait(false); } catch { _resumeRequired = true; throw new ModOperationException(new(ModLoadError.RollbackFailed, "Snapshot complete or rejected; resume requires retry.")); } } }
            finally { if (boundary) _boundary.Release(); _operation.Release(); }
        }
    }
    /// <summary>Retries reversible resume after a rejected replacement or snapshot.</summary>
    public async Task<ModDiagnostic> ResumeAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false); await _boundary.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _closed) != 0) return new(ModLoadError.AdmissionClosed, "Session is closed.");
            if (!_resumeRequired) return new(ModLoadError.None, "No resume required.");
            try { await _current!.ResumeReloadAsync().ConfigureAwait(false); _resumeRequired = false; return new(ModLoadError.None, "Old generation resumed."); }
            catch { return new(ModLoadError.RollbackFailed, "Old generation resume requires retry."); }
        }
        finally { _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Retries candidate/retired cleanup without changing the current World.</summary>
    public async Task<ModDiagnostic> RetryCleanupAsync()
    { await _operation.WaitAsync().ConfigureAwait(false); try { return await CleanupOwnersAsync().ConfigureAwait(false); } finally { _operation.Release(); } }
    private async Task<ModDiagnostic> CleanupOwnersAsync(bool all = false)
    {
        ModGroup[] owners; lock (_owners) owners = _owners.ToArray(); var result = new ModDiagnostic(ModLoadError.None, "Eligible owners released.");
        foreach (var owner in owners)
        {
            if (!all && ReferenceEquals(owner, _current)) continue;
            ModDiagnostic status; try { status = await owner.UnloadAsync().ConfigureAwait(false); } catch { status = new(ModLoadError.CleanupFailed, "Generation cleanup requires retry."); }
            if (status.Error == ModLoadError.None) { lock (_owners) _owners.Remove(owner); } else result = status;
        }
        return result;
    }
    internal void RequestStop() { Interlocked.Exchange(ref _closed, 1); Volatile.Read(ref _current)?.CloseGeneration(); try { _stop.Cancel(); } catch { } }
    /// <summary>Closes admission and releases schedulers, owned Worlds, resources and contexts. Failed cleanup remains retryable.</summary>
    public async Task<ModDiagnostic> StopAsync()
    {
        RequestStop(); await _operation.WaitAsync().ConfigureAwait(false); await _boundary.WaitAsync().ConfigureAwait(false);
        try { var result = await CleanupOwnersAsync(true).ConfigureAwait(false); if (result.Error == ModLoadError.None) { _world = null; _current = null; Volatile.Write(ref _published, new(null, null, Generation)); IsDisposed = true; } return result; }
        finally { _boundary.Release(); _operation.Release(); }
    }
    /// <summary>Stops all owners or reports explicit retryable failure.</summary>
    public async ValueTask DisposeAsync() { var status = await StopAsync().ConfigureAwait(false); if (status.Error != ModLoadError.None) throw new ModOperationException(status); }
    private void Ready() { if (Volatile.Read(ref _closed) != 0 || _resumeRequired || _current == null) throw new ModOperationException(new(ModLoadError.AdmissionClosed, "No ready World; load or recover the session.")); }
    private ModReloadResult Result(ModLoadError error, string message) => new(false, Generation, new(error, message), new(ModLoadError.None, "No cleanup required."), null);
}
