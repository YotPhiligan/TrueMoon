using System.Numerics;

namespace TrueMoon.Kton.Characters;

/// <summary>Immutable +Y-up capsule controller settings. Forces are newtons; speeds are metres/second.</summary>
public sealed record PhysicsCharacterOptions
{
    /// <summary>Maximum magnitude of the requested world XZ velocity.</summary>
    public float MaximumSpeed { get; init; } = 6;
    /// <summary>Maximum support slope in radians, strictly between zero and pi/2.</summary>
    public float MaximumSlopeRadians { get; init; } = MathF.PI / 4;
    /// <summary>Maximum force tangent to the support, including braking.</summary>
    public float MaximumHorizontalForce { get; init; } = 1000;
    /// <summary>Maximum force retaining contact over descending slopes.</summary>
    public float MaximumVerticalForce { get; init; } = 1000;
    /// <summary>Jump speed relative to the support along +Y.</summary>
    public float JumpSpeed { get; init; } = 5;
    /// <summary>Maximum automatic rise in metres on supported static box steps. Zero disables stepping.</summary>
    public float MaximumStepHeight { get; init; }
    /// <summary>Minimum remaining tread depth in metres along the direction of input.</summary>
    public float MinimumTreadDepth { get; init; } = .30f;
    /// <summary>Validates the bounded PHYS6 profile without mutating a world.</summary>
    public PhysicsError Validate()
    {
        if (!Positive(MaximumSpeed, 50) || !Positive(MaximumSlopeRadians, 1.5f) ||
            !Positive(MaximumHorizontalForce, 100000) || !Positive(MaximumVerticalForce, 100000) || !Positive(JumpSpeed, 50) ||
            !(MaximumStepHeight >= 0 && MaximumStepHeight <= 1) || !Positive(MinimumTreadDepth, 2))
            return new(PhysicsErrorCode.InvalidArgument, "Character.Options");
        return default;
    }
    /// <summary>Validates a world XZ target velocity against this profile.</summary>
    public PhysicsError ValidateInput(PhysicsCharacterInput input)
    {
        var error = Validate();
        if (error.IsFailure) return error;
        return !float.IsFinite(input.Velocity.X) || !float.IsFinite(input.Velocity.Y) || input.Velocity.LengthSquared() > MaximumSpeed * MaximumSpeed
            ? new(PhysicsErrorCode.InvalidArgument, "Character.Input") : default;
    }
    private static bool Positive(float value, float maximum) => float.IsFinite(value) && value > 0 && value <= maximum;
}

/// <summary>World XZ velocity persists until replaced. Jump is consumed once by the next successful begun step, including failed attempts.</summary>
public readonly record struct PhysicsCharacterInput(Vector2 Velocity, bool Jump = false);
