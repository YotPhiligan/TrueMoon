namespace TrueMoon.Diagnostics;

/// <summary>Observer failures and operation ownership; no exception objects or operation identifiers are retained.</summary>
public sealed record DiagnosticTelemetryStatus(long ActiveOperations, long RejectedOperations, long Failures, bool Closed, bool Released);
