namespace TrueMoon.Diagnostics;

/// <summary>Severity of a structured diagnostic event, independently of its payload type.</summary>
public enum DiagnosticLevel
{
    /// <summary>Detailed diagnostic capture.</summary>
    Trace,
    /// <summary>Development diagnostics.</summary>
    Debug,
    /// <summary>Normal operational event.</summary>
    Information,
    /// <summary>Recoverable unexpected condition.</summary>
    Warning,
    /// <summary>Failed operation.</summary>
    Error,
    /// <summary>Failure preventing continued operation.</summary>
    Critical
}
