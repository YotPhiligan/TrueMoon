namespace TrueMoon.Diagnostics;

// Non-disposable views prevent DI aliases from independently disposing the pipeline.
internal sealed class DiagnosticRecordAccess(DiagnosticPipeline pipeline) : IDiagnosticRecordReader, IDiagnosticRecordImporter, IDiagnosticHealthReader,
    IDiagnosticArchiveReader, IDiagnosticConfigurationReader
{
    public long LatestSequence => pipeline.LatestSequence;
    public IReadOnlyList<DiagnosticRecord> ReadAfter(long afterSequence, int maximumCount) => pipeline.ReadAfter(afterSequence, maximumCount);
    public bool TryImport(DiagnosticRecord record, IReadOnlyDictionary<string, string?>? context = null) => pipeline.TryImport(record, context);
    public DiagnosticHealthSnapshot ReadHealth() => pipeline.ReadHealth();
    public DiagnosticCollectorConfiguration ReadConfiguration() => pipeline.ReadConfiguration();
    public Task<DiagnosticHistorySnapshot> ReadHistoryAsync(DiagnosticHistoryReadOptions options, CancellationToken cancellationToken = default)
        => pipeline.ReadHistoryAsync(options, cancellationToken);
}
