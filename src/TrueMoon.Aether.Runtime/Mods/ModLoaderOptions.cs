using System.Reflection;
using System.Runtime.Loader;
using TrueMoon.Aether.Mods;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Host composition policy, copied before use. Loading managed modules is not a security sandbox.</summary>
public sealed class ModLoaderOptions
{
    /// <summary>Maximum modules in a single atomic group.</summary>
    public int MaximumModules { get; set; } = 32;
    /// <summary>Maximum managed files per module.</summary>
    public int MaximumFilesPerModule { get; set; } = 64;
    /// <summary>Maximum raw JSON bytes per manifest.</summary>
    public int MaximumManifestBytes { get; set; } = 128 * 1024;
    /// <summary>Maximum combined private assembly bytes per group.</summary>
    public long MaximumArtifactBytes { get; set; } = 128 * 1024 * 1024;
    /// <summary>Additional stable host APIs shared by exact identity/hash. Abstractions and Contracts are always shared.</summary>
    public Assembly[] AdditionalSharedAssemblies { get; set; } = [];

    internal ModLoaderOptions Snapshot()
    {
        if (MaximumModules is < 1 or > 256 || MaximumFilesPerModule is < 1 or > 256 ||
            MaximumManifestBytes is < 256 or > 1024 * 1024 || MaximumArtifactBytes is < 1024 or > 1024L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(ModLoaderOptions));
        ArgumentNullException.ThrowIfNull(AdditionalSharedAssemblies);
        var copy = AdditionalSharedAssemblies.ToArray();
        if (copy.Length > 32 || copy.Any(a => a == null || a.IsDynamic || string.IsNullOrEmpty(a.Location) ||
            AssemblyLoadContext.GetLoadContext(a) != AssemblyLoadContext.Default))
            throw new ArgumentException("Shared APIs must be on-disk assemblies in the default context.");
        return new() { MaximumModules = MaximumModules, MaximumFilesPerModule = MaximumFilesPerModule,
            MaximumManifestBytes = MaximumManifestBytes, MaximumArtifactBytes = MaximumArtifactBytes, AdditionalSharedAssemblies = copy };
    }

    internal Assembly[] Shared() => new[] { typeof(IModEntry).Assembly, typeof(IFactory<>).Assembly }
        .Concat(AdditionalSharedAssemblies).Distinct().ToArray();
}

/// <summary>Immutable group request. Supply the complete transitive dependency closure, with no unrelated modules.</summary>
public sealed class ModLoadRequest
{
    /// <summary>Copies artifact paths. Each directory must be named by the SHA256 of its manifest.json bytes.</summary>
    /// <param name="rootModuleId">Requested module.</param><param name="artifactDirectories">Complete closure of content-addressed directories.</param>
    public ModLoadRequest(string rootModuleId, IEnumerable<string> artifactDirectories)
    {
        ArgumentNullException.ThrowIfNull(artifactDirectories);
        RootModuleId = rootModuleId; ArtifactDirectories = Array.AsReadOnly(artifactDirectories.ToArray());
    }
    /// <summary>Requested module ID.</summary>
    public string RootModuleId { get; }
    /// <summary>Complete frozen directory list.</summary>
    public IReadOnlyList<string> ArtifactDirectories { get; }
}
