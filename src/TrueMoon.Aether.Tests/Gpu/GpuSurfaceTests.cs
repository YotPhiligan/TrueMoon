using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuSurfaceTests
{
    private static SessionIdentity Identity() => new(Guid.Parse("11111111-2222-3333-4444-555555555555"), 4, new('A', 64), new('B', 64), new('C', 64));
    private static GpuAdapterId Adapter() => new(new('D', 32), new('E', 32));
    private static GpuFrameOffer[] Offers() => Enumerable.Range(0, 3).Select(i => new GpuFrameOffer(new(Identity(), 7, 1, i, 2), Adapter(), 32, 16, 4096, (ulong)(4 + i * 12), (ulong)(8 + i * 12), (ulong)(12 + i * 12))).ToArray();

    [Fact]
    public void ThreeSlots_ExhaustionAndOutOfOrderRetirementNeverOverwriteBusyImage()
    {
        var ledger = new GpuSlotLedger(Identity(), 7);
        Assert.Throws<InvalidOperationException>(() => ledger.Begin(0)); ledger.Imported();
        var stamps = Enumerable.Range(0, 3).Select(ledger.Begin).ToArray();
        Assert.Equal(new[] { 0, 1, 2 }, stamps.Select(s => s.Slot)); Assert.All(stamps, s => { Assert.Equal(1ul, s.FrameId); Assert.Equal(7ul, s.SurfaceGeneration); Assert.Equal(2, s.Version); });
        foreach (var stamp in stamps) { ledger.Produced(new(stamp, true)); Assert.Throws<InvalidOperationException>(() => ledger.Begin(stamp.Slot)); }
        Assert.False(ledger.IsIdle);
        foreach (var slot in new[] { 2, 0, 1 })
        {
            var done = new GpuFrameCompletion(stamps[slot], true); ledger.BeginRead(done); ledger.ReadCompleted(done); ledger.Return(done);
            Assert.Throws<InvalidOperationException>(() => ledger.Begin(slot)); ledger.Retired(done);
            var next = ledger.Begin(slot); Assert.Equal(stamps[slot] with { FrameId = 2 }, next); Assert.Equal("Producing", ledger.Slots[slot].Phase);
        }
        Assert.All(ledger.Slots, s => Assert.Equal(2ul, s.Stamp!.FrameId));
    }

    [Theory]
    [InlineData("Produced", "false")] [InlineData("Produced", "frame")] [InlineData("Produced", "surface")] [InlineData("Produced", "identity")] [InlineData("Produced", "slot")] [InlineData("Produced", "version")] [InlineData("Produced", "null")]
    [InlineData("BeginRead", "false")] [InlineData("BeginRead", "frame")] [InlineData("BeginRead", "surface")] [InlineData("BeginRead", "identity")] [InlineData("BeginRead", "slot")] [InlineData("BeginRead", "version")] [InlineData("BeginRead", "null")]
    [InlineData("ReadCompleted", "false")] [InlineData("ReadCompleted", "frame")] [InlineData("ReadCompleted", "surface")] [InlineData("ReadCompleted", "identity")] [InlineData("ReadCompleted", "slot")] [InlineData("ReadCompleted", "version")] [InlineData("ReadCompleted", "null")]
    [InlineData("Return", "false")] [InlineData("Return", "frame")] [InlineData("Return", "surface")] [InlineData("Return", "identity")] [InlineData("Return", "slot")] [InlineData("Return", "version")] [InlineData("Return", "null")]
    [InlineData("Retired", "false")] [InlineData("Retired", "frame")] [InlineData("Retired", "surface")] [InlineData("Retired", "identity")] [InlineData("Retired", "slot")] [InlineData("Retired", "version")] [InlineData("Retired", "null")]
    [InlineData("RemoteReturn", "false")] [InlineData("RemoteReturn", "frame")] [InlineData("RemoteReturn", "surface")] [InlineData("RemoteReturn", "identity")] [InlineData("RemoteReturn", "slot")] [InlineData("RemoteReturn", "version")] [InlineData("RemoteReturn", "null")]
    public void CompletionGuards_RejectEveryStaleDimensionWithoutAdvancingPhase(string operation, string invalid)
    {
        var ledger = new GpuSlotLedger(Identity(), 7); ledger.Imported(); var stamp = ledger.Begin(1); var completion = new GpuFrameCompletion(stamp, true);
        if (operation != "Produced") ledger.Produced(completion);
        if (operation is "ReadCompleted" or "Return" or "Retired") ledger.BeginRead(completion);
        if (operation is "Return" or "Retired") ledger.ReadCompleted(completion);
        if (operation == "Retired") ledger.Return(completion);
        Action<GpuFrameCompletion> action = operation switch { "Produced" => ledger.Produced, "BeginRead" => ledger.BeginRead, "ReadCompleted" => ledger.ReadCompleted, "Return" => ledger.Return, "Retired" => ledger.Retired, _ => ledger.RemoteReturn };
        var bad = invalid switch
        {
            "false" => completion with { FenceCompleted = false }, "frame" => completion with { Stamp = stamp with { FrameId = 2 } },
            "surface" => completion with { Stamp = stamp with { SurfaceGeneration = 8 } }, "identity" => completion with { Stamp = stamp with { Identity = Identity() with { Generation = 5 } } },
            "slot" => completion with { Stamp = stamp with { Slot = 3 } }, "version" => completion with { Stamp = stamp with { Version = 1 } }, _ => null!
        };
        var before = ledger.Slots.ToArray(); Assert.Throws<InvalidDataException>(() => action(bad)); Assert.Equal(before, ledger.Slots);
        action(completion); Assert.NotEqual(before[1].Phase, ledger.Slots[1].Phase);
    }

    [Fact]
    public void SlotGenerations_RejectPreviousUseAfterReuseAndFaultIsTerminal()
    {
        var ledger = new GpuSlotLedger(Identity(), 7); ledger.Imported(); var first = new GpuFrameCompletion(ledger.Begin(0), true);
        ledger.Produced(first); ledger.RemoteReturn(first); ledger.Retired(first); Assert.True(ledger.IsIdle);
        var snapshot = ledger.Slots; var second = new GpuFrameCompletion(ledger.Begin(0), true); Assert.Equal(2ul, second.Stamp.FrameId);
        Assert.Throws<InvalidDataException>(() => ledger.Produced(first)); ledger.Produced(second);
        Assert.Equal("Idle", snapshot[0].Phase); Assert.Equal(1ul, snapshot[0].Stamp!.FrameId);
        ledger.Fault(); Assert.All(ledger.Slots, s => Assert.Equal("Faulted", s.Phase));
        Assert.Throws<InvalidOperationException>(() => ledger.Begin(2)); Assert.Throws<InvalidOperationException>(() => ledger.Imported());
        Assert.Throws<InvalidOperationException>(() => ledger.BeginRead(second)); Assert.False(ledger.IsIdle);
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("generation")] [InlineData("slot")] [InlineData("version")] [InlineData("frame")] [InlineData("width")] [InlineData("height")] [InlineData("adapter")] [InlineData("identity")] [InlineData("alias")] [InlineData("profile")]
    public void SurfaceOffer_ValidatesEntireAllocationSetBeforeImport(string invalid)
    {
        var slots = Offers(); var generation = 7ul;
        if (invalid == "missing") slots = slots[..2];
        else if (invalid == "extra") slots = [.. slots, slots[0]];
        else if (invalid == "generation") generation = 8;
        else slots[2] = invalid switch
        {
            "slot" => slots[2] with { Stamp = slots[2].Stamp with { Slot = 1 } }, "version" => slots[2] with { Stamp = slots[2].Stamp with { Version = 1 } },
            "frame" => slots[2] with { Stamp = slots[2].Stamp with { FrameId = 2 } }, "width" => slots[2] with { Width = 16 }, "height" => slots[2] with { Height = 32 },
            "adapter" => slots[2] with { Adapter = Adapter() with { DriverUuid = new('F', 32) } },
            "identity" => slots[2] with { Stamp = slots[2].Stamp with { Identity = Identity() with { Generation = 5 } } },
            "alias" => slots[2] with { MemoryHandle = slots[0].MemoryHandle }, _ => slots[2] with { Origin = "BottomLeft" }
        };
        Assert.Throws<InvalidDataException>(() => new GpuSurfaceOffer(generation, slots).Validate(Identity(), Adapter(), 7, 32, 16));
    }
    [Fact]
    public void SurfaceOffer_SnapshotsSourceAndOneFrameV1StillRejectsVersionTwo()
    {
        var slots = Offers(); var surface = new GpuSurfaceOffer(7, slots); slots[0] = slots[0] with { MemoryHandle = 0 };
        surface.Validate(Identity(), Adapter(), 7, 32, 16); Assert.Equal(4ul, surface.Slots[0].MemoryHandle);
        Assert.Throws<InvalidDataException>(() => surface.Slots[0].Validate(Identity(), Adapter()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GpuSlotLedger(Identity(), 0));
        var ledger = new GpuSlotLedger(Identity(), 7); ledger.Imported();
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Begin(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Begin(3)); Assert.True(ledger.IsIdle);
    }
    [Theory]
    [InlineData(0, 16)] [InlineData(16, 0)] [InlineData(-1, 0)] [InlineData(15, 16)] [InlineData(16, 15)] [InlineData(1921, 1080)] [InlineData(1920, 1081)]
    public void Extent_PartialZeroAndUnsupportedBoundsAreRejected(int width, int height) => Assert.Throws<ArgumentOutOfRangeException>(() => GpuSurfaceOffer.ValidateExtent(width, height));
    [Fact]
    public void ReferencePattern_IndependentFrameSlotAndSurfaceMarkersPreventStaleFrameAcceptance()
    {
        GpuSurfaceOffer.ValidateExtent(0, 0); GpuSurfaceOffer.ValidateExtent(16, 16); GpuSurfaceOffer.ValidateExtent(1920, 1080);
        var stamp = new GpuFrameStamp(Identity(), 1, 1, 0, 2); var first = GpuReferencePattern.Create(16, 16, stamp);
        Assert.Equal(new byte[] { 29, 67, 157, 255 }, first[..4]);
        Assert.Equal(new byte[] { 46, 118, 20, 255 }, GpuReferencePattern.Create(16, 16, stamp with { FrameId = 2 })[..4]);
        Assert.Equal(new byte[] { 58, 154, 104, 255 }, GpuReferencePattern.Create(16, 16, stamp with { Slot = 1 })[..4]);
        Assert.Equal(new byte[] { 40, 100, 234, 255 }, GpuReferencePattern.Create(16, 16, stamp with { SurfaceGeneration = 2 })[..4]);
        for (var i = 0; i < first.Length; i += 4) { Assert.True(first[i] <= first[i + 3]); Assert.True(first[i + 1] <= first[i + 3]); Assert.True(first[i + 2] <= first[i + 3]); }
    }
}
