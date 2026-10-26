using System.Text.Json;

namespace TrueMoon.Diagnostics;

/// <summary>Writes JSON on a sink worker and borrows, rather than disposes, Console.Out.</summary>
public sealed class ConsoleDiagnosticSink : IDiagnosticSink
{
    /// <inheritdoc />
    public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        => await Console.Out.WriteLineAsync(JsonSerializer.Serialize(record).AsMemory(), cancellationToken).ConfigureAwait(false);
    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
