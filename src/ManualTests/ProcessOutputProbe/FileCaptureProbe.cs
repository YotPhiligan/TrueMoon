using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace ProcessOutputProbe;

internal static class FileCaptureProbe
{
    internal static int RunStarvation(string directory)
    {
        Directory.CreateDirectory(directory);
        ThreadPool.GetMinThreads(out var oldMinimum, out var oldIoMinimum);
        ThreadPool.GetMaxThreads(out var oldMaximum, out var oldIoMaximum);
        using var reader = new EofGateReader();
        using var callbackEntered = new ManualResetEventSlim();
        using var callbackRelease = new ManualResetEventSlim();
        ProcessOutputCapture? capture = null;
        var passed = false;
        try
        {
            if (!ThreadPool.SetMinThreads(1, oldIoMinimum) || !ThreadPool.SetMaxThreads(1, oldIoMaximum))
                throw new InvalidOperationException("Cannot establish isolated one-worker precondition.");
            capture = new(reader, filePath: Path.Combine(directory, "capture.log"));
            if (!reader.AtEof.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Reader did not reach EOF gate.");
            var writeDeadline = Stopwatch.StartNew();
            while (capture.Read().FileBytesWritten != 24)
            {
                if (writeDeadline.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Initial file chunk did not drain.");
                Thread.Sleep(1);
            }
            var hold = capture.ReadCompletion.ContinueWith(_ =>
            {
                callbackEntered.Set();
                if (!callbackRelease.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Controller did not release the worker.");
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            reader.ReleaseEof.TrySetResult(0);
            if (!callbackEntered.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Completion callback did not enter.");
            var readCompleted = capture.ReadCompletion.IsCompletedSuccessfully;
            var completed = capture.Completion.Wait(TimeSpan.FromSeconds(2));
            var fileCompleted = capture.FileCompletion.IsCompletedSuccessfully;
            passed = readCompleted && fileCompleted && completed;
            File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
            {
                Passed = passed, Environment.ProcessId, ReadCompleted = readCompleted, FileCompletedWhileWorkerHeld = fileCompleted,
                CompletedWhileWorkerHeld = completed, ReaderDisposed = reader.Disposed,
                State = State(capture), WorkerCount = ThreadPool.ThreadCount,
                ThreadPool.PendingWorkItemCount
            }));
            callbackRelease.Set();
            if (!hold.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Completion callback did not drain.");
        }
        finally
        {
            reader.ReleaseEof.TrySetResult(0); callbackRelease.Set();
            ThreadPool.SetMaxThreads(oldMaximum, oldIoMaximum);
            ThreadPool.SetMinThreads(oldMinimum, oldIoMinimum);
            if (capture != null) capture.Completion.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        return passed ? 0 : 1;
    }

    internal static async Task<int> RunAsync(string directory, int iterations, bool diagnostics = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, 10000);
        Directory.CreateDirectory(directory);
        await using var app = diagnostics ? App.Build(c => c.UseDiagnostics(new DiagnosticPipelineOptions
        {
            JsonDirectory = Path.Combine(directory, "diagnostics"), JsonUseRunDirectories = true
        })) : null;
        IEventsSource? events = null;
        if (app != null)
        {
            await app.StartAsync();
            events = app.Services.Resolve<IEventsSourceFactory>()!.Create("TrueMoon.FileCaptureProbe");
        }
        var elapsed = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            events?.Write(DiagnosticLevel.Warning, 991, "Capture.Round", "File capture round");
            using var stdout = new StringReader("PACKAGE_RUNTIME_STDOUT\r\n");
            using var stderr = new StringReader("PACKAGE_RUNTIME_STDERR\r\n");
            var output = new ProcessOutputCapture(stdout, filePath: Path.Combine(directory, iteration + ".stdout.log"));
            var error = new ProcessOutputCapture(stderr, filePath: Path.Combine(directory, iteration + ".stderr.log"));
            try
            {
                await Task.WhenAll(output.Completion, error.Completion).WaitAsync(TimeSpan.FromSeconds(2));
                if (File.ReadAllText(Path.Combine(directory, iteration + ".stdout.log")) != output.Read().Text ||
                    File.ReadAllText(Path.Combine(directory, iteration + ".stderr.log")) != error.Read().Text)
                    throw new IOException("Persisted capture mismatch.");
            }
            catch (Exception failure)
            {
                File.WriteAllText(Path.Combine(directory, "failure.json"), JsonSerializer.Serialize(new
                {
                    Environment.ProcessId, Framework = RuntimeInformation.FrameworkDescription, iteration,
                    Elapsed = elapsed.Elapsed, Error = failure.ToString(), Diagnostics = diagnostics,
                    Stdout = State(output), Stderr = State(error), ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount
                }));
                Console.Error.WriteLine("FILE_CAPTURE_FAILURE " + directory);
                // Retain this owned process briefly so the caller can collect its stacks.
                await Task.Delay(TimeSpan.FromSeconds(15));
                return 1;
            }
        }
        if (app != null) await app.DisposeAsync();
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Environment.ProcessId, Framework = RuntimeInformation.FrameworkDescription,
            Iterations = iterations, Captures = iterations * 2, Elapsed = elapsed.Elapsed, Diagnostics = diagnostics
        }));
        return 0;
    }

    private static object State(ProcessOutputCapture capture) => new
    {
        Read = capture.ReadCompletion.Status.ToString(), File = capture.FileCompletion.Status.ToString(),
        capture.FileStage, Snapshot = capture.Read()
    };

    private sealed class EofGateReader : TextReader
    {
        internal readonly ManualResetEventSlim AtEof = new();
        internal readonly TaskCompletionSource<int> ReleaseEof = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _read;
        internal bool Disposed;
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (!_read) { _read = true; "PACKAGE_RUNTIME_STDOUT\r\n".AsMemory().CopyTo(buffer); return ValueTask.FromResult(24); }
            AtEof.Set();
            return new(ReleaseEof.Task);
        }
        protected override void Dispose(bool disposing)
        { Disposed = true; AtEof.Dispose(); base.Dispose(disposing); }
    }
}
