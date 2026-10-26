namespace TrueMoon.Diagnostics;

/// <summary>Independent emergency-worker counters. Writer failures never recursively report themselves.</summary>
/// <param name="Accepted">Reports accepted within the queue and lifetime budgets.</param>
/// <param name="Written">Completed writer calls.</param>
/// <param name="Dropped">Reports rejected because the executing-inclusive queue was full.</param>
/// <param name="Suppressed">Reports beyond the lifetime attempt budget.</param>
/// <param name="Failures">Failed emergency writer calls.</param>
/// <param name="Queued">Queued plus executing reports.</param>
/// <param name="LastError">Bounded type of the last writer failure; contains no exception message or payload.</param>
public sealed record DiagnosticEmergencyStatus(long Accepted, long Written, long Dropped, long Suppressed,
    long Failures, int Queued, string? LastError);
