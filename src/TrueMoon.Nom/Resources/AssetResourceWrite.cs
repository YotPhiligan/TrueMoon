using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Resources;

/// <summary>One resource descriptor; bytes are supplied separately through a borrowed Stream.</summary>
/// <param name="Asset">Logical identity and metadata.</param><param name="ExpectedLength">Exact byte count, or null when unknown.</param>
/// <param name="ExpectedDigest">Canonical lowercase SHA256, or null when unknown.</param><param name="MaximumBytes">Positive ceiling, at most512MiB.</param>
public sealed record AssetResourceWrite(AssetRecord Asset, long? ExpectedLength = null, string? ExpectedDigest = null, long MaximumBytes = 512L * 1024 * 1024);
