using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests.Pipes;

public sealed class PipesTraceLifecycleTests
{
    [Theory]
    [InlineData("sampling-none", true)] [InlineData("no-listener", true)] [InlineData("sampling-none", false)] [InlineData("sampled-client", true)]
    public async Task UnsampledMiddleHop_ForwardsAcceptedRemoteContextOnlyWithinThatInvocation(string mode, bool acceptRemote)
    {
        using var front = new TraceCapture(); using var middle = new TraceCapture(ActivitySamplingResult.None); using var back = new TraceCapture();
        if (mode == "no-listener") middle.Observer.Dispose();
        if (mode == "sampled-client") middle.Observer.SampleKind = kind => kind == ActivityKind.Client ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
        var middleEndpoint = TraceTestSupport.Endpoint(); var backEndpoint = TraceTestSupport.Endpoint(); var backHandler = new TraceHandler();
        await using var backServer = new PipesInvocationServer<PipesTraceTests.ITraceContract>(back.Events.Create<PipesInvocationServer<PipesTraceTests.ITraceContract>>(), backHandler, backEndpoint,
            new() { PropagateTraceContext = true, CaptureRequestEvents = true }, back.Telemetry);
        await using var downstream = Client(middle, backEndpoint); var forwarder = new ForwardingHandler(downstream);
        await using var middleServer = new PipesInvocationServer<PipesTraceTests.ITraceContract>(middle.Events.Create<PipesInvocationServer<PipesTraceTests.ITraceContract>>(), forwarder, middleEndpoint,
            new() { PropagateTraceContext = acceptRemote }, middle.Telemetry);
        await using var upstream = Client(front, middleEndpoint);
        Activity original;
        using (var parent = new Activity("FrontCaller").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            Assert.Equal(17, await upstream.InvokeAsync(17, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
            await front.Observer.WaitForStoppedAsync(1); await back.Observer.WaitForStoppedAsync(1);
            original = Assert.Single(front.Observer.Stopped); var remote = Assert.Single(back.Observer.Stopped);
            Assert.Equal(parent.TraceId, original.TraceId);
            if (acceptRemote)
            {
                Assert.Equal(original.TraceId, remote.TraceId); Assert.True(Assert.Single(back.Observer.SampleParents).IsRemote);
                if (mode == "sampled-client")
                {
                    var outgoing = Assert.Single(middle.Observer.Stopped); Assert.Equal(ActivityKind.Client, outgoing.Kind);
                    Assert.Equal(original.TraceId, outgoing.TraceId); Assert.Equal(original.SpanId, outgoing.ParentSpanId); Assert.Equal(outgoing.SpanId, remote.ParentSpanId);
                }
                else Assert.Equal(original.SpanId, remote.ParentSpanId);
            }
            else { Assert.NotEqual(original.TraceId, remote.TraceId); Assert.Equal(default, remote.ParentSpanId); }
            Assert.Same(parent, Activity.Current);
        }
        await TraceTestSupport.UntilAsync(() => middle.Telemetry.Read().ActiveOperations == 0);
        Assert.DoesNotContain(middle.Observer.Started, activity => activity.Kind == ActivityKind.Server);
        if (mode != "sampled-client") { Assert.Empty(middle.Observer.Started); Assert.Empty(middle.Observer.Stopped); }
        front.Observer.Dispose(); // The next request on the same physical connection has no ambient or sampled client context.
        Assert.Equal(18, await upstream.InvokeAsync(18, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        await back.Observer.WaitForStoppedAsync(2);
        var next = back.Observer.Stopped.Last(); Assert.NotEqual(original.TraceId, next.TraceId);
        if (mode == "sampled-client")
        {
            var nextClient = middle.Observer.Stopped.Last(); Assert.Equal(default, nextClient.ParentSpanId);
            Assert.Equal(nextClient.TraceId, next.TraceId); Assert.Equal(nextClient.SpanId, next.ParentSpanId); Assert.Equal("Remote", next.GetTagItem("TraceContext"));
        }
        else { Assert.Equal(default, next.ParentSpanId); Assert.Equal("Missing", next.GetTagItem("TraceContext")); }
        Assert.Equal(2, forwarder.Invocations); Assert.True(forwarder.CurrentWasAlwaysNull); Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task QueuedWriters_UseEachInvocationContextCapturedBeforeScheduling()
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint();
        using var peer = Peer(endpoint); using var release = new ManualResetEventSlim(); var entered = 0;
        await using var client = Client(capture, endpoint);
        await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        var tasks = new List<Task<int>>(); var parents = new Dictionary<byte, ActivityContext>();
        try
        {
            for (byte method = 10; method < 13; method++)
            {
                using var parent = new Activity("Producer-" + method).SetIdFormat(ActivityIdFormat.W3C).Start(); parents[method] = parent.Context;
                tasks.Add(client.InvokeAsync(method, _ => { Interlocked.Increment(ref entered); Assert.True(release.Wait(TraceTestSupport.Deadline)); }, bytes => (int)bytes.Span[0]));
            }
            await TraceTestSupport.UntilAsync(() => Volatile.Read(ref entered) == 2);
            Assert.Equal(3, client.PendingRequestCount); Assert.Equal(3, capture.Observer.Started.Count);
            release.Set();
            var seen = new HashSet<byte>();
            for (var index = 0; index < 3; index++)
            {
                var header = await TraceTestSupport.HeaderAsync(peer); Assert.True(seen.Add(header.Method));
                var span = Assert.Single(capture.Observer.Started, item => Equals(item.GetTagItem("MethodCode"), header.Method.ToString()));
                Assert.Equal(parents[header.Method].TraceId, span.TraceId); Assert.Equal(parents[header.Method].SpanId, span.ParentSpanId);
                Assert.Equal(span.TraceId, header.Context!.Value.TraceId); Assert.Equal(span.SpanId, header.Context.Value.SpanId);
                await TraceTestSupport.ReplyAsync(peer, header.Id, [header.Method]);
            }
            Assert.Equal(new[] { 10, 11, 12 }, await Task.WhenAll(tasks).WaitAsync(TraceTestSupport.Deadline));
            await capture.Observer.WaitForStoppedAsync(3); Assert.Equal(3, capture.Observer.Stopped.Select(span => span.TraceId).Distinct().Count());
            await TraceTestSupport.UntilAsync(() => client.PendingRequestCount == 0); Assert.Null(Activity.Current);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LocalCancellationAndTimeout_EndScopeButKeepCapacityAndOriginalLateResponseCorrelation(bool timeout)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); using var peer = Peer(endpoint);
        var options = new PipesTransportOptions { PropagateTraceContext = true, CaptureRequestEvents = true, MaxPendingRequests = 1, MaxQueuedRequests = 0,
            RequestTimeout = timeout ? TimeSpan.FromMilliseconds(600) : TimeSpan.FromSeconds(10) };
        await using var client = Client(capture, endpoint, options); await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        using var cancellation = new CancellationTokenSource();
        Task pending;
        using (new Activity("OriginalCaller").SetIdFormat(ActivityIdFormat.W3C).Start()) pending = client.InvokeAsync(10, null, cancellation.Token);
        var header = await TraceTestSupport.HeaderAsync(peer); if (!timeout) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(1);
        var stopped = Assert.Single(capture.Observer.Stopped); Assert.Equal("Cancelled", stopped.GetTagItem("outcome"));
        Assert.Equal(0, capture.Telemetry.Read().ActiveOperations); Assert.Equal(1, client.PendingRequestCount);
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(11, null));
        using (new Activity("UnrelatedLateReplyCaller").SetIdFormat(ActivityIdFormat.W3C).Start())
            await TraceTestSupport.ReplyAsync(peer, header.Id, []);
        await TraceTestSupport.UntilAsync(() => client.PendingRequestCount == 0 && capture.Pipeline.Read().Any(record => record.EventId == 2104 && record.Properties.GetValueOrDefault("RequestId") == header.Id.ToString("D")));
        var late = Assert.Single(capture.Pipeline.Read(), record => record.EventId == 2104 && record.Properties.GetValueOrDefault("RequestId") == header.Id.ToString("D"));
        Assert.Equal(header.Context!.Value.TraceId.ToHexString(), late.Properties["RequestTraceId"]);
        Assert.Equal(header.Context.Value.SpanId.ToHexString(), late.Properties["RequestSpanId"]);
        Assert.Null(late.TraceId); Assert.Null(late.SpanId);
        Assert.Contains(capture.Pipeline.Read(), record => record.EventName == "LateResponse" && record.MessageTemplate == $"Discarded late response {header.Id}");
        var next = client.InvokeAsync(12, null, bytes => bytes.Span[0]); var nextHeader = await TraceTestSupport.HeaderAsync(peer);
        Assert.NotEqual(header.Id, nextHeader.Id); await TraceTestSupport.ReplyAsync(peer, nextHeader.Id, [73]);
        Assert.Equal(73, await next.WaitAsync(TraceTestSupport.Deadline)); Assert.True(client.IsConnected);
    }

    [Theory]
    [InlineData("sampling-none")] [InlineData("disabled")] [InlineData("metrics-only")]
    public async Task AmbientContext_IsPropagatedWithoutInventingAnUnsampledClientActivity(string mode)
    {
        using var capture = new TraceCapture(mode == "sampling-none" ? ActivitySamplingResult.None : ActivitySamplingResult.AllDataAndRecorded,
            telemetryOptions: new() { TracingEnabled = mode == "sampling-none", MetricsEnabled = mode != "disabled" });
        var endpoint = TraceTestSupport.Endpoint(); using var peer = Peer(endpoint); await using var client = Client(capture, endpoint);
        await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        using var ambient = new Activity("AmbientWithoutClientSpan").SetIdFormat(ActivityIdFormat.W3C).Start();
        var pending = client.InvokeAsync(15, null); var header = await TraceTestSupport.HeaderAsync(peer);
        Assert.Equal(ambient.TraceId, header.Context!.Value.TraceId); Assert.Equal(ambient.SpanId, header.Context.Value.SpanId);
        await TraceTestSupport.ReplyAsync(peer, header.Id, []); await pending.WaitAsync(TraceTestSupport.Deadline);
        Assert.Same(ambient, Activity.Current); Assert.Empty(capture.Observer.Started); Assert.Empty(capture.Observer.Stopped);
        if (mode == "disabled") Assert.Empty(capture.Observer.Measurements);
        else TraceTestSupport.AssertMeasurements(capture.Observer, "Enerit.Pipes.Client", "Succeeded");
    }

    [Theory]
    [InlineData("serialize")] [InlineData("decode")] [InlineData("disconnect")]
    public async Task ClientFailure_PreservesOriginalFailureAndReleasesScopeAndCapacity(string phase)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(capture, endpoint); await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        var original = new IOException("original-" + phase);
        var pending = client.InvokeAsync<int>(16, phase == "serialize" ? _ => throw original : null, _ => throw original);
        if (phase != "serialize")
        {
            var header = await TraceTestSupport.HeaderAsync(peer);
            if (phase == "disconnect") peer.Dispose(); else await TraceTestSupport.ReplyAsync(peer, header.Id, []);
        }
        var error = await Assert.ThrowsAnyAsync<IOException>(() => pending.WaitAsync(TraceTestSupport.Deadline));
        if (phase != "disconnect") Assert.Same(original, error);
        await capture.Observer.WaitForStoppedAsync(1); Assert.Equal("Failed", Assert.Single(capture.Observer.Stopped).GetTagItem("outcome"));
        Assert.Equal(0, capture.Telemetry.Read().ActiveOperations); await TraceTestSupport.UntilAsync(() => client.PendingRequestCount == 0);
        var request = Assert.IsType<string>(Assert.Single(capture.Observer.Stopped).GetTagItem("RequestId"));
        Assert.Contains(capture.Pipeline.Read(), record => record.EventId == 2103 && record.ExceptionType == error.GetType().FullName && record.Properties.GetValueOrDefault("RequestId") == request);
    }

    [Fact]
    public async Task ConnectionTimeout_CancelsClientOperationBeforeAnyRequestIsSent()
    {
        using var capture = new TraceCapture(); await using var client = Client(capture, TraceTestSupport.Endpoint(),
            new() { PropagateTraceContext = true, CaptureRequestEvents = true, RequestTimeout = TimeSpan.FromMilliseconds(150) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.InvokeAsync(10, null).WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(1); Assert.Equal("Cancelled", Assert.Single(capture.Observer.Stopped).GetTagItem("outcome"));
        Assert.Equal(0, client.PendingRequestCount); Assert.Equal(0, capture.Telemetry.Read().ActiveOperations);
    }

    [Theory]
    [InlineData("trace")] [InlineData("span")] [InlineData("flags")]
    public async Task InvalidTraceMetadata_StillDispatchesAndPreservesFollowingLegacyFrame(string invalid)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        await using var connection = new PipesSignalServerConnection(endpoint, capture.Events.Create<PipesSignalServerConnection>(), handler,
            new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
        var start = connection.StartAsync(); using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(TraceTestSupport.Deadline); await start.WaitAsync(TraceTestSupport.Deadline);
        var frame = Frame(17, true); if (invalid == "trace") frame.AsSpan(23, 16).Clear(); else if (invalid == "span") frame.AsSpan(39, 8).Clear(); else frame[22] = 2;
        await peer.WriteAsync(frame); var response = await Task.Run(peer.GetRequestHeader).WaitAsync(TraceTestSupport.Deadline);
        Assert.Equal(0, response.Item2); Assert.Equal(1, response.Item3); Assert.Equal(17, peer.ReadByte());
        await peer.WriteAsync(Frame(18, false)); var next = await Task.Run(peer.GetRequestHeader).WaitAsync(TraceTestSupport.Deadline);
        Assert.Equal(0, next.Item2); Assert.Equal(18, peer.ReadByte());
        await capture.Observer.WaitForStoppedAsync(2); Assert.Equal(2, handler.Finished.Count);
        Assert.All(capture.Observer.Stopped, span => Assert.Equal(default, span.ParentSpanId));
        Assert.Equal(new[] { "Invalid", "Missing" }, capture.Observer.Stopped.Select(span => Assert.IsType<string>(span.GetTagItem("TraceContext"))));
        Assert.NotEqual(capture.Observer.Stopped.First().TraceId, capture.Observer.Stopped.Last().TraceId);
    }

    [Theory]
    [InlineData("version")] [InlineData("negative-length")] [InlineData("oversize")] [InlineData("truncated")]
    public async Task MalformedTraceFrame_FaultsConnectionBeforeHandlerDispatch(string malformed)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        await using var connection = new PipesSignalServerConnection(endpoint, capture.Events.Create<PipesSignalServerConnection>(), handler,
            new() { PropagateTraceContext = true }, capture.Telemetry);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); connection.Disconnected += (_, _) => disconnected.TrySetResult();
        var start = connection.StartAsync(); using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(TraceTestSupport.Deadline); await start.WaitAsync(TraceTestSupport.Deadline);
        var frame = Frame(17, true);
        if (malformed == "version") { frame[21] = 2; frame = frame[..22]; }
        if (malformed == "negative-length") BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(47), -1);
        if (malformed == "oversize") BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(47), PipesProtocol.MaxPayloadLength + 1);
        if (malformed == "truncated") frame = frame[..40];
        await peer.WriteAsync(frame); if (malformed == "truncated") peer.Dispose();
        // Unknown versions must disconnect after byte 22 even while the sender keeps the pipe open.
        await disconnected.Task.WaitAsync(TraceTestSupport.Deadline);
        Assert.Empty(handler.Started); Assert.Empty(capture.Observer.Started); Assert.Equal(0, connection.ActiveRequestCount); Assert.Equal(0, connection.BufferedRequestBytes);
    }

