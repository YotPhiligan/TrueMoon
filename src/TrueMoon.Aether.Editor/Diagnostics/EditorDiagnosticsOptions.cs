namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>Independent presentation budgets; these do not change the App collector or its sinks.</summary>
public sealed record EditorDiagnosticsOptions
{
    public int MaximumRecords { get; init; } = 512;
    public int MaximumRetainedBytes { get; init; } = 1024 * 1024;
    public int MaximumRecordCharacters { get; init; } = 8192;
    public int RecordsPerRefresh { get; init; } = 64;
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMilliseconds(100);
    public int MaximumProcesses { get; init; } = 16;
    public int MaximumRawCharacters { get; init; } = 4096;

    internal EditorDiagnosticsOptions Snapshot()
    {
        if (MaximumRecords is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(MaximumRecords));
        if (MaximumRetainedBytes is < 4096 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaximumRetainedBytes));
        if (MaximumRecordCharacters is < 512 or > 16384) throw new ArgumentOutOfRangeException(nameof(MaximumRecordCharacters));
        if (RecordsPerRefresh is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(RecordsPerRefresh));
        if (MaximumProcesses is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(MaximumProcesses));
        if (MaximumRawCharacters is < 0 or > 16384) throw new ArgumentOutOfRangeException(nameof(MaximumRawCharacters));
        if (RefreshInterval < TimeSpan.FromMilliseconds(16) || RefreshInterval > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(RefreshInterval));
        return this with { };
    }
}
