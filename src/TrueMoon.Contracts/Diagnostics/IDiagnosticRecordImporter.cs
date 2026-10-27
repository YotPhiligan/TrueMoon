namespace TrueMoon.Diagnostics;

/// <summary>Borrowed admission of remote snapshots without re-emitting process-global diagnostic events.</summary>
public interface IDiagnosticRecordImporter
{
    /// <summary>Copies and bounds a local-origin record into this collector, applying its current filters and sink queues.</summary>
    /// <param name="record">Remote snapshot with a valid origin and no prior import.</param>
    /// <param name="context">Authoritative bounded properties, taking precedence over remote properties.</param>
    /// <returns>Whether the record was admitted; this does not guarantee sink delivery.</returns>
    bool TryImport(DiagnosticRecord record, IReadOnlyDictionary<string, string?>? context = null);
}
