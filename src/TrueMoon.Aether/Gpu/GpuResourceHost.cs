using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Gpu.Vulkan;
using TrueMoon.Aether.Gpu.OpenGl;
using System.IO.Pipes;

namespace TrueMoon.Aether.Gpu;

/// <summary>App-owned GPU resource with retryable cleanup; false means ownership must be retained.</summary>
public interface IGpuResource : IAsyncDisposable
{
    /// <summary>Whether all owned native resources were released.</summary>
    bool IsReleased { get; }
}

/// <summary>Explicit, lazy GPU resource ownership for one Aether App. Startup allocates no GPU resources.</summary>
public sealed class GpuResourceHost : IAsyncDisposable
{
    private readonly Lock _gate = new();
    private readonly List<IGpuResource> _resources = [];
    private readonly CancellationTokenSource _end = new();
    private bool _closed;
    private Task? _dispose;
    /// <summary>Unreleased resources, including failed cleanup retained for retry.</summary>
    public int ResourceCount { get { lock (_gate) return _resources.Count(r => !r.IsReleased); } }
    /// <summary>Observed cleanup failure; cleared by a successful retry.</summary>
    public Exception? CleanupError { get; private set; }
    /// <summary>Process-wide NT handle count owned by the Aether GPU bridge.</summary>
    public int OpenHandleCount => Volatile.Read(ref Interop.GpuNtHandle.ActiveCount);
    /// <summary>Last producer's validation/cleanup snapshot; no unbounded history is retained.</summary>
    public GpuProducerDiagnostics? LastProducer { get; private set; }
    /// <summary>Aggregate reusable-surface validation/retirement snapshot from the runtime role.</summary>
    public Surfaces.GpuSurfaceProducerDiagnostics? LastSurfaceProducer { get; internal set; }
    internal CancellationToken End => _end.Token;
    /// <summary>Connects one App-owned reusable GPU surface session. Configure explicitly after connection.</summary>
    public async Task<Surfaces.GpuSurfaceSession> ConnectSurfaceAsync(NativeRuntimeLease runtime, GpuAdapterId adapter, GpuOpenGlContext context, CancellationToken token = default)
    {
        Surfaces.GpuSurfaceSession session;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_resources.OfType<Surfaces.GpuSurfaceSession>().Any(s => !s.IsReleased)) throw new InvalidOperationException("Release the prior GPU surface session before connecting another.");
            session = Own(new Surfaces.GpuSurfaceSession(runtime, adapter, context, End));
        }
        try { await session.ConnectAsync(token).ConfigureAwait(false); return session; }
        catch (Exception original)
        { try { await session.DisposeAsync().ConfigureAwait(false); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); } throw; }
    }
    /// <summary>Hosts the separate version-two bounded surface channel in the owned runtime role.</summary>
    public Task RunSurfaceProducerAsync(SessionLaunch launch, CancellationToken token, Func<string, CancellationToken, Task>? beforeOperation = null, bool validation = false,
        Func<GpuFrameStamp, int, int, Surfaces.GpuFrameData>? frameProvider = null)
    {
        var work = Own(new GpuProbeWork());
        work.Start(() => Surfaces.GpuSurfaceProducer.RunAsync(this, launch, token, beforeOperation, validation, frameProvider)); return work.Completion;
    }
    /// <summary>Connects the separate one-frame probe channel to a currently owned runtime.</summary>
    public async Task<GpuFrameConnection> ConnectAsync(NativeRuntimeLease runtime, CancellationToken token = default)
    {
        var connection = Own(new GpuFrameConnection(runtime));
        try { using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, End); await connection.ConnectAsync(linked.Token).ConfigureAwait(false); return connection; }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }
    /// <summary>Hosts a single exact-profile transfer in the runtime role. Optional instrumentation runs before each native operation.</summary>
    /// <remarks>Use the matching launch supplied to the owned runtime. The pipe is CurrentUserOnly and bound to its full identity.
    /// User instrumentation must honor cancellation; an owned-process guard is still required for native driver stalls.</remarks>
    public Task RunProducerProbeAsync(SessionLaunch launch, CancellationToken token, Func<string, CancellationToken, Task>? beforeOperation = null, bool validation = false)
    {
        var work = Own(new GpuProbeWork());
        work.Start(() => RunProducerCoreAsync(launch, token, beforeOperation, validation)); return work.Completion;
    }
    private async Task RunProducerCoreAsync(SessionLaunch launch, CancellationToken token, Func<string, CancellationToken, Task>? beforeOperation, bool validation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, End); var end = linked.Token;
        await using var pipe = new NamedPipeServerStream(launch.Endpoint + "-gpu", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        VulkanSharedFrame? frame = null; GpuFrameHandshake? handshake = null; ulong sequence = 0;
        try
        {
            await pipe.WaitForConnectionAsync(end).ConfigureAwait(false);
            while (true)
            {
                var message = await GpuWire.ReadAsync(pipe, end).ConfigureAwait(false);
                if (message.Identity != launch.Identity || message.Sequence != checked(++sequence)) throw new InvalidDataException("GPU request identity/sequence mismatch.");
                GpuWireMessage reply;
                if (beforeOperation != null) await beforeOperation(message.Kind, end).ConfigureAwait(false);
                if (message.Kind == "Create" && frame == null)
                {
                    frame = await CreateProducerAsync(launch.Identity, message.Adapter ?? throw new InvalidDataException("Missing GPU adapter."), message.Width, message.Height, end, validation).ConfigureAwait(false);
                    handshake = new(frame.Offer); reply = new(sequence, "Offer", launch.Identity, Offer: frame.Offer);
                }
                else if (message.Kind == "ImportAck" && frame != null)
                {
                    if (message.Completion?.Stamp != frame.Offer.Stamp) throw new InvalidDataException("ImportAck stamp mismatch.");
                    handshake!.Imported(); reply = new(sequence, "Imported", launch.Identity);
                }
                else if (message.Kind == "Produce" && frame != null && handshake!.Phase == "Imported")
                {
                    var ready = await frame.ProduceAsync(end).ConfigureAwait(false); handshake.Ready(ready); reply = new(sequence, "Ready", launch.Identity, Completion: ready);
                }
                else if (message.Kind == "Release" && frame != null)
                {
                    var release = message.Completion ?? throw new InvalidDataException("Missing GL completion."); handshake!.Released(release);
                    var retired = await frame.RetireAsync(release, end).ConfigureAwait(false); handshake.Retired(retired); reply = new(sequence, "Retired", launch.Identity, Completion: retired);
                }
                else throw new InvalidDataException("Unexpected GPU phase/operation.");
                await GpuWire.WriteAsync(pipe, reply, end).ConfigureAwait(false);
                if (reply.Kind == "Retired") break;
            }
        }
        catch (Exception error)
        {
            handshake?.Fault();
            if (pipe.IsConnected && !end.IsCancellationRequested)
            {
                using var errorDeadline = CancellationTokenSource.CreateLinkedTokenSource(end); errorDeadline.CancelAfter(TimeSpan.FromSeconds(2));
                try { await GpuWire.WriteAsync(pipe, new(sequence, "Error", launch.Identity, Error: error.Message[..Math.Min(error.Message.Length, 4096)]), errorDeadline.Token).ConfigureAwait(false); }
                catch (Exception writeError) when (writeError is IOException or OperationCanceledException) { }
            }
            throw;
        }
        finally { if (frame != null) { try { await frame.DisposeAsync().ConfigureAwait(false); } finally { LastProducer = frame.Diagnostics; } } }
    }
    /// <summary>Transfers cleanup ownership to the App. Do not register another disposable alias.</summary>
    public T Own<T>(T resource) where T : IGpuResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            _resources.RemoveAll(r => r.IsReleased);
            if (_resources.Contains(resource)) throw new InvalidOperationException("GPU resource already owned.");
            _resources.Add(resource); return resource;
        }
    }
    /// <summary>Creates an App-owned dedicated one-frame Vulkan exporter on the explicitly selected adapter.</summary>
    public async Task<VulkanSharedFrame> CreateProducerAsync(SessionIdentity identity, GpuAdapterId adapter, int width, int height, CancellationToken token = default, bool validation = false)
    {
        var frame = Own(new VulkanSharedFrame(identity, adapter, width, height, validation));
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, End);
            await frame.InitializeAsync(linked.Token).ConfigureAwait(false);
            lock (_gate) ObjectDisposedException.ThrowIf(_closed, this);
            return frame;
        }
        catch (Exception original)
        {
            try { await frame.DisposeAsync().ConfigureAwait(false); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    /// <summary>Creates an App-owned import on the caller's current GL owner context.</summary>
    public OpenGlSharedFrame Import(NativeRuntimeLease runtime, GpuFrameOffer offer, GpuAdapterId adapter, GpuOpenGlContext context)
    {
        offer.Validate(runtime.Identity, adapter);
        var frame = Own(new OpenGlSharedFrame(offer, context));
        try { frame.Initialize(runtime); return frame; }
        catch (Exception original)
        {
            try { frame.DisposeOnOwner(); } catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            throw;
        }
    }
    internal void CloseAdmission() { lock (_gate) { if (_closed) return; _closed = true; _end.Cancel(); } }
    /// <summary>Closes admission and attempts every cleanup in reverse order; failed resources remain for an explicit retry.</summary>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion; IGpuResource[] resources;
        lock (_gate)
        {
            if (_dispose is { IsCompletedSuccessfully: true } or { IsCompleted: false }) return new(_dispose);
            CloseAdmission(); resources = _resources.ToArray(); completion = new(TaskCreationOptions.RunContinuationsAsynchronously); _dispose = completion.Task;
        }
        _ = CleanupAsync(); return new(completion.Task);
        async Task CleanupAsync()
        {
            List<Exception> errors = [];
            foreach (var resource in resources.Reverse())
            {
                try { await resource.DisposeAsync().ConfigureAwait(false); if (!resource.IsReleased) throw new IOException("GPU cleanup did not release ownership."); }
                catch (Exception error) { errors.Add(error); }
            }
            lock (_gate)
            {
                _resources.RemoveAll(r => r.IsReleased);
                CleanupError = errors.Count == 0 ? null : errors.Count == 1 ? errors[0] : new AggregateException(errors);
                if (CleanupError == null) completion.TrySetResult(); else completion.TrySetException(CleanupError);
            }
        }
    }
}

internal sealed class GpuProbeWork : IGpuResource
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Completion => _completion.Task;
    public bool IsReleased { get; private set; }
    internal void Start(Func<Task> body) => _ = ExecuteAsync(body);
    private async Task ExecuteAsync(Func<Task> body)
    {
        try { await body().ConfigureAwait(false); _completion.TrySetResult(); }
        catch (Exception error) { _completion.TrySetException(error); }
    }
    public async ValueTask DisposeAsync()
    {
        try { await Completion.ConfigureAwait(false); } catch (Exception) { /* Originating caller owns the worker error; native resources have independent cleanup entries. */ }
        IsReleased = true;
    }
}
