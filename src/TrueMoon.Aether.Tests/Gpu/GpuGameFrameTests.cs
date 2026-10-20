using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using Xunit;

namespace TrueMoon.Aether.Tests.Gpu;

public sealed class GpuGameFrameTests
{
    [Fact]
    public async Task RuntimeCapture_RequiresLoadedReadyGameAndRejectsStoppingGeneration()
    {
        var identity=Stamp().Identity; using var game=new Game.Logic.ProbeGame();var protocol=new TrueMoon.Aether.Runtime.RuntimeProtocol(identity,game,17);
        Assert.Throws<InvalidOperationException>(()=>protocol.CaptureFrame());
        await protocol.SendAsync(ControlRequest.Create(identity,1,CommandKind.Hello));
        Assert.Throws<InvalidOperationException>(()=>protocol.CaptureFrame());
        var load=ControlRequest.Create(identity,2,CommandKind.LoadScene);load.SceneId="room";load.Revision=7;load.Entities=[];await protocol.SendAsync(load);
        Assert.Equal((0ul,game.PatternColor),protocol.CaptureFrame());
        await protocol.SendAsync(ControlRequest.Create(identity,3,CommandKind.Step));Assert.Equal(1ul,protocol.CaptureFrame().Tick);
        await protocol.SendAsync(ControlRequest.Create(identity,4,CommandKind.Stop));Assert.Throws<InvalidOperationException>(()=>protocol.CaptureFrame());
    }
    [Fact]
    public void FrameNumber_RejectsInvalidStampAndOverflowBeforeAllocation()
    {
        Assert.Throws<ArgumentNullException>(() => GpuFrameData.FrameNumber(null!));
        Assert.Throws<InvalidDataException>(() => GpuFrameData.FrameNumber(Stamp() with { Version=1 }));
        Assert.Throws<InvalidDataException>(() => GpuFrameData.FrameNumber(Stamp() with { FrameId=0 }));
        Assert.Throws<InvalidDataException>(() => GpuFrameData.FrameNumber(Stamp() with { Slot=3 }));
        Assert.Throws<OverflowException>(() => GpuFrameData.FrameNumber(Stamp(frame:ulong.MaxValue)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GpuFrameData.GamePattern(Stamp(),0,0,0,0));
    }
    private static GpuFrameStamp Stamp(int slot = 0, ulong frame = 1) => new(new(Guid.NewGuid(), 2, new('A', 64), new('B', 64), new('C', 64)), 7, frame, slot, 2);
    [Theory]
    [InlineData(0, 1ul, 1ul)] [InlineData(1, 1ul, 2ul)] [InlineData(2, 1ul, 3ul)] [InlineData(0, 2ul, 4ul)] [InlineData(2, 5ul, 15ul)]
    public void Pattern_PreservesFrameTickSelectedColorRgbaAndOrientation(int slot, ulong use, ulong frame)
    {
        var stamp = Stamp(slot, use); var data = GpuFrameData.GamePattern(stamp, 80, 32, 37, 0xffc06020); data.Validate(stamp, 80, 32);
        Assert.Equal(slot, data.Metadata.Slot); Assert.Equal(frame, data.Metadata.FrameId); Assert.Equal(37ul, data.Metadata.Tick); Assert.True(data.Metadata.ProducedTimestamp > 0);
        byte[] Pixel(int x, int y) => data.Pixels[((y * 80 + x) * 4)..((y * 80 + x) * 4 + 4)];
        Assert.Equal(new byte[] {255,0,0,255}, Pixel(0,0)); Assert.Equal(new byte[] {0,255,0,255}, Pixel(79,0));
        Assert.Equal(new byte[] {0,0,255,255}, Pixel(0,31)); Assert.Equal(new byte[] {255,255,255,255}, Pixel(79,31));
        Assert.Equal((frame & 1) == 1 ? new byte[] {255,255,255,255} : [0,0,0,255], Pixel(0,7));
        Assert.Equal(new byte[] {255,255,255,255}, Pixel(0,24)); Assert.Equal(new byte[] {0,0,0,255}, Pixel(1,24));
        var block = 1 + 1 + (int)(frame % 32); var expected = (0xffc06020u + (uint)(block * 0x030507)) & 0xffffff;
        Assert.Equal(new byte[] {(byte)(expected>>16),(byte)(expected>>8),(byte)expected,255},Pixel(20,16));
    }
    [Theory]
    [InlineData("shape")] [InlineData("metadata")] [InlineData("slot")] [InlineData("frame")] [InlineData("time")]
    public void FrameData_RejectsMismatchBeforeUpload(string invalid)
    {
        var stamp = Stamp(); var good = GpuFrameData.GamePattern(stamp, 16, 16, 0, 0xff2080c0);
        var bad = invalid switch { "shape" => good with { Pixels = new byte[1023] }, "metadata" => good with { Metadata = null! },
            "slot" => good with { Metadata = good.Metadata with { Slot = 1 } }, "frame" => good with { Metadata = good.Metadata with { FrameId = 2 } }, _ => good with { Metadata = good.Metadata with { ProducedTimestamp = 0 } } };
        Assert.Throws<InvalidDataException>(() => bad.Validate(stamp,16,16)); good.Validate(stamp,16,16);
    }
}
