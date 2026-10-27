using System.Collections.Concurrent;
using System.Collections.Frozen;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticImportTests
{
    [Fact]
    public async Task ReadAfter_UsesExclusiveHostCursorAndOldestFirstBoundedOrder()
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        Assert.Equal(0, pipeline.LatestSequence);
        Assert.Empty(pipeline.ReadAfter(0, 1));
        for (var id = 1; id <= 3; id++) Assert.True(pipeline.TryImport(Remote(id) with { Sequence = 100 - id }));

        Assert.Equal(3, pipeline.LatestSequence);
        Assert.Equal(new[] { 1 }, pipeline.ReadAfter(0, 1).Select(record => record.EventId));
        Assert.Equal(new[] { 2, 3 }, pipeline.ReadAfter(1, 1024).Select(record => record.EventId));
        Assert.Empty(pipeline.ReadAfter(3, 1024));
        Assert.Empty(pipeline.ReadAfter(long.MaxValue, 1));
        Assert.Equal(new long[] { 1, 2, 3 }, pipeline.Read().Select(record => record.Sequence));
    }

    [Fact]
    public async Task ReadAfter_MaximumPageAndEvictionKeepEarlierSnapshotsIndependent()
    {
        var options = Options(); options.MemoryCapacity = 1025;
        await using var pipeline = new DiagnosticPipeline(options);
        for (var id = 1; id <= 1025; id++) Assert.True(pipeline.TryImport(Remote(id)));
        var page = pipeline.ReadAfter(0, 1024);
        Assert.Equal(Enumerable.Range(1, 1024), page.Select(record => record.EventId));
        Assert.Equal(1025, Assert.Single(pipeline.ReadAfter(page[^1].Sequence, 1024)).EventId);

        Assert.True(pipeline.TryImport(Remote(1026)));
        Assert.True(pipeline.TryImport(Remote(1027)));
        Assert.Equal(2, pipeline.MemoryEvicted);
        Assert.Equal(Enumerable.Range(3, 1024), pipeline.ReadAfter(0, 1024).Select(record => record.EventId));
        Assert.Equal(Enumerable.Range(1, 1024), page.Select(record => record.EventId));
        Assert.Equal(1027, pipeline.LatestSequence);
    }

    [Theory]
    [InlineData(-1L, 1)]
    [InlineData(long.MinValue, 1)]
    [InlineData(0L, -1)]
    [InlineData(0L, 0)]
    [InlineData(0L, 1025)]
    public async Task ReadAfter_InvalidBoundsThrowWithoutChangingHistory(long afterSequence, int maximumCount)
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.ReadAfter(afterSequence, maximumCount));
        Assert.Equal(0, pipeline.LatestSequence);
        Assert.Empty(pipeline.Read());
    }

    [Fact]
    public async Task TryImport_NullThrowsBeforeCaptureOrAdmission()
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        Assert.Throws<ArgumentNullException>(() => pipeline.TryImport(null!));
        Assert.Equal(0, pipeline.LatestSequence);
        Assert.Equal(0, pipeline.CaptureFailures);
        Assert.Empty(pipeline.Read());
    }

    [Theory]
    [InlineData("empty-app")]
    [InlineData("self-app")]
    [InlineData("zero-sequence")]
    [InlineData("negative-sequence")]
    [InlineData("zero-process")]
    [InlineData("negative-process")]
    [InlineData("level-below")]
    [InlineData("level-above")]
    [InlineData("already-imported")]
    public async Task TryImport_InvalidIdentityOrRecursiveOriginRejectsBeforeAdmission(string invalid)
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        var record = Remote(1);
        record = invalid switch
        {
            "empty-app" => record with { ApplicationId = Guid.Empty },
            "self-app" => record with { ApplicationId = pipeline.ApplicationId },
            "zero-sequence" => record with { Sequence = 0 },
            "negative-sequence" => record with { Sequence = -1 },
            "zero-process" => record with { ProcessId = 0 },
            "negative-process" => record with { ProcessId = -1 },
            "level-below" => record with { Level = (DiagnosticLevel)(-1) },
            "level-above" => record with { Level = (DiagnosticLevel)99 },
            "already-imported" => record with { Origin = new(Guid.NewGuid(), 17) },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };

        Assert.False(pipeline.TryImport(record));
        Assert.Equal(1, pipeline.CaptureFailures);
        Assert.Equal(0, pipeline.LatestSequence);
        Assert.Empty(pipeline.ReadAfter(0, 1024));
    }

    [Theory]
    [InlineData("null-source")]
    [InlineData("empty-source")]
    [InlineData("blank-source")]
    [InlineData("null-event")]
    [InlineData("null-template")]
    [InlineData("null-properties")]
    public async Task TryImport_MalformedSnapshotRejectsAndLeavesAnUnavailableCaptureSlot(string invalid)
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        var record = Remote(1);
        record = invalid switch
        {
            "null-source" => record with { Source = null! },
            "empty-source" => record with { Source = "" },
            "blank-source" => record with { Source = " \t" },
            "null-event" => record with { EventName = null! },
            "null-template" => record with { MessageTemplate = null! },
            "null-properties" => record with { Properties = null! },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };

        Assert.False(pipeline.TryImport(record));
        Assert.Equal(1, pipeline.CaptureFailures);
        Assert.Equal(1, pipeline.LatestSequence);
        Assert.Empty(pipeline.Read());
        Assert.True(pipeline.TryImport(Remote(2)));
        Assert.Equal(2, Assert.Single(pipeline.ReadAfter(0, 1)).Sequence);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdef", false)]
    [InlineData("ABCDEF0123456789ABCDEF0123456789", "ABCDEF0123456789", false)]
    [InlineData("00000000000000000000000000000000", "0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef", "0000000000000000", true)]
    [InlineData("0123456789abcdef0123456789abcde", "0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdeff", true)]
    [InlineData("g123456789abcdef0123456789abcdef", "0123456789abcdef", true)]
    [InlineData("0123456789abcdef0123456789abcdef", "0123456789abcdeg", true)]
    [InlineData("0123456789abcdef0123456789abcdef", null, true)]
    [InlineData(null, "0123456789abcdef", true)]
    public async Task TryImport_TracePairIsPreservedOrClearedTogether(string? trace, string? span, bool malformed)
    {
        await using var pipeline = new DiagnosticPipeline(Options());
        Assert.True(pipeline.TryImport(Remote(1) with { TraceId = trace, SpanId = span }));
        var imported = Assert.Single(pipeline.Read());
        Assert.Equal(malformed ? null : trace, imported.TraceId);
        Assert.Equal(malformed ? null : span, imported.SpanId);
        Assert.Equal(malformed, imported.IsTruncated);
        Assert.Equal(0, pipeline.CaptureFailures);
        Assert.Equal(1, imported.Sequence);
    }

    [Fact]
    public async Task TryImport_RefreezesClippedAuthoritativeContextAndPreservesChildCorrelation()
    {
        var options = Options(); options.MaxNameChars = 7; options.MaxMessageChars = 3;
        options.MaxExceptionChars = 4; options.MaxPropertyChars = 5; options.MaxProperties = 5;
        await using var pipeline = new DiagnosticPipeline(options);
        Assert.True(pipeline.TryImport(Remote(1)));
        var childProperties = new Dictionary<string, string?>
        {
            ["session.identification"] = "spoofed", ["sharedSuffix"] = "child",
            ["remoteKey"] = "child-value", ["excess"] = "omitted"
        };
        var context = new Dictionary<string, string?> { ["session.identifier"] = "ctx-value", ["sharedSuffix"] = null };
        var child = Remote(72) with
        {
            Sequence = 900, TimestampUtc = new(2026, 10, 27, 12, 34, 56, TimeSpan.FromHours(3)),
            ProcessId = 4321, Source = "source😀", EventName = "event😀", MessageTemplate = "ab😀tail",
            Level = DiagnosticLevel.Error, Properties = childProperties, ExceptionType = "error.type",
            ExceptionMessage = "err😀end", ExceptionStackTrace = "stack-long", Caller = "caller😀",
            TraceId = "0123456789abcdef0123456789abcdef", SpanId = "0123456789abcdef"
        };

        Assert.True(pipeline.TryImport(child, context));
        childProperties.Clear(); context["session.identifier"] = "changed"; context.Clear();
        var imported = Assert.Single(pipeline.ReadAfter(1, 1));
        Assert.NotSame(child, imported);
        Assert.IsAssignableFrom<FrozenDictionary<string, string?>>(imported.Properties);
        Assert.Equal(new[] { "remoteK", "session", "sharedS" }, imported.Properties.Keys.Order());
        Assert.Equal("ctx-v", imported.Properties["session"]);
        Assert.Null(imported.Properties["sharedS"]);
        Assert.Equal("child", imported.Properties["remoteK"]);
        Assert.Equal("source", imported.Source); Assert.Equal("event😀", imported.EventName);
        Assert.Equal("ab", imported.MessageTemplate); Assert.Equal("caller", imported.Caller);
        Assert.Equal("error.t", imported.ExceptionType); Assert.Equal("err", imported.ExceptionMessage);
        Assert.Equal("stac", imported.ExceptionStackTrace); Assert.True(imported.IsTruncated);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 9, 34, 56, TimeSpan.Zero), imported.TimestampUtc);
        Assert.Equal(TimeSpan.Zero, imported.TimestampUtc.Offset);
        Assert.Equal(4321, imported.ProcessId); Assert.Equal(72, imported.EventId);
        Assert.Equal(DiagnosticLevel.Error, imported.Level);
        Assert.Equal("0123456789abcdef0123456789abcdef", imported.TraceId);
        Assert.Equal("0123456789abcdef", imported.SpanId);
        Assert.Equal(pipeline.ApplicationId, imported.ApplicationId); Assert.NotEqual(child.ApplicationId, imported.ApplicationId);
        Assert.Equal(2, imported.Sequence); Assert.Equal(2, pipeline.LatestSequence);
        Assert.Equal(new DiagnosticOrigin(child.ApplicationId, 900), imported.Origin);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task TryImport_PropertyBudgetPrioritizesContextAtAndBelowExactBoundary(int maximum, bool truncated)
    {
        var options = Options(); options.MaxProperties = maximum;
        await using var pipeline = new DiagnosticPipeline(options);
        var context = new Dictionary<string, string?> { ["session"] = "receiver" };
        var remote = Remote(1) with { Properties = new Dictionary<string, string?> { ["remote"] = null } };
        Assert.True(pipeline.TryImport(remote, context));
        var imported = Assert.Single(pipeline.Read());
        Assert.Equal(maximum, imported.Properties.Count);
        if (maximum >= 1) Assert.Equal("receiver", imported.Properties["session"]);
        if (maximum == 2) Assert.Null(imported.Properties["remote"]);
        Assert.Equal(truncated, imported.IsTruncated);
    }

    [Fact]
    public async Task TryImport_BlankPropertyKeyConsumesBudgetAndMarksTruncation()
    {
        var options = Options(); options.MaxProperties = 2;
        await using var pipeline = new DiagnosticPipeline(options);
        var context = new Dictionary<string, string?> { [" "] = "invalid", ["session"] = "receiver" };
        Assert.True(pipeline.TryImport(Remote(1) with { Properties = new Dictionary<string, string?> { ["remote"] = "omitted" } }, context));
        var imported = Assert.Single(pipeline.Read());
        Assert.Equal(new KeyValuePair<string, string?>("session", "receiver"), Assert.Single(imported.Properties));
        Assert.True(imported.IsTruncated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryImport_EmptyAndOptionalValuesRetainExistingTruncation(bool truncated)
    {
        var options = Options(); options.MaxProperties = 0;
        await using var pipeline = new DiagnosticPipeline(options);
        Assert.True(pipeline.TryImport(Remote(1) with { EventName = "", MessageTemplate = "", IsTruncated = truncated }));
        var imported = Assert.Single(pipeline.Read());
        Assert.Empty(imported.Properties); Assert.Equal("", imported.EventName); Assert.Equal("", imported.MessageTemplate);
        Assert.Null(imported.TraceId); Assert.Null(imported.SpanId); Assert.Null(imported.Caller);
        Assert.Null(imported.ExceptionType); Assert.Null(imported.ExceptionMessage); Assert.Null(imported.ExceptionStackTrace);
        Assert.Equal(truncated, imported.IsTruncated);
        Assert.Equal(1, imported.Origin!.Sequence);
    }

    [Theory]
    [InlineData(DiagnosticLevel.Trace)]
    [InlineData(DiagnosticLevel.Critical)]
    public async Task TryImport_ValidSeverityEndpointsAreAcceptedWhenLocallyEnabled(DiagnosticLevel level)
    {
        var options = Options(); options.MinimumLevel = DiagnosticLevel.Trace;
        await using var pipeline = new DiagnosticPipeline(options);
        Assert.True(pipeline.TryImport(Remote(1) with { Level = level }));
        Assert.Equal(level, Assert.Single(pipeline.Read()).Level);
        Assert.Equal(0, pipeline.CaptureFailures); Assert.Equal(1, pipeline.LatestSequence);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task TryImport_MemoryAndSinkByteLimitsAcceptExactBoundaryAndRejectOneByteBelow(int budgetDelta)
    {
        var bytes = await ImportedSizeAsync();
        var sink = new RecordingSink(); var options = Options(sink);
        options.MemoryByteCapacity = bytes + budgetDelta; options.QueueByteCapacity = bytes + budgetDelta;
        await using var pipeline = new DiagnosticPipeline(options);
        Assert.True(pipeline.TryImport(Remote(1)));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, pipeline.LatestSequence);
        var health = Assert.Single(pipeline.Health());
        if (budgetDelta < 0)
        {
            Assert.Empty(pipeline.ReadAfter(0, 1)); Assert.Equal(1, pipeline.MemoryEvicted);
            Assert.Empty(sink.Records); Assert.Equal(0, health.Accepted); Assert.Equal(1, health.Dropped);
        }
        else
        {
            Assert.Equal(1, Assert.Single(pipeline.ReadAfter(0, 1)).EventId); Assert.Equal(0, pipeline.MemoryEvicted);
            Assert.Equal(1, Assert.Single(sink.Records).EventId); Assert.Equal(1, health.Accepted);
            Assert.Equal(1, health.Written); Assert.Equal(0, health.Dropped);
        }
        Assert.Equal(0, health.Bytes); Assert.Equal(0, health.Queued);
    }

    [Fact]
    public async Task TryImport_DoesNotReemitToGlobalListenersOrFeedAnotherCollector()
    {
        var name = "ImportTests." + Guid.NewGuid().ToString("N");
        await using var host = new DiagnosticPipeline(Options());
        await using var observer = new DiagnosticPipeline(Options());
        var legacy = new List<DiagnosticEvent>();
        var configuration = new DiagnosticsConfiguration(); configuration.AddFilter(name); configuration.AddEventListener(legacy.Add);
        using var hostSubscription = new DiagnosticSubscription(configuration, host);
        using var observerSubscription = new DiagnosticSubscription(configuration, observer);
        using var source = new EventsSource(name);
        source.Write(DiagnosticLevel.Information, 1, "Local", "local");
        Assert.Equal(2, legacy.Count);
        Assert.Null(Assert.Single(host.Read()).Origin);
        Assert.Equal(1, Assert.Single(observer.Read()).EventId);

        Assert.True(host.TryImport(Remote(2) with { Source = name }));
        var imported = Assert.Single(host.ReadAfter(1, 1));
        Assert.Equal(2, imported.EventId); Assert.Equal(2, legacy.Count);
        Assert.Equal(1, Assert.Single(observer.Read()).EventId);
        Assert.False(observer.TryImport(imported));
        Assert.Equal(1, observer.CaptureFailures);
        Assert.Equal(1, observer.LatestSequence);
        Assert.Equal(new[] { 1, 2 }, host.Read().Select(record => record.EventId));
    }

    [Fact]
    public async Task TryImport_AppliesLiveSourceEventAndSinkFiltersWithoutCaptureFailures()
    {
        var sink = new RecordingSink(); var options = Options(sink);
        options.MinimumLevel = DiagnosticLevel.Warning;
        options.SourceMinimumLevels = new Dictionary<string, DiagnosticLevel> { ["child"] = DiagnosticLevel.Error };
        options.DisabledEvents = ["Hidden"];
        options.SinkMinimumLevels = new Dictionary<string, DiagnosticLevel> { ["custom-0"] = DiagnosticLevel.Critical };
        await using var pipeline = new DiagnosticPipeline(options);
        Assert.False(pipeline.TryImport(Remote(1)));
        Assert.False(pipeline.TryImport(Remote(2) with { Source = "child.Render", Level = DiagnosticLevel.Warning }));
        Assert.False(pipeline.TryImport(Remote(3) with { Level = DiagnosticLevel.Error, EventName = "Hidden" }));
        Assert.True(pipeline.TryImport(Remote(4) with { Level = DiagnosticLevel.Error }));
        Assert.True(pipeline.TryImport(Remote(5) with { Level = DiagnosticLevel.Critical }));
        pipeline.MinimumLevel = DiagnosticLevel.Trace; pipeline.SetSourceMinimumLevel("child", DiagnosticLevel.Debug);
        pipeline.SetEventEnabled("Hidden", true);
        Assert.True(pipeline.TryImport(Remote(6) with { Source = "child.Render", Level = DiagnosticLevel.Debug, EventName = "Hidden" }));
        await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { 4, 5, 6 }, pipeline.Read().Select(record => record.EventId));
        Assert.Equal(5, Assert.Single(sink.Records).EventId);
        Assert.Equal(3, pipeline.LatestSequence); Assert.Equal(0, pipeline.CaptureFailures);
        Assert.Equal(1, Assert.Single(pipeline.Health()).Accepted);
    }

    [Fact]
    public async Task TryImport_UsesReservedBoundedSinkQueueAndClosedLateWriteAccounting()
    {
        var sink = new BlockingSink(); var options = Options(sink);
        options.QueueCapacity = 3; options.ReservedErrorCapacity = 1;
        await using var pipeline = new DiagnosticPipeline(options);
        try
        {
            Assert.True(pipeline.TryImport(Remote(1)));
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(pipeline.TryImport(Remote(2)));
            Assert.True(pipeline.TryImport(Remote(3)));
            Assert.True(pipeline.TryImport(Remote(4) with { Level = DiagnosticLevel.Error }));
            Assert.True(pipeline.TryImport(Remote(5) with { Level = DiagnosticLevel.Critical }));
            var queued = Assert.Single(pipeline.Health());
            Assert.Equal(3, queued.Accepted); Assert.Equal(2, queued.Dropped); Assert.Equal(3, queued.Queued);
            Assert.Equal(0, queued.Written); Assert.Equal(5, pipeline.Read().Count);
            sink.Release.TrySetResult();
            await pipeline.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { 1, 2, 4 }, sink.Records.Select(record => record.EventId));
            var drained = Assert.Single(pipeline.Health());
            Assert.Equal(3, drained.Written); Assert.Equal(0, drained.Queued); Assert.Equal(0, drained.Bytes);
            Assert.Equal(1, sink.Disposals);
            Assert.False(pipeline.TryImport(Remote(6)));
            Assert.False(pipeline.TryImport(Remote(7)));
            Assert.Equal(2, pipeline.LateWrites); Assert.Equal(5, pipeline.LatestSequence);
            Assert.Equal(0, pipeline.CaptureFailures); Assert.Equal(5, pipeline.ReadAfter(0, 1024).Count);
        }
        finally { sink.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AppRegistration_ReplacementProvidesOneBorrowedAccessAndDrainsOwnedPipeline(bool microsoftDI, bool legacyFirst)
    {
        var child = Remote(1) with { ApplicationId = Guid.NewGuid() };
        var origin = new DiagnosticOrigin(child.ApplicationId, child.Sequence);
        var discardedCreations = 0; var activeCreations = 0; var sink = new BlockingSink(origin);
        var discarded = Options(); discarded.SinkFactories = [new Factory<IDiagnosticSink>(() => { discardedCreations++; return new RecordingSink(); })];
        var replacement = Options(); replacement.SinkFactories = [new Factory<IDiagnosticSink>(() => { activeCreations++; return sink; })];
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            if (legacyFirst) context.UseDiagnostics(); else context.UseDiagnostics(discarded);
            context.UseDiagnostics(replacement);
        }).Build();
        try
        {
            var pipeline = Assert.Single(app.Services.ResolveAll<DiagnosticPipeline>());
            var reader = Assert.Single(app.Services.ResolveAll<IDiagnosticRecordReader>());
            var importer = Assert.Single(app.Services.ResolveAll<IDiagnosticRecordImporter>());
            Assert.Same(reader, importer); Assert.NotSame(pipeline, reader);
            Assert.False(reader is IDisposable); Assert.False(reader is IAsyncDisposable);
            Assert.Equal(0, discardedCreations); Assert.Equal(1, activeCreations);
            // The default TrueMoon listener prefix also captures parallel Apps in this process.
            var cursor = reader.LatestSequence;
            Assert.True(importer.TryImport(child, new Dictionary<string, string?> { ["session"] = "host" }));
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var imported = Assert.Single(reader.ReadAfter(cursor, 1024), record => record.Origin == origin);
            Assert.InRange(imported.Sequence, cursor + 1, long.MaxValue);
            Assert.InRange(reader.LatestSequence, imported.Sequence, long.MaxValue);
            Assert.Equal("host", imported.Properties["session"]);
            var disposing = app.DisposeAsync().AsTask();
            Assert.False(disposing.IsCompleted); Assert.Equal(0, sink.Disposals); Assert.Empty(sink.Records);
            sink.Release.TrySetResult(); await disposing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, Assert.Single(sink.Records).EventId); Assert.Equal(1, sink.Disposals);
            var closedSequence = reader.LatestSequence;
            var closedRecords = reader.ReadAfter(0, 1024).Select(record => record.Sequence).ToArray();
            Assert.False(importer.TryImport(child with { Sequence = 2 }));
            Assert.Equal(closedSequence, reader.LatestSequence);
            Assert.Equal(closedRecords, reader.ReadAfter(0, 1024).Select(record => record.Sequence));
        }
        finally { sink.Release.TrySetResult(); await app.DisposeAsync(); }
        await app.DisposeAsync(); Assert.Equal(1, sink.Disposals);
    }

    [Theory]
    [InlineData(false, "default")]
    [InlineData(true, "default")]
    [InlineData(false, "legacy")]
    [InlineData(true, "legacy")]
    [InlineData(false, "pipeline-then-legacy")]
    [InlineData(true, "pipeline-then-legacy")]
    public async Task AppRegistration_DefaultOrLegacyHasNoDeliveryAccessAndNeverCreatesDiscardedPipeline(bool microsoftDI, string setup)
    {
        var creations = 0; var discarded = Options();
        discarded.SinkFactories = [new Factory<IDiagnosticSink>(() => { creations++; return new RecordingSink(); })];
        await using var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            if (setup == "pipeline-then-legacy") context.UseDiagnostics(discarded);
            if (setup != "default") context.UseDiagnostics();
        }).Build();

        Assert.Empty(app.Services.ResolveAll<IDiagnosticRecordReader>());
        Assert.Empty(app.Services.ResolveAll<IDiagnosticRecordImporter>());
        Assert.Empty(app.Services.ResolveAll<DiagnosticPipeline>());
        Assert.Equal(0, creations);
    }

    private static DiagnosticPipelineOptions Options(IDiagnosticSink? sink = null) => new()
    {
        Emergency = new() { Enabled = false },
        SinkFactories = sink is null ? [] : [new Factory<IDiagnosticSink>(() => sink)]
    };

    private static DiagnosticRecord Remote(int id) => new(
        new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero), id,
        new Guid("24a8ae5b-4b24-4cb2-9055-61a6e39639fa"), 1234, "remote", DiagnosticLevel.Information,
        id, "Remote.Event", "message", new Dictionary<string, string?>(), null, null, null, null, null, null, false);

    private static async Task<long> ImportedSizeAsync()
    {
        var sink = new BlockingSink();
        await using var pipeline = new DiagnosticPipeline(Options(sink));
        try
        {
            Assert.True(pipeline.TryImport(Remote(1)));
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return Assert.Single(pipeline.Health()).Bytes;
        }
        finally { sink.Release.TrySetResult(); }
    }

    private class RecordingSink : IDiagnosticSink
    {
        internal readonly ConcurrentQueue<DiagnosticRecord> Records = new();
        internal int Disposals;
        public virtual ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        { Records.Enqueue(record); return ValueTask.CompletedTask; }
        public ValueTask DisposeAsync() { Interlocked.Increment(ref Disposals); return ValueTask.CompletedTask; }
    }

    private sealed class BlockingSink(DiagnosticOrigin? expectedOrigin = null) : RecordingSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            if (expectedOrigin is not null && record.Origin != expectedOrigin) return;
            Entered.TrySetResult(); await Release.Task; await base.WriteAsync(record, cancellationToken);
        }
    }
}
