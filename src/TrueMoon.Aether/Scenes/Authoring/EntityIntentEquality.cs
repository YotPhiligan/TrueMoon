namespace TrueMoon.Aether.Scenes.Authoring;

internal static class EntityIntentEquality
{
    internal static bool Same(EntityInstance left, EntityInstance right) => left.Id == right.Id && left.DefinitionId == right.DefinitionId &&
        left.ParentId == right.ParentId && left.Name == right.Name && left.SiblingOrder == right.SiblingOrder && Same(left.Changes, right.Changes);
    internal static bool Same(EntityDefinition left, EntityDefinition right) => left.Id == right.Id && left.BaseDefinitionId == right.BaseDefinitionId &&
        left.Revision == right.Revision && Same(left.Changes, right.Changes);
    internal static bool Same(ComponentSchema left, ComponentSchema right) => left.TypeId == right.TypeId && left.Version == right.Version &&
        left.AllowMultiple == right.AllowMultiple && left.RequiredTypes.SetEquals(right.RequiredTypes) && left.Properties.Count == right.Properties.Count &&
        left.Properties.All(pair => right.Properties.TryGetValue(pair.Key, out var value) && pair.Value.Kind == value.Kind &&
            pair.Value.IsNullable == value.IsNullable && pair.Value.DefaultValue == value.DefaultValue);
    private static bool Same(EntityPatch left, EntityPatch right) => left.RemovedComponents.SetEquals(right.RemovedComponents) &&
        left.RestoredComponents.SetEquals(right.RestoredComponents) && left.PropertyOverrides.Length == right.PropertyOverrides.Length &&
        left.PropertyOverrides.Zip(right.PropertyOverrides).All(pair => pair.First.ComponentId == pair.Second.ComponentId &&
            pair.First.PropertyId == pair.Second.PropertyId && pair.First.Value == pair.Second.Value) &&
        left.AddedComponents.Length == right.AddedComponents.Length && left.AddedComponents.Zip(right.AddedComponents).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.TypeId == pair.Second.TypeId && pair.First.SchemaVersion == pair.Second.SchemaVersion &&
            pair.First.Properties.Count == pair.Second.Properties.Count && pair.First.Properties.All(property =>
                pair.Second.Properties.TryGetValue(property.Key, out var value) && property.Value == value));
}
