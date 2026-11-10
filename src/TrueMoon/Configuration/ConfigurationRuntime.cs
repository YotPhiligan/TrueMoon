namespace TrueMoon.Configuration;

// Only this DI singleton is disposable. All public aliases borrow it.
internal sealed class ConfigurationRuntime(ConfigurationStage stage, BoundConfigurationSnapshot snapshot) : IDisposable, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConfigurationNotifications _notifications = new();
    private BoundConfigurationSnapshot? _current = snapshot;
    private bool _stopped;
    private Task? _stop;

    internal IConfigurationSnapshot Current => Volatile.Read(ref _current)
        ?? throw new ObjectDisposedException(nameof(ConfigurationRuntime));

    private void Active()
    {
        ObjectDisposedException.ThrowIf(_current is null, this);
        if (_stopped) throw new InvalidOperationException("The App configuration runtime has stopped.");
    }

    internal IDisposable Subscribe(Action<ConfigurationUpdate> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_sync) { Active(); return _notifications.Subscribe(observer); }
    }

    internal async ValueTask<ConfigurationUpdate> ReloadAsync(CancellationToken token)
    {
        lock (_sync) Active();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            lock (_sync) Active();
            var candidate = await stage.ReloadAsync(linked.Token).ConfigureAwait(false);
            var current = Volatile.Read(ref _current) ?? throw new ObjectDisposedException(nameof(ConfigurationRuntime));
            var changed = stage.Keys.Where((_, index) => !current.Observations[index].Same(candidate.Observations[index]))
                .OrderBy(key => key.Path, StringComparer.Ordinal).ToArray();
            var restart = changed.Any(key => key.Field.Change == ConfigurationChangeMode.StartupOnly);
            lock (_sync)
            {
                Active();
                linked.Token.ThrowIfCancellationRequested();
                if (changed.Length == 0 || restart)
                    return new(current.Generation, false, restart, changed.Select(key => key.Path).ToArray());
                long generation;
                try { generation = checked(current.Generation + 1); }
                catch (OverflowException) { throw ConfigurationMetadata.Error("GenerationOverflow"); }
                var update = new ConfigurationUpdate(generation, true, false, changed.Select(key => key.Path).ToArray());
                Volatile.Write(ref _current, candidate.WithGeneration(generation));
                _notifications.Publish(update);
                return update;
            }
        }
        finally { _gate.Release(); }
    }

    internal async ValueTask WriteAsync(string sourceId, IReadOnlyList<ConfigurationEdit> edits, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(sourceId);
        var patch = ConfigurationPatches.Copy(edits);
        lock (_sync) Active();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            lock (_sync) Active();
            await stage.WriteAsync(sourceId, patch, linked.Token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    internal Task StopAsync(CancellationToken token = default)
    {
        Task stop;
        lock (_sync)
        {
            _stopped = true;
            stop = _stop ??= DrainAsync(_notifications.Close());
        }
        // Never run cancellation callbacks under the publication lock.
        try { _shutdown.Cancel(); }
        catch (Exception) { /* A source's cancellation callback cannot prevent notification cleanup. */ }
        return token.CanBeCanceled ? stop.WaitAsync(token) : stop;
    }

    private async Task DrainAsync(Task notifications)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        _gate.Release();
        await notifications.ConfigureAwait(false);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
    public async ValueTask DisposeAsync()
    {
        lock (_sync) Volatile.Write(ref _current, null);
        await StopAsync().ConfigureAwait(false);
        // Do not dispose synchronization primitives while callers can still hold borrowed aliases.
    }
}
