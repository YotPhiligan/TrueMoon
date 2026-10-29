namespace TrueMoon.Aether.Diagnostics;

/// <summary>Independent raw stream data. No severity or compiler classification is inferred from its stream.</summary>
public sealed record ProcessStreamDiagnostic(ProcessOutputSnapshot Output, bool ReadFinished, bool FileFinished,
    string? ReadFailureType, string? FileFailureType)
{
    internal static ProcessStreamDiagnostic? Read(ProcessOutputCapture? capture, int maximumCharacters)
        => capture == null ? null : new(capture.Read(maximumCharacters), capture.ReadCompletion.IsCompleted, capture.FileCompletion.IsCompleted,
            Failure(capture.ReadCompletion), Failure(capture.FileCompletion));

    internal ProcessStreamDiagnostic Limit(int maximumCharacters) => this with { Output = Output.Limit(maximumCharacters) };
    private static string? Failure(Task task) => task.IsFaulted ? task.Exception?.GetBaseException().GetType().FullName : null;
}

/// <summary>Bounded memory snapshot with public correlation only. Never includes nonce, endpoint, environment or project contents.</summary>
public sealed record ProcessDiagnosticSnapshot(string Id, string Source, DateTimeOffset StartedUtc, int? ProcessId,
    Guid? SessionId, ulong? Generation, string? RuntimeRole, string? GameBuildId, string? JobId, long? SourceRevision,
    bool Completed, bool ResourcesReleased, ProcessStreamDiagnostic? StandardOutput, ProcessStreamDiagnostic? StandardError,
    ChildDiagnosticStatus? Delivery)
{
    internal ProcessDiagnosticSnapshot Limit(int maximumCharacters) => this with
    { StandardOutput = StandardOutput?.Limit(maximumCharacters), StandardError = StandardError?.Limit(maximumCharacters) };

    internal static void ValidateLimits(int maximumCount, int maximumCharacters)
    {
        if (maximumCount is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(maximumCount));
        if (maximumCharacters is < 0 or > 32768) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
    }
}

/// <summary>Available capture capabilities and most recent runtime/build snapshots; not persistent disk history.</summary>
public sealed record ProcessDiagnosticBatch(bool RuntimeAvailable, bool BuildAvailable, IReadOnlyList<ProcessDiagnosticSnapshot> Processes);

/// <summary>Optional runtime factory capability. Consumers borrow snapshots, never the underlying process leases.</summary>
public interface IRuntimeDiagnostics
{
    /// <summary>Returns at most64 processes, at most32768characters per stream. No IO, flush or waiting.</summary>
    IReadOnlyList<ProcessDiagnosticSnapshot> ReadDiagnostics(int maximumCount, int maximumOutputCharacters);
}

/// <summary>App-owned aggregation over borrowed process owners. Reading performs no IO or IPC.</summary>
public interface IProcessDiagnosticsSource
{
    ProcessDiagnosticBatch Read(int maximumCount, int maximumOutputCharacters);
}
