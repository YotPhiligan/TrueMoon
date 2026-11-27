using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Kton;

/// <summary>Optional exact authoring schemas. Runtime handles, contact entities and teleport commands are not persisted.</summary>
public static class PhysicsComponentSchemas
{
    /// <summary>Stable singleton body schema identity.</summary>
    public static ComponentTypeId BodyId { get; } = new(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5a001"));
    /// <summary>Stable optional velocity schema identity.</summary>
    public static ComponentTypeId VelocityId { get; } = new(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5a002"));
    /// <summary>Stable optional character settings identity.</summary>
    public static ComponentTypeId CharacterId { get; } = new(new Guid("b5ec3c81-79a9-4a11-b052-9fe141d5a003"));
    /// <summary>Controller settings v1; requires an upright dynamic capsule body. Inputs and state are runtime data.</summary>
    public static ComponentSchema Character { get; } = new(CharacterId, 1,
        [Number("speed",6),Number("slope", MathF.PI / 4),Number("horizontalForce",1000),Number("verticalForce",1000),Number("jumpSpeed",5)], requiredTypes: [BodyId]);
    /// <summary>Controller settings v2; adds explicit automatic step settings with stepping disabled by default.</summary>
    public static ComponentSchema CharacterV2 { get; } = new(CharacterId, 2,
        [.. Character.Properties.Values, Number("maximumStepHeight", 0), Number("minimumTreadDepth", .30)], requiredTypes: [BodyId]);
    /// <summary>Body v1; requires the canonical Transform.</summary>
    public static ComponentSchema Body { get; } = new(BodyId, 1,
    [
        Integer("motion", 1), Integer("shape", 1), Number("x", 1), Number("y", 1), Number("z", 1), Number("mass", 1),
        Number("friction", .8), Integer("layer", 0), Integer("mask", uint.MaxValue),
        new("sensor", PropertyValueKind.Boolean, PropertyValue.From(false))
    ], requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Body v2; adds a collision asset ID for authored static triangle meshes.</summary>
    public static ComponentSchema BodyV2 { get; } = new(BodyId, 2,
        [.. Body.Properties.Values, new("meshAsset", PropertyValueKind.String, PropertyValue.Null, isNullable: true)],
        requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Optional initial velocities v1; absent means zero.</summary>
    public static ComponentSchema Velocity { get; } = new(VelocityId, 1,
        [Number("x",0), Number("y",0), Number("z",0), Number("ax",0), Number("ay",0), Number("az",0)], requiredTypes: [BodyId]);
    private static ComponentPropertySchema Number(string name, double value) => new(name, PropertyValueKind.Number, PropertyValue.From(value));
    private static ComponentPropertySchema Integer(string name, long value) => new(name, PropertyValueKind.Integer, PropertyValue.From(value));
}
