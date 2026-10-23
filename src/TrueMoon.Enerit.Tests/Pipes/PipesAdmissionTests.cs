using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests.Pipes;

public class PipesAdmissionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Factories_SnapshotClientAndServerLimitsBeforeCreatingOwnedProducts(bool generic)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var options = new PipesTransportOptions { MaxPendingRequests = 1, MaxQueuedRequests = 0, MaxConcurrentRequests = 1, MaxBufferedRequestBytes = 8 };
        var handler = new GatedHandler();
        var events = new EventsFactory();
        Func<PipesInvocationClient<GatedHandler>> createClient;
        Func<PipesInvocationServer<GatedHandler>> createServer;
        if (generic)
        {
            IFactory<PipesInvocationClient<GatedHandler>, string> clients = new PipesInvocationClientFactory<GatedHandler>(events.Create<PipesInvocationClient<GatedHandler>>(), options);
            IFactory<PipesInvocationServer<GatedHandler>, PipesInvocationServerArguments<GatedHandler>> servers = new PipesInvocationServerFactory<GatedHandler>(events.Create<PipesInvocationServer<GatedHandler>>(), options);
            createClient = () => clients.Create(endpoint);
            createServer = () => servers.Create(new(endpoint, handler));
        }
        else
        {
            var clients = new PipesInvocationClientFactory(events, _ => endpoint, options);
            var servers = new PipesInvocationServerFactory(events, new HandlerResolver(handler), _ => endpoint, options);
            createClient = () => (PipesInvocationClient<GatedHandler>)clients.Create<GatedHandler>();
            createServer = () => (PipesInvocationServer<GatedHandler>)servers.Create<GatedHandler>();
        }
        options.MaxPendingRequests = options.MaxQueuedRequests = options.MaxConcurrentRequests = 99;
        options.MaxBufferedRequestBytes = 9999;
        await using var server = createServer();
        await using var client = createClient();
        await using var otherClient = Client(endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        try
        {
            var pending = client.InvokeAsync(1, w => w.Write(new byte[7]), timeout.Token);
            await UntilAsync(() => handler.Calls == 1);
            var serialized = 0;
            await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(2, _ => serialized++, timeout.Token));
            Assert.Equal(0, serialized);
            var remote = await Assert.ThrowsAsync<InvocationException>(() => otherClient.InvokeAsync(3, null, timeout.Token));
            Assert.Contains("ENERIT_OVERLOADED", remote.Message);
            Assert.Equal(1, handler.Calls); Assert.Equal(1, server.ActiveRequestCount); Assert.Equal(7, server.BufferedRequestBytes);
            handler.Release.TrySetResult();
            await pending.WaitAsync(Deadline);
        }
        finally { handler.Release.TrySetResult(); }
        await client.StopAsync().WaitAsync(Deadline);
        await server.StopAsync().WaitAsync(Deadline);
        Assert.True(client.Completion.IsCompletedSuccessfully); Assert.True(server.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Client_ResponseDecoderHoldsSlotUntilRentalAndCallbackAreReleased()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 1, MaxQueuedRequests = 0 });
        using var timeout = new CancellationTokenSource(Deadline);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await peer.WaitForConnectionAsync(timeout.Token);
        var pending = client.InvokeAsync(1, null, _ =>
        {
            entered.TrySetResult();
            Assert.True(release.Wait(Deadline), "Decoder was not released.");
            return 73;
        }, timeout.Token);
        try
        {
            var header = await HeaderAsync(peer);
            await ReplyAsync(peer, header.Id);
            await entered.Task.WaitAsync(Deadline);
            Assert.Equal(1, client.PendingRequestCount);
            var serialized = 0;
            await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(2, _ => serialized++, timeout.Token));
            Assert.Equal(0, serialized);
            release.Set();
            Assert.Equal(73, await pending.WaitAsync(Deadline));
            await UntilAsync(() => client.PendingRequestCount == 0);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task Client_DisposeInsideResponseDecoderDrainsAfterCallbackWithoutSelfDeadlock()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(timeout.Token);
        var pending = client.InvokeAsync(1, null, _ =>
        {
            client.Dispose();
            Assert.False(client.Completion.IsCompleted);
            Assert.Equal(1, client.PendingRequestCount);
            return 73;
        }, timeout.Token);
        var header = await HeaderAsync(peer);
        await ReplyAsync(peer, header.Id);
        Assert.Equal(73, await pending.WaitAsync(Deadline));
        await client.Completion.WaitAsync(Deadline);
        Assert.Equal(0, client.PendingRequestCount);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Client_DisconnectDuringSerializationHoldsSlotUntilWriterCleanup()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 1, MaxQueuedRequests = 0 });
        using var timeout = new CancellationTokenSource(Deadline);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await peer.WaitForConnectionAsync(timeout.Token);
        var pending = client.InvokeAsync(1, _ =>
        {
            entered.TrySetResult();
            Assert.True(release.Wait(Deadline), "Serializer was not released.");
        }, timeout.Token);
        try
        {
            await entered.Task.WaitAsync(Deadline);
            peer.Dispose();
            await Assert.ThrowsAnyAsync<IOException>(() => pending.WaitAsync(Deadline));
            Assert.Equal(1, client.PendingRequestCount);
            var serialized = 0;
            await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(2, _ => serialized++, timeout.Token));
            Assert.Equal(0, serialized);
            release.Set();
            await UntilAsync(() => client.PendingRequestCount == 0);
            await client.StopAsync().WaitAsync(Deadline);
            Assert.True(client.Completion.IsCompletedSuccessfully);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task Client_CancellationDuringPartialWriteAbortsConnectionWithoutReplay()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 1, MaxQueuedRequests = 0 });
        using var timeout = new CancellationTokenSource(Deadline);
        using var local = new CancellationTokenSource();
        await peer.WaitForConnectionAsync(timeout.Token);
        var pending = client.InvokeAsync(1, w => w.Write(new byte[PipesProtocol.MaxPayloadLength]), local.Token);
        var header = await HeaderAsync(peer);
        Assert.Equal(1, header.Method); Assert.Equal(PipesProtocol.MaxPayloadLength, header.Length);
        // Reading only the header leaves the large payload blocked in the writer.
        local.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Deadline));
        await UntilAsync(() => !client.IsConnected && client.PendingRequestCount == 0);
        peer.Dispose();
        using var replacement = Peer(endpoint);
        await replacement.WaitForConnectionAsync(timeout.Token);
        var next = client.InvokeAsync(2, null, timeout.Token);
        var nextHeader = await HeaderAsync(replacement);
        Assert.Equal(2, nextHeader.Method); Assert.Equal(0, nextHeader.Length);
        await ReplyAsync(replacement, nextHeader.Id);
        await next.WaitAsync(Deadline);
        Assert.True(client.IsConnected);
    }

    [Theory]
    [InlineData("pending-zero")]
    [InlineData("queued-negative")]
    [InlineData("concurrent-zero")]
    [InlineData("bytes-zero")]
    [InlineData("connections-zero")]
    [InlineData("connections-over")]
    [InlineData("timeout-zero")]
    [InlineData("timeout-over")]
    public void Owners_RejectInvalidOptionsBeforeStartingIo(string invalid)
    {
        var options = new PipesTransportOptions();
        switch (invalid)
        {
            case "pending-zero": options.MaxPendingRequests = 0; break;
            case "queued-negative": options.MaxQueuedRequests = -1; break;
            case "concurrent-zero": options.MaxConcurrentRequests = 0; break;
            case "bytes-zero": options.MaxBufferedRequestBytes = 0; break;
            case "connections-zero": options.MaxConnections = 0; break;
            case "connections-over": options.MaxConnections = 65; break;
            case "timeout-zero": options.RequestTimeout = TimeSpan.Zero; break;
            case "timeout-over": options.RequestTimeout = TimeSpan.FromHours(24) + TimeSpan.FromTicks(1); break;
        }
        var handler = new GatedHandler();
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationClient<GatedHandler>(new Events<PipesInvocationClient<GatedHandler>>(), "invalid-options", options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationServer<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), handler, "invalid-options", options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesSignalServerConnection("invalid-options", new Events<GatedHandler>(), handler, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationClientFactory<GatedHandler>(new Events<PipesInvocationClient<GatedHandler>>(), options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationServerFactory<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationClientFactory(new EventsFactory(), null, options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PipesInvocationServerFactory(new EventsFactory(), new HandlerResolver(handler), null, options));
    }

    [Fact]
    public async Task Client_BoundedQueueCancelsBeforeSerializationAndRejectsExcessWithoutSending()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        var options = new PipesTransportOptions { MaxPendingRequests = 2, MaxQueuedRequests = 1 };
        await using var client = Client(endpoint, options);
        // Mutating the caller's options must not expand an already constructed owner.
        options.MaxPendingRequests = options.MaxQueuedRequests = 99;
        using var timeout = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(timeout.Token);
        var first = client.InvokeAsync(1, null, timeout.Token);
        var second = client.InvokeAsync(2, null, timeout.Token);
        var headers = new[] { await HeaderAsync(peer), await HeaderAsync(peer) };
        Assert.Equal(new byte[] { 1, 2 }, headers.Select(h => h.Method).Order().ToArray());
        var serialized = 0;
        using var cancelled = new CancellationTokenSource();
        var queued = client.InvokeAsync(3, _ => Interlocked.Increment(ref serialized), cancelled.Token);
        await UntilAsync(() => client.QueuedRequestCount == 1);
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(4, _ => Interlocked.Increment(ref serialized), timeout.Token));
        Assert.Equal(2, client.PendingRequestCount);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
        await UntilAsync(() => client.QueuedRequestCount == 0);
        Assert.Equal(0, serialized);
        var admitted = client.InvokeAsync(5, _ => Interlocked.Increment(ref serialized), timeout.Token);
        await UntilAsync(() => client.QueuedRequestCount == 1);
        Assert.Equal(0, serialized);
        await ReplyAsync(peer, headers[0].Id);
        var admittedHeader = await HeaderAsync(peer);
        Assert.Equal(5, admittedHeader.Method); Assert.Equal(1, serialized);
        await ReplyAsync(peer, headers[1].Id);
        await ReplyAsync(peer, admittedHeader.Id);
        await Task.WhenAll(first, second, admitted).WaitAsync(Deadline);
        var recovered = client.InvokeAsync(6, null, timeout.Token);
        var next = await HeaderAsync(peer);
        Assert.Equal(6, next.Method);
        Assert.Equal(0, next.Length);
        await ReplyAsync(peer, next.Id);
        await recovered.WaitAsync(Deadline);
        await UntilAsync(() => client.PendingRequestCount == 0);
    }

    [Fact]
    public async Task Client_AdmissionDeadlineExpiresWithoutSerializingOrReleasingSentRequest()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions
        { MaxPendingRequests = 1, MaxQueuedRequests = 1, RequestTimeout = TimeSpan.FromMilliseconds(350) });
        using var timeout = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(timeout.Token);
        var sent = client.InvokeAsync(1, null, timeout.Token);
        var header = await HeaderAsync(peer);
        var serialized = 0;
        var queued = client.InvokeAsync(2, _ => Interlocked.Increment(ref serialized), timeout.Token);
        await UntilAsync(() => client.QueuedRequestCount == 1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sent.WaitAsync(Deadline));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
        Assert.False(timeout.IsCancellationRequested);
        Assert.Equal(0, serialized);
        Assert.Equal(1, client.PendingRequestCount);
        Assert.Equal(0, client.QueuedRequestCount);
        await ReplyAsync(peer, header.Id);
        await UntilAsync(() => client.PendingRequestCount == 0);
        var recovered = client.InvokeAsync(3, null, timeout.Token);
        var next = await HeaderAsync(peer);
        Assert.Equal(3, next.Method);
        await ReplyAsync(peer, next.Id);
        await recovered.WaitAsync(Deadline);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_SentCancellationHoldsCapacityUntilLateReplyOrDisconnect(bool disconnect)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 2, MaxQueuedRequests = 0 });
        using var timeout = new CancellationTokenSource(Deadline);
        using var local = new CancellationTokenSource();
        await peer.WaitForConnectionAsync(timeout.Token);
        var first = client.InvokeAsync(1, null, local.Token);
        var second = client.InvokeAsync(2, null, timeout.Token);
        var headers = new[] { await HeaderAsync(peer), await HeaderAsync(peer) };
        local.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Deadline));
        Assert.Equal(2, client.PendingRequestCount);
        var serialized = 0;
        await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(3, _ => serialized++, timeout.Token));
        Assert.Equal(0, serialized);
        if (disconnect)
        {
            peer.Dispose();
            await Assert.ThrowsAnyAsync<IOException>(() => second.WaitAsync(Deadline));
            await UntilAsync(() => client.PendingRequestCount == 0);
        }
        else
        {
            await ReplyAsync(peer, headers.Single(h => h.Method == 1).Id);
            await UntilAsync(() => client.PendingRequestCount == 1);
            var next = client.InvokeAsync(4, null, timeout.Token);
            var nextHeader = await HeaderAsync(peer);
            Assert.Equal(4, nextHeader.Method);
            await ReplyAsync(peer, headers.Single(h => h.Method == 2).Id);
            await ReplyAsync(peer, nextHeader.Id);
            await Task.WhenAll(second, next).WaitAsync(Deadline);
            await UntilAsync(() => client.PendingRequestCount == 0);
        }
    }

    [Fact]
    public async Task Client_StopFaultsPendingAndAdmissionWaitersAndCompletesOwnedWorkers()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 1, MaxQueuedRequests = 1 });
        using var timeout = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(timeout.Token);
        var sent = client.InvokeAsync(1, null, timeout.Token);
        await HeaderAsync(peer);
        var serialized = 0;
        var queued = client.InvokeAsync(2, _ => serialized++, timeout.Token);
        await UntilAsync(() => client.QueuedRequestCount == 1);
        await client.StopAsync().WaitAsync(Deadline);
        AssertShutdown(await Record.ExceptionAsync(() => sent));
        AssertShutdown(await Record.ExceptionAsync(() => queued));
        Assert.Equal(0, serialized);
        Assert.Equal(0, client.PendingRequestCount);
        Assert.Equal(0, client.QueuedRequestCount);
        Assert.True(client.Completion.IsCompletedSuccessfully);
        await client.StopAsync().WaitAsync(Deadline);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InvokeAsync(3, null));
    }

    [Fact]
    public async Task Client_ConcurrentAdmissionAndStopLeaveNoOwnedWorkAfterCompletion()
    {
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var endpoint = Guid.NewGuid().ToString("N");
            using var peer = Peer(endpoint);
            await using var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 2, MaxQueuedRequests = 2 });
            using var timeout = new CancellationTokenSource(Deadline);
            await peer.WaitForConnectionAsync(timeout.Token);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempts = Enumerable.Range(0, 16).Select(async _ =>
            {
                await start.Task;
                return await Record.ExceptionAsync(() => client.InvokeAsync(1, null, timeout.Token));
            }).ToArray();
            var stop = Task.Run(async () => { await start.Task; await client.StopAsync().WaitAsync(Deadline); });
            start.SetResult();
            var outcomes = await Task.WhenAll(attempts).WaitAsync(Deadline);
            await stop.WaitAsync(Deadline);
            Assert.All(outcomes, error =>
            {
                if (error is not PipesInvocationOverloadedException) AssertShutdown(error);
            });
            Assert.True(client.Completion.IsCompletedSuccessfully);
            Assert.False(client.IsConnected);
            Assert.Equal(0, client.PendingRequestCount);
            Assert.Equal(0, client.QueuedRequestCount);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InvokeAsync(2, null));
            Assert.Equal(0, client.PendingRequestCount);
            Assert.Equal(0, client.QueuedRequestCount);
        }
    }

    [Fact]
    public async Task Client_StopDrainsWriterQueuedAfterItsCallerCancellationSourceWasDisposed()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = Peer(endpoint);
        // Disposal is joined explicitly with a deadline: this regression must fail instead of hanging teardown.
        var client = Client(endpoint, new PipesTransportOptions { MaxPendingRequests = 3, MaxQueuedRequests = 0 });
        using var timeout = new CancellationTokenSource(Deadline);
        using var release = new ManualResetEventSlim();
        var bothWritersEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writersEntered = 0;
        Task? stop = null;
        Exception? failure = null;
        try
        {
            await peer.WaitForConnectionAsync(timeout.Token);
            void Serialize(IBufferWriter<byte> writer)
            {
                if (Interlocked.Increment(ref writersEntered) == 2) bothWritersEntered.TrySetResult();
                Assert.True(release.Wait(Deadline), "Blocked writer was not released.");
            }
            var first = client.InvokeAsync(1, Serialize, timeout.Token);
            var second = client.InvokeAsync(2, Serialize, timeout.Token);
            await bothWritersEntered.Task.WaitAsync(Deadline);
            // Both dedicated writer workers are occupied, so this third writer cannot start yet.
            var third = client.InvokeAsync(3, null, timeout.Token);
            Assert.Equal(3, client.PendingRequestCount);
            stop = client.StopAsync();
            var outcomes = await Task.WhenAll(new[] { first, second, third }
                .Select(pending => Record.ExceptionAsync(() => pending))).WaitAsync(Deadline);
            Assert.All(outcomes, error => Assert.IsType<ObjectDisposedException>(error));
            // Every Invoke has now exited its using scope and disposed its linked cancellation source.
            Assert.Equal(3, client.PendingRequestCount);
            Assert.False(client.Completion.IsCompleted);
            release.Set();
            await stop.WaitAsync(Deadline);
            Assert.True(client.Completion.IsCompletedSuccessfully);
            Assert.Equal(0, client.PendingRequestCount);
            Assert.Equal(0, client.QueuedRequestCount);
            Assert.False(client.IsConnected);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            release.Set();
            try { await (stop ?? client.StopAsync()).WaitAsync(Deadline); }
            catch (Exception cleanup) when (failure is not null && !ReferenceEquals(failure, cleanup))
            { throw new AggregateException("Regression and bounded writer cleanup both failed.", failure, cleanup); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Server_SharedRequestAndByteBudgetsRejectBeforeHandlerAndRecover(bool bytes)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var handler = new GatedHandler();
        var options = new PipesTransportOptions { MaxConcurrentRequests = bytes ? 8 : 2, MaxBufferedRequestBytes = bytes ? 7 : PipesProtocol.MaxPayloadLength, MaxConnections = 3 };
        await using var server = new PipesInvocationServer<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), handler, endpoint, options);
        options.MaxConcurrentRequests = 99; options.MaxBufferedRequestBytes = 9999;
        await using var firstClient = Client(endpoint, new PipesTransportOptions());
        await using var secondClient = Client(endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        try
        {
            var first = firstClient.InvokeAsync(1, w => w.Write(new byte[3]), timeout.Token);
            var second = secondClient.InvokeAsync(2, w => w.Write(new byte[4]), timeout.Token);
            await UntilAsync(() => handler.Calls == 2);
            Assert.Equal(2, server.ActiveRequestCount);
            Assert.Equal(7, server.BufferedRequestBytes);
            // Cross the fixed discard scratch boundary; the next frame must remain aligned.
            var rejected = await Assert.ThrowsAsync<InvocationException>(() => secondClient.InvokeAsync(3, w => w.Write(new byte[4097]), timeout.Token));
            Assert.Contains("ENERIT_OVERLOADED", rejected.Message);
            Assert.Equal(2, handler.Calls);
            Assert.Equal(2, server.ActiveRequestCount);
            Assert.Equal(7, server.BufferedRequestBytes);
            handler.Release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(Deadline);
        }
        finally { handler.Release.TrySetResult(); }
        await UntilAsync(() => server.ActiveRequestCount == 0 && server.BufferedRequestBytes == 0);
        await secondClient.InvokeAsync(4, w => w.Write(new byte[7]), timeout.Token).WaitAsync(Deadline);
        Assert.Equal(3, handler.Calls);
        Assert.True(secondClient.IsConnected);
        await server.StopAsync().WaitAsync(Deadline);
        Assert.Equal(0, server.ConnectionCount);
        Assert.True(server.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Server_ConnectionBudgetIncludesListenerAndRecoversAfterDisconnect()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var handler = new GatedHandler(); handler.Release.TrySetResult();
        await using var server = new PipesInvocationServer<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), handler, endpoint,
            new PipesTransportOptions { MaxConnections = 1 });
        await using var first = Client(endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        await first.InvokeAsync(1, null, timeout.Token).WaitAsync(Deadline);
        using (var blocked = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous))
        using (var refused = new CancellationTokenSource(TimeSpan.FromMilliseconds(350)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.ConnectAsync(refused.Token));
            Assert.Equal(1, server.ConnectionCount);
            Assert.Equal(1, handler.Calls);
        }
        await first.StopAsync().WaitAsync(Deadline);
        await using var second = Client(endpoint, new PipesTransportOptions());
        await second.InvokeAsync(2, null, timeout.Token).WaitAsync(Deadline);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1, server.ConnectionCount);
        await server.StopAsync().WaitAsync(Deadline);
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public async Task Server_CancelledStopWaitStillOwnsHandlerDrainAndAccounting()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var handler = new GatedHandler();
        await using var server = new PipesInvocationServer<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), handler, endpoint,
            new PipesTransportOptions());
        await using var client = Client(endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        var pending = client.InvokeAsync(1, w => w.Write(new byte[11]), timeout.Token);
        await UntilAsync(() => handler.Calls == 1);
        try
        {
            using var cancelled = new CancellationTokenSource();
            var stop = server.StopAsync(cancelled.Token);
            await handler.CancellationObserved.Task.WaitAsync(Deadline);
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(Deadline));
            Assert.False(server.Completion.IsCompleted);
            Assert.Equal(1, server.ActiveRequestCount);
            Assert.Equal(11, server.BufferedRequestBytes);
            var secondWait = server.StopAsync();
            Assert.False(secondWait.IsCompleted);
            handler.Release.TrySetResult();
            await secondWait.WaitAsync(Deadline);
            await Assert.ThrowsAnyAsync<IOException>(() => pending.WaitAsync(Deadline));
            Assert.True(server.Completion.IsCompletedSuccessfully);
            Assert.Equal(0, server.ActiveRequestCount);
            Assert.Equal(0, server.BufferedRequestBytes);
            Assert.Equal(0, server.ConnectionCount);
        }
        finally { handler.Release.TrySetResult(); }
    }

    [Fact]
    public async Task Server_StopInterruptsResponseWriterWhenPeerDoesNotRead()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var handler = new GatedHandler { LargeResponse = true }; handler.Release.TrySetResult();
        await using var server = new PipesInvocationServer<GatedHandler>(new Events<PipesInvocationServer<GatedHandler>>(), handler, endpoint,
            new PipesTransportOptions { MaxConnections = 1 });
        using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(Deadline);
        await peer.ConnectAsync(timeout.Token);
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(Guid.NewGuid(), writer); SerializationUtils.Write((byte)1, writer); SerializationUtils.Write(13, writer);
        writer.Write(new byte[13]);
        await peer.WriteAsync(writer.WrittenMemory, timeout.Token);
        await handler.ResponseCreated.Task.WaitAsync(Deadline);
        var (id, status, length) = await Task.Run(peer.GetResponseHeader).WaitAsync(Deadline);
        Assert.NotEqual(Guid.Empty, id); Assert.Equal(0, status); Assert.Equal(PipesProtocol.MaxPayloadLength, length);
        Assert.Equal(1, server.ActiveRequestCount);
        Assert.Equal(13, server.BufferedRequestBytes);
        await server.StopAsync().WaitAsync(Deadline);
        Assert.True(server.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, server.ActiveRequestCount);
        Assert.Equal(0, server.BufferedRequestBytes);
        Assert.Equal(0, server.ConnectionCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Server_BlockingOrThrowingCancellationCallbackCannotAbandonOwnedDrain(bool throws)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var handler = new CancellationHandler(throws);
        var server = new PipesInvocationServer<CancellationHandler>(new Events<PipesInvocationServer<CancellationHandler>>(), handler, endpoint, new PipesTransportOptions());
        await using var client = new PipesInvocationClient<CancellationHandler>(new Events<PipesInvocationClient<CancellationHandler>>(), endpoint, new PipesTransportOptions());
        using var timeout = new CancellationTokenSource(Deadline);
        var pending = client.InvokeAsync(1, w => w.Write(new byte[5]), timeout.Token);
        try
        {
            await handler.Entered.Task.WaitAsync(Deadline);
            using var wait = new CancellationTokenSource();
            var stopped = server.StopAsync(wait.Token);
            await handler.Cancelling.Task.WaitAsync(Deadline);
            wait.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopped.WaitAsync(Deadline));
            Assert.False(server.Completion.IsCompleted);
            Assert.Equal(1, server.ActiveRequestCount); Assert.Equal(5, server.BufferedRequestBytes);
            handler.CallbackRelease.Set(); handler.Release.TrySetResult();
            if (throws)
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() => server.StopAsync().WaitAsync(Deadline));
                Assert.Contains(handler.Original, error.Flatten().InnerExceptions);
                Assert.True(server.Completion.IsFaulted);
            }
            else
            {
                await server.StopAsync().WaitAsync(Deadline);
                Assert.True(server.Completion.IsCompletedSuccessfully);
            }
            Assert.Equal(1, handler.Callbacks);
            Assert.Equal(0, server.ActiveRequestCount); Assert.Equal(0, server.BufferedRequestBytes); Assert.Equal(0, server.ConnectionCount);
            AssertShutdown(await Record.ExceptionAsync(() => pending.WaitAsync(Deadline)));
        }
        finally
        {
            handler.CallbackRelease.Set(); handler.Release.TrySetResult();
            // A faulted Stop is still the owner's completed cleanup task; the asserted fault is retained on repeat disposal.
            if (throws) await Record.ExceptionAsync(() => server.DisposeAsync().AsTask().WaitAsync(Deadline));
            else await server.DisposeAsync().AsTask().WaitAsync(Deadline);
        }
    }

    private static NamedPipeServerStream Peer(string endpoint) => new("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
    private static PipesInvocationClient<GatedHandler> Client(string endpoint, PipesTransportOptions options) => new(new Events<PipesInvocationClient<GatedHandler>>(), endpoint, options);
    private static async Task<(Guid Id, byte Method, int Length)> HeaderAsync(PipeStream stream) => await Task.Run(stream.GetRequestHeader).WaitAsync(Deadline);
    private static async Task ReplyAsync(PipeStream stream, Guid id)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(id, writer); SerializationUtils.Write((byte)0, writer); SerializationUtils.Write(0, writer);
        await stream.WriteAsync(writer.WrittenMemory).AsTask().WaitAsync(Deadline);
    }
    private static async Task UntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(Deadline);
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }
    private static void AssertShutdown(Exception? error) => Assert.True(error is ObjectDisposedException or OperationCanceledException or IOException, $"Unexpected shutdown outcome: {error}");
    private sealed class GatedHandler : IInvocationServerHandler<GatedHandler>
    {
        private int _calls;
        internal int Calls => Volatile.Read(ref _calls);
        internal bool LargeResponse;
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ResponseCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            Interlocked.Increment(ref _calls);
            using var registration = token.Register(() => CancellationObserved.TrySetResult());
            await Release.Task;
            if (LargeResponse) writer.Write(new byte[PipesProtocol.MaxPayloadLength]);
            ResponseCreated.TrySetResult();
            return (true, null);
        }
    }
    private sealed class Events<T> : IEventsSource<T>
    {
        public string Name => typeof(T).Name;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? func, string? category = null, string? caller = null) { }
    }
    private sealed class EventsFactory : IEventsSourceFactory
    {
        public IEventsSource Create(string name) => new Events<object>();
        public IEventsSource<T> Create<T>() => new Events<T>();
    }
    private sealed class HandlerResolver(GatedHandler handler) : IInvocationServerHandlerResolver
    {
        public IInvocationServerHandler<T> Resolve<T>() => (IInvocationServerHandler<T>)(object)handler;
    }
    private sealed class CancellationHandler(bool throws) : IInvocationServerHandler<CancellationHandler>, IDisposable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Cancelling = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim CallbackRelease = new();
        internal readonly ApplicationException Original = new("handler-cancellation-failed");
        internal int Callbacks;
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            using var registration = token.Register(() =>
            {
                Interlocked.Increment(ref Callbacks); Cancelling.TrySetResult();
                if (!CallbackRelease.Wait(Deadline)) throw new TimeoutException("Cancellation callback was not released.");
                if (throws) throw Original;
            });
            Entered.TrySetResult();
            await Release.Task;
            return (true, null);
        }
        public void Dispose() => CallbackRelease.Dispose();
    }
}
