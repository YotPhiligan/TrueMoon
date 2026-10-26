using System.Diagnostics;
using System.Globalization;
using TrueMoon.Diagnostics;

namespace TrueMoon.Enerit.IO.Pipes;

internal static class PipesDiagnostics
{
    // Continue accepted wire context through nested Enerit calls even when sampling creates no Activity.
    // Only fixed-size IDs/flags enter this carrier; it never contains baggage, Activity or payload objects.
    private static readonly AsyncLocal<ActivityContext?> Remote = new();
    internal static ActivityContext? RemoteContext { get => Remote.Value; set => Remote.Value = value; }

    internal static ActivityContext? CurrentContext()
    {
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity)
        {
            var context = new ActivityContext(activity.TraceId, activity.SpanId, activity.ActivityTraceFlags);
            if (PipesRequestHeader.IsValid(context)) return context;
        }
        return Remote.Value;
    }

    internal static IDiagnosticOperation? Start(IDiagnosticTelemetry? telemetry, string name, ActivityKind kind,
        ActivityContext? parent, Func<IReadOnlyDictionary<string, string?>> properties)
    {
        try { return telemetry?.StartOperation(name, kind, parent, properties) is { } operation ? new SafeOperation(operation) : null; }
        catch { return null; }
    }
    internal static Dictionary<string, string?> Properties(string service, Guid request, Guid owner, byte method, bool server)
        => new()
        {
            ["Service"] = service, ["RequestId"] = request.ToString("D"),
            [server ? "ConnectionId" : "ClientId"] = owner.ToString("D"),
            ["MethodCode"] = method.ToString(CultureInfo.InvariantCulture)
        };
    internal static void Emit(IEventsSource source, bool enabled, int id, string name,
        Func<IReadOnlyDictionary<string, string?>> properties, Exception? error = null)
    {
        if (!enabled) return;
        try { source.Write(error == null ? DiagnosticLevel.Information : error is OperationCanceledException ? DiagnosticLevel.Warning : DiagnosticLevel.Error,
            id, name, "Pipes invocation transition", properties, error); } catch { }
    }
    internal static void Legacy(Action emit) { try { emit(); } catch { } }

    private sealed class SafeOperation(IDiagnosticOperation inner) : IDiagnosticOperation
    {
        private int _completed, _disposed;
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null)
        { if (Interlocked.Exchange(ref _completed, 1) == 0) try { inner.Complete(outcome, error); } catch { } }
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) try { inner.Dispose(); } catch { } }
    }
}
