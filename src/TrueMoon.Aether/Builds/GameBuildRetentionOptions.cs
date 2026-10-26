namespace TrueMoon.Aether.Builds;

/// <summary>Limits completed, owned compiler diagnostic jobs independently of published game artifacts.</summary>
public sealed class GameBuildRetentionOptions
{
    /// <summary>Maximum completed jobs, including the protected newest job.</summary>
    public int MaxJobs { get; set; } = 16;
    /// <summary>Maximum bytes in completed job files, including ownership metadata.</summary>
    public long MaxTotalBytes { get; set; } = 64L * 1024 * 1024;
    /// <summary>Maximum age since job cleanup completed. The newest job remains available even when older.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    internal GameBuildRetentionOptions Snapshot()
    {
        if (MaxJobs < 1) throw new ArgumentOutOfRangeException(nameof(MaxJobs));
        if (MaxTotalBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes));
        if (MaxAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaxAge));
        return new() { MaxJobs = MaxJobs, MaxTotalBytes = MaxTotalBytes, MaxAge = MaxAge };
    }
}
