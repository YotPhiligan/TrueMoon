namespace TrueMoon.Aether.Builds;

/// <summary>Content identity of one observed SDK build input.</summary>
/// <param name="Path">Absolute input path.</param><param name="Length">Observed byte length.</param><param name="Sha256">SHA256 of the observed bytes.</param>
public sealed record GameBuildInputFile(string Path, long Length, string Sha256);
