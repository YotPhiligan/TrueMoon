using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuTransportProbeTests
{
    private static readonly SessionIdentity Identity = new(Guid.Parse("11111111-2222-3333-4444-555555555555"), 7, new('A', 64), new('B', 64), new('C', 64));
    private static readonly GpuAdapterId Adapter = new(new('D', 32), new('E', 32));
    private static GpuProducerDevice Device() => new(Adapter, "GPU", (1u << 22) | (1u << 12), true, true, true,
        new(0, true, false, false, true, 1920, 1080, 16 * 1024 * 1024, true), new(true, false, true));
    private static GpuOpenGlFeatures Features() => new(true, true, true, true, true, true, true, true);
    private static GpuProducerCapabilities Producer(GpuProducerDevice? device = null) => new(Identity, [device ?? Device()]);
    private static GpuConsumerCapabilities Consumer(GpuOpenGlFeatures? features = null) => new(Identity, [Adapter], features ?? Features());

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Eligible_CorrelatesActualDeviceAndDriverPreservesDedicatedAndDoesNotRequireVulkanImport(bool dedicated)
    {
        var other = Device() with { Adapter = Adapter with { DeviceUuid = new('F', 32) } };
        var selected = Device() with { Adapter = new(Adapter.DeviceUuid.ToLowerInvariant(), Adapter.DriverUuid.ToLowerInvariant()), Image = Device().Image with { DedicatedOnly = dedicated } };
        var decision = GpuTransportProbe.Evaluate(Identity, new(Identity, [other, selected]), Consumer());
        Assert.True(decision.Eligible); Assert.Equal(1, decision.ProducerIndex); Assert.Equal(dedicated, decision.DedicatedAllocation); Assert.Empty(decision.Issues);
        Assert.False(selected.Image.Importable); Assert.False(selected.Semaphore.Importable);
    }

    [Theory]
    [InlineData("identity-producer", "Identity")] [InlineData("identity-consumer", "Identity")]
    [InlineData("schema-producer", "Schema")] [InlineData("schema-consumer", "Schema")]
    [InlineData("profile-producer", "Profile")] [InlineData("profile-consumer", "Profile")]
    [InlineData("platform-producer", "Platform")] [InlineData("platform-consumer", "Platform")]
    [InlineData("bits-producer", "Platform")] [InlineData("bits-consumer", "Platform")]
    [InlineData("query-producer", "Query")] [InlineData("query-consumer", "Query")]
    [InlineData("empty-producer", "Devices")] [InlineData("empty-consumer", "Devices")]
    [InlineData("many-producer", "Devices")] [InlineData("many-consumer", "Devices")]
    public void InvalidPeerEvidence_RejectsBeforeMatchingAndKeepsExplicitReason(string failure, string code)
    {
        var producer = new GpuProducerCapabilities(failure == "identity-producer" ? Identity with { Generation = 6 } : Identity,
            failure == "empty-producer" ? [] : failure == "many-producer" ? Enumerable.Repeat(Device(), 17).ToArray() : [Device()],
            version: failure == "schema-producer" ? 2 : 1, profile: failure == "profile-producer" ? 2 : 1, windows: failure != "platform-producer",
            pointerBits: failure == "bits-producer" ? 32 : 64, error: failure == "query-producer" ? "native failure" : null);
        var consumer = new GpuConsumerCapabilities(failure == "identity-consumer" ? Identity with { Nonce = new('F', 64) } : Identity,
            failure == "empty-consumer" ? [] : failure == "many-consumer" ? [Adapter, Adapter] : [Adapter], Features(),
            version: failure == "schema-consumer" ? 0 : 1, profile: failure == "profile-consumer" ? 0 : 1, windows: failure != "platform-consumer",
            pointerBits: failure == "bits-consumer" ? 32 : 64, error: failure == "query-consumer" ? "native failure" : null);
        var result = GpuTransportProbe.Evaluate(Identity, producer, consumer);
        Assert.False(result.Eligible); Assert.Null(result.ProducerIndex); Assert.Equal(code, Assert.Single(result.Issues).Code);
        if (code == "Query") Assert.Equal("native failure", result.Issues[0].Detail);
    }

    [Theory]
    [InlineData("adapter", "Adapter")] [InlineData("driver", "Driver")] [InlineData("duplicate", "Adapter")]
    [InlineData("uuid-zero", "Uuid")] [InlineData("uuid-size", "Uuid")] [InlineData("uuid-hex", "Uuid")]
    [InlineData("api", "Vulkan")] [InlineData("variant", "Vulkan")] [InlineData("queue", "Queue")]
    [InlineData("memory-extension", "Win32")] [InlineData("semaphore-extension", "Win32")]
    [InlineData("image-query", "ImageQuery")] [InlineData("image-incomplete", "ImageQuery")] [InlineData("image-export", "Memory")] [InlineData("memory-compatible", "Memory")]
    [InlineData("semaphore-export", "Semaphore")] [InlineData("semaphore-compatible", "Semaphore")]
    [InlineData("samples", "Samples")] [InlineData("extent-x", "Extent")] [InlineData("extent-y", "Extent")]
    [InlineData("size", "ResourceSize")]
    public void NativeRequirementMissing_CannotBecomeEligible(string failure, string code)
    {
        var device = Device(); device = failure switch
        {
            "adapter" => device with { Adapter = Adapter with { DeviceUuid = new('F', 32) } },
            "driver" => device with { Adapter = Adapter with { DriverUuid = new('F', 32) } },
            "uuid-zero" => device with { Adapter = Adapter with { DriverUuid = new('0', 32) } },
            "uuid-size" => device with { Adapter = Adapter with { DeviceUuid = new('F', 31) } },
            "uuid-hex" => device with { Adapter = Adapter with { DeviceUuid = new('G', 32) } },
            "api" => device with { ApiVersion = 1u << 22 }, "queue" => device with { GraphicsQueue = false },
            "variant" => device with { ApiVersion = device.ApiVersion | (1u << 29) },
            "memory-extension" => device with { MemoryWin32 = false }, "semaphore-extension" => device with { SemaphoreWin32 = false },
            "image-query" => device with { Image = device.Image with { QueryResult = -11 } },
            "image-incomplete" => device with { Image = device.Image with { QueryResult = 5 } },
            "image-export" => device with { Image = device.Image with { Exportable = false } },
            "memory-compatible" => device with { Image = device.Image with { OpaqueWin32Compatible = false } },
            "semaphore-export" => device with { Semaphore = device.Semaphore with { Exportable = false } },
            "semaphore-compatible" => device with { Semaphore = device.Semaphore with { OpaqueWin32Compatible = false } },
            "samples" => device with { Image = device.Image with { SingleSample = false } },
            "extent-x" => device with { Image = device.Image with { MaxWidth = 1279 } },
            "extent-y" => device with { Image = device.Image with { MaxHeight = 719 } },
            "size" => device with { Image = device.Image with { MaxResourceSize = 1280 * 720 * 4 - 1 } }, _ => device
        };
        var result = GpuTransportProbe.Evaluate(Identity, new(Identity, failure == "duplicate" ? [device, device] : [device]), Consumer());
        Assert.False(result.Eligible); Assert.Null(result.ProducerIndex); Assert.Equal(code, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("memory")] [InlineData("memory-win32")] [InlineData("semaphore")] [InlineData("semaphore-win32")]
    [InlineData("procedures")] [InlineData("rgba")] [InlineData("tiling")] [InlineData("fence")]
    public void EachOpenGlRequirement_IsRequiredEvenWhenUuidMatches(string failure)
    {
        var features = Features(); features = failure switch
        {
            "memory" => features with { MemoryObjects = false }, "memory-win32" => features with { MemoryWin32 = false },
            "semaphore" => features with { Semaphores = false }, "semaphore-win32" => features with { SemaphoreWin32 = false },
            "procedures" => features with { RequiredProcedures = false }, "rgba" => features with { Rgba8 = false },
            "tiling" => features with { OptimalTiling = false }, _ => features with { FencePolling = false }
        };
        var result = GpuTransportProbe.Evaluate(Identity, Producer(), Consumer(features));
        Assert.False(result.Eligible); Assert.Equal("OpenGL", Assert.Single(result.Issues).Code); Assert.Null(result.ProducerIndex);
    }

    [Theory]
    [InlineData(16, 16, true)] [InlineData(1920, 1080, true)] [InlineData(1280, 720, true)]
    [InlineData(15, 16, false)] [InlineData(16, 15, false)] [InlineData(1921, 1080, false)] [InlineData(1920, 1081, false)]
    [InlineData(0, 16, false)] [InlineData(-1, 16, false)]
    public void Dimensions_UseExactViewportLimitsAndMinimumResourceSize(int width, int height, bool valid)
    {
        var device = Device(); if (valid) device = device with { Image = device.Image with { MaxWidth = (uint)width, MaxHeight = (uint)height, MaxResourceSize = (ulong)width * (ulong)height * 4 } };
        var result = GpuTransportProbe.Evaluate(Identity, Producer(device), Consumer(), width, height);
        Assert.Equal(valid, result.Eligible);
        if (valid) { Assert.Equal(0, result.ProducerIndex); Assert.Empty(result.Issues); }
        else { Assert.Null(result.ProducerIndex); Assert.Equal("Dimensions", Assert.Single(result.Issues).Code); }
    }

    [Fact]
    public void Snapshots_DoNotFollowCallerArrayMutationsAndReportAllGlBlockers()
    {
        var devices = new[] { Device() }; var ids = new[] { Adapter };
        var producer = new GpuProducerCapabilities(Identity, devices); var consumer = new GpuConsumerCapabilities(Identity, ids, Features());
        devices[0] = devices[0] with { GraphicsQueue = false }; ids[0] = new(new('F', 32), new('F', 32));
        Assert.True(GpuTransportProbe.Evaluate(Identity, producer, consumer).Eligible);
        var result = GpuTransportProbe.Evaluate(Identity, producer, Consumer(new(false, false, false, false, false, false, false, false)));
        Assert.False(result.Eligible); Assert.Equal(8, result.Issues.Count); Assert.All(result.Issues, i => Assert.Equal("OpenGL", i.Code));
        Assert.Equal(8, result.Issues.Select(i => i.Detail).Distinct().Count()); Assert.Null(result.ProducerIndex);
    }

    [Theory]
    [InlineData("image")] [InlineData("semaphore")] [InlineData("features")]
    public void IncompleteNativeEvidence_DoesNotBecomeEligibleOrThrow(string missing)
    {
        var device = Device(); if (missing == "image") device = device with { Image = null! }; if (missing == "semaphore") device = device with { Semaphore = null! };
        var result = GpuTransportProbe.Evaluate(Identity, Producer(device), missing == "features" ? new(Identity, [Adapter], null!) : Consumer());
        Assert.False(result.Eligible); Assert.Null(result.ProducerIndex); Assert.Equal("Query", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void LastAllowedProducerCountAndExactMaximumViewport_CanStillChooseTheLastAdapter()
    {
        var devices = Enumerable.Range(1, 15).Select(i => Device() with { Adapter = Adapter with { DeviceUuid = i.ToString("X32") } }).Append(Device()).ToArray();
        var result = GpuTransportProbe.Evaluate(Identity, new(Identity, devices), Consumer(), 1920, 1080);
        Assert.True(result.Eligible); Assert.Equal(15, result.ProducerIndex); Assert.Empty(result.Issues);
    }

    [Fact]
    public void InvalidConsumerUuid_IsReportedBeforeAdapterMatching()
    {
        var consumer = new GpuConsumerCapabilities(Identity, [Adapter with { DriverUuid = new('0', 32) }], Features());
        var result = GpuTransportProbe.Evaluate(Identity, Producer(), consumer);
        Assert.False(result.Eligible); Assert.Null(result.ProducerIndex); Assert.Equal("Uuid", Assert.Single(result.Issues).Code);
    }
}
