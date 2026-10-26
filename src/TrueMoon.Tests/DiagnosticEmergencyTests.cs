using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using TrueMoon.Diagnostics;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticEmergencyTests
{
    [Fact]
    public async Task EmergencyReport_UsesOnlyBoundedMetadataAndOneLiteralNewline()
    {
        var writer = new EmergencyTestWriter { NewLine = new string('x', 5000) };
        var id = Guid.NewGuid();
        var output = new DiagnosticEmergencyOutput(new() { Writer = writer, MaxMessageChars = 1024 }, id);
        var error = new HostileMessageException();
        try
        {
            output.Report("Write\r\nInjected", "custom-0\n\0", error, 42);
            await output.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var line = Assert.Single(writer.Lines);
            Assert.Contains("App=" + id.ToString("N"), line);
            Assert.Contains(typeof(HostileMessageException).FullName!, line);
            Assert.Contains("Sequence=42", line);
            Assert.Equal(1, line.Count(character => character == '\n'));
            Assert.EndsWith("\n", line); Assert.DoesNotContain("\r", line, StringComparison.Ordinal); Assert.DoesNotContain("\0", line, StringComparison.Ordinal);
            Assert.InRange(line.Length, 1, 1024); Assert.Equal(0, error.MessageReads);
            Assert.DoesNotContain(new string('x', 5000), line);
        }
        finally { await output.DisposeAsync(); }
        Assert.False(writer.Disposed);

        var clipped = new DiagnosticEmergencyOutput(new() { Writer = writer, MaxMessageChars = 128 }, id);
        try
        {
            clipped.Report(new string('s', 300), new string('k', 300));
            await clipped.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.InRange(writer.Lines.Last().Length, 1, 128);
            Assert.EndsWith("\n", writer.Lines.Last());
        }
        finally { await clipped.DisposeAsync(); }
    }

    [Fact]
    public async Task EmergencyReport_CountsExecutingQueueAndDroppedAttemptsBeforeLifetimeSuppression()
    {
        var writer = new EmergencyTestWriter(block: true);
        var output = new DiagnosticEmergencyOutput(new() { Writer = writer, QueueCapacity = 2, MaxReports = 4 }, Guid.NewGuid());
        try
        {
            output.Report("first", "sink"); await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(() =>
            {
                output.Report("second", "sink"); output.Report("third", "sink");
                output.Report("fourth", "sink"); output.Report("fifth", "sink");
            }).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new DiagnosticEmergencyStatus(2, 0, 2, 1, 0, 2, null), output.Read());
        }
        finally { writer.Release.TrySetResult(); }
        await output.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        output.Report("sixth", "sink");
        Assert.Equal(new DiagnosticEmergencyStatus(2, 2, 2, 2, 0, 0, null), output.Read());
        Assert.Contains("Stage=first", writer.Lines.First()); Assert.Contains("Stage=second", writer.Lines.Last());
        await output.DisposeAsync(); Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task EmergencyReport_DoesNotRetainExceptionWhileWriterIsBlocked()
    {
        var writer = new EmergencyTestWriter(block: true);
        var output = new DiagnosticEmergencyOutput(new() { Writer = writer }, Guid.NewGuid());
        try
        {
            output.Report("blocked", "sink"); await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var exception = ReportCollectibleException(output);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.False(exception.IsAlive);
            Assert.Equal(2, output.Read().Queued);
        }
        finally { writer.Release.TrySetResult(); await output.DisposeAsync(); }
        Assert.Equal(2, output.Read().Written);
    }

    [Fact]
    public async Task EmergencyWriterFailure_CountsTypeWithoutMessageOrRecursiveReportsAndKeepsWriterBorrowed()
    {
        var error = new HostileMessageException();
        var writer = new EmergencyTestWriter { Failure = error };
        var output = new DiagnosticEmergencyOutput(new() { Writer = writer }, Guid.NewGuid());
        output.Report("Write", "sink"); await output.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        output.Report("Dispose", "sink"); await output.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, writer.Calls); Assert.Equal(0, error.MessageReads);
        Assert.Equal(new DiagnosticEmergencyStatus(2, 0, 0, 0, 2, 0, typeof(HostileMessageException).FullName), output.Read());
        await output.DisposeAsync(); await output.DisposeAsync();
        Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task EmergencyWriterReentry_IsFiniteAndNeverWaitsOnItsOwnWorker()
    {
        var writer = new EmergencyTestWriter();
        var output = new DiagnosticEmergencyOutput(new() { Writer = writer, QueueCapacity = 2, MaxReports = 3 }, Guid.NewGuid());
        writer.OnWrite = _ => output.Report("Reentry", "writer");
        output.Report("Initial", "sink");
        await writer.Third.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await output.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new DiagnosticEmergencyStatus(3, 3, 0, 1, 0, 0, null), output.Read());
        Assert.Equal(3, writer.Calls);
        await output.DisposeAsync(); Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task Pipeline_ReportsWriteAndDisposeFailuresWithoutPayloadOrExceptionMessage()
    {
        var writer = new EmergencyTestWriter(); var sink = new EmergencyTestSink { WriteError = new IOException("secret-error-message"), DisposeFailures = 1 };
        var pipeline = new DiagnosticPipeline(Options(writer, sink));
        pipeline.Publish("source", new(DiagnosticLevel.Error, 1, "Event", "secret-payload"));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<IOException>(() => pipeline.DisposeAsync().AsTask());
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, pipeline.EmergencyStatus!.Written);
        Assert.Contains(writer.Lines, line => line.Contains("Stage=Write") && line.Contains("Sink=custom-0") && line.Contains("Sequence=1"));
        Assert.Contains(writer.Lines, line => line.Contains("Stage=Dispose") && line.Contains("Error=System.IO.IOException"));
        Assert.All(writer.Lines, line => { Assert.DoesNotContain("secret-payload", line); Assert.DoesNotContain("secret-error-message", line); });
        await pipeline.DisposeAsync();
        Assert.Equal(2, sink.DisposeAttempts); Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task Pipeline_CaptureFailureUsesEmergencyWithoutPublishingPartialRecord()
    {
        var writer = new EmergencyTestWriter();
        await using var pipeline = new DiagnosticPipeline(Options(writer));
        pipeline.Publish("capture-source", new(DiagnosticLevel.Error, 1, "Event", "secret-payload", Error: new HostileMessageException()));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, pipeline.CaptureFailures); Assert.Empty(pipeline.Read());
        var line = Assert.Single(writer.Lines); Assert.Contains("Stage=Capture", line); Assert.Contains("Sink=capture-source", line);
        Assert.DoesNotContain("secret-payload", line); Assert.DoesNotContain("secret-message", line);
    }

    [Fact]
    public async Task Pipeline_ReportsOnlyErrorAndCriticalQueueLossWhileNormalSinkIsBlocked()
    {
        var writer = new EmergencyTestWriter(); var sink = new EmergencyTestSink(block: true);
        var options = Options(writer, sink); options.QueueCapacity = 1; options.ReservedErrorCapacity = 0;
        await using var pipeline = new DiagnosticPipeline(options);
        try
        {
            pipeline.Publish("source", new(DiagnosticLevel.Information, 1, "Event", "payload"));
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            foreach (var (id, level) in new[] { (2, DiagnosticLevel.Information), (3, DiagnosticLevel.Warning), (4, DiagnosticLevel.Error), (5, DiagnosticLevel.Critical) })
                pipeline.Publish("source", new(level, id, "Event", "secret-payload"));
            await writer.Second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(4, Assert.Single(pipeline.Health()).Dropped); Assert.Equal(1, Assert.Single(pipeline.Health()).Queued);
            Assert.Equal(2, pipeline.EmergencyStatus!.Accepted);
            Assert.Contains(writer.Lines, line => line.Contains("Stage=QueueOverflow") && line.Contains("Sequence=4"));
            Assert.Contains(writer.Lines, line => line.Contains("Stage=QueueOverflow") && line.Contains("Sequence=5"));
            Assert.All(writer.Lines, line => Assert.DoesNotContain("secret-payload", line));
        }
        finally { sink.Release.TrySetResult(); }
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, pipeline.EmergencyStatus!.Written);
    }

    [Fact]
    public async Task PipelineFlush_WaitsForEmergencyReportProducedAfterNormalFence()
    {
        var writer = new EmergencyTestWriter(block: true); var sink = new EmergencyTestSink(block: true) { WriteError = new IOException("secret") };
        await using var pipeline = new DiagnosticPipeline(Options(writer, sink));
        try
        {
            pipeline.Publish("source", new(DiagnosticLevel.Error, 1, "Event", "payload"));
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var flush = pipeline.FlushAsync(); Assert.False(flush.IsCompleted);
            sink.Release.TrySetResult(); await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(flush.IsCompleted); Assert.Equal(1, pipeline.EmergencyStatus!.Queued);
            writer.Release.TrySetResult(); await flush.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, pipeline.EmergencyStatus.Written); Assert.Equal(0, pipeline.EmergencyStatus.Queued);
        }
        finally { sink.Release.TrySetResult(); writer.Release.TrySetResult(); }
        Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task BlockedEmergencyWriter_DoesNotBlockProducersOrHealthySink()
    {
        var writer = new EmergencyTestWriter(block: true);
        var failing = new EmergencyTestSink { WriteError = new IOException("secret") };
        var healthy = new HealthySink();
        await using var pipeline = new DiagnosticPipeline(Options(writer, failing, healthy));
        try
        {
            pipeline.Publish("source", new(DiagnosticLevel.Error, 1, "First", "payload"));
            await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(() => pipeline.Publish("source", new(DiagnosticLevel.Information, 2, "Second", "payload")))
                .WaitAsync(TimeSpan.FromSeconds(5));
            await healthy.Second.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { 1, 2 }, healthy.Records.Select(record => record.EventId));
            Assert.Equal(new[] { 1, 2 }, pipeline.Read().Select(record => record.EventId));
            Assert.Equal(0, pipeline.EmergencyStatus!.Written);
            Assert.True(pipeline.EmergencyStatus.Queued > 0);
            Assert.False(pipeline.FlushAsync().IsCompleted);
        }
        finally { writer.Release.TrySetResult(); }
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, pipeline.EmergencyStatus!.Written);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PipelineShutdownTimeout_RetainsBlockedOwnerAndAllowsRetry(bool emergencyBlocked)
    {
        var writer = new EmergencyTestWriter(block: emergencyBlocked);
        var sink = new EmergencyTestSink(block: !emergencyBlocked) { WriteError = emergencyBlocked ? new IOException("secret") : null };
        var options = Options(writer, sink); options.ShutdownTimeout = TimeSpan.FromMilliseconds(20);
        var pipeline = new DiagnosticPipeline(options);
        try
        {
            pipeline.Publish("source", new(DiagnosticLevel.Error, 1, "Event", "payload"));
            if (emergencyBlocked) await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
            else await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => pipeline.DisposeAsync().AsTask());
            Assert.False(pipeline.IsEnabled(DiagnosticLevel.Error)); Assert.False(writer.Disposed);
            if (emergencyBlocked) { Assert.Equal(1, pipeline.EmergencyStatus!.Queued); Assert.Equal(1, sink.DisposeAttempts); }
            else
            {
                await writer.First.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Contains(writer.Lines, line => line.Contains("Stage=Shutdown")); Assert.Equal(0, sink.DisposeAttempts);
            }
        }
        finally { sink.Release.TrySetResult(); writer.Release.TrySetResult(); }
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await pipeline.DisposeAsync(); await pipeline.DisposeAsync();
        Assert.Equal(1, sink.DisposeAttempts); Assert.False(writer.Disposed); Assert.Equal(0, pipeline.EmergencyStatus!.Queued);
    }

    [Fact]
    public async Task Pipeline_EmergencyDisabledDoesNotWriteOrChangeNormalFailureHandling()
    {
        var writer = new EmergencyTestWriter(); var sink = new EmergencyTestSink { WriteError = new IOException("normal") };
        var options = Options(writer, sink); options.Emergency.Enabled = false;
        await using var pipeline = new DiagnosticPipeline(options);
        pipeline.Publish("source", new(DiagnosticLevel.Error, 1, "Event", "payload"));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(pipeline.EmergencyStatus); Assert.Equal(1, Assert.Single(pipeline.Health()).Failures);
        Assert.Empty(writer.Lines); Assert.False(writer.Disposed);
    }

    [Fact]
    public async Task PipelineDispose_SynchronousSinkCleanupRunsOffCallerAndTimeoutKeepsOwnership()
    {
        var writer = new EmergencyTestWriter(); var sink = new SynchronousDisposeSink();
        var options = Options(writer, sink); options.ShutdownTimeout = TimeSpan.FromMilliseconds(100);
        var pipeline = new DiagnosticPipeline(options);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposing = Task.Run(() => { var result = pipeline.DisposeAsync().AsTask(); returned.TrySetResult(); return result; });
        try
        {
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => disposing);
            Assert.Equal(0, sink.Released); Assert.False(writer.Disposed);
        }
        finally { sink.Release.TrySetResult(); }
        await sink.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await pipeline.DisposeAsync();
        Assert.Equal(1, sink.Released); Assert.False(writer.Disposed);
    }

    [Fact]
    public void PipelineConstructionFailure_DrainsStartupReportAndReleasesOwnedSinkOnly()
    {
        var writer = new EmergencyTestWriter(); var sink = new EmergencyTestSink(); var error = new IOException("secret-factory-message");
        var options = Options(writer, sink);
        options.SinkFactories = [new Factory<IDiagnosticSink>(() => sink), new Factory<IDiagnosticSink>(() => throw error)];
        Assert.Same(error, Assert.Throws<IOException>(() => new DiagnosticPipeline(options)));
        Assert.Equal(1, sink.DisposeAttempts); Assert.False(writer.Disposed);
        var line = Assert.Single(writer.Lines); Assert.Contains("Stage=Startup", line); Assert.Contains("Error=System.IO.IOException", line);
        Assert.DoesNotContain("secret-factory-message", line);
    }

    [Fact]
    public async Task ConstructorRollback_UsesOneTimeoutAndRetainsConcurrentCleanupUntilReleased()
    {
        var writer = new EmergencyTestWriter(block: true);
        var first = new SynchronousDisposeSink(); var second = new SynchronousDisposeSink();
        var original = new IOException("private-factory-message");
        var options = Options(writer, first, second);
        options.ShutdownTimeout = TimeSpan.FromMilliseconds(100);
        options.SinkFactories = [new Factory<IDiagnosticSink>(() => first), new Factory<IDiagnosticSink>(() => second),
            new Factory<IDiagnosticSink>(() => throw original)];
        var creating = Task.Run(() => new DiagnosticPipeline(options));
        try
        {
            await Task.WhenAll(first.Entered.Task, second.Entered.Task, writer.First.Task).WaitAsync(TimeSpan.FromSeconds(5));
            var error = await Assert.ThrowsAsync<AggregateException>(() => creating);
            var failures = error.Flatten().InnerExceptions;
            Assert.Equal(2, failures.Count); Assert.Same(original, failures[0]);
            Assert.IsType<TimeoutException>(failures[1]);
            Assert.Equal(0, first.Released); Assert.Equal(0, second.Released);
            Assert.False(writer.Disposed);
        }
        finally { first.Release.TrySetResult(); second.Release.TrySetResult(); writer.Release.TrySetResult(); }
        await Task.WhenAll(first.Completed.Task, second.Completed.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, first.Released); Assert.Equal(1, second.Released);
        // The constructor cannot return an owner on failure, but its rollback still owns and finishes the admitted report.
        await writer.Written.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(writer.Lines, line => line.Contains("Stage=Startup", StringComparison.Ordinal));
        Assert.False(writer.Disposed);
    }

    [Theory]
    [InlineData("queue", 0)] [InlineData("queue", 129)]
    [InlineData("reports", 0)] [InlineData("reports", 1025)]
    [InlineData("message", 127)] [InlineData("message", 4097)]
    public void InvalidEmergencyBudgets_AreRejectedEvenWhenDisabledBeforeSinkCreation(string budget, int value)
    {
        var calls = 0; var options = new DiagnosticPipelineOptions { Emergency = new() { Enabled = false } };
        if (budget == "queue") options.Emergency.QueueCapacity = value;
        if (budget == "reports") options.Emergency.MaxReports = value;
        if (budget == "message") options.Emergency.MaxMessageChars = value;
        options.SinkFactories = [new Factory<IDiagnosticSink>(() => { calls++; return new EmergencyTestSink(); })];
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticPipeline(options)); Assert.Equal(0, calls);
    }

    private static DiagnosticPipelineOptions Options(EmergencyTestWriter writer, params IDiagnosticSink[] sinks)
        => new() { Emergency = new() { Writer = writer }, SinkFactories = sinks.Select(sink => (IFactory<IDiagnosticSink>)new Factory<IDiagnosticSink>(() => sink)).ToArray() };
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReportCollectibleException(DiagnosticEmergencyOutput output)
    {
        var error = new IOException("secret-collectible"); output.Report("Write", "sink", error); return new WeakReference(error);
    }
    private sealed class HostileMessageException : Exception
    {
        internal int MessageReads;
        public override string Message { get { Interlocked.Increment(ref MessageReads); throw new InvalidOperationException("secret-message"); } }
    }
    private sealed class SynchronousDisposeSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously), Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Released;
        public ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            Entered.TrySetResult(); Release.Task.GetAwaiter().GetResult(); Interlocked.Increment(ref Released); Completed.TrySetResult(); return ValueTask.CompletedTask;
        }
    }
    private sealed class HealthySink : IDiagnosticSink
    {
        internal readonly ConcurrentQueue<DiagnosticRecord> Records = new();
        internal readonly TaskCompletionSource Second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            Records.Enqueue(record); if (Records.Count == 2) Second.TrySetResult(); return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class EmergencyTestWriter(bool block = false) : TextWriter
{
    internal readonly ConcurrentQueue<string> Lines = new();
    internal readonly TaskCompletionSource First = Signal(), Second = Signal(), Third = Signal(), Release = Signal(), Written = Signal();
    internal Exception? Failure;
    internal Action<string>? OnWrite;
    internal int Calls;
    internal bool Disposed;
    public override Encoding Encoding => Encoding.UTF8;
    public override async Task WriteAsync(string? value)
    {
        var count = Interlocked.Increment(ref Calls); var text = value ?? "";
        if (count == 1 && block) First.TrySetResult();
        if (block) await Release.Task;
        if (Failure is not null) throw Failure;
        Lines.Enqueue(text); Written.TrySetResult(); OnWrite?.Invoke(text);
        if (count == 1 && !block) First.TrySetResult();
        if (count == 2) Second.TrySetResult(); if (count == 3) Third.TrySetResult();
    }
    protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class EmergencyTestSink(bool block = false) : IDiagnosticSink
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Exception? WriteError;
    internal int DisposeFailures, DisposeAttempts;
    public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
    {
        Entered.TrySetResult(); if (block) await Release.Task;
        if (WriteError is not null) throw WriteError;
    }
    public ValueTask DisposeAsync()
    {
        DisposeAttempts++; if (DisposeFailures-- > 0) throw new IOException("secret-error-message"); return ValueTask.CompletedTask;
    }
}
