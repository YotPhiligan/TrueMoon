using System.Globalization;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.UI.Diagnostics
{
    /// <summary>A displayed group of equivalent retained events. Count covers this console's retained window only.</summary>
    public sealed record EditorDiagnosticEntry(DiagnosticRecord Record, int Count)
    {
        public string Id => Record.Sequence.ToString(CultureInfo.InvariantCulture);
        public string Label => (Count > 1 ? $"×{Count} · " : "") +
                               $"{Record.TimestampUtc:HH:mm:ss.fff} · {Record.Level} · {Record.Source} · {Record.EventName} · {Record.MessageTemplate}".ReplaceLineEndings(" ");
    }
}
