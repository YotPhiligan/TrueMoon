using System.Diagnostics;

namespace TrueMoon.Diagnostics;

/// <summary>Borrowed operation instrumentation. Implementations isolate observer failures from application work.</summary>
public interface IDiagnosticTelemetry
{
    /// <summary>Starts a bounded named operation, or returns null when no observer needs it.
    /// Properties are evaluated only for a trace requesting all data. Dispose the result in the starting async flow.</summary>
    IDiagnosticOperation? StartOperation(string name, Func<IReadOnlyDictionary<string, string?>>? properties = null);

    /// <summary>Starts a client/server operation with an optional explicit W3C parent.
    /// Null uses the current async flow. Existing implementations fall back to their original operation API.</summary>
    IDiagnosticOperation? StartOperation(string name, ActivityKind kind, ActivityContext? parentContext,
        Func<IReadOnlyDictionary<string, string?>>? properties = null) => StartOperation(name, properties);
}
