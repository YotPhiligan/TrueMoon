namespace TrueMoon.Aether.Scenes;

/// <summary>Content identity of the exact observed UTF8 file bytes.</summary>
/// <param name="Sha256">SHA256 digest, independent of timestamps.</param>
public sealed record SceneFileStamp(string Sha256);
