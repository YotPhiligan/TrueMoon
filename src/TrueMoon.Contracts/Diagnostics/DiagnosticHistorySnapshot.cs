namespace TrueMoon.Diagnostics;

/// <summary>A bounded archive view. Reading neither flushes sinks nor establishes disk durability.</summary>
/// <param name="Available">Whether the owner has configured JSONL history.</param>
/// <param name="Records">Frozen records ordered by timestamp, application identity and collector sequence.</param>
public sealed record DiagnosticHistorySnapshot(bool Available, IReadOnlyList<DiagnosticRecord> Records)
{
    /// <summary>Event files successfully opened.</summary>
    public int FilesRead { get; init; }
    /// <summary>Event file bytes actually read.</summary>
    public long BytesRead { get; init; }
    /// <summary>Malformed or invalid complete JSONL records.</summary>
    public int InvalidLines { get; init; }
    /// <summary>Incomplete file tails or clipped line boundaries.</summary>
    public int IncompleteLines { get; init; }
    /// <summary>Complete lines exceeding the line byte budget.</summary>
    public int OversizedLines { get; init; }
    /// <summary>Files or directories that disappeared during discovery/read.</summary>
    public int MissingEntries { get; init; }
    /// <summary>Unknown, unsafe, locked or inaccessible entries skipped during discovery/read.</summary>
    public int SkippedEntries { get; init; }
    /// <summary>Filesystem failures independent of invalid JSONL lines.</summary>
    public int ReadFailures { get; init; }
    /// <summary>Known records omitted by retention count/bytes limits.</summary>
    public long OmittedRecords { get; init; }
    /// <summary>Duplicate collector identities ignored within the retained window.</summary>
    public long DuplicateRecords { get; init; }
    /// <summary>Whether discovery, input, parsing or retained output was limited.</summary>
    public bool IsTruncated { get; init; }
    /// <summary>Whether the cooperative read budget expired. Caller cancellation still throws.</summary>
    public bool TimeBudgetExceeded { get; init; }
}
