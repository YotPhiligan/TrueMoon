namespace TrueMoon.Diagnostics;

/// <summary>Limits closed, owned JSONL run directories under one diagnostics root.</summary>
public sealed class DiagnosticRunRetentionOptions
{
    /// <summary>Maximum closed runs; the most recently created closed run is always protected.</summary>
    public int MaxRuns { get; set; } = 8;
    /// <summary>Maximum bytes in recognized closed runs, including their marker and lock files.</summary>
    public long MaxTotalBytes { get; set; } = 32L * 1024 * 1024;
    /// <summary>Maximum age since run creation; active runs are excluded and the newest closed run remains available.</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    internal DiagnosticRunRetentionOptions Snapshot()
    {
        if (MaxRuns < 1) throw new ArgumentOutOfRangeException(nameof(MaxRuns));
        if (MaxTotalBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes));
        if (MaxAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaxAge));
        return new() { MaxRuns = MaxRuns, MaxTotalBytes = MaxTotalBytes, MaxAge = MaxAge };
    }
}
