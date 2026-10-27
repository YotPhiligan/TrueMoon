namespace TrueMoon.Diagnostics;

/// <summary>An immutable, bounded event snapshot. Properties supplied by the pipeline are frozen.</summary>
/// <param name="TimestampUtc">UTC capture time.</param>
/// <param name="Sequence">Collector-local admission order; not a cross-process clock.</param>
/// <param name="ApplicationId">Identity of the collecting pipeline; legacy listener routing remains process-wide.</param>
/// <param name="ProcessId">Emitting process identity.</param>
/// <param name="Source">Source category.</param>
/// <param name="Level">Severity.</param>
/// <param name="EventId">Stable module-owned number.</param>
/// <param name="EventName">Stable event name.</param>
/// <param name="MessageTemplate">Unformatted message template.</param>
/// <param name="Properties">Copied, bounded named values.</param>
/// <param name="ExceptionType">Exception type, when present.</param>
/// <param name="ExceptionMessage">Exception message, when present.</param>
/// <param name="ExceptionStackTrace">Exception stack, when present.</param>
/// <param name="TraceId">Activity trace identity at emission time.</param>
/// <param name="SpanId">Activity span identity at emission time.</param>
/// <param name="Caller">Optional caller.</param>
/// <param name="IsTruncated">Whether any part of the event was omitted or shortened.</param>
public sealed record DiagnosticRecord(DateTimeOffset TimestampUtc, long Sequence, Guid ApplicationId,
    int ProcessId, string Source, DiagnosticLevel Level, int EventId, string EventName,
    string MessageTemplate, IReadOnlyDictionary<string, string?> Properties,
    string? ExceptionType, string? ExceptionMessage, string? ExceptionStackTrace,
    string? TraceId, string? SpanId, string? Caller, bool IsTruncated)
{
    /// <summary>Original collector identity for an imported record; null for local capture.</summary>
    public DiagnosticOrigin? Origin { get; init; }
}
