using System.Runtime.ExceptionServices;

namespace TrueMoon.Aether.Hosting;

// Process.Close leaves accessed standard streams to the caller. This owner retains
// them across process reap and a timed-out wait, then closes them after capture drain.
internal sealed class OwnedProcessOutput
{
    private readonly Lock _gate = new();
    private readonly IDisposable[] _streams;
    private readonly bool[] _released;
    private Task? _drain;

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
        try { await drain.WaitAsync(timeout).ConfigureAwait(false); }
        catch (TimeoutException error) when (!drain.IsCompleted)
        {
            throw new TimeoutException($"Process output drain timed out after process reap. " +
                $"stdout: {Describe(Stdout)}; stderr: {Describe(Stderr)}. " +
                $"ThreadPool workers={ThreadPool.ThreadCount}, pending={ThreadPool.PendingWorkItemCount}. " +
                $"Cleanup remains owned and can be retried.", error);
        }
    }

    private async Task DrainCoreAsync()
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
            try { _streams[index].Dispose(); _released[index] = true; }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Process standard stream cleanup failed.", errors);
    }

    private static string Describe(ProcessOutputCapture capture)
    {
        var snapshot = capture.Read();
        return $"read={capture.ReadCompletion.Status}, file={capture.FileCompletion.Status}/{capture.FileStage}, " +
            $"characters={snapshot.TotalCharacters}, written={snapshot.FileBytesWritten}, dropped={snapshot.FileBytesDropped}";
    }
}
