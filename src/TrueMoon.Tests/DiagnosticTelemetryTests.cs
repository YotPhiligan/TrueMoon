using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public sealed class DiagnosticTelemetryTests
{
    [Theory]
    [InlineData(ActivityKind.Client)] [InlineData(ActivityKind.Server)]
    public void ExplicitRemoteParent_UsesRequestedKindInsteadOfAmbientParentAndRestoresCurrent(ActivityKind kind)
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        using var ambient = new Activity("UnrelatedAmbient").SetIdFormat(ActivityIdFormat.W3C).Start();
        var remote = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        ActivityContext observedParent = default;
        observer.OnSampleParent = context => observedParent = context;
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Remote", kind, remote)))
        {
            var activity = Assert.Single(observer.Started);
            Assert.Same(activity, Activity.Current); Assert.Equal(kind, activity.Kind);
            Assert.Equal(remote.TraceId, activity.TraceId); Assert.Equal(remote.SpanId, activity.ParentSpanId);
            Assert.NotEqual(ambient.TraceId, activity.TraceId); Assert.Null(activity.Parent);
            Assert.Equal(remote, observedParent); Assert.True(observedParent.IsRemote);
            operation.Complete();
        }
        Assert.Same(ambient, Activity.Current);
        Assert.Equal("Succeeded", Assert.Single(observer.Stopped).GetTagItem("outcome"));
        Assert.Equal(2, observer.Measurements.Count); Assert.Equal(0, telemetry.Read().ActiveOperations);
    }

    [Theory]
    [InlineData(-1)] [InlineData(99)]
    public void InvalidActivityKind_IsRejectedBeforePropertiesOrObservers(int kind)
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        var calls = 0;
        Assert.Null(telemetry.StartOperation("InvalidKind", (ActivityKind)kind, null, properties: () => { calls++; throw new InvalidOperationException(); }));
        Assert.Equal(0, calls); Assert.Empty(observer.Started); Assert.Empty(observer.Measurements);
        Assert.Equal(new DiagnosticTelemetryStatus(0, 1, 0, false, false), telemetry.Read());
    }

    [Fact]
    public void InterfaceKindOverload_ForwardsToExistingOldOnlyImplementationWithoutEvaluatingProperties()
    {
        var legacy = new LegacyTelemetry(); IDiagnosticTelemetry telemetry = legacy;
        var calls = 0;
        Func<IReadOnlyDictionary<string, string?>> properties = () => { calls++; return new Dictionary<string, string?>(); };
        var remote = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true);
        var operation = telemetry.StartOperation("Compatibility", ActivityKind.Server, remote, properties);
        Assert.Same(legacy, operation); Assert.Equal(1, legacy.Starts);
        Assert.Equal("Compatibility", legacy.Name); Assert.Same(properties, legacy.Properties); Assert.Equal(0, calls);
        operation!.Complete(); operation.Dispose();
        Assert.Equal(1, legacy.Completions); Assert.Equal(1, legacy.Disposals);
    }

    [Fact]
    public void OldOperationOverload_DefaultLiteralRemainsUnambiguousAndUsesInternalKind()
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("LegacyDefault", default))) operation.Complete();
        var activity = Assert.Single(observer.Stopped);
        Assert.Equal(ActivityKind.Internal, activity.Kind); Assert.Equal("Succeeded", activity.GetTagItem("outcome"));
        Assert.Equal(2, observer.Measurements.Count); Assert.Equal(0, telemetry.Read().Failures);
    }

    [Fact]
    public void NoConsumers_ReturnsNullWithoutEvaluatingPropertiesOrRejectingWork()
    {
        using var telemetry = new DiagnosticTelemetry();
        var evaluations = 0;
        Assert.Null(telemetry.StartOperation("NoConsumer", () => { evaluations++; throw new InvalidOperationException(); }));
        Assert.Equal(0, evaluations);
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, false, false), telemetry.Read());
        Assert.NotEqual(Guid.Empty, telemetry.ApplicationId);
        Assert.Equal("TrueMoon", telemetry.ActivitySource!.Name); Assert.Equal("TrueMoon", telemetry.Meter.Name);
        telemetry.Dispose(); telemetry.Dispose();
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, true, true), telemetry.Read());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EnableFlags_IndependentlyControlTraceAndMetricConsumers(bool tracing, bool metrics)
    {
        using var telemetry = new DiagnosticTelemetry(new() { TracingEnabled = tracing, MetricsEnabled = metrics });
        using var observer = new TelemetryTestObserver(telemetry);
        var evaluations = 0;
        var operation = telemetry.StartOperation("Flags", () => { evaluations++; return new Dictionary<string, string?> { ["proof"] = "value" }; });
        Assert.Equal(tracing || metrics, operation is not null);
        operation?.Complete(); operation?.Dispose();
        Assert.Equal(tracing ? 1 : 0, evaluations);
        Assert.Equal(tracing ? 1 : 0, observer.Stopped.Count);
        Assert.Equal(metrics ? 2 : 0, observer.Measurements.Count);
        Assert.Equal(0, telemetry.Read().ActiveOperations); Assert.Equal(0, telemetry.Read().RejectedOperations);
    }

    [Theory]
    [InlineData(ActivitySamplingResult.None)]
    [InlineData(ActivitySamplingResult.PropagationData)]
    public void Sampling_ControlsScopeAndLazyPropertiesWithoutCountingConsumerRejection(ActivitySamplingResult sampling)
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry, sampling, metrics: false);
        var evaluations = 0;
        var original = Activity.Current;
        var operation = telemetry.StartOperation("Sampled", () => { evaluations++; throw new InvalidOperationException(); });
        if (sampling == ActivitySamplingResult.None)
        {
            Assert.Null(operation); Assert.Empty(observer.Started);
        }
        else
        {
            Assert.NotNull(operation);
            var activity = Assert.Single(observer.Started);
            Assert.Same(activity, Activity.Current); Assert.False(activity.IsAllDataRequested);
            Assert.False(activity.Recorded); Assert.Empty(activity.TagObjects);
            operation.Complete(); operation.Dispose();
            Assert.Same(activity, Assert.Single(observer.Stopped)); Assert.Empty(activity.TagObjects);
        }
        Assert.Same(original, Activity.Current); Assert.Equal(0, evaluations);
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, false, false), telemetry.Read());
    }

    [Fact]
    public void UnsampledNames_ConsumeFiniteNameBudgetButUnobservedNamesDoNot()
    {
        using var telemetry = new DiagnosticTelemetry(new() { MaxOperationNames = 1 });
        Assert.Null(telemetry.StartOperation("Unobserved"));
        using var observer = new TelemetryTestObserver(telemetry, ActivitySamplingResult.None, metrics: false);
        Assert.Null(telemetry.StartOperation("SampledNone"));
        Assert.Equal(0, telemetry.Read().RejectedOperations);
        observer.SampleOverride = () => ActivitySamplingResult.AllDataAndRecorded;
        Assert.Null(telemetry.StartOperation("NewName"));
        using (var known = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("SampledNone"))) known.Complete();
        Assert.Equal(1, telemetry.Read().RejectedOperations);
        Assert.Equal("SampledNone", Assert.Single(observer.Stopped).OperationName);
        Assert.Equal(0, telemetry.Read().ActiveOperations);
    }

    [Fact]
    public void AllDataProperties_BoundInspectedEntriesClipSurrogatesAndPreserveReservedMetadata()
    {
        var id = Guid.NewGuid();
        using var telemetry = new DiagnosticTelemetry(new() { MaxProperties = 5, MaxPropertyChars = 4, MaxNameChars = 20 }, id);
        using var observer = new TelemetryTestObserver(telemetry, metrics: false);
        var evaluations = 0;
        var properties = new Dictionary<string, string?>
        {
            ["truemoon.app.id"] = "forged", ["outcome"] = "forged", ["error.type"] = "forged",
            [new string('x', 21)] = "invalid", ["proof"] = "abc😀tail", ["omitted"] = "later"
        };
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Properties", () => { evaluations++; return properties; })))
        {
            properties["proof"] = "changed"; properties.Clear();
            Assert.Equal(1, evaluations); Assert.Equal(1, telemetry.Read().ActiveOperations);
            var activity = Assert.Single(observer.Started);
            Assert.True(activity.IsAllDataRequested); Assert.True(activity.Recorded);
            Assert.Equal(id.ToString("D"), activity.GetTagItem("truemoon.app.id"));
            Assert.Equal("abc", activity.GetTagItem("proof")); Assert.Null(activity.GetTagItem("omitted"));
            Assert.Null(activity.GetTagItem("outcome")); Assert.Null(activity.GetTagItem("error.type"));
            Assert.Equal(new[] { "proof", "truemoon.app.id" }, activity.TagObjects.Select(pair => pair.Key).Order());
            operation.Complete();
        }
        Assert.Equal("Succeeded", Assert.Single(observer.Stopped).GetTagItem("outcome"));
        Assert.Equal(0, telemetry.Read().Failures);
    }

    [Fact]
    public void ZeroPropertyBudget_DoesNotEvaluateFactoryEvenWhenAllTraceDataIsRequested()
    {
        using var telemetry = new DiagnosticTelemetry(new() { MaxProperties = 0 });
        using var observer = new TelemetryTestObserver(telemetry, metrics: false);
        var calls = 0;
        using var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Zero", () => { calls++; throw new InvalidOperationException(); }));
        Assert.Equal(0, calls); Assert.Equal(0, telemetry.Read().Failures);
        Assert.Single(Assert.Single(observer.Started).TagObjects);
    }

    [Fact]
    public async Task NestedAsyncOperations_RestoreCurrentAndCorrelateDiagnosticRecords()
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        await using var pipeline = new DiagnosticPipeline(new() { Emergency = new() { Enabled = false } });
        var original = Activity.Current;
        Activity parent;
        Activity child;
        using (var outer = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Parent")))
        {
            parent = Assert.IsType<Activity>(Activity.Current);
            pipeline.Publish("Telemetry.Tests", new(DiagnosticLevel.Information, 1, "Parent", "proof"));
            await Task.Yield();
            Assert.Same(parent, Activity.Current);
            using (var inner = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Child")))
            {
                child = Assert.IsType<Activity>(Activity.Current);
                await Task.Yield();
                Assert.Same(child, Activity.Current); Assert.Equal(parent.TraceId, child.TraceId);
                Assert.Equal(parent.SpanId, child.ParentSpanId); Assert.NotEqual(parent.SpanId, child.SpanId);
                pipeline.Publish("Telemetry.Tests", new(DiagnosticLevel.Information, 2, "Child", "proof"));
                inner.Complete();
            }
            Assert.Same(parent, Activity.Current);
            pipeline.Publish("Telemetry.Tests", new(DiagnosticLevel.Information, 3, "ParentAgain", "proof"));
            outer.Complete();
        }
        Assert.Same(original, Activity.Current);
        pipeline.Publish("Telemetry.Tests", new(DiagnosticLevel.Information, 4, "After", "proof"));
        var records = pipeline.Read();
        Assert.Equal(new[] { 1, 2, 3, 4 }, records.Select(record => record.EventId));
        Assert.Equal(new[] { parent.SpanId.ToHexString(), child.SpanId.ToHexString(), parent.SpanId.ToHexString(), original?.SpanId.ToHexString() }, records.Select(record => record.SpanId));
        Assert.All(records.Take(3), record => Assert.Equal(parent.TraceId.ToHexString(), record.TraceId));
        Assert.Equal(original?.TraceId.ToHexString(), records[3].TraceId);
        Assert.Equal(new[] { "Child", "Parent" }, observer.Stopped.Select(activity => activity.OperationName));
        Assert.Equal(0, telemetry.Read().ActiveOperations);
    }

    [Theory]
    [InlineData(-1, "Incomplete", ActivityStatusCode.Unset)]
    [InlineData(0, "Succeeded", ActivityStatusCode.Ok)]
    [InlineData(1, "Failed", ActivityStatusCode.Error)]
    [InlineData(2, "Cancelled", ActivityStatusCode.Unset)]
    [InlineData(3, "Incomplete", ActivityStatusCode.Unset)]
    [InlineData(99, "Incomplete", ActivityStatusCode.Unset)]
    public void OperationOutcome_FirstCompleteWinsAndRepeatedDisposePublishesExactlyOnce(int selected, string expected, ActivityStatusCode status)
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        var elapsed = Stopwatch.StartNew();
        var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Terminal"));
        if (selected == 0) operation.Complete();
        else if (selected >= 0) operation.Complete((DiagnosticOperationOutcome)selected);
        if (selected >= 0) operation.Complete(DiagnosticOperationOutcome.Failed, new IOException("later ignored"));
        operation.Dispose(); operation.Dispose(); operation.Complete(DiagnosticOperationOutcome.Succeeded);
        elapsed.Stop();

        var activity = Assert.Single(observer.Stopped);
        Assert.Equal(expected, activity.GetTagItem("outcome")); Assert.Equal(status, activity.Status);
        Assert.Null(activity.GetTagItem("error.type"));
        Assert.Equal(0, telemetry.Read().ActiveOperations); Assert.Equal(0, telemetry.Read().Failures);
        Assert.Equal(2, observer.Measurements.Count);
        var count = Assert.Single(observer.Measurements, measurement => measurement.Instrument.Name == "truemoon.operation.count");
        var duration = Assert.Single(observer.Measurements, measurement => measurement.Instrument.Name == "truemoon.operation.duration");
        Assert.IsType<Counter<long>>(count.Instrument); Assert.Equal("{operation}", count.Instrument.Unit);
        Assert.Equal(1L, Assert.IsType<long>(count.Value));
        Assert.IsType<Histogram<double>>(duration.Instrument); Assert.Equal("s", duration.Instrument.Unit);
        var seconds = Assert.IsType<double>(duration.Value);
        Assert.True(double.IsFinite(seconds)); Assert.InRange(seconds, 0, elapsed.Elapsed.TotalSeconds);
        Assert.All(observer.Measurements, measurement =>
        {
            Assert.Equal(new[] { "operation", "outcome" }, measurement.Tags.Keys.Order());
            Assert.Equal("Terminal", measurement.Tags["operation"]); Assert.Equal(expected, measurement.Tags["outcome"]);
        });
    }

    [Fact]
    public void MetricsOnly_UsesNoActivityOrPropertiesAndNeverAddsHighCardinalityTags()
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry, activities: false);
        var original = Activity.Current; var calls = 0;
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("MetricsOnly", () => { calls++; return new Dictionary<string, string?> { ["SessionId"] = Guid.NewGuid().ToString() }; })))
        {
            Assert.Same(original, Activity.Current); operation.Complete(DiagnosticOperationOutcome.Cancelled);
        }
        Assert.Equal(0, calls); Assert.Empty(observer.Started); Assert.Empty(observer.Stopped);
        Assert.Equal(2, observer.Measurements.Count);
        Assert.All(observer.Measurements, measurement =>
        {
            Assert.Equal(new[] { "operation", "outcome" }, measurement.Tags.Keys.Order());
            Assert.Equal("MetricsOnly", measurement.Tags["operation"]); Assert.Equal("Cancelled", measurement.Tags["outcome"]);
        });
    }

    [Fact]
    public void Completion_RetainsOnlyExceptionTypeWhileScopeIsStillActive()
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Failure"));
        var reads = new MessageReadCounter();
        var error = CompleteCollectibleFailure(operation, reads);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(error.IsAlive); Assert.Equal(0, reads.Count);
        operation.Dispose();
        var activity = Assert.Single(observer.Stopped);
        Assert.Equal(typeof(HostileMessageException).FullName, activity.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.DoesNotContain(activity.TagObjects, pair => pair.Key.Contains("message", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("stack", StringComparison.OrdinalIgnoreCase));
        Assert.All(observer.Measurements, measurement => Assert.Equal(new[] { "operation", "outcome" }, measurement.Tags.Keys.Order()));
    }

    [Fact]
    public void NameCardinality_RejectsNewNamesAtLimitButContinuesKnownOperations()
    {
        using var telemetry = new DiagnosticTelemetry(new() { MaxOperationNames = 2 });
        using var observer = new TelemetryTestObserver(telemetry);
        foreach (var name in new[] { "First", "Second" })
        {
            using var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation(name)); operation.Complete();
        }
        var calls = 0;
        Assert.Null(telemetry.StartOperation("Third", () => { calls++; throw new InvalidOperationException(); }));
        using (var repeated = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("First"))) repeated.Complete();
        Assert.Equal(0, calls); Assert.Equal(1, telemetry.Read().RejectedOperations);
        Assert.Equal(new[] { "First", "Second", "First" }, observer.Stopped.Select(activity => activity.OperationName));
        Assert.Equal(3L, observer.Measurements.Where(measurement => measurement.Value is long).Sum(measurement => (long)measurement.Value));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("123456789")]
    public void InvalidOperationNames_AreRejectedBeforeLazyProperties(string? name)
    {
        using var telemetry = new DiagnosticTelemetry(new() { MaxNameChars = 8 });
        using var observer = new TelemetryTestObserver(telemetry);
        var calls = 0;
        Assert.Null(telemetry.StartOperation(name!, () => { calls++; throw new InvalidOperationException(); }));
        Assert.Equal(0, calls); Assert.Empty(observer.Started); Assert.Empty(observer.Measurements);
        Assert.Equal(new DiagnosticTelemetryStatus(0, 1, 0, false, false), telemetry.Read());
    }

    [Fact]
    public void Options_AreCopiedBeforeCallerMutatesFlagsAndEveryLimit()
    {
        var options = new DiagnosticTelemetryOptions { MaxOperationNames = 2, MaxNameChars = 8, MaxProperties = 2, MaxPropertyChars = 4 };
        using var telemetry = new DiagnosticTelemetry(options);
        options.TracingEnabled = false; options.MetricsEnabled = false;
        options.MaxOperationNames = 1; options.MaxNameChars = 1; options.MaxProperties = 0; options.MaxPropertyChars = 1;
        using var observer = new TelemetryTestObserver(telemetry);
        foreach (var name in new[] { "First", "Second" })
        {
            using var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation(name,
                () => new Dictionary<string, string?> { ["proof"] = "abcdef", ["other"] = "ghijkl", ["omitted"] = "none" }));
            operation.Complete();
        }
        Assert.Equal(2, observer.Stopped.Count); Assert.Equal(4, observer.Measurements.Count);
        Assert.All(observer.Stopped, activity =>
        {
            Assert.Equal("abcd", activity.GetTagItem("proof")); Assert.Equal("ghij", activity.GetTagItem("other")); Assert.Null(activity.GetTagItem("omitted"));
        });
        Assert.Equal(0, telemetry.Read().RejectedOperations);
    }

    [Theory]
    [InlineData("names", 0)] [InlineData("names", 257)]
    [InlineData("properties", -1)] [InlineData("properties", 65)]
    [InlineData("property-chars", 0)] [InlineData("property-chars", 4097)]
    [InlineData("name-chars", 0)] [InlineData("name-chars", 513)]
    public void InvalidBudgets_AreRejectedBeforeAppCreatesAnySink(string field, int value)
    {
        var options = new DiagnosticTelemetryOptions { TracingEnabled = false, MetricsEnabled = false };
        if (field == "names") options.MaxOperationNames = value;
        if (field == "properties") options.MaxProperties = value;
        if (field == "property-chars") options.MaxPropertyChars = value;
        if (field == "name-chars") options.MaxNameChars = value;
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiagnosticTelemetry(options));
        var calls = 0;
        var pipelineOptions = new DiagnosticPipelineOptions
        {
            Telemetry = options, SinkFactories = [new Factory<IDiagnosticSink>(() => { calls++; return new EmergencyTestSink(); })]
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => App.Build(context => context.UseDiagnostics(pipelineOptions)));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(1, 0, 1, 1)]
    [InlineData(256, 64, 4096, 512)]
    public void BudgetBounds_AcceptFirstAndLastValidValues(int names, int properties, int propertyChars, int nameChars)
    {
        using var telemetry = new DiagnosticTelemetry(new() { MaxOperationNames = names, MaxProperties = properties, MaxPropertyChars = propertyChars, MaxNameChars = nameChars });
        using var observer = new TelemetryTestObserver(telemetry);
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation(new string('n', nameChars)))) operation.Complete();
        Assert.Equal(nameChars, Assert.Single(observer.Stopped).OperationName.Length);
        Assert.Equal(2, observer.Measurements.Count); Assert.Equal(0, telemetry.Read().RejectedOperations);
    }

    [Theory]
    [InlineData("sample", true)] [InlineData("sample", false)]
    [InlineData("start", true)] [InlineData("start", false)]
    [InlineData("stop", true)] [InlineData("properties", true)] [InlineData("metrics", true)]
    public void ThrowingCallbacks_AreCountedAndRestoreParentWithoutBreakingCaller(string callback, bool metrics)
    {
        using var telemetry = new DiagnosticTelemetry(new() { MetricsEnabled = metrics });
        using var observer = new TelemetryTestObserver(telemetry, metrics: metrics);
        using var parent = new Activity("CallbackParent").Start();
        var calls = 0;
        if (callback == "sample") observer.SampleOverride = () => throw new IOException("sample");
        if (callback == "start") observer.OnStart = _ => throw new IOException("start");
        if (callback == "stop") observer.OnStop = _ => throw new IOException("stop");
        if (callback == "metrics") observer.OnMeasurement = _ => throw new IOException("measurement");
        var operation = telemetry.StartOperation("Callback", () => { calls++; if (callback == "properties") throw new IOException("properties"); return new Dictionary<string, string?>(); });
        if (callback is "sample" or "start")
        {
            Assert.Equal(metrics, operation is not null); Assert.Same(parent, Activity.Current); Assert.Equal(0, calls);
        }
        else Assert.NotNull(operation);
        operation?.Complete(); operation?.Dispose();
        Assert.Same(parent, Activity.Current); Assert.Equal(0, telemetry.Read().ActiveOperations);
        Assert.Equal(callback == "metrics" ? 2 : 1, telemetry.Read().Failures);
        Assert.Equal(metrics ? 2 : 0, observer.Measurements.Count);
        Assert.Equal(0, telemetry.Read().RejectedOperations);
    }

    [Theory]
    [InlineData("start")] [InlineData("stop")] [InlineData("properties")] [InlineData("metrics")]
    public void ReentrantCallbacks_RejectNestedWorkWithoutRecursingOrCorruptingOuterScope(string callback)
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        var attempts = 0;
        void Reenter() { attempts++; Assert.Null(telemetry.StartOperation("Reentrant")); }
        if (callback == "start") observer.OnStart = _ => Reenter();
        if (callback == "stop") observer.OnStop = _ => Reenter();
        if (callback == "metrics") observer.OnMeasurement = _ => Reenter();
        using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Outer", () =>
        {
            if (callback == "properties") Reenter(); return new Dictionary<string, string?>();
        }))) operation.Complete();
        Assert.Equal(callback == "metrics" ? 2 : 1, attempts);
        Assert.Equal(attempts, telemetry.Read().RejectedOperations); Assert.Equal(0, telemetry.Read().Failures);
        Assert.Equal("Outer", Assert.Single(observer.Stopped).OperationName);
        Assert.Equal(2, observer.Measurements.Count); Assert.Equal(0, telemetry.Read().ActiveOperations);
    }

    [Fact]
    public void Constructor_ContainsInstrumentPublishedFailuresAndLeavesTracingUsable()
    {
        var thread = Environment.CurrentManagedThreadId;
        var armed = false; var throws = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (armed && Environment.CurrentManagedThreadId == thread && instrument.Meter.Name == DiagnosticTelemetry.InstrumentationName)
                { throws++; throw new IOException("instrument-published"); }
            }
        };
        listener.Start();
        DiagnosticTelemetry telemetry;
        armed = true;
        try { telemetry = new DiagnosticTelemetry(); }
        finally { armed = false; }
        using (telemetry)
        using (var observer = new TelemetryTestObserver(telemetry, metrics: false))
        {
            using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("StillTraces"))) operation.Complete();
            Assert.Equal(2, throws); Assert.Equal(2, telemetry.Read().Failures);
            Assert.Equal("StillTraces", Assert.Single(observer.Stopped).OperationName);
            Assert.Equal(0, telemetry.Read().ActiveOperations);
        }
    }

    [Fact]
    public void Constructor_ContainsShouldListenFailureAndKeepsMetricsUsableWithoutActivitySource()
    {
        var thread = Environment.CurrentManagedThreadId;
        var armed = false; var throws = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
            {
                if (armed && Environment.CurrentManagedThreadId == thread && source.Name == DiagnosticTelemetry.InstrumentationName)
                { throws++; throw new IOException("should-listen"); }
                return false;
            }
        };
        ActivitySource.AddActivityListener(listener);
        DiagnosticTelemetry telemetry;
        armed = true;
        try { telemetry = new DiagnosticTelemetry(); }
        finally { armed = false; }
        using (telemetry)
        using (var observer = new TelemetryTestObserver(telemetry, activities: false))
        {
            Assert.Null(telemetry.ActivitySource);
            using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("StillMeasures"))) operation.Complete();
            Assert.Equal(1, throws); Assert.Equal(1, telemetry.Read().Failures);
            Assert.Empty(observer.Started); Assert.Equal(2, observer.Measurements.Count);
            Assert.Equal(0, telemetry.Read().ActiveOperations);
        }
    }

    [Fact]
    public void CloseWhileActive_FencesAdmissionAndDefersInstrumentationReleaseUntilLastScope()
    {
        using var telemetry = new DiagnosticTelemetry();
        using var observer = new TelemetryTestObserver(telemetry);
        var outer = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Outer"));
        var inner = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("Inner"));
        telemetry.Dispose(); telemetry.Dispose();
        Assert.Equal(new DiagnosticTelemetryStatus(2, 0, 0, true, false), telemetry.Read());
        Assert.Empty(observer.CompletedInstruments);
        Assert.Null(telemetry.StartOperation("AfterClose"));
        inner.Complete(); inner.Dispose();
        Assert.Equal(1, telemetry.Read().ActiveOperations); Assert.False(telemetry.Read().Released);
        Assert.Empty(observer.CompletedInstruments);
        outer.Complete(); outer.Dispose(); outer.Dispose();
        Assert.Equal(new DiagnosticTelemetryStatus(0, 1, 0, true, true), telemetry.Read());
        Assert.Equal(new[] { "truemoon.operation.count", "truemoon.operation.duration" }, observer.CompletedInstruments.Order());
        Assert.Equal(4, observer.Measurements.Count); Assert.Equal(2, observer.Stopped.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DefaultAppRegistration_ProvidesUsableTelemetryWithoutExplicitDiagnostics(bool microsoftDI)
    {
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Build();
        var telemetry = Assert.IsType<DiagnosticTelemetry>(Assert.Single(app.Services.ResolveAll<IDiagnosticTelemetry>()));
        using var observer = new TelemetryTestObserver(telemetry);
        try
        {
            Assert.Empty(app.Services.ResolveAll<DiagnosticPipeline>());
            Assert.Empty(app.Services.ResolveAll<DiagnosticSubscription>());
            Assert.Empty(app.Services.ResolveAll<DiagnosticTelemetry>());
            using (var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("DefaultApp"))) operation.Complete();
            var activity = Assert.Single(observer.Stopped);
            Assert.Equal("DefaultApp", activity.OperationName); Assert.Equal("Succeeded", activity.GetTagItem("outcome"));
            Assert.Equal(telemetry.ApplicationId.ToString("D"), activity.GetTagItem("truemoon.app.id"));
            Assert.Equal(2, observer.Measurements.Count);
            Assert.Equal(1L, Assert.IsType<long>(Assert.Single(observer.Measurements, value => value.Instrument.Name == "truemoon.operation.count").Value));
            Assert.All(observer.Measurements, value =>
            {
                Assert.Equal(new[] { "operation", "outcome" }, value.Tags.Keys.Order());
                Assert.Equal("DefaultApp", value.Tags["operation"]); Assert.Equal("Succeeded", value.Tags["outcome"]);
            });
            Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, false, false), telemetry.Read());
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, true, true), telemetry.Read());
        Assert.Equal(new[] { "truemoon.operation.count", "truemoon.operation.duration" }, observer.CompletedInstruments.Order());
        Assert.Null(telemetry.StartOperation("AfterClose"));
        await app.DisposeAsync();
        Assert.Equal(2, observer.CompletedInstruments.Count);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task AppRegistration_HasOneInterfaceOwnerAndDefersReleaseForActiveScope(bool microsoftDI, bool legacy)
    {
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            if (legacy) context.UseDiagnostics();
            else context.UseDiagnostics(new DiagnosticPipelineOptions { Emergency = new() { Enabled = false } });
        }).Build();
        var telemetry = Assert.IsType<DiagnosticTelemetry>(Assert.Single(app.Services.ResolveAll<IDiagnosticTelemetry>()));
        using var observer = new TelemetryTestObserver(telemetry);
        var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation("AppOwned"));
        try
        {
            Assert.Empty(app.Services.ResolveAll<DiagnosticTelemetry>());
            if (!legacy) Assert.Equal(app.Services.Resolve<DiagnosticPipeline>()!.ApplicationId, telemetry.ApplicationId);
            await app.DisposeAsync();
            Assert.True(telemetry.Read().Closed); Assert.False(telemetry.Read().Released);
            Assert.Equal(1, telemetry.Read().ActiveOperations); Assert.Empty(observer.CompletedInstruments);
            Assert.Null(telemetry.StartOperation("Closed"));
        }
        finally { operation.Complete(); operation.Dispose(); await app.DisposeAsync(); }
        Assert.True(telemetry.Read().Released); Assert.Equal(0, telemetry.Read().ActiveOperations);
        Assert.Equal(2, observer.CompletedInstruments.Count); Assert.Equal(2, observer.Measurements.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AppRepeatedSetup_ReplacesTelemetryAndCopiesNestedOptionsForBothResolvers(bool microsoftDI)
    {
        var discarded = new DiagnosticPipelineOptions { Telemetry = new() { TracingEnabled = false, MetricsEnabled = false } };
        var replacement = new DiagnosticPipelineOptions { Emergency = new() { Enabled = false }, Telemetry = new() { MaxOperationNames = 2, MaxPropertyChars = 4 } };
        var app = App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            context.UseDiagnostics(discarded).UseDiagnostics(replacement);
            replacement.Telemetry.TracingEnabled = false; replacement.Telemetry.MetricsEnabled = false;
            replacement.Telemetry.MaxOperationNames = 1; replacement.Telemetry.MaxPropertyChars = 1;
        }).Build();
        var telemetry = Assert.IsType<DiagnosticTelemetry>(Assert.Single(app.Services.ResolveAll<IDiagnosticTelemetry>()));
        using var observer = new TelemetryTestObserver(telemetry);
        try
        {
            Assert.Equal(app.Services.Resolve<DiagnosticPipeline>()!.ApplicationId, telemetry.ApplicationId);
            foreach (var name in new[] { "First", "Second" })
            {
                using var operation = Assert.IsAssignableFrom<IDiagnosticOperation>(telemetry.StartOperation(name, () => new Dictionary<string, string?> { ["proof"] = "abcdef" })); operation.Complete();
            }
            Assert.Equal(2, observer.Stopped.Count); Assert.Equal(4, observer.Measurements.Count);
            Assert.All(observer.Stopped, activity => Assert.Equal("abcd", activity.GetTagItem("proof")));
            Assert.Equal(0, telemetry.Read().RejectedOperations); Assert.Empty(app.Services.ResolveAll<DiagnosticTelemetry>());
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, true, true), telemetry.Read());
        Assert.Equal(2, observer.CompletedInstruments.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompleteCollectibleFailure(IDiagnosticOperation operation, MessageReadCounter reads)
    {
        var error = new HostileMessageException(reads); operation.Complete(DiagnosticOperationOutcome.Failed, error); return new WeakReference(error);
    }
    private sealed class MessageReadCounter { internal int Count; }
    private sealed class LegacyTelemetry : IDiagnosticTelemetry, IDiagnosticOperation
    {
        internal int Starts, Completions, Disposals;
        internal string? Name;
        internal Func<IReadOnlyDictionary<string, string?>>? Properties;
        public IDiagnosticOperation? StartOperation(string name, Func<IReadOnlyDictionary<string, string?>>? properties = null)
        { Starts++; Name = name; Properties = properties; return this; }
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null) => Completions++;
        public void Dispose() => Disposals++;
    }
    private sealed class HostileMessageException(MessageReadCounter reads) : Exception
    { public override string Message { get { reads.Count++; throw new IOException("private-message"); } } }
}

