using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests.Preview;

public sealed class GpuProducerShutdownTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task BoundaryRead_CleanEofIsNullWhileStrictReadStillRejectsEofAndCancellation()
    {
        using var empty = new MemoryStream();
        Assert.Null(await GpuWire.ReadMessageOrEndAsync<Message>(empty, default));
        await Assert.ThrowsAsync<EndOfStreamException>(() => GpuWire.ReadMessageAsync<Message>(empty, default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GpuWire.ReadMessageOrEndAsync<Message>(empty, cancelled.Token));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task BoundaryRead_TruncatedHeaderIsNeverCleanShutdown(int bytes)
    {
        using var stream = new MemoryStream(Header(12)[..bytes]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(4)]
    public async Task BoundaryRead_TruncatedPayloadIsNeverCleanShutdown(int available)
    {
        using var stream = new MemoryStream([.. Header(5), .. new byte[available]]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(65537)] [InlineData(int.MaxValue)]
    public async Task BoundaryRead_InvalidLengthRemainsAProtocolFailure(int length)
    {
        using var stream = new MemoryStream(Header(length));
        await Assert.ThrowsAsync<InvalidDataException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Theory]
    [InlineData("{")] [InlineData("{\"Value\":1,\"Text\":\"ok\",\"Unexpected\":2}")]
    public async Task BoundaryRead_MalformedOrUnmappedJsonRemainsAJsonFailure(string json)
    {
        using var stream = new MemoryStream(Frame(json));
        await Assert.ThrowsAsync<JsonException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Fact]
    public async Task BoundaryRead_NullJsonIsNotAnEndOfStreamMarker()
    {
        using var stream = new MemoryStream(Frame("null"));
        await Assert.ThrowsAsync<InvalidDataException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Fact]
    public async Task BoundaryRead_ConsumesFragmentedCompleteMessagesThenOnlyTerminalEofReturnsNull()
    {
        using var stream = new FragmentedStream([.. Frame("{\"Value\":17,\"Text\":\"Луна\"}"), .. Frame("{\"Value\":23,\"Text\":\"next\"}")]);
        Assert.Equal(new(17, "Луна"), await GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
        Assert.Equal(new(23, "next"), await GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
        Assert.Null(await GpuWire.ReadMessageOrEndAsync<Message>(stream, default));
    }

    [Fact]
    public async Task BoundaryRead_ExactMaximumPayloadRemainsValid()
    {
        var value = new string('x', 65534);
        var bytes = Frame("\"" + value + "\""); Assert.Equal(65540, bytes.Length);
        using var stream = new MemoryStream(bytes);
        Assert.Equal(value, await GpuWire.ReadMessageOrEndAsync<string>(stream, default));
        Assert.Null(await GpuWire.ReadMessageOrEndAsync<string>(stream, default));
    }

    [Theory]
    [InlineData(0x8007006du, true)] [InlineData(0x800700e8u, true)] [InlineData(0x800700e9u, true)]
    [InlineData(0x80070020u, false)] [InlineData(0x80070005u, false)] [InlineData(0x80070026u, false)]
    [InlineData(0x80131620u, false)] [InlineData(0x0000006du, false)] [InlineData(0x8001006du, false)]
    [InlineData(0x0007006du, false)] [InlineData(0x80070000u, false)]
    public void PeerClosedFilter_AcceptsOnlyExactWin32PipeClosureHResults(uint hresult, bool expected)
    {
        Assert.Equal(expected, GpuWire.IsPeerClosed(new IOException("wire error", unchecked((int)hresult))));
        Assert.False(GpuWire.IsPeerClosed(new IOException("generic I/O failure")));
        Assert.False(GpuWire.IsPeerClosed(new EndOfStreamException("truncated frame")));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BoundaryCodec_DoesNotSuppressOriginalIoExceptionsEvenWhenPeerClosedClassifierMatches(bool closed)
    {
        var original = new IOException("original read error", closed ? unchecked((int)0x8007006d) : unchecked((int)0x80131620));
        using var stream = new ThrowingStream(original);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => GpuWire.ReadMessageOrEndAsync<Message>(stream, default)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Producer_SuspendedClientDisconnectCompletesNormallyAndReleasesAllOwnedWork(bool readFinalReply)
    {
        await using var gpu = new GpuResourceHost(); var launch = Launch();
        using var timeout = new CancellationTokenSource(Deadline);
        var worker = gpu.RunSurfaceProducerAsync(launch, timeout.Token);
        using (var pipe = Client(launch))
        {
            await pipe.ConnectAsync(timeout.Token);
            await GpuWire.WriteMessageAsync(pipe, new GpuSurfaceMessage(1, "Configure", launch.Identity, 1), timeout.Token);
            if (readFinalReply)
            {
                var reply = await GpuWire.ReadMessageAsync<GpuSurfaceMessage>(pipe, timeout.Token);
                Assert.Equal("Suspended", reply.Kind); Assert.Equal(1UL, reply.Sequence); Assert.Equal(1UL, reply.Generation); Assert.Null(reply.Offer);
            }
        }
        await worker.WaitAsync(Deadline); Assert.True(worker.IsCompletedSuccessfully);
        await gpu.DisposeAsync().AsTask().WaitAsync(Deadline); AssertNoOwnedResources(gpu);
    }

    [Theory]
    [InlineData(0x80131620u)] [InlineData(0x8007006du)] [InlineData(0x800700e8u)] [InlineData(0x800700e9u)]
    public async Task Producer_CallbackIoFailureAlwaysFaultsWithOriginalIdentityRatherThanBecomingPeerShutdown(uint hresult)
    {
        await using var gpu = new GpuResourceHost(); var launch = Launch();
        var original = new IOException("callback IO must remain observable", unchecked((int)hresult)); var callbacks = 0;
        using var timeout = new CancellationTokenSource(Deadline);
        var worker = gpu.RunSurfaceProducerAsync(launch, timeout.Token, (_, _) => { callbacks++; throw original; });
        using var pipe = Client(launch); await pipe.ConnectAsync(timeout.Token);
        await GpuWire.WriteMessageAsync(pipe, new GpuSurfaceMessage(1, "Configure", launch.Identity, 1), timeout.Token);
        var reply = await GpuWire.ReadMessageAsync<GpuSurfaceMessage>(pipe, timeout.Token);
        Assert.Equal("Error", reply.Kind); Assert.Equal(original.Message, reply.Error); Assert.Equal(1, callbacks);
        Assert.Same(original, await Assert.ThrowsAsync<IOException>(() => worker.WaitAsync(Deadline))); Assert.True(worker.IsFaulted);
        await gpu.DisposeAsync().AsTask().WaitAsync(Deadline); AssertNoOwnedResources(gpu);
    }

    [Fact]
    public async Task Producer_TruncatedIncomingHeaderStillFaultsInsteadOfOrderlyShutdown()
    {
        await using var gpu = new GpuResourceHost(); var launch = Launch();
        using var timeout = new CancellationTokenSource(Deadline);
        var worker = gpu.RunSurfaceProducerAsync(launch, timeout.Token);
        using (var pipe = Client(launch))
        { await pipe.ConnectAsync(timeout.Token); await pipe.WriteAsync(new byte[] { 12 }, timeout.Token); }
        await Assert.ThrowsAsync<EndOfStreamException>(() => worker.WaitAsync(Deadline));
        await gpu.DisposeAsync().AsTask().WaitAsync(Deadline); AssertNoOwnedResources(gpu);
    }

    private static byte[] Header(int length)
    { var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, length); return bytes; }
    private static byte[] Frame(string json)
    { var bytes = Encoding.UTF8.GetBytes(json); return [.. Header(bytes.Length), .. bytes]; }
    private static SessionLaunch Launch() => new(new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64)),
        "engine-preview-shutdown-" + Guid.NewGuid().ToString("N"), "preview-shutdown-tests");
    private static NamedPipeClientStream Client(SessionLaunch launch) => new(".", launch.Endpoint + "-gpu-surface", PipeDirection.InOut,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    private static void AssertNoOwnedResources(GpuResourceHost gpu)
    {
        var diagnostic = Assert.IsType<GpuSurfaceProducerDiagnostics>(gpu.LastSurfaceProducer);
        Assert.False(diagnostic.ValidationEnabled); Assert.Equal(0, diagnostic.Errors); Assert.Equal(0, diagnostic.Warnings); Assert.Empty(diagnostic.Messages);
        Assert.Equal(0, diagnostic.SurfacesCreated); Assert.Equal(0, diagnostic.SurfacesReleased);
        Assert.Equal(0, diagnostic.SlotsCreated); Assert.Equal(0, diagnostic.SlotsReleased);
        Assert.Equal(0, diagnostic.DevicesCreated); Assert.Equal(0, diagnostic.DevicesReleased);
        Assert.Equal(0, diagnostic.LiveSlots); Assert.Equal(0, diagnostic.OpenHandles);
        Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount); Assert.Null(gpu.CleanupError);
    }
    private sealed record Message(int Value, string Text);
    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
    private sealed class ThrowingStream(IOException error) : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(error);
    }
}
