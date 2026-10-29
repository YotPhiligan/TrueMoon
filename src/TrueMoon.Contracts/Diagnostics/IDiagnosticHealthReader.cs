namespace TrueMoon.Diagnostics;

/// <summary>Borrowed in-memory health. Reading never flushes sinks, inspects files or transfers collector ownership.</summary>
public interface IDiagnosticHealthReader
{
    /// <summary>Returns independent bounded counters. Sink acceptance/writes do not promise disk durability.</summary>
    DiagnosticHealthSnapshot ReadHealth();
}
