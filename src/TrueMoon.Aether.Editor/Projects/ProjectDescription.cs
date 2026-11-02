namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Portable project metadata. Paths are relative to the .aetherproj directory; preferences are not project data.</summary>
/// <param name="FormatVersion">Manifest schema version:1 for existing projects,2 for private dependencies,3 for a local package directory.</param>
/// <param name="Id">Persistent nonempty project identity.</param>
/// <param name="Name">Display name, up to 128 characters.</param>
/// <param name="GameProject">Existing game .csproj within the project directory.</param>
/// <param name="EntryAssembly">Flat managed DLL file name.</param>
/// <param name="EntryType">Qualified game entry type name.</param>
/// <param name="ScenesRoot">Existing scene directory within the project directory.</param>
/// <param name="ContentRoot">Existing content directory within the project directory.</param>
public sealed record ProjectDescription(int FormatVersion, Guid Id, string Name, string GameProject,
    string EntryAssembly, string EntryType, string ScenesRoot, string ContentRoot)
{
    /// <summary>Explicit flat private managed DLL names admitted to the game artifact; empty for existing manifests.</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    /// <summary>Optional project-relative local nupkg directory. Requires version3; null preserves earlier manifest fields.</summary>
    public string? PackageDirectory { get; init; }
    /// <summary>Gets the current manifest schema version. Versions1/2 remain readable and writable.</summary>
    public const int CurrentVersion = 3;
}
