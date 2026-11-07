namespace TrueMoon.Aether.Ecs;

public sealed partial class World
{
    private bool _activeQuery;
    private SystemPlan? _systemPlan;
    private object? _schedulerOwner;
    private bool _activatingSystems;
    private Exception? _failure;

    /// <summary>Gets whether an execution failure requires disposal/recreation before further mutations.</summary>
    /// <remarks>Inspection reads remain available; no partial execution is silently resumed.</remarks>
    public bool IsFaulted => _failure != null;

    /// <summary>Describes a synchronous query over one component type. No pool is registered by lookup.</summary>
    /// <typeparam name="T">Supported value component type.</typeparam><returns>A reusable query descriptor.</returns>
    public EntityQuery<T> Query<T>() where T : struct
    { ThrowIfDisposed(); ValidateComponent<T>(); return new(this, null); }

    /// <summary>Describes a query requiring two distinct component types.</summary>
    /// <typeparam name="T1">First component type.</typeparam><typeparam name="T2">Second component type.</typeparam>
    /// <returns>A reusable query descriptor. Packed iteration order is unspecified.</returns>
    public EntityQuery<T1, T2> Query<T1, T2>() where T1 : struct where T2 : struct
    {
        ThrowIfDisposed(); ValidateComponent<T1>(); ValidateComponent<T2>();
        if (typeof(T1) == typeof(T2)) throw new ArgumentException("Required query types must be distinct.");
        return new(this, null);
    }

    internal void EnsureRead(Type type)
    {
        ThrowIfDisposed();
        if (_systemPlan != null && !_systemPlan.CanRead(type))
            throw new InvalidOperationException($"System '{_systemPlan.Id}' did not declare read access to {type.Name}.");
    }

    private void EnsureHealthy()
    {
        ThrowIfDisposed();
        if (_failure != null) throw new InvalidOperationException("The World is faulted; dispose and recreate it before further mutations.", _failure);
    }

    private void EnsureMutation(Type? type, bool structural)
    {
        EnsureHealthy();
        if (_sceneLoading && !_sceneWrite) throw new InvalidOperationException("Scene preparation cannot mutate unpublished World data.");
        if (_activatingSystems) throw new InvalidOperationException("System factories cannot mutate World during graph activation.");
        if (_activeQuery) throw new InvalidOperationException("Use query write access or deferred commands during iteration.");
        if (_systemPlan != null)
        {
            if (structural) throw new InvalidOperationException("Systems must record structural changes in their phase command buffer.");
            EnsureWrite(type!);
        }
    }

    private void EnsureWrite(Type type)
    {
        EnsureHealthy();
        if (_sceneLoading && !_sceneWrite) throw new InvalidOperationException("Scene preparation cannot write unpublished World data.");
        if (_activatingSystems) throw new InvalidOperationException("System factories cannot write World during graph activation.");
        if (_systemPlan != null && !_systemPlan.Writes.Contains(type))
            throw new InvalidOperationException($"System '{_systemPlan.Id}' did not declare write access to {type.Name}.");
    }

    internal QueryScope BeginQuery(Type first, Type? second, bool writeFirst, bool writeSecond, QueryExclusions? exclusions)
    {
        ThrowIfDisposed();
        if (_activeQuery) throw new InvalidOperationException("Nested queries are not supported.");
        if (writeFirst) EnsureWrite(first); else EnsureRead(first);
        if (second != null) { if (writeSecond) EnsureWrite(second); else EnsureRead(second); }
        if (exclusions != null) foreach (var type in exclusions.Types) EnsureRead(type);
        var scope = new QueryScope(this, exclusions);
        _activeQuery = true;
        return scope;
    }

    internal void EndQuery() => _activeQuery = false;
    internal IComponentPool? FindPool(Type type) => _pools.GetValueOrDefault(type);
    internal EntityHandle HandleAt(int index) => new(Id, index, _slots[index].Generation);
    internal void CheckQueryOwner() => ThrowIfDisposed();

    internal void CheckCommandRecording(Type? type)
    { EnsureHealthy(); if (type != null) EnsureWrite(type); }

    internal void CheckPlayback()
    {
        EnsureHealthy();
        if (_activeQuery || _systemPlan != null || _activatingSystems || _sceneLoading) throw new InvalidOperationException("Playback requires an idle published phase boundary.");
    }

    internal void MarkFaulted(Exception failure) => _failure ??= failure;

    internal void ClaimScheduler(object owner)
    {
        CheckPlayback();
        if (_schedulerOwner != null) throw new InvalidOperationException("The World already has a scheduler owner.");
        _schedulerOwner = owner;
        _activatingSystems = true;
    }

    internal void CompleteActivation(object owner)
    { if (ReferenceEquals(_schedulerOwner, owner)) _activatingSystems = false; }

    internal void ReleaseScheduler(object owner)
    { if (ReferenceEquals(_schedulerOwner, owner)) { _schedulerOwner = null; _activatingSystems = false; } }

    internal void BeginSystem(object owner, SystemPlan plan)
    {
        CheckPlayback();
        if (!ReferenceEquals(_schedulerOwner, owner)) throw new InvalidOperationException("Scheduler ownership mismatch.");
        _systemPlan = plan;
    }
    internal void EndSystem() => _systemPlan = null;
}
