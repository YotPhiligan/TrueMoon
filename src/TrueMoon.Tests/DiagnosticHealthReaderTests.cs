using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticHealthReaderTests
{
    [Fact]
    public async Task Health_ReadsBlockedSinkWithoutDrainAndSeparatesMemoryEvictionFromSinkLoss()
    {
        var sink = new BlockedSink();
        await using var pipeline = new DiagnosticPipeline(new() { MemoryCapacity = 2, QueueCapacity = 1, ReservedErrorCapacity = 0,
            SinkFactories = [new Factory<IDiagnosticSink>(() => sink)] });
        try
        {
            Assert.True(pipeline.TryImport(Record(1))); await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var index = 2; index <= 5; index++) Assert.True(pipeline.TryImport(Record(index)));
            var health = await Task.Run(pipeline.ReadHealth).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(2, health.RetainedRecords); Assert.Equal(3, health.MemoryEvicted); Assert.Equal(0, health.CaptureFailures);
            Assert.Equal(5, health.LatestSequence); Assert.False(health.Closed);
            var status = Assert.Single(health.Sinks); Assert.Equal(1, status.Accepted); Assert.Equal(0, status.Written);
            Assert.Equal(4, status.Dropped); Assert.Equal(1, status.Queued); Assert.True(status.Bytes > 0);
            Assert.False(sink.Released.Task.IsCompleted); Assert.NotNull(health.Emergency); Assert.Equal(0, health.Emergency.Failures); Assert.Null(health.Retention);
            sink.Released.TrySetResult(); await pipeline.FlushAsync(); var final = pipeline.ReadHealth();
            Assert.Equal(1, Assert.Single(final.Sinks).Written); Assert.Equal(0, Assert.Single(final.Sinks).Queued);
            Assert.Equal(0, status.Written); // Prior snapshot remains frozen.
            await pipeline.DisposeAsync(); Assert.True(pipeline.ReadHealth().Closed); Assert.Equal(1, sink.Disposals);
        }
        finally { sink.Released.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AppHealth_IsBorrowedAndRepeatedLegacySetupRemovesEveryAccessView(bool microsoft)
    {
        var sink = new BlockedSink(); sink.Released.TrySetResult();
        using (var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c => c.UseDiagnostics(new DiagnosticPipelineOptions
        { SinkFactories = [new Factory<IDiagnosticSink>(() => sink)] })).Build())
        {
            var reader = app.Services.Resolve<IDiagnosticHealthReader>()!;
            Assert.False(reader is IDisposable); Assert.False(reader is IAsyncDisposable);
            Assert.Same(reader, app.Services.Resolve<IDiagnosticRecordReader>());
            var pipeline = app.Services.Resolve<DiagnosticPipeline>()!; Assert.True(pipeline.TryImport(Record(1)));
            Assert.Equal(pipeline.ApplicationId, reader.ReadHealth().ApplicationId);
            app.Dispose(); Assert.True(reader.ReadHealth().Closed); Assert.Equal(1, sink.Disposals);
        }
        using var legacy = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c =>
        { c.UseDiagnostics(new DiagnosticPipelineOptions()); c.UseDiagnostics(); }).Build();
        Assert.Null(legacy.Services.GetService(typeof(IDiagnosticHealthReader)));
        Assert.Null(legacy.Services.GetService(typeof(IDiagnosticRecordReader)));
    }

    [Fact]
    public async Task FailingSink_HealthReportsFailureAndBoundedErrorInsteadOfSuccessfulWrite()
    {
        await using var pipeline = new DiagnosticPipeline(new() { SinkFactories = [new Factory<IDiagnosticSink>(() => new FailingSink())] });
        Assert.True(pipeline.TryImport(Record(1))); await pipeline.FlushAsync();
        var sink = Assert.Single(pipeline.ReadHealth().Sinks); Assert.Equal(1, sink.Accepted); Assert.Equal(1, sink.Failures);
        Assert.Equal(0, sink.Written); Assert.Equal(0, sink.Queued); Assert.NotNull(sink.LastError); Assert.InRange(sink.LastError.Length, 1, 1024);
    }
    private static DiagnosticRecord Record(int sequence) => new(DateTimeOffset.UtcNow, sequence, Guid.NewGuid(), Environment.ProcessId,
        "TrueMoon.Health.Test", DiagnosticLevel.Warning, 101, "Health.Record", "record", new Dictionary<string, string?>(), null, null, null, null, null, null, false);
    private sealed class BlockedSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Disposals;
        public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        { Entered.TrySetResult(); await Released.Task.WaitAsync(cancellationToken); }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
    private sealed class FailingSink : IDiagnosticSink
    {
        public ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default) => ValueTask.FromException(new IOException(new string('e', 10000)));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
