using System.Globalization;
using TrueMoon.Aether.Sessions;
using TrueMoon.Diagnostics;
using TrueMoon.Mithril;

namespace TrueMoon.Aether;

internal static class AetherDiagnosticEvents
{
    internal static IDiagnosticOperation? Start(IDiagnosticTelemetry? telemetry, string name,
        Func<IReadOnlyDictionary<string, string?>> properties)
    {
        try { return telemetry?.StartOperation(name, properties) is { } operation ? new SafeOperation(operation) : null; }
        catch { return null; }
    }

    internal static async Task<T> ObserveAsync<T>(IEventsSource? source, IDiagnosticTelemetry? telemetry,
        string name, int eventId, Func<IReadOnlyDictionary<string, string?>> properties, Func<Task<T>> action)
    {
        using var operation = Start(telemetry, "Aether." + name, properties);
        Emit(source, DiagnosticLevel.Information, eventId, name + ".Starting", "Engine operation starting", properties);
        try
        {
            var result = await action().ConfigureAwait(false);
            Emit(source, DiagnosticLevel.Information, eventId + 1, name + ".Completed", "Engine operation completed", properties);
            operation?.Complete();
            return result;
        }
        catch (Exception error)
        {
            var cancelled = error is OperationCanceledException || error is GameReplacementException { InnerException: OperationCanceledException };
            var cause = error is GameReplacementException replacement ? replacement.InnerException ?? error : error;
            IReadOnlyDictionary<string, string?> FailureProperties()
            {
                var result = new Dictionary<string, string?>(properties());
                if (error is GameReplacementException failure)
                {
                    result["Phase"] = failure.Phase;
                    result["RollbackAttempted"] = failure.RollbackAttempted.ToString();
                    result["RollbackSucceeded"] = failure.RollbackSucceeded.ToString();
                    result["RecoveryErrorType"] = failure.RecoveryError?.GetType().FullName;
                }
                return result;
            }
            Emit(source, cancelled ? DiagnosticLevel.Warning : DiagnosticLevel.Error, eventId + (cancelled ? 2 : 3),
                name + (cancelled ? ".Cancelled" : ".Failed"), "Engine operation ended", FailureProperties, cause);
            operation?.Complete(cancelled ? DiagnosticOperationOutcome.Cancelled : DiagnosticOperationOutcome.Failed, cause);
            throw;
        }
    }

    internal static Task ObserveAsync(IEventsSource? source, IDiagnosticTelemetry? telemetry,
        string name, int eventId, Func<IReadOnlyDictionary<string, string?>> properties, Func<Task> action)
        => ObserveAsync(source, telemetry, name, eventId, properties, async () => { await action().ConfigureAwait(false); return true; });

    internal static Dictionary<string, string?> Context(SessionStatus status) => new()
    {
        ["SessionId"] = status.Identity?.SessionId.ToString("D"),
        ["Generation"] = status.Identity?.Generation.ToString(CultureInfo.InvariantCulture),
        ["GameBuildId"] = status.Identity?.GameBuildId,
        ["SceneRevision"] = status.SceneRevision?.ToString(CultureInfo.InvariantCulture),
        ["State"] = status.State.ToString()
    };

    private sealed class SafeOperation(IDiagnosticOperation inner) : IDiagnosticOperation
    {
        private int _completed, _disposed;
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null)
        { if (Interlocked.Exchange(ref _completed, 1) == 0) try { inner.Complete(outcome, error); } catch { } }
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) try { inner.Dispose(); } catch { } }
    }

    internal static void Session(IEventsSource source, SessionStatus status)
        => Emit(source, status.Failure is null ? DiagnosticLevel.Information : DiagnosticLevel.Error,
            1000, "Session.StateChanged", "Engine session changed state", () => new Dictionary<string, string?>
            {
                ["State"] = status.State.ToString(), ["SessionId"] = status.Identity?.SessionId.ToString("D"),
                ["Generation"] = status.Identity?.Generation.ToString(CultureInfo.InvariantCulture),
                ["GameBuildId"] = status.Identity?.GameBuildId, ["ProcessId"] = status.ProcessId?.ToString(CultureInfo.InvariantCulture),
                ["Tick"] = status.Tick.ToString(CultureInfo.InvariantCulture),
                ["SceneRevision"] = status.SceneRevision?.ToString(CultureInfo.InvariantCulture),
                ["Phase"] = status.Failure?.Phase
            }, status.Failure?.Cause);

    internal static void Process(IEventsSource? source, SessionLaunch launch, UnitProcessDiagnostic process)
    {
        if (source is null) return;
        Emit(source, process.Error is null ? DiagnosticLevel.Information : DiagnosticLevel.Error,
            1100, "Runtime.Process", "Owned runtime process transition", () => new Dictionary<string, string?>
            {
                ["SessionId"] = launch.Identity.SessionId.ToString("D"),
                ["Generation"] = launch.Identity.Generation.ToString(CultureInfo.InvariantCulture),
                ["GameBuildId"] = launch.Identity.GameBuildId, ["Role"] = launch.Role.ToString(),
                ["RunId"] = process.RunId.ToString("D"), ["ProcessGeneration"] = process.Generation.ToString(CultureInfo.InvariantCulture),
                ["ProcessId"] = process.ProcessId?.ToString(CultureInfo.InvariantCulture),
                ["Stage"] = process.Stage.ToString(), ["Reason"] = process.Reason,
                ["ExitCode"] = process.ExitCode?.ToString(CultureInfo.InvariantCulture)
            }, process.Error);
    }

    internal static void Emit(IEventsSource? source, DiagnosticLevel level, int id, string name,
        string message, Func<IReadOnlyDictionary<string, string?>> properties, Exception? error = null)
    {
        // Diagnostics must not alter lifecycle outcomes, including custom legacy sources.
        try { source?.Write(level, id, name, message, properties, error); } catch (Exception) { }
    }
}
