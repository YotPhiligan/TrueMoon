using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Tests.Ecs.Rendering;

internal static class PrimitivePipelineFixture
{
    internal static readonly SceneId SceneId = new(Id(1));
    internal static readonly EntityDefinitionId Definition = new(Id(2));
    internal static readonly ComponentId Transform = new(Id(3));
    internal static readonly ComponentId Appearance = new(Id(4));
    internal static readonly ComponentId Interaction = new(Id(5));
    internal static readonly ComponentId Velocity = new(Id(6));
    internal static EntityId Entity(int index) => new(Id(100 + index));
    internal static Guid Id(int value) => new($"dab35eaf-71cf-4265-9f0e-{value:000000000000}");
    internal static KeyValuePair<string, PropertyValue> Number(string key, double value) => new(key, PropertyValue.From(value));
    internal static SceneResolutionService Resolution() => new(new Factory<EntityResolver, SceneDocument>(source => new(source.Definitions, source.Schemas)));
    internal static ComponentRegistry Registry() => PrimitiveEcsComponents.Register(new()).Build();
    internal static SceneWorldLoader Loader(ComponentRegistry? registry = null, IFactory<World>? worlds = null)
        => new(registry ?? Registry(), worlds ?? new Factory<World>(() => new()), Resolution());
    internal static SceneDocument Document(int count = 1, bool appearance = true, bool velocity = false,
        IEnumerable<KeyValuePair<string, PropertyValue>>? transform = null,
        IEnumerable<KeyValuePair<string, PropertyValue>>? color = null)
    {
        var components = new List<ComponentData> { new(Transform, BuiltInComponents.TransformId, 1, transform) };
        if (appearance) components.Add(new(Appearance, BuiltInComponents.AppearanceId, 1, color));
        if (velocity) components.Add(new(Velocity, PrimitiveEcsComponents.VelocityId, 1, [Number("x", 6)]));
        return new(SceneId, 23, Enumerable.Range(1, count).Select(index => new EntityInstance(Entity(index), Definition)),
            [new(Definition, new(components))], BuiltInComponents.Schemas.Concat([PrimitiveEcsComponents.VelocitySchema]));
    }
    internal static void Near(Vector3 expected, Vector3 actual)
    {
        Xunit.Assert.InRange(Vector3.Distance(expected, actual), 0, 0.0001f);
    }
}
