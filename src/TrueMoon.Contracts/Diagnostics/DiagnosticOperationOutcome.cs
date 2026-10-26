namespace TrueMoon.Diagnostics;

/// <summary>Finite operation outcomes shared by traces and metrics.</summary>
public enum DiagnosticOperationOutcome
{
    /// <summary>The operation finished successfully.</summary>
    Succeeded,
    /// <summary>The operation failed.</summary>
    Failed,
    /// <summary>The operation was cancelled.</summary>
    Cancelled,
    /// <summary>The owner disposed the operation without selecting an outcome.</summary>
    Incomplete
}
