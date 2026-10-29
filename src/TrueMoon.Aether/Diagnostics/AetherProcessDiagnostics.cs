using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Diagnostics;

/// <summary>Non-disposable memory view over App-owned runtime/compiler services; never releases or waits for those owners.</summary>
public sealed class AetherProcessDiagnostics : IProcessDiagnosticsSource
{
    private readonly IRuntimeDiagnostics? _runtime;
    private readonly GameBuildCompiler _compiler;
    public AetherProcessDiagnostics(IRuntimeFactory runtime, GameBuildCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(runtime); ArgumentNullException.ThrowIfNull(compiler);
        _runtime = runtime as IRuntimeDiagnostics; _compiler = compiler;
    }

    public ProcessDiagnosticBatch Read(int maximumCount, int maximumOutputCharacters)
    {
        ProcessDiagnosticSnapshot.ValidateLimits(maximumCount, maximumOutputCharacters);
        var runtime = _runtime?.ReadDiagnostics(maximumCount, maximumOutputCharacters) ?? [];
        var builds = _compiler.ReadDiagnostics(maximumCount, maximumOutputCharacters);
        return new(_runtime != null, true, Array.AsReadOnly(runtime.Concat(builds).OrderByDescending(item => item.StartedUtc)
            .Take(maximumCount).ToArray()));
    }
}
