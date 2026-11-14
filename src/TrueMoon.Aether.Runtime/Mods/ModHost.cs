namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>App-owned module admission and group owner, including failed unpublished startup cleanup.</summary>
public sealed class ModHost(IFactory<ModGroup, ModLoadRequest> groups) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<ModGroup> _owned = [];
    private int _closed;
    /// <summary>Counts active and failed groups still awaiting cleanup; unloaded groups are removed by the next host operation.</summary>
    public int OwnedGroupCount { get { lock (_owned) return _owned.Count(g => !g.UnloadTicket.UnloadRequested); } }

    /// <summary>Loads one dependency closure. Published results are borrowed: App/host retains ownership until unload succeeds.</summary>
    /// <param name="request">Complete immutable artifact closure.</param><param name="cancellationToken">Cooperative activation cancellation.</param><returns>Explicit status with optional owned group.</returns>
    public async Task<ModLoadResult> LoadAsync(ModLoadRequest request, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _closed) != 0) return Closed();
        try { await _gate.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return new(new(ModLoadError.Cancelled, "Module admission cancelled."), null); }
        try
        {
            if (Volatile.Read(ref _closed) != 0) return Closed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            ModGroup group;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                group = groups.Create(request) ?? throw new ModFailure(ModLoadError.ActivationFailed, "Group factory returned null.");
            }
            catch (OperationCanceledException) { return new(new(ModLoadError.Cancelled, "Module admission cancelled."), null); }
            catch (ModFailure error) { return new(new(error.Error, error.Message), null); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return new(new(ModLoadError.InvalidManifest, "Artifact files are missing or inaccessible."), null); }
            catch { return new(new(ModLoadError.InvalidManifest, "Module artifact preparation failed."), null); }
            lock (_owned) { _owned.RemoveAll(g => g.UnloadTicket.UnloadRequested); _owned.Add(group); }
            var diagnostic = await group.ActivateAsync(linked.Token).ConfigureAwait(false);
            if (diagnostic.Error == ModLoadError.None && !linked.IsCancellationRequested) return new(diagnostic, group);
            if (diagnostic.Error == ModLoadError.None) diagnostic = new(ModLoadError.Cancelled, "Module publication cancelled.");
            var cleanup = await group.UnloadAsync().ConfigureAwait(false);
            if (cleanup.Error != ModLoadError.None) return new(new(ModLoadError.CleanupFailed, $"Startup outcome: {diagnostic.Error}; cleanup requires retry."), group);
            lock (_owned) _owned.Remove(group);
            return new(diagnostic, group);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Closes admission before waiting for pending startup/ticks, then releases all owned groups. Retry is supported.</summary>
    /// <returns>Cleanup status. Failed owners remain held by this host.</returns>
    public async Task<ModDiagnostic> StopAsync()
    {
        Interlocked.Exchange(ref _closed, 1);
        try { _stop.Cancel(); } catch { /* Module cancellation callbacks cannot bypass cleanup. */ }
        ModGroup[] closing; lock (_owned) closing = _owned.ToArray();
        foreach (var group in closing) group.RequestStop();
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            ModGroup[] owned; lock (_owned) owned = _owned.ToArray();
            for (var i = owned.Length - 1; i >= 0; i--)
            {
                var status = await owned[i].UnloadAsync().ConfigureAwait(false);
                if (status.Error != ModLoadError.None) return status;
                lock (_owned) _owned.Remove(owned[i]);
            }
            return new(ModLoadError.None, "Module host stopped.");
        }
        finally { _gate.Release(); }
    }
    /// <summary>Stops all owned groups; failure remains explicit and retryable.</summary>
    public async ValueTask DisposeAsync()
    {
        var status = await StopAsync().ConfigureAwait(false);
        if (status.Error != ModLoadError.None) throw new ModOperationException(status);
    }
    private static ModLoadResult Closed() => new(new(ModLoadError.AdmissionClosed, "Module host admission is closed."), null);
}
