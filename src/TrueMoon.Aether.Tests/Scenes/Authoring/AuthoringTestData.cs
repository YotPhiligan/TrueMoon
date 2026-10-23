using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Tests.Scenes.Authoring;

internal static class AuthoringTestData
{
    internal static EntityId First => Entity(1);
    internal static EntityId Second => Entity(2);
    internal static EntityDefinitionId Definition => new(Id(10));
    internal static ComponentId Transform => new(Id(100));
    internal static ComponentId Appearance => new(Id(101));
    internal static ComponentId Interaction => new(Id(102));
    internal static ComponentId Custom => new(Id(103));
    internal static ComponentTypeId CustomType => new(Id(200));
    internal static Guid Id(int value) => new(value, 0, 0, new byte[8]);
    internal static EntityId Entity(int value) => new(Id(value));
    internal static AuthoringOperation Operation(long revision, Guid? id = null, string actor = "headless-test") => new(id ?? Guid.NewGuid(), revision, actor);
    internal static TransformEdit Edit(EntityId entity, double x) => new(entity, Transform, new(x, 2, 3));
    internal static TransformEditBatch Batch(long revision, double x, Guid? id = null) => new(Operation(revision, id), [Edit(First, x)]);
    internal static SceneDocument Document(long revision = 7, int count = 2, bool parented = true)
    {
        var definition = new EntityDefinition(Definition, new(addedComponents:
        [new(Transform, BuiltInComponents.TransformId, 1), new(Appearance, BuiltInComponents.AppearanceId, 1), new(Interaction, BuiltInComponents.InteractionId, 1)]));
        var first = new EntityPatch(addedComponents: [new(Custom, CustomType, 1, [new("note", PropertyValue.From("keep"))])],
            removedComponents: [Interaction], propertyOverrides:
            [new(Transform, "x", PropertyValue.From(3d)), new(Appearance, "g", PropertyValue.From(.25d))]);
        var entities = Enumerable.Range(1, count).Select(index => new EntityInstance(Entity(index), Definition, index == 1 ? first : EntityPatch.Empty,
            parented && index > 1 ? First : null));
        return new(new SceneId(Id(500)), revision, entities, [definition],
            [.. BuiltInComponents.Schemas, new(CustomType, 1, [new("note", PropertyValueKind.String, PropertyValue.From(""))])]);
    }
    internal static double Number(SceneAuthoringState state, EntityId entity, string key)
        => state.Resolved.Entities[entity].Components[Transform].Properties[key].Value.GetNumber();
    internal static SceneResolutionService Resolution(Action<SceneDocument>? beforeCreate = null)
        => new(new ResolverFactory(beforeCreate));
    internal static ISceneAuthoringSession Session(SceneDocument? document = null, SceneAuthoringOptions? options = null, Action<SceneDocument>? beforeCreate = null)
        => new SceneAuthoringSessionFactory(Resolution(beforeCreate), options ?? new()).Create(document ?? Document());

    internal sealed class ResolverFactory(Action<SceneDocument>? beforeCreate = null) : IFactory<EntityResolver, SceneDocument>, IDisposable
    {
        internal int Calls, Disposals;
        public EntityResolver Create(SceneDocument document)
        {
            Calls++; beforeCreate?.Invoke(document);
            return new(document.Definitions, document.Schemas);
        }
        public void Dispose() => Disposals++;
    }
}
