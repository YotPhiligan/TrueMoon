using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Active import scope: immutable data, resolved handles and tracked owned leases, without mutable World.</summary>
public sealed class ComponentLoadContext : ComponentValidationContext
{
    private readonly SceneWorld _owner;
    private readonly EntityHandle _entity;
    private readonly EntityHandle? _parent;
    internal ComponentLoadContext(SceneWorld owner, ResolvedEntity entity, ResolvedComponent component, IComponentBinding binding, CancellationToken token)
        : base(owner.Scene, entity, component, binding, token)
    { _owner = owner; _entity = owner.Entities[entity.Id]; _parent = entity.ParentId is { } parent ? owner.Entities[parent] : null; }
    /// <summary>Gets this entity's private reservation, valid in the resulting World after publication.</summary>
    public EntityHandle Entity { get { CheckActive(); return _entity; } }
    /// <summary>Gets the scene parent's reserved handle, independently of template inheritance.</summary>
    public EntityHandle? Parent { get { CheckActive(); return _parent; } }
    /// <summary>Resolves a declared reference, including forward/self references; explicit null returns null.</summary>
    /// <param name="propertyId">Declared entity reference key.</param><returns>Reserved handle or null.</returns>
    public EntityHandle? GetEntityReference(string propertyId)
    { var id = ReferenceId(propertyId, ComponentReferenceKind.Entity); return id is { } value ? _owner.Entities[new(value)] : null; }
    /// <summary>Reads a declared asset ID already checked by the registry asset validator.</summary>
    /// <param name="propertyId">Declared asset reference key.</param><returns>Persistent asset ID or null.</returns>
    public Guid? GetAssetReference(string propertyId) => ReferenceId(propertyId, ComponentReferenceKind.Asset);
    /// <summary>Acquires an exclusively owned IDisposable lease from an injected provider/factory.</summary>
    /// <typeparam name="T">Reference lease type.</typeparam><param name="acquire">Acquisition callback with cooperative cancellation.</param><returns>The tracked lease.</returns>
    /// <remarks>Budget is checked before acquisition. Already-owned references are rejected without double disposal.</remarks>
    public T Acquire<T>(Func<CancellationToken, T> acquire) where T : class, IDisposable
    {
        CheckActive(); ArgumentNullException.ThrowIfNull(acquire); CancellationToken.ThrowIfCancellationRequested();
        if (!_owner.ReserveResource()) throw Error(SceneWorldLoadError.LimitExceeded, "Prepared resource budget exhausted.");
        try
        {
            var resource = acquire(CancellationToken);
            if (resource == null) throw Error(SceneWorldLoadError.ImportFailed, "Resource acquisition returned null.");
            if (!_owner.Track(resource)) throw Error(SceneWorldLoadError.ImportFailed, "A resource lease was returned twice without exclusive ownership.");
            CancellationToken.ThrowIfCancellationRequested(); return resource;
        }
        finally { _owner.ReleaseResourceReservation(); }
    }
}
