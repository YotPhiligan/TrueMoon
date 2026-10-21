using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Sessions;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;

public sealed class FrameTraceTests
{
    [Fact]
    public void GamePattern_OptInTraceMatchesExactFrameAndEnclosesProductionTimestamp()
    {
        using var listener = new Listener();
        var identity = new SessionIdentity(Guid.NewGuid(), 7, new('A', 64), new('B', 64), new('C', 64));
        var stamp = new GpuFrameStamp(identity, SurfaceGeneration: 3, FrameId: 2, Slot: 1, Version: 2);
        var data = GpuFrameData.GamePattern(stamp, 32, 16, 123, 0xff2080c0);
        var payload = Assert.Single(listener.Frames, p => (string)p[1]! == identity.SessionId.ToString("N"));
        Assert.Empty(listener.Errors);
        Assert.Equal("pattern", payload[0]); Assert.Equal(7L, payload[2]); Assert.Equal(3L, payload[3]);
        Assert.Equal((long)data.Metadata.FrameId, payload[4]); Assert.Equal(123UL, data.Metadata.Tick);
        Assert.True((long)payload[5]! <= data.Metadata.ProducedTimestamp);
        Assert.True(data.Metadata.ProducedTimestamp <= (long)payload[6]!);
        Assert.Equal(32 * 16 * 4, data.Pixels.Length);
    }

    private sealed class Listener : EventListener
    {
        internal ConcurrentQueue<object?[]> Frames { get; } = new();
        internal ConcurrentQueue<string> Errors { get; } = new();
        protected override void OnEventSourceCreated(EventSource source)
        { if (source.Name == "TrueMoon-Aether-Frames") EnableEvents(source, EventLevel.Informational); }
        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (data.EventId == 1) Frames.Enqueue(data.Payload!.ToArray());
            if (data.EventId == 0) Errors.Enqueue(string.Join(";", data.Payload ?? []));
        }
    }
}
