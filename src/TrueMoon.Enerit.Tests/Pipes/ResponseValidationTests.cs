using System.Buffers;
using System.IO.Pipes;
using System.Text;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;

namespace TrueMoon.Enerit.Tests.Pipes;

public partial class PipesProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_NonemptyAcknowledgementIsRejectedAndNextCallSucceeds(bool synchronous)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var pending = synchronous ? Task.Run(() => client.Invoke(0, null)) : client.InvokeAsync(0, null, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ReplyAsync(peer, guid, 1, new byte[] { 42 }, deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => pending.WaitAsync(Deadline));
        Assert.True(client.IsConnected);
        var next = client.InvokeAsync(0, null, deadline.Token);
        var (nextGuid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        Assert.NotEqual(guid, nextGuid);
        await ReplyAsync(peer, nextGuid, 0, [], deadline.Token);
        await next.WaitAsync(Deadline);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Client_DecodeFailureReturnsOuterResponseAndPreservesNextFrame()
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        ReadOnlyMemory<byte> observed = default;
        var pending = client.InvokeAsync(0, null, data =>
        {
            observed = data;
            var offset = 0;
            var value = SerializationUtils.Read<int>(data.Span, ref offset);
            SerializationUtils.EnsureFullyConsumed(offset, data.Length);
            return value;
        }, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        using var payload = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(37, payload);
        payload.Write(new byte[] { 99 });
        await ReplyAsync(peer, guid, payload.WrittenCount, payload.WrittenSpan.ToArray(), deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => pending.WaitAsync(Deadline));
        Assert.Equal(5, observed.Length);
        Assert.Throws<ObjectDisposedException>(() => _ = observed.Span.Length);
        Assert.False(observed.TryReturn());
        var next = client.InvokeAsync(0, null, data =>
        {
            var offset = 0;
            var value = SerializationUtils.Read<int>(data.Span, ref offset);
            SerializationUtils.EnsureFullyConsumed(offset, data.Length);
            return value;
        }, deadline.Token);
        var (nextGuid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ReplyAsync(peer, nextGuid, 4, BitConverter.GetBytes(73), deadline.Token);
        Assert.Equal(73, await next.WaitAsync(Deadline));
        Assert.True(client.IsConnected);
    }

    [Theory]
    [InlineData(new byte[] { 255 })]
    [InlineData(new byte[] { 195 })]
    [InlineData(new byte[] { 192, 175 })]
    public async Task Client_InvalidUtf8ErrorFaultsAllPendingWithoutLosingMatchedRequest(byte[] error)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var first = client.InvokeAsync(0, null, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        var second = client.InvokeAsync(0, null, deadline.Token);
        await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ErrorReplyAsync(peer, guid, error, deadline.Token);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => first.WaitAsync(Deadline));
        Assert.IsType<DecoderFallbackException>(failure.InnerException);
        await Assert.ThrowsAsync<InvalidDataException>(() => second.WaitAsync(Deadline));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ошибка 🌙")]
    public async Task Client_ValidUtf8ErrorPreservesTextAndNextAcknowledgement(string error)
    {
        var endpoint = Guid.NewGuid().ToString("N");
        using var peer = new NamedPipeServerStream("tm_" + endpoint, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new PipesInvocationClient<Handler>(new SilentEvents<PipesInvocationClient<Handler>>(), endpoint);
        using var deadline = new CancellationTokenSource(Deadline);
        await peer.WaitForConnectionAsync(deadline.Token);
        var pending = client.InvokeAsync(0, null, deadline.Token);
        var (guid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ErrorReplyAsync(peer, guid, Encoding.UTF8.GetBytes(error), deadline.Token);
        Assert.Equal(error, (await Assert.ThrowsAsync<InvocationException>(() => pending.WaitAsync(Deadline))).Message);
        var next = client.InvokeAsync(0, null, deadline.Token);
        var (nextGuid, _, _) = await Task.Run(peer.GetRequestHeader).WaitAsync(Deadline);
        await ReplyAsync(peer, nextGuid, 0, [], deadline.Token);
        await next.WaitAsync(Deadline);
        Assert.True(client.IsConnected);
    }

    private static async Task ErrorReplyAsync(PipeStream peer, Guid guid, byte[] error, CancellationToken token)
    {
        using var writer = new ArrayPoolBufferWriter<byte>();
        SerializationUtils.Write(guid, writer);
        SerializationUtils.Write((byte)1, writer);
        SerializationUtils.Write(error.Length, writer);
        writer.Write(error);
        await peer.WriteAsync(writer.WrittenMemory, token);
    }
}
