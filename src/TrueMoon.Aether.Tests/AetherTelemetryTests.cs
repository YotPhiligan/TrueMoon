using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Builds;
using TrueMoon.Aether.Tests.Preview;
using TrueMoon.Aether.Tests.Replacements;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests;

public sealed class AetherTelemetryTests(GameBuildFixture builds) : IClassFixture<GameBuildFixture>
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static SceneSnapshot Scene() => new("room", 7, []);

    [Theory]
    [InlineData("success", false)] [InlineData("success", true)]
    [InlineData("failure", false)] [InlineData("cancel", false)]
    public async Task Build_EndsCorrelatedScopeBeforeReturningResultFailureOrCancellation(string outcome, bool microsoftDI)
    {
        await using var harness = new Harness(builds.Root, microsoftDI);
        using var cancellation = new CancellationTokenSource();
        string project;
        string? marker = null;
        string? release = null;
        if (outcome == "cancel") { var held = builds.HeldProject(); project = held.Project; marker = held.Marker; release = held.Release; }
        else project = outcome == "failure" ? builds.Project("telemetry-invalid-" + Guid.NewGuid().ToString("N"), "Game.Invalid", "invalid C#") : builds.ProjectA;
        var request = new GameBuildRequest(project, outcome == "failure" ? "Game.Invalid.dll" : outcome == "cancel" ? "Game.Held.dll" : builds.A.EntryAssembly,
            outcome == "failure" ? "Game.Invalid.Entry" : outcome == "cancel" ? "Game.Candidate.Game" : builds.A.EntryType, 71,
            outcome == "success" ? ["Game.Private.dll"] : []);
        var pending = harness.Host.Builds.BuildAsync(request, cancellation.Token);
        try
        {
            if (outcome == "cancel")
            {
                await TestWait.UntilAsync(() => File.Exists(marker!), TimeSpan.FromSeconds(30));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(30)));
            }
            else if (outcome == "failure") await Assert.ThrowsAsync<GameBuildException>(() => pending);
            else Assert.Equal(71, (await pending).SourceRevision);
        }
        finally { if (release != null) File.WriteAllText(release, "release"); }

        var span = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == "Aether.Build");
        var expected = outcome == "success" ? "Succeeded" : outcome == "failure" ? "Failed" : "Cancelled";
        Assert.Equal(expected, span.GetTagItem("outcome")); Assert.Equal("71", span.GetTagItem("SourceRevision"));
        Assert.Equal(0, harness.Telemetry.Read().ActiveOperations);
        var records = harness.Records(span);
        Assert.Equal(new[] { 1200, outcome == "success" ? 1201 : outcome == "cancel" ? 1202 : 1203 }, records.Select(record => record.EventId));
        Assert.All(records, record =>
        {
            Assert.Equal("71", record.Properties["SourceRevision"]);
            Assert.Equal(Path.GetFileName(harness.Host.Builds.DiagnosticsDirectory), record.Properties["JobId"]);
            Assert.DoesNotContain("Nonce", record.Properties.Keys);
        });
        Assert.Null(harness.Host.Builds.ProcessId);
        AssertMetrics(harness.Observer, "Aether.Build", expected);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Replace_SuccessAndPreflightCancellationPreserveExpectedIdentityAndOperationOutcome(bool cancelled)
    {
        var runtimes = new ReplacementRuntimeFixture();
        await using var harness = new Harness(builds.Root, runtimes: runtimes, gpu: false);
        await harness.Host.Supervisor.StartAsync(Scene(), builds.A);
        var before = harness.Host.Supervisor.Status.Identity!;
        using var cancellation = new CancellationTokenSource(); if (cancelled) cancellation.Cancel();
        var replacing = harness.Host.Replacement.ReplaceAsync(new(builds.B, before, Scene(), 8), cancellation.Token);
        if (cancelled) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replacing);
        else
        {
            var result = await replacing;
            Assert.Equal(before, result.PreviousIdentity); Assert.Equal(builds.B.BuildId, result.Identity.GameBuildId);
            Assert.True(runtimes.Leases[0].ResourcesReleased);
        }
        var span = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == "Aether.Replace");
        var records = harness.Records(span);
        Assert.Equal(new[] { 1300, cancelled ? 1302 : 1301 }, records.Select(record => record.EventId));
        Assert.All(records, record =>
        {
            Assert.Equal(before.SessionId.ToString("D"), record.Properties["ExpectedSessionId"]);
            Assert.Equal(before.Generation.ToString(), record.Properties["ExpectedGeneration"]);
            Assert.Equal(before.GameBuildId, record.Properties["ExpectedGameBuildId"]);
            Assert.Equal(builds.B.BuildId, record.Properties["CandidateBuildId"]);
            Assert.DoesNotContain(before.Nonce, record.Properties.Values);
        });
        if (cancelled) { Assert.Single(runtimes.Leases); Assert.Equal(before, harness.Host.Supervisor.Status.Identity); Assert.Equal(0, runtimes.Leases[0].Stops); }
        AssertMetrics(harness.Observer, "Aether.Replace", cancelled ? "Cancelled" : "Succeeded");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Replace_RecoveryFieldsAndDistinctCausesRemainCorrelatedToOriginalFailure(bool recoveryFails)
    {
        var original = new IOException("original candidate failure");
        var recovery = new UnauthorizedAccessException("separate recovery failure");
        var runtimes = new ReplacementRuntimeFixture();
        await using var harness = new Harness(builds.Root, runtimes: runtimes, gpu: false);
        await harness.Host.Supervisor.StartAsync(Scene(), builds.A);
        var before = harness.Host.Supervisor.Status.Identity!;
        runtimes.Configure = lease =>
        {
            if (runtimes.Leases.Count == 2) lease.BeforeSend = (_, _) => throw original;
            if (runtimes.Leases.Count == 3 && recoveryFails) lease.BeforeSend = (_, _) => throw recovery;
        };
        var error = await Assert.ThrowsAsync<GameReplacementException>(() => harness.Host.Replacement.ReplaceAsync(new(builds.B, before, Scene(), 8)));
        Assert.Same(original, error.InnerException); Assert.Equal("Candidate", error.Phase);
        Assert.True(error.RollbackAttempted); Assert.Equal(!recoveryFails, error.RollbackSucceeded);
        if (recoveryFails) Assert.Same(recovery, error.RecoveryError); else Assert.Null(error.RecoveryError);
        var span = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == "Aether.Replace");
        Assert.Equal("Failed", span.GetTagItem("outcome")); Assert.Equal(typeof(IOException).FullName, span.GetTagItem("error.type"));
        var records = harness.Records(span).Where(record => record.EventName.StartsWith("Replace.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(recoveryFails ? new[] { 1300, 1304, 1306, 1303 } : new[] { 1300, 1304, 1305, 1306, 1303 }, records.Select(record => record.EventId));
        var terminal = records[^1];
        Assert.Equal(typeof(IOException).FullName, terminal.ExceptionType); Assert.Equal(original.Message, terminal.ExceptionMessage);
        Assert.Equal("Candidate", terminal.Properties["Phase"]); Assert.Equal("True", terminal.Properties["RollbackAttempted"]);
        Assert.Equal((!recoveryFails).ToString(), terminal.Properties["RollbackSucceeded"]);
        Assert.Equal(recoveryFails ? typeof(UnauthorizedAccessException).FullName : null, terminal.Properties["RecoveryErrorType"]);
        Assert.Equal(before.Generation.ToString(), terminal.Properties["ExpectedGeneration"]);
        var recoveryResult = Assert.Single(records, record => record.EventId == 1306);
        Assert.Equal(recoveryFails ? typeof(UnauthorizedAccessException).FullName : null, recoveryResult.ExceptionType);
        Assert.Equal((!recoveryFails).ToString(), recoveryResult.Properties["RollbackSucceeded"]);
        Assert.Equal(3, runtimes.Leases.Count); Assert.Equal(1, runtimes.MaxLive);
        AssertMetrics(harness.Observer, "Aether.Replace", "Failed");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Preview_ShowResizeAndStopCompleteOnlyAfterActualPresentationAndDoNotLogEveryFrame(bool microsoftDI)
    {
        await using var harness = new Harness(builds.Root, microsoftDI);
        var showing = harness.Host.Preview.ShowAsync(PreviewFixture.Scene(), 32, 16);
        var first = await harness.ConfiguredAsync();
        var show = Assert.Single(harness.Observer.Started, activity => activity.OperationName == "Aether.Preview.Show");
        Assert.False(showing.IsCompleted); Assert.Equal(new[] { 1400 }, harness.Records(show).Select(record => record.EventId));
        Assert.DoesNotContain(harness.Observer.Stopped, activity => ReferenceEquals(activity, show));
        Assert.True(harness.Host.Preview.Presented(first.Issue(PreviewFixture.Metadata())));
        var document = await showing.WaitAsync(Deadline);
        Assert.Equal(document, harness.Host.Preview.Document);
        Assert.Equal(new[] { 1400, 1401 }, harness.Records(show).Select(record => record.EventId));
        Assert.Equal(document.Runtime.SessionId.ToString("D"), harness.Records(show)[^1].Properties["SessionId"]);
        Assert.Equal("7", harness.Records(show)[^1].Properties["RequestedSceneRevision"]);
        AssertMetrics(harness.Observer, "Aether.Preview.Show", "Succeeded");

        var resizing = harness.Host.Preview.ResizeAsync(64, 32);
        var second = await harness.ConfiguredAsync();
        var resize = Assert.Single(harness.Observer.Started, activity => activity.OperationName == "Aether.Preview.Resize");
        Assert.False(resizing.IsCompleted); Assert.Equal(new[] { 1410 }, harness.Records(resize).Select(record => record.EventId));
        Assert.True(second.Surface.Generation > first.Surface.Generation);
        Assert.True(harness.Host.Preview.Presented(second.Issue(PreviewFixture.Metadata())));
        await resizing.WaitAsync(Deadline);
        Assert.Equal(new[] { 1410, 1411 }, harness.Records(resize).Select(record => record.EventId));
        Assert.Equal("64", harness.Records(resize)[^1].Properties["Width"]); Assert.Equal("32", harness.Records(resize)[^1].Properties["Height"]);
        AssertMetrics(harness.Observer, "Aether.Preview.Resize", "Succeeded");

        var beforeFrames = harness.PreviewRecords(document.Runtime.SessionId).Length;
        var beforeSpans = harness.Observer.Stopped.Count; var beforeMeasurements = harness.Observer.Measurements.Count;
        for (ulong frame = 2; frame <= 6; frame++) Assert.True(harness.Host.Preview.Presented(second.Issue(PreviewFixture.Metadata(frame))));
        Assert.Equal(beforeFrames, harness.PreviewRecords(document.Runtime.SessionId).Length);
        Assert.Equal(beforeSpans, harness.Observer.Stopped.Count); Assert.Equal(beforeMeasurements, harness.Observer.Measurements.Count);
        await harness.Host.Preview.StopAsync().WaitAsync(Deadline);
        var stop = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == "Aether.Preview.Stop");
        Assert.Equal(new[] { 1420, 1421 }, harness.Records(stop).Select(record => record.EventId));
        AssertMetrics(harness.Observer, "Aether.Preview.Stop", "Succeeded");
        Assert.True(Assert.Single(harness.Runtimes!.Leases).ResourcesReleased);
        Assert.True(Assert.Single(harness.Viewport!.Connections).Disposed);
        Assert.Null(harness.Host.Preview.Document); Assert.Equal(0, harness.Telemetry.Read().Failures);
    }

    [Theory]
    [InlineData("Show", false)] [InlineData("Show", true)]
    [InlineData("Resize", false)] [InlineData("Resize", true)]
    public async Task Preview_FailureAndCancellationKeepOriginalOutcomeAndCloseTheOperation(string action, bool cancelled)
    {
        await using var harness = new Harness(builds.Root);
        var original = new IOException("original preview operation");
        using var cancellation = new CancellationTokenSource();
        if (action == "Resize") await harness.ShowReadyAsync();
        if (!cancelled)
        {
            if (action == "Show") harness.Runtimes!.LaunchError = original;
            else harness.Viewport!.ConfigureError = original;
        }
        var pending = action == "Show" ? (Task)harness.Host.Preview.ShowAsync(PreviewFixture.Scene(), 32, 16, cancellation.Token)
            : harness.Host.Preview.ResizeAsync(64, 32, cancellation.Token);
        if (cancelled)
        {
            await harness.ConfiguredAsync(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Deadline));
        }
        else Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => pending.WaitAsync(Deadline)));
        var name = "Aether.Preview." + action;
        var span = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == name);
        var id = action == "Show" ? 1400 : 1410;
        Assert.Equal(new[] { id, id + (cancelled ? 2 : 3) }, harness.Records(span).Select(record => record.EventId));
        Assert.Equal(cancelled ? DiagnosticLevel.Warning : DiagnosticLevel.Error, harness.Records(span)[^1].Level);
        if (!cancelled) Assert.Same(original, harness.Host.Preview.Error);
        Assert.Null(harness.Host.Preview.Document); Assert.All(harness.Runtimes!.Leases, lease => Assert.True(lease.ResourcesReleased));
        AssertMetrics(harness.Observer, name, cancelled ? "Cancelled" : "Failed");
        Assert.Equal(0, harness.Telemetry.Read().ActiveOperations);
    }

    [Fact]
    public async Task PreviewStop_CleanupFailureIsReportedAndRetainedUntilSuccessfulRetry()
    {
        await using var harness = new Harness(builds.Root);
        await harness.ShowReadyAsync();
        var connection = Assert.Single(harness.Viewport!.Connections); connection.FailDispose = true;
        try
        {
            var error = await Assert.ThrowsAsync<IOException>(() => harness.Host.Preview.StopAsync().WaitAsync(Deadline));
            Assert.Same(error, harness.Host.Preview.Error); Assert.False(connection.Disposed);
            Assert.False(harness.Runtimes!.Leases[0].ResourcesReleased); Assert.Equal(0, harness.Runtimes.Leases[0].Stops);
            var span = Assert.Single(harness.Observer.Stopped, activity => activity.OperationName == "Aether.Preview.Stop");
            var records = harness.Records(span);
            Assert.Equal(new[] { 1420, 1423 }, records.Select(record => record.EventId));
            Assert.Equal(error.Message, records[^1].ExceptionMessage); Assert.Equal(typeof(IOException).FullName, records[^1].ExceptionType);
            AssertMetrics(harness.Observer, "Aether.Preview.Stop", "Failed");
        }
        finally { connection.FailDispose = false; }
        await harness.Host.Preview.StopAsync().WaitAsync(Deadline);
        Assert.True(connection.Disposed); Assert.True(harness.Runtimes!.Leases[0].ResourcesReleased);
        Assert.Equal(new[] { "Failed", "Succeeded" }, harness.Observer.Stopped.Where(activity => activity.OperationName == "Aether.Preview.Stop").Select(activity => activity.GetTagItem("outcome")));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task FrameFault_CapturesPreDrainIdentitySurfaceAndOriginalFailedOutcome(bool afterPresentation, bool cleanupFails)
    {
        await using var harness = new Harness(builds.Root);
        var stopped = new TaskCompletionSource<Activity>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Observer.OnStopped = activity => { if (activity.OperationName == "Aether.FrameFault") stopped.TrySetResult(activity); };
        var showing = harness.Host.Preview.ShowAsync(PreviewFixture.Scene(), 32, 16);
        var surface = await harness.ConfiguredAsync();
        var identity = surface.Surface.Identity;
        if (afterPresentation) { Assert.True(harness.Host.Preview.Presented(surface.Issue(PreviewFixture.Metadata()))); await showing.WaitAsync(Deadline); }
        var connection = Assert.Single(harness.Viewport!.Connections);
        var original = new InvalidDataException("original frame failure");
        connection.FailDispose = cleanupFails;
        try
        {
            connection.Fail(original);
            if (!afterPresentation) Assert.Same(original, await Assert.ThrowsAsync<InvalidDataException>(() => showing.WaitAsync(Deadline)));
            var span = await stopped.Task.WaitAsync(Deadline);
            await TestWait.UntilAsync(() => harness.Telemetry.Read().ActiveOperations == 0, Deadline);
            Assert.Equal("Failed", span.GetTagItem("outcome")); Assert.Equal(typeof(InvalidDataException).FullName, span.GetTagItem("error.type"));
            var records = harness.Records(span);
            Assert.Equal(new[] { 1500, cleanupFails ? 1503 : 1501 }, records.Select(record => record.EventId));
            Assert.All(records, record =>
            {
                Assert.Equal(identity.SessionId.ToString("D"), record.Properties["SessionId"]);
                Assert.Equal(identity.Generation.ToString(), record.Properties["Generation"]);
                Assert.Equal(identity.GameBuildId, record.Properties["GameBuildId"]);
                Assert.Equal(surface.Surface.Generation.ToString(), record.Properties["SurfaceGeneration"]);
                Assert.Equal("7", record.Properties["SceneRevision"]); Assert.Equal("Paused", record.Properties["State"]);
                Assert.Equal(afterPresentation ? "FrameTransport" : "FirstFrame", record.Properties["Phase"]);
                Assert.Equal("Gpu", record.Properties["Transport"]); Assert.DoesNotContain(identity.Nonce, record.Properties.Values);
            });
            Assert.Equal(typeof(InvalidDataException).FullName, records[0].ExceptionType); Assert.Equal(original.Message, records[0].ExceptionMessage);
            if (cleanupFails)
            {
                Assert.Equal(typeof(AggregateException).FullName, records[^1].ExceptionType);
                var error = Assert.IsType<AggregateException>(harness.Host.Frames.TransportError);
                Assert.Contains(original, error.InnerExceptions); Assert.Contains(error.InnerExceptions, failure => failure is IOException);
                Assert.False(connection.Disposed); Assert.False(harness.Runtimes!.Leases[0].ResourcesReleased);
            }
            else { Assert.True(connection.Disposed); Assert.True(harness.Runtimes!.Leases[0].ResourcesReleased); }
            AssertMetrics(harness.Observer, "Aether.FrameFault", "Failed");
        }
        finally { connection.FailDispose = false; }
        await harness.Host.Preview.StopAsync().WaitAsync(Deadline);
        Assert.True(connection.Disposed); Assert.True(harness.Runtimes!.Leases[0].ResourcesReleased);
    }

    [Theory]
    [InlineData("start", false)] [InlineData("start", true)]
    [InlineData("complete", false)] [InlineData("complete", true)]
    [InlineData("dispose", false)] [InlineData("dispose", true)]
    public async Task InjectedTelemetryFailure_DoesNotReplaceBusinessResultOrOriginalException(string callback, bool businessFails)
    {
        var telemetry = new ThrowingTelemetry(callback);
        var original = new IOException("original operation");
        var pending = AetherDiagnosticEvents.ObserveAsync<int>(null, telemetry, "Probe", 1600,
            () => new Dictionary<string, string?>(), () => businessFails ? Task.FromException<int>(original) : Task.FromResult(42));
        if (businessFails) Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => pending));
        else Assert.Equal(42, await pending);
        Assert.Equal(1, telemetry.Starts); Assert.Equal("Aether.Probe", telemetry.Name);
        Assert.Equal(callback == "start" ? 0 : 1, telemetry.Completions); Assert.Equal(callback == "start" ? 0 : 1, telemetry.Disposals);
        if (callback != "start") { Assert.Equal(businessFails ? DiagnosticOperationOutcome.Failed : DiagnosticOperationOutcome.Succeeded, telemetry.Outcome); Assert.Same(businessFails ? original : null, telemetry.Error); }
    }

    [Fact]
    public void SafeOperation_OnlyCompletesAndDisposesBorrowedScopeOnce()
    {
        var telemetry = new ThrowingTelemetry("none");
        using var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(AetherDiagnosticEvents.Start(telemetry, "Aether.Once", () => new Dictionary<string, string?>()));
        operation.Complete(DiagnosticOperationOutcome.Cancelled); operation.Complete(); operation.Dispose(); operation.Dispose();
        Assert.Equal(1, telemetry.Starts); Assert.Equal(1, telemetry.Completions); Assert.Equal(1, telemetry.Disposals);
        Assert.Equal(DiagnosticOperationOutcome.Cancelled, telemetry.Outcome);
    }

    private static void AssertMetrics(EngineTelemetryObserver observer, string name, string outcome)
    {
        var values = observer.Measurements.Where(value => Equals(value.Tags.GetValueOrDefault("operation"), name)).ToArray();
        Assert.Equal(2, values.Length);
        Assert.Equal(1L, Assert.IsType<long>(Assert.Single(values, value => value.Instrument == "truemoon.operation.count").Value));
        Assert.True(double.IsFinite(Assert.IsType<double>(Assert.Single(values, value => value.Instrument == "truemoon.operation.duration").Value)));
        Assert.All(values, value => { Assert.Equal(new[] { "operation", "outcome" }, value.Tags.Keys.Order()); Assert.Equal(outcome, value.Tags["outcome"]); });
    }

    private sealed class Harness : IAsyncDisposable
    {
        internal readonly IApp App;
        internal readonly AetherHost Host;
        internal readonly DiagnosticPipeline Pipeline;
        internal readonly DiagnosticTelemetry Telemetry;
        internal readonly EngineTelemetryObserver Observer;
        internal readonly PreviewFixture.RuntimeFactory? Runtimes;
        internal readonly PreviewFixture.Transport? Viewport;
        internal Harness(string root, bool microsoftDI = false, ReplacementRuntimeFixture? runtimes = null, bool gpu = true)
        {
            if (runtimes == null) Runtimes = new();
            if (gpu) Viewport = new(ViewportTransportKind.Gpu);
            App = global::TrueMoon.App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context => context
                .UseDiagnostics(new DiagnosticPipelineOptions { MemoryCapacity = 8192, MemoryByteCapacity = 8 * 1024 * 1024, Emergency = new() { Enabled = false } })
                .UseAether(options =>
                {
                    options.HostContractId = GameBuildFixture.Hash; options.GameBuildId = GameBuildFixture.Hash;
                    options.ReportDirectory = Path.Combine(root, "telemetry-" + Guid.NewGuid().ToString("N"));
                    options.BuildDirectory = Path.Combine(options.ReportDirectory, "builds");
                    options.RuntimeFactory = _ => runtimes != null ? runtimes : Runtimes!;
                    if (Viewport != null) options.ViewportTransport = _ => Viewport;
                    options.Deadlines = new() { HeartbeatInterval = TimeSpan.FromSeconds(30) };
                })).Build();
            Host = App.Services.Resolve<AetherHost>()!; Pipeline = App.Services.Resolve<DiagnosticPipeline>()!;
            Telemetry = Assert.IsType<DiagnosticTelemetry>(App.Services.Resolve<IDiagnosticTelemetry>());
            Observer = new(Telemetry);
        }
        internal DiagnosticRecord[] Records(Activity span) => Pipeline.Read().Where(record => record.TraceId == span.TraceId.ToHexString() && record.SpanId == span.SpanId.ToHexString()
            && record.EventName != "Session.StateChanged" && record.EventName != "Runtime.Process").ToArray();
        internal DiagnosticRecord[] PreviewRecords(Guid session) => Pipeline.Read().Where(record => record.Source == typeof(ScenePreview).FullName && record.Properties.GetValueOrDefault("SessionId") == session.ToString("D")).ToArray();
        internal Task<ViewportPresentation> ConfiguredAsync() => Viewport!.Configurations.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
        internal async Task ShowReadyAsync()
        {
            var pending = Host.Preview.ShowAsync(PreviewFixture.Scene(), 32, 16); var surface = await ConfiguredAsync();
            Assert.True(Host.Preview.Presented(surface.Issue(PreviewFixture.Metadata()))); await pending.WaitAsync(Deadline);
        }
        public async ValueTask DisposeAsync() { try { await App.DisposeAsync(); } finally { Observer.Dispose(); } }
    }

    private sealed class ThrowingTelemetry(string callback) : IDiagnosticTelemetry, IDiagnosticOperation
    {
        internal int Starts, Completions, Disposals;
        internal string? Name; internal Exception? Error; internal DiagnosticOperationOutcome Outcome;
        public IDiagnosticOperation? StartOperation(string name, Func<IReadOnlyDictionary<string, string?>>? properties = null)
        { Starts++; Name = name; if (callback == "start") throw new IOException("telemetry start"); return this; }
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null)
        { Completions++; Outcome = outcome; Error = error; if (callback == "complete") throw new IOException("telemetry complete"); }
        public void Dispose() { Disposals++; if (callback == "dispose") throw new IOException("telemetry dispose"); }
    }
}

