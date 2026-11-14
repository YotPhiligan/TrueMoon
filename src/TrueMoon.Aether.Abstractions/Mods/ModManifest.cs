namespace TrueMoon.Aether.Abstractions.Mods;

/// <summary>Versioned, data-only description of a managed module artifact. Runtime validates every field before activation.</summary>
public sealed record ModManifest
{
    /// <summary>Supported manifest format.</summary>
    public const int CurrentFormatVersion = 1;
    /// <summary>Supported module contract generation.</summary>
    public const int CurrentApiVersion = 1;
    /// <summary>Manifest serialization version.</summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;
    /// <summary>Exact module API generation required.</summary>
    public int ApiVersion { get; init; } = CurrentApiVersion;
    /// <summary>Stable lowercase ASCII module identifier.</summary>
    public required string Id { get; init; }
    /// <summary>Canonical three-part nonnegative module version.</summary>
    public required string Version { get; init; }
    /// <summary>Atomic dependency-group identity.</summary>
    public required string ReloadGroup { get; init; }
    /// <summary>Only WholeGroup is supported in API 1.</summary>
    public string ReloadPolicy { get; init; } = "WholeGroup";
    /// <summary>File name of the managed entry assembly.</summary>
    public required string EntryAssembly { get; init; }
    /// <summary>Public, concrete IModEntryPoint implementation full name.</summary>
    public required string EntryType { get; init; }
    /// <summary>Declared dependency ranges; lower bound inclusive, upper bound exclusive.</summary>
    public ModDependency[] Dependencies { get; init; } = [];
    /// <summary>Complete flat private managed assembly catalog.</summary>
    public ModAssemblyFile[] Files { get; init; } = [];
    /// <summary>Exact host contract identities and content hashes. No private copy may shadow them.</summary>
    public ModSharedAssembly[] SharedAssemblies { get; init; } = [];
}

/// <summary>Required module and half-open version interval.</summary>
public sealed record ModDependency(string Id, string MinimumVersion, string MaximumVersionExclusive);
/// <summary>Managed file name, byte length and SHA256 content address.</summary>
public sealed record ModAssemblyFile(string Name, long Length, string Sha256);
/// <summary>Host-provided assembly identity and SHA256. Explicit additional host APIs require an allowlist.</summary>
public sealed record ModSharedAssembly(string Identity, string Sha256);
