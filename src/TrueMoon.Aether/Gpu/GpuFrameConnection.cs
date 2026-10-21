using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Hosting;

namespace TrueMoon.Aether.Gpu;

/// <summary>One-shot same-build GPU probe channel to an owned runtime. Cancellation/unknown outcome permanently closes the channel.</summary>
public sealed class GpuFrameConnection : IGpuResource
{
    private readonly NativeRuntimeLease _runtime;
    private readonly NamedPipeClientStream _pipe;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ulong _sequence;
    private GpuFrameHandshake? _handshake;
    private GpuFrameOffer? _offer;
    private int _closed;
    private readonly Lock _workGate = new();
    private Task _pending = Task.CompletedTask;
    internal GpuFrameConnection(NativeRuntimeLease runtime)
    { _runtime = runtime; _pipe = new(".", runtime.GpuEndpoint, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); }
    /// <summary>Whether local pipe resources are closed.</summary>
    public bool IsReleased { get { lock (_workGate) return _closed != 0 && _pending.IsCompleted; } }
    internal async Task ConnectAsync(CancellationToken token)
    {
        TaskCompletionSource completion;
        lock (_workGate)
        {
            ObjectDisposedException.ThrowIf(_closed != 0, this); completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _pending = completion.Task;
        }
        try { await _pipe.ConnectAsync(3000, token).ConfigureAwait(false); }
        finally { completion.TrySetResult(); }
    }
    /// <summary>Creates one exact-profile exported image; does not submit GPU work.</summary>
    public async Task<GpuFrameOffer> CreateAsync(GpuAdapterId adapter, int width, int height, CancellationToken token = default)
    {
        if (_offer != null) throw new InvalidOperationException("Only one GPU image per probe connection.");
        var reply = await ExchangeAsync("Create", new(0, "Create", _runtime.Identity, Adapter: adapter, Width: width, Height: height), "Offer", token).ConfigureAwait(false);
        GpuFrameOffer offer;
        try { offer = reply.Offer ?? throw new InvalidDataException("Missing GPU offer."); offer.Validate(_runtime.Identity, adapter); }
        catch { await DisposeAsync().ConfigureAwait(false); throw; }
        _offer = offer; _handshake = new(offer); return offer;
    }
    /// <summary>Acknowledges successful import, allowing producer submission.</summary>
    public async Task ImportedAsync(CancellationToken token = default)
    {
        Guard().Imported();
        try { await ExchangeAsync("ImportAck", new(0, "ImportAck", _runtime.Identity, Completion: new(_offer!.Stamp, false)), "Imported", token).ConfigureAwait(false); }
        catch { Close(); throw; }
    }
    /// <summary>Requests submission; the reply is accepted only after matching CPU-observed Vulkan completion.</summary>
    public async Task<GpuFrameCompletion> ProduceAsync(CancellationToken token = default)
    {
        if (Guard().Phase != "Imported") throw new InvalidOperationException("GPU image was not imported.");
        var reply = await ExchangeAsync("Produce", new(0, "Produce", _runtime.Identity), "Ready", token).ConfigureAwait(false);
        try { var ready = reply.Completion ?? throw new InvalidDataException("Missing ready completion."); Guard().Ready(ready); return ready; }
        catch { Close(); throw; }
    }
    /// <summary>Returns an image only after GL CPU-visible completion and accepts retirement only after Vulkan completion.</summary>
    public async Task<GpuFrameCompletion> ReturnAsync(GpuFrameCompletion release, CancellationToken token = default)
    {
        Guard().Released(release);
        try
        {
            var reply = await ExchangeAsync("Release", new(0, "Release", _runtime.Identity, Completion: release), "Retired", token).ConfigureAwait(false);
            var retired = reply.Completion ?? throw new InvalidDataException("Missing retired completion."); Guard().Retired(retired); return retired;
        }
        catch { Close(); throw; }
    }
    private GpuFrameHandshake Guard() { ObjectDisposedException.ThrowIf(IsReleased, this); return _handshake ?? throw new InvalidOperationException("Create a GPU image first."); }
    private async Task<GpuWireMessage> ExchangeAsync(string operation, GpuWireMessage message, string expected, CancellationToken token)
    {
        // Reject concurrent commands rather than queueing a phase check made against an earlier state.
        if (!await _gate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("GPU command already pending.");
        TaskCompletionSource? completion = null;
        try
        {
            lock (_workGate)
            {
                ObjectDisposedException.ThrowIf(_closed != 0, this);
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _pending = completion.Task;
            }
            if (_runtime.Exited.IsCompleted) throw new IOException("GPU runtime exited.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(8));
            var sequence = checked(++_sequence); message = message with { Sequence = sequence };
            await GpuWire.WriteAsync(_pipe, message, deadline.Token).ConfigureAwait(false);
            var reply = await GpuWire.ReadAsync(_pipe, deadline.Token).ConfigureAwait(false);
            if (reply.Sequence != sequence || reply.Identity != _runtime.Identity || reply.Kind != expected)
                throw new InvalidDataException(operation + " GPU reply mismatch/error: " + reply.Error);
            return reply;
        }
        catch { _handshake?.Fault(); Close(); throw; }
        finally { _gate.Release(); completion?.TrySetResult(); }
    }
    /// <summary>Fences reuse and releases the pipe. GPU objects remain owned separately until confirmed cleanup.</summary>
    public ValueTask DisposeAsync()
    { Close(); lock (_workGate) return new(_pending); }
    private void Close()
    { lock (_workGate) if (Interlocked.Exchange(ref _closed, 1) == 0) { _handshake?.Fault(); _pipe.Dispose(); } }
}

internal sealed record GpuWireMessage(ulong Sequence, string Kind, SessionIdentity Identity, GpuFrameOffer? Offer = null,
    GpuAdapterId? Adapter = null, int Width = 0, int Height = 0, GpuFrameCompletion? Completion = null, string? Error = null);
internal static class GpuWire
{
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    internal static async Task WriteAsync(Stream stream, GpuWireMessage message, CancellationToken token)
        => await WriteMessageAsync(stream, message, token).ConfigureAwait(false);
    internal static async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        if (bytes.Length is 0 or > 65536) throw new InvalidDataException("GPU message exceeds 64KiB.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false); await stream.WriteAsync(bytes, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false);
    }
    internal static async Task<GpuWireMessage> ReadAsync(Stream stream, CancellationToken token)
        => await ReadMessageAsync<GpuWireMessage>(stream, token).ConfigureAwait(false);
    internal static async Task<T> ReadMessageAsync<T>(Stream stream, CancellationToken token) where T : class
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token).ConfigureAwait(false); var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is <= 0 or > 65536) throw new InvalidDataException("Invalid GPU message length.");
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException("Missing GPU message.");
    }
}
