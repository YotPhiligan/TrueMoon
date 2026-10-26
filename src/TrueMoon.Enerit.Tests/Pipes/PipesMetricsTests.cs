using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Pipes;
using System.Text.Json;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.Enerit.Tests.Pipes;

[CollectionDefinition("Pipes aggregate metrics", DisableParallelization = true)]
public sealed class PipesMetricsCollection;

[Collection("Pipes aggregate metrics")]
public sealed class PipesMetricsTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public void ObservableCells_AreEmptyWithoutOwnersSumLateSubscriptionsAndKeepThreeCumulativeRejectionSeries()
    {
        using (new PipesMetrics()) { }
        using var early = new MetricsObserver(); var empty = early.Read(); AssertGauges(empty);
        using var first = new PipesMetrics(); using var second = new PipesMetrics();
        Parallel.Invoke(() => { first.Client(2, 3); first.Server(4, 17); first.Connections(2); },
            () => { second.Client(3, 1); second.Server(1, 5); second.Connections(1); });
        using var late = new MetricsObserver(); AssertGauges(late.Read(), 5, 4, 5, 22, 3);
        first.RejectClient(); second.RejectClient(); first.RejectServer(false); second.RejectServer(true);
        var rejected = early.Read(); AssertRejections(empty, rejected, client: 2, requests: 1, bytes: 1);
        first.Dispose(); AssertGauges(late.Read(), 3, 1, 1, 5, 1);
        second.Dispose(); var removed = early.Read(); AssertGauges(removed); Assert.Equal(rejected.Rejections, removed.Rejections);
        using var afterDisposal = new MetricsObserver(); Assert.Equal(rejected.Rejections, afterDisposal.Read().Rejections);
        Assert.Equal(6, late.Instruments.Count); Assert.All(late.Instruments, instrument => Assert.Equal("TrueMoon.Enerit.Pipes", instrument.Meter.Name));
    }

    [Theory]
    [InlineData("cancel", true)] [InlineData("timeout", true)] [InlineData("promote", true)] [InlineData("stop", true)] [InlineData("cancel", false)]
    public async Task ClientQueue_PullsCurrentCountsRejectsBeforeSerializationAndRemovesWaitersOnEveryExit(string exit, bool enabled)
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        var options = new PipesTransportOptions { MetricsEnabled = enabled, MaxPendingRequests = 1, MaxQueuedRequests = 1,
            RequestTimeout = exit == "timeout" ? TimeSpan.FromMilliseconds(600) : Deadline };
        await using var client = Client(endpoint, options); await peer.WaitForConnectionAsync().WaitAsync(Deadline);
        var first = client.InvokeAsync(10, null); var firstHeader = await HeaderAsync(peer); using var cancellation = new CancellationTokenSource(); var serialized = 0;
        var queued = client.InvokeAsync(11, _ => Interlocked.Increment(ref serialized), cancellation.Token);
        await UntilAsync(() => client.QueuedRequestCount == 1);
        AssertGauges(metrics.Read(), pending: enabled ? 1 : 0, queued: enabled ? 1 : 0);
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(12, _ => Interlocked.Increment(ref serialized)));
        Assert.Equal(0, serialized); var afterOverload = metrics.Read(); AssertRejections(baseline, afterOverload, client: enabled ? 1 : 0);
        if (exit == "stop")
        {
            await client.StopAsync().WaitAsync(Deadline); AssertShutdown(await Record.ExceptionAsync(() => first)); AssertShutdown(await Record.ExceptionAsync(() => queued));
            Assert.Equal(0, serialized); AssertGauges(metrics.Read()); Assert.Equal(afterOverload.Rejections, metrics.Read().Rejections);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InvokeAsync(13, null)); Assert.Equal(afterOverload.Rejections, metrics.Read().Rejections);
            return;
        }
        if (exit != "promote")
        {
            if (exit == "cancel") cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
            AssertGauges(metrics.Read(), pending: enabled ? 1 : 0); Assert.Equal(0, serialized);
        }
        await ReplyAsync(peer, firstHeader.Item1);
        if (exit == "timeout") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Deadline)); else await first.WaitAsync(Deadline);
        if (exit == "promote")
        {
            var promoted = await HeaderAsync(peer); Assert.Equal(11, promoted.Item2); Assert.Equal(1, serialized);
            AssertGauges(metrics.Read(), pending: 1); await ReplyAsync(peer, promoted.Item1); await queued.WaitAsync(Deadline);
        }
        await UntilAsync(() => client.PendingRequestCount == 0); AssertGauges(metrics.Read()); Assert.Equal(afterOverload.Rejections, metrics.Read().Rejections);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ClientLocalCancellation_KeepsPendingUntilLateReplyOrDisconnect(bool disconnect)
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 0 });
        await peer.WaitForConnectionAsync().WaitAsync(Deadline); using var cancellation = new CancellationTokenSource();
        var pending = client.InvokeAsync(10, null, cancellation.Token); var header = await HeaderAsync(peer); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Deadline)); AssertGauges(metrics.Read(), pending: 1);
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(11, null)); AssertRejections(baseline, metrics.Read(), client: 1);
        if (disconnect) peer.Dispose(); else await ReplyAsync(peer, header.Item1);
        await UntilAsync(() => client.PendingRequestCount == 0); AssertGauges(metrics.Read());
        await client.StopAsync().WaitAsync(Deadline); AssertGauges(metrics.Read()); AssertRejections(baseline, metrics.Read(), client: 1);
    }

    [Fact]
    public async Task ClientCancelledStopWait_RetainsBlockedWriterPendingUntilActualDrain()
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        using var release = new ManualResetEventSlim(); var entered = Signal();
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 1 });
        await peer.WaitForConnectionAsync().WaitAsync(Deadline);
        var first = client.InvokeAsync(10, _ => { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); }); await entered.Task.WaitAsync(Deadline);
        var second = client.InvokeAsync(11, _ => Assert.Fail("Queued payload must not serialize during stop.")); await UntilAsync(() => client.QueuedRequestCount == 1);
        try
        {
            using var wait = new CancellationTokenSource(); var stopping = client.StopAsync(wait.Token);
            await UntilAsync(() => client.QueuedRequestCount == 0); wait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping.WaitAsync(Deadline));
            Assert.False(client.Completion.IsCompleted); AssertGauges(metrics.Read(), pending: 1); Assert.Equal(baseline.Rejections, metrics.Read().Rejections);
            AssertShutdown(await Record.ExceptionAsync(() => first)); AssertShutdown(await Record.ExceptionAsync(() => second));
        }
        finally { release.Set(); }
        await client.StopAsync().WaitAsync(Deadline); Assert.True(client.Completion.IsCompletedSuccessfully); AssertGauges(metrics.Read());
    }

    [Theory]
    [InlineData(false, true)] [InlineData(true, true)] [InlineData(false, false)] [InlineData(true, false)]
    public async Task SharedServer_ReportsExactCountAndByteBoundsOnceAcrossConnectionsWithCountReasonPrecedence(bool bytesLimit, bool enabled)
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); var endpoint = Endpoint(); var handler = new MetricsHandler();
        await using var server = Server(endpoint, handler, new() { MetricsEnabled = enabled, MaxConcurrentRequests = bytesLimit ? 3 : 2, MaxBufferedRequestBytes = 7, MaxConnections = 3 });
        await using var firstClient = Client(endpoint, new() { MetricsEnabled = false }); await using var secondClient = Client(endpoint, new() { MetricsEnabled = false });
        var first = firstClient.InvokeAsync(10, writer => writer.Write(new byte[3]), bytes => bytes.Span[0]);
        var second = secondClient.InvokeAsync(11, writer => writer.Write(new byte[4]), bytes => bytes.Span[0]);
        try
        {
            await UntilAsync(() => handler.Calls == 2 && server.ConnectionCount == 3);
            AssertGauges(metrics.Read(), requests: enabled ? 2 : 0, bytes: enabled ? 7 : 0, connections: enabled ? 3 : 0);
            var error = await Assert.ThrowsAsync<InvocationException>(() => secondClient.InvokeAsync(12, writer => writer.Write(new byte[1])).WaitAsync(Deadline)); Assert.Contains("ENERIT_OVERLOADED", error.Message);
            Assert.Equal(2, handler.Calls); AssertRejections(baseline, metrics.Read(), requests: enabled && !bytesLimit ? 1 : 0, bytes: enabled && bytesLimit ? 1 : 0);
        }
        finally { handler.Release.TrySetResult(); }
        Assert.Equal(new byte[] { 10, 11 }, await Task.WhenAll(first, second).WaitAsync(Deadline));
        await UntilAsync(() => server.ActiveRequestCount == 0); AssertGauges(metrics.Read(), connections: enabled ? 3 : 0);
        Assert.Equal(13, await secondClient.InvokeAsync(13, writer => writer.Write(new byte[7]), bytes => bytes.Span[0]).WaitAsync(Deadline)); Assert.Equal(3, handler.Calls);
        await server.StopAsync().WaitAsync(Deadline); AssertGauges(metrics.Read());
    }

    [Fact]
    public async Task ClosedAdmission_DoesNotTurnShutdownIntoAnOverloadRejection()
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); using var cell = new PipesMetrics();
        var clients = new PipesClientAdmission(new() { MaxPendingRequests = 1, MaxQueuedRequests = 0 }, cell);
        await clients.EnterAsync(default); clients.Close(); await Assert.ThrowsAsync<ObjectDisposedException>(() => clients.EnterAsync(default)); clients.Release();
        var servers = new PipesServerAdmission(new() { MaxConcurrentRequests = 1, MaxBufferedRequestBytes = 1 }, cell);
        Assert.True(servers.TryEnter(1)); servers.Close(); Assert.False(servers.TryEnter(1)); servers.Release(1);
        AssertGauges(metrics.Read()); Assert.Equal(baseline.Rejections, metrics.Read().Rejections);
    }

    [Fact]
    public async Task GenericServer_IncludesWaitingAndRetiringConnectionsUntilCancelledStopActuallyDrains()
    {
        using var metrics = new MetricsObserver(); var endpoint = Endpoint(); var handler = new MetricsHandler();
        await using var server = Server(endpoint, handler, new() { MaxConnections = 2 });
        await UntilAsync(() => server.ConnectionCount == 1); AssertGauges(metrics.Read(), connections: 1);
        await using var client = Client(endpoint, new() { MetricsEnabled = false });
        var pending = client.InvokeAsync(10, writer => writer.Write(new byte[11]));
        try
        {
            await UntilAsync(() => handler.Calls == 1 && server.ConnectionCount == 2); AssertGauges(metrics.Read(), requests: 1, bytes: 11, connections: 2);
            await client.StopAsync().WaitAsync(Deadline); await handler.CancellationObserved.Task.WaitAsync(Deadline);
            AssertGauges(metrics.Read(), requests: 1, bytes: 11, connections: 2); // Draining old connection + waiting replacement listener.
            using var wait = new CancellationTokenSource(); var stop = server.StopAsync(wait.Token); wait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(Deadline)); Assert.False(server.Completion.IsCompleted);
            Assert.Equal(1, server.ActiveRequestCount); Assert.Equal(11, server.BufferedRequestBytes);
            Assert.InRange(metrics.Read().Connections, 1, 2); Assert.Equal(1, metrics.Read().Requests); Assert.Equal(11, metrics.Read().Bytes);
        }
        finally { handler.Release.TrySetResult(); }
        await server.StopAsync().WaitAsync(Deadline); Assert.True(server.Completion.IsCompletedSuccessfully); AssertGauges(metrics.Read()); AssertShutdown(await Record.ExceptionAsync(() => pending));
    }

    [Fact]
    public async Task DirectConnection_ContributesOneOwnerBeforeStartAndZeroAfterSuccessfulStop()
    {
        using var metrics = new MetricsObserver(); var endpoint = Endpoint(); var handler = new MetricsHandler();
        await using var connection = new PipesSignalServerConnection(endpoint, new MetricsEvents<PipesSignalServerConnection>(), handler, new PipesTransportOptions());
        AssertGauges(metrics.Read(), connections: 1); var start = connection.StartAsync();
        using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(Deadline); await start.WaitAsync(Deadline); await SendAsync(peer, 10, new byte[5]);
        try { await UntilAsync(() => handler.Calls == 1); AssertGauges(metrics.Read(), requests: 1, bytes: 5, connections: 1); }
        finally { handler.Release.TrySetResult(); }
        var response = await Task.Run(peer.GetResponseHeader).WaitAsync(Deadline); Assert.Equal(0, response.Item2); Assert.Equal(1, response.Item3); Assert.Equal(10, peer.ReadByte());
        await connection.StopAsync().WaitAsync(Deadline); Assert.True(connection.Completion.IsCompletedSuccessfully); AssertGauges(metrics.Read());
    }

    [Fact]
    public async Task ConcurrentClients_SumIndependentLiveOwnersAndRemoveOnlyTheDrainedContribution()
    {
        var firstEndpoint = Endpoint(); var secondEndpoint = Endpoint(); using var firstPeer = Peer(firstEndpoint); using var secondPeer = Peer(secondEndpoint);
        await using var first = Client(firstEndpoint, new()); await using var second = Client(secondEndpoint, new());
        await Task.WhenAll(firstPeer.WaitForConnectionAsync(), secondPeer.WaitForConnectionAsync()).WaitAsync(Deadline);
        var firstCall = first.InvokeAsync(10, null); var secondCall = second.InvokeAsync(11, null);
        var firstHeader = await HeaderAsync(firstPeer); var secondHeader = await HeaderAsync(secondPeer);
        using var late = new MetricsObserver(); AssertGauges(late.Read(), pending: 2);
        await ReplyAsync(firstPeer, firstHeader.Item1); await firstCall.WaitAsync(Deadline); await first.StopAsync().WaitAsync(Deadline);
        AssertGauges(late.Read(), pending: 1); Assert.Equal(1, second.PendingRequestCount);
        await ReplyAsync(secondPeer, secondHeader.Item1); await secondCall.WaitAsync(Deadline); await second.StopAsync().WaitAsync(Deadline); AssertGauges(late.Read());
    }

    [Theory]
    [InlineData("generic", false)] [InlineData("generic", true)]
    [InlineData("non-generic", false)] [InlineData("non-generic", true)]
    [InlineData("cobalt", false)] [InlineData("cobalt", true)]
    [InlineData("microsoft-di", false)] [InlineData("microsoft-di", true)]
    public async Task FactoriesAndApp_CopyAggregateOptionBeforeResolvingProductsWhileRpcStillWorks(string composition, bool enabled)
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read(); await using var pair = new MetricsPair(composition, enabled);
        var pending = pair.Client.InvokeAsync(10, writer => writer.Write(new byte[6]), bytes => bytes.Span[0]);
        try
        {
            await UntilAsync(() => pair.Handler.Calls == 1 && pair.Server.ConnectionCount == 2);
            AssertGauges(metrics.Read(), pending: enabled ? 1 : 0, requests: enabled ? 1 : 0, bytes: enabled ? 6 : 0, connections: enabled ? 2 : 0);
            using var late = new MetricsObserver(); AssertGauges(late.Read(), pending: enabled ? 1 : 0, requests: enabled ? 1 : 0, bytes: enabled ? 6 : 0, connections: enabled ? 2 : 0);
        }
        finally { pair.Handler.Release.TrySetResult(); }
        Assert.Equal(10, await pending.WaitAsync(Deadline)); Assert.Equal(1, pair.Handler.Calls);
        await pair.DisposeAsync(); AssertGauges(metrics.Read()); Assert.Equal(baseline.Rejections, metrics.Read().Rejections);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task AggregateOption_IsIndependentFromCoreOperationMetricOption(bool aggregate, bool operations)
    {
        using var metrics = new MetricsObserver(); using var telemetry = new DiagnosticTelemetry(new() { MetricsEnabled = operations, TracingEnabled = false });
        using var operationMetrics = new MeterListener { InstrumentPublished = (instrument, listener) => { if (ReferenceEquals(instrument.Meter, telemetry.Meter)) listener.EnableMeasurementEvents(instrument); } };
        var measurements = new ConcurrentQueue<string>(); operationMetrics.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Enqueue(instrument.Name));
        operationMetrics.SetMeasurementEventCallback<double>((instrument, _, _, _) => measurements.Enqueue(instrument.Name)); operationMetrics.Start();
        var handler = new MetricsHandler(); var endpoint = Endpoint(); var options = new PipesTransportOptions { MetricsEnabled = aggregate, MaxConnections = 2 };
        await using var server = new PipesInvocationServer<IMetricsContract>(new MetricsEvents<PipesInvocationServer<IMetricsContract>>(), handler, endpoint, options, telemetry);
        await using var client = new PipesInvocationClient<IMetricsContract>(new MetricsEvents<PipesInvocationClient<IMetricsContract>>(), endpoint, options, telemetry);
        var pending = client.InvokeAsync(10, writer => writer.Write(new byte[3]), bytes => bytes.Span[0]);
        try
        {
            await UntilAsync(() => handler.Calls == 1 && server.ConnectionCount == 2);
            AssertGauges(metrics.Read(), pending: aggregate ? 1 : 0, requests: aggregate ? 1 : 0, bytes: aggregate ? 3 : 0, connections: aggregate ? 2 : 0); Assert.Empty(measurements);
        }
        finally { handler.Release.TrySetResult(); }
        Assert.Equal(10, await pending.WaitAsync(Deadline)); await UntilAsync(() => server.ActiveRequestCount == 0 && telemetry.Read().ActiveOperations == 0);
        Assert.Equal(operations ? 4 : 0, measurements.Count);
        if (operations) Assert.Equal(new[] { "truemoon.operation.count", "truemoon.operation.count", "truemoon.operation.duration", "truemoon.operation.duration" }, measurements.Order());
        await client.StopAsync(); await server.StopAsync(); AssertGauges(metrics.Read()); Assert.False(telemetry.Read().Closed);
    }

    [Theory]
    [InlineData("slow")] [InlineData("throw")] [InlineData("reenter")]
    public async Task ObserverCallbacks_RunOnlyOnPullAndCannotBlockOrReplaceTransportBusiness(string callback)
    {
        using var metrics = new MetricsObserver(); var endpoint = Endpoint(); var handler = new MetricsHandler(); handler.Release.TrySetResult();
        await using var server = Server(endpoint, handler, new()); await using var client = Client(endpoint, new());
        var calls = 0; var original = new IOException("observer-owned-failure"); Task<byte>? reentrant = null;
        using var release = new ManualResetEventSlim(); var entered = Signal();
        metrics.OnMeasurement = () =>
        {
            var invocation = Interlocked.Increment(ref calls);
            if (callback == "throw") throw original;
            if (callback == "slow" && invocation == 1) { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); }
            if (callback == "reenter" && invocation == 1) reentrant = client.InvokeAsync(11, null, bytes => bytes.Span[0]);
        };
        Assert.Equal(10, await client.InvokeAsync(10, null, bytes => bytes.Span[0]).WaitAsync(Deadline)); Assert.Equal(0, calls);
        if (callback == "slow")
        {
            var observing = Task.Run(metrics.Read);
            try { await entered.Task.WaitAsync(Deadline); Assert.Equal(12, await client.InvokeAsync(12, null, bytes => bytes.Span[0]).WaitAsync(Deadline)); }
            finally { release.Set(); }
            await observing.WaitAsync(Deadline);
        }
        else if (callback == "throw")
        {
            var error = Assert.IsType<AggregateException>(Record.Exception(() => metrics.Read())); Assert.Contains(original, error.Flatten().InnerExceptions);
            metrics.OnMeasurement = null; Assert.Equal(12, await client.InvokeAsync(12, null, bytes => bytes.Span[0]).WaitAsync(Deadline));
        }
        else { metrics.Read(); Assert.NotNull(reentrant); Assert.Equal(11, await reentrant!.WaitAsync(Deadline)); }
        Assert.True(calls > 0); Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task OwnProcesses_FailedDirectStopRetainsConnectionMetricWithoutPollutingParentProcess()
    {
        using var metrics = new MetricsObserver(); var baseline = metrics.Read();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "enerit-fixture", "EneritPipesProbe.dll")); start.ArgumentList.Add("metrics-failed-stop");
        using var process = Process.Start(start) ?? throw new IOException("Metrics failure fixture did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); var stdout = await output; var stderr = await error;
            Assert.True(process.ExitCode == 0, $"Exit {process.ExitCode}: {stderr}\n{stdout}"); Assert.Empty(stderr);
            using var report = JsonDocument.Parse(Assert.Single(stdout.Split('\n'), line => line.StartsWith("RESULT ", StringComparison.Ordinal))[7..]); var result = report.RootElement;
            Assert.Equal("metrics-failed-stop", result.GetProperty("Scenario").GetString()); Assert.Equal(process.Id, result.GetProperty("ProcessId").GetInt32());
            Assert.Equal(0, result.GetProperty("BeforeConnections").GetInt64()); Assert.Equal(1, result.GetProperty("DuringRequests").GetInt64()); Assert.Equal(5, result.GetProperty("DuringBytes").GetInt64());
            Assert.Equal(1, result.GetProperty("WhileStoppingConnections").GetInt64()); Assert.Equal(1, result.GetProperty("AfterFailedConnections").GetInt64());
            Assert.Equal(1, result.GetProperty("AfterRetriedConnections").GetInt64()); Assert.Equal(0, result.GetProperty("AfterRequests").GetInt64()); Assert.Equal(0, result.GetProperty("AfterBytes").GetInt64());
            Assert.True(result.GetProperty("CompletionFaulted").GetBoolean()); Assert.True(result.GetProperty("SameFailureOnRetry").GetBoolean()); Assert.Equal(1, result.GetProperty("CancellationCallbacks").GetInt32());
            Assert.Equal("metric-stop-original", result.GetProperty("OriginalFailure").GetString()); AssertGauges(metrics.Read()); Assert.Equal(baseline.Rejections, metrics.Read().Rejections);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(Deadline); } }
    }

    public interface IMetricsContract { }

    private static string Endpoint() => "metrics-" + Guid.NewGuid().ToString("N");
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static NamedPipeServerStream Peer(string endpoint) => new("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    private static PipesInvocationClient<IMetricsContract> Client(string endpoint, PipesTransportOptions options) => new(new MetricsEvents<PipesInvocationClient<IMetricsContract>>(), endpoint, options);
    private static PipesInvocationServer<IMetricsContract> Server(string endpoint, MetricsHandler handler, PipesTransportOptions options) => new(new MetricsEvents<PipesInvocationServer<IMetricsContract>>(), handler, endpoint, options);
    private static Task<(Guid, byte, int)> HeaderAsync(PipeStream peer) => Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
    private static async Task ReplyAsync(PipeStream peer, Guid id)
    {
        using var writer = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(id, writer); SerializationUtils.Write((byte)0, writer); SerializationUtils.Write(0, writer);
        await peer.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    private static async Task SendAsync(PipeStream peer, byte method, byte[] payload)
    {
        using var writer = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(Guid.NewGuid(), writer); SerializationUtils.Write(method, writer); SerializationUtils.Write(payload.Length, writer); writer.Write(payload);
        await peer.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    private static async Task UntilAsync(Func<bool> predicate)
    { using var timeout = new CancellationTokenSource(Deadline); while (!predicate()) await Task.Delay(5, timeout.Token); }
    private static void AssertShutdown(Exception? error) => Assert.True(error is ObjectDisposedException or OperationCanceledException or IOException, $"Unexpected shutdown outcome: {error}");
    private static void AssertGauges(MetricsSnapshot actual, long pending = 0, long queued = 0, long requests = 0, long bytes = 0, long connections = 0)
    { Assert.Equal(pending, actual.Pending); Assert.Equal(queued, actual.Queued); Assert.Equal(requests, actual.Requests); Assert.Equal(bytes, actual.Bytes); Assert.Equal(connections, actual.Connections); }
    private static void AssertRejections(MetricsSnapshot before, MetricsSnapshot after, long client = 0, long requests = 0, long bytes = 0)
    { Assert.Equal(before.Rejections["client/queue_full"] + client, after.Rejections["client/queue_full"]); Assert.Equal(before.Rejections["server/requests"] + requests, after.Rejections["server/requests"]); Assert.Equal(before.Rejections["server/bytes"] + bytes, after.Rejections["server/bytes"]); }

    private sealed record MetricsSnapshot(long Pending, long Queued, long Requests, long Bytes, long Connections, SortedDictionary<string, long> Rejections);
    private sealed record MetricValue(Instrument Instrument, long Value, KeyValuePair<string, object?>[] Tags);
    private sealed class MetricsObserver : IDisposable
    {
        private const string Prefix = "truemoon.enerit.pipes.";
        private readonly MeterListener _listener;
        private readonly ConcurrentQueue<MetricValue> _values = new();
        internal readonly ConcurrentQueue<Instrument> Instruments = new();
        internal Action? OnMeasurement;
        internal MetricsObserver()
        {
            using (new PipesMetrics()) { } // Publish the process meter before subscribing, including the empty-owner case.
            _listener = new() { InstrumentPublished = (instrument, listener) => { if (instrument.Meter.Name == "TrueMoon.Enerit.Pipes") { Instruments.Enqueue(instrument); listener.EnableMeasurementEvents(instrument); } } };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => { _values.Enqueue(new(instrument, value, tags.ToArray())); OnMeasurement?.Invoke(); }); _listener.Start();
        }
        internal MetricsSnapshot Read()
        {
            _values.Clear(); _listener.RecordObservableInstruments(); var values = _values.ToArray();
            Assert.Equal(8, values.Length);
            Assert.All(values, value => Assert.Equal(value.Instrument.Name.EndsWith(".bytes", StringComparison.Ordinal) ? "By" : value.Instrument.Name.EndsWith(".connections", StringComparison.Ordinal) ? "{connection}" : "{request}", value.Instrument.Unit));
            var gauges = values.Where(value => value.Instrument.Name != Prefix + "rejected").ToDictionary(value => value.Instrument.Name, value => value.Value);
            Assert.Equal(new[] { Prefix + "client.pending", Prefix + "client.queued", Prefix + "server.connections", Prefix + "server.request.bytes", Prefix + "server.requests" }, gauges.Keys.Order());
            Assert.All(values.Where(value => value.Instrument.Name != Prefix + "rejected"), value => { Assert.IsType<ObservableUpDownCounter<long>>(value.Instrument); Assert.Empty(value.Tags); Assert.InRange(value.Value, 0, long.MaxValue); });
            var rejected = new SortedDictionary<string, long>();
            foreach (var value in values.Where(value => value.Instrument.Name == Prefix + "rejected"))
            {
                Assert.IsType<ObservableCounter<long>>(value.Instrument); Assert.Equal(new[] { "reason", "side" }, value.Tags.Select(tag => tag.Key).Order());
                var tags = value.Tags.ToDictionary(tag => tag.Key, tag => tag.Value); rejected.Add(tags["side"] + "/" + tags["reason"], value.Value); Assert.InRange(value.Value, 0, long.MaxValue);
            }
            Assert.Equal(new[] { "client/queue_full", "server/bytes", "server/requests" }, rejected.Keys);
            return new(gauges[Prefix + "client.pending"], gauges[Prefix + "client.queued"], gauges[Prefix + "server.requests"], gauges[Prefix + "server.request.bytes"], gauges[Prefix + "server.connections"], rejected);
        }
        public void Dispose() => _listener.Dispose();
    }
    private sealed class MetricsHandler : IInvocationServerHandler<IMetricsContract>
    {
        internal int Calls; internal readonly TaskCompletionSource Release = Signal(), CancellationObserved = Signal();
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        { Interlocked.Increment(ref Calls); using var registration = token.Register(() => CancellationObserved.TrySetResult()); await Release.Task; writer.Write(new[] { method }); return (true, null); }
    }
    private sealed class MetricsPair : IAsyncDisposable
    {
        internal readonly MetricsHandler Handler = new(); internal readonly PipesInvocationClient<IMetricsContract> Client; internal readonly PipesInvocationServer<IMetricsContract> Server;
        private readonly IApp? _app;
        internal MetricsPair(string composition, bool enabled)
        {
            var endpoint = Endpoint(); var options = new PipesTransportOptions { MetricsEnabled = enabled, MaxConnections = 2 }; var events = new MetricsEventsFactory();
            if (composition == "generic")
            {
                var clients = new PipesInvocationClientFactory<IMetricsContract>(events.Create<PipesInvocationClient<IMetricsContract>>(), options);
                var servers = new PipesInvocationServerFactory<IMetricsContract>(events.Create<PipesInvocationServer<IMetricsContract>>(), options);
                options.MetricsEnabled = !enabled; Server = servers.Create(new(endpoint, Handler)); Client = clients.Create(endpoint);
            }
            else if (composition == "non-generic")
            {
                var clients = new PipesInvocationClientFactory(events, _ => endpoint, options); var servers = new PipesInvocationServerFactory(events, new MetricsResolver(Handler), _ => endpoint, options);
                options.MetricsEnabled = !enabled; Server = Assert.IsType<PipesInvocationServer<IMetricsContract>>(servers.Create<IMetricsContract>()); Client = Assert.IsType<PipesInvocationClient<IMetricsContract>>(clients.Create<IMetricsContract>());
            }
            else
            {
                _app = App.Builder(context => { if (composition == "microsoft-di") context.UseDI(); }).Setup(context =>
                {
                    context.UseDiagnostics(); context.PipesServiceTransport(_ => endpoint, options);
                    context.Services(services => services.Singleton<IInvocationServerHandler<IMetricsContract>>(_ => Handler));
                }).Build();
                options.MetricsEnabled = !enabled;
                Server = Assert.IsType<PipesInvocationServer<IMetricsContract>>(_app.Services.Resolve<IInvocationServerFactory>()!.Create<IMetricsContract>());
                Client = Assert.IsType<PipesInvocationClient<IMetricsContract>>(_app.Services.Resolve<IInvocationClientFactory>()!.Create<IMetricsContract>());
            }
        }
        public async ValueTask DisposeAsync() { Handler.Release.TrySetResult(); await Client.DisposeAsync(); await Server.DisposeAsync(); if (_app != null) await _app.DisposeAsync(); }
    }
    private sealed class MetricsResolver(MetricsHandler handler) : IInvocationServerHandlerResolver
    { public IInvocationServerHandler<T> Resolve<T>() => (IInvocationServerHandler<T>)(object)handler; }
    private sealed class MetricsEventsFactory : IEventsSourceFactory
    { public IEventsSource Create(string name) => new MetricsEvents<object>(); public IEventsSource<T> Create<T>() => new MetricsEvents<T>(); }
    private sealed class MetricsEvents<T> : IEventsSource<T>
    {
        public string Name => typeof(T).FullName!;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? value, string? category = null, string? caller = null) { }
    }
}
