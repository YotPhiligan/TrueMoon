namespace TrueMoon.Diagnostics;

/// <summary>Borrowed bounded history. Reading never transfers ownership of the collector.</summary>
public interface IDiagnosticRecordReader
{
    /// <summary>Latest collector-local sequence, including unavailable capture slots.</summary>
    long LatestSequence { get; }
    /// <summary>Returns an independent oldest-first snapshot after the given collector sequence.</summary>
    /// <param name="afterSequence">Exclusive nonnegative cursor.</param>
    /// <param name="maximumCount">Maximum records, from 1 through 1024.</param>
    /// <returns>Frozen records still available in the bounded ring.</returns>
    IReadOnlyList<DiagnosticRecord> ReadAfter(long afterSequence, int maximumCount);
}
