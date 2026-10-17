using System.Buffers.Binary;
using System.Buffers;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using Engine.Probe.Contracts;
using TrueMoon.Generated.EngineSessionProbe;
using Xunit;

namespace EngineSessionProbe.Tests;

[SupportedOSPlatform("windows")]
public sealed class FrameTransportTests
{
    private static SessionIdentity Identity() => new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64));
    private static FrameSurface Surface(int width = 32, int height = 16) => FrameSurface.Create(Identity(), 1, width, height);
    private static byte[] Header(FrameSurface surface) { var h = new byte[1024]; FrameCodec.Initialize(h, surface); return h; }
    private static void Edit(FrameSurface surface, Action<byte[]> edit)
    {
        using var map = MemoryMappedFile.OpenExisting(surface.Mapping);
        using var view = map.CreateViewAccessor(); var header = new byte[1024]; view.ReadArray(0, header, 0, 1024);
        edit(header); view.WriteArray(0, header, 0, 1024);
    }

    [Fact]
    public void ConfigureSurface_GeneratedWirePreservesUnsignedGenerationAndDescriptor()
    {
        var surface = FrameSurface.Create(Identity(), ulong.MaxValue, 1920, 1080);
        var request = ControlRequest.Create(surface.Identity, 9, CommandKind.ConfigureSurface); request.Surface = surface.ToWire();
        var writer = new ArrayBufferWriter<byte>(); request.Serialize(writer); var offset = 0;
        var decoded = new ControlRequest().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(writer.WrittenCount, offset); Assert.Equal(surface, decoded.Surface!.Describe(decoded.Identity()));
        Assert.True(writer.WrittenCount < 64 * 1024);
        var reply = new ControlReply { Stamp = request, SurfaceGeneration = -1, DroppedFrames = 123 };
        writer.Clear(); reply.Serialize(writer); offset = 0;
        var ack = new ControlReply().Deserialize(writer.WrittenSpan, ref offset);
        Assert.Equal(ulong.MaxValue, unchecked((ulong)ack.SurfaceGeneration)); Assert.Equal(123, ack.DroppedFrames);
    }

    [Fact]
    public void Codec_ExplicitLittleEndianLayoutAndUnsignedCountersRoundTrip()
    {
        var surface = Surface(); var bytes = Header(surface);
        Assert.Equal("TMFRAME1"u8.ToArray(), bytes[..8]); Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(32u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20)));
        Assert.Equal(1024 + 3 * 32 * 16 * 4, surface.MappingBytes);
        Assert.Equal(640, FrameCodec.SlotOffset(2)); Assert.Equal(5120, FrameCodec.PixelsOffset(surface, 2));
        var metadata = new FrameMetadata(2, ulong.MaxValue, 0xabcdef0123456789, 0x12345678, 123456);
        FrameCodec.WriteSlot(bytes, surface, metadata, FrameSlotState.Ready);
        Assert.Equal(0x78, bytes[680]); Assert.Equal(0x56, bytes[681]);
        Assert.Equal(metadata, FrameCodec.ReadSlot(bytes, surface, 2)); FrameCodec.ValidateHeader(bytes, surface);
        Assert.Equal(FrameSlotState.Ready, FrameCodec.State(bytes, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrameCodec.SlotOffset(3));
    }
    [Theory]
    [InlineData(0)] [InlineData(8)] [InlineData(12)] [InlineData(16)] [InlineData(20)] [InlineData(24)]
    [InlineData(28)] [InlineData(32)] [InlineData(36)] [InlineData(40)] [InlineData(48)] [InlineData(56)]
    [InlineData(64)] [InlineData(96)] [InlineData(128)] [InlineData(160)]
    public void Codec_RejectsEveryHeaderIdentityAndCapacityField(int offset)
    {
        var s = Surface(); var h = Header(s); h[offset] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => FrameCodec.ValidateHeader(h, s));
    }
    [Theory]
    [InlineData(4)] [InlineData(8)] [InlineData(24)] [InlineData(28)] [InlineData(32)] [InlineData(36)]
    [InlineData(48)] [InlineData(56)] [InlineData(64)] [InlineData(72)] [InlineData(104)] [InlineData(136)]
    public void Codec_RejectsInvalidSlotBeforePixelCopy(int offset)
    {
        var s = Surface(); var h = Header(s); FrameCodec.WriteSlot(h, s, new(0, 1, 9, 0, 1), FrameSlotState.Ready);
        if (offset is 8 or 48) Array.Clear(h, FrameCodec.SlotOffset(0) + offset, 8);
        else h[FrameCodec.SlotOffset(0) + offset] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => FrameCodec.ReadSlot(h, s, 0));
    }
    [Theory]
    [InlineData(15, 16)] [InlineData(16, 15)] [InlineData(1921, 1080)] [InlineData(1920, 1081)]
    [InlineData(0, 16)] [InlineData(16, 0)] [InlineData(-1, 16)] [InlineData(int.MaxValue, int.MaxValue)]
    public void Surface_RejectsOutOfBoundDimensions(int width, int height)
        => Assert.Throws<InvalidDataException>(() => Surface(width, height));
    [Fact]
    public void Surface_MaximumAndSuspensionHaveBoundedDistinctResources()
    {
        var s = Surface(1920, 1080); Assert.Equal(24884224, s.MappingBytes); Assert.True(s.MappingBytes < 25L * 1024 * 1024);
        Assert.NotEqual(s.Mapping, s.Mutex); Assert.NotEqual(s.Mapping, s.ReadyEvent);
        var zero = FrameSurface.Create(s.Identity, 2, 0, 0); Assert.True(zero.Suspended); Assert.Equal(0, zero.MappingBytes);
        Assert.Throws<InvalidDataException>(() => (zero with { Mapping = s.Mapping }).Validate());
        Assert.Throws<InvalidDataException>(() => (s with { Generation = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (s with { Mutex = s.Mapping }).Validate());
        Assert.Throws<InvalidDataException>(() => (s with { Mapping = "../unsafe" }).Validate());
        var wire = s.ToWire(); Assert.Equal(s, wire.Describe(s.Identity));
    }
    [Fact]
    public void Pattern_KnownCrcCornersAndCountersAreActualBgraBytes()
    {
        Assert.Equal(0xcbf43926u, FramePattern.Checksum("123456789"u8));
        var s = Surface(80, 32); var row = new byte[s.Stride];
        FramePattern.FillRow(row, s, 0, 5, 6, 0xff2080c0);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, row[..4]); Assert.Equal(new byte[] { 0, 255, 0, 255 }, row[^4..]);
        FramePattern.FillRow(row, s, 7, 5, 6, 0xff2080c0);
        Assert.Equal(0xffffffffu, BinaryPrimitives.ReadUInt32LittleEndian(row));
        Assert.Equal(0xff000000u, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4)));
        FramePattern.FillRow(row, s, 24, 5, 6, 0xff2080c0);
        Assert.Equal(0xff000000u, BinaryPrimitives.ReadUInt32LittleEndian(row));
        Assert.Equal(0xffffffffu, BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4)));
    }
    [Fact]
    public void Ring_ChoosesNewestReadyReclaimsOlderAndDropsWhenFull()
    {
        var s = Surface(); using var editor = SharedFrameRing.Create(s); using var child = SharedFrameRing.Open(s);
        var row = new byte[s.Stride]; var pixels = new byte[s.PixelBytes];
        Assert.Null(editor.CopyLatest(pixels, 0));
        for (ulong i = 1; i <= 3; i++) Assert.True(child.WritePattern(i, i + 10, 0xff2080c0, row));
        Assert.False(child.WritePattern(4, 14, 0xff2080c0, row)); Assert.Equal(1, child.Dropped);
        var latest = editor.CopyLatest(pixels, 0)!; Assert.Equal(3UL, latest.FrameId); Assert.Equal(13UL, latest.Tick);
        Assert.Equal(latest.Checksum, FramePattern.Checksum(pixels)); Assert.Equal(2, editor.Superseded);
        Assert.True(child.WritePattern(5, 15, 0xff2080c0, row)); Assert.Equal(5UL, editor.CopyLatest(pixels, 3)!.FrameId);
        Assert.Null(editor.CopyLatest(pixels, 5));
    }
    [Fact]
    public void Ring_ReadingSlotIsNeverOverwrittenAndStaleReadyIsDiscarded()
    {
        var s = Surface(); using var editor = SharedFrameRing.Create(s); using var child = SharedFrameRing.Open(s);
        var row = new byte[s.Stride]; child.WritePattern(1, 1, 0xff2080c0, row);
        using var map = MemoryMappedFile.OpenExisting(s.Mapping); using var view = map.CreateViewAccessor();
        var original = new byte[s.PixelBytes]; view.ReadArray(1024, original, 0, original.Length);
        Edit(s, h => FrameCodec.SetState(h, 0, FrameSlotState.Reading));
        Assert.True(child.WritePattern(2, 2, 0xff2080c0, row)); Assert.True(child.WritePattern(3, 3, 0xff2080c0, row));
        Assert.False(child.WritePattern(4, 4, 0xff2080c0, row));
        var unchanged = new byte[s.PixelBytes]; view.ReadArray(1024, unchanged, 0, unchanged.Length); Assert.Equal(original, unchanged);
        Assert.Null(editor.CopyLatest(unchanged, 3)); Assert.Equal(2, editor.Superseded);
        Edit(s, h => Assert.Equal(FrameSlotState.Reading, FrameCodec.State(h, 0)));
    }
    [Theory]
    [InlineData("header")] [InlineData("slot")] [InlineData("state")] [InlineData("crc")] [InlineData("eof")]
    public void Ring_CorruptionAndEofAreTerminal(string fault)
    {
        var s = Surface(); using var ring = SharedFrameRing.Create(s); ring.WritePattern(1, 0, 0xff2080c0, new byte[s.Stride]);
        if (fault == "eof") ring.End();
        else if (fault == "crc")
        { using var map = MemoryMappedFile.OpenExisting(s.Mapping); using var view = map.CreateViewAccessor(); view.Write(1024, (byte)99); }
        else Edit(s, h => { if (fault == "header") h[0] = 0; else if (fault == "slot") h[FrameCodec.SlotOffset(0) + 56]++; else h[FrameCodec.SlotOffset(2)] = 4; });
        var error = Record.Exception(() => ring.CopyLatest(new byte[s.PixelBytes], 0));
        if (fault == "eof") Assert.IsType<EndOfStreamException>(error); else Assert.IsType<InvalidDataException>(error);
        Assert.Throws<InvalidDataException>(() => ring.CopyLatest(new byte[s.PixelBytes], 0));
    }
    [Fact]
    public void Ring_MetadataTimeoutAndAbandonmentFailWithoutUnboundedWait()
    {
        var s = Surface(); using var ring = SharedFrameRing.Create(s);
        using var held = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var thread = new Thread(() => { using var mutex = Mutex.OpenExisting(s.Mutex); mutex.WaitOne(); held.Set(); release.Wait(); mutex.ReleaseMutex(); });
        thread.Start(); Assert.True(held.Wait(TimeSpan.FromSeconds(3)));
        try
        {
            var elapsed = Stopwatch.StartNew(); Assert.Throws<TimeoutException>(() => ring.CopyLatest(new byte[s.PixelBytes], 0));
            Assert.InRange(elapsed.Elapsed.TotalMilliseconds, 70, 2000);
        }
        finally { release.Set(); Assert.True(thread.Join(3000)); }
        var fresh = Surface(); using var abandonedRing = SharedFrameRing.Create(fresh);
        var abandon = new Thread(() => { using var mutex = Mutex.OpenExisting(fresh.Mutex); mutex.WaitOne(); });
        abandon.Start(); Assert.True(abandon.Join(3000));
        var error = Assert.Throws<InvalidDataException>(() => abandonedRing.CopyLatest(new byte[fresh.PixelBytes], 0));
        Assert.Contains("Abandoned", error.Message);
    }
    [Fact]
    public async Task Receiver_TwoBuffersKeepLatestWhileUiLeaseStaysUnchangedAndDrain()
    {
        var s = Surface(); using var ring = SharedFrameRing.Create(s); var receiver = new FrameReceiver(ring);
        var row = new byte[s.Stride]; ring.WritePattern(1, 0, 0xff2080c0, row);
        await receiver.FirstFrame.WaitAsync(TimeSpan.FromSeconds(3));
        using var first = receiver.TakeLatest()!; Assert.NotNull(first); var saved = first.Pixels.ToArray();
        for (ulong i = 2; i <= 5; i++)
        { ring.WritePattern(i, i, 0xff2080c0, row); await UntilAsync(() => receiver.Received >= (long)i); }
        Assert.Equal(saved, first.Pixels.ToArray()); Assert.True(receiver.Replaced >= 3);
        using (var latest = receiver.TakeLatest()) { Assert.NotNull(latest); Assert.Equal(5UL, latest.Metadata.FrameId); Assert.Equal(latest.Metadata.Checksum, FramePattern.Checksum(latest.Pixels)); }
        var draining = receiver.DisposeAsync().AsTask(); Assert.False(draining.IsCompleted);
        first.Dispose(); await draining.WaitAsync(TimeSpan.FromSeconds(3)); await receiver.DisposeAsync(); Assert.Null(receiver.TakeLatest());
        Assert.Throws<ObjectDisposedException>(() => first.Pixels.ToArray());
    }
    [Fact]
    public async Task Receiver_ScansWithoutReadySignalAndPropagatesEof()
    {
        var s = Surface(); using var ring = SharedFrameRing.Create(s);
        ring.WritePattern(9, 10, 0xff2080c0, new byte[s.Stride]); ring.Ready.Reset();
        await using var receiver = new FrameReceiver(ring); await receiver.FirstFrame.WaitAsync(TimeSpan.FromSeconds(3));
        using (var frame = receiver.TakeLatest()) { Assert.NotNull(frame); Assert.Equal(9UL, frame.Metadata.FrameId); }
        ring.End(); await Assert.ThrowsAsync<EndOfStreamException>(() => receiver.Completion.WaitAsync(TimeSpan.FromSeconds(3)));
    }
    private static async Task UntilAsync(Func<bool> ready)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!ready()) await Task.Delay(5, deadline.Token);
    }
}
