namespace TrueMoon.Aether.Tooling.Builds;

/// <summary>An exact original path consulted by frozen preparation, including expected absence.</summary>
public sealed record GameBuildSourceWatchProbe(string Path, bool Exists);
