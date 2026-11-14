namespace TrueMoon.Aether.Runtime.Ecs;

/// <summary>Frozen metadata and a borrowed factory for exclusively owned system instances.</summary>
public sealed class EcsSystemRegistration
{
    /// <summary>Copies the description without creating a system. The factory must return a fresh owned instance per call.</summary>
    /// <param name="description">Composition-time metadata.</param><param name="factory">Borrowed system factory, never disposed by scheduler.</param>
    public EcsSystemRegistration(EcsSystemDescription description, IFactory<IEcsSystem, World> factory)
    {
        ArgumentNullException.ThrowIfNull(description); ArgumentNullException.ThrowIfNull(factory);
        Plan = description.Snapshot(); Factory = factory;
    }
    /// <summary>Registers a neutral system factory with explicit borrowed World creation arguments.</summary>
    /// <param name="description">Copied neutral access/order metadata.</param><param name="factory">Borrowed factory; its fresh result belongs to the scheduler.</param>
    public EcsSystemRegistration(Abstractions.Ecs.EcsSystemDescription description, IFactory<Abstractions.Ecs.ISystem, World> factory)
        : this(EcsSystemDescription.FromContract(description), Wrap(factory)) { }
    /// <summary>Registers an independent neutral extension that requires no Runtime creation arguments.</summary>
    /// <param name="description">Copied neutral metadata.</param><param name="factory">Borrowed DI factory, never disposed by the scheduler.</param>
    public EcsSystemRegistration(Abstractions.Ecs.EcsSystemDescription description, IFactory<Abstractions.Ecs.ISystem> factory)
        : this(description, WithWorld(factory)) { }
    /// <summary>Consumes immutable metadata exported by an independent extension.</summary><param name="metadata">Frozen access/order declarations.</param><param name="factory">Borrowed factory for a caller-owned result.</param>
    public EcsSystemRegistration(Abstractions.Ecs.EcsSystemMetadata metadata, IFactory<Abstractions.Ecs.ISystem> factory)
        : this(EcsSystemDescription.FromContract(metadata), Wrap(WithWorld(factory))) { }
    /// <summary>Consumes immutable neutral metadata with explicit World creation arguments.</summary><param name="metadata">Frozen declarations.</param><param name="factory">Borrowed runtime factory.</param>
    public EcsSystemRegistration(Abstractions.Ecs.EcsSystemMetadata metadata, IFactory<Abstractions.Ecs.ISystem, World> factory)
        : this(EcsSystemDescription.FromContract(metadata), Wrap(factory)) { }
    private static IFactory<Abstractions.Ecs.ISystem, World> WithWorld(IFactory<Abstractions.Ecs.ISystem> factory)
    { ArgumentNullException.ThrowIfNull(factory); return new Factory<Abstractions.Ecs.ISystem, World>(_ => factory.Create()!); } // Wrap rejects a null result.
    internal static IFactory<IEcsSystem, World> Wrap(IFactory<Abstractions.Ecs.ISystem, World> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new Factory<IEcsSystem, World>(world => new EcsContractSystem(factory.Create(world) ?? throw new InvalidOperationException("A neutral system factory returned null."), world));
    }
    private EcsSystemRegistration(SystemPlan plan, IFactory<IEcsSystem, World> factory) { Plan = plan; Factory = factory; }
    internal static EcsSystemRegistration FromPlan(SystemPlan plan, IFactory<IEcsSystem, World> factory) => new(plan, factory);
    internal SystemPlan Plan { get; }
    internal IFactory<IEcsSystem, World> Factory { get; }
}
