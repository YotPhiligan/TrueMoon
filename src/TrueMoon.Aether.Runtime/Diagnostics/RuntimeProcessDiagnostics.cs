using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime.Diagnostics;

/// <summary>Borrowed in-memory process snapshots; Runtime never activates a compiler.</summary>
public sealed class RuntimeProcessDiagnostics(IRuntimeFactory factory) : IProcessDiagnosticsSource
{
    public ProcessDiagnosticBatch Read(int maximumCount, int maximumOutputCharacters)
    {
        ProcessDiagnosticSnapshot.ValidateLimits(maximumCount, maximumOutputCharacters);
        var source = factory as IRuntimeDiagnostics;
        return new(source != null, false, source?.ReadDiagnostics(maximumCount, maximumOutputCharacters) ?? []);
    }
}
