namespace TrueMoon.Aether.Rendering.Resources;

/// <summary>Current catalog ownership counters, without IO/native queries.</summary>
/// <param name="RevisionCount">Exact keys.</param><param name="ResidentBytes">Distinct retained physical definition buffers.</param>
/// <param name="ScopeCount">Frozen scope bundles.</param><param name="LeaseCount">Exclusive lease wrappers.</param><param name="AdmissionClosed">New acquisitions are rejected.</param>
public sealed record RenderCatalogCounts(int RevisionCount, long ResidentBytes, int ScopeCount, int LeaseCount, bool AdmissionClosed);

/// <summary>Thread-safe counted CPU content owner. No latest-version lookup or GPU activation.</summary>
public sealed class RenderContentCatalog : IDisposable, IFactory<RenderManifestLease, RenderContentManifest>
{
    private readonly Lock _gate = new();
    private readonly RenderResourceOptions _options;
    private readonly Dictionary<RenderAssetKey, Entry> _entries = [];
    private readonly Dictionary<RenderAssetDefinition, int> _physical = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ScopeKey, Scope> _scopes = [];
    private long _bytes;
    private int _leases;
    private bool _closed;
    /// <summary>Creates a CPU owner with copied limits.</summary><param name="options">Optional lower admission limits.</param>
    public RenderContentCatalog(RenderResourceOptions? options = null) => _options = (options ?? new()).Snapshot();
    /// <summary>Gets the unique owner identity.</summary>
    public Guid Id { get; } = Guid.NewGuid();
    /// <summary>Gets true only after every lease and retained scope has released.</summary>
    public bool IsReleased { get; private set; }
    /// <summary>Gets a copied counter snapshot.</summary>
    public RenderCatalogCounts Counts { get { lock (_gate) return new(_entries.Count, _bytes, _scopes.Count, _leases, _closed); } }
    /// <summary>Acquires an exclusive lease to a frozen CPU bundle.</summary><param name="arguments">Validated manifest.</param><returns>Caller-owned lease.</returns>
    public RenderManifestLease Create(RenderContentManifest arguments) => Acquire(arguments);
    /// <summary>Admits the complete closure atomically, reusing exact keys and physical buffers.</summary><param name="manifest">Source immutable data.</param><returns>Caller-owned lease; dispose before catalog cleanup.</returns>
    public RenderManifestLease Acquire(RenderContentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        lock (_gate)
        {
            EnsureOpen(); CheckLeaseCapacity();
            var identity = new ScopeKey(manifest.ProjectId, manifest.BuildId, manifest.Generation, manifest.Digest);
            if (_scopes.TryGetValue(identity, out var existing)) return Lease(existing);
            ResourceGuard.Require(_scopes.Count < _options.MaximumCpuScopes, RenderResourceError.LimitExceeded, "CPU scope admission exhausted.");
            var selected = new List<RenderAssetEntry>(manifest.Entries.Length);
            var added = new List<RenderAssetEntry>();
            var newPhysical = new HashSet<RenderAssetDefinition>(ReferenceEqualityComparer.Instance);
            long additionalBytes = 0;
            foreach (var item in manifest.Entries)
            {
                if (_entries.TryGetValue(item.Key, out var current)) selected.Add(current.Value);
                else
                {
                    selected.Add(item); added.Add(item);
                    if (!_physical.ContainsKey(item.Definition) && newPhysical.Add(item.Definition)) additionalBytes = checked(additionalBytes + RenderResourceAccounting.ResidentBytes(item.Definition));
                }
            }
            ResourceGuard.Require(_entries.Count + added.Count <= _options.MaximumCpuRevisions && additionalBytes <= _options.MaximumCpuBytes - _bytes,
                RenderResourceError.LimitExceeded, "CPU revision/buffer peak admission exhausted.");
            // All potentially fallible data validation/copying precedes ownership publication.
            var canonical = new RenderContentManifest(manifest.ProjectId, manifest.BuildId, manifest.Generation, selected);
            var scope = new Scope(identity, canonical, selected.Select(static entry => entry.Key).ToArray());
            foreach (var item in added)
            {
                _entries.Add(item.Key, new(item));
                if (_physical.TryGetValue(item.Definition, out var refs)) _physical[item.Definition] = refs + 1;
                else { _physical.Add(item.Definition, 1); _bytes += RenderResourceAccounting.ResidentBytes(item.Definition); }
            }
            foreach (var key in scope.Keys) _entries[key].Scopes++;
            _scopes.Add(identity, scope);
            return Lease(scope);
        }
    }
    internal RenderManifestLease Retain(RenderManifestLease lease)
    {
        lock (_gate) { EnsureOpen(); lease.CheckLive(); ResourceGuard.Require(ReferenceEquals(lease.Owner, this), RenderResourceError.InvalidIdentity, "Foreign CPU lease."); CheckLeaseCapacity(); return Lease(lease.Bundle); }
    }
    private RenderManifestLease Lease(Scope scope) { scope.Leases++; _leases++; return new(this, scope); }
    private void CheckLeaseCapacity() => ResourceGuard.Require(_leases < _options.MaximumCpuLeases, RenderResourceError.LimitExceeded, "CPU lease admission exhausted.");
    internal void EnsureOpen() { ResourceGuard.Require(!Volatile.Read(ref _closed), RenderResourceError.Closed, "CPU content admission is closed."); }
    internal void CloseAdmission() { lock (_gate) _closed = true; }
    internal void Release(Scope scope)
    {
        lock (_gate)
        {
            _leases--; if (--scope.Leases != 0) return;
            _scopes.Remove(scope.Identity);
            foreach (var key in scope.Keys)
            {
                var entry = _entries[key]; if (--entry.Scopes != 0) continue;
                _entries.Remove(key);
                var definition = entry.Value.Definition;
                if (_physical[definition] == 1) { _physical.Remove(definition); _bytes -= RenderResourceAccounting.ResidentBytes(definition); }
                else _physical[definition]--;
            }
        }
    }
    /// <summary>Closes admission. Live leases retain ownership and cause a retryable failure.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (IsReleased) return; _closed = true;
            if (_leases != 0) throw new InvalidOperationException("Release scene/preparation CPU leases before disposing the catalog.");
            IsReleased = true;
        }
    }
    internal readonly record struct ScopeKey(Guid Project, RenderContentHash Build, Guid Generation, RenderContentHash Digest);
    internal sealed class Scope(ScopeKey identity, RenderContentManifest manifest, RenderAssetKey[] keys)
    { internal ScopeKey Identity { get; } = identity; internal RenderContentManifest Manifest { get; } = manifest; internal RenderAssetKey[] Keys { get; } = keys; internal int Leases; }
    private sealed class Entry(RenderAssetEntry value) { internal RenderAssetEntry Value { get; } = value; internal int Scopes; }
}

/// <summary>Exclusive CPU bundle lease. Copying the reference does not acquire another lease.</summary>
public sealed class RenderManifestLease : IDisposable
{
    private int _released;
    internal RenderContentCatalog Owner { get; }
    internal RenderContentCatalog.Scope Bundle { get; }
    internal RenderManifestLease(RenderContentCatalog owner, RenderContentCatalog.Scope scope) { Owner = owner; Bundle = scope; }
    /// <summary>Gets the owner identity.</summary>
    public Guid CatalogId => Owner.Id;
    /// <summary>Gets whether this wrapper released its ownership.</summary>
    public bool IsReleased => Volatile.Read(ref _released) != 0;
    /// <summary>Gets immutable CPU data while this lease is live; retaining data alone grants no GPU use.</summary>
    public RenderContentManifest Manifest { get { CheckLive(); return Bundle.Manifest; } }
    internal void CheckLive() { ObjectDisposedException.ThrowIf(IsReleased, this); }
    internal void CheckAdmission() { CheckLive(); Owner.EnsureOpen(); }
    internal RenderManifestLease Retain() => Owner.Retain(this);
    /// <summary>Releases this lease once; other leases and data users are unaffected.</summary>
    public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) Owner.Release(Bundle); }
}
