using TrueMoon.Aether.Abstractions.Mods;
using TrueMoon.Aether.Abstractions.Scenes;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Host-owned atomic module closure, local factories and private Worlds. Never register its collectible Types in App DI.</summary>
/// <remarks>Owner calls must not reenter this group from module callbacks. Stop is cooperative and waits for active ticks.</remarks>
public sealed partial class ModGroup
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ModArtifactGroup? _artifacts;
    private ModLoadContext? _context;
    private ModRegistration? _registration = new();
    private IFactory<IModEntry, ModEntryArguments>? _entries;
    private IFactory<ModWorldSession>? _worlds;
    private ModHostServices? _host;
    private readonly List<Owned<IModEntry>> _modules = [];
    private readonly List<Owned<IModService>> _services = [];
    private readonly List<ModWorldSession> _sessions = [];
    private int _closed;
    internal ModGroup(ModArtifactGroup artifacts, IFactory<IModEntry, ModEntryArguments> entries, IFactory<ModWorldSession> worlds, ModHostServices host)
    {
        _artifacts = artifacts; _entries = entries; _worlds = worlds; _host = host;
        _context = new(artifacts); UnloadTicket = new(_context);
        ModuleIds = Array.AsReadOnly(artifacts.Ordered.Select(m => m.Id).ToArray()); ReloadGroup = artifacts.Ordered[^1].ReloadGroup;
    }
    /// <summary>Dependency-first module IDs. Contains no live module types.</summary>
    public IReadOnlyList<string> ModuleIds { get; }
    /// <summary>WholeGroup replacement boundary.</summary>
    public string ReloadGroup { get; }
    /// <summary>Weak collectible-context observation, safe to retain after disposal.</summary>
    public ModUnloadTicket UnloadTicket { get; }
    /// <summary>Whether admission/ticks have closed.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    internal Task<ModDiagnostic> ActivateAsync(CancellationToken token) => ActivateAsync(token, false);
    internal Task<ModDiagnostic> ActivateAsync(CancellationToken token, bool reload) => ActivateAsync(token, reload, false);
    internal async Task<ModDiagnostic> ActivateAsync(CancellationToken token, bool reload, bool migration)
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            _host!.BindGeneration(_generation);
            foreach (var module in _artifacts!.Ordered)
            {
                linked.Token.ThrowIfCancellationRequested();
                var type = _context!.Entry(module.EntryAssembly).GetType(module.EntryType, false, false)
                    ?? throw new ModFailure(ModLoadError.InvalidEntry, "Declared entry type is unavailable.");
                if (System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(type.Assembly) != _context)
                    throw new ModFailure(ModLoadError.InvalidEntry, "Entry type must belong to this module group.");
                var entry = _entries!.Create(new(type, _host!)) ?? throw new ModFailure(ModLoadError.InvalidEntry, "Entry factory returned null.");
                if (_modules.Any(o => ReferenceEquals(o.Value, entry))) throw new ModFailure(ModLoadError.InvalidEntry, "Entry factory returned an aliased result.");
                _modules.Add(new(entry)); entry.Register(_registration!);
            }
            _registration!.Seal(); _host!.Seal(); _host = null; _entries = null;
            if (reload) { if (migration) ValidateMigration(); else ValidateReload(); }
            foreach (var registered in _registration.Services)
            {
                linked.Token.ThrowIfCancellationRequested();
                var service = registered.Factory.Create() ?? throw new ModFailure(ModLoadError.ActivationFailed, "Module service factory returned null.");
                if (_services.Any(o => ReferenceEquals(o.Value, service)) || _modules.Any(o => ReferenceEquals(o.Value, service)))
                    throw new ModFailure(ModLoadError.ActivationFailed, "Module factory results must not alias.");
                _services.Add(new(service));
                if (reload && service is not IModReloadParticipant) throw new ModFailure(ModLoadError.ReloadNotSupported, "Services must support reversible quiesce/resume.");
                await service.StartAsync(linked.Token).ConfigureAwait(false);
            }
            foreach (var module in _modules) { linked.Token.ThrowIfCancellationRequested(); await module.Value!.StartAsync(linked.Token).ConfigureAwait(false); }
            linked.Token.ThrowIfCancellationRequested();
            if (!reload) _generation.Open();
            return new(ModLoadError.None, "Module group activated.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || _lifetime.IsCancellationRequested) { return new(ModLoadError.Cancelled, "Module startup cancelled."); }
        catch (ModFailure error) { return new(error.Error, error.Message); }
        catch { return new(ModLoadError.ActivationFailed, "Module factory, registration or startup failed."); }
        finally { _host?.Seal(); _host = null; _registration!.Seal(); _entries = null; _gate.Release(); }
    }

    /// <summary>Creates a private World with group-local component/system factories. The group retains ownership even on partial failure.</summary>
    /// <param name="scene">Neutral scene document.</param><param name="cancellationToken">Cooperative preparation cancellation.</param><returns>Borrowed session.</returns>
    public ModWorldSession CreateWorld(SceneDocument scene, CancellationToken cancellationToken = default) => Invoke(() =>
    {
        var session = _worlds!.Create() ?? throw new InvalidOperationException("Module World factory returned null.");
        _sessions.Add(session); session.Load(this, scene, _registration!, cancellationToken); return session;
    });

    internal T Invoke<T>(Func<T> operation)
    {
        if (IsClosed) throw new ModOperationException(new(ModLoadError.AdmissionClosed, "Module group is closed."));
        _gate.Wait();
        try
        {
            if (IsClosed) throw new ModOperationException(new(ModLoadError.AdmissionClosed, "Module group is closed."));
            try { return operation(); }
            catch (ModOperationException) { throw; }
            catch { Interlocked.Exchange(ref _closed, 1); throw new ModOperationException(new(ModLoadError.ActivationFailed, "Module World creation or execution failed; unload the group.")); }
        }
        finally { _gate.Release(); }
    }

    // Safe to call concurrently with admission or a tick; actual cleanup is serialized after callbacks leave.
    internal void RequestStop() { Interlocked.Exchange(ref _closed, 1); _generation.Close(); try { _lifetime.Cancel(); } catch { /* User cancellation callbacks cannot bypass drain. */ } }

    /// <summary>Closes admission, drains module work, disposes systems before Worlds, clears roots and requests collectible unload.</summary>
    /// <remarks>On failure the host retains this group; call again after fixing the failure. No forced abort or forced GC occurs.</remarks>
    /// <returns>Explicit cleanup status. Successful Unload request is distinct from collection.</returns>
    public async Task<ModDiagnostic> UnloadAsync()
    {
        RequestStop(); await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (UnloadTicket.UnloadRequested) return new(ModLoadError.None, "Unload already requested.");
            try
            {
                await _generation.QuiesceAsync(CancellationToken.None).ConfigureAwait(false);
                _generation.Retire();
                // Successful stop is remembered. Failed stop is retried before dependent resources are disposed.
                for (var i = _modules.Count - 1; i >= 0; i--)
                    if (!_modules[i].Stopped) { await _modules[i].Value!.StopAsync(CancellationToken.None).ConfigureAwait(false); _modules[i].Stopped = true; }
                for (var i = _services.Count - 1; i >= 0; i--)
                    if (!_services[i].Stopped) { await _services[i].Value!.StopAsync(CancellationToken.None).ConfigureAwait(false); _services[i].Stopped = true; }
                for (var i = _reloadSchedulers.Count - 1; i >= 0; i--) _reloadSchedulers[i].Dispose();
                _reloadSchedulers.Clear();
                ReleaseMigrationConverters();
                for (var i = _migrationWorlds.Count - 1; i >= 0; i--) _migrationWorlds[i].Dispose();
                _migrationWorlds.Clear();
                for (var i = _sessions.Count - 1; i >= 0; i--) _sessions[i].DisposeCore();
                _sessions.Clear();
                for (var i = _services.Count - 1; i >= 0; i--)
                {
                    var owned = _services[i]; if (owned.Value == null) continue;
                    await owned.Value.DisposeAsync().ConfigureAwait(false); owned.Value = null;
                }
                for (var i = _modules.Count - 1; i >= 0; i--)
                {
                    var owned = _modules[i]; if (owned.Value == null) continue;
                    await owned.Value.DisposeAsync().ConfigureAwait(false); owned.Value = null;
                }
            }
            catch { return new(ModLoadError.CleanupFailed, "Module cleanup failed; group remains owned for explicit retry."); }
            _services.Clear(); _modules.Clear(); _worlds = null;
            _registration!.Clear(); _registration = null;
            _context!.Unload(); _context = null;
            _artifacts!.Dispose(); _artifacts = null;
            _lifetime.Dispose(); UnloadTicket.UnloadRequested = true;
            return new(ModLoadError.None, "Module roots released and collectible unload requested.");
        }
        finally { _gate.Release(); }
    }
    private sealed class Owned<T>(T value) where T : class { internal T? Value = value; internal bool Stopped, Quiesced; }
}
