using TrueMoon.Aether.Contracts;

namespace TrueMoon.Aether.Gpu.Surfaces;

/// <summary>Immutable version-two allocation set. FrameId in each offer is the first use of that slot.</summary>
public sealed class GpuSurfaceOffer
{
    /// <summary>Snapshots at most four entries; validation requires exactly three.</summary>
    public GpuSurfaceOffer(ulong generation, IReadOnlyList<GpuFrameOffer> slots)
    { Generation = generation; Slots = Array.AsReadOnly((slots ?? []).Take(4).ToArray()); }
    /// <summary>Monotonic surface generation, including suspended configurations.</summary>
    public ulong Generation { get; }
    /// <summary>Three persistent dedicated allocations with independent binary semaphores.</summary>
    public IReadOnlyList<GpuFrameOffer> Slots { get; }
    /// <summary>Validates the complete set before any handle duplication/import.</summary>
    public void Validate(SessionIdentity identity, GpuAdapterId adapter, ulong expectedGeneration, int width, int height)
    {
        ValidateExtent(width, height);
        if (width == 0 || Generation == 0 || Generation != expectedGeneration || Slots.Count != 3)
            throw new InvalidDataException("Invalid GPU surface allocation set/generation.");
        var handles = new HashSet<ulong>();
        for (var slot = 0; slot < Slots.Count; slot++)
        {
            var offer = Slots[slot] ?? throw new InvalidDataException("Missing GPU slot.");
            offer.Validate(identity, adapter, true);
            if (offer.Stamp.SurfaceGeneration != Generation || offer.Stamp.Slot != slot || offer.Stamp.FrameId != 1 || offer.Width != width || offer.Height != height ||
                !handles.Add(offer.MemoryHandle) || !handles.Add(offer.ReadyHandle) || !handles.Add(offer.ReturnHandle))
                throw new InvalidDataException("GPU slot layout/handle aliases mismatch.");
        }
    }
    /// <summary>Zero-by-zero suspends; partial zero and dimensions outside the bounded profile are rejected.</summary>
    public static void ValidateExtent(int width, int height)
    {
        if ((width != 0 || height != 0) && (width is < 16 or > 1920 || height is < 16 or > 1080))
            throw new ArgumentOutOfRangeException(nameof(width), "Expected 0x0 or a bounded GPU extent.");
    }
}

/// <summary>Snapshot of one slot's current use and protocol phase.</summary>
public sealed record GpuSlotSnapshot(int Slot, string Phase, GpuFrameStamp? Stamp);

/// <summary>Strict per-slot guard; retirement is required before incrementing a slot's FrameId.</summary>
public sealed class GpuSlotLedger
{
    private readonly Lock _gate = new();
    private readonly SessionIdentity _identity;
    private readonly ulong _generation;
    private readonly string[] _phases = ["Offered", "Offered", "Offered"];
    private readonly GpuFrameStamp?[] _stamps = new GpuFrameStamp?[3];
    /// <summary>Creates a fresh surface guard; no GPU work occurs.</summary>
    public GpuSlotLedger(SessionIdentity identity, ulong generation)
    { identity.Validate(); if (generation == 0) throw new ArgumentOutOfRangeException(nameof(generation)); _identity = identity; _generation = generation; }
    /// <summary>Immutable copy of all three phases/stamps.</summary>
    public IReadOnlyList<GpuSlotSnapshot> Slots { get { lock (_gate) return Array.AsReadOnly(Enumerable.Range(0, 3).Select(i => new GpuSlotSnapshot(i, _phases[i], _stamps[i])).ToArray()); } }
    /// <summary>True only when every imported slot has retired.</summary>
    public bool IsIdle { get { lock (_gate) return _phases.All(p => p == "Idle"); } }
    /// <summary>Atomically confirms import of all slots.</summary>
    public void Imported()
    {
        lock (_gate)
        {
            if (_phases.Any(p => p != "Offered")) throw new InvalidOperationException("GPU surface already imported/faulted.");
            Array.Fill(_phases, "Idle");
        }
    }
    /// <summary>Reserves one idle slot, rejecting exhaustion instead of queueing or overwriting.</summary>
    public GpuFrameStamp Begin(int slot)
    {
        lock (_gate)
        {
            if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
            if (_phases[slot] != "Idle") throw new InvalidOperationException("GPU slot is not retired/available.");
            var stamp = new GpuFrameStamp(_identity, _generation, checked((_stamps[slot]?.FrameId ?? 0) + 1), slot, 2);
            _stamps[slot] = stamp; _phases[slot] = "Producing"; return stamp;
        }
    }
    /// <summary>Authorizes local GL use only after observed Vulkan completion.</summary>
    public void Produced(GpuFrameCompletion completion) => Move("Producing", "Ready", completion);
    /// <summary>Reserves the exact ready frame for one local GL use.</summary>
    public void BeginRead(GpuFrameCompletion completion) => Move("Ready", "Reading", completion);
    /// <summary>Records the matching observed local GL fence.</summary>
    public void ReadCompleted(GpuFrameCompletion completion) => Move("Reading", "Complete", completion);
    /// <summary>Authorizes the return command after local GL completion.</summary>
    public void Return(GpuFrameCompletion completion) => Move("Complete", "Returning", completion);
    /// <summary>Producer-side acknowledgement of remote observed GL completion.</summary>
    public void RemoteReturn(GpuFrameCompletion completion) => Move("Ready", "Returning", completion);
    /// <summary>Allows reuse only after observed Vulkan return completion.</summary>
    public void Retired(GpuFrameCompletion completion) => Move("Returning", "Idle", completion);
    /// <summary>Permanently fences every slot after an unknown outcome or runtime exit.</summary>
    public void Fault() { lock (_gate) Array.Fill(_phases, "Faulted"); }
    private void Move(string expected, string next, GpuFrameCompletion completion)
    {
        lock (_gate)
        {
            if (completion?.Stamp == null) throw new InvalidDataException("Missing GPU completion stamp.");
            ValidateStamp(completion.Stamp, _identity); var slot = completion.Stamp.Slot;
            if (completion.Stamp.SurfaceGeneration != _generation || completion.Stamp != _stamps[slot] || !completion.FenceCompleted)
                throw new InvalidDataException("Unconfirmed/stale GPU surface completion.");
            if (_phases[slot] != expected) throw new InvalidOperationException("GPU slot phase " + _phases[slot] + "; expected " + expected);
            _phases[slot] = next;
        }
    }
    internal static void ValidateStamp(GpuFrameStamp stamp, SessionIdentity identity)
    {
        identity.Validate();
        if (stamp.Identity != identity || stamp.SurfaceGeneration == 0 || stamp.FrameId == 0 || stamp.Slot is < 0 or >= 3 || stamp.Version != 2)
            throw new InvalidDataException("Invalid/stale version-two GPU stamp.");
    }
}
