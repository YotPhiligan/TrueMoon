using System.Numerics;

namespace TrueMoon.Kton;

/// <summary>Caller-supplied immutable world arguments. A provider validates and snapshots them before acquiring resources.</summary>
public sealed record PhysicsWorldOptions
{
    /// <summary>Whole-world solver/contact/CCD profile.</summary>
    public PhysicsQualityProfile Quality { get; init; } = PhysicsQualityProfile.Baseline60;
    /// <summary>Acceleration in metres/second squared, Y up.</summary>
    public Vector3 Gravity { get; init; } = new(0, -9.81f, 0);
    /// <summary>Admission policy. Mutable arrays or delegates are not retained.</summary>
    public PhysicsLimits Limits { get; init; } = new();
    /// <summary>Diagnostic p99 Step target in milliseconds; not a hardware-independent failure gate.</summary>
    public double StepDiagnosticBudgetMilliseconds { get; init; } = 1;
}
