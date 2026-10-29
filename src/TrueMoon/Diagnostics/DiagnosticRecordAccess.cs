namespace TrueMoon.Diagnostics;

// Non-disposable views prevent DI aliases from independently disposing the pipeline.
internal sealed class DiagnosticRecordAccess(DiagnosticPipeline pipeline) : IDiagnosticRecordReader, IDiagnosticRecordImporter, IDiagnosticHealthReader
{
    public long LatestSequence => pipeline.LatestSequence;
    public IReadOnlyList<DiagnosticRecord> ReadAfter(long afterSequence, int maximumCount) => pipeline.ReadAfter(afterSequence, maximumCount);
    public bool TryImport(DiagnosticRecord record, IReadOnlyDictionary<string, string?>? context = null) => pipeline.TryImport(record, context);
    public DiagnosticHealthSnapshot ReadHealth() => pipeline.ReadHealth();
}
