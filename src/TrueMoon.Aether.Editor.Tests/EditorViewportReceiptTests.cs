using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorViewportReceiptTests
{
    private static ViewportPresentation Presentation() => new(new(new SessionIdentity(Guid.NewGuid(),1,new('A',64),new('B',64),new('C',64)),1,32,16,ViewportTransportKind.Gpu));
    private static SkiaImageSource Image() => SkiaImageSource.CopyBgraPixels(32,16,new byte[32*16*4]);

    [Fact]
    public void Draw_FreezesLetterboxAndClipAndKeepsLayoutRevisionAcrossNewFrameIds()
    {
        using var viewport=new EditorViewport(); var context=new TypographyRecordingContext(); var presentation=Presentation();
        viewport.Measure(new(200,160),context); viewport.Arrange(new(0,0,200,160));
        var first=presentation.Issue(new(0,1,0,0,1)); var image=Image(); viewport.Upload(image,first,()=>true);
        Assert.Equal(new Rect(0,30,200,100),viewport.ImageBounds);
        Assert.Null(viewport.DrawnFrame); Assert.Equal(0ul,viewport.LayoutRevision); Assert.False(viewport.CanAcknowledgeDraw);
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.Same(first,viewport.DrawnFrame); Assert.Equal(new Rect(0,30,200,100),viewport.ImageBounds);
        Assert.Equal(new Rect(0,0,200,160),viewport.DrawnClipBounds); Assert.Equal(1ul,viewport.LayoutRevision); Assert.True(viewport.CanAcknowledgeDraw);
        Assert.True(viewport.MatchesPresentedLayout(new(0,30,200,100),new(0,0,200,160),1));
        Assert.False(viewport.MatchesPresentedLayout(new(0,30,200,100),new(0,0,200,160),2));
        Assert.False(viewport.MatchesPresentedLayout(new(0,31,200,100),new(0,0,200,160),1));
        Assert.False(viewport.MatchesPresentedLayout(new(0,30,200,100),new(0,0,199,160),1));
        var second=presentation.Issue(new(1,2,0,0,2)); viewport.Upload(Image(),second,()=>true);
        Assert.Throws<ObjectDisposedException>(()=>image.Size); Assert.False(viewport.CanAcknowledgeDraw);
        Assert.True(viewport.MatchesPresentedLayout(new(0,30,200,100),new(0,0,200,160),1));
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.Same(second,viewport.DrawnFrame); Assert.Equal(1ul,viewport.LayoutRevision); Assert.True(viewport.CanAcknowledgeDraw);
        viewport.Arrange(new(0,0,400,160)); Assert.False(viewport.CanAcknowledgeDraw);
        Assert.Equal(new Rect(40,0,320,160),viewport.ImageBounds);
        Assert.False(viewport.MatchesPresentedLayout(new(0,30,200,100),new(0,0,200,160),1));
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.Equal(new Rect(40,0,320,160),viewport.ImageBounds); Assert.Equal(2ul,viewport.LayoutRevision); Assert.True(viewport.CanAcknowledgeDraw);
    }

    [Fact]
    public void Draw_HiddenTransparentSuspendedAndForeignUploadCannotAcknowledgeAnOldImage()
    {
        using var viewport=new EditorViewport(); var context=new TypographyRecordingContext(); var presentation=Presentation(); var current=true;
        viewport.Measure(new(200,160),context); viewport.Arrange(new(0,0,200,160)); viewport.Upload(Image(),presentation.Issue(new(0,1,0,0,1)),()=>current);
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.True(viewport.CanAcknowledgeDraw);
        viewport.IsVisible=false; TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.False(viewport.CanAcknowledgeDraw);
        viewport.IsVisible=true; viewport.Opacity=0; Assert.False(viewport.CanAcknowledgeDraw);
        viewport.Opacity=1; viewport.Arrange(new(0,0,0,0)); TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.False(viewport.CanAcknowledgeDraw);
        viewport.Arrange(new(0,0,200,160)); TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.True(viewport.CanAcknowledgeDraw);
        current=false; Assert.False(viewport.CanAcknowledgeDraw); TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport, context); Assert.Null(viewport.DrawnFrame);
        viewport.Dispose(); Assert.False(viewport.CanAcknowledgeDraw);
    }

    [Fact]
    public void ImageFailureInvalidatesPreviousDrawAndOnlyHostCanConfirmRetry()
    {
        using var viewport = new EditorViewport();
        var context = new TypographyRecordingContext();
        var presentation = Presentation(); var frame = presentation.Issue(new(0,1,0,0,1));
        viewport.Arrange(new(0,0,200,160)); viewport.Upload(Image(),frame,()=>true);
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context);
        Assert.Same(frame,viewport.CurrentDraw!.Value.Frame);
        context.OnImage = _ => throw new IOException("Image failed.");
        Assert.Throws<IOException>(() => TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context));
        Assert.Null(viewport.CurrentDraw); Assert.Null(viewport.DrawnFrame);
        Assert.False(presentation.FirstPresented.IsCompleted);
        context.OnImage = null;
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context);
        var draw = viewport.CurrentDraw!.Value;
        Assert.Same(frame,draw.Frame); Assert.False(presentation.FirstPresented.IsCompleted);
        Assert.True(presentation.Presented(draw.Frame)); Assert.Same(frame,presentation.FirstPresentedFrame);
    }

    [Fact]
    public void UploadDuringImageRejectsLateDrawAndSkippedReplacementRemainsUnpresented()
    {
        using var viewport = new EditorViewport();
        var context = new TypographyRecordingContext(); var presentation = Presentation();
        var first = presentation.Issue(new(0,1,0,0,1));
        viewport.Arrange(new(0,0,200,160)); viewport.Upload(Image(),first,()=>true);
        var next = presentation.Issue(new(1,2,0,0,2));
        context.OnImage = rect =>
        {
            Assert.Equal(new Rect(0,30,200,100),rect);
            viewport.Upload(Image(),next,()=>true);
        };
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context);
        Assert.Null(viewport.CurrentDraw); Assert.Null(viewport.DrawnFrame);
        viewport.IsVisible = false;
        context.OnImage = _ => throw new InvalidOperationException("Hidden image was drawn.");
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context);
        Assert.Null(viewport.CurrentDraw); Assert.False(presentation.FirstPresented.IsCompleted);
        viewport.IsVisible = true; context.OnImage = null;
        TrueMoon.Alloy.Runtime.UiTreeRenderer.Draw(viewport,context);
        Assert.Same(next,viewport.CurrentDraw!.Value.Frame); Assert.False(presentation.FirstPresented.IsCompleted);
    }
}
