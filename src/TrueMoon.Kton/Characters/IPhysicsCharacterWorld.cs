using System.Numerics;
using TrueMoon.Kton.Bodies;

namespace TrueMoon.Kton.Characters;

/// <summary>Optional capsule controller capability of an externally serialized physics world.</summary>
/// <remarks>The controller borrows a dynamic, upright, nonsensor capsule and locks its rotation. Remove the controller
/// before removing its body. The world owns controller resources; disposal invalidates all handles. No hidden timer,
/// air steering, automatic stairs, crouching or root motion. Input is applied only by the caller's fixed Step.</remarks>
public interface IPhysicsCharacterWorld
{
    /// <summary>Attaches one controller to a live capsule, retaining its pose and linear velocity.</summary>
    PhysicsCharacterId CreateCharacter(PhysicsBodyId body, PhysicsCharacterOptions options);
    /// <summary>Removes motion constraints and restores body inertia; leaves the borrowed body alive.</summary>
    void RemoveCharacter(PhysicsCharacterId character);
    /// <summary>Replaces the persistent target and next-step jump pulse.</summary>
    void SetCharacterInput(PhysicsCharacterId character, PhysicsCharacterInput input);
    /// <summary>Returns the current pose and last collision-analysis support; discontinuities clear support immediately.</summary>
    PhysicsCharacterState GetCharacterState(PhysicsCharacterId character);
}

/// <summary>Snapshot of an upright character. Support identity and normal are default when unsupported.</summary>
public readonly record struct PhysicsCharacterState(PhysicsCharacterId Id, PhysicsBodyId Body, PhysicsPose Pose,
    Vector3 LinearVelocity, bool IsSupported, PhysicsBodyId SupportBody, Vector3 SupportNormal);
