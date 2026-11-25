namespace TrueMoon.Aether.Builds;


/// <summary>A child bootstrap reference to an already-published, validated artifact.</summary>
/// <param name="DirectoryPath">Absolute artifact directory.</param>
/// <param name="BuildId">Expected manifest SHA256.</param>
public sealed record GameBuildReference(string DirectoryPath, string BuildId);
