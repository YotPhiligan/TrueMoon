namespace TrueMoon.Aether.Ecs;

/// <summary>Finite graph and phase command limits, copied before activation.</summary>
public sealed class EcsSchedulerOptions
{
    /// <summary>Gets or sets the system count limit. Default128, allowed1..1024; empty graphs are valid.</summary>
    public int MaximumSystems { get; set; } = 128;
    /// <summary>Gets or sets commands per phase. Default4096, allowed1..1048576.</summary>
    public int MaximumCommandsPerPhase { get; set; } = 4096;
    internal EcsSchedulerOptions Snapshot()
    {
        var copy = (EcsSchedulerOptions)MemberwiseClone();
        if (copy.MaximumSystems is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaximumSystems));
        if (copy.MaximumCommandsPerPhase is < 1 or > 1048576) throw new ArgumentOutOfRangeException(nameof(MaximumCommandsPerPhase));
        return copy;
    }
}
