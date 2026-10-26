namespace TrueMoon.Diagnostics;

/// <summary>Producer data consumed synchronously before a bounded diagnostic snapshot is queued.</summary>
/// <param name="Level">Event severity.</param>
/// <param name="EventId">Stable module-owned event number.</param>
/// <param name="EventName">Stable event name, independent of the caller's method name.</param>
/// <param name="MessageTemplate">Unformatted message template.</param>
/// <param name="Properties">Optional named string values. The pipeline copies and bounds these values.</param>
/// <param name="Error">Original exception, snapshotted rather than retained by the asynchronous pipeline.</param>
/// <param name="Caller">Optional source member.</param>
public sealed record DiagnosticMessage(DiagnosticLevel Level, int EventId, string EventName,
    string MessageTemplate, IReadOnlyDictionary<string, string?>? Properties = null,
    Exception? Error = null, string? Caller = null);
