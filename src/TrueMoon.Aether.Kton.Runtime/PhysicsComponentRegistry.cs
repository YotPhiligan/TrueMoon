using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Exact scene data bindings; no physics world or backend resource is acquired by the registry.</summary>
public static class PhysicsComponentRegistry
{
    /// <summary>Adds the exact optional body, velocity and character schemas to the caller's existing registry.</summary>
    /// <param name="builder">Composition-time builder, normally containing primitive Transform bindings.</param><returns>The same builder.</returns>
    public static ComponentRegistryBuilder Register(ComponentRegistryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Register<PhysicsBody>(PhysicsComponentSchemas.Body, ReadBody, validate: context =>
        {
            if (context.SourceEntity.ParentId != null) throw new InvalidDataException("Physics v1 requires root entities.");
            _ = ReadBody(context).CaptureShape();
        });
        builder.Register<PhysicsVelocity>(PhysicsComponentSchemas.Velocity, context => ReadVelocity(context));
        builder.Register<PhysicsCharacter>(PhysicsComponentSchemas.Character, ReadCharacter);
        builder.Register<PhysicsCharacter>(PhysicsComponentSchemas.CharacterV2, ReadCharacter);
        return builder;
    }
    private static PhysicsCharacter ReadCharacter(ComponentValidationContext context)
    {
        var descriptor = new PhysicsCharacter
        {
            MaximumSpeed = context.GetSingle("speed"), MaximumSlopeRadians = context.GetSingle("slope"),
            MaximumHorizontalForce = context.GetSingle("horizontalForce"), MaximumVerticalForce = context.GetSingle("verticalForce"),
            JumpSpeed = context.GetSingle("jumpSpeed")
        };
        if (context.Schema.Version == 1) return descriptor;
        return descriptor with
        {
            MaximumStepHeight = context.GetSingle("maximumStepHeight"),
            MinimumTreadDepth = context.GetSingle("minimumTreadDepth")
        };
    }
    private static PhysicsBody ReadBody(ComponentValidationContext context) => new(
        (PhysicsBodyMotion)context.GetInt32("motion", 0, 2), (PhysicsColliderShape)context.GetInt32("shape", 1, 3),
        new(context.GetSingle("x"), context.GetSingle("y"), context.GetSingle("z")), context.GetSingle("mass"))
    {
        Material = new(context.GetSingle("friction", 0, 4)),
        Filter = new((byte)context.GetInt32("layer", 0, 31), checked((uint)context.GetInteger("mask"))),
        IsSensor = context.GetBoolean("sensor")
    };
    private static PhysicsVelocity ReadVelocity(ComponentValidationContext context) => new(
        new(context.GetSingle("x"), context.GetSingle("y"), context.GetSingle("z")),
        new(context.GetSingle("ax"), context.GetSingle("ay"), context.GetSingle("az")));
}
