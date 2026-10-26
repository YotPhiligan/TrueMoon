using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Pipes;
using System.Text;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.Enerit.Tests.Pipes;

public sealed class PipesTraceTests
{
    [Fact]
    public async Task DefaultTransportOptions_KeepLegacyPeerCompatibleEvenWithSampledAmbientClientTracing()
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint();
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), endpoint, new PipesTransportOptions(), capture.Telemetry);
        await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        using var parent = new Activity("LegacyPeerCaller").SetIdFormat(ActivityIdFormat.W3C).Start();
        var pending = client.InvokeAsync(17, writer => writer.Write(new byte[] { 41 }), bytes => bytes.Span[0]);
        var header = await Task.Run(peer.GetRequestHeader).WaitAsync(TraceTestSupport.Deadline);
        Assert.Equal(17, header.Item2); Assert.Equal(1, header.Item3); Assert.Equal(41, peer.ReadByte());
        await TraceTestSupport.ReplyAsync(peer, header.Item1, [42]); Assert.Equal(42, await pending.WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(1); var span = Assert.Single(capture.Observer.Stopped);
        Assert.Equal(ActivityKind.Client, span.Kind); Assert.Equal(parent.TraceId, span.TraceId); Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Same(parent, Activity.Current); var request = Assert.IsType<string>(span.GetTagItem("RequestId"));
        Assert.DoesNotContain(capture.Pipeline.Read(), record => record.Properties.GetValueOrDefault("RequestId") == request);
        TraceTestSupport.AssertMeasurements(capture.Observer, "Enerit.Pipes.Client", "Succeeded");
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task AppFactories_PropagateAsyncClientServerTreeAndSnapshotOptionsOnBothResolvers(bool microsoftDI, bool pipeline)
    {
        await using var pair = new TraceAppPair(microsoftDI, pipeline);
        pair.Options.PropagateTraceContext = false; pair.Options.CaptureRequestEvents = false;
        using var parent = new Activity("Caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.TraceStateString = "vendor=private"; parent.AddBaggage("private-key", "private-baggage");
        var before = Activity.Current;
        Assert.Equal((byte)17, await pair.Client.InvokeAsync(17, writer => writer.Write("private-payload"u8), payload => payload.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        await pair.ServerObserver.WaitForStoppedAsync(1);
        Assert.Same(before, Activity.Current);
        var client = Assert.Single(pair.ClientObserver.Stopped);
        var server = Assert.Single(pair.ServerObserver.Stopped);
        Assert.Equal(ActivityKind.Client, client.Kind); Assert.Equal(parent.TraceId, client.TraceId); Assert.Equal(parent.SpanId, client.ParentSpanId);
        Assert.Equal(ActivityKind.Server, server.Kind); Assert.Equal(client.TraceId, server.TraceId); Assert.Equal(client.SpanId, server.ParentSpanId);
        Assert.True(Assert.Single(pair.ServerObserver.SampleParents).IsRemote);
        var handled = Assert.Single(pair.Handler.Finished);
        Assert.Same(server, handled.Activity); Assert.True(handled.CurrentSurvivedAwait);
        Assert.Null(server.TraceStateString); Assert.Empty(server.Baggage);
        var request = Assert.IsType<string>(client.GetTagItem("RequestId"));
        Assert.Equal(request, server.GetTagItem("RequestId"));
        Assert.Equal(typeof(ITraceContract).FullName, client.GetTagItem("Service")); Assert.Equal(typeof(ITraceContract).FullName, server.GetTagItem("Service"));
        Assert.True(Guid.TryParse(Assert.IsType<string>(client.GetTagItem("ClientId")), out _));
        Assert.True(Guid.TryParse(Assert.IsType<string>(server.GetTagItem("ConnectionId")), out _));
        if (pipeline)
        {
            var records = pair.ClientPipeline!.Read().Where(record => record.Properties.GetValueOrDefault("RequestId") == request).ToArray();
            Assert.Equal(new[] { 2100, 2101, 2200, 2201 }, records.Select(record => record.EventId).Order());
            Assert.All(records, record =>
            {
                Assert.Equal(client.TraceId.ToHexString(), record.TraceId);
                Assert.Equal(record.EventId < 2200 ? client.SpanId.ToHexString() : server.SpanId.ToHexString(), record.SpanId);
                Assert.Equal("17", record.Properties["MethodCode"]);
                Assert.DoesNotContain(pair.Endpoint, record.Properties.Values);
                Assert.DoesNotContain("private-payload", record.Properties.Values);
                Assert.DoesNotContain("private-baggage", record.Properties.Values);
            });
        }
        TraceTestSupport.AssertMeasurements(pair.ClientObserver, "Enerit.Pipes.Client", "Succeeded");
        TraceTestSupport.AssertMeasurements(pair.ServerObserver, "Enerit.Pipes.Server", "Succeeded");
        await pair.Client.StopAsync(); await pair.Server.StopAsync();
        Assert.False(pair.ClientTelemetry.Read().Closed); Assert.False(pair.ServerTelemetry.Read().Closed);
    }

    [Theory]
    [InlineData(false, true)] [InlineData(true, false)] [InlineData(false, false)]
    public async Task DisabledPropagation_UsesIndependentServerRootWhileBusinessCallStillSucceeds(bool clientPropagation, bool serverPropagation)
    {
        using var capture = new TraceCapture();
        var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        await using var server = new PipesInvocationServer<ITraceContract>(capture.Events.Create<PipesInvocationServer<ITraceContract>>(), handler, endpoint,
            new() { PropagateTraceContext = serverPropagation, CaptureRequestEvents = true }, capture.Telemetry);
        await using var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), endpoint,
            new() { PropagateTraceContext = clientPropagation, CaptureRequestEvents = true }, capture.Telemetry);
        using var parent = new Activity("Caller").SetIdFormat(ActivityIdFormat.W3C).Start();
        Assert.Equal(7, await client.InvokeAsync(7, null, payload => payload.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(2);
        var outgoing = Assert.Single(capture.Observer.Stopped, activity => activity.Kind == ActivityKind.Client);
        var incoming = Assert.Single(capture.Observer.Stopped, activity => activity.Kind == ActivityKind.Server);
        Assert.NotEqual(outgoing.TraceId, incoming.TraceId); Assert.Equal(default, incoming.ParentSpanId);
        Assert.False(Assert.Single(capture.Observer.SampleParents, context => context.TraceId != parent.TraceId).IsRemote);
        Assert.Equal(serverPropagation ? "Missing" : "Disabled", incoming.GetTagItem("TraceContext"));
        Assert.True(Assert.Single(handler.Finished).CurrentSurvivedAwait); Assert.Same(parent, Activity.Current);
    }

    [Fact]
    public async Task SamplingNoneWithoutAmbient_UsesLegacyHeaderAndMetricsWithoutCreatingActivities()
    {
        using var capture = new TraceCapture(ActivitySamplingResult.None);
        var name = TraceTestSupport.Endpoint();
        using var serverPeer = new NamedPipeServerStream("tm_" + name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await using var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), name,
            new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
        Assert.Null(Activity.Current);
        await serverPeer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        var pending = client.InvokeAsync(8, null, payload => payload.Length);
        var header = await TraceTestSupport.HeaderAsync(serverPeer);
        Assert.Null(header.Context); Assert.False(header.InvalidContext); Assert.Equal(8, header.Method);
        await TraceTestSupport.ReplyAsync(serverPeer, header.Id, []); Assert.Equal(0, await pending.WaitAsync(TraceTestSupport.Deadline));
        Assert.Empty(capture.Observer.Started); Assert.Empty(capture.Observer.Stopped);
        TraceTestSupport.AssertMeasurements(capture.Observer, "Enerit.Pipes.Client", "Succeeded");
        var records = capture.Pipeline.Read().Where(record => record.Properties.GetValueOrDefault("RequestId") == header.Id.ToString("D")).ToArray();
        Assert.Equal(new[] { 2100, 2101 }, records.Select(record => record.EventId)); Assert.All(records, record => { Assert.Null(record.TraceId); Assert.Null(record.SpanId); });
    }

    [Fact]
    public async Task Server_DoesNotInheritConstructionAmbientOrPreviousLegacyRequest()
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        PipesInvocationServer<ITraceContract> server;
        ActivityTraceId inherited;
        using (var construction = new Activity("ListenerConstruction").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            inherited = construction.TraceId;
            server = new(capture.Events.Create<PipesInvocationServer<ITraceContract>>(), handler, endpoint,
                new() { PropagateTraceContext = true }, capture.Telemetry);
        }
        await using (server)
        await using (var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), endpoint))
        {
            Assert.Null(Activity.Current);
            Assert.Equal(10, await client.InvokeAsync(10, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
            Assert.Equal(11, await client.InvokeAsync(11, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
            await capture.Observer.WaitForStoppedAsync(2);
            var spans = capture.Observer.Stopped.ToArray();
            Assert.All(spans, span => { Assert.Equal(ActivityKind.Server, span.Kind); Assert.Equal(default, span.ParentSpanId); Assert.NotEqual(inherited, span.TraceId); });
            Assert.NotEqual(spans[0].TraceId, spans[1].TraceId); Assert.All(handler.Finished, item => Assert.True(item.CurrentSurvivedAwait));
            Assert.Null(Activity.Current);
        }
    }

    [Theory]
    [InlineData(2, "Failed")] [InlineData(3, "Failed")] [InlineData(4, "Cancelled")]
    public async Task RemoteHandlerFailure_ProducesDistinctServerOutcomeAndClientFailureWithoutPoisoningConnection(byte method, string serverOutcome)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        await using var server = new PipesInvocationServer<ITraceContract>(capture.Events.Create<PipesInvocationServer<ITraceContract>>(), handler, endpoint,
            new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
        await using var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), endpoint,
            new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
        var error = await Assert.ThrowsAsync<InvocationException>(() => client.InvokeAsync(method, null).WaitAsync(TraceTestSupport.Deadline));
        Assert.Contains(method == 4 ? "remote-cancel" : "remote-original", error.Message);
        await capture.Observer.WaitForStoppedAsync(2);
        var clientSpan = Assert.Single(capture.Observer.Stopped, span => span.Kind == ActivityKind.Client);
        var serverSpan = Assert.Single(capture.Observer.Stopped, span => span.Kind == ActivityKind.Server);
        Assert.Equal("Failed", clientSpan.GetTagItem("outcome")); Assert.Equal(serverOutcome, serverSpan.GetTagItem("outcome"));
        Assert.Equal(method == 4 ? typeof(OperationCanceledException).FullName : typeof(IOException).FullName, serverSpan.GetTagItem("error.type"));
        var request = Assert.IsType<string>(clientSpan.GetTagItem("RequestId"));
        var events = capture.Pipeline.Read().Where(record => record.Properties.GetValueOrDefault("RequestId") == request).ToArray();
        Assert.Contains(events, record => record.EventId == 2103 && record.ExceptionType == typeof(InvocationException).FullName);
        Assert.Contains(events, record => record.EventId == (method == 4 ? 2202 : 2203));
        Assert.Equal(9, await client.InvokeAsync(9, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline)); Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task CaptureRequestEvents_RemainsOptInWhileTracesAndMetricsAreEnabled()
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        var options = new PipesTransportOptions { PropagateTraceContext = true };
        await using var server = new PipesInvocationServer<ITraceContract>(capture.Events.Create<PipesInvocationServer<ITraceContract>>(), handler, endpoint, options, capture.Telemetry);
        await using var client = new PipesInvocationClient<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), endpoint, options, capture.Telemetry);
        options.CaptureRequestEvents = true;
        Assert.Equal(5, await client.InvokeAsync(5, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(2);
        var request = Assert.IsType<string>(Assert.Single(capture.Observer.Stopped, activity => activity.Kind == ActivityKind.Client).GetTagItem("RequestId"));
        Assert.DoesNotContain(capture.Pipeline.Read(), record => record.Properties.GetValueOrDefault("RequestId") == request && record.EventName.StartsWith("Pipes.", StringComparison.Ordinal));
        Assert.Equal(4, capture.Observer.Measurements.Count); Assert.Equal(2, capture.Observer.Stopped.Count);
    }

    [Fact]
    public async Task GenericFactories_CopyPropagationAndCaptureFlagsAndBorrowTelemetryUntilProductsAreDisposed()
    {
        using var capture = new TraceCapture(); var handler = new TraceHandler(); var endpoint = TraceTestSupport.Endpoint();
        var options = new PipesTransportOptions { PropagateTraceContext = true, CaptureRequestEvents = true };
        var clients = new PipesInvocationClientFactory<ITraceContract>(capture.Events.Create<PipesInvocationClient<ITraceContract>>(), options, capture.Telemetry);
        var servers = new PipesInvocationServerFactory<ITraceContract>(capture.Events.Create<PipesInvocationServer<ITraceContract>>(), options, capture.Telemetry);
        options.PropagateTraceContext = options.CaptureRequestEvents = false;
        await using var server = servers.Create(new(endpoint, handler)); await using var client = clients.Create(endpoint);
        Assert.Equal(6, await client.InvokeAsync(6, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(2);
        var outgoing = Assert.Single(capture.Observer.Stopped, span => span.Kind == ActivityKind.Client);
        var incoming = Assert.Single(capture.Observer.Stopped, span => span.Kind == ActivityKind.Server);
        Assert.Equal(outgoing.TraceId, incoming.TraceId); Assert.Equal(outgoing.SpanId, incoming.ParentSpanId);
        var request = Assert.IsType<string>(outgoing.GetTagItem("RequestId"));
        Assert.Equal(new[] { 2100, 2101, 2200, 2201 }, capture.Pipeline.Read().Where(record => record.Properties.GetValueOrDefault("RequestId") == request).Select(record => record.EventId).Order());
        await client.DisposeAsync(); await server.DisposeAsync();
        Assert.Equal(new DiagnosticTelemetryStatus(0, 0, 0, false, false), capture.Telemetry.Read());
        using var stillOwned = Assert.IsAssignableFrom<IDiagnosticOperation>(capture.Telemetry.StartOperation("OwnerStillUsable"));
        stillOwned.Complete();
    }

    public interface ITraceContract { }
}

internal sealed record TraceObservation(byte Method, Activity? Activity, bool CurrentSurvivedAwait, byte[] Payload);

internal sealed class TraceHandler : IInvocationServerHandler<PipesTraceTests.ITraceContract>
{
    internal readonly ConcurrentQueue<TraceObservation> Started = new(), Finished = new();
    public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
    {
        var activity = Activity.Current; var payload = memory.ToArray(); Started.Enqueue(new(method, activity, true, payload));
        await Task.Yield();
        Finished.Enqueue(new(method, activity, ReferenceEquals(activity, Activity.Current), payload));
        if (method == 2) return (false, new IOException("remote-original"));
        if (method == 3) throw new IOException("remote-original");
        if (method == 4) throw new OperationCanceledException("remote-cancel");
        writer.Write(new[] { method }); return (true, null);
    }
}

internal sealed class TraceAppPair : IAsyncDisposable
{
    internal readonly string Endpoint = TraceTestSupport.Endpoint();
    internal readonly PipesTransportOptions Options = new() { PropagateTraceContext = true, CaptureRequestEvents = true };
    internal readonly TraceHandler Handler = new();
    internal readonly IApp ClientApp, ServerApp;
    internal readonly DiagnosticTelemetry ClientTelemetry, ServerTelemetry;
    internal readonly PipesTraceObserver ClientObserver, ServerObserver;
    internal readonly DiagnosticPipeline? ClientPipeline;
    internal readonly PipesInvocationClient<PipesTraceTests.ITraceContract> Client;
    internal readonly PipesInvocationServer<PipesTraceTests.ITraceContract> Server;
    internal TraceAppPair(bool microsoftDI, bool pipeline)
    {
        IApp Build(bool server) => App.Builder(context => { if (microsoftDI) context.UseDI(); }).Setup(context =>
        {
            if (pipeline) context.UseDiagnostics(new DiagnosticPipelineOptions { MemoryCapacity = 4096, Emergency = new() { Enabled = false } });
            else context.UseDiagnostics();
            context.PipesServiceTransport(_ => Endpoint, Options);
            if (server) context.Services(services => services.Singleton<IInvocationServerHandler<PipesTraceTests.ITraceContract>>(_ => Handler));
        }).Build();
        ServerApp = Build(true); ClientApp = Build(false);
        Options.PropagateTraceContext = false; Options.CaptureRequestEvents = false;
        ClientTelemetry = Assert.IsType<DiagnosticTelemetry>(ClientApp.Services.Resolve<IDiagnosticTelemetry>());
        ServerTelemetry = Assert.IsType<DiagnosticTelemetry>(ServerApp.Services.Resolve<IDiagnosticTelemetry>());
        ClientObserver = new(ClientTelemetry); ServerObserver = new(ServerTelemetry);
        ClientPipeline = ClientApp.Services.ResolveAll<DiagnosticPipeline>().SingleOrDefault();
        Server = Assert.IsType<PipesInvocationServer<PipesTraceTests.ITraceContract>>(ServerApp.Services.Resolve<IInvocationServerFactory>()!.Create<PipesTraceTests.ITraceContract>());
        Client = Assert.IsType<PipesInvocationClient<PipesTraceTests.ITraceContract>>(ClientApp.Services.Resolve<IInvocationClientFactory>()!.Create<PipesTraceTests.ITraceContract>());
    }
    public async ValueTask DisposeAsync()
    {
        try { await Client.DisposeAsync(); await Server.DisposeAsync(); await ClientApp.DisposeAsync(); await ServerApp.DisposeAsync(); }
        finally { ClientObserver.Dispose(); ServerObserver.Dispose(); }
    }
}

internal sealed class TraceCapture : IDisposable
{
    internal readonly DiagnosticPipeline Pipeline;
    internal readonly DiagnosticTelemetry Telemetry;
    internal readonly PipesTraceObserver Observer;
    internal readonly EventsSourceFactory Events = new();
    private readonly DiagnosticSubscription _subscription;
    internal TraceCapture(ActivitySamplingResult sampling = ActivitySamplingResult.AllDataAndRecorded, DiagnosticPipelineOptions? options = null, DiagnosticTelemetryOptions? telemetryOptions = null)
    {
        Telemetry = new(telemetryOptions);
        Observer = new(Telemetry, sampling);
        Pipeline = new(options ?? new() { MemoryCapacity = 4096, Emergency = new() { Enabled = false } });
        var configuration = new DiagnosticsConfiguration(); configuration.AddFilter("TrueMoon.Enerit.");
        _subscription = new(configuration, Pipeline);
    }
    public void Dispose() { Events.Dispose(); _subscription.Dispose(); Pipeline.Dispose(); Telemetry.Dispose(); Observer.Dispose(); }
}

internal sealed record TraceMeasurement(string Name, object Value, IReadOnlyDictionary<string, object?> Tags);

internal sealed class PipesTraceObserver : IDisposable
{
    private readonly ActivityListener _activities;
    private readonly MeterListener _metrics;
    internal readonly ConcurrentQueue<Activity> Started = new(), Stopped = new();
    internal readonly ConcurrentQueue<ActivityContext> SampleParents = new();
    internal readonly ConcurrentQueue<TraceMeasurement> Measurements = new();
    internal Action<Activity>? OnStarted, OnStopped;
    internal Action? OnMeasurement;
    internal Func<ActivityKind, ActivitySamplingResult>? SampleKind;
    internal PipesTraceObserver(DiagnosticTelemetry owner, ActivitySamplingResult sampling = ActivitySamplingResult.AllDataAndRecorded)
    {
        _activities = new()
        {
            ShouldListenTo = source => ReferenceEquals(source, owner.ActivitySource),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => { SampleParents.Enqueue(options.Parent); return SampleKind?.Invoke(options.Kind) ?? sampling; },
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => sampling,
            ActivityStarted = activity => { Started.Enqueue(activity); OnStarted?.Invoke(activity); },
            ActivityStopped = activity => { Stopped.Enqueue(activity); OnStopped?.Invoke(activity); }
        };
        ActivitySource.AddActivityListener(_activities);
        _metrics = new() { InstrumentPublished = (instrument, listener) => { if (ReferenceEquals(instrument.Meter, owner.Meter)) listener.EnableMeasurementEvents(instrument); } };
        _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Observe(instrument.Name, value, tags));
        _metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Observe(instrument.Name, value, tags));
        _metrics.Start();
    }
    private void Observe(string name, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    { Measurements.Enqueue(new(name, value, tags.ToArray().ToDictionary(pair => pair.Key, pair => pair.Value))); OnMeasurement?.Invoke(); }
    internal Task WaitForStoppedAsync(int count) => TraceTestSupport.UntilAsync(() => Stopped.Count >= count && Measurements.Count >= count * 2);
    public void Dispose() { _activities.Dispose(); _metrics.Dispose(); }
}

internal static class TraceTestSupport
{
    internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    internal static string Endpoint() => "trace-private-" + Guid.NewGuid().ToString("N");
    internal static async Task UntilAsync(Func<bool> condition)
    { using var deadline = new CancellationTokenSource(Deadline); while (!condition()) await Task.Delay(5, deadline.Token); }
    internal static Task<PipesRequestHeader> HeaderAsync(Stream stream) => Task.Run(() => PipesRequestHeader.Read(stream)).WaitAsync(Deadline);
    internal static async Task ReplyAsync(PipeStream stream, Guid id, byte[] payload, byte status = 0)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(id, writer); SerializationUtils.Write(status, writer); SerializationUtils.Write(payload.Length, writer); writer.Write(payload);
        await stream.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    internal static void AssertMeasurements(PipesTraceObserver observer, string operation, string outcome)
    {
        var values = observer.Measurements.Where(value => Equals(value.Tags.GetValueOrDefault("operation"), operation)).ToArray();
        Assert.Equal(2, values.Length);
        Assert.Equal(1L, Assert.IsType<long>(Assert.Single(values, value => value.Name == "truemoon.operation.count").Value));
        Assert.True(double.IsFinite(Assert.IsType<double>(Assert.Single(values, value => value.Name == "truemoon.operation.duration").Value)));
        Assert.All(values, value => { Assert.Equal(new[] { "operation", "outcome" }, value.Tags.Keys.Order()); Assert.Equal(outcome, value.Tags["outcome"]); });
    }
}
