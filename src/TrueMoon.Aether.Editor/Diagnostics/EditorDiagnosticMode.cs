namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Raw stream snapshots and channel health are separate from classified structured events.</summary>
public enum EditorDiagnosticMode { Structured, StandardOutput, StandardError, Health, Compiler }

/// <summary>A retained list row with an explicit presentation identity, never a synthetic collector event.</summary>
public sealed record EditorDiagnosticRow(string Id, string Label)
{
    /// <summary>Classified structured warning; never inferred from a raw stream choice.</summary>
    public bool Warning { get; init; }
}
