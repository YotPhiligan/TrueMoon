namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Per-operation budgets bounded by source-capture profile v1. Limits may be reduced, never raised.</summary>
public sealed record ModelSourceCaptureLimits
{
    internal const int MaximumFiles = 32;
    /// <summary>Maximum captured files, including the root; at most 32.</summary>
    public int Files { get; init; } = MaximumFiles;
    /// <summary>Maximum bytes per file; at most 16 MiB.</summary>
    public int FileBytes { get; init; } = 16 * 1024 * 1024;
    /// <summary>Maximum retained source bytes; at most 32 MiB.</summary>
    public int TotalBytes { get; init; } = 32 * 1024 * 1024;

    internal ModelSourceCaptureLimits Snapshot()
    {
        if (Files is < 1 or > MaximumFiles || FileBytes is < 1 or > 16 * 1024 * 1024 || TotalBytes is < 1 or > 32 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(ModelSourceCaptureLimits), "Capture budgets must be positive and within profile v1.");
        return this with { };
    }
}
