namespace TrueMoon.Aether.Builds;

/// <summary>Availability of a completed job's structured compiler diagnostics.</summary>
public enum GameBuildDiagnosticState
{
    /// <summary>The compiler has not completed capture.</summary>
    Pending,
    /// <summary>A supported report was read, including a valid report with no diagnostics.</summary>
    Available,
    /// <summary>No report was produced, for example an MSBuild failure before C# compilation.</summary>
    Unavailable,
    /// <summary>The report could not be read or its schema was unsupported.</summary>
    Failed,
    /// <summary>The report exceeds the bounded read budget.</summary>
    TooLarge
}

/// <summary>Detached bounded SARIF input. An empty list alone does not establish a successful build.</summary>
/// <param name="State">Capture availability, separate from the compiler exit outcome.</param>
/// <param name="Entries">At most128 diagnostics, each with bounded text.</param>
/// <param name="Omitted">Known results omitted by the record limit.</param>
/// <param name="IsTruncated">Whether text, records or input exceeded a limit.</param>
/// <param name="FailureType">Read/schema failure class without error text, or null.</param>
public sealed record GameBuildDiagnosticBatch(GameBuildDiagnosticState State, IReadOnlyList<GameBuildDiagnostic> Entries,
    int Omitted = 0, bool IsTruncated = false, string? FailureType = null);
