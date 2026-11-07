using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Tests.Ecs.Loading;

internal static class LoadFixture
{
    internal static readonly SceneId SceneId = new(Guid.Parse("a1000000-0000-0000-0000-000000000001"));
    internal static readonly ComponentTypeId Type = new(Guid.Parse("a1000000-0000-0000-0000-000000000002"));
    internal static readonly ComponentId Component = new(Guid.Parse("a1000000-0000-0000-0000-000000000003"));
    internal static readonly EntityDefinitionId Definition = new(Guid.Parse("a1000000-0000-0000-0000-000000000004"));
    internal static readonly EntityId First = Entity(1);
    internal static readonly EntityId Second = Entity(2);
    internal static EntityId Entity(int index) => new(new Guid(index, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1));
    internal static ComponentSchema Schema(params ComponentPropertySchema[] properties) => new(Type, 1, properties);
    internal static ComponentSchema IntegerSchema => Schema(new ComponentPropertySchema("value", PropertyValueKind.Integer, PropertyValue.From(7L)));
    internal static ComponentPropertySchema Property(string name, PropertyValue value, bool nullable = false)
        => new(name, value.Kind == PropertyValueKind.Null ? PropertyValueKind.String : value.Kind, value, nullable);
    internal static SceneDocument Scene(ComponentSchema schema, IEnumerable<KeyValuePair<string, PropertyValue>>? values = null, int count = 1)
        => new(SceneId, 13, Enumerable.Range(1, count).Select(index => new EntityInstance(Entity(index), Definition)),
            [new(Definition, new([new(Component, schema.TypeId, schema.Version, values)]), revision: 11)], [schema]);
    internal static SceneDocument Empty() => new(SceneId, 13, [], [], []);
    internal static SceneResolutionService Resolution() => new(new Factory<EntityResolver, SceneDocument>(document =>
        new(document.Definitions, document.Schemas)));
    internal static Factory<World> Worlds() => new(() => new World(new() { InitialEntityCapacity = 0 }));
    internal readonly record struct Value(int Number);
}
