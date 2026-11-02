namespace TrueMoon.Aether.Builds;

/// <summary>Process-local identity of the supported project closure frozen before MSBuild evaluation.</summary>
/// <remarks>The installed Windows SDK/runtime are trusted, pinned and read-leased for the job. This is not a sandbox for arbitrary MSBuild code.</remarks>
public sealed record GameBuildPreparationSnapshot
{
    internal GameBuildPreparationSnapshot(string sdkVersion, string runtimeVersion, string toolchainFingerprint,
        string environmentFingerprint, string fingerprint, IReadOnlyList<GameBuildInputFile> files,
        double elapsedMilliseconds, double sourceMilliseconds, double toolchainMilliseconds, int toolchainFiles, long toolchainBytes,
        double toolchainEnumerationMilliseconds, double toolchainOpenMilliseconds, double toolchainHashMilliseconds,
        GameBuildSourceWatchDescriptor? sourceWatch = null, string? sourceWatchUnavailableReason = null)
    {
        SdkVersion = sdkVersion; RuntimeVersion = runtimeVersion; ToolchainFingerprint = toolchainFingerprint;
        EnvironmentFingerprint = environmentFingerprint; Fingerprint = fingerprint; Files = files;
        ElapsedMilliseconds = elapsedMilliseconds; SourceMilliseconds = sourceMilliseconds; ToolchainMilliseconds = toolchainMilliseconds;
        ToolchainFiles = toolchainFiles; ToolchainBytes = toolchainBytes;
        ToolchainEnumerationMilliseconds = toolchainEnumerationMilliseconds;
        ToolchainOpenMilliseconds = toolchainOpenMilliseconds; ToolchainHashMilliseconds = toolchainHashMilliseconds;
        SourceWatch = sourceWatch; SourceWatchUnavailableReason = sourceWatchUnavailableReason;
    }
    /// <summary>Exact installed SDK selected for this job.</summary>
    public string SdkVersion { get; }
    /// <summary>Exact Microsoft.NETCore.App runtime used for SDK and compiler processes.</summary>
    public string RuntimeVersion { get; }
    /// <summary>SHA256 identity of the read-leased SDK, reference pack, runtime and native host files.</summary>
    public string ToolchainFingerprint { get; }
    /// <summary>SHA256 identity of the explicit process environment, excluding owned job paths.</summary>
    public string EnvironmentFingerprint { get; }
    /// <summary>SHA256 identity of source closure, toolchain and environment policy.</summary>
    public string Fingerprint { get; }
    /// <summary>Original identities of copied project/import/source/reference/configuration files.</summary>
    public IReadOnlyList<GameBuildInputFile> Files { get; }
    /// <summary>Process-local source-only projection; null when its watch-specific limits or paths are unavailable.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public GameBuildSourceWatchDescriptor? SourceWatch { get; }
    /// <summary>Reason the watch projection is unavailable, without invalidating frozen preparation.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SourceWatchUnavailableReason { get; }
    /// <summary>Wall time spent freezing inputs, excluding subsequent SDK restore/evaluation. Not part of content identity.</summary>
    public double ElapsedMilliseconds { get; }
    /// <summary>Wall time selecting the profile and copying/validating source, imports, references and configuration.</summary>
    public double SourceMilliseconds { get; }
    /// <summary>Wall time enumerating, read-leasing and hashing the installed toolchain.</summary>
    public double ToolchainMilliseconds { get; }
    /// <summary>Wall time enumerating installed files and validating their paths.</summary>
    public double ToolchainEnumerationMilliseconds { get; }
    /// <summary>Accumulated wall time rechecking paths, opening read leases and checking byte budgets.</summary>
    public double ToolchainOpenMilliseconds { get; }
    /// <summary>Accumulated wall time reading and hashing all leased file bytes.</summary>
    public double ToolchainHashMilliseconds { get; }
    /// <summary>Number of installed SDK/runtime/host/reference-pack files read-leased by this capture.</summary>
    public int ToolchainFiles { get; }
    /// <summary>Total installed file bytes read and hashed by this capture.</summary>
    public long ToolchainBytes { get; }
}
