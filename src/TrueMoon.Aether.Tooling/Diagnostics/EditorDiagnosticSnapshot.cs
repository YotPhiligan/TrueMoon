using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Tooling.Diagnostics
{
    /// <summary>Immutable report input, detached from the UI and the borrowed collector.</summary>
    public sealed record EditorDiagnosticSnapshot(IReadOnlyList<DiagnosticRecord> Records, long Cursor, long UnavailableSequences,
        long PresentationEvicted, long PresentationTruncated, bool CollectorAvailable)
    {
        /// <summary>Effective presentation limits, when captured by the console model.</summary>
        public EditorDiagnosticsOptions? PresentationLimits { get; init; }
        public TrueMoon.Diagnostics.DiagnosticHealthSnapshot? Health { get; init; }
        public ProcessDiagnosticBatch? Processes { get; init; }
        /// <summary>Actual value-only presentation observation, when a UI session has reported one.</summary>
        public EditorDiagnosticPresentation? Presentation { get; init; }
    }
}