namespace TrueMoon.Diagnostics;

/// <summary>A best-effort maintenance pass over recognized closed JSONL runs; other directories are preserved.</summary>
/// <param name="RetainedRuns">Closed recognized runs remaining.</param>
/// <param name="RetainedBytes">Bytes in those runs; conservative after partial deletion failure.</param>
/// <param name="ActiveRuns">Runs holding an exclusive marker lease, excluded from closed-run budgets.</param>
/// <param name="SkippedDirectories">Unknown, incomplete or unsafe directories preserved.</param>
/// <param name="DeletedRuns">Runs removed in this pass.</param>
/// <param name="Failures">Filesystem failures encountered.</param>
/// <param name="LastError">Last failure summary, bounded to 1024 characters.</param>
/// <param name="IsOverBudget">Protected or inaccessible runs prevented satisfying count, byte or age limits.</param>
/// <param name="MaintenanceBusy">Another cleaner held the root lease; this pass did not inspect runs.</param>
public sealed record DiagnosticRetentionStatus(int RetainedRuns, long RetainedBytes, int ActiveRuns,
    int SkippedDirectories, int DeletedRuns, int Failures, string? LastError, bool IsOverBudget, bool MaintenanceBusy);
