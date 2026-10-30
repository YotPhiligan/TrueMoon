namespace TrueMoon.Diagnostics;

/// <summary>Borrowed read-only access to the configured logger's JSONL history.</summary>
/// <remarks>Only configured event slots and recognized managed runs are inspected.
/// The caller owns and drains accepted read tasks; reading does not wait for or dispose sink workers.</remarks>
public interface IDiagnosticArchiveReader
{
    /// <summary>Reads a bounded best-effort history snapshot outside the caller/UI thread.</summary>
    /// <param name="options">Validated input and retention budgets.</param>
    /// <param name="cancellationToken">Cancels accepted reads.</param>
    /// <returns>Frozen records and explicit loss/unavailability counters.</returns>
    Task<DiagnosticHistorySnapshot> ReadHistoryAsync(DiagnosticHistoryReadOptions options, CancellationToken cancellationToken = default);
}