internal sealed record EngineTelemetryMeasurement(string Instrument, object Value, IReadOnlyDictionary<string, object?> Tags);

internal sealed class EngineTelemetryObserver : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _metrics;
    internal readonly ConcurrentQueue<Activity> Started = new(), Stopped = new();
    internal readonly ConcurrentQueue<EngineTelemetryMeasurement> Measurements = new();
    internal Action<Activity>? OnStopped;
    internal EngineTelemetryObserver(DiagnosticTelemetry owner)
    {
        _activities = new()
        {
            ShouldListenTo = source => ReferenceEquals(source, owner.ActivitySource),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Started.Enqueue(activity),
            ActivityStopped = activity => { Stopped.Enqueue(activity); OnStopped?.Invoke(activity); }
        };
        ActivitySource.AddActivityListener(_activities);
        _metrics = new() { InstrumentPublished = (instrument, listener) => { if (ReferenceEquals(instrument.Meter, owner.Meter)) listener.EnableMeasurementEvents(instrument); } };
        _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Observe(instrument, value, tags));
        _metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Observe(instrument, value, tags));
        _metrics.Start();
    }
    private void Observe(Instrument instrument, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        => Measurements.Enqueue(new(instrument.Name, value, tags.ToArray().ToDictionary(pair => pair.Key, pair => pair.Value)));
    public void Dispose() { _activities.Dispose(); _metrics.Dispose(); }
}
