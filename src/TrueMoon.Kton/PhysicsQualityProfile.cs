namespace TrueMoon.Kton;

/// <summary>Version-one candidate quality presets. These are policy definitions, not universal collision guarantees.</summary>
public enum PhysicsQualityProfile
{
    /// <summary>60 Hz, unbounded speculative margin and 120 Hz contact spring. Baseline PHYS0 profile.</summary>
    Baseline60,
    /// <summary>60 Hz, swept linear detection, capped margin and 600 Hz contact spring. Limited PHYS0 projectile evidence only.</summary>
    FastLinear60
}

/// <summary>Collision detection algorithm, independent of a backend type.</summary>
public enum PhysicsCcdMode
{
    /// <summary>Velocity-expanded speculative contacts without sweeps.</summary>
    Speculative,
    /// <summary>Sweep-assisted speculative contacts, not a hard time-of-impact clamp.</summary>
    Swept
}

/// <summary>Immutable settings derived from one known profile. No independent soft-contact/swept override.</summary>
public sealed class PhysicsProfileSettings
{
    private PhysicsProfileSettings(PhysicsQualityProfile profile) => Profile = profile;
    /// <summary>Selected version-one profile.</summary>
    public PhysicsQualityProfile Profile { get; }
    /// <summary>Fixed seconds per external step; the world has no timer.</summary>
    public float FixedDeltaSeconds => 1f / 60;
    /// <summary>Velocity iterations per solver substep.</summary>
    public int SolverIterations => 8;
    /// <summary>Solver substeps, distinct from full collision detection steps.</summary>
    public int SolverSubsteps => 4;
    /// <summary>Full collision detection steps per external tick.</summary>
    public int CollisionSteps => 1;
    /// <summary>Supported worker count in this initial contract profile.</summary>
    public int Workers => 1;
    /// <summary>Detection mode selected by the whole world profile.</summary>
    public PhysicsCcdMode CcdMode => Profile == PhysicsQualityProfile.FastLinear60 ? PhysicsCcdMode.Swept : PhysicsCcdMode.Speculative;
    /// <summary>Contact spring frequency in Hz, not a restitution coefficient.</summary>
    public float ContactSpringFrequency => Profile == PhysicsQualityProfile.FastLinear60 ? 600 : 120;
    /// <summary>Contact spring damping ratio.</summary>
    public float ContactDampingRatio => 1;
    /// <summary>Maximum penetration recovery speed in metres/second.</summary>
    public float MaximumRecoveryVelocity => 2;
    /// <summary>Maximum speculative contact margin in metres; float.MaxValue means unbounded.</summary>
    public float MaximumSpeculativeMargin => Profile == PhysicsQualityProfile.FastLinear60 ? 0.01f : float.MaxValue;
    /// <summary>Minimum sweep time interval, in seconds. Zero means no sweeps in this profile.</summary>
    public float MinimumSweepTimestep => Profile == PhysicsQualityProfile.FastLinear60 ? 1e-6f : 0;
    /// <summary>Sweep convergence threshold in seconds.</summary>
    public float SweepConvergenceThreshold => MinimumSweepTimestep;
    /// <summary>Admission envelope for explicit linear velocities, not a guarantee for arbitrary geometry or solver-generated motion.</summary>
    public float MaximumLinearSpeed => Profile == PhysicsQualityProfile.FastLinear60 ? 600 : 60;
    /// <summary>Gets a validated known profile.</summary>
    /// <param name="profile">Supported profile.</param><returns>Immutable shared settings.</returns>
    public static PhysicsProfileSettings For(PhysicsQualityProfile profile) => profile switch
    {
        PhysicsQualityProfile.Baseline60 => Baseline,
        PhysicsQualityProfile.FastLinear60 => Fast,
        _ => throw new PhysicsException(new(PhysicsErrorCode.UnsupportedProfile, nameof(profile)))
    };
    private static readonly PhysicsProfileSettings Baseline = new(PhysicsQualityProfile.Baseline60);
    private static readonly PhysicsProfileSettings Fast = new(PhysicsQualityProfile.FastLinear60);
}
