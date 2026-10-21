using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Gpu;

/// <summary>A named blocker; codes are stable within capability schema one.</summary>
public sealed record GpuProbeIssue(string Code, string Detail);

/// <summary>Eligibility for an import experiment. This result never enables or allocates a GPU transport.</summary>
public sealed class GpuProbeDecision
{
    internal GpuProbeDecision(int? producerIndex, bool dedicated, IEnumerable<GpuProbeIssue> issues)
    { ProducerIndex = producerIndex; DedicatedAllocation = dedicated; Issues = Array.AsReadOnly(issues.ToArray()); }
    /// <summary>Whether the next, explicitly requested import probe is allowed by these capabilities.</summary>
    public bool Eligible => Issues.Count == 0 && ProducerIndex != null;
    /// <summary>Matching native enumeration index; absent when evidence is insufficient.</summary>
    public int? ProducerIndex { get; }
    /// <summary>Whether Vulkan requires a dedicated image allocation, to be honored by the import probe.</summary>
    public bool DedicatedAllocation { get; }
    /// <summary>All observed blockers. A blocked result leaves the existing CPU transport selected.</summary>
    public IReadOnlyList<GpuProbeIssue> Issues { get; }
}

/// <summary>Pure, conservative Windows x64 Vulkan/OpenGL profile-one capability validation.</summary>
/// <remarks>No native resources or lifecycle: callers collect evidence through their App-owned runtime/UI.
/// Profile one queries Vulkan1.1 RGBA8_UNORM/2D/optimal/one sample, usage mask23
/// (transfer source/destination, sampled and color attachment), opaque Win32 memory/binary semaphores.</remarks>
public static class GpuTransportProbe
{
    /// <summary>Validates identity/schema/profile before correlating physical-device/driver UUIDs and native requirements.</summary>
    public static GpuProbeDecision Evaluate(SessionIdentity expected, GpuProducerCapabilities producer,
        GpuConsumerCapabilities consumer, int width = 1280, int height = 720)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(producer); ArgumentNullException.ThrowIfNull(consumer);
        expected.Validate(); var issues = new List<GpuProbeIssue>();
        void Require(bool valid, string code, string detail) { if (!valid) issues.Add(new(code, detail)); }
        Require(producer.Identity == expected && consumer.Identity == expected, "Identity", "Full capability session identity must match the current runtime.");
        Require(producer.Version == 1 && consumer.Version == 1, "Schema", "Capability schema must be one on both peers.");
        Require(producer.Profile == 1 && consumer.Profile == 1, "Profile", "Both peers must query the exact profile-one image/handle/semaphore tuple.");
        Require(producer.Windows && consumer.Windows && producer.PointerBits == 64 && consumer.PointerBits == 64, "Platform", "Only Windows x64 is probed.");
        Require(width is >= 16 and <= 1920 && height is >= 16 and <= 1080, "Dimensions", "Physical dimensions are outside the agreed viewport range.");
        Require(producer.Error == null && consumer.Error == null, "Query", producer.Error ?? consumer.Error ?? "Native query failed.");
        Require(producer.Devices.Count is >= 1 and <= 16 && consumer.Devices.Count == 1, "Devices", "Require 1..16 producer devices and exactly one GL context device.");
        if (issues.Count != 0) return new(null, false, issues);
        var gl = consumer.Devices[0];
        if (!ValidId(gl) || producer.Devices.Any(d => d == null || !ValidId(d.Adapter)))
            return new(null, false, [new("Uuid", "Device/driver UUIDs must be nonzero 16-byte hex values.")]);
        var matches = producer.Devices.Select((device, index) => (device, index))
            .Where(x => Same(x.device.Adapter.DeviceUuid, gl.DeviceUuid)).ToArray();
        if (matches.Length != 1) return new(null, false, [new("Adapter", "Require exactly one Vulkan device matching the actual GL device UUID.")]);
        var (device, index) = matches[0];
        Require(Same(device.Adapter.DriverUuid, gl.DriverUuid), "Driver", "Vulkan and GL driver UUIDs differ.");
        Require(device.ApiVersion >> 29 == 0 && device.ApiVersion >= ((1u << 22) | (1u << 12)), "Vulkan", "Vulkan1.1 physical-device capabilities with API variant zero are required.");
        Require(device.GraphicsQueue, "Queue", "No graphics queue.");
        Require(device.MemoryWin32 && device.SemaphoreWin32, "Win32", "Require both external-memory-win32 and external-semaphore-win32 device extensions.");
        if (device.Image is not { } image || device.Semaphore is not { } semaphore || consumer.Features is not { } features)
            return new(null, false, [.. issues, new("Query", "Missing image/semaphore/GL features.")]);
        Require(image.QueryResult == 0, "ImageQuery", "Exact external image query failed: " + image.QueryResult);
        Require(image.Exportable && image.OpaqueWin32Compatible, "Memory", "OPAQUE_WIN32 memory export is unavailable.");
        Require(semaphore.Exportable && semaphore.OpaqueWin32Compatible, "Semaphore", "OPAQUE_WIN32 binary semaphore export is unavailable.");
        Require(image.SingleSample, "Samples", "The queried image does not support one sample.");
        Require(image.MaxWidth >= width && image.MaxHeight >= height, "Extent", "The queried image extent is too small.");
        Require(image.MaxResourceSize >= checked((ulong)width * (ulong)height * 4), "ResourceSize", "The queried maximum resource size cannot hold even tight RGBA8 pixels.");
        foreach (var (supported, name) in new[] { (features.MemoryObjects, "memory_object"), (features.MemoryWin32, "memory_object_win32"),
                     (features.Semaphores, "semaphore"), (features.SemaphoreWin32, "semaphore_win32"), (features.RequiredProcedures, "entry points"),
                     (features.Rgba8, "RGBA8 internal format"), (features.OptimalTiling, "optimal tiling"), (features.FencePolling, "fence polling") })
            Require(supported, "OpenGL", "GL consumer is missing " + name + ".");
        return new(issues.Count == 0 ? index : null, image.DedicatedOnly, issues);
    }
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool ValidId(GpuAdapterId? id) => id != null && Hex(id.DeviceUuid) && Hex(id.DriverUuid);
    private static bool Hex(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit) && value.Any(c => c != '0');
}
