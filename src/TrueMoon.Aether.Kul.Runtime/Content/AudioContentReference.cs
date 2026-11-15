namespace TrueMoon.Aether.Kul.Runtime.Content;

/// <summary>Explicit captured artifact identity. No project/cwd/source fallback is performed.</summary>
public sealed record AudioContentReference
{
    /// <summary>Absolute capture directory; final segment must equal ManifestSha256.</summary>
    public string DirectoryPath { get; }
    /// <summary>Raw audio-content.json SHA256.</summary>
    public string ManifestSha256 { get; }
    /// <summary>Validates identity syntax without IO.</summary><param name="directoryPath">Absolute capture directory.</param><param name="manifestSha256">Raw metadata hash.</param>
    public AudioContentReference(string directoryPath, string manifestSha256)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Path.IsPathFullyQualified(directoryPath)) throw new ArgumentException("An absolute capture directory is required.", nameof(directoryPath));
        if (!AudioContentManifest.IsHash(manifestSha256)) throw new ArgumentException("A full manifest SHA256 is required.", nameof(manifestSha256));
        DirectoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath)); ManifestSha256 = manifestSha256.ToUpperInvariant();
        if (!string.Equals(Path.GetFileName(DirectoryPath), ManifestSha256, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Capture directory must be named by the manifest hash.", nameof(directoryPath));
    }
}
