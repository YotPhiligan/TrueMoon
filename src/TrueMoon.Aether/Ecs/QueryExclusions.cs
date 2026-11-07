using System.Buffers;

namespace TrueMoon.Aether.Ecs;

internal sealed class QueryExclusions(Type[] types)
{
    internal Type[] Types { get; } = types;
    internal static QueryExclusions Add<T>(QueryExclusions? current, Type first, Type? second) where T : struct
    {
        World.ValidateComponent<T>();
        var type = typeof(T);
        if (type == first || type == second) throw new ArgumentException("A required component cannot also be excluded.");
        if (current?.Types.Contains(type) == true) return current;
        if (current?.Types.Length == 8) throw new InvalidOperationException("Queries support at most eight excluded types.");
        return new([.. current?.Types ?? [], type]);
    }
}

internal readonly struct QueryScope : IDisposable
{
    private readonly World _world;
    private readonly IComponentPool?[]? _excluded;
    private readonly int _count;
    internal QueryScope(World world, QueryExclusions? exclusions)
    {
        _world = world; _count = exclusions?.Types.Length ?? 0;
        _excluded = _count == 0 ? null : ArrayPool<IComponentPool?>.Shared.Rent(_count);
        for (var i = 0; i < _count; i++) _excluded![i] = world.FindPool(exclusions!.Types[i]);
    }
    internal bool Includes(int index)
    {
        for (var i = 0; i < _count; i++) if (_excluded![i]?.Contains(index) == true) return false;
        return true;
    }
    public void Dispose()
    {
        try { if (_excluded != null) ArrayPool<IComponentPool?>.Shared.Return(_excluded, clearArray: true); }
        finally { _world.EndQuery(); }
    }
}
