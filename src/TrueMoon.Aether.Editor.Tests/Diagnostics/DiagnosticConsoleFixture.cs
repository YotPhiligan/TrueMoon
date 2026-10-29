using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Editor.Tests.Diagnostics;

internal sealed class DiagnosticConsoleFixture : IDiagnosticRecordReader, IDisposable
{
    internal static readonly Guid Application = Guid.Parse("e5f7e36e-321c-44c6-9ec1-a2ff376fc309");
    public List<DiagnosticRecord> Records { get; } = [];
    public long LatestSequence { get; set; }
    public int Reads, LastMaximum, Disposals;
    public IReadOnlyList<DiagnosticRecord> ReadAfter(long afterSequence, int maximumCount)
    { Reads++; LastMaximum = maximumCount; return Records.Where(record => record.Sequence > afterSequence).Take(maximumCount).ToArray(); }
    public void Add(params DiagnosticRecord[] records) { Records.AddRange(records); LatestSequence = Records.Max(record => record.Sequence); }
    public EditorDiagnosticsViewModel Model(EditorDiagnosticsOptions? options = null) => new(this, options ?? new(), new());
    public static DiagnosticRecord Record(long sequence, string message = "started", string generation = "1", string session = "session-a",
        DiagnosticLevel level = DiagnosticLevel.Warning) => new(DateTimeOffset.Parse("2026-10-29T12:00:00Z"), sequence, Application,
        123, "TrueMoon.Runtime", level, 1201, "Runtime.Started", message,
        new Dictionary<string, string?> { ["SessionId"] = session, ["Generation"] = generation, ["RuntimeRole"] = "Game", ["GameBuildId"] = "build-a" },
        null, null, null, null, null, null, false);
    public void Dispose() => Disposals++;
}
