using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Motion;
using TrueMoon.Aether.Ecs.Transforms;

namespace TrueMoon.Aether.Kton;

/// <summary>Atomic phase-batch composition for complete runtime physics roles; no service or resource creation.</summary>
public static class PhysicsEntityCommands
{
    /// <summary>Declares component writes used by these helpers; ordinary scheduler validation still applies.</summary>
    /// <param name="description">Game system metadata.</param><returns>The same description.</returns>
    public static EcsSystemDescription DeclareWrites(EcsSystemDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return description.Write<PhysicsBody>().Write<PhysicsVelocity>().Write<PhysicsTeleport>().Write<ExternalMotion>()
            .Write<PhysicsCharacter>().Write<PhysicsCharacterInput>().Write<PhysicsCharacterState>()
            .Write<LocalTransform>().Write<TransformParent>().Write<WorldTransform>();
    }
    /// <summary>Records a root entity with complete transform and physics roles. Gameplay-phase spawns activate in the next Movement.</summary>
    /// <param name="commands">Active game command writer.</param><param name="body">Descriptor.</param>
    /// <param name="transform">Unit-scale root pose.</param><param name="velocity">Initial velocities.</param><returns>Batch-local entity token.</returns>
    public static DeferredEntity CreateBody(EcsCommandWriter commands, PhysicsBody body, LocalTransform transform, PhysicsVelocity velocity = default)
    {
        var entity = commands.CreateEntity();
        commands.AddComponent(entity, transform); commands.AddComponent(entity, default(TransformParent)); commands.AddComponent(entity, default(WorldTransform));
        AddBody(commands, entity, body, velocity); return entity;
    }
    /// <summary>Attaches physics to an existing complete root transform at a phase boundary.</summary>
    /// <param name="commands">Active writer.</param><param name="entity">Live/batch-local identity.</param>
    /// <param name="body">Descriptor.</param><param name="velocity">Initial velocities.</param>
    public static void AddBody(EcsCommandWriter commands, CommandTarget entity, PhysicsBody body, PhysicsVelocity velocity = default)
    { commands.AddComponent(entity, body); commands.AddComponent(entity, velocity); commands.AddComponent(entity, new ExternalMotion()); }
    /// <summary>Detaches physics; the entity and its transform remain. Existing ordinary LinearVelocity may resume movement.</summary>
    /// <param name="commands">Active writer.</param><param name="entity">Live/batch-local identity.</param>
    public static void RemoveBody(EcsCommandWriter commands, CommandTarget entity)
    {
        RemoveCharacter(commands, entity);
        commands.RemoveComponent<PhysicsBody>(entity); commands.RemoveComponent<PhysicsVelocity>(entity);
        commands.RemoveComponent<PhysicsTeleport>(entity); commands.RemoveComponent<ExternalMotion>(entity);
    }
    /// <summary>Attaches complete character roles to an existing PhysicsBody capsule at a phase boundary.</summary>
    public static void AddCharacter(EcsCommandWriter commands, CommandTarget entity, PhysicsCharacter character, PhysicsCharacterInput input = default)
    { commands.AddComponent(entity, character); commands.AddComponent(entity, input); commands.AddComponent(entity, default(PhysicsCharacterState)); }
    /// <summary>Removes the controller while leaving the body and its transform alive.</summary>
    public static void RemoveCharacter(EcsCommandWriter commands, CommandTarget entity)
    { commands.RemoveComponent<PhysicsCharacter>(entity); commands.RemoveComponent<PhysicsCharacterInput>(entity); commands.RemoveComponent<PhysicsCharacterState>(entity); }
}
