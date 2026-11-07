namespace TrueMoon.Aether.Ecs;

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
    private EcsSystemRegistration(SystemPlan plan, IFactory<IEcsSystem, World> factory) { Plan = plan; Factory = factory; }
    internal static EcsSystemRegistration FromPlan(SystemPlan plan, IFactory<IEcsSystem, World> factory) => new(plan, factory);
    internal SystemPlan Plan { get; }
    internal IFactory<IEcsSystem, World> Factory { get; }
}
