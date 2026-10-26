namespace TrueMoon.Aether.Builds;

/// <summary>A snapshot of one best-effort compiler diagnostics maintenance pass.</summary>
/// <param name="RetainedJobs">Completed managed jobs remaining after maintenance.</param>
/// <param name="RetainedBytes">Bytes in those jobs, including ownership metadata; conservative after partial deletion failure.</param>
/// <param name="ActiveJobs">Jobs whose ownership marker is exclusively held by another operation.</param>
/// <param name="SkippedDirectories">Unrecognized, incomplete or unsafe directories preserved without deletion.</param>
/// <param name="DeletedJobs">Completed jobs removed by this pass.</param>
/// <param name="Failures">Filesystem failures encountered by this pass.</param>
/// <param name="LastError">Last failure summary, limited to 1024 characters.</param>
/// <param name="IsOverBudget">A protected or inaccessible completed job prevented satisfying count, byte or age limits.</param>
/// <param name="MaintenanceBusy">Another maintenance pass held the root lease; this pass did not inspect jobs.</param>
public sealed record GameBuildRetentionStatus(int RetainedJobs, long RetainedBytes, int ActiveJobs,
    int SkippedDirectories, int DeletedJobs, int Failures, string? LastError, bool IsOverBudget, bool MaintenanceBusy);
