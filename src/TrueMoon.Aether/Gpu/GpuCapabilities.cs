using TrueMoon.Aether.Contracts;

namespace TrueMoon.Aether.Gpu;

/// <summary>Cross-API identifiers, encoded as 16 bytes of hexadecimal in native byte order.</summary>
public sealed record GpuAdapterId(string DeviceUuid, string DriverUuid);

/// <summary>One exact RGBA8 UNORM, optimal 2D, single-sample color/sampled/transfer image query.</summary>
public sealed record GpuImageCapabilities(int QueryResult, bool Exportable, bool Importable, bool DedicatedOnly,
    bool OpaqueWin32Compatible, uint MaxWidth, uint MaxHeight, ulong MaxResourceSize, bool SingleSample);

/// <summary>Binary OPAQUE_WIN32 semaphore capability query; import is performed by OpenGL.</summary>
public sealed record GpuSemaphoreCapabilities(bool Exportable, bool Importable, bool OpaqueWin32Compatible);

/// <summary>Physical-device capabilities without logical devices, allocations or exported handles.</summary>
public sealed record GpuProducerDevice(GpuAdapterId Adapter, string Name, uint ApiVersion, bool GraphicsQueue,
    bool MemoryWin32, bool SemaphoreWin32, GpuImageCapabilities Image, GpuSemaphoreCapabilities Semaphore);

/// <summary>Required consumer extensions, query results and callable procedure set on the actual current GL context.</summary>
public sealed record GpuOpenGlFeatures(bool MemoryObjects, bool MemoryWin32, bool Semaphores, bool SemaphoreWin32,
    bool RequiredProcedures, bool Rgba8, bool OptimalTiling, bool FencePolling);

/// <summary>Immutable producer report bound to one full session identity and exact query profile.</summary>
public sealed class GpuProducerCapabilities
{
    /// <summary>Snapshots the adapter list; Error indicates unavailable native evidence.</summary>
    public GpuProducerCapabilities(SessionIdentity identity, IReadOnlyList<GpuProducerDevice> devices,
        int version = 1, int profile = 1, bool windows = true, int pointerBits = 64, string? error = null)
    {
        Identity = identity; Devices = Array.AsReadOnly(devices.ToArray()); Version = version; Profile = profile;
        Windows = windows; PointerBits = pointerBits; Error = error;
    }
    /// <summary>Originating runtime.</summary>
    public SessionIdentity Identity { get; }
    /// <summary>Physical devices in native enumeration order.</summary>
    public IReadOnlyList<GpuProducerDevice> Devices { get; }
    /// <summary>Capability schema.</summary>
    public int Version { get; }
    /// <summary>Exact query profile; version one is documented by GpuTransportProbe.</summary>
    public int Profile { get; }
    /// <summary>Whether the native producer runs on Windows.</summary>
    public bool Windows { get; }
    /// <summary>Native pointer width.</summary>
    public int PointerBits { get; }
    /// <summary>Native evidence failure; it must not become an eligible result.</summary>
    public string? Error { get; }
}

/// <summary>Immutable consumer evidence from one current GL context, bound to the same session.</summary>
public sealed class GpuConsumerCapabilities
{
    /// <summary>Snapshots all context adapter identities; multi-device contexts are outside profile one.</summary>
    public GpuConsumerCapabilities(SessionIdentity identity, IReadOnlyList<GpuAdapterId> devices, GpuOpenGlFeatures features,
        int version = 1, int profile = 1, bool windows = true, int pointerBits = 64, string? error = null)
    {
        Identity = identity; Devices = Array.AsReadOnly(devices.ToArray()); Features = features; Version = version; Profile = profile;
        Windows = windows; PointerBits = pointerBits; Error = error;
    }
    /// <summary>Target runtime identity.</summary>
    public SessionIdentity Identity { get; }
    /// <summary>Devices used by this GL context.</summary>
    public IReadOnlyList<GpuAdapterId> Devices { get; }
    /// <summary>Exact consumer requirements.</summary>
    public GpuOpenGlFeatures Features { get; }
    /// <summary>Capability schema.</summary>
    public int Version { get; }
    /// <summary>Exact query profile.</summary>
    public int Profile { get; }
    /// <summary>Whether the consumer runs on Windows.</summary>
    public bool Windows { get; }
    /// <summary>Native pointer width.</summary>
    public int PointerBits { get; }
    /// <summary>Query failure; incomplete native evidence is never sufficient.</summary>
    public string? Error { get; }
}
