namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Validated inputs to the project session factory.</summary>
/// <param name="Project">Resolved manifest.</param>
/// <param name="Toolchain">Successful selected SDK preflight.</param>
public sealed record ProjectSessionArguments(ResolvedProject Project, ProjectToolchainInfo Toolchain);
