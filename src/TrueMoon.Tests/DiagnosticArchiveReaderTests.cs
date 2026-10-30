using System.Text;
using System.Text.Json;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticArchiveReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "TrueMoon-archive-read-" + Guid.NewGuid().ToString("N"));
    private static readonly Guid Application = Guid.Parse("7052242d-bd06-48bd-9e56-72409f2ca6be");
    public DiagnosticArchiveReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task FlatHistory_RejectsMalformedOversizedAndIncompleteLinesWithoutReadingOtherFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "events-0.jsonl"), Line(Record(1)) + "{invalid}\n" + new string('x', 2048) + "\n" + Line(Record(2)) + "{unfinished");
        await File.WriteAllTextAsync(Path.Combine(_root, "project.json"), Line(Record(999)));
        await using var pipeline = Pipeline(); var history = await pipeline.ReadHistoryAsync(new() { MaximumLineBytes = 1024 });
        Assert.True(history.Available); Assert.Equal(new long[] { 1, 2 }, history.Records.Select(record => record.Sequence));
        Assert.Equal(1, history.InvalidLines); Assert.Equal(1, history.OversizedLines); Assert.Equal(1, history.IncompleteLines);
        Assert.True(history.IsTruncated); Assert.Equal(1, history.FilesRead); Assert.Equal(0, history.ReadFailures);
        Assert.True(File.Exists(Path.Combine(_root, "project.json"))); Assert.DoesNotContain(history.Records, record => record.Sequence == 999);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RecordBudget_KeepsLatestAndReportsExactOmissions(int maximum)
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "events-0.jsonl"), string.Concat(Enumerable.Range(1, 4).Select(i => Line(Record(i)))));
        await using var pipeline = Pipeline(); var history = await pipeline.ReadHistoryAsync(new() { MaximumRecords = maximum });
        Assert.Equal(Enumerable.Range(5 - maximum, maximum).Select(i => (long)i), history.Records.Select(record => record.Sequence));
        Assert.Equal(4 - maximum, history.OmittedRecords); Assert.True(history.IsTruncated);
    }

    [Fact]
    public async Task TailRead_RespectsActualByteBudgetAndRetainsLastCompleteRecord()
    {
        var lines = Enumerable.Range(1, 10).Select(i => Line(Record(i))).ToArray();
        await File.WriteAllTextAsync(Path.Combine(_root, "events-0.jsonl"), string.Concat(lines));
        var budget = Encoding.UTF8.GetByteCount(lines[^1] + lines[^2]) + 20;
        await using var pipeline = Pipeline(); var history = await pipeline.ReadHistoryAsync(new() { MaximumReadBytes = budget });
        Assert.Equal(budget, history.BytesRead); Assert.Equal(10, history.Records[^1].Sequence); Assert.Equal(1, history.IncompleteLines); Assert.True(history.IsTruncated);
    }

    [Fact]
    public async Task FileBudget_AndCollectorIdentityDeduplicationAreIndependentOfSlotNumber()
    {
        var older = Path.Combine(_root, "events-0.jsonl"); var newer = Path.Combine(_root, "events-1.jsonl");
        await File.WriteAllTextAsync(older, Line(Record(1)) + Line(Record(2))); await File.WriteAllTextAsync(newer, Line(Record(2)) + Line(Record(3)));
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-1)); File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);
        await using var pipeline = Pipeline(); var limited = await pipeline.ReadHistoryAsync(new() { MaximumFiles = 1 });
        Assert.Equal(new long[] { 2, 3 }, limited.Records.Select(record => record.Sequence)); Assert.Equal(1, limited.FilesRead); Assert.True(limited.IsTruncated);
        var all = await pipeline.ReadHistoryAsync(new()); Assert.Equal(new long[] { 1, 2, 3 }, all.Records.Select(record => record.Sequence)); Assert.Equal(1, all.DuplicateRecords);
    }

    [Fact]
    public async Task ManagedHistory_ReadsRecognizedClosedRunsAndSkipsUnknownDirectories()
    {
        await using (var sink = new JsonDiagnosticSink(_root, 4096, 4, null, new())) { await sink.WriteAsync(Record(1)); }
        var previous = Directory.EnumerateDirectories(_root).Single();
        await File.WriteAllTextAsync(Path.Combine(previous, "events-3.jsonl"), Line(Record(3)));
        await using (var sink = new JsonDiagnosticSink(_root, 4096, 4, null, new())) { await sink.WriteAsync(Record(2)); }
        var unknown = Path.Combine(_root, "my-project"); Directory.CreateDirectory(unknown); await File.WriteAllTextAsync(Path.Combine(unknown, "events-0.jsonl"), Line(Record(999)));
        await using var pipeline = new DiagnosticPipeline(new() { JsonDirectory = _root, JsonUseRunDirectories = true, JsonMaxFiles = 1 });
        var history = await pipeline.ReadHistoryAsync(new());
        Assert.Equal(new long[] { 1, 2, 3 }, history.Records.Select(record => record.Sequence)); Assert.Equal(3, history.FilesRead); Assert.Equal(1, history.SkippedEntries);
        Assert.True(File.Exists(Path.Combine(unknown, "events-0.jsonl")));
    }

    [Fact]
    public async Task CurrentManagedRun_IsReadableWithoutFlushOrWaitingForBlockedOtherSink()
    {
        var held = new HeldSink(); await using var pipeline = new DiagnosticPipeline(new()
        { JsonDirectory = _root, JsonUseRunDirectories = true, SinkFactories = [new Factory<IDiagnosticSink>(() => held)] });
        try
        {
            Assert.True(pipeline.TryImport(Record(1))); await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (pipeline.Health().Single(sink => sink.Name == "jsonl").Written == 0 && DateTime.UtcNow < deadline) await Task.Yield();
            var history = await pipeline.ReadHistoryAsync(new()).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Single(history.Records); Assert.Equal(pipeline.ApplicationId, history.Records[0].ApplicationId);
            Assert.Equal(new DiagnosticOrigin(Application, 1), history.Records[0].Origin); Assert.False(held.Release.Task.IsCompleted);
        }
        finally { held.Release.TrySetResult(); }
    }

    [Fact]
    public async Task DisabledHistoryAndCancelledReadAreExplicitAndDoNotCreateStorage()
    {
        await using var disabled = new DiagnosticPipeline(new()); Assert.False((await disabled.ReadHistoryAsync(new())).Available);
        await using var pipeline = Pipeline(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.ReadHistoryAsync(new(), cancellation.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pipeline.ReadHistoryAsync(new() { MaximumFiles = 0 }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigurationViews_AreBorrowedFrozenPathFreeAndRemovedByLegacyReplacement(bool microsoft)
    {
        var options = new DiagnosticPipelineOptions { JsonDirectory = _root, QueueCapacity = 32, MemoryCapacity = 17 };
        using var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c => c.UseDiagnostics(options)).Build(); options.QueueCapacity = 1;
        var archive = app.Services.Resolve<IDiagnosticArchiveReader>()!; var reader = app.Services.Resolve<IDiagnosticConfigurationReader>()!;
        Assert.Same(archive, reader); Assert.Same(reader, app.Services.Resolve<IDiagnosticHealthReader>()); Assert.False(reader is IDisposable);
        var pipeline = app.Services.Resolve<DiagnosticPipeline>()!; pipeline.MinimumLevel = DiagnosticLevel.Error;
        pipeline.SetSourceMinimumLevel("PRIVATE_RULE", DiagnosticLevel.Trace); pipeline.SetEventEnabled("PRIVATE_EVENT", false);
        var snapshot = reader.ReadConfiguration(); Assert.Equal(32, snapshot.QueueCapacity); Assert.Equal(17, snapshot.MemoryCapacity);
        Assert.Equal(DiagnosticLevel.Error, snapshot.MinimumLevel); Assert.Equal(1, snapshot.SourceRuleCount); Assert.Equal(1, snapshot.DisabledEventCount);
        pipeline.MinimumLevel = DiagnosticLevel.Trace; Assert.Equal(DiagnosticLevel.Error, snapshot.MinimumLevel);
        var json = JsonSerializer.Serialize(snapshot); Assert.DoesNotContain(_root, json); Assert.DoesNotContain("PRIVATE", json);
        using var legacy = App.Builder(_ => { }).Setup(c => { c.UseDiagnostics(new DiagnosticPipelineOptions()); c.UseDiagnostics(); }).Build();
        Assert.Null(legacy.Services.GetService(typeof(IDiagnosticArchiveReader))); Assert.Null(legacy.Services.GetService(typeof(IDiagnosticConfigurationReader)));
    }

    private DiagnosticPipeline Pipeline(bool managed = false) => new(new() { JsonDirectory = _root, JsonUseRunDirectories = managed });
    private static string Line(DiagnosticRecord record) => JsonSerializer.Serialize(record) + "\n";
    private static DiagnosticRecord Record(int sequence) => new(DateTimeOffset.UnixEpoch.AddSeconds(sequence), sequence, Application, 123,
        "TrueMoon.Archive.Tests", DiagnosticLevel.Warning, 1, "Archive.Record", "record " + sequence, new Dictionary<string, string?>(), null, null, null, null, null, null, false);
    public void Dispose()
    {
        var full = Path.GetFullPath(_root); var temp = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixture escaped TEMP.");
        Directory.Delete(full, true);
    }
    private sealed class HeldSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default) { Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