internal sealed record TelemetryTestMeasurement(Instrument Instrument, object Value, IReadOnlyDictionary<string, object?> Tags);

internal sealed class TelemetryTestObserver : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _metrics;
    internal readonly ConcurrentQueue<Activity> Started = new(), Stopped = new();
    internal readonly ConcurrentQueue<TelemetryTestMeasurement> Measurements = new();
    internal readonly ConcurrentQueue<string> CompletedInstruments = new();
    internal Func<ActivitySamplingResult>? SampleOverride;
    internal Action<ActivityContext>? OnSampleParent;
    internal Action<Activity>? OnStart, OnStop;
    internal Action<TelemetryTestMeasurement>? OnMeasurement;

    internal TelemetryTestObserver(DiagnosticTelemetry owner, ActivitySamplingResult sampling = ActivitySamplingResult.AllDataAndRecorded, bool activities = true, bool metrics = true)
    {
        _activities = new()
        {
            ShouldListenTo = source => activities && ReferenceEquals(source, owner.ActivitySource),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => { OnSampleParent?.Invoke(options.Parent); return SampleOverride?.Invoke() ?? sampling; },
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => SampleOverride?.Invoke() ?? sampling,
            ActivityStarted = activity => { Started.Enqueue(activity); OnStart?.Invoke(activity); },
            ActivityStopped = activity => { Stopped.Enqueue(activity); OnStop?.Invoke(activity); }
        };
        ActivitySource.AddActivityListener(_activities);
        _metrics = new()
        {
            InstrumentPublished = (instrument, listener) => { if (metrics && ReferenceEquals(instrument.Meter, owner.Meter)) listener.EnableMeasurementEvents(instrument); },
            MeasurementsCompleted = (instrument, _) => CompletedInstruments.Enqueue(instrument.Name)
        };
        _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Observe(instrument, value, tags));
        _metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Observe(instrument, value, tags));
        _metrics.Start();
    }
    private void Observe(Instrument instrument, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var measurement = new TelemetryTestMeasurement(instrument, value, tags.ToArray().ToDictionary(pair => pair.Key, pair => pair.Value));
        Measurements.Enqueue(measurement); OnMeasurement?.Invoke(measurement);
    }
    public void Dispose() { _activities.Dispose(); _metrics.Dispose(); }
}
