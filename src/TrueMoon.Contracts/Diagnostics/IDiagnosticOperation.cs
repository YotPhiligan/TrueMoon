namespace TrueMoon.Diagnostics;

/// <summary>An operation owned by its caller. Dispose records duration and restores the parent trace once.</summary>
public interface IDiagnosticOperation : IDisposable
{
    /// <summary>Selects the terminal outcome; the first call wins. Only the exception type is retained.</summary>
    void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null);
}
