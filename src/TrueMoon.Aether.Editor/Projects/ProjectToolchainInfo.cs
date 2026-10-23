namespace TrueMoon.Aether.Editor.Projects;

/// <summary>SDK identity observed during project preflight; compilation compatibility is checked by an explicit build.</summary>
/// <param name="SdkVersion">Selected dotnet SDK version.</param>
public sealed record ProjectToolchainInfo(string SdkVersion);
