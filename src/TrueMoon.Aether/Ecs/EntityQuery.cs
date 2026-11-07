namespace TrueMoon.Aether.Ecs;

/// <summary>Reusable one-component query; iteration is synchronous and packed order is unspecified.</summary>
/// <typeparam name="T">Required component type.</typeparam>
public readonly struct EntityQuery<T> where T : struct
{
    private readonly World? _world;
    private readonly QueryExclusions? _exclusions;
    internal EntityQuery(World world, QueryExclusions? exclusions) { _world = world; _exclusions = exclusions; }
    private World Owner => _world ?? throw new InvalidOperationException("The default query descriptor is invalid.");

    /// <summary>Adds a distinct excluded type without modifying this descriptor.</summary>
    /// <typeparam name="TExcluded">Excluded type.</typeparam><returns>A filtered descriptor.</returns>
    public EntityQuery<T> Without<TExcluded>() where TExcluded : struct
    { var world = Owner; world.CheckQueryOwner(); return new(world, QueryExclusions.Add<TExcluded>(_exclusions, typeof(T), null)); }

    /// <summary>Visits each match with readonly access.</summary><param name="action">Synchronous callback.</param>
    public void ForEach(QueryReadAction<T> action) { ArgumentNullException.ThrowIfNull(action); Visit(action, null); }
    /// <summary>Visits each match with writable access. Structural mutations require deferred commands.</summary><param name="action">Synchronous callback.</param>
    public void ForEachWrite(QueryWriteAction<T> action) { ArgumentNullException.ThrowIfNull(action); Visit(null, action); }

    private void Visit(QueryReadAction<T>? read, QueryWriteAction<T>? write)
    {
        var world = Owner;
        using var scope = world.BeginQuery(typeof(T), null, write != null, false, _exclusions);
        var pool = world.FindPool<T>();
        if (pool == null) return;
        for (var i = 0; i < pool.Count; i++)
        {
            var index = pool.EntityAt(i);
            if (!scope.Includes(index)) continue;
            ref var value = ref pool.Reference(index);
            if (read != null) read(world.HandleAt(index), in value); else write!(world.HandleAt(index), ref value);
        }
    }
}

/// <summary>Reusable intersection query over two distinct required types.</summary>
/// <typeparam name="T1">First type.</typeparam><typeparam name="T2">Second type.</typeparam>
public readonly struct EntityQuery<T1, T2> where T1 : struct where T2 : struct
{
    private readonly World? _world;
    private readonly QueryExclusions? _exclusions;
    internal EntityQuery(World world, QueryExclusions? exclusions) { _world = world; _exclusions = exclusions; }
    private World Owner => _world ?? throw new InvalidOperationException("The default query descriptor is invalid.");

    /// <summary>Adds an excluded type, up to eight distinct types.</summary>
    /// <typeparam name="TExcluded">Excluded type.</typeparam><returns>A filtered descriptor.</returns>
    public EntityQuery<T1, T2> Without<TExcluded>() where TExcluded : struct
    { var world = Owner; world.CheckQueryOwner(); return new(world, QueryExclusions.Add<TExcluded>(_exclusions, typeof(T1), typeof(T2))); }

    /// <summary>Observes both components.</summary><param name="action">Synchronous callback.</param>
    public void ForEach(QueryReadAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Visit(action, null, null); }
    /// <summary>Writes the first component and observes the second.</summary><param name="action">Synchronous callback.</param>
    public void ForEachWrite(QueryWriteAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Visit(null, action, null); }
    /// <summary>Writes both components.</summary><param name="action">Synchronous callback.</param>
    public void ForEachWriteBoth(QueryWriteBothAction<T1, T2> action) { ArgumentNullException.ThrowIfNull(action); Visit(null, null, action); }

    private void Visit(QueryReadAction<T1, T2>? read, QueryWriteAction<T1, T2>? write, QueryWriteBothAction<T1, T2>? both)
    {
        var world = Owner;
        using var scope = world.BeginQuery(typeof(T1), typeof(T2), write != null || both != null, both != null, _exclusions);
        var first = world.FindPool<T1>(); var second = world.FindPool<T2>();
        if (first == null || second == null) return;
        IComponentPool candidates = first.Count <= second.Count ? first : second;
        for (var i = 0; i < candidates.Count; i++)
        {
            var index = candidates.EntityAt(i);
            if (!first.Contains(index) || !second.Contains(index) || !scope.Includes(index)) continue;
            ref var a = ref first.Reference(index); ref var b = ref second.Reference(index);
            var entity = world.HandleAt(index);
            if (read != null) read(entity, in a, in b);
            else if (write != null) write(entity, ref a, in b);
            else both!(entity, ref a, ref b);
        }
    }
}
