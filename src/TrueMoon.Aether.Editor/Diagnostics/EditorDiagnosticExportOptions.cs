using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>One explicit report request. Producer text is opt-in and is not automatically anonymized.</summary>
public sealed record EditorDiagnosticExportOptions
{
    /// <summary>Includes bounded messages, exception text, raw tails and compiler text/locations.</summary>
    public bool IncludeText { get; init; }
    /// <summary>Reads the configured Core JSONL history as well as the frozen console window.</summary>
    public bool IncludePersistentHistory { get; init; } = true;
    /// <summary>Maximum final UTF8 report bytes, from4096 to4MiB.</summary>
    public int MaximumBytes { get; init; } = EditorDiagnosticReportWriter.DefaultMaximumBytes;
    /// <summary>Maximum characters in one exported text field.</summary>
    public int MaximumTextCharacters { get; init; } = 4096;
    /// <summary>Cooperative collection/write deadline, at most10seconds.</summary>
    public TimeSpan TimeBudget { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Independent finite archive-read limits.</summary>
    public DiagnosticHistoryReadOptions History { get; init; } = new();
    /// <summary>Optional host-known sensitive literals removed from every opted-in text field. Never serialized.</summary>
    public IReadOnlyList<string> RedactedValues { get; init; } = [];

    internal EditorDiagnosticExportOptions Snapshot()
    {
        if (MaximumBytes is < 4096 or > 4 * 1024 * 1024 || MaximumTextCharacters is < 128 or > 16384 ||
            TimeBudget <= TimeSpan.Zero || TimeBudget > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(EditorDiagnosticExportOptions));
        ArgumentNullException.ThrowIfNull(History); History.Validate(); ArgumentNullException.ThrowIfNull(RedactedValues);
        if (RedactedValues.Count > 64 || RedactedValues.Any(value => string.IsNullOrEmpty(value) || value.Length > 512))
            throw new ArgumentException("Expected at most64 bounded sensitive literals.", nameof(RedactedValues));
        return this with { History = History with { }, RedactedValues = Array.AsReadOnly(RedactedValues.ToArray()) };
    }
}
