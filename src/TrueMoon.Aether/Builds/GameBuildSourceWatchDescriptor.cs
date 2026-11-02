using System.Security.Cryptography;
using System.Text.Json;

namespace TrueMoon.Aether.Builds;

/// <summary>Immutable, process-local source projection of a successful frozen capture.</summary>
/// <remarks>Contains no live resources or toolchain census. This is not an input-verification or reopening witness.</remarks>
public sealed class GameBuildSourceWatchDescriptor
{
    internal GameBuildSourceWatchDescriptor(string projectPath, GameBuildSourceWatchPolicy policy,
        IEnumerable<GameBuildInputFile> files, IEnumerable<string> sourceTrees, IEnumerable<GameBuildSourceWatchProbe> probes,
        IEnumerable<string> treeFiles)
    {
        ProjectPath = projectPath; Policy = policy;
        Files = Array.AsReadOnly(files.OrderBy(file => Canonical(file.Path), StringComparer.Ordinal).ToArray());
        SourceTrees = Array.AsReadOnly(sourceTrees.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Canonical, StringComparer.Ordinal).ToArray());
        Probes = Array.AsReadOnly(probes.OrderBy(probe => Canonical(probe.Path), StringComparer.Ordinal).ToArray());
        TreeFiles = Array.AsReadOnly(treeFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Canonical, StringComparer.Ordinal).ToArray());
        // An artifact owner outside every source tree cannot affect recursive membership.
        // Neither the job directory nor toolchain/environment/evaluation identities enter this projection.
        var excluded = SourceTrees.Any(tree => GameBuildSourceWatchPolicy.Contains(tree, policy.ExcludedDirectory))
            ? Canonical(policy.ExcludedDirectory) : null;
        SourceFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, Policy = GameBuildSourceWatchPolicy.Identity, ExcludedDirectory = excluded,
            ProjectPath = Canonical(ProjectPath), SourceTrees = SourceTrees.Select(Canonical),
            TreeFiles = TreeFiles.Select(Canonical),
            Files = Files.Select(file => new { Path = Canonical(file.Path), file.Length, file.Sha256 }),
            Probes = Probes.Select(probe => new { Path = Canonical(probe.Path), probe.Exists })
        })));
    }

    /// <summary>Source descriptor format version; not a persisted artifact schema.</summary>
    public int Version => 1;
    /// <summary>Original root project path.</summary>
    public string ProjectPath { get; }
    /// <summary>Shared compiler/observer recursive census policy.</summary>
    public GameBuildSourceWatchPolicy Policy { get; }
    /// <summary>Deduplicated original file identities, including linked files, HintPaths, locks and nupkg archives.</summary>
    public IReadOnlyList<GameBuildInputFile> Files { get; }
    /// <summary>Original project directories; recursively census file membership with Policy, without scanning their ancestors.</summary>
    /// <remarks>All copied files affect membership, including non-Compile files. Empty directories alone do not.</remarks>
    public IReadOnlyList<string> SourceTrees { get; }
    /// <summary>Original file paths actually observed by recursive tree enumeration, distinct from explicit inputs.</summary>
    /// <remarks>Preserves membership even when an explicit excluded input uses a different Windows path spelling.</remarks>
    public IReadOnlyList<string> TreeFiles { get; }
    /// <summary>Exact present/absent import, ancestor configuration and forbidden response-file paths.</summary>
    public IReadOnlyList<GameBuildSourceWatchProbe> Probes { get; }
    /// <summary>SHA256 of source bytes, file membership, probe presence and policy, independent of compile tickets and staging.</summary>
    public string SourceFingerprint { get; }

    private static string Canonical(string path) => path.ToUpperInvariant();
}
