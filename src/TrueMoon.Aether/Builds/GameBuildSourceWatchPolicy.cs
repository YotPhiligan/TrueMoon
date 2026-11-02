namespace TrueMoon.Aether.Builds;

/// <summary>The frozen preparation's recursive source census policy. Explicit inputs are not filtered by this policy.</summary>
public sealed class GameBuildSourceWatchPolicy
{
    /// <summary>Identity of the Windows, case-insensitive path and case-sensitive excluded-directory-name policy.</summary>
    public const string Identity = "frozen-source-windows-v1";
    /// <summary>Maximum deduplicated exact paths in each file/probe set.</summary>
    public const int MaximumPaths = GameBuildInputSnapshot.MaximumFiles;
    /// <summary>Maximum original project trees.</summary>
    public const int MaximumTrees = 4;
    /// <summary>Maximum entries visited across all project trees, including excluded entries.</summary>
    public const int MaximumEntries = 8192;
    /// <summary>Maximum directory depth below a project root.</summary>
    public const int MaximumDepth = 64;
    /// <summary>Maximum bytes of a single original input.</summary>
    public const long MaximumFileBytes = GameBuildInputSnapshot.MaximumFileBytes;
    /// <summary>Maximum original input bytes in one census.</summary>
    public const long MaximumTotalBytes = GameBuildInputSnapshot.MaximumTotalBytes;

    internal GameBuildSourceWatchPolicy(string excludedDirectory) => ExcludedDirectory = Path.GetFullPath(excludedDirectory);

    /// <summary>Absolute artifact-owner subtree excluded from recursive enumeration.</summary>
    public string ExcludedDirectory { get; }

    /// <summary>Tests one enumerated entry before descending into it, without accessing the filesystem.</summary>
    /// <remarks>Call for each directory on descent. A directory name retains the compiler's exact case-sensitive semantics.</remarks>
    public bool ExcludesEntry(string path, bool isDirectory) =>
        Contains(ExcludedDirectory, path) || isDirectory && Path.GetFileName(path) is "bin" or "obj" or ".git" or ".codex" or ".agents";

    internal static bool Contains(string directory, string path) =>
        StringComparer.OrdinalIgnoreCase.Equals(directory, path) ||
        path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
