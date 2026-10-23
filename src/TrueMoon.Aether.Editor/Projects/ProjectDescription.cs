namespace TrueMoon.Aether.Editor.Projects;

/// <summary>Portable project metadata. Paths are relative to the .aetherproj directory; preferences are not project data.</summary>
/// <param name="FormatVersion">Supported manifest schema version (currently 1).</param>
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
    /// <summary>Gets the only supported manifest schema version.</summary>
    public const int CurrentVersion = 1;
}
