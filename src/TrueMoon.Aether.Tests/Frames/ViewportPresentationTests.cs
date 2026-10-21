using TrueMoon.Aether.Frames;
using Xunit;

namespace TrueMoon.Aether.Tests.Frames;

public sealed class ViewportPresentationTests
{
    private static ViewportSurface Surface() => new(new(Guid.NewGuid(), 1, new('A', 64), new('B', 64), new('C', 64)), 3, 32, 16, ViewportTransportKind.Gpu);
    private static FrameMetadata Metadata(ulong id = 1) => new(0, id, 37, 0, 1);
    [Fact]
    public void FirstPresent_RequiresExactLatestTicketAndRejectsForeignOrClosedSurface()
    {
        var surface = Surface(); var first = new ViewportPresentation(surface); var other = new ViewportPresentation(surface);
        var old = first.Issue(Metadata()); var foreign = other.Issue(Metadata()); var next = first.Issue(Metadata(2));
        Assert.Same(surface, next.Presentation.Surface); Assert.Equal(37ul, next.Metadata.Tick);
        Assert.False(first.Presented(old)); Assert.False(first.Presented(foreign)); Assert.False(first.Presented(null!)); Assert.False(first.FirstPresented.IsCompleted);
        Assert.True(first.Presented(next)); Assert.True(first.FirstPresented.IsCompletedSuccessfully);
        first.Close(); Assert.False(first.Presented(next)); Assert.Throws<ObjectDisposedException>(() => first.Issue(Metadata(3))); Assert.True(first.FirstPresented.IsCompletedSuccessfully);
        other.Close(); Assert.True(other.FirstPresented.IsCanceled);
    }
    [Theory]
    [InlineData("slot-negative")] [InlineData("slot-over")] [InlineData("zero-id")] [InlineData("zero-time")] [InlineData("negative-time")] [InlineData("stale-id")]
    public void InvalidFrame_DoesNotReplaceValidPendingTicket(string invalid)
    {
        var presentation = new ViewportPresentation(Surface()); var first = presentation.Issue(Metadata(7));
        var bad = invalid switch
        { "slot-negative" => Metadata(8) with { Slot = -1 }, "slot-over" => Metadata(8) with { Slot = 3 }, "zero-id" => Metadata(0),
            "zero-time" => Metadata(8) with { ProducedTimestamp = 0 }, "negative-time" => Metadata(8) with { ProducedTimestamp = -1 }, _ => Metadata(7) };
        Assert.Throws<InvalidDataException>(() => presentation.Issue(bad)); Assert.False(presentation.FirstPresented.IsCompleted); Assert.True(presentation.Presented(first));
    }
    [Fact]
    public async Task Close_RetainsExactFailureAndSuspensionNeverAcceptsFrame()
    {
        var presentation = new ViewportPresentation(Surface()); var failure = new IOException("transport failed");
        presentation.Close(failure); presentation.Close(); Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => presentation.FirstPresented));
        var suspended = new ViewportPresentation(Surface() with { Width = 0, Height = 0 }); Assert.True(suspended.Surface.Suspended);
        Assert.Throws<InvalidDataException>(() => suspended.Issue(Metadata())); Assert.False(suspended.FirstPresented.IsCompleted); suspended.Close(); Assert.True(suspended.FirstPresented.IsCanceled);
    }
    [Theory]
    [InlineData(0, 16)] [InlineData(16, 0)] [InlineData(15, 16)] [InlineData(16, 15)] [InlineData(1921, 1080)] [InlineData(1920, 1081)]
    public void Surface_RejectsInvalidExtent(int width, int height) => Assert.Throws<InvalidDataException>(() => new ViewportPresentation(Surface() with { Width = width, Height = height }));
    [Theory]
    [InlineData(16, 16)] [InlineData(1920, 1080)] [InlineData(0, 0)]
    public void Surface_AcceptsReferenceBounds(int width, int height)
    { var surface = Surface() with { Width = width, Height = height }; var presentation = new ViewportPresentation(surface); Assert.Same(surface, presentation.Surface); Assert.Equal(width == 0, surface.Suspended); presentation.Close(); }
}
