using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Builds;

/// <summary>Inputs for one explicit trusted-local game project build. Dependencies are flat managed DLL file names.</summary>
/// <param name="ProjectPath">SDK-style net10.0 library consuming the AetherContractPath MSBuild property.</param>
/// <param name="EntryAssembly">Produced game DLL name.</param>
/// <param name="EntryType">Public IGameSession implementation providing the IGameSessionEntry activation factory.</param>
/// <param name="SourceRevision">Caller revision; build completion never changes the running session automatically.</param>
/// <param name="Dependencies">Explicit private managed DLLs; no native/assets/framework/contract copies.</param>
public sealed record GameBuildRequest(string ProjectPath, string EntryAssembly, string EntryType, long SourceRevision,
    IReadOnlyList<string>? Dependencies = null)
{
    /// <summary>Opt-in evaluated-input witness and rechecks around compilation/startup. Not a hermetic build sandbox.</summary>
    public bool TrackInputs { get; init; }
    /// <summary>Compile from owned byte copies of prepared C# inputs and SDK compiler files. Requires TrackInputs.</summary>
    /// <remarks>Without CapturePreparation, MSBuild preparation and the installed dotnet runtime remain trusted outside the byte capture.</remarks>
    public bool CaptureCompilation { get; init; }
    /// <summary>Freeze the supported declarative project closure before MSBuild, using pinned Windows SDK/runtime and an explicit environment.</summary>
    /// <remarks>Requires TrackInputs and CaptureCompilation. ProjectReference additionally requires CaptureProjectReferences.
    /// Custom tasks/functions are unsupported. Packages additionally require PackageDirectory.</remarks>
    public bool CapturePreparation { get; init; }
    /// <summary>Compile a frozen graph of at most four declarative net10.0 C# library projects.</summary>
    /// <remarks>Requires CapturePreparation. Only unconditional literal ProjectReference items are supported.
    /// Project directories must not overlap. Every private graph DLL must be declared in Dependencies;
    /// custom reference metadata remains unsupported.</remarks>
    public bool CaptureProjectReferences { get; init; }
    /// <summary>Explicit local directory of unsigned id.version.nupkg archives, or null to reject PackageReference.</summary>
    /// <remarks>Requires CapturePreparation and a packages.lock.json beside each package-consuming project.
    /// Only stable exact versions and a single managed lib target are supported; build, analyzer, native and content assets are rejected.
    /// Every package DLL must be declared in Dependencies. No package source, cache or network restore is consulted.</remarks>
    public string? PackageDirectory { get; init; }
    /// <summary>Reuse this compiler's last successful managed payload after full input and payload verification.</summary>
    /// <remarks>Requires CapturePreparation. Keeps one process-local result; a new source ticket republishes its manifest.
    /// Missing/changed inputs or unavailable payloads rebuild normally. No persisted cache or reopened provenance is trusted.</remarks>
    public bool ReuseArtifact { get; init; }
}

/// <summary>A child bootstrap reference to an already-published, validated artifact.</summary>
/// <param name="DirectoryPath">Absolute artifact directory.</param>
/// <param name="BuildId">Expected manifest SHA256.</param>
public sealed record GameBuildReference(string DirectoryPath, string BuildId);

/// <summary>A failed compiler exit with persistent stdout/stderr diagnostics.</summary>
public sealed class GameBuildException(string message, int exitCode, string diagnosticsDirectory)
    : IOException($"{message} Exit code: {exitCode}. Diagnostics: {diagnosticsDirectory}")
{
    /// <summary>Gets the observed compiler exit code.</summary>
    public int ExitCode { get; } = exitCode;
    /// <summary>Gets the owned job directory containing build.stdout.log/build.stderr.log.</summary>
    public string DiagnosticsDirectory { get; } = diagnosticsDirectory;
}
