using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.ManualTests.ContractExtension;

public readonly record struct ExtensionCounter(int Value) : IComponent;
// Existing unmarked reference-free component structs remain valid.
public readonly record struct UnmarkedVelocity(int Value);
public readonly record struct Disabled;

public sealed class ExtensionSystem : IEcsSystem
{
    public static ComponentSchema ValueSchema => new(CounterSchema.TypeId, CounterSchema.Version,
        [new("value", PropertyValueKind.Integer, PropertyValue.From(0L))]);
    public static EcsSystemMetadata Describe() => new EcsSystemDescription("extension.counter")
        .Write<ExtensionCounter>().Read<UnmarkedVelocity>().Read<Disabled>().Freeze();
    public static ComponentSchemaReference CounterSchema => new(new ComponentTypeId(new("47a88cb9-70af-4f50-90df-9296f2ee26aa")), 1);
    public int Executions { get; private set; }
    public int Stops { get; private set; }
    public int Disposals { get; private set; }
    public ulong LastTick { get; private set; }
    public void Execute(EcsSystemContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Query<ExtensionCounter, UnmarkedVelocity>().Without<Disabled>().ForEachWrite(
            (EntityHandle entity, ref ExtensionCounter counter, in UnmarkedVelocity velocity) => counter = new(counter.Value + velocity.Value + 1));
        var reservation = context.Commands.CreateEntity();
        context.Commands.AddComponent(reservation, new ExtensionCounter(41));
        LastTick = context.TickNumber; Executions++;
    }
    public void Stop() { if (Disposals != 0) throw new InvalidOperationException("Stop after Dispose."); Stops++; }
    public void Dispose() { if (Stops != 1) throw new InvalidOperationException("Dispose before Stop."); Disposals++; }
}

public sealed class CounterFactory : TrueMoon.IFactory<ExtensionCounter, ComponentData>
{
    public ExtensionCounter Create(ComponentData data) => new(checked((int)data.Properties["value"].GetInteger()));
}

public sealed class ObserverSystem : IEcsSystem
{
    public static EcsSystemMetadata Describe() => new EcsSystemDescription("extension.observer", EcsPhase.Extraction).Read<ExtensionCounter>().After("extension.counter").Freeze();
    public long Total { get; private set; }
    public int Count { get; private set; }
    public int Stops { get; private set; }
    public int Disposals { get; private set; }
    public Guid WorldId { get; private set; }
    public void Execute(EcsSystemContext context)
    {
        Total = 0; Count = 0; WorldId = context.WorldId;
        context.Query<ExtensionCounter>().ForEach((EntityHandle entity, in ExtensionCounter value) => { Total += value.Value; Count++; });
    }
    public void Stop() => Stops++;
    public void Dispose() => Disposals++;
}
