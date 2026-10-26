namespace TrueMoon.Diagnostics;

/// <summary>A pipeline-owned output. Calls are serialized on its worker, never on the producer thread.</summary>
public interface IDiagnosticSink : IAsyncDisposable
{
    /// <summary>Writes one already bounded snapshot.</summary>
    /// <param name="record">Immutable event data.</param>
    /// <param name="cancellationToken">Cooperative IO cancellation.</param>
    /// <returns>Completion of the write.</returns>
    ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default);
}