    [Fact]
    public async Task LegacyReader_RejectsOptInFrameWithoutDispatchOrReplay()
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(capture, endpoint); await peer.WaitForConnectionAsync().WaitAsync(TraceTestSupport.Deadline);
        var pending = client.InvokeAsync(10, null); await Assert.ThrowsAsync<InvalidDataException>(() => Task.Run(peer.GetRequestHeader).WaitAsync(TraceTestSupport.Deadline));
        peer.Dispose(); await Assert.ThrowsAnyAsync<IOException>(() => pending.WaitAsync(TraceTestSupport.Deadline));
        await capture.Observer.WaitForStoppedAsync(1); Assert.Equal("Failed", Assert.Single(capture.Observer.Stopped).GetTagItem("outcome"));
        Assert.Single(capture.Observer.Started); Assert.Equal(0, capture.Telemetry.Read().ActiveOperations);
    }

    [Theory]
    [InlineData("start")] [InlineData("complete")] [InlineData("dispose")] [InlineData("legacy-events")] [InlineData("listeners")]
    public async Task ThrowingDiagnostics_DoNotReplaceBusinessResultOrOwnBorrowedTelemetry(string failure)
    {
        using var capture = new TraceCapture(); var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        var broken = new BrokenTelemetry(failure); IDiagnosticTelemetry telemetry = failure == "listeners" ? capture.Telemetry : broken;
        if (failure == "listeners")
        {
            capture.Observer.OnStarted = _ => throw new IOException("listener-start"); capture.Observer.OnStopped = _ => throw new IOException("listener-stop");
            capture.Observer.OnMeasurement = () => throw new IOException("listener-measurement");
        }
        IEventsSource<PipesInvocationClient<PipesTraceTests.ITraceContract>> clientEvents = failure == "legacy-events" ? new BrokenEvents<PipesInvocationClient<PipesTraceTests.ITraceContract>>() : capture.Events.Create<PipesInvocationClient<PipesTraceTests.ITraceContract>>();
        IEventsSource<PipesInvocationServer<PipesTraceTests.ITraceContract>> serverEvents = failure == "legacy-events" ? new BrokenEvents<PipesInvocationServer<PipesTraceTests.ITraceContract>>() : capture.Events.Create<PipesInvocationServer<PipesTraceTests.ITraceContract>>();
        await using var server = new PipesInvocationServer<PipesTraceTests.ITraceContract>(serverEvents, handler, endpoint, new() { PropagateTraceContext = true, CaptureRequestEvents = true }, telemetry);
        await using var client = new PipesInvocationClient<PipesTraceTests.ITraceContract>(clientEvents, endpoint, new() { PropagateTraceContext = true, CaptureRequestEvents = true }, telemetry);
        Assert.Equal(19, await client.InvokeAsync(19, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
        var error = await Assert.ThrowsAsync<InvocationException>(() => client.InvokeAsync(3, null).WaitAsync(TraceTestSupport.Deadline)); Assert.Contains("remote-original", error.Message);
        await client.DisposeAsync(); await server.DisposeAsync(); Assert.False(broken.Disposed); Assert.False(capture.Telemetry.Read().Closed);
        if (failure == "listeners") Assert.True(capture.Telemetry.Read().Failures > 0);
        else Assert.True(broken.Starts >= 4);
    }

    [Fact]
    public async Task BlockedBoundedSink_DropsDiagnosticsWithoutBlockingTransportProducers()
    {
        var sink = new BlockedSink();
        using var capture = new TraceCapture(options: new() { QueueCapacity = 2, ReservedErrorCapacity = 0, SinkFactories = [new Factory<IDiagnosticSink>(() => sink)], Emergency = new() { Enabled = false } });
        var endpoint = TraceTestSupport.Endpoint(); var handler = new TraceHandler();
        await using var server = new PipesInvocationServer<PipesTraceTests.ITraceContract>(capture.Events.Create<PipesInvocationServer<PipesTraceTests.ITraceContract>>(), handler, endpoint, new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
        await using var client = Client(capture, endpoint);
        try
        {
            for (byte method = 10; method < 16; method++) Assert.Equal(method, await client.InvokeAsync(method, null, bytes => bytes.Span[0]).WaitAsync(TraceTestSupport.Deadline));
            await sink.Entered.Task.WaitAsync(TraceTestSupport.Deadline); await capture.Observer.WaitForStoppedAsync(12);
            var status = Assert.Single(capture.Pipeline.Health()); Assert.True(status.Dropped > 0); Assert.InRange(status.Queued, 1, 2);
            var requests = capture.Observer.Stopped.Select(activity => Assert.IsType<string>(activity.GetTagItem("RequestId"))).ToHashSet();
            Assert.Equal(24, capture.Pipeline.Read().Count(record => record.Properties.TryGetValue("RequestId", out var request) && requests.Contains(request!)));
            Assert.Equal(6, handler.Finished.Count); Assert.Equal(0, capture.Telemetry.Read().ActiveOperations);
        }
        finally { sink.Release.TrySetResult(); }
        await capture.Pipeline.FlushAsync().WaitAsync(TraceTestSupport.Deadline);
    }

    private static NamedPipeServerStream Peer(string endpoint) => new("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    private static PipesInvocationClient<PipesTraceTests.ITraceContract> Client(TraceCapture capture, string endpoint, PipesTransportOptions? options = null) =>
        new(capture.Events.Create<PipesInvocationClient<PipesTraceTests.ITraceContract>>(), endpoint, options ?? new() { PropagateTraceContext = true, CaptureRequestEvents = true }, capture.Telemetry);
    private static byte[] Frame(byte method, bool traced)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        PipesRequestHeader.Write(writer, Guid.NewGuid(), method, traced ? new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded) : null);
        return writer.WrittenMemory.ToArray();
    }
    private sealed class BrokenTelemetry(string failure) : IDiagnosticTelemetry, IDisposable
    {
        internal int Starts; internal bool Disposed;
        public IDiagnosticOperation? StartOperation(string name, Func<IReadOnlyDictionary<string, string?>>? properties = null)
        { Interlocked.Increment(ref Starts); if (failure == "start") throw new IOException("telemetry-start"); return new BrokenOperation(failure); }
        public void Dispose() => Disposed = true;
    }
    private sealed class ForwardingHandler(PipesInvocationClient<PipesTraceTests.ITraceContract> client) : IInvocationServerHandler<PipesTraceTests.ITraceContract>
    {
        internal int Invocations; internal bool CurrentWasAlwaysNull = true;
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            CurrentWasAlwaysNull &= Activity.Current is null; await Task.Yield(); CurrentWasAlwaysNull &= Activity.Current is null;
            var result = await client.InvokeAsync(method, null, bytes => bytes.Span[0], token); CurrentWasAlwaysNull &= Activity.Current is null;
            Interlocked.Increment(ref Invocations); writer.Write(new[] { result }); return (true, null);
        }
    }
    private sealed class BrokenOperation(string failure) : IDiagnosticOperation
    {
        public void Complete(DiagnosticOperationOutcome outcome = DiagnosticOperationOutcome.Succeeded, Exception? error = null) { if (failure == "complete") throw new IOException("telemetry-complete"); }
        public void Dispose() { if (failure == "dispose") throw new IOException("telemetry-dispose"); }
    }
    private sealed class BrokenEvents<T> : IEventsSource<T>
    {
        public string Name => typeof(T).FullName!;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => throw new IOException("events-start");
        public void StopActivity(Activity activity) => throw new IOException("events-stop");
        public void Write(Func<object>? value, string? category = null, string? caller = null) => throw new IOException("events-write");
    }
    private sealed class BlockedSink : IDiagnosticSink
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask WriteAsync(DiagnosticRecord record, CancellationToken token) { Entered.TrySetResult(); await Release.Task; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
