using TrueMoon.Services;

namespace TrueMoon.Aether.Ecs;

/// <summary>Composition-time system graph. Each type consumes an existing injected IFactory&lt;T,World&gt;.</summary>
public sealed class EcsSystemsBuilder
{
    private readonly List<(SystemPlan Plan, Func<IServiceResolver, IFactory<IEcsSystem, World>> Resolve)> _entries = [];
    /// <summary>Registers a system type without creating an instance. Metadata is copied immediately.</summary>
    /// <typeparam name="T">Owned system implementation.</typeparam><param name="description">Access/order metadata.</param><returns>This builder.</returns>
    public EcsSystemsBuilder Add<T>(EcsSystemDescription description) where T : class, IEcsSystem
    {
        ArgumentNullException.ThrowIfNull(description);
        if (_entries.Count == 1024) throw new InvalidOperationException("System registration budget exhausted.");
        if (_entries.Any(e => e.Plan.Id == description.Id)) throw new ArgumentException("Duplicate system ID.", nameof(description));
        _entries.Add((description.Snapshot(), resolver => resolver.TryResolve<IFactory<T, World>>() ??
            throw new InvalidOperationException($"Register IFactory<{typeof(T).Name},World> before activating the scheduler factory.")));
        return this;
    }
    internal (SystemPlan Plan, Func<IServiceResolver, IFactory<IEcsSystem, World>> Resolve)[] Snapshot(EcsSchedulerOptions options)
    {
        if (_entries.Count > options.MaximumSystems) throw new ArgumentException("The configured system limit is exceeded.");
        var copy = _entries.ToArray(); SystemGraph.Order(copy.Select(e => e.Plan).ToArray()); return copy;
    }
}
