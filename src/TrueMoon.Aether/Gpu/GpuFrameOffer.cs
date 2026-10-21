using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Gpu;

/// <summary>Identity of the single ENG4b image; no native pointers cross this boundary.</summary>
public sealed record GpuFrameStamp(SessionIdentity Identity, ulong SurfaceGeneration = 1, ulong FrameId = 1, int Slot = 0, int Version = 1)
{
    /// <summary>Rejects stale, malformed or unsupported one-frame identity before native work.</summary>
    public void Validate(SessionIdentity expected)
    {
        expected.Validate();
        if (Identity != expected || SurfaceGeneration == 0 || FrameId != 1 || Slot != 0 || Version != 1)
            throw new InvalidDataException("Invalid/stale GPU frame stamp.");
    }
}

/// <summary>Dedicated exported allocation and source-process NT handle tokens, duplicated only through the owned runtime lease.</summary>
public sealed record GpuFrameOffer(GpuFrameStamp Stamp, GpuAdapterId Adapter, int Width, int Height, ulong AllocationBytes,
    ulong MemoryHandle, ulong ReadyHandle, ulong ReturnHandle, ulong Offset = 0, bool Dedicated = true,
    string Format = "RGBA8_UNORM", string ColorSpace = "sRGB", string Alpha = "Premultiplied", string Origin = "TopLeft")
{
    /// <summary>Preflights the exact ENG4b profile without using a source handle as a local handle.</summary>
    public void Validate(SessionIdentity expected, GpuAdapterId adapter)
        => Validate(expected, adapter, false);
    internal void Validate(SessionIdentity expected, GpuAdapterId adapter, bool reusable)
    {
        if (Stamp == null || Adapter == null || adapter == null) throw new InvalidDataException("Missing GPU image identity/adapter.");
        if (reusable) Surfaces.GpuSlotLedger.ValidateStamp(Stamp, expected);
        else Stamp.Validate(expected);
        if (Adapter != adapter || !Hex(adapter.DeviceUuid) || !Hex(adapter.DriverUuid) || Width is < 16 or > 1920 || Height is < 16 or > 1080 ||
            AllocationBytes < checked((ulong)Width * (ulong)Height * 4) || AllocationBytes > 64 * 1024 * 1024 || Offset != 0 || !Dedicated ||
            Format != "RGBA8_UNORM" || ColorSpace != "sRGB" || Alpha != "Premultiplied" || Origin != "TopLeft" ||
            !Token(MemoryHandle) || !Token(ReadyHandle) || !Token(ReturnHandle) || MemoryHandle == ReadyHandle || MemoryHandle == ReturnHandle || ReadyHandle == ReturnHandle)
            throw new InvalidDataException("Unsupported GPU image/handle profile.");
    }
    private static bool Token(ulong value) => value is > 0 and < long.MaxValue;
    private static bool Hex(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit) && value.Any(c => c != '0');
}

/// <summary>Acknowledgement that the relevant local fence was observed complete on the CPU.</summary>
public sealed record GpuFrameCompletion(GpuFrameStamp Stamp, bool FenceCompleted);

/// <summary>One-frame protocol guard; invalid transitions cannot enqueue a foreign GPU wait.</summary>
public sealed class GpuFrameHandshake(GpuFrameOffer offer)
{
    private readonly Lock _gate = new();
    private string _phase = "Offered";
    /// <summary>Current immutable protocol phase.</summary>
    public string Phase { get { lock (_gate) return _phase; } }
    /// <summary>Confirms successful import before producer submission.</summary>
    public void Imported() => Move("Offered", "Imported", null);
    /// <summary>Requires an observed producer fence before GL wait/use.</summary>
    public void Ready(GpuFrameCompletion completion) => Move("Imported", "Ready", completion);
    /// <summary>Requires observed GL completion before producer return wait.</summary>
    public void Released(GpuFrameCompletion completion) => Move("Ready", "Released", completion);
    /// <summary>Requires observed Vulkan return fence before retirement.</summary>
    public void Retired(GpuFrameCompletion completion) => Move("Released", "Retired", completion);
    /// <summary>Permanently fences further phase changes after disconnect/cancel/unknown outcome.</summary>
    public void Fault() { lock (_gate) _phase = "Faulted"; }
    private void Move(string expected, string next, GpuFrameCompletion? completion)
    {
        lock (_gate)
        {
            if (_phase != expected) throw new InvalidOperationException("GPU phase " + _phase + "; expected " + expected);
            if (next != "Imported" && (completion == null || !completion.FenceCompleted || completion.Stamp != offer.Stamp))
                throw new InvalidDataException("GPU fence completion/identity was not confirmed.");
            _phase = next;
        }
    }
}
