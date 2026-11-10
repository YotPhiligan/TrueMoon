namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Data-only startup presentation budgets. Export requests and UI state remain operation data.</summary>
public sealed record EditorDiagnosticSettings
{
    /// <summary>Initial MaximumRecords limit.</summary>
    public int MaximumRecords { get; set; } = 512;
    /// <summary>Initial MaximumRetainedBytes limit.</summary>
    public int MaximumRetainedBytes { get; set; } = 1024 * 1024;
    /// <summary>Initial MaximumRecordCharacters limit.</summary>
    public int MaximumRecordCharacters { get; set; } = 8192;
    /// <summary>Initial RecordsPerRefresh limit.</summary>
    public int RecordsPerRefresh { get; set; } = 64;
    /// <summary>Initial RefreshInterval limit.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>Initial MaximumProcesses limit.</summary>
    public int MaximumProcesses { get; set; } = 16;
    /// <summary>Initial MaximumRawCharacters limit.</summary>
    public int MaximumRawCharacters { get; set; } = 4096;
    /// <summary>Copies values to the existing immutable presentation options.</summary>
    /// <returns>Detached options.</returns>
    public EditorDiagnosticsOptions ToOptions() => new()
    {
        MaximumRecords = MaximumRecords,
        MaximumRetainedBytes = MaximumRetainedBytes,
        MaximumRecordCharacters = MaximumRecordCharacters,
        RecordsPerRefresh = RecordsPerRefresh,
        RefreshInterval = RefreshInterval,
        MaximumProcesses = MaximumProcesses,
        MaximumRawCharacters = MaximumRawCharacters
    };
}
