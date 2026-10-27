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

[Collection("Pipes aggregate metrics")]
public sealed class PipesOutcomeMetricsTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public void FullCatalog_UsesThirteenObservableInstrumentsThirtySevenBoundedMeasurementsAndLifetimeTotals()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read();
        using var cell = new PipesMetrics();
        cell.ClientFailure(PipesClientFailure.Other); cell.ServerFailure(PipesServerFailure.Response);
        cell.ConnectionFailure(false, new InvalidDataException()); cell.ConnectionFailure(false, new IOException()); cell.ConnectionFailure(false, new InvalidOperationException());
        cell.ConnectionFailure(true, new InvalidDataException()); cell.ConnectionFailure(true, new ObjectDisposedException("peer")); cell.ConnectionFailure(true, new InvalidOperationException());
        cell.Reconnected(); cell.LateResponse();
        cell.QueueWait(TimeSpan.FromMilliseconds(1250), PipesQueueWaitOutcome.Admitted); cell.QueueWait(TimeSpan.FromMilliseconds(2500), PipesQueueWaitOutcome.Admitted);
        cell.QueueWait(TimeSpan.FromSeconds(-1), PipesQueueWaitOutcome.Admitted);
        var after = observer.Read(); AssertClientDelta(before, after, "other"); AssertServerDelta(before, after, "response");
        foreach (var side in new[] { "client", "server" }) foreach (var reason in new[] { "protocol", "transport", "other" }) Assert.Equal(before.Connection(side, reason) + 1, after.Connection(side, reason));
        Assert.Equal(before.Reconnects + 1, after.Reconnects); Assert.Equal(before.LateResponses + 1, after.LateResponses);
        Assert.Equal(before.WaitCount("admitted") + 3, after.WaitCount("admitted")); Assert.Equal(3.75, after.WaitSeconds("admitted") - before.WaitSeconds("admitted"), precision: 7);
        Assert.Equal(13, observer.Instruments.Count); cell.Dispose(); using var late = new OutcomeObserver(); var retained = late.Read();
        Assert.Equal(after.Longs, retained.Longs); Assert.Equal(after.Doubles, retained.Doubles);
    }

    [Fact]
    public void ClientClassifier_PinsCancellationPriorityPhaseOriginsAndUnclassifiedFallbackWithoutMessageParsing()
    {
        var cancellation = new OperationCanceledException("not a timeout classifier input");
        Assert.Equal(PipesClientFailure.Stopped, PipesMetrics.ClassifyClientFailure(cancellation, true, true, true, PipesClientFailure.Decode));
        Assert.Equal(PipesClientFailure.Cancelled, PipesMetrics.ClassifyClientFailure(cancellation, false, true, true));
        Assert.Equal(PipesClientFailure.Timeout, PipesMetrics.ClassifyClientFailure(cancellation, false, false, true));
        Assert.Equal(PipesClientFailure.Serialize, PipesMetrics.ClassifyClientFailure(new IOException("ENERIT_OVERLOADED timeout"), true, true, true, PipesClientFailure.Serialize));
        Assert.Equal(PipesClientFailure.Decode, PipesMetrics.ClassifyClientFailure(new InvocationException("remote"), true, true, true, PipesClientFailure.Decode));
        Assert.Equal(PipesClientFailure.Remote, PipesMetrics.ClassifyClientFailure(new ObjectDisposedException("peer"), true, true, true, PipesClientFailure.Remote));
        Assert.Equal(PipesClientFailure.Other, PipesMetrics.ClassifyClientFailure(new InvalidOperationException("cancelled timeout remote"), true, true, true));
        Assert.Equal(PipesClientFailure.Stopped, PipesMetrics.ClassifyClientFailure(new ObjectDisposedException("peer"), true, false, false));
        Assert.Equal(PipesClientFailure.Transport, PipesMetrics.ClassifyClientFailure(new ObjectDisposedException("peer"), false, false, false));
        Assert.Equal(PipesClientFailure.Overload, PipesMetrics.ClassifyClientFailure(new PipesInvocationOverloadedException(), false, false, false));
    }

    [Theory]
    [InlineData("promote")] [InlineData("cancel")] [InlineData("timeout")] [InlineData("stop")]
    public async Task QueueWait_RecordsOnlyTheAcceptedWaitExitAndNotTheRemainingInvocationLifetime(string exit)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 1, RequestTimeout = exit == "timeout" ? TimeSpan.FromMilliseconds(600) : Deadline });
        await peer.WaitForConnectionAsync().WaitAsync(Deadline); var first = client.InvokeAsync(10, null); var firstHeader = await HeaderAsync(peer);
        using var cancellation = new CancellationTokenSource(); var serialized = 0;
        var queued = client.InvokeAsync(11, _ => Interlocked.Increment(ref serialized), cancellation.Token); await UntilAsync(() => client.QueuedRequestCount == 1);
        AssertWaitUnchanged(before, observer.Read());
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(12, _ => Assert.Fail("Overflow must not serialize.")));
        AssertWaitUnchanged(before, observer.Read());
        var expected = exit == "promote" ? "admitted" : exit == "stop" ? "stopped" : "cancelled";
        if (exit == "promote")
        {
            await ReplyAsync(peer, firstHeader.Item1); await first.WaitAsync(Deadline); var next = await HeaderAsync(peer);
            Assert.Equal(11, next.Item2); Assert.Equal(1, serialized); Assert.Equal(1, client.PendingRequestCount);
            var admitted = observer.Read(); AssertWaitDelta(before, admitted, expected); Assert.False(queued.IsCompleted);
            var third = client.InvokeAsync(13, null); await UntilAsync(() => client.QueuedRequestCount == 1); AssertWaitEqual(admitted, observer.Read());
            await ReplyAsync(peer, next.Item1); await queued.WaitAsync(Deadline); var thirdHeader = await HeaderAsync(peer);
            var twice = observer.Read(); Assert.Equal(before.WaitCount("admitted") + 2, twice.WaitCount("admitted")); Assert.True(twice.WaitSeconds("admitted") > admitted.WaitSeconds("admitted"));
            Assert.False(third.IsCompleted); await ReplyAsync(peer, thirdHeader.Item1); await third.WaitAsync(Deadline); AssertWaitEqual(twice, observer.Read());
        }
        else if (exit == "stop")
        {
            await client.StopAsync().WaitAsync(Deadline); AssertShutdown(await Record.ExceptionAsync(() => first)); AssertShutdown(await Record.ExceptionAsync(() => queued));
            AssertWaitDelta(before, observer.Read(), expected); Assert.Equal(0, serialized);
        }
        else
        {
            if (exit == "cancel") cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
            if (exit == "timeout") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Deadline));
            var ended = observer.Read(); AssertWaitDelta(before, ended, expected); Assert.Equal(1, client.PendingRequestCount); Assert.Equal(0, serialized);
            Assert.Equal(before.Client(exit == "timeout" ? "timeout" : "cancelled") + (exit == "timeout" ? 2 : 1), ended.Client(exit == "timeout" ? "timeout" : "cancelled"));
            await ReplyAsync(peer, firstHeader.Item1);
            if (exit == "timeout") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Deadline)); else await first.WaitAsync(Deadline);
            AssertWaitEqual(ended, observer.Read());
        }
        await UntilAsync(() => client.PendingRequestCount == 0); var final = observer.Read();
        Assert.Equal(before.Longs["rejected/reason=queue_full,side=client"] + 1, final.Longs["rejected/reason=queue_full,side=client"]);
        foreach (var reason in OutcomeObserver.ClientReasons)
        {
            var extra = reason == "overload" ? 1 : exit == "cancel" && reason == "cancelled" ? 1 : exit == "timeout" && reason == "timeout" ? 2 : exit == "stop" && reason == "stopped" ? 2 : 0;
            Assert.Equal(before.Client(reason) + extra, final.Client(reason));
        }
        await client.StopAsync(); AssertWaitEqual(final, observer.Read());
    }

    [Fact]
    public async Task Admission_OnlyActualWaitersCreateSamplesAndAcquiredSlotWinsClosedGateRace()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); using var cell = new PipesMetrics();
        var gate = new PipesClientAdmission(new() { MaxPendingRequests = 1, MaxQueuedRequests = 0 }, cell);
        await gate.EnterAsync(default); await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => gate.EnterAsync(default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gate.EnterAsync(cancelled.Token));
        gate.Close(); await Assert.ThrowsAsync<ObjectDisposedException>(() => gate.EnterAsync(default)); gate.Release();
        AssertWaitUnchanged(before, observer.Read());
        var race = new PipesClientAdmission(new() { MaxPendingRequests = 1, MaxQueuedRequests = 1 }, cell);
        await race.EnterAsync(default); var waiter = race.EnterAsync(default); Assert.Equal(1, race.Queued);
        race.Close(); race.Release(); await waiter.WaitAsync(Deadline);
        AssertWaitDelta(before, observer.Read(), "admitted"); Assert.Equal(1, race.Pending); Assert.Equal(0, race.Queued);
        race.Release(); Assert.True(race.Drained.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData("timeout")] [InlineData("cancelled")] [InlineData("stopped")]
    public async Task ClientLocalFailure_EndsOnceWithItsTokenReasonAndRetainsSentCapacityUntilRemoteOrOwnedCleanup(string reason)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 0, RequestTimeout = reason == "timeout" ? TimeSpan.FromMilliseconds(200) : Deadline });
        await peer.WaitForConnectionAsync().WaitAsync(Deadline); using var cancellation = new CancellationTokenSource();
        var pending = client.InvokeAsync(10, null, cancellation.Token); var header = await HeaderAsync(peer);
        if (reason == "cancelled") cancellation.Cancel(); if (reason == "stopped") await client.StopAsync().WaitAsync(Deadline);
        var error = await Record.ExceptionAsync(() => pending.WaitAsync(Deadline));
        if (reason == "stopped") Assert.IsType<ObjectDisposedException>(error); else Assert.IsAssignableFrom<OperationCanceledException>(error);
        var failed = observer.Read(); AssertClientDelta(before, failed, reason); AssertConnectionUnchanged(before, failed);
        if (reason != "stopped")
        {
            Assert.Equal(1, client.PendingRequestCount); await ReplyAsync(peer, header.Item1); await UntilAsync(() => client.PendingRequestCount == 0);
            var late = observer.Read(); Assert.Equal(before.LateResponses + 1, late.LateResponses); AssertClientDelta(before, late, reason);
        }
        await client.StopAsync(); AssertClientDelta(before, observer.Read(), reason); AssertConnectionUnchanged(before, observer.Read());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InvokeAsync(11, null)); AssertClientDelta(before, observer.Read(), reason);
    }

    [Theory]
    [InlineData(false, "io")] [InlineData(false, "remote")] [InlineData(false, "overload")] [InlineData(false, "cancel")] [InlineData(false, "other")]
    [InlineData(true, "io")] [InlineData(true, "remote")] [InlineData(true, "overload")] [InlineData(true, "cancel")] [InlineData(true, "other")]
    public async Task CallbackFailures_KeepExactExceptionAndPhaseEvenWhenTheExceptionResemblesAnotherFailure(bool decode, string kind)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new()); await peer.WaitForConnectionAsync().WaitAsync(Deadline); var original = Error(kind);
        var pending = client.InvokeAsync<int>(10, decode ? null : _ => throw original, _ => throw original);
        if (decode) { var header = await HeaderAsync(peer); await ReplyAsync(peer, header.Item1); }
        Assert.Same(original, await Record.ExceptionAsync(() => pending.WaitAsync(Deadline))); await UntilAsync(() => client.PendingRequestCount == 0);
        var after = observer.Read(); AssertClientDelta(before, after, decode ? "decode" : "serialize"); AssertConnectionUnchanged(before, after); AssertWaitUnchanged(before, after);
        var recovered = client.InvokeAsync(11, null); var recoveryHeader = await HeaderAsync(peer); await ReplyAsync(peer, recoveryHeader.Item1); await recovered.WaitAsync(Deadline);
        AssertClientDelta(before, observer.Read(), decode ? "decode" : "serialize"); Assert.True(client.IsConnected);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WinningRemoteOrTransportFailure_IsNotReplacedOrCountedAgainByALateSerializerFailure(bool disconnect)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        var events = new OutcomeEvents<PipesInvocationClient<IOutcomeContract>>(); await using var client = Client(endpoint, new() { CaptureRequestEvents = true }, events);
        await peer.WaitForConnectionAsync().WaitAsync(Deadline); using var release = new ManualResetEventSlim(); var entered = Signal(); var loser = new IOException("late serializer loser");
        var pending = client.InvokeAsync(10, _ => { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); throw loser; });
        try
        {
            await entered.Task.WaitAsync(Deadline); var id = await events.Starting.Task.WaitAsync(Deadline);
            if (disconnect) peer.Dispose(); else await ReplyAsync(peer, id, Encoding.UTF8.GetBytes("remote winner"), 1);
            var winning = await Record.ExceptionAsync(() => pending.WaitAsync(Deadline));
            if (disconnect) Assert.IsAssignableFrom<IOException>(winning); else Assert.Contains("remote winner", Assert.IsType<InvocationException>(winning).Message);
            Assert.NotSame(loser, winning); Assert.Equal(1, client.PendingRequestCount); AssertClientDelta(before, observer.Read(), disconnect ? "transport" : "remote");
        }
        finally { release.Set(); }
        await UntilAsync(() => client.PendingRequestCount == 0); AssertClientDelta(before, observer.Read(), disconnect ? "transport" : "remote");
    }

    [Fact]
    public async Task ValidatedUnknownDuplicateAndPostCancelResponses_CountOncePerFrameWithoutChangingRequestFailures()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint); var events = new OutcomeEvents<PipesInvocationClient<IOutcomeContract>>();
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 0 }, events); await peer.WaitForConnectionAsync().WaitAsync(Deadline);
        var successful = client.InvokeAsync(10, null); var first = await HeaderAsync(peer); await ReplyAsync(peer, first.Item1); await successful.WaitAsync(Deadline);
        await ReplyAsync(peer, first.Item1, [91]); await ReplyAsync(peer, first.Item1); await ReplyAsync(peer, Guid.NewGuid(), Encoding.UTF8.GetBytes("unknown valid error"), 1);
        await UntilAsync(() => Volatile.Read(ref events.Late) == 3); Assert.Equal(before.LateResponses + 3, observer.Read().LateResponses);
        using var cancellation = new CancellationTokenSource(); var cancelled = client.InvokeAsync(11, null, cancellation.Token); var sent = await HeaderAsync(peer); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(Deadline)); Assert.Equal(1, client.PendingRequestCount);
        await ReplyAsync(peer, sent.Item1); await UntilAsync(() => Volatile.Read(ref events.Late) == 4 && client.PendingRequestCount == 0);
        var after = observer.Read(); Assert.Equal(before.LateResponses + 4, after.LateResponses); AssertClientDelta(before, after, "cancelled"); AssertConnectionUnchanged(before, after);
        var next = client.InvokeAsync(12, null); var nextHeader = await HeaderAsync(peer); await ReplyAsync(peer, nextHeader.Item1); await next.WaitAsync(Deadline);
        Assert.Equal(4, events.Late); Assert.Equal(before.LateResponses + 4, observer.Read().LateResponses);
    }

    [Theory]
    [InlineData("status")] [InlineData("length")] [InlineData("error-utf8")] [InlineData("eof")]
    public async Task MalformedResponseOrEof_SeparatesPhysicalConnectionReasonFromThePendingRequestTransportFailure(string fault)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new()); await peer.WaitForConnectionAsync().WaitAsync(Deadline); var pending = client.InvokeAsync(10, null); await HeaderAsync(peer);
        if (fault == "eof") peer.Dispose();
        else if (fault == "error-utf8") await ReplyAsync(peer, Guid.NewGuid(), [255], 1);
        else
        {
            using var writer = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(Guid.NewGuid(), writer); SerializationUtils.Write((byte)(fault == "status" ? 2 : 0), writer); SerializationUtils.Write(fault == "length" ? -1 : 0, writer);
            await peer.WriteAsync(writer.WrittenMemory);
        }
        var error = await Record.ExceptionAsync(() => pending.WaitAsync(Deadline));
        if (fault == "eof") Assert.IsAssignableFrom<IOException>(error); else Assert.IsType<InvalidDataException>(error);
        await UntilAsync(() => client.PendingRequestCount == 0); var after = observer.Read(); AssertClientDelta(before, after, "transport");
        AssertConnectionDelta(before, after, "client", fault == "eof" ? "transport" : "protocol"); Assert.Equal(before.LateResponses, after.LateResponses);
        await client.StopAsync(); AssertConnectionDelta(before, observer.Read(), "client", fault == "eof" ? "transport" : "protocol");
    }

    [Fact]
    public async Task ReconnectCounter_ExcludesInitialConnectionWaitingAttemptsAndLocalStopButCountsLaterSuccess()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var events = new OutcomeEvents<PipesInvocationClient<IOutcomeContract>>();
        await using var client = Client(endpoint, new() { RequestTimeout = TimeSpan.FromMilliseconds(150) }, events);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.InvokeAsync(10, null).WaitAsync(Deadline));
        Assert.Equal(before.Reconnects, observer.Read().Reconnects); AssertConnectionUnchanged(before, observer.Read());
        using var firstPeer = Peer(endpoint); await firstPeer.WaitForConnectionAsync().WaitAsync(Deadline); await UntilAsync(() => Volatile.Read(ref events.Connected) == 1);
        Assert.Equal(before.Reconnects, observer.Read().Reconnects); firstPeer.Dispose();
        await UntilAsync(() => observer.Read().Connection("client", "transport") == before.Connection("client", "transport") + 1);
        using var replacement = Peer(endpoint); await replacement.WaitForConnectionAsync().WaitAsync(Deadline); await UntilAsync(() => Volatile.Read(ref events.Connected) == 2);
        Assert.Equal(before.Reconnects + 1, observer.Read().Reconnects);
        var recovered = client.InvokeAsync(11, null); var header = await HeaderAsync(replacement); await ReplyAsync(replacement, header.Item1); await recovered.WaitAsync(Deadline);
        await client.StopAsync(); var final = observer.Read(); Assert.Equal(before.Reconnects + 1, final.Reconnects); AssertClientDelta(before, final, "timeout"); AssertConnectionDelta(before, final, "client", "transport");
    }

    [Theory]
    [InlineData("return", "handler")] [InlineData("throw", "handler")] [InlineData("cancel", "cancelled")]
    [InlineData("return-cancel", "cancelled")] [InlineData("return-null", "handler")]
    [InlineData("encode", "encode")] [InlineData("handler-overload", "handler")] [InlineData("success-error", "none")]
    public async Task ServerTerminalOutcome_PreservesHandlerEncodeAndRemoteReasonsWithoutParsingExceptionText(string mode, string reason)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var handler = new OutcomeHandler(mode);
        await using var server = Server(endpoint, handler, new()); await using var client = Client(endpoint, new());
        var pending = client.InvokeAsync(10, null, bytes => bytes.Span[0]);
        if (reason == "none") Assert.Equal(10, await pending.WaitAsync(Deadline));
        else { var error = await Assert.ThrowsAsync<InvocationException>(() => pending.WaitAsync(Deadline)); Assert.Contains(mode == "encode" ? typeof(InvalidDataException).FullName! : mode == "handler-overload" ? "ENERIT_OVERLOADED" : mode == "return-null" ? "Remote invocation failed." : handler.Original.Message, error.Message); }
        await UntilAsync(() => reason == "none" ? server.ActiveRequestCount == 0 : observer.Read().Server(reason) == before.Server(reason) + 1);
        var after = observer.Read();
        if (reason == "none") { AssertServerUnchanged(before, after); AssertClientUnchanged(before, after); }
        else { AssertServerDelta(before, after, reason); AssertClientDelta(before, after, "remote"); }
        AssertConnectionUnchanged(before, after);
    }

    [Fact]
    public async Task SharedAdmissionRefusal_AndHandlerFailureHaveDifferentReasonsAndAreCountedOnlyOnceAcrossConnections()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var handler = new OutcomeHandler("return", hold: true);
        await using var server = Server(endpoint, handler, new() { MaxConcurrentRequests = 1, MaxBufferedRequestBytes = 1, MaxConnections = 3 });
        await using var firstClient = Client(endpoint, new()); await using var secondClient = Client(endpoint, new());
        var first = firstClient.InvokeAsync(10, writer => writer.Write(new byte[1]));
        try
        {
            await handler.Entered.Task.WaitAsync(Deadline); var refused = await Assert.ThrowsAsync<InvocationException>(() => secondClient.InvokeAsync(11, writer => writer.Write(new byte[1])).WaitAsync(Deadline)); Assert.Contains("ENERIT_OVERLOADED", refused.Message);
            Assert.Equal(1, handler.Calls); await UntilAsync(() => observer.Read().Server("overload") == before.Server("overload") + 1);
            AssertServerDelta(before, observer.Read(), "overload");
        }
        finally { handler.Release.TrySetResult(); }
        Assert.Contains(handler.Original.Message, (await Assert.ThrowsAsync<InvocationException>(() => first.WaitAsync(Deadline))).Message);
        await UntilAsync(() => observer.Read().Server("handler") == before.Server("handler") + 1); var after = observer.Read();
        foreach (var reason in OutcomeObserver.ServerReasons) Assert.Equal(before.Server(reason) + (reason is "handler" or "overload" ? 1 : 0), after.Server(reason));
        AssertClientDelta(before, after, "remote", 2); Assert.Equal(before.Longs["rejected/reason=requests,side=server"] + 1, after.Longs["rejected/reason=requests,side=server"]);
    }

    [Fact]
    public async Task LocalServerStop_RecordsStoppedRequestAndSuppressesItsOwnConnectionFailure()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var handler = new OutcomeHandler("cooperative-stop", hold: true);
        await using var connection = new PipesSignalServerConnection(endpoint, new OutcomeEvents<PipesSignalServerConnection>(), handler, new PipesTransportOptions());
        var starting = connection.StartAsync(); using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(Deadline); await starting.WaitAsync(Deadline); await SendAsync(peer, Guid.NewGuid()); await handler.Entered.Task.WaitAsync(Deadline);
        await connection.StopAsync().WaitAsync(Deadline); var after = observer.Read(); AssertServerDelta(before, after, "stopped"); AssertConnectionUnchanged(before, after); Assert.True(connection.Completion.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData("success", "response")] [InlineData("return", "handler")] [InlineData("cancel", "cancelled")] [InlineData("encode", "encode")]
    public async Task ResponseWriteFailure_RetainsFirstRequestReasonAndOnePhysicalConnectionFailure(string mode, string reason)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var handler = new OutcomeHandler(mode, hold: true);
        await using var connection = new PipesSignalServerConnection(endpoint, new OutcomeEvents<PipesSignalServerConnection>(), handler, new PipesTransportOptions());
        var start = connection.StartAsync(); using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(Deadline); await start.WaitAsync(Deadline); await SendAsync(peer, Guid.NewGuid()); await handler.Entered.Task.WaitAsync(Deadline);
        try { peer.Dispose(); await UntilAsync(() => observer.Read().Connection("server", "transport") == before.Connection("server", "transport") + 1); }
        finally { handler.Release.TrySetResult(); }
        await UntilAsync(() => observer.Read().Server(reason) == before.Server(reason) + 1); var after = observer.Read(); AssertServerDelta(before, after, reason); AssertConnectionDelta(before, after, "server", "transport");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FirstProtocolConnectionFault_IsNotCountedAgainWhenAResponseWriteAlsoFails(bool handlerFails)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); var handler = new OutcomeHandler(handlerFails ? "return" : "success", hold: true);
        await using var connection = new PipesSignalServerConnection(endpoint, new OutcomeEvents<PipesSignalServerConnection>(), handler, new PipesTransportOptions());
        var start = connection.StartAsync(); using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        await peer.ConnectAsync().WaitAsync(Deadline); await start.WaitAsync(Deadline); await SendAsync(peer, Guid.NewGuid()); await handler.Entered.Task.WaitAsync(Deadline);
        try
        {
            using var malformed = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(Guid.NewGuid(), malformed); SerializationUtils.Write((byte)11, malformed); SerializationUtils.Write(-1, malformed);
            await peer.WriteAsync(malformed.WrittenMemory); await UntilAsync(() => observer.Read().Connection("server", "protocol") == before.Connection("server", "protocol") + 1); peer.Dispose();
        }
        finally { handler.Release.TrySetResult(); }
        var reason = handlerFails ? "handler" : "response"; await UntilAsync(() => observer.Read().Server(reason) == before.Server(reason) + 1);
        var after = observer.Read(); AssertServerDelta(before, after, reason); AssertConnectionDelta(before, after, "server", "protocol");
    }

    [Fact]
    public async Task DirectServerStartFailure_UsesOtherConnectionReasonOnceAndNoRequestOutcome()
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var handler = new OutcomeHandler("success");
        await using var connection = new PipesSignalServerConnection("invalid\0endpoint", new OutcomeEvents<PipesSignalServerConnection>(), handler, new PipesTransportOptions());
        Assert.IsAssignableFrom<ArgumentException>(await Record.ExceptionAsync(() => connection.StartAsync().WaitAsync(Deadline)));
        var failed = observer.Read(); AssertConnectionDelta(before, failed, "server", "other"); AssertServerUnchanged(before, failed); Assert.Equal(0, handler.Calls);
        await connection.StopAsync().WaitAsync(Deadline); AssertConnectionDelta(before, observer.Read(), "server", "other");
    }

    [Theory]
    [InlineData("generic")] [InlineData("non-generic")] [InlineData("cobalt")] [InlineData("microsoft-di")]
    public async Task DisabledMetricSnapshots_ExcludeFailuresWaitsLateResponsesAndReconnectsAcrossFactoriesAndBothAppResolvers(string composition)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); await using var pair = new DisabledOutcomePair(composition);
        using var firstCancellation = new CancellationTokenSource(); using var queuedCancellation = new CancellationTokenSource();
        var first = pair.Client.InvokeAsync(10, null, firstCancellation.Token); await pair.Handler.Entered.Task.WaitAsync(Deadline);
        var queued = pair.Client.InvokeAsync(11, _ => Assert.Fail("Cancelled waiter must not serialize."), queuedCancellation.Token); await UntilAsync(() => pair.Client.QueuedRequestCount == 1);
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => pair.Client.InvokeAsync(12, null)); queuedCancellation.Cancel(); firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Deadline));
        Assert.Equal(1, pair.Client.PendingRequestCount); pair.Handler.Release.TrySetResult(); await UntilAsync(() => pair.Client.PendingRequestCount == 0 && pair.Server.ActiveRequestCount == 0);
        pair.Handler.Mode = "success"; Assert.Equal(13, await pair.Client.InvokeAsync(13, null, bytes => bytes.Span[0]).WaitAsync(Deadline));
        await pair.RestartServerAsync(); pair.Handler.Mode = "return";
        Assert.Contains(pair.Handler.Original.Message, (await Assert.ThrowsAsync<InvocationException>(() => pair.Client.InvokeAsync(14, null).WaitAsync(Deadline))).Message);
        await UntilAsync(() => pair.Server.ActiveRequestCount == 0); await pair.DisposeAsync(); var after = observer.Read();
        Assert.Equal(before.Longs, after.Longs); Assert.Equal(before.Doubles, after.Doubles); Assert.Equal(3, pair.Handler.Calls);
    }

    [Theory]
    [InlineData("slow")] [InlineData("throw")] [InlineData("reenter")]
    public async Task OutcomeObservers_RemainPullOnlyWhileQueueWaitAndFailureProducersContinueNormally(string callback)
    {
        using var observer = new OutcomeObserver(); var before = observer.Read(); var endpoint = Endpoint(); using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new() { MaxPendingRequests = 1, MaxQueuedRequests = 1 }); await peer.WaitForConnectionAsync().WaitAsync(Deadline);
        var original = new IOException("producer serializer failure"); var observerError = new IOException("observer pull failure"); var calls = 0; var entered = Signal(); using var release = new ManualResetEventSlim(); Task? nested = null;
        observer.OnMeasurement = () =>
        {
            var invocation = Interlocked.Increment(ref calls);
            if (callback == "throw") throw observerError;
            if (callback == "slow" && invocation == 1) { entered.TrySetResult(); Assert.True(release.Wait(Deadline)); }
            if (callback == "reenter" && invocation == 1) nested = client.InvokeAsync(15, _ => throw original);
        };
        var first = client.InvokeAsync(10, null); var header = await HeaderAsync(peer); var queued = client.InvokeAsync(11, _ => throw original); await UntilAsync(() => client.QueuedRequestCount == 1); Assert.Equal(0, calls);
        Task<OutcomeSnapshot>? observing = null;
        if (callback == "slow") { observing = Task.Run(observer.Read); await entered.Task.WaitAsync(Deadline); }
        try
        {
            await ReplyAsync(peer, header.Item1); await first.WaitAsync(Deadline); Assert.Same(original, await Record.ExceptionAsync(() => queued.WaitAsync(Deadline)));
            if (callback != "slow") Assert.Equal(0, calls);
        }
        finally { release.Set(); }
        if (callback == "slow") await observing!.WaitAsync(Deadline);
        else if (callback == "throw") Assert.Contains(observerError, Assert.IsType<AggregateException>(Record.Exception(() => observer.Read())).Flatten().InnerExceptions);
        else { observer.Read(); Assert.NotNull(nested); Assert.Same(original, await Record.ExceptionAsync(() => nested!.WaitAsync(Deadline))); }
        observer.OnMeasurement = null; var after = observer.Read(); AssertClientDelta(before, after, "serialize", callback == "reenter" ? 2 : 1); AssertWaitDelta(before, after, "admitted"); Assert.True(calls > 0);
        var next = client.InvokeAsync(12, null); var nextHeader = await HeaderAsync(peer); await ReplyAsync(peer, nextHeader.Item1); await next.WaitAsync(Deadline);
        AssertWaitEqual(after, observer.Read()); AssertClientDelta(before, observer.Read(), "serialize", callback == "reenter" ? 2 : 1);
    }

    public interface IOutcomeContract { }
    private static string Endpoint() => "outcome-" + Guid.NewGuid().ToString("N");
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static NamedPipeServerStream Peer(string endpoint) => new("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    private static PipesInvocationClient<IOutcomeContract> Client(string endpoint, PipesTransportOptions options, OutcomeEvents<PipesInvocationClient<IOutcomeContract>>? events = null) => new(events ?? new(), endpoint, options);
    private static PipesInvocationServer<IOutcomeContract> Server(string endpoint, OutcomeHandler handler, PipesTransportOptions options) => new(new OutcomeEvents<PipesInvocationServer<IOutcomeContract>>(), handler, endpoint, options);
    private static Task<(Guid, byte, int)> HeaderAsync(PipeStream peer) => Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
    private static async Task ReplyAsync(PipeStream peer, Guid id, byte[]? payload = null, byte status = 0)
    {
        payload ??= []; using var writer = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(id, writer); SerializationUtils.Write(status, writer); SerializationUtils.Write(payload.Length, writer); writer.Write(payload);
        await peer.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    private static async Task SendAsync(PipeStream peer, Guid id, byte method = 10, byte[]? payload = null)
    {
        payload ??= []; using var writer = new ArrayPoolBufferWriter<byte>(); SerializationUtils.Write(id, writer); SerializationUtils.Write(method, writer); SerializationUtils.Write(payload.Length, writer); writer.Write(payload);
        await peer.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    private static async Task UntilAsync(Func<bool> predicate)
    { using var timeout = new CancellationTokenSource(Deadline); while (!predicate()) await Task.Delay(5, timeout.Token); }
    private static void AssertShutdown(Exception? error) => Assert.True(error is ObjectDisposedException or OperationCanceledException or IOException, $"Unexpected stop outcome: {error}");
    private static Exception Error(string kind) => kind switch
    { "io" => new IOException("ENERIT_OVERLOADED timeout"), "remote" => new InvocationException("timeout"), "overload" => new PipesInvocationOverloadedException(), "cancel" => new OperationCanceledException("unsolicited"), _ => new InvalidOperationException("other") };
    private static void AssertClientDelta(OutcomeSnapshot before, OutcomeSnapshot after, string reason, long count = 1)
    { foreach (var key in OutcomeObserver.ClientReasons) Assert.Equal(before.Client(key) + (key == reason ? count : 0), after.Client(key)); }
    private static void AssertServerDelta(OutcomeSnapshot before, OutcomeSnapshot after, string reason, long count = 1)
    { foreach (var key in OutcomeObserver.ServerReasons) Assert.Equal(before.Server(key) + (key == reason ? count : 0), after.Server(key)); }
    private static void AssertClientUnchanged(OutcomeSnapshot before, OutcomeSnapshot after) { foreach (var reason in OutcomeObserver.ClientReasons) Assert.Equal(before.Client(reason), after.Client(reason)); }
    private static void AssertServerUnchanged(OutcomeSnapshot before, OutcomeSnapshot after) { foreach (var reason in OutcomeObserver.ServerReasons) Assert.Equal(before.Server(reason), after.Server(reason)); }
    private static void AssertConnectionDelta(OutcomeSnapshot before, OutcomeSnapshot after, string changedSide, string changedReason)
    { foreach (var side in new[] { "client", "server" }) foreach (var reason in new[] { "protocol", "transport", "other" }) Assert.Equal(before.Connection(side, reason) + (side == changedSide && reason == changedReason ? 1 : 0), after.Connection(side, reason)); }
    private static void AssertConnectionUnchanged(OutcomeSnapshot before, OutcomeSnapshot after)
    { foreach (var side in new[] { "client", "server" }) foreach (var reason in new[] { "protocol", "transport", "other" }) Assert.Equal(before.Connection(side, reason), after.Connection(side, reason)); }
    private static void AssertWaitUnchanged(OutcomeSnapshot before, OutcomeSnapshot after) => AssertWaitEqual(before, after);
    private static void AssertWaitEqual(OutcomeSnapshot before, OutcomeSnapshot after)
    { foreach (var outcome in OutcomeObserver.WaitOutcomes) { Assert.Equal(before.WaitCount(outcome), after.WaitCount(outcome)); Assert.Equal(before.WaitSeconds(outcome), after.WaitSeconds(outcome)); } }
    private static void AssertWaitDelta(OutcomeSnapshot before, OutcomeSnapshot after, string outcome)
    {
        foreach (var key in OutcomeObserver.WaitOutcomes)
        {
            Assert.Equal(before.WaitCount(key) + (key == outcome ? 1 : 0), after.WaitCount(key));
            if (key == outcome) Assert.True(after.WaitSeconds(key) > before.WaitSeconds(key), "An accepted waiter must record positive causal elapsed time.");
            else Assert.Equal(before.WaitSeconds(key), after.WaitSeconds(key));
        }
    }
    private sealed record OutcomeSnapshot(SortedDictionary<string, long> Longs, SortedDictionary<string, double> Doubles)
    {
        internal long Client(string reason) => Longs["client.failures/reason=" + reason];
        internal long Server(string reason) => Longs["server.failures/reason=" + reason];
        internal long Connection(string side, string reason) => Longs["connection.failures/reason=" + reason + ",side=" + side];
        internal long WaitCount(string outcome) => Longs["client.queue.wait.count/outcome=" + outcome];
        internal double WaitSeconds(string outcome) => Doubles["client.queue.wait.duration/outcome=" + outcome];
        internal long Reconnects => Longs["client.reconnects/"];
        internal long LateResponses => Longs["client.late_responses/"];
    }
    private sealed record OutcomeValue(Instrument Instrument, object Value, KeyValuePair<string, object?>[] Tags);
    private sealed class OutcomeObserver : IDisposable
    {
        private const string Prefix = "truemoon.enerit.pipes.";
        internal static readonly string[] ClientReasons = ["timeout", "cancelled", "stopped", "overload", "serialize", "decode", "remote", "transport", "other"];
        internal static readonly string[] ServerReasons = ["handler", "cancelled", "stopped", "overload", "encode", "response"];
        internal static readonly string[] WaitOutcomes = ["admitted", "cancelled", "stopped"];
        private readonly MeterListener _listener;
        private readonly ConcurrentQueue<OutcomeValue> _values = new();
        internal readonly ConcurrentQueue<Instrument> Instruments = new();
        internal Action? OnMeasurement;
        internal OutcomeObserver()
        {
            using (new PipesMetrics()) { }
            _listener = new() { InstrumentPublished = (instrument, listener) => { if (instrument.Meter.Name == "TrueMoon.Enerit.Pipes") { Instruments.Enqueue(instrument); listener.EnableMeasurementEvents(instrument); } } };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Observe(instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Observe(instrument, value, tags)); _listener.Start();
        }
        private void Observe(Instrument instrument, object value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        { _values.Enqueue(new(instrument, value, tags.ToArray())); OnMeasurement?.Invoke(); }
        internal OutcomeSnapshot Read()
        {
            _values.Clear(); _listener.RecordObservableInstruments(); var values = _values.ToArray(); Assert.Equal(37, values.Length); Assert.Equal(13, Instruments.Count);
            var longs = new SortedDictionary<string, long>(); var doubles = new SortedDictionary<string, double>();
            foreach (var measurement in values)
            {
                Assert.StartsWith(Prefix, measurement.Instrument.Name); var name = measurement.Instrument.Name[Prefix.Length..];
                var tags = measurement.Tags.OrderBy(tag => tag.Key).ToArray(); Assert.Equal(tags.Select(tag => tag.Key).Distinct().Count(), tags.Length);
                Assert.All(tags, tag => Assert.IsType<string>(tag.Value)); var suffix = string.Join(',', tags.Select(tag => tag.Key + "=" + tag.Value));
                if (name == "client.queue.wait.duration")
                {
                    Assert.IsType<ObservableCounter<double>>(measurement.Instrument); Assert.Equal("s", measurement.Instrument.Unit); var value = Assert.IsType<double>(measurement.Value);
                    Assert.True(double.IsFinite(value)); Assert.True(value >= 0); doubles.Add(name + "/" + suffix, value);
                }
                else
                {
                    var gauge = name is "client.pending" or "client.queued" or "server.requests" or "server.request.bytes" or "server.connections";
                    if (gauge) Assert.IsType<ObservableUpDownCounter<long>>(measurement.Instrument); else Assert.IsType<ObservableCounter<long>>(measurement.Instrument);
                    var unit = name == "server.request.bytes" ? "By" : name is "server.connections" or "client.reconnects" or "connection.failures" ? "{connection}" : name == "client.late_responses" ? "{response}" : "{request}";
                    Assert.Equal(unit, measurement.Instrument.Unit); var value = Assert.IsType<long>(measurement.Value); Assert.InRange(value, 0, long.MaxValue); longs.Add(name + "/" + suffix, value);
                }
            }
            var expected = new List<string> { "client.pending/", "client.queued/", "server.requests/", "server.request.bytes/", "server.connections/", "rejected/reason=queue_full,side=client", "rejected/reason=requests,side=server", "rejected/reason=bytes,side=server", "client.reconnects/", "client.late_responses/" };
            expected.AddRange(ClientReasons.Select(reason => "client.failures/reason=" + reason)); expected.AddRange(ServerReasons.Select(reason => "server.failures/reason=" + reason));
            expected.AddRange(new[] { "client", "server" }.SelectMany(side => new[] { "protocol", "transport", "other" }.Select(reason => "connection.failures/reason=" + reason + ",side=" + side)));
            expected.AddRange(WaitOutcomes.Select(outcome => "client.queue.wait.count/outcome=" + outcome));
            Assert.Equal(34, longs.Count); Assert.Equal(3, doubles.Count); Assert.Equal(expected.Order(), longs.Keys);
            Assert.Equal(WaitOutcomes.Select(outcome => "client.queue.wait.duration/outcome=" + outcome).Order(), doubles.Keys);
            return new(longs, doubles);
        }
        public void Dispose() => _listener.Dispose();
    }
    private sealed class OutcomeHandler(string mode, bool hold = false) : IInvocationServerHandler<IOutcomeContract>
    {
        internal readonly TaskCompletionSource Entered = Signal(), Release = Signal(); internal int Calls;
        internal string Mode = mode;
        internal readonly Exception Original = mode is "cancel" or "return-cancel" ? new OperationCanceledException("handler cancellation") : new IOException("ENERIT_OVERLOADED text is still a handler error");
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            Interlocked.Increment(ref Calls); Entered.TrySetResult();
            if (hold) { if (Mode == "cooperative-stop") await Release.Task.WaitAsync(token); else await Release.Task; }
            if (Mode == "throw" || Mode == "cancel") throw Original;
            if (Mode == "handler-overload") throw new PipesInvocationOverloadedException();
            if (Mode is "return" or "return-cancel" or "return-null") return (false, Mode == "return-null" ? null : Original);
            writer.Write(Mode == "encode" ? new byte[PipesProtocol.MaxPayloadLength + 1] : new byte[] { method });
            return (true, Mode == "success-error" ? Original : null);
        }
    }
    private sealed class DisabledOutcomePair : IAsyncDisposable
    {
        internal readonly OutcomeHandler Handler = new("return", hold: true);
        internal readonly PipesInvocationClient<IOutcomeContract> Client;
        internal PipesInvocationServer<IOutcomeContract> Server;
        private readonly Func<PipesInvocationServer<IOutcomeContract>> _createServer;
        private readonly IApp? _app;
        internal DisabledOutcomePair(string composition)
        {
            var endpoint = Endpoint(); var options = new PipesTransportOptions { MetricsEnabled = false, MaxPendingRequests = 1, MaxQueuedRequests = 1, MaxConnections = 2 };
            if (composition == "generic")
            {
                var servers = new PipesInvocationServerFactory<IOutcomeContract>(new OutcomeEvents<PipesInvocationServer<IOutcomeContract>>(), options);
                var clients = new PipesInvocationClientFactory<IOutcomeContract>(new OutcomeEvents<PipesInvocationClient<IOutcomeContract>>(), options);
                options.MetricsEnabled = true; _createServer = () => servers.Create(new(endpoint, Handler)); Server = _createServer(); Client = clients.Create(endpoint);
            }
            else if (composition == "non-generic")
            {
                var events = new OutcomeEventsFactory(); var servers = new PipesInvocationServerFactory(events, new OutcomeResolver(Handler), _ => endpoint, options); var clients = new PipesInvocationClientFactory(events, _ => endpoint, options);
                options.MetricsEnabled = true; _createServer = () => Assert.IsType<PipesInvocationServer<IOutcomeContract>>(servers.Create<IOutcomeContract>()); Server = _createServer(); Client = Assert.IsType<PipesInvocationClient<IOutcomeContract>>(clients.Create<IOutcomeContract>());
            }
            else
            {
                _app = App.Builder(context => { if (composition == "microsoft-di") context.UseDI(); }).Setup(context =>
                {
                    context.UseDiagnostics(); context.PipesServiceTransport(_ => endpoint, options); context.Services(services => services.Singleton<IInvocationServerHandler<IOutcomeContract>>(_ => Handler));
                }).Build();
                options.MetricsEnabled = true; var servers = _app.Services.Resolve<IInvocationServerFactory>()!; var clients = _app.Services.Resolve<IInvocationClientFactory>()!;
                _createServer = () => Assert.IsType<PipesInvocationServer<IOutcomeContract>>(servers.Create<IOutcomeContract>()); Server = _createServer(); Client = Assert.IsType<PipesInvocationClient<IOutcomeContract>>(clients.Create<IOutcomeContract>());
            }
        }
        internal async Task RestartServerAsync() { await Server.StopAsync().WaitAsync(Deadline); await UntilAsync(() => !Client.IsConnected); Server = _createServer(); await UntilAsync(() => Client.IsConnected); }
        public async ValueTask DisposeAsync() { Handler.Release.TrySetResult(); await Client.DisposeAsync(); await Server.DisposeAsync(); if (_app != null) await _app.DisposeAsync(); }
    }
    private sealed class OutcomeResolver(OutcomeHandler handler) : IInvocationServerHandlerResolver
    { public IInvocationServerHandler<T> Resolve<T>() => (IInvocationServerHandler<T>)(object)handler; }
    private sealed class OutcomeEventsFactory : IEventsSourceFactory
    { public IEventsSource Create(string name) => new OutcomeEvents<object>(); public IEventsSource<T> Create<T>() => new OutcomeEvents<T>(); }
    private sealed class OutcomeEvents<T> : IEventsSource<T>
    {
        internal int Connected, Late; internal readonly TaskCompletionSource<Guid> Starting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Name => typeof(T).FullName!;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? value, string? category = null, string? caller = null)
        {
            var payload = value?.Invoke(); if (payload is "Connected") Interlocked.Increment(ref Connected); if (category == "LateResponse") Interlocked.Increment(ref Late);
            if (payload is DiagnosticMessage { EventId: 2100 } message && Guid.TryParse(message.Properties?.GetValueOrDefault("RequestId"), out var id)) Starting.TrySetResult(id);
        }
    }
}
