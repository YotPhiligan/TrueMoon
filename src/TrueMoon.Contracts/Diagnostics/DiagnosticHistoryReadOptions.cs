namespace TrueMoon.Diagnostics;

/// <summary>Finite read-only budgets for a best-effort JSONL history snapshot.</summary>
public sealed record DiagnosticHistoryReadOptions
{
    /// <summary>Maximum retained records, from1 to1024.</summary>
    public int MaximumRecords { get; init; } = 512;
    /// <summary>Maximum bytes read from event files, including partial line boundaries.</summary>
    public int MaximumReadBytes { get; init; } = 4 * 1024 * 1024;
    /// <summary>Maximum estimated retained record bytes.</summary>
    public int MaximumRetainedBytes { get; init; } = 2 * 1024 * 1024;
    /// <summary>Maximum event files opened, from1 to64.</summary>
    public int MaximumFiles { get; init; } = 16;
    /// <summary>Maximum JSONL record bytes, from256 to262144.</summary>
    public int MaximumLineBytes { get; init; } = 65536;
    /// <summary>Cooperative discovery/read/parse budget, at most5seconds.</summary>
    public TimeSpan TimeBudget { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Validates all limits before any filesystem work.</summary>
    public void Validate()
    {
        if (MaximumRecords is < 1 or > 1024 || MaximumReadBytes is < 256 or > 16 * 1024 * 1024 ||
            MaximumRetainedBytes is < 1024 or > 8 * 1024 * 1024 || MaximumFiles is < 1 or > 64 ||
            MaximumLineBytes is < 256 or > 262144 || TimeBudget <= TimeSpan.Zero || TimeBudget > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(DiagnosticHistoryReadOptions));
    }
}
