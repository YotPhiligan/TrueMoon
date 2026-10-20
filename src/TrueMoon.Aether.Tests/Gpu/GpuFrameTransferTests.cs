using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Hosting;
using TrueMoon.Dependencies;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuFrameTransferTests
{
    private static SessionIdentity Identity() => new(Guid.Parse("11111111-2222-3333-4444-555555555555"), 3, new('A', 64), new('B', 64), new('C', 64));
    private static GpuAdapterId Adapter() => new(new('D', 32), new('E', 32));
    private static GpuFrameOffer Offer() => new(new(Identity()), Adapter(), 32, 16, 4096, 4, 8, 12);
    private static GpuFrameCompletion Completed() => new(Offer().Stamp, true);

    [Theory]
    [InlineData("identity")] [InlineData("surface")] [InlineData("frame")] [InlineData("slot")] [InlineData("schema")]
    [InlineData("adapter")] [InlineData("width-low")] [InlineData("width-high")] [InlineData("height-low")] [InlineData("height-high")]
    [InlineData("allocation-low")] [InlineData("allocation-high")] [InlineData("offset")] [InlineData("dedicated")]
    [InlineData("memory")] [InlineData("ready")] [InlineData("return")] [InlineData("pseudo")] [InlineData("duplicate")]
    [InlineData("format")] [InlineData("color")] [InlineData("alpha")] [InlineData("origin")] [InlineData("missing-stamp")]
    public void Offer_InvalidIdentityShapeOrHandleProfileIsRejectedBeforeNativeUse(string invalid)
    {
        var original = Offer(); var offer = invalid switch
        {
            "identity" => original with { Stamp = original.Stamp with { Identity = Identity() with { Generation = 2 } } },
            "surface" => original with { Stamp = original.Stamp with { SurfaceGeneration = 0 } },
            "frame" => original with { Stamp = original.Stamp with { FrameId = 2 } },
            "slot" => original with { Stamp = original.Stamp with { Slot = 1 } },
            "schema" => original with { Stamp = original.Stamp with { Version = 2 } },
            "adapter" => original with { Adapter = Adapter() with { DriverUuid = new('F', 32) } },
            "width-low" => original with { Width = 15 }, "width-high" => original with { Width = 1921 },
            "height-low" => original with { Height = 15 }, "height-high" => original with { Height = 1081 },
            "allocation-low" => original with { AllocationBytes = 2047 }, "allocation-high" => original with { AllocationBytes = 64 * 1024 * 1024 + 1 },
            "offset" => original with { Offset = 1 }, "dedicated" => original with { Dedicated = false },
            "memory" => original with { MemoryHandle = 0 }, "ready" => original with { ReadyHandle = 0 }, "return" => original with { ReturnHandle = 0 },
            "pseudo" => original with { MemoryHandle = ulong.MaxValue }, "duplicate" => original with { ReturnHandle = original.ReadyHandle },
            "format" => original with { Format = "BGRA8" }, "color" => original with { ColorSpace = "Linear" },
            "alpha" => original with { Alpha = "Straight" }, "origin" => original with { Origin = "BottomLeft" }, _ => original with { Stamp = null! }
        };
        Assert.Throws<InvalidDataException>(() => offer.Validate(Identity(), Adapter()));
        original.Validate(Identity(), Adapter()); Assert.Equal(4096ul, original.AllocationBytes);
    }

    [Theory]
    [InlineData(16, 16, 1024ul)] [InlineData(1920, 1080, 8294400ul)] [InlineData(32, 16, 67108864ul)]
    public void Offer_ExactSupportedBoundsAreAcceptedWithoutAlteringSourceHandleTokens(int width, int height, ulong bytes)
    {
        var offer = Offer() with { Width = width, Height = height, AllocationBytes = bytes };
        offer.Validate(Identity(), Adapter()); Assert.Equal(4ul, offer.MemoryHandle); Assert.Equal(8ul, offer.ReadyHandle); Assert.Equal(12ul, offer.ReturnHandle);
    }

    [Fact]
    public void Handshake_RequiresImportAndBothObservedFencesBeforeRetirement()
    {
        var protocol = new GpuFrameHandshake(Offer()); Assert.Equal("Offered", protocol.Phase);
        Assert.Throws<InvalidOperationException>(() => protocol.Ready(Completed())); Assert.Equal("Offered", protocol.Phase);
        protocol.Imported(); Assert.Equal("Imported", protocol.Phase);
        protocol.Ready(Completed()); Assert.Equal("Ready", protocol.Phase);
        protocol.Released(Completed()); Assert.Equal("Released", protocol.Phase);
        protocol.Retired(Completed()); Assert.Equal("Retired", protocol.Phase);
        Assert.Throws<InvalidOperationException>(() => protocol.Imported()); Assert.Throws<InvalidOperationException>(() => protocol.Ready(Completed()));
    }

    [Theory]
    [InlineData("ready", "false")] [InlineData("ready", "stale")] [InlineData("ready", "null")]
    [InlineData("release", "false")] [InlineData("release", "stale")] [InlineData("release", "null")]
    [InlineData("retire", "false")] [InlineData("retire", "stale")] [InlineData("retire", "null")]
    public void Handshake_UnconfirmedOrStaleCompletionCannotAdvanceOrAuthorizeGpuWait(string phase, string invalid)
    {
        var protocol = new GpuFrameHandshake(Offer()); protocol.Imported(); if (phase != "ready") protocol.Ready(Completed()); if (phase == "retire") protocol.Released(Completed());
        var previous = protocol.Phase; var completion = invalid == "null" ? null! : invalid == "false" ? Completed() with { FenceCompleted = false } : Completed() with { Stamp = Offer().Stamp with { SurfaceGeneration = 2 } };
        Assert.Throws<InvalidDataException>(() => { if (phase == "ready") protocol.Ready(completion); else if (phase == "release") protocol.Released(completion); else protocol.Retired(completion); });
        Assert.Equal(previous, protocol.Phase); protocol.Fault(); Assert.Equal("Faulted", protocol.Phase);
        Assert.Throws<InvalidOperationException>(() => protocol.Ready(Completed())); Assert.Throws<InvalidOperationException>(() => protocol.Released(Completed()));
    }

    [Fact]
    public void ReferencePattern_HasIndependentChannelAlphaAndOrientationMarkers()
    {
        var pixels = GpuReferencePattern.Create(16, 16); Assert.Equal(1024, pixels.Length);
        Assert.Equal(new byte[] { 17, 31, 73, 255 }, pixels[..4]);
        Assert.Equal(new byte[] { 37, 20, 89, 128 }, pixels[32..36]);
        Assert.Equal(new byte[] { 31, 98, 44, 128 }, pixels[(15 * 16 * 4)..(15 * 16 * 4 + 4)]);
        for (var i = 0; i < pixels.Length; i += 4) { Assert.True(pixels[i] <= pixels[i + 3]); Assert.True(pixels[i + 1] <= pixels[i + 3]); Assert.True(pixels[i + 2] <= pixels[i + 3]); }
    }

    [Fact]
    public async Task AppGpuOwner_StartupIsLazyAndCleanupFailuresRetainOwnershipForExplicitRetry()
    {
        await using var app = App.Build(c => c.UseAether(o => { o.HostContractId = new('A', 64); o.GameBuildId = new('B', 64); o.ReportDirectory = "gpu-owner-tests"; }));
        await app.StartAsync(); var host = app.Services.Resolve<AetherHost>()!; var gpu = host.Gpu;
        Assert.Equal(0, gpu.ResourceCount); Assert.Equal(0, gpu.OpenHandleCount);
        var trace = new List<int>(); var first = gpu.Own(new Resource(1, trace) { Fail = true }); var second = gpu.Own(new Resource(2, trace));
        Assert.Throws<InvalidOperationException>(() => gpu.Own(first));
        var error = await Assert.ThrowsAsync<IOException>(() => gpu.DisposeAsync().AsTask());
        Assert.Same(error, gpu.CleanupError); Assert.Equal(new[] { 2, 1 }, trace); Assert.True(second.IsReleased); Assert.False(first.IsReleased); Assert.Equal(1, gpu.ResourceCount);
        Assert.Throws<ObjectDisposedException>(() => gpu.Own(new Resource(3, trace)));
        first.Fail = false; await gpu.DisposeAsync(); Assert.True(first.IsReleased); Assert.Equal(0, gpu.ResourceCount); Assert.Null(gpu.CleanupError);
        await app.StopAsync(); Assert.Equal(new[] { 2, 1, 1 }, trace);
    }

    [Fact]
    public async Task GpuOwner_ConcurrentDisposeSharesCompletionAndDrainsAcceptedCleanup()
    {
        var gpu = new GpuResourceHost(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resource = gpu.Own(new Resource(1, []) { Before = async () => { entered.TrySetResult(); await release.Task; } });
        var first = gpu.DisposeAsync().AsTask(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try { Assert.Same(first, gpu.DisposeAsync().AsTask()); Assert.False(first.IsCompleted); Assert.Equal(1, gpu.ResourceCount); }
        finally { release.TrySetResult(); }
        await first; Assert.True(resource.IsReleased); Assert.Equal(0, gpu.ResourceCount);
    }
    private sealed class Resource(int id, List<int> trace) : IGpuResource
    {
        internal bool Fail; internal Func<Task>? Before;
        public bool IsReleased { get; private set; }
        public async ValueTask DisposeAsync()
        { if (IsReleased) return; trace.Add(id); if (Before != null) await Before(); if (Fail) throw new IOException("injected GPU cleanup failure"); IsReleased = true; }
    }
}
