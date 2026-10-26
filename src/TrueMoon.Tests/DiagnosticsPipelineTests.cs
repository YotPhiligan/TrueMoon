using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticsPipelineTests
{
    [Fact]
    public async Task SourceAndEventFilters_UseLongestPrefixSnapshotAndLiveChangesBeforeLazyProperties()
    {
        var name = UniqueName() + ".Render";
        var levels = new Dictionary<string, DiagnosticLevel>
        {
            ["PipelineTests."] = DiagnosticLevel.Error,
            [name[..^7]] = DiagnosticLevel.Warning,
            [name] = DiagnosticLevel.Debug
        };
        var disabled = new List<string> { "Runtime.Hidden" };
        await using var harness = new Harness(new()
        {
            MinimumLevel = DiagnosticLevel.Critical, SourceMinimumLevels = levels, DisabledEvents = disabled
        }, sourceName: name);
        levels[name] = DiagnosticLevel.Critical; disabled.Clear();
        var evaluations = 0;
        void Write(int id, DiagnosticLevel level, string eventName = "Runtime.Allowed")
            => harness.Source.Write(level, id, eventName, "message", () =>
            {
                evaluations++; return new Dictionary<string, string?> { ["proof"] = "evaluated" };
            });

        Assert.True(harness.Pipeline.IsEnabled(DiagnosticLevel.Debug, name, "Runtime.Allowed"));
        Write(1, DiagnosticLevel.Debug); Write(2, DiagnosticLevel.Trace); Write(3, DiagnosticLevel.Critical, "Runtime.Hidden");
        Assert.Equal(1, evaluations);
        harness.Pipeline.SetSourceMinimumLevel(name, DiagnosticLevel.Warning);
        Write(4, DiagnosticLevel.Information); Write(5, DiagnosticLevel.Warning);
        harness.Pipeline.SetEventEnabled("Runtime.Allowed", false);
        Write(6, DiagnosticLevel.Critical);
        Assert.False(harness.Source.IsEnabled(DiagnosticLevel.Critical, "Runtime.Allowed"));
        harness.Pipeline.SetEventEnabled("Runtime.Allowed", true);
        Write(7, DiagnosticLevel.Warning);
        harness.Pipeline.SetEventEnabled("Runtime.Hidden", true);
        Write(8, DiagnosticLevel.Warning, "Runtime.Hidden");

        Assert.Equal(4, evaluations);
        Assert.Equal(new[] { 1, 5, 7, 8 }, harness.Pipeline.Read().Select(record => record.EventId));
        Assert.Equal("Runtime.Hidden", harness.Pipeline.Read()[^1].EventName);
    }

    [Fact]
    public async Task SinkLevelFilters_AreIndependentSnapshotsAndDoNotFilterMemoryHistory()
    {
        var first = new RecordingSink(); var second = new RecordingSink();
        var levels = new Dictionary<string, DiagnosticLevel>
        {
            ["custom-0"] = DiagnosticLevel.Information, ["custom-1"] = DiagnosticLevel.Error
        };
        var options = Options(first, second); options.MinimumLevel = DiagnosticLevel.Trace; options.SinkMinimumLevels = levels;
        await using var harness = new Harness(options);
        levels["custom-0"] = DiagnosticLevel.Critical;
        Emit(harness, 1, DiagnosticLevel.Trace); Emit(harness, 2, DiagnosticLevel.Debug);
        Emit(harness, 3); Emit(harness, 4, DiagnosticLevel.Warning);
        Emit(harness, 5, DiagnosticLevel.Error); Emit(harness, 6, DiagnosticLevel.Critical);
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, harness.Pipeline.Read().Select(record => record.EventId));
        Assert.Equal(new[] { 3, 4, 5, 6 }, first.Records.Select(record => record.EventId));
        Assert.Equal(new[] { 5, 6 }, second.Records.Select(record => record.EventId));
        Assert.Equal(new long[] { 4, 2 }, harness.Pipeline.Health().Select(status => status.Accepted));
        Assert.All(harness.Pipeline.Health(), status =>
        {
            Assert.Equal(0, status.Dropped); Assert.Equal(0, status.Failures); Assert.Equal(0, status.Queued);
        });
    }

    [Fact]
    public async Task StructuredWrite_FiltersBeforeEvaluatingPropertiesAndUsesLiveMinimum()
    {
        await using var harness = new Harness(new() { MinimumLevel = DiagnosticLevel.Warning });
        var evaluations = 0;
        IReadOnlyDictionary<string, string?> Properties() { evaluations++; return new Dictionary<string, string?> { ["state"] = "ready" }; }

        Assert.False(harness.Source.IsEnabled(DiagnosticLevel.Information, "Runtime.Ready"));
        harness.Source.Write(DiagnosticLevel.Information, 17, "Runtime.Ready", "Runtime {state}", Properties);
        Assert.Equal(0, evaluations);
        Assert.Empty(harness.Pipeline.Read());

        harness.Source.Write(DiagnosticLevel.Warning, 18, "Runtime.Warning", "Runtime {state}", Properties, caller: "FirstCaller");
        harness.Pipeline.MinimumLevel = DiagnosticLevel.Debug;
        Assert.True(harness.Source.IsEnabled(DiagnosticLevel.Information, "Runtime.Ready"));
        harness.Source.Write(DiagnosticLevel.Information, 17, "Runtime.Ready", "Runtime {state}", Properties, caller: "OtherCaller");
        harness.Pipeline.MinimumLevel = DiagnosticLevel.Critical;
        harness.Source.Write(DiagnosticLevel.Error, 19, "Runtime.Error", "ignored", Properties);

        Assert.Equal(2, evaluations);
        var records = harness.Pipeline.Read();
        Assert.Equal(new[] { "Runtime.Warning", "Runtime.Ready" }, records.Select(record => record.EventName));
        Assert.Equal(new[] { 18, 17 }, records.Select(record => record.EventId));
        Assert.Equal(new[] { DiagnosticLevel.Warning, DiagnosticLevel.Information }, records.Select(record => record.Level));
        Assert.Equal("Runtime {state}", records[1].MessageTemplate);
        Assert.Equal("ready", records[1].Properties["state"]);
        Assert.Equal("OtherCaller", records[1].Caller);
    }

    [Theory]
    [InlineData("event-name", typeof(ArgumentException))]
    [InlineData("event-id", typeof(ArgumentOutOfRangeException))]
    [InlineData("level", typeof(ArgumentOutOfRangeException))]
    [InlineData("template", typeof(ArgumentNullException))]
    public async Task StructuredWrite_RejectsInvalidIdentityBeforeEvaluatingProperties(string invalid, Type errorType)
    {
        await using var harness = new Harness(new());
        var evaluations = 0;
        Assert.Throws(errorType, () => harness.Source.Write(invalid == "level" ? (DiagnosticLevel)99 : DiagnosticLevel.Information,
            invalid == "event-id" ? -1 : 1, invalid == "event-name" ? " " : "Event", invalid == "template" ? null! : "message",
            () => { evaluations++; return new Dictionary<string, string?>(); }));
        Assert.Equal(0, evaluations);
        Assert.Empty(harness.Pipeline.Read());
    }

    [Fact]
    public async Task StructuredRecord_CapturesCollectorProcessUtcAndActivity()
    {
        await using var harness = new Harness(new());
        using var activity = new Activity("diagnostic-operation").SetIdFormat(ActivityIdFormat.W3C).Start();
        var before = DateTimeOffset.UtcNow;
        Emit(harness, 1);
        Emit(harness, 2);
        var after = DateTimeOffset.UtcNow;

        var records = harness.Pipeline.Read();
        Assert.Equal(new long[] { 1, 2 }, records.Select(record => record.Sequence));
        Assert.All(records, record =>
        {
            Assert.Equal(harness.Pipeline.ApplicationId, record.ApplicationId);
            Assert.NotEqual(Guid.Empty, record.ApplicationId);
            Assert.Equal(Environment.ProcessId, record.ProcessId);
            Assert.Equal(harness.Source.Name, record.Source);
            Assert.Equal(TimeSpan.Zero, record.TimestampUtc.Offset);
            Assert.InRange(record.TimestampUtc, before, after);
            Assert.Equal(activity.TraceId.ToHexString(), record.TraceId);
            Assert.Equal(activity.SpanId.ToHexString(), record.SpanId);
            Assert.False(record.IsTruncated);
        });
    }

    [Fact]
    public async Task StructuredRecord_CopiesAndBoundsPropertiesMessagesAndExceptionsWithoutSplittingSurrogates()
    {
        await using var harness = new Harness(new()
        {
            MaxMessageChars = 4, MaxPropertyChars = 4, MaxExceptionChars = 6,
            MaxNameChars = 8, MaxProperties = 2
        });
        var properties = new Dictionary<string, string?> { ["value"] = "abc😀tail", ["null"] = null, ["omitted"] = "third" };
        var error = RecordedError();
        harness.Source.Write(DiagnosticLevel.Error, 42, "Runtime.Failed", "abc😀tail", () => properties, error, "LongCallerName");
        properties["value"] = "changed";
        properties.Clear();

        var record = Assert.Single(harness.Pipeline.Read());
        Assert.Equal("abc", record.MessageTemplate);
        Assert.Equal("abc", record.Properties["value"]);
        Assert.Null(record.Properties["null"]);
        Assert.Equal(2, record.Properties.Count);
        Assert.DoesNotContain("omitted", record.Properties.Keys);
        Assert.Equal("Runtime.", record.EventName);
        Assert.Equal(harness.Source.Name[..8], record.Source);
        Assert.Equal("LongCall", record.Caller);
        Assert.Equal(typeof(InvalidOperationException).FullName![..8], record.ExceptionType);
        Assert.Equal("error-", record.ExceptionMessage);
        Assert.Equal(error.StackTrace![..6], record.ExceptionStackTrace);
        Assert.True(record.IsTruncated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task StructuredRecord_LimitsExaminedPropertiesAndKeepsFirstClippedKey(int maxProperties)
    {
        await using var harness = new Harness(new() { MaxNameChars = 3, MaxProperties = maxProperties });
        harness.Source.Write(DiagnosticLevel.Information, 1, "Event", "message", () => new Dictionary<string, string?>
        {
            ["abc1"] = "first", ["abc2"] = "second", ["xyz"] = "must not be examined"
        });

        var record = Assert.Single(harness.Pipeline.Read());
        Assert.Equal(maxProperties == 0 ? 0 : 1, record.Properties.Count);
        if (maxProperties != 0) Assert.Equal("first", record.Properties["abc"]);
        Assert.DoesNotContain("xyz", record.Properties.Keys);
        Assert.True(record.IsTruncated);
    }

    [Fact]
    public async Task LegacyBridge_PreservesCallbacksAndSafelySnapshotsUnknownPayloads()
    {
        var received = new List<DiagnosticEvent>();
        await using var harness = new Harness(new() { MinimumLevel = DiagnosticLevel.Trace }, received.Add);
        var value = new DangerousPayload();
        harness.Source.Write(() => value, category: "Payload", caller: "legacy");
        harness.Source.Trace(category: "Trace", caller: "trace");
        var error = RecordedError();
        harness.Source.Exception(error, category: "Failure", caller: "error");

        Assert.Equal(3, received.Count);
        Assert.Same(value, received[0].Payload);
        Assert.Same(error, received[2].Payload);
        var records = harness.Pipeline.Read();
        Assert.Equal(new[] { DiagnosticLevel.Information, DiagnosticLevel.Trace, DiagnosticLevel.Error }, records.Select(record => record.Level));
        Assert.Equal("<" + typeof(DangerousPayload).FullName + ">", records[0].MessageTemplate);
        Assert.Equal("Payload", records[0].EventName);
        Assert.Equal("error-message", records[2].ExceptionMessage);
        Assert.Equal(0, harness.Pipeline.CaptureFailures);
    }

    [Fact]
    public async Task SnapshotFailure_IsCountedAndDoesNotDisableLaterEvents()
    {
        await using var harness = new Harness(new());
        harness.Source.Write(DiagnosticLevel.Error, 1, "Bad", "failure", error: new BrokenMessageException());
        Emit(harness, 2);

        Assert.Equal(1, harness.Pipeline.CaptureFailures);
        Assert.Equal(2, Assert.Single(harness.Pipeline.Read()).EventId);
    }

    [Fact]
    public async Task BoundedSink_ReservesErrorSlotsAndCountsExecutingWrites()
    {
        var sink = new BlockingSink();
        await using var harness = new Harness(Options(sink, queueCapacity: 4, reserved: 1));
        try
        {
            Emit(harness, 1);
            await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Emit(harness, 2); Emit(harness, 3); Emit(harness, 4);
            Emit(harness, 5, DiagnosticLevel.Error); Emit(harness, 6, DiagnosticLevel.Critical);

            var status = Assert.Single(harness.Pipeline.Health());
            Assert.Equal(4, status.Accepted);
            Assert.Equal(2, status.Dropped);
            Assert.Equal(4, status.Queued);
            Assert.Equal(0, status.Written);
            Assert.True(status.Bytes > 0);
            Assert.Equal(6, harness.Pipeline.Read().Count);
        }
        finally { sink.Release.TrySetResult(); }

        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2, 3, 5 }, sink.Records.Select(record => record.EventId));
        var drained = Assert.Single(harness.Pipeline.Health());
        Assert.Equal(4, drained.Written);
        Assert.Equal(0, drained.Queued);
        Assert.Equal(0, drained.Bytes);
        Assert.Equal(0, drained.Failures);
    }

    [Fact]
    public async Task BoundedSink_RejectsByteOverflowAtExactBudgetAndRecoversAfterDrain()
    {
        var size = await RecordSizeAsync();
        var sink = new BlockingSink();
        var options = Options(sink, queueCapacity: 10);
        options.QueueByteCapacity = size * 2;
        await using var harness = new Harness(options);
        try
        {
            Emit(harness, 1); await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Emit(harness, 2); Emit(harness, 3, DiagnosticLevel.Error);
            var status = Assert.Single(harness.Pipeline.Health());
            Assert.Equal(2, status.Accepted);
            Assert.Equal(1, status.Dropped);
            Assert.Equal(size * 2, status.Bytes);
            Assert.Equal(2, status.Queued);
        }
        finally { sink.Release.TrySetResult(); }
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Emit(harness, 4);
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2, 4 }, sink.Records.Select(record => record.EventId));
        Assert.Equal(0, Assert.Single(harness.Pipeline.Health()).Bytes);
    }

    [Fact]
    public async Task SlowAndFailingSinks_AreIndependentAndFailureDoesNotStopLaterWrites()
    {
        var slow = new BlockingSink(); var healthy = new RecordingSink(); var failing = new FailOnceSink();
        await using var harness = new Harness(Options(slow, healthy, failing));
        try
        {
            Emit(harness, 1);
            await slow.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Emit(harness, 2);
            await healthy.Second.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { 1, 2 }, healthy.Records.Select(record => record.EventId));
            Assert.Equal(0, Assert.Single(harness.Pipeline.Health(), status => status.Name == "custom-0").Written);
        }
        finally { slow.Release.TrySetResult(); }
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var status = Assert.Single(harness.Pipeline.Health(), item => item.Name == "custom-2");
        Assert.Equal(2, status.Accepted);
        Assert.Equal(1, status.Failures);
        Assert.Equal(1, status.Written);
        Assert.Contains("sink failure", status.LastError);
        Assert.Equal(new[] { 2 }, failing.Records.Select(record => record.EventId));
        Assert.Equal(2, harness.Pipeline.Read().Count);
    }

    [Fact]
    public async Task MemoryRing_EvictsOldestAndReadSnapshotsRemainIndependent()
    {
        await using var harness = new Harness(new() { MemoryCapacity = 3 });
        Emit(harness, 1); Emit(harness, 2);
        var earlier = harness.Pipeline.Read();
        Emit(harness, 3); Emit(harness, 4); Emit(harness, 5);

        Assert.Equal(new[] { 1, 2 }, earlier.Select(record => record.EventId));
        Assert.Equal(new[] { 3, 4, 5 }, harness.Pipeline.Read().Select(record => record.EventId));
        Assert.Equal(new long[] { 3, 4, 5 }, harness.Pipeline.Read().Select(record => record.Sequence));
        Assert.Equal(2, harness.Pipeline.MemoryEvicted);
    }

    [Fact]
    public async Task MemoryRing_EnforcesExactByteBudgetAndRejectsOversizedRecord()
    {
        var size = await RecordSizeAsync();
        await using var harness = new Harness(new() { MemoryCapacity = 10, MemoryByteCapacity = size * 2 });
        Emit(harness, 1); Emit(harness, 2);
        Assert.Equal(new[] { 1, 2 }, harness.Pipeline.Read().Select(record => record.EventId));
        Emit(harness, 3);
        Assert.Equal(new[] { 2, 3 }, harness.Pipeline.Read().Select(record => record.EventId));
        Assert.Equal(1, harness.Pipeline.MemoryEvicted);

        await using var tooSmall = new Harness(new() { MemoryByteCapacity = size - 1 });
        Emit(tooSmall, 1);
        Assert.Empty(tooSmall.Pipeline.Read());
        Assert.Equal(1, tooSmall.Pipeline.MemoryEvicted);
    }

    [Fact]
    public async Task FlushAsync_WaitsOnlyForWritesAcceptedBeforeItsCall()
    {
        var sink = new TwoStepSink();
        await using var harness = new Harness(Options(sink));
        try
        {
            Emit(harness, 1); await sink.FirstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var flush = harness.Pipeline.FlushAsync();
            Assert.False(flush.IsCompleted);
            Emit(harness, 2);
            sink.FirstRelease.TrySetResult();
            await sink.SecondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await flush.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(sink.SecondRelease.Task.IsCompleted);
            var status = Assert.Single(harness.Pipeline.Health());
            Assert.Equal(1, status.Written);
            Assert.Equal(1, status.Queued);
        }
        finally { sink.FirstRelease.TrySetResult(); sink.SecondRelease.TrySetResult(); }
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, Assert.Single(harness.Pipeline.Health()).Written);
    }

    [Fact]
    public async Task FlushCancellation_CancelsOnlyWaitAndKeepsAdmissionAndOwnership()
    {
        var sink = new BlockingSink();
        await using var harness = new Harness(Options(sink));
        try
        {
            Emit(harness, 1); await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var flush = harness.Pipeline.FlushAsync(cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => flush);
            Assert.True(harness.Pipeline.IsEnabled(DiagnosticLevel.Information));
            Assert.Equal(0, sink.Disposals);
            Emit(harness, 2);
        }
        finally { sink.Release.TrySetResult(); }
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 1, 2 }, sink.Records.Select(record => record.EventId));
    }

    [Fact]
    public async Task DisposeTimeout_ClosesAdmissionAndRetainsSinkOwnershipUntilRetry()
    {
        var sink = new BlockingSink();
        var options = Options(sink); options.ShutdownTimeout = TimeSpan.FromMilliseconds(20);
        await using var harness = new Harness(options);
        try
        {
            Emit(harness, 1); await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => harness.Pipeline.DisposeAsync().AsTask());
            Assert.False(harness.Pipeline.IsEnabled(DiagnosticLevel.Information));
            Assert.Equal(0, sink.Disposals);
            Assert.Equal(1, Assert.Single(harness.Pipeline.Health()).Queued);
            harness.Source.Write(() => "late");
            Assert.Equal(1, harness.Pipeline.LateWrites);
        }
        finally { sink.Release.TrySetResult(); }
        await harness.Pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await harness.Pipeline.DisposeAsync();
        await harness.Pipeline.DisposeAsync();
        Assert.Equal(1, sink.Disposals);
        Assert.Equal(new[] { 1 }, sink.Records.Select(record => record.EventId));
    }

    [Fact]
    public async Task SinkDisposeFailure_IsObservableAndRetryReleasesExactlyOnce()
    {
        var sink = new RetryDisposeSink();
        await using var harness = new Harness(Options(sink));
        Emit(harness, 1);
        await Assert.ThrowsAsync<IOException>(() => harness.Pipeline.DisposeAsync().AsTask());
        var failed = Assert.Single(harness.Pipeline.Health());
        Assert.Equal(1, failed.Failures);
        Assert.Contains("cleanup failure", failed.LastError);
        await harness.Pipeline.DisposeAsync();
        await harness.Pipeline.DisposeAsync();
        Assert.Equal(2, sink.Attempts);
        Assert.Equal(1, sink.Releases);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppRegistration_SnapshotsOptionsAndDisposesOwnedSinksOnce(bool microsoftDI)
    {
        var sink = new RecordingSink();
        var options = Options(sink);
        var name = UniqueName();
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            context.UseDiagnostics(options, configuration => configuration.Filters(name));
            options.MinimumLevel = DiagnosticLevel.Critical;
            options.SinkFactories = [];
        }).Build();
        try
        {
            var pipeline = Assert.Single(app.Services.ResolveAll<DiagnosticPipeline>());
            var source = app.Services.Resolve<IEventsSourceFactory>()!.Create(name);
            source.Write(DiagnosticLevel.Information, 23, "App.Proof", "proof");
            await pipeline.FlushAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(pipeline.Read(), record => record.Source == name && record.EventId == 23);
            Assert.Contains(sink.Records, record => record.Source == name && record.EventId == 23);
        }
        finally { await app.DisposeAsync(); }
        await app.DisposeAsync();
        Assert.Equal(1, sink.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedAppSetup_CreatesOnlyReplacementPipelineAndSink(bool microsoftDI)
    {
        var discardedCreations = 0; var activeCreations = 0; var active = new RecordingSink();
        var first = new DiagnosticPipelineOptions
        {
            SinkFactories = [new Factory<IDiagnosticSink>(() => { discardedCreations++; return new RecordingSink(); })]
        };
        var replacement = new DiagnosticPipelineOptions
        {
            SinkFactories = [new Factory<IDiagnosticSink>(() => { activeCreations++; return active; })]
        };
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); })
            .Setup(context => context.UseDiagnostics(first).UseDiagnostics(replacement)).Build();
        try
        {
            Assert.Single(app.Services.ResolveAll<DiagnosticPipeline>());
            Assert.Single(app.Services.ResolveAll<DiagnosticSubscription>());
            Assert.Equal(0, discardedCreations);
            Assert.Equal(1, activeCreations);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, active.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppDispose_DrainsStopAndServiceCleanupEventsBeforeDisposingSinks(bool microsoftDI)
    {
        var sink = new RecordingSink(); var name = UniqueName();
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context => context
            .UseDiagnostics(Options(sink), configuration => configuration.Filters(name))
            .Services(services => services.Singleton<IStartable>(resolver =>
                new CleanupEmitter(resolver.Resolve<IEventsSourceFactory>()!.Create(name))))).Build();
        try
        {
            await app.StartAsync();
            await app.StopAsync();
            var pipeline = app.Services.Resolve<DiagnosticPipeline>()!;
            Assert.True(pipeline.IsEnabled(DiagnosticLevel.Information));
            var source = app.Services.Resolve<IEventsSourceFactory>()!.Create(name);
            source.Write(DiagnosticLevel.Information, 702, "AfterStop", "accepted");
            await app.DisposeAsync();
            Assert.Equal(new[] { 701, 702, 703 }, sink.Records.Where(record => record.Source == name).Select(record => record.EventId));
            Assert.Equal(1, sink.Disposals);
        }
        finally { await app.DisposeAsync(); }
    }

    [Fact]
    public async Task JsonSink_RotatesWithinUtf8BudgetAndPreservesUnrelatedFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-Diagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var unrelated = Path.Combine(directory, "keep.txt");
        await File.WriteAllTextAsync(unrelated, "unrelated");
        var sample = JsonRecord(1);
        var lineBytes = JsonSerializer.SerializeToUtf8Bytes(sample).LongLength + 1;
        try
        {
            await using (var sink = new JsonDiagnosticSink(directory, lineBytes * 2, 2, "test"))
                for (var index = 1; index <= 7; index++) await sink.WriteAsync(JsonRecord(index));
            var files = Directory.GetFiles(directory, "test-*.jsonl");
            Assert.Equal(2, files.Length);
            var ids = new List<int>();
            foreach (var file in files)
            {
                Assert.InRange(new FileInfo(file).Length, 1, lineBytes * 2);
                foreach (var line in await File.ReadAllLinesAsync(file))
                {
                    using var json = JsonDocument.Parse(line);
                    ids.Add(json.RootElement.GetProperty("EventId").GetInt32());
                    Assert.Equal("Unicode 😀 and \"quotes\"\nnext", json.RootElement.GetProperty("MessageTemplate").GetString());
                    Assert.Equal("значение", json.RootElement.GetProperty("Properties").GetProperty("key").GetString());
                }
            }
            Assert.Equal(new[] { 5, 6, 7 }, ids.Order());
            Assert.Equal("unrelated", await File.ReadAllTextAsync(unrelated));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task JsonSink_RejectsOversizedRecordBeforeCreatingJsonlFileAndAcceptsExactBoundary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-Diagnostics-" + Guid.NewGuid().ToString("N"));
        var record = JsonRecord(1);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record).LongLength + 1;
        try
        {
            await using (var tooSmall = new JsonDiagnosticSink(directory, bytes - 1, 2, "test"))
                await Assert.ThrowsAsync<IOException>(() => tooSmall.WriteAsync(record).AsTask());
            Assert.Empty(Directory.GetFiles(directory, "*.jsonl"));
            await using (var exact = new JsonDiagnosticSink(directory, bytes, 2, "test")) await exact.WriteAsync(record);
            var file = Assert.Single(Directory.GetFiles(directory, "test-*.jsonl"));
            Assert.Equal(bytes, new FileInfo(file).Length);
            using var json = JsonDocument.Parse((await File.ReadAllLinesAsync(file)).Single());
            Assert.Equal(1, json.RootElement.GetProperty("EventId").GetInt32());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task JsonSink_ReopenShrinksOwnedSlotsAndKeepsOtherNamesAndPrefixes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-Diagnostics-" + Guid.NewGuid().ToString("N"));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(JsonRecord(1)).LongLength + 1;
        try
        {
            await using (var first = new JsonDiagnosticSink(directory, bytes, 3))
                for (var index = 1; index <= 3; index++) await first.WriteAsync(JsonRecord(index));
            Assert.Equal(3, Directory.GetFiles(directory, "events-*.jsonl").Length);
            await File.WriteAllTextAsync(Path.Combine(directory, "events-keep.jsonl"), "keep name");
            await File.WriteAllTextAsync(Path.Combine(directory, "another-2.jsonl"), "keep prefix");
            await using (var reopened = new JsonDiagnosticSink(directory, bytes, 1)) await reopened.WriteAsync(JsonRecord(4));

            Assert.False(File.Exists(Path.Combine(directory, "events-1.jsonl")));
            Assert.False(File.Exists(Path.Combine(directory, "events-2.jsonl")));
            using var record = JsonDocument.Parse((await File.ReadAllLinesAsync(Path.Combine(directory, "events-0.jsonl"))).Single());
            Assert.Equal(4, record.RootElement.GetProperty("EventId").GetInt32());
            Assert.Equal("keep name", await File.ReadAllTextAsync(Path.Combine(directory, "events-keep.jsonl")));
            Assert.Equal("keep prefix", await File.ReadAllTextAsync(Path.Combine(directory, "another-2.jsonl")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task JsonSink_ExclusiveOwnerRejectsConcurrentWriterAndReleasesLeaseOnDispose()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TrueMoon-Diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var first = new JsonDiagnosticSink(directory);
            await using var second = new JsonDiagnosticSink(directory);
            await first.WriteAsync(JsonRecord(1));
            await Assert.ThrowsAsync<IOException>(() => second.WriteAsync(JsonRecord(2)).AsTask());
            await first.DisposeAsync();
            using (var original = JsonDocument.Parse((await File.ReadAllLinesAsync(Path.Combine(directory, "events-0.jsonl"))).Single()))
                Assert.Equal(1, original.RootElement.GetProperty("EventId").GetInt32());
            await second.WriteAsync(JsonRecord(2));
            await second.DisposeAsync();
            using var replacement = JsonDocument.Parse((await File.ReadAllLinesAsync(Path.Combine(directory, "events-0.jsonl"))).Single());
            Assert.Equal(2, replacement.RootElement.GetProperty("EventId").GetInt32());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void PipelineConstructionFailure_CleansPreviouslyCreatedSinkAndRejectsAliasOwnership()
    {
        var sink = new RecordingSink();
        var error = new IOException("factory failure");
        var options = Options(sink);
        options.SinkFactories = [new Factory<IDiagnosticSink>(() => sink), new Factory<IDiagnosticSink>(() => throw error)];
        Assert.Same(error, Assert.Throws<IOException>(() => new DiagnosticPipeline(options)));
        Assert.Equal(1, sink.Disposals);
        var alias = new RecordingSink();
        Assert.Throws<InvalidOperationException>(() => new DiagnosticPipeline(Options(alias, alias)));
        Assert.Equal(1, alias.Disposals);
    }

    [Theory]
    [InlineData("queue-zero")]
    [InlineData("queue-negative")]
    [InlineData("reserve-negative")]
    [InlineData("reserve-full")]
    [InlineData("bytes-zero")]
    [InlineData("memory-zero")]
    [InlineData("properties-negative")]
    [InlineData("timeout-zero")]
    [InlineData("timeout-excessive")]
    [InlineData("level-invalid")]
    public void InvalidBudgets_AreRejectedBeforeCreatingSinks(string invalid)
    {
        var creations = 0;
        var options = new DiagnosticPipelineOptions { SinkFactories = [new Factory<IDiagnosticSink>(() => { creations++; return new RecordingSink(); })] };
        switch (invalid)
        {
            case "queue-zero": options.QueueCapacity = 0; break;
            case "queue-negative": options.QueueCapacity = -1; break;
            case "reserve-negative": options.ReservedErrorCapacity = -1; break;
            case "reserve-full": options.ReservedErrorCapacity = options.QueueCapacity; break;
            case "bytes-zero": options.QueueByteCapacity = 0; break;
            case "memory-zero": options.MemoryCapacity = 0; break;
            case "properties-negative": options.MaxProperties = -1; break;
            case "timeout-zero": options.ShutdownTimeout = TimeSpan.Zero; break;
            case "timeout-excessive": options.ShutdownTimeout = TimeSpan.FromSeconds(31); break;
            case "level-invalid": options.MinimumLevel = (DiagnosticLevel)99; break;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticPipeline(options));
        Assert.Equal(0, creations);
    }

    private static void Emit(Harness harness, int id, DiagnosticLevel level = DiagnosticLevel.Information)
        => harness.Source.Write(level, id, "Event", "message", caller: "");

    private static DiagnosticPipelineOptions Options(IDiagnosticSink sink, int queueCapacity = 16, int reserved = 0)
        => new() { QueueCapacity = queueCapacity, ReservedErrorCapacity = reserved, SinkFactories = [new Factory<IDiagnosticSink>(() => sink)] };

    private static DiagnosticPipelineOptions Options(params IDiagnosticSink[] sinks)
        => new() { SinkFactories = sinks.Select(sink => (IFactory<IDiagnosticSink>)new Factory<IDiagnosticSink>(() => sink)).ToArray() };

    private static async Task<long> RecordSizeAsync()
    {
        var sink = new BlockingSink();
        await using var harness = new Harness(Options(sink));
        try
        {
            Emit(harness, 1); await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return Assert.Single(harness.Pipeline.Health()).Bytes;
        }
        finally { sink.Release.TrySetResult(); }
    }

    private static InvalidOperationException RecordedError()
    {
        try { throw new InvalidOperationException("error-message"); }
        catch (InvalidOperationException error) { return error; }
    }

    private static DiagnosticRecord JsonRecord(int id)
        => new(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), id, Guid.Empty, 123, "source",
            DiagnosticLevel.Information, id, "Event", "Unicode 😀 and \"quotes\"\nnext",
            new Dictionary<string, string?> { ["key"] = "значение" }.ToFrozenDictionary(),
            null, null, null, null, null, null, false);

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string UniqueName() => "PipelineTests." + Guid.NewGuid().ToString("N");

    private sealed class Harness : IAsyncDisposable
    {
        private readonly DiagnosticSubscription _subscription;
        internal DiagnosticPipeline Pipeline { get; }
        internal EventsSource Source { get; }
        internal Harness(DiagnosticPipelineOptions options, Action<DiagnosticEvent>? legacy = null, string? sourceName = null)
        {
            Pipeline = new(options);
            var configuration = new DiagnosticsConfiguration();
            var name = sourceName ?? UniqueName(); configuration.AddFilter(name);
            if (legacy is not null) configuration.AddEventListener(legacy);
            _subscription = new(configuration, Pipeline);
            Source = new(name);
        }
        public async ValueTask DisposeAsync()
        {
            Source.Dispose(); _subscription.Dispose(); await Pipeline.DisposeAsync();
        }
    }

    private class RecordingSink : IDiagnosticSink
    {
        internal readonly ConcurrentQueue<DiagnosticRecord> Records = new();
        internal readonly TaskCompletionSource Second = Signal();
        internal int Disposals;
        public virtual ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            Records.Enqueue(record);
            if (Records.Count >= 2) Second.TrySetResult();
            return ValueTask.CompletedTask;
        }
        public virtual ValueTask DisposeAsync() { Interlocked.Increment(ref Disposals); return ValueTask.CompletedTask; }
    }

    private sealed class BlockingSink : RecordingSink
    {
        internal readonly TaskCompletionSource Entered = Signal(), Release = Signal();
        public override async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(); await Release.Task;
            await base.WriteAsync(record, cancellationToken);
        }
    }

    private sealed class FailOnceSink : RecordingSink
    {
        private int _calls;
        public override ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1) throw new IOException("sink failure");
            return base.WriteAsync(record, cancellationToken);
        }
    }

    private sealed class TwoStepSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource FirstEntered = Signal(), FirstRelease = Signal();
        internal readonly TaskCompletionSource SecondEntered = Signal(), SecondRelease = Signal();
        public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default)
        {
            if (record.EventId == 1) { FirstEntered.TrySetResult(); await FirstRelease.Task; }
            else { SecondEntered.TrySetResult(); await SecondRelease.Task; }
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RetryDisposeSink : RecordingSink
    {
        internal int Attempts, Releases;
        public override ValueTask DisposeAsync()
        {
            if (Interlocked.Increment(ref Attempts) == 1) throw new IOException("cleanup failure");
            Interlocked.Increment(ref Releases); return ValueTask.CompletedTask;
        }
    }

    private sealed class DangerousPayload
    {
        public override string ToString() => throw new InvalidOperationException("Payload formatting must not run on the producer.");
    }

    private sealed class BrokenMessageException : Exception
    {
        public override string Message => throw new InvalidOperationException("Exception formatting failed.");
    }

    private sealed class CleanupEmitter(IEventsSource source) : IStartable, IStoppable, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            source.Write(DiagnosticLevel.Information, 701, "Stopping", "cleanup");
            return Task.CompletedTask;
        }
        public void Dispose() => source.Write(DiagnosticLevel.Information, 703, "Disposed", "cleanup");
    }
}
