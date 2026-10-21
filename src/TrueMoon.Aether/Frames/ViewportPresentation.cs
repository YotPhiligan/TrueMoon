using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Frames;

/// <summary>Explicit viewport delivery mode; CPU is the default.</summary>
public enum ViewportTransportKind
{
    /// <summary>Owned shared-memory frames copied into a UI image.</summary>
    Cpu,
    /// <summary>Externally synchronized GPU frames copied into a UI-owned GPU image.</summary>
    Gpu
}

/// <summary>Transport-independent surface identity and extent; contains no native or mapping handles.</summary>
public sealed record ViewportSurface(SessionIdentity Identity, ulong Generation, int Width, int Height, ViewportTransportKind Transport)
{
    /// <summary>Whether rendering is suspended without allocations.</summary>
    public bool Suspended => Width == 0 && Height == 0;
    /// <summary>Validates the shared reference viewport bounds and identity.</summary>
    public void Validate()
    {
        Identity.Validate();
        if (Generation == 0 || !Enum.IsDefined(Transport) || (!Suspended && (Width is < 16 or > 1920 || Height is < 16 or > 1080)))
            throw new InvalidDataException("Invalid viewport surface.");
    }
}

/// <summary>A frame ticket issued by one presentation, retained alongside the consumer-owned image.</summary>
public sealed class ViewportFrame
{
    internal ViewportFrame(ViewportPresentation presentation, FrameMetadata metadata) { Presentation = presentation; Metadata = metadata; }
    /// <summary>Originating surface and first-present owner.</summary>
    public ViewportPresentation Presentation { get; }
    /// <summary>Exact produced frame/tick metadata.</summary>
    public FrameMetadata Metadata { get; }
}

/// <summary>Matches actual UI presentation to the last issued frame on one live surface.</summary>
public sealed class ViewportPresentation
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _presented;
    private ViewportFrame? _latest;
    private bool _closed;
    /// <summary>Creates a pending presentation; issuing or copying a frame does not complete it.</summary>
    public ViewportPresentation(ViewportSurface surface) : this(surface, new(TaskCreationOptions.RunContinuationsAsynchronously)) { }
    internal ViewportPresentation(ViewportSurface surface, TaskCompletionSource presented)
    { ArgumentNullException.ThrowIfNull(surface); surface.Validate(); Surface = surface; _presented = presented; }
    /// <summary>Immutable descriptor of this presentation.</summary>
    public ViewportSurface Surface { get; }
    /// <summary>Completes only after an explicit actual-presentation acknowledgement, or faults/cancels when closed.</summary>
    public Task FirstPresented => _presented.Task;
    /// <summary>Issues a ticket after production. Keep it with the copied UI image; acknowledge after drawing and presentation.</summary>
    public ViewportFrame Issue(FrameMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (Surface.Suspended || metadata.FrameId == 0 || metadata.Slot is < 0 or > 2 || metadata.ProducedTimestamp <= 0 ||
                (_latest != null && metadata.FrameId <= _latest.Metadata.FrameId)) throw new InvalidDataException("Stale/invalid viewport frame.");
            return _latest = new(this, metadata);
        }
    }
    internal ViewportFrame? TryIssue(FrameMetadata metadata)
    { lock (_gate) return _closed ? null : Issue(metadata); }
    /// <summary>Acknowledges only the exact latest issued ticket. Stale/foreign/closed tickets return false.</summary>
    public bool Presented(ViewportFrame frame)
    {
        lock (_gate)
        {
            if (_closed || frame == null || !ReferenceEquals(_latest, frame) || !ReferenceEquals(frame.Presentation, this)) return false;
            _presented.TrySetResult(); return true;
        }
    }
    /// <summary>Fences tickets and completes pending readiness with the original cause, or cancellation.</summary>
    public void Close(Exception? error = null)
    { lock (_gate) { if (_closed) return; _closed = true; _latest = null; if (error == null) _presented.TrySetCanceled(); else _presented.TrySetException(error); } }
}

/// <summary>Creates a fresh connection for an owned runtime. FrameSession owns and drains the result before process Stop.</summary>
public interface IGameViewportTransport
{
    /// <summary>Explicit delivery mode.</summary>
    ViewportTransportKind Kind { get; }
    /// <summary>Connects without taking process ownership; return only fully owned/retryable connection resources.</summary>
    Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime, CancellationToken token);
}

/// <summary>One runtime's viewport channel; Configure stops/drains the previous pump before reusing its allocations.</summary>
public interface IGameViewportConnection : IAsyncDisposable
{
    /// <summary>Configures a surface and starts delivery without waiting for UI presentation.</summary>
    Task ConfigureAsync(ViewportPresentation presentation, CancellationToken token);
    /// <summary>Current delivery worker completion; unexpected completion/failure invalidates the viewport.</summary>
    Task Completion { get; }
}
