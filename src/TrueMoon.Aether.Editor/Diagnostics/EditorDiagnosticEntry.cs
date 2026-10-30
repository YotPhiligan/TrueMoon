using System.Globalization;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Diagnostics;

/// <summary>A displayed group of equivalent retained events. Count covers this console's retained window only.</summary>
public sealed record EditorDiagnosticEntry(DiagnosticRecord Record, int Count)
{
    public string Id => Record.Sequence.ToString(CultureInfo.InvariantCulture);
    public string Label => (Count > 1 ? $"×{Count} · " : "") +
        $"{Record.TimestampUtc:HH:mm:ss.fff} · {Record.Level} · {Record.Source} · {Record.EventName} · {Record.MessageTemplate}".ReplaceLineEndings(" ");
}

/// <summary>Immutable report input, detached from the UI and the borrowed collector.</summary>
public sealed record EditorDiagnosticSnapshot(IReadOnlyList<DiagnosticRecord> Records, long Cursor, long UnavailableSequences,
    long PresentationEvicted, long PresentationTruncated, bool CollectorAvailable)
{
    /// <summary>Effective presentation limits, when captured by the console model.</summary>
    public EditorDiagnosticsOptions? PresentationLimits { get; init; }
    public TrueMoon.Diagnostics.DiagnosticHealthSnapshot? Health { get; init; }
    public TrueMoon.Aether.Diagnostics.ProcessDiagnosticBatch? Processes { get; init; }
    /// <summary>Actual value-only presentation observation, when a UI session has reported one.</summary>
    public EditorDiagnosticPresentation? Presentation { get; init; }
}
