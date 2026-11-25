using TrueMoon.Aether.Ecs;
using TrueMoon.Services;

namespace TrueMoon.Aether.Runtime.Ecs;

/// <summary>Composition-time graph using injected independent or World-parameterized system factories.</summary>
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
    /// <summary>Registers a neutral system via IFactory&lt;T&gt; or an explicitly World-parameterized factory.</summary>
    /// <typeparam name="T">Owned neutral implementation.</typeparam><param name="description">Metadata copied now.</param><returns>This builder.</returns>
    public EcsSystemsBuilder Add<T>(Aether.Ecs.EcsSystemDescription description) where T : class, ISystem
        => Add<T>(description?.Freeze() ?? throw new ArgumentNullException(nameof(description)));
    /// <summary>Uses immutable metadata exported by an Abstractions-only library.</summary><typeparam name="T">Neutral implementation.</typeparam><param name="metadata">Frozen declarations.</param><returns>This builder.</returns>
    public EcsSystemsBuilder Add<T>(EcsSystemMetadata metadata) where T : class, ISystem
    {
        var plan = EcsSystemDescription.FromContract(metadata).Snapshot();
        if (_entries.Count == 1024) throw new InvalidOperationException("System registration budget exhausted.");
        if (_entries.Any(e => e.Plan.Id == plan.Id)) throw new ArgumentException("Duplicate system ID.", nameof(metadata));
        _entries.Add((plan, resolver =>
        {
            var withWorld = resolver.TryResolve<IFactory<T, World>>(); var independent = resolver.TryResolve<IFactory<T>>();
            if (withWorld != null && independent != null && !ReferenceEquals(withWorld, independent)) throw new InvalidOperationException($"Choose one IFactory<{typeof(T).Name}> creation profile.");
            if (withWorld != null) return EcsSystemRegistration.Wrap(withWorld);
            if (independent != null) return EcsSystemRegistration.Wrap(new Factory<T, World>(_ => independent.Create()!)); // Wrap rejects null.
            throw new InvalidOperationException($"Register IFactory<{typeof(T).Name}> before activating the scheduler factory.");
        }
        ));
        return this;
    }
    internal (SystemPlan Plan, Func<IServiceResolver, IFactory<IEcsSystem, World>> Resolve)[] Snapshot(EcsSchedulerOptions options)
    {
        if (_entries.Count > options.MaximumSystems) throw new ArgumentException("The configured system limit is exceeded.");
        var copy = _entries.ToArray(); SystemGraph.Order(copy.Select(e => e.Plan).ToArray()); return copy;
    }
}
