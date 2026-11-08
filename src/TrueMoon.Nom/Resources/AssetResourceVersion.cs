namespace TrueMoon.Nom.Resources;

/// <summary>Exact immutable resource identity; a changed logical head does not change this version.</summary>
/// <param name="AssetId">Logical asset identifier.</param><param name="KindId">Consumer-defined kind.</param>
/// <param name="Revision">Canonical revision digest including metadata.</param><param name="Digest">Canonical SHA256 of resource bytes.</param>
/// <param name="Length">Resource byte count.</param>
public sealed record AssetResourceVersion(Guid AssetId, Guid KindId, string Revision, string Digest, long Length);
