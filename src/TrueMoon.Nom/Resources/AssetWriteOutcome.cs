namespace TrueMoon.Nom.Resources;

/// <summary>Detached operation journal outcome.</summary>
/// <param name="Status">One of pending, committed or aborted.</param><param name="Generation">Committed generation, otherwise null.</param>
/// <param name="Resources">Exact versions only for a committed operation.</param>
public sealed record AssetWriteOutcome(string Status, long? Generation, AssetResourceVersion[] Resources);
