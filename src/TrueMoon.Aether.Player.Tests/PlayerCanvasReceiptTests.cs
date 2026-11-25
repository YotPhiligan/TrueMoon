using TrueMoon.Argentis.Painting;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Player.Tests;

public sealed class PlayerCanvasReceiptTests
{
    private static ViewportPresentation Presentation() => new(new(new SessionIdentity(Guid.NewGuid(), 1, new('A',64), new('B',64), new('C',64)), 1, 32, 16, ViewportTransportKind.Gpu));
    private static SkiaImageSource Image() => SkiaImageSource.CopyBgraPixels(32, 16, new byte[32 * 16 * 4]);

    [Fact]
    public void ImageAndHudMustBothSucceedAndDrawingDoesNotAcknowledgePresentation()
    {
        using var presenters = UiPresenters.Create(PlayerPresenters.Register);
        using var canvas = new PlayerCanvas(new()); var context = new Drawing();
        var presentation = Presentation(); var frame = presentation.Issue(new(0,1,0,0,1));
        canvas.Arrange(new(10,20,200,160));
        canvas.Upload(Image(), frame, new GameHudSnapshot("Use", null, []), () => true);
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Equal(new Rect(10,50,200,100), Assert.Single(context.Images));
        Assert.Equal("Use", Assert.Single(context.Labels)); Assert.Same(frame, canvas.Drawn);
        Assert.False(presentation.FirstPresented.IsCompleted);

        context.OnText = () => throw new IOException("HUD failed.");
        Assert.Throws<IOException>(() => UiTreeRenderer.Draw(canvas, context, presenters));
        Assert.Equal(2, context.Images.Count); Assert.Null(canvas.Drawn);
        Assert.False(presentation.FirstPresented.IsCompleted);

        canvas.Arrange(new(10,20,400,160)); context.OnText = null;
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Equal(new Rect(50,20,320,160), context.Images[^1]); Assert.Same(frame, canvas.Drawn);
        Assert.False(presentation.FirstPresented.IsCompleted);
        Assert.True(presentation.Presented(canvas.Drawn!)); Assert.Same(frame, presentation.FirstPresentedFrame);
    }

    [Fact]
    public void SupersededAndSkippedImagesNeverConfirmReplacementAndForeignSurfaceInvalidatesDraw()
    {
        using var presenters = UiPresenters.Create(PlayerPresenters.Register);
        using var canvas = new PlayerCanvas(new()); var context = new Drawing();
        canvas.Arrange(new(0,0,200,160)); var presentation = Presentation();
        var first = presentation.Issue(new(0,1,0,0,1)); var current = true;
        canvas.Upload(Image(), first, null, () => true);
        var second = presentation.Issue(new(1,2,0,0,2));
        context.OnImage = () => canvas.Upload(Image(), second, null, () => current);
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Single(context.Images); Assert.Null(canvas.Drawn);
        canvas.IsVisible = false; context.OnImage = null;
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Single(context.Images); Assert.Null(canvas.Drawn); Assert.False(presentation.FirstPresented.IsCompleted);
        canvas.IsVisible = true;
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Equal(2, context.Images.Count); Assert.Same(second, canvas.Drawn);
        current = false; Assert.Null(canvas.Drawn);
        UiTreeRenderer.Draw(canvas, context, presenters);
        Assert.Equal(2, context.Images.Count); Assert.Null(canvas.Drawn); Assert.False(presentation.FirstPresented.IsCompleted);
    }

    private sealed class Drawing : IDrawingContext
    {
        internal List<Rect> Images { get; } = [];
        internal List<string> Labels { get; } = [];
        internal Action? OnImage { get; set; }
        internal Action? OnText { get; set; }
        public void Image(IImageSource image, Rect destination) { Images.Add(destination); OnImage?.Invoke(); }
        public void Text(string text, float x, float y, Color color, float size, string family) { Labels.Add(text); OnText?.Invoke(); }
        public void Save() { }
        public void Restore() { }
        public void Clip(Rect bounds) { }
        public void Translate(float x, float y) { }
        public void Fill(Rect bounds, Color color, float radius = 0) { }
        public void Stroke(Rect bounds, Color color, float width = 1, float radius = 0) { }
    }
}
