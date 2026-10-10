using System.Buffers;
using System.IO.Pipes;
using System.Reflection;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Threading;

namespace TrueMoon.Enerit.Tests;

public class UtilityLifetimeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(17)]
    public void DeserializedBytesHaveTrackedWholeRentalOwnership(int length)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        var expected = Enumerable.Range(0, length).Select(index => (byte)index).ToArray();
        SerializationUtils.WriteBytes(expected, writer);
        var offset = 0;
        var result = SerializationUtils.ReadBytes(writer.WrittenSpan, ref offset);
        Assert.Equal(expected, result.ToArray());
        Assert.Equal(sizeof(int) + length, offset);
        Assert.True(result.TryReturn());
        Assert.False(result.TryReturn());
        Assert.Throws<ObjectDisposedException>(() => _ = result.Span.Length);
    }

    [Fact]
    public void DeserializedBytesRejectTruncatedAndNegativeLengthBeforeRenting()
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(4, writer);
        SerializationUtils.Write((byte)1, writer);
        var offset = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => SerializationUtils.ReadBytes(writer.WrittenSpan, ref offset));
        Assert.Equal(0, offset);
        writer.Clear();
        SerializationUtils.Write(-1, writer);
        Assert.Throws<ArgumentOutOfRangeException>(() => SerializationUtils.ReadBytes(writer.WrittenSpan, ref offset));
        Assert.Equal(0, offset);
    }

    [Fact]
    public async Task ClientDisposeWhileConnectingTerminatesBothOwnedSchedulers()
    {
        using var events = new EventsSource<PipesInvocationClient<UnconnectedClientMarker>>();
        var client = new PipesInvocationClient<UnconnectedClientMarker>(events);
        await Task.Run(client.Dispose).WaitAsync(Timeout);
        client.Dispose();
        Assert.False(client.IsConnected);
        AssertSchedulersCompleted(client, "_listenTaskScheduler", "_taskScheduler");
    }

    [Fact]
    public async Task ClientCompletesEmptyResponseAndDisposalRejectsFurtherInvocations()
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.ToArray(), deadline.Token);
        var (guid, _, _) = await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        using var reply = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, reply);
        SerializationUtils.Write((byte)0, reply);
        SerializationUtils.Write(0, reply);
        await stream.WriteAsync(reply.WrittenMemory, deadline.Token);
        Assert.Empty(await resultTask.WaitAsync(Timeout));
        await Task.Run(client.Dispose).WaitAsync(Timeout);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.InvokeAsync((byte)1, null, memory => memory.Length));
        AssertSchedulersCompleted(client, "_listenTaskScheduler", "_taskScheduler");
    }

    [Fact]
    public async Task ClientDisposeFaultsPendingResponseAndTerminatesWorkers()
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.Length, deadline.Token);
        await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        await Task.Run(client.Dispose).WaitAsync(Timeout);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => resultTask.WaitAsync(Timeout));
        AssertSchedulersCompleted(client, "_listenTaskScheduler", "_taskScheduler");
    }

    [Fact]
    public async Task ClientReportsTruncatedResponseInsteadOfLeavingRequestPending()
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.Length, deadline.Token);
        var (guid, _, _) = await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        using var reply = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, reply);
        SerializationUtils.Write((byte)0, reply);
        SerializationUtils.Write(2, reply);
        SerializationUtils.Write((byte)42, reply);
        await stream.WriteAsync(reply.WrittenMemory, deadline.Token);
        stream.Dispose();
        await Assert.ThrowsAsync<EndOfStreamException>(() => resultTask.WaitAsync(Timeout));
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(2, 0)]
    public async Task ClientReportsInvalidResponseHeader(int status, int length)
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.Length, deadline.Token);
        var (guid, _, _) = await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        using var reply = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, reply);
        SerializationUtils.Write((byte)status, reply);
        SerializationUtils.Write(length, reply);
        await stream.WriteAsync(reply.WrittenMemory, deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => resultTask.WaitAsync(Timeout));
    }
    [Fact]
    public async Task ClientDisconnectFaultsPendingResponseWithoutWaitingForTimeout()
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.Length, deadline.Token);
        await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        stream.Dispose();
        await Assert.ThrowsAsync<IOException>(() => resultTask.WaitAsync(Timeout));
        AssertNoPendingResponses(client);
    }

    [Fact]
    public async Task ClientCancellationRemovesPendingResponseRegistration()
    {
        var name = $"tm_{typeof(ResponseClientMarker).FullName}";
        using var stream = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var events = new EventsSource<PipesInvocationClient<ResponseClientMarker>>();
        using var client = new PipesInvocationClient<ResponseClientMarker>(events);
        using var deadline = new CancellationTokenSource(Timeout);
        using var requestCancellation = new CancellationTokenSource();
        await stream.WaitForConnectionAsync(deadline.Token);
        var resultTask = client.InvokeAsync((byte)1, null, memory => memory.Length, requestCancellation.Token);
        await Task.Run(() => stream.GetRequestHeader()).WaitAsync(Timeout);
        requestCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask.WaitAsync(Timeout));
        AssertNoPendingResponses(client);
    }

    private static void AssertNoPendingResponses(object client)
    {
        var responses = client.GetType().GetField("_responses", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        Assert.Equal(0, (int)responses.GetType().GetProperty("Count")!.GetValue(responses)!);
    }
    [Fact]
    public async Task ServerConnectionDisposeInterruptsOutstandingConnectAndTerminatesWorkers()
    {
        using var events = new EventsSource("connection-dispose");
        var connection = new PipesSignalServerConnection(Guid.NewGuid().ToString("N"), events, new EmptyHandler());
        var connecting = connection.StartAsync();
        Assert.False(connecting.IsCompleted);
        await Task.Run(connection.Dispose).WaitAsync(Timeout);
        var error = await Record.ExceptionAsync(() => connecting.WaitAsync(Timeout));
        Assert.True(error is IOException or ObjectDisposedException or OperationCanceledException, $"Unexpected result: {error}");
        connection.Dispose();
        AssertSchedulersCompleted(connection, "_listenTaskScheduler", "_execTaskScheduler");
    }

    [Fact]
    public async Task ServerDisposeTerminatesOwnedSchedulerWithoutClient()
    {
        using var events = new EventsSource<PipesInvocationServer<UnconnectedServerMarker>>();
        var server = new PipesInvocationServer<UnconnectedServerMarker>(events, new EmptyServerHandler());
        await Task.Run(server.Dispose).WaitAsync(Timeout);
        server.Dispose();
        AssertSchedulersCompleted(server, "_taskScheduler");
    }

    [Fact]
    public async Task ConnectionDisposeWaitsAsyncHandlersAndReturnsTheirRequestMemory()
    {
        using var events = new EventsSource("handler-dispose");
        var handler = new PausedHandler();
        using var connection = new PipesSignalServerConnection(Guid.NewGuid().ToString("N"), events, handler);
        using var client = new NamedPipeClientStream(".", connection.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var deadline = new CancellationTokenSource(Timeout);
        var starting = connection.StartAsync();
        await client.ConnectAsync(deadline.Token);
        await starting.WaitAsync(Timeout);
        using var request = new ArrayPoolBufferWriter<byte>();
        var guid = Guid.NewGuid();
        SerializationUtils.Write(guid, request);
        SerializationUtils.Write((byte)3, request);
        SerializationUtils.Write(1, request);
        SerializationUtils.Write((byte)73, request);
        await client.WriteAsync(request.WrittenMemory, deadline.Token);
        await handler.Entered.Task.WaitAsync(Timeout);
        Assert.Equal(73, handler.Memory.Span[0]);
        var disposing = Task.Run(connection.Dispose);
        try
        {
            await handler.CancellationObserved.Task.WaitAsync(Timeout);
            Assert.False(disposing.IsCompleted);
        }
        finally { handler.Release.TrySetResult(); }
        await disposing.WaitAsync(Timeout);
        Assert.True(handler.Finished.Task.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(() => _ = handler.Memory.Span.Length);
        AssertSchedulersCompleted(connection, "_listenTaskScheduler", "_execTaskScheduler");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PipeHeadersReadEntireFragmentedFrame(bool response)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        var expected = Guid.NewGuid();
        SerializationUtils.Write(expected, writer);
        SerializationUtils.Write((byte)7, writer);
        SerializationUtils.Write(123, writer);
        using var stream = new FragmentedPipeStream(writer.WrittenMemory.ToArray());
        var (guid, code, length) = response ? stream.GetResponseHeader() : stream.GetRequestHeader();
        Assert.Equal(expected, guid);
        Assert.Equal(7, code);
        Assert.Equal(123, length);
        Assert.Equal(21, stream.ReadCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PipeHeadersRejectPrematureEof(bool response)
    {
        using var stream = new FragmentedPipeStream(new byte[] { 1 });
        Assert.Throws<EndOfStreamException>(() =>
        {
            if (response) { stream.GetResponseHeader(); }
            else { stream.GetRequestHeader(); }
        });
        Assert.Equal(2, stream.ReadCalls);
    }

    [Fact]
    public void PipePayloadReadRejectsPrematureEof()
    {
        using var stream = new FragmentedPipeStream(new byte[] { 1 });
        Assert.Throws<EndOfStreamException>(() => stream.ReadFullBuffer(new byte[2]));
        Assert.Equal(2, stream.ReadCalls);
    }

    private sealed class FragmentedPipeStream(byte[] payload) : PipeStream(PipeDirection.InOut, 0)
    {
        private int _offset;
        public int ReadCalls { get; private set; }
        public override int Read(Span<byte> buffer)
        {
            ReadCalls++;
            if (_offset == payload.Length || buffer.IsEmpty) { return 0; }
            buffer[0] = payload[_offset++];
            return 1;
        }
    }
    private static void AssertSchedulersCompleted(object owner, params string[] fields)
    {
        foreach (var field in fields)
        {
            var scheduler = (TmTaskScheduler)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
            Assert.True(scheduler.Completion.IsCompletedSuccessfully, field);
        }
    }

    private sealed class ResponseClientMarker;
    private sealed class UnconnectedClientMarker;
    private sealed class UnconnectedServerMarker;
    private sealed class EmptyHandler : IInvocationServerHandler
    {
        public Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default) => Task.FromResult<(bool, Exception?)>((true, null));
    }
    private sealed class EmptyServerHandler : IInvocationServerHandler<UnconnectedServerMarker>
    {
        public Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default) => Task.FromResult<(bool, Exception?)>((true, null));
    }
    private sealed class PausedHandler : IInvocationServerHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ReadOnlyMemory<byte> Memory { get; private set; }
        public async Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> readMemory, IBufferWriter<byte> bufferWriter, CancellationToken cancellationToken = default)
        {
            Memory = readMemory;
            using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
            Entered.TrySetResult();
            // Deliberately capture the worker scheduler, proving it survives until continuations finish.
            await Release.Task;
            Finished.TrySetResult();
            return (true, null);
        }
    }
}
