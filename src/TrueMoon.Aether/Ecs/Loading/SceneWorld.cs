using System.Collections.Immutable;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Caller-owned published World, initial persistent identity map and exclusively owned prepared leases.</summary>
public sealed class SceneWorld : IDisposable
{
    private World? _world;
    private bool _claimed, _worldReleased, _disposing;
    private readonly IDisposable?[] _resources;
    private readonly HashSet<IDisposable> _known;
    private int _resourceSlots, _remaining, _reservations;
    internal SceneWorld(ResolvedSceneSnapshot scene, int resources)
    { Scene = scene; _resources = new IDisposable?[resources]; _known = new(resources, ReferenceEqualityComparer.Instance); }
    /// <summary>Gets immutable resolved metadata, including schema/definition revisions and hierarchy.</summary>
    public ResolvedSceneSnapshot Scene { get; }
    /// <summary>Gets the borrowed World. Dispose this owner, not World directly.</summary>
    public World World => _world ?? throw new InvalidOperationException("No World was attached.");
    /// <summary>Gets initial persistent-ID mappings. Later runtime destruction can make a handle stale.</summary>
    public ImmutableDictionary<EntityId, EntityHandle> Entities { get; private set; } = ImmutableDictionary<EntityId, EntityHandle>.Empty;
    /// <summary>Gets whether complete data was published and disposal has not begun.</summary>
    public bool IsReady { get; private set; }
    /// <summary>Gets whether World and every owned resource have been released.</summary>
    public bool IsDisposed { get; private set; }
    /// <summary>Gets unreleased prepared leases, including failures retained for cleanup retry.</summary>
    public int PreparedResourceCount => _remaining;
    internal bool ReserveResource()
    {
        if (_resourceSlots + _reservations >= _resources.Length) return false;
        _reservations++; return true;
    }
    internal void ReleaseResourceReservation() => _reservations--;
    internal void Attach(World world) { _world = world; world.ClaimScene(this); _claimed = true; }
    internal void SetEntities(ImmutableDictionary<EntityId, EntityHandle> entities) => Entities = entities;
    internal bool Track(IDisposable resource)
    {
        if (!_known.Add(resource)) return false;
        _resources[_resourceSlots++] = resource; _remaining++; return true;
    }
    internal void Publish() { World.PublishScene(this); IsReady = true; }
    /// <summary>Invalidates World first, then releases leases in reverse order. Failed leases stay owned for explicit retry.</summary>
    /// <remarks>Dispose an active scheduler before this owner. Rejected preconditions preserve resources and ownership.</remarks>
    public void Dispose()
    {
        if (IsDisposed) return;
        if (_disposing) throw new InvalidOperationException("SceneWorld disposal is not reentrant.");
        _disposing = true;
        try
        {
            if (_claimed && !_worldReleased) { World.DisposeScene(this); _worldReleased = true; }
            IsReady = false;
            List<Exception>? errors = null;
            for (var i = _resourceSlots - 1; i >= 0; i--)
            {
                var resource = _resources[i]; if (resource == null) continue;
                try { resource.Dispose(); _resources[i] = null; _known.Remove(resource); _remaining--; }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
            if (_remaining == 0) { _known.Clear(); IsDisposed = true; }
            if (errors != null) throw new AggregateException("Prepared scene resource cleanup failed; retry Dispose.", errors);
        }
        finally { _disposing = false; }
    }
}
