using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Builds;

/// <summary>Inputs for one explicit trusted-local game project build. Dependencies are flat managed DLL file names.</summary>
/// <param name="ProjectPath">SDK-style net10.0 library consuming the AetherContractPath MSBuild property.</param>
/// <param name="EntryAssembly">Produced game DLL name.</param>
/// <param name="EntryType">Public IGameSession implementation providing the IGameSessionEntry activation factory.</param>
/// <param name="SourceRevision">Caller revision; build completion never changes the running session automatically.</param>
/// <param name="Dependencies">Explicit private managed DLLs; no native/assets/framework/contract copies.</param>
public sealed record GameBuildRequest(string ProjectPath, string EntryAssembly, string EntryType, long SourceRevision,
    IReadOnlyList<string>? Dependencies = null);

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
