namespace TrueMoon.Diagnostics;

/// <summary>Bounded status of one sink; queued counts include its executing write.</summary>
/// <param name="Name">Sink identity within this pipeline.</param>
/// <param name="Accepted">Events admitted since creation.</param>
/// <param name="Written">Successfully written events.</param>
/// <param name="Dropped">Events rejected by count or byte admission limits.</param>
/// <param name="Failures">Failed write or cleanup attempts.</param>
/// <param name="Queued">Queued plus executing records.</param>
/// <param name="Bytes">Estimated retained bytes of queued and executing records.</param>
/// <param name="LastError">Bounded description of the most recent failure.</param>
public sealed record DiagnosticSinkStatus(string Name, long Accepted, long Written, long Dropped,
    long Failures, int Queued, long Bytes, string? LastError);
