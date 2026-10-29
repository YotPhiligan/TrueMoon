namespace TrueMoon.Diagnostics;

/// <summary>Collector counters and independent sink snapshots; counters across workers are best-effort, not one transaction.</summary>
/// <param name="ApplicationId">Collecting pipeline identity.</param>
/// <param name="Closed">Collector admission is closed.</param>
/// <param name="LatestSequence">Latest local admission sequence.</param>
/// <param name="RetainedRecords">Records currently in the memory ring.</param>
/// <param name="RetainedBytes">Estimated ring payload bytes.</param>
/// <param name="MemoryEvicted">Records evicted or too large for the ring.</param>
/// <param name="CaptureFailures">Failures before record admission.</param>
/// <param name="LateWrites">Write attempts after admission closed.</param>
/// <param name="Sinks">Independent immutable sink snapshots.</param>
public sealed record DiagnosticHealthSnapshot(Guid ApplicationId, bool Closed, long LatestSequence,
    int RetainedRecords, long RetainedBytes, long MemoryEvicted, long CaptureFailures, long LateWrites,
    IReadOnlyList<DiagnosticSinkHealth> Sinks)
{
    /// <summary>Emergency worker health; null when disabled.</summary>
    public DiagnosticEmergencyHealth? Emergency { get; init; }
    /// <summary>Cached managed-run retention health; null until available or when disabled.</summary>
    public DiagnosticRetentionHealth? Retention { get; init; }
}

/// <summary>Executing-inclusive queue counters. LastError is bounded display text and may contain personal paths.</summary>
/// <param name="Name">Collector-local sink name.</param>
/// <param name="Accepted">Records admitted to this sink.</param>
/// <param name="Written">Completed successful writes; not a durability guarantee.</param>
/// <param name="Dropped">Records refused by queue count or byte admission.</param>
/// <param name="Failures">Write or cleanup failures.</param>
/// <param name="Queued">Queued plus executing records.</param>
/// <param name="Bytes">Estimated queued plus executing payload bytes.</param>
/// <param name="LastError">Bounded last failure display text.</param>
public sealed record DiagnosticSinkHealth(string Name, long Accepted, long Written, long Dropped,
    long Failures, int Queued, long Bytes, string? LastError);

/// <summary>Independent emergency-worker counters; LastError is a failure type, never an exception payload.</summary>
/// <param name="Accepted">Admitted emergency reports.</param>
/// <param name="Written">Completed writer calls.</param>
/// <param name="Dropped">Reports refused by queue admission.</param>
/// <param name="Suppressed">Reports beyond the lifetime budget.</param>
/// <param name="Failures">Writer failures.</param>
/// <param name="Queued">Queued plus executing reports.</param>
/// <param name="LastError">Bounded last failure type.</param>
public sealed record DiagnosticEmergencyHealth(long Accepted, long Written, long Dropped, long Suppressed,
    long Failures, int Queued, string? LastError);

/// <summary>Cached retention maintenance counters. Reading does not run maintenance; null means not yet available or disabled.</summary>
/// <param name="RetainedRuns">Recognized closed runs remaining.</param>
/// <param name="RetainedBytes">Conservative size of retained runs.</param>
/// <param name="ActiveRuns">Protected active run leases.</param>
/// <param name="SkippedDirectories">Unknown or unsafe directories preserved.</param>
/// <param name="DeletedRuns">Runs deleted in the latest maintenance pass.</param>
/// <param name="Failures">Maintenance filesystem failures.</param>
/// <param name="LastError">Bounded maintenance failure display text.</param>
/// <param name="IsOverBudget">Protected or inaccessible runs prevent satisfying the configured limits.</param>
/// <param name="MaintenanceBusy">Another maintenance owner held the root lease.</param>
public sealed record DiagnosticRetentionHealth(int RetainedRuns, long RetainedBytes, int ActiveRuns,
    int SkippedDirectories, int DeletedRuns, int Failures, string? LastError, bool IsOverBudget, bool MaintenanceBusy);
