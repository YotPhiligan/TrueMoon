namespace TrueMoon.Aether.Kton.Runtime;

/// <summary>Copied admission policy for ECS contact projection; backend world options remain in the selected provider.</summary>
public sealed class PhysicsBridgeOptions
{
    /// <summary>Maximum projected contacts per successful step,1..256. Overflow faults instead of silently truncating.</summary>
    public int MaximumContactEvents { get; set; } = 64;
    internal PhysicsBridgeOptions Snapshot()
    {
        if (MaximumContactEvents is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(MaximumContactEvents));
        return new() { MaximumContactEvents = MaximumContactEvents };
    }
}
