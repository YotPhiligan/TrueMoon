using System.Buffers;
using System.Diagnostics;
using System.IO.Pipes;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests.Pipes;

public partial class PipesProtocolTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(3, "InvalidDataException")]
    [InlineData(4, "Remote error exceeded the pipes payload limit.")]
    public async Task Server_OversizedResultOrError_ReturnsBoundedRemoteErrorAndKeepsChannel(byte method, string message)
    {
        var handler = new Handler();
        var endpoint = Guid.NewGuid().ToString("N");
        using var server = new PipesInvocationServer<Handler>(new SilentEvents<PipesInvocationServer<Handler>>(), handler, endpoint);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        var error = await Assert.ThrowsAsync<InvocationException>(() => client.InvokeAsync(method, null, deadline.Token));
        Assert.Contains(message, error.Message);
        Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(error.Message), 1, PipesProtocol.MaxPayloadLength);
        await client.InvokeAsync(0, null, deadline.Token);
        Assert.Equal(2, handler.Calls);
        Assert.True(client.IsConnected);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public async Task Client_UnknownResponseStatus_FaultsPending(byte status)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var pending = client.InvokeAsync(0, null, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, writer); SerializationUtils.Write(status, writer); SerializationUtils.Write(0, writer);
        await peer.WriteAsync(writer.WrittenMemory, deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => pending.WaitAsync(Deadline));
    }

    [Fact]
    public async Task Server_EmptySuccessAndRemoteErrors_AreAcknowledgedWithoutDisconnect()
    {
        var handler = new Handler();
        var endpoint = Guid.NewGuid().ToString("N");
        using var server = new PipesInvocationServer<Handler>(new SilentEvents<PipesInvocationServer<Handler>>(), handler, endpoint);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await client.InvokeAsync(0, null, deadline.Token);
        var remote = await Assert.ThrowsAsync<InvocationException>(() => client.InvokeAsync(1, null, deadline.Token));
        Assert.Contains("handler-reported-failure", remote.Message);
        var thrown = await Assert.ThrowsAsync<InvocationException>(() => client.InvokeAsync(2, null, deadline.Token));
        Assert.Contains("handler-threw", thrown.Message);
        await client.InvokeAsync(0, null, deadline.Token);
        Assert.True(client.IsConnected);
        Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1048577)]
    [InlineData(int.MaxValue)]
    public async Task Client_InvalidResponseLength_FaultsPendingWithoutAllocatingPayload(int length)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var pending = client.InvokeAsync(0, null, static result => result.Length, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ReplyAsync(peer, guid, length, [], deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => pending.WaitAsync(Deadline));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1048577)]
    [InlineData(int.MaxValue)]
    public async Task Server_InvalidRequestLength_DisconnectsBeforeDispatch(int length)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        var handler = new Handler();
        using var server = new PipesSignalServerConnection(endpoint, new SilentEvents<Handler>(), handler);
        using var peer = new NamedPipeClientStream(".", "tm_" + endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.Disconnected += (_, _) => disconnected.TrySetResult();
        using var deadline = new CancellationTokenSource(Deadline);
        var start = server.StartAsync(deadline.Token);
        await peer.ConnectAsync(deadline.Token);
        await start.WaitAsync(Deadline);
        await ReplyAsync(peer, Guid.NewGuid(), length, [], deadline.Token);
        await disconnected.Task.WaitAsync(Deadline);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048576)]
    public async Task Client_ResponseLengthBoundaries_ConsumeEntireFrameAndPreserveNextResponse(int length)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var pending = client.InvokeAsync(0, null, data => (data.Length, data.IsEmpty ? 0 : data.Span[0], data.IsEmpty ? 0 : data.Span[^1]), deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        var bytes = new byte[length];
        if (length > 0) { bytes[0] = 42; bytes[^1] = 61; }
        await ReplyAsync(peer, guid, length, bytes, deadline.Token);
        Assert.Equal((length, length == 0 ? 0 : 42, length == 0 ? 0 : 61), await pending.WaitAsync(Deadline));
        var next = client.InvokeAsync(0, null, data => data.Span[0], deadline.Token);
        var (nextGuid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ReplyAsync(peer, nextGuid, 1, new byte[] { 93 }, deadline.Token);
        Assert.Equal(93, await next.WaitAsync(Deadline));
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Client_OversizedRequest_IsNotWrittenAndNextRequestSucceeds()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.InvokeAsync(4, w => w.Write(new byte[PipesProtocol.MaxPayloadLength + 1]), deadline.Token));
        var valid = client.InvokeAsync(55, null, deadline.Token);
        var (guid, method, length) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        Assert.Equal(55, method);
        Assert.Equal(0, length);
        await ReplyAsync(peer, guid, 0, [], deadline.Token);
        await valid.WaitAsync(Deadline);
    }

    private static async Task ReplyAsync(PipeStream pipe, Guid guid, int length, byte[] bytes, CancellationToken token)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, writer); SerializationUtils.Write((byte)0, writer); SerializationUtils.Write(length, writer);
        writer.Write(bytes);
        await pipe.WriteAsync(writer.WrittenMemory, token);
    }
    private sealed class Handler : IInvocationServerHandler<Handler>
    {
        internal int Calls;
        public Task<(bool, Exception?)> HandleAsync(byte method, ReadOnlyMemory<byte> memory, IBufferWriter<byte> writer, CancellationToken token = default)
        {
            Interlocked.Increment(ref Calls);
            if (method == 2) throw new InvalidOperationException("handler-threw");
            if (method == 3) writer.Write(new byte[PipesProtocol.MaxPayloadLength + 1]);
            if (method == 4) throw new InvalidOperationException(new string('x', PipesProtocol.MaxPayloadLength + 1));
            return Task.FromResult<(bool, Exception?)>(method == 1 ? (false, new InvalidOperationException("handler-reported-failure")) : (true, null));
        }
    }
    private sealed class SilentEvents<T> : IEventsSource<T>
    {
        public string Name => typeof(T).Name;
        public Activity StartActivity(string? details = null, string? category = null, string? caller = null) => new(Name);
        public void StopActivity(Activity activity) => activity.Stop();
        public void Write(Func<object>? func, string? category = null, string? caller = null) { }
    }
}
