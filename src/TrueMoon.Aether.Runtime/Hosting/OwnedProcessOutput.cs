using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace TrueMoon.Aether.Runtime.Hosting;

// Process.Close leaves accessed standard streams to the caller. This owner retains
// them across process reap and a timed-out wait, then closes them after capture drain.
internal sealed class OwnedProcessOutput
{
    private readonly Lock _gate = new();
    private readonly IDisposable[] _streams;
    private readonly bool[] _released;
    private Task? _drain;
    private string _stage = "NotStarted";
    private string? _lastFailureType;
    private long _startedUtcTicks, _finishedUtcTicks;
    private int _attempts, _releasedCount;

    internal const string DiagnosticKey = "TrueMoon.ProcessOutputDrain";
    internal string Owner { get; init; } = "process";
    internal int? ProcessId { get; init; }
    internal Guid? SessionId { get; init; }
    internal ulong? Generation { get; init; }
    internal string? JobId { get; init; }
    internal string CleanupStage => Volatile.Read(ref _stage);
    internal string? CleanupFailureType => Volatile.Read(ref _lastFailureType);

    internal OwnedProcessOutput(TextReader stdout, TextReader stderr, TextWriter? stdin, string directory, string prefix)
    {
        _streams = stdin == null ? [stdout, stderr] : [stdout, stderr, stdin];
        _released = new bool[_streams.Length];
        Stdout = new(stdout, filePath: Path.Combine(directory, prefix + ".stdout.log"));
        Stderr = new(stderr, filePath: Path.Combine(directory, prefix + ".stderr.log"));
    }

    internal ProcessOutputCapture Stdout { get; }
    internal ProcessOutputCapture Stderr { get; }

    // Only called after the process has been reaped. A wait timeout does not cancel
    // the actual drain or discard its readers, snapshots, file task, or failure.
    internal async Task DrainAsync(TimeSpan timeout)
    {
        Task drain;
        lock (_gate)
        {
            if (_drain == null || _drain.IsFaulted || _drain.IsCanceled) _drain = DrainCoreAsync();
            drain = _drain;
        }
        try { await drain.WaitAsync(timeout).ConfigureAwait(false); Volatile.Write(ref _lastFailureType, null); }
        catch (TimeoutException error) when (!drain.IsCompleted)
        {
            Volatile.Write(ref _lastFailureType, error.GetType().FullName);
            var diagnostic = ReadDiagnostics();
            var failure = new TimeoutException($"Process output drain timed out after process reap. " +
                $"stdout: {Describe(Stdout)}; stderr: {Describe(Stderr)}. " +
                $"Cleanup remains owned and can be retried. {diagnostic}", error);
            failure.Data[DiagnosticKey] = diagnostic;
            throw failure;
        }
        catch (Exception error)
        {
            Volatile.Write(ref _lastFailureType, error.GetType().FullName);
            error.Data[DiagnosticKey] = ReadDiagnostics();
            throw;
        }
    }

    // Read only memory, without taking the drain gate or performing IO. A blocked
    // stream Dispose must not block the failure snapshot. Never include raw output.
    internal string ReadDiagnostics()
    {
        ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
        return JsonSerializer.Serialize(new
        {
            Owner, ProcessId, SessionId, Generation, JobId, CapturedUtc = DateTimeOffset.UtcNow,
            DrainStartedUtc = Timestamp(Interlocked.Read(ref _startedUtcTicks)),
            DrainFinishedUtc = Timestamp(Interlocked.Read(ref _finishedUtcTicks)),
            DrainTask = Volatile.Read(ref _drain)?.Status.ToString(), CleanupStage, CleanupFailureType,
            Attempts = Volatile.Read(ref _attempts), ReleasedStreams = Volatile.Read(ref _releasedCount), Streams = _streams.Length,
            StdoutReleased = Volatile.Read(ref _released[0]), StderrReleased = Volatile.Read(ref _released[1]),
            StdinReleased = _streams.Length == 3 ? Volatile.Read(ref _released[2]) : (bool?)null,
            ThreadPoolWorkers = ThreadPool.ThreadCount, ThreadPoolPending = ThreadPool.PendingWorkItemCount,
            AvailableWorkers = availableWorkers, AvailableIo = availableIo,
            Stdout = Stdout.Read(0) with { FileError = null }, Stderr = Stderr.Read(0) with { FileError = null }
        });
        static DateTimeOffset? Timestamp(long ticks) => ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private async Task DrainCoreAsync()
    {
        Interlocked.Increment(ref _attempts);
        Interlocked.Exchange(ref _startedUtcTicks, DateTimeOffset.UtcNow.Ticks);
        Interlocked.Exchange(ref _finishedUtcTicks, 0);
        Volatile.Write(ref _stage, "CaptureDrain");
        try
        {
            var errors = new List<Exception>();
            var captures = Task.WhenAll(Stdout.Completion, Stderr.Completion);
            try { await captures.ConfigureAwait(false); }
            catch (Exception error)
            {
                if (captures.Exception is { } aggregate) errors.AddRange(aggregate.Flatten().InnerExceptions);
                else errors.Add(error);
            }
            // Attempt every release even if one stream fails to close. Capture errors
            // remain observable after files have drained and no reader is used again.
            for (var index = 0; index < _streams.Length; index++)
            {
                if (_released[index]) continue;
                Volatile.Write(ref _stage, index == 0 ? "CloseStdout" : index == 1 ? "CloseStderr" : "CloseStdin");
                try { _streams[index].Dispose(); _released[index] = true; Interlocked.Increment(ref _releasedCount); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Process standard stream cleanup failed.", errors);
            Volatile.Write(ref _stage, "Released");
        }
        catch { Volatile.Write(ref _stage, "Failed"); throw; }
        finally { Interlocked.Exchange(ref _finishedUtcTicks, DateTimeOffset.UtcNow.Ticks); }
    }

    private static string Describe(ProcessOutputCapture capture)
    {
        var snapshot = capture.Read();
        return $"read={capture.ReadCompletion.Status}, file={capture.FileCompletion.Status}/{capture.FileStage}, " +
            $"characters={snapshot.TotalCharacters}, written={snapshot.FileBytesWritten}, dropped={snapshot.FileBytesDropped}";
    }
}
