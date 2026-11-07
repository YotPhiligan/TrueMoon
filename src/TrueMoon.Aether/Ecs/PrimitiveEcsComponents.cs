using System.Numerics;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs;

/// <summary>Explicit runtime bindings for existing built-in primitive roles and optional parent-space velocity.</summary>
public static class PrimitiveEcsComponents
{
    /// <summary>Stable optional v1 translation velocity identity. Existing BuiltInComponents.Schemas remains unchanged.</summary>
    public static ComponentTypeId VelocityId { get; } = new(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1004"));
    /// <summary>Exact optional velocity schema, requiring the existing v1 Transform.</summary>
    public static ComponentSchema VelocitySchema { get; } = new(VelocityId, 1,
        [Number("x"), Number("y"), Number("z")], requiredTypes: [BuiltInComponents.TransformId]);
    /// <summary>Registers exact v1 primitive bindings in a composition-time builder. No World/resources are created.</summary>
    /// <param name="builder">Builder to extend; discard it if a registration fails.</param><returns>The same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Register<LocalTransform>(Schema(BuiltInComponents.TransformId), Local,
            validate: context => Local(context).CreateMatrix());
        builder.Register<PrimitiveAppearance>(Schema(BuiltInComponents.AppearanceId),
            context => Appearance(context), validate: context => Appearance(context).Validate());
        builder.Register<InteractionEnabled>(Schema(BuiltInComponents.InteractionId), context => new(context.GetBoolean("enabled")));
        builder.Register<LinearVelocity>(VelocitySchema, context => Velocity(context), validate: context => Velocity(context).Validate());
        return builder;
    }
    private static LocalTransform Local(ComponentValidationContext context) => new(
        new(context.GetSingle("x"), context.GetSingle("y"), context.GetSingle("z")),
        Quaternion.CreateFromYawPitchRoll(context.GetSingle("yaw"), context.GetSingle("pitch"), context.GetSingle("roll")),
        new(context.GetSingle("sx", float.Epsilon), context.GetSingle("sy", float.Epsilon), context.GetSingle("sz", float.Epsilon)));
    private static PrimitiveAppearance Appearance(ComponentValidationContext context)
        => new(new(context.GetSingle("r", 0, 1), context.GetSingle("g", 0, 1), context.GetSingle("b", 0, 1)));
    private static LinearVelocity Velocity(ComponentValidationContext context) => new(new(context.GetSingle("x"), context.GetSingle("y"), context.GetSingle("z")));
    private static ComponentSchema Schema(ComponentTypeId id) => BuiltInComponents.Schemas.Single(schema => schema.TypeId == id);
    private static ComponentPropertySchema Number(string id) => new(id, PropertyValueKind.Number, PropertyValue.From(0d));
}
