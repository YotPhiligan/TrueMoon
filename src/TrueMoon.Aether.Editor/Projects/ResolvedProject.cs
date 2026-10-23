namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Validated project metadata and canonical paths. No files, runtime or document are held open.</summary>
/// <param name="Description">Portable normalized metadata.</param>
/// <param name="ManifestPath">Absolute .aetherproj path.</param>
/// <param name="RootDirectory">Manifest directory.</param>
/// <param name="GameProjectPath">Absolute existing game project path.</param>
/// <param name="ScenesDirectory">Absolute existing scenes directory.</param>
/// <param name="ContentDirectory">Absolute existing content directory.</param>
public sealed record ResolvedProject(ProjectDescription Description, string ManifestPath, string RootDirectory,
    string GameProjectPath, string ScenesDirectory, string ContentDirectory);
