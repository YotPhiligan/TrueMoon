namespace TrueMoon.Diagnostics;

/// <summary>Borrowed memory-only view of effective collector settings, excluding paths, delegates and environment.</summary>
public interface IDiagnosticConfigurationReader
{
    /// <summary>Copies current filter counts and numeric budgets without IO or sink waits.</summary>
    /// <returns>A detached configuration snapshot.</returns>
    DiagnosticCollectorConfiguration ReadConfiguration();
}
