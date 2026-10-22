using TrueMoon.Aether.Scenes;

namespace EngineSessionProbe;

/// <summary>Authoring-only fixture: room, three differently colored cubes and a transformed parent.</summary>
internal static class RoomScene
{
    internal static SceneSnapshot Snapshot(string variant = "normal")
    {
        if (variant is not ("normal" or "occluded" or "disabled")) throw new ArgumentOutOfRangeException(nameof(variant));
        var definitions = new List<EntityDefinition>(); var instances = new List<EntityInstance>();
        var transform = new ComponentId(Id(101)); var appearance = new ComponentId(Id(102));
        var interaction = new ComponentId(Id(103));
        var box = new EntityDefinitionId(Id(201));
        definitions.Add(new(box, new(addedComponents:
        [new(transform, BuiltInComponents.TransformId, 1), new(appearance, BuiltInComponents.AppearanceId, 1)]), revision: 1));
        void Add(int id, double x, double y, double z, double sx, double sy, double sz, double r, double g, double b,
            EntityId? parent = null)
        {
            KeyValuePair<string, PropertyValue> P(string key, double value) => new(key, PropertyValue.From(value));
            instances.Add(new(new(Id(id)), box, new(propertyOverrides:
                new[] { P("x", x), P("y", y), P("z", z), P("sx", sx), P("sy", sy), P("sz", sz) }
                    .Select(p => new ComponentPropertyOverride(transform, p.Key, p.Value))
                    .Concat(new[] { P("r", r), P("g", g), P("b", b) }.Select(p => new ComponentPropertyOverride(appearance, p.Key, p.Value)))), parent));
        }
        Add(1, -1.5, 1, -1, 1, 1, 1, .8, .2, .15);
        Add(2, 1.5, 1, -1, 1, 1, 1, .2, .8, .3);
        var group = new EntityId(Id(3)); var groupDefinition = new EntityDefinitionId(Id(202));
        definitions.Add(new(groupDefinition, new(addedComponents:
            [new(transform, BuiltInComponents.TransformId, 1, new Dictionary<string, PropertyValue>
            { ["yaw"] = PropertyValue.From(Math.PI / 2), ["sx"] = PropertyValue.From(2d) })]), revision: 1));
        instances.Add(new(group, groupDefinition));
        Add(4, 1, 1, 0, .5, 1, 1, .15, .4, .9, group); // world center (0,1,-2)
        Add(5, 0, -.1, -1, 8, .2, 10, .35, .4, .45);
        Add(6, 0, 1.5, -5, 8, 3, .2, .45, .45, .5);
        Add(7, -4, 1.5, -1, .2, 3, 8, .5, .4, .35);
        Add(8, 4, 1.5, -1, .2, 3, 8, .35, .45, .4);
        if (variant == "occluded") Add(9, 0, 1, -3.5, 1, 1, 1, 1, 1, 1);
        var eligible = variant == "normal" ? new[] { 1, 2, 4 } : variant == "occluded" ? [1, 2, 9] : new[] { 1, 2 };
        var eligibleIds = eligible.Select(value => new EntityId(Id(value))).ToHashSet();
        for (var i = 0; i < instances.Count; i++)
        {
            var current = instances[i]; if (!eligibleIds.Contains(current.Id)) continue;
            instances[i] = new(current.Id, current.DefinitionId, new(addedComponents:
                [new(interaction, BuiltInComponents.InteractionId, 1)], propertyOverrides: current.Changes.PropertyOverrides), current.ParentId);
        }
        var revision = variant == "normal" ? 7 : variant == "occluded" ? 8 : 9;
        return PrimitiveSceneAdapter.Project(SceneResolver.Resolve(new(new(Id(301)), revision, instances, definitions, BuiltInComponents.Schemas)));
    }
    private static Guid Id(int value) => new(value, 0, 0, new byte[8]);
}
