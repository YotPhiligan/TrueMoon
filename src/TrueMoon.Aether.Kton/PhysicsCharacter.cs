using System.Numerics;
using TrueMoon.Aether.Ecs;
using TrueMoon.Kton.Characters;

namespace TrueMoon.Aether.Kton;

/// <summary>Reference-free optional controller settings for an upright dynamic PhysicsBody capsule.</summary>
public readonly record struct PhysicsCharacter : IComponent
{
    /// <summary>Creates the standard PHYS6 profile. Default(struct) is invalid.</summary>
    public PhysicsCharacter() { }
    /// <summary>Maximum world XZ target speed, metres/second.</summary>
    public float MaximumSpeed { get; init; } = 6;
    /// <summary>Maximum support slope, radians.</summary>
    public float MaximumSlopeRadians { get; init; } = MathF.PI / 4;
    /// <summary>Maximum tangent force, newtons.</summary>
    public float MaximumHorizontalForce { get; init; } = 1000;
    /// <summary>Maximum support retention force, newtons.</summary>
    public float MaximumVerticalForce { get; init; } = 1000;
    /// <summary>Jump speed relative to support.</summary>
    public float JumpSpeed { get; init; } = 5;
    /// <summary>Captures neutral controller options; callers validate before acquiring resources.</summary>
    public PhysicsCharacterOptions CaptureOptions() => new()
    {
        MaximumSpeed = MaximumSpeed, MaximumSlopeRadians = MaximumSlopeRadians, MaximumHorizontalForce = MaximumHorizontalForce,
        MaximumVerticalForce = MaximumVerticalForce, JumpSpeed = JumpSpeed
    };
}

/// <summary>Persistent world XZ velocity. Increase JumpSequence for each jump; unchanged sequences never retrigger.</summary>
public readonly record struct PhysicsCharacterInput(Vector2 Velocity, ulong JumpSequence = 0) : IComponent;

/// <summary>Last completed step's support projection. Physics owns this output; default support entity means airborne.</summary>
public readonly record struct PhysicsCharacterState(bool IsSupported, EntityHandle SupportEntity, Vector3 SupportNormal) : IComponent;
