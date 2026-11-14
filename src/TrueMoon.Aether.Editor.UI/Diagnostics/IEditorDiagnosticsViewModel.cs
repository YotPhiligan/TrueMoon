using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.UI.Diagnostics;

public interface IEditorDiagnosticsViewModel : System.ComponentModel.INotifyPropertyChanged
{
    void ObservePresentation(string backend, int width, int height, float scale, string? device);
    /// <summary>Whether the next report explicitly includes producer text and source paths.</summary>
    bool ExportIncludesText { get; set; }

    /// <summary>Whether the next report reads Core's configured persistent JSONL history.</summary>
    bool ExportIncludesHistory { get; set; }

    /// <summary>Whether an export can still accept a user cancellation request.</summary>
    bool CanCancelExport { get; }

    /// <summary>Whether history/text controls can be changed.</summary>
    bool CanChangeExportOptions { get; }

    /// <summary>Explicit data-sharing policy for the chosen report.</summary>
    string ExportPrivacyNotice { get; }

    TimeSpan RefreshInterval { get; }
    bool CollectorAvailable { get; }
    IReadOnlyList<EditorDiagnosticEntry> Entries { get; }
    int RetainedCount { get; }

    /// <summary>Conservative payload estimate including per-record/property overhead; not a managed heap measurement.</summary>
    int RetainedBytes { get; }

    long Cursor { get; }

    /// <summary>Collector sequences not returned to this console; this is not a sink/durability counter.</summary>
    long UnavailableSequences { get; }

    long PresentationEvicted { get; }
    long PresentationTruncated { get; }
    string Details { get; }
    string? SelectedId { get; }
    string Summary { get; }
    string ExportStatus { get; }
    bool IsExporting { get; }
    bool CanExport { get; }
    string ExportPath { get; set; }
    bool IsOpen { get; set; }
    bool FollowTail { get; set; }
    bool GroupRepeats { get; set; }
    DiagnosticLevel? MinimumLevel { get; set; }
    string Search { get; set; }
    string Source { get; set; }
    string EventName { get; set; }
    string Session { get; set; }
    string Generation { get; set; }
    string RuntimeRole { get; set; }
    IReadOnlyList<EditorDiagnosticRow> Rows { get; }
    ProcessDiagnosticBatch? Processes { get; }
    DiagnosticHealthSnapshot? CollectorHealth { get; }
    bool CanClear { get; }
    bool CanFilter { get; }
    bool CanRefreshDetails { get; }
    EditorDiagnosticMode Mode { get; set; }

    /// <summary>Whether the selected compiler diagnostic can be sent to the configured source editor.</summary>
    bool CanOpenSource { get; }

    /// <summary>Observed navigation outcome; completion acknowledges a request rather than another application's UI.</summary>
    string NavigationStatus { get; }

    /// <summary>Requests cooperative cancellation. Admission remains closed until the accepted task has drained.</summary>
    void CancelExport();

    /// <summary>Copies a value-only presentation observation on the UI owner; no renderer/device is retained.</summary>
    /// <param name="presentation">Observed backend and viewport; null clears the capability.</param>

    void Toggle();

    /// <summary>Reads at most one bounded batch. Call on the presentation owner, at RefreshInterval or slower.</summary>
    void Refresh();

    /// <summary>Clears presentation only. Collector and persistent JSONL remain unchanged.</summary>
    void Clear();

    void Select(string? id);

    /// <summary>Starts one explicit partial export. Existing files are never overwritten.</summary>
    void Export();

    /// <summary>Explicitly updates the inspected raw/health snapshot. Ordinary polling preserves selection and the inspected text.</summary>
    void RefreshDetails();

    /// <summary>Starts one explicit request over the frozen selected location. No file IO or process launch runs on the UI owner.</summary>
    void OpenSource();
}
