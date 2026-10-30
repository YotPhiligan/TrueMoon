namespace TrueMoon.Aether.Builds;

/// <summary>Severity reported by the compiler, independently of stdout/stderr.</summary>
public enum GameBuildDiagnosticSeverity
{
    /// <summary>The producer did not provide a supported severity.</summary>
    Unknown,
    /// <summary>A compiler or analyzer error.</summary>
    Error,
    /// <summary>A compiler or analyzer warning.</summary>
    Warning,
    /// <summary>An informational diagnostic.</summary>
    Information,
    /// <summary>A hidden diagnostic.</summary>
    Hidden
}

/// <summary>One immutable compiler diagnostic; coordinates are one-based.</summary>
/// <param name="Code">Compiler/analyzer rule identifier.</param>
/// <param name="Severity">Producer-reported severity.</param>
/// <param name="Message">Bounded diagnostic text, which may contain personal paths.</param>
/// <param name="FilePath">Absolute local source path, or null for unsupported/non-file locations.</param>
/// <param name="Line">Source line, or null when no usable region was reported.</param>
/// <param name="Column">Source column, or null when no usable region was reported.</param>
public sealed record GameBuildDiagnostic(string Code, GameBuildDiagnosticSeverity Severity, string Message,
    string? FilePath, int? Line, int? Column);
