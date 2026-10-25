using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class OverlayWindowChromeTests
{
    [Fact]
    public void DefaultFrame_ReservesCaptionAndOverlayToggleRetainsOwnedPartsAndRestoresGeometry()
    {
        var title = new SizedElement { Height = 40, WindowRegion = WindowRegionRole.Caption };
        var body = new SizedElement();
        using var frame = new WindowFrame(title, body);
        Arrange(frame);
        Assert.False(frame.TitleBarOverContent);
        Assert.Equal(new Rect(0, 40, 400, 160), body.Bounds);
        Assert.Equal(new Size(400, 160), body.Available);
        Assert.Equal(new Element[] { title, body }, frame.Children);

        frame.TitleBarOverContent = true; Arrange(frame);
        Assert.Same(title, frame.TitleBar); Assert.Same(body, frame.Content);
        Assert.Equal(new Rect(0, 0, 400, 200), body.Bounds);
        Assert.Equal(new Size(400, 200), body.Available);
        Assert.Equal(new Rect(0, 0, 400, 40), title.Bounds);
        Assert.Equal(new Element[] { body, title }, frame.Children);
        Assert.Equal(WindowRegionRole.Caption, WindowRegionMap.Create(frame).HitTest(20, 20));

        frame.TitleBarOverContent = false; Arrange(frame);
        Assert.Equal(new Rect(0, 40, 400, 160), body.Bounds);
        Assert.Equal(new Element[] { title, body }, frame.Children);
        Assert.False(title.IsDisposed); Assert.False(body.IsDisposed);
    }

    [Theory]
    [InlineData(400, 200)]
    [InlineData(90, 12)]
    [InlineData(0, 0)]
    public void OverlayFrame_UsesWholeContentEvenWhenSmallerThanCaption(float width, float height)
    {
        var title = new SizedElement { Height = 40 }; var body = new SizedElement();
        using var frame = new WindowFrame(title, body) { TitleBarOverContent = true };
        Arrange(frame, width, height);
        Assert.Equal(new Rect(0, 0, width, height), body.Bounds);
        Assert.Equal(new Size(width, height), body.Available);
        Assert.Equal(new Rect(0, 0, width, Math.Min(40, height)), title.Bounds);
    }

    [Fact]
    public void DefaultOverlay_PaintsContentBeforeTitleAndKeepsResizeGripAboveBoth()
    {
        var order = new List<string>();
        var body = new PaintProbe("body", order);
        using var frame = WindowFrame.CreateDefault("caption", body);
        var title = frame.DefaultTitleBar!;
        title.SetHeader(new PaintProbe("header", order));
        frame.TitleBarOverContent = true;
        frame.ApplyWindowState(new(true, false, true, true, true));
        Arrange(frame);
        frame.Draw(new RecordingDrawingContext());
        Assert.Equal(new[] { "body", "header" }, order);
        Assert.Equal(new Element[] { body, title, frame.ResizeGrip! }, frame.Children);
        Assert.True(title.OverlayContent);
        Assert.Equal(new Rect(1, 1, 398, 198), body.Bounds);
        var grip = frame.ResizeGrip!.Bounds;
        Assert.Equal(399, grip.X + grip.Width); Assert.Equal(199, grip.Y + grip.Height);
        Assert.Equal(WindowRegionRole.ResizeBottomRight,
            WindowRegionMap.Create(frame).HitTest(grip.X + 4, grip.Y + 4));
        frame.TitleBarOverContent = false;
        Assert.False(title.OverlayContent);
    }

    [Fact]
    public void OverlayCaption_ReceivesWindowActionsBeforeFullAreaBodyAndLeavesExposedContentInteractive()
    {
        var body = new Button("body"); var title = new WindowTitleBar("caption");
        using var frame = new WindowFrame(title, body) { TitleBarOverContent = true };
        using var ui = Create(frame, new ManualTime());
        var bodyClicks = 0; var closeClicks = 0;
        body.Click += () => bodyClicks++; title.CloseButton.Click += () => closeClicks++;
        ui.Update();
        Click(ui, title.CloseButton.Bounds);
        Assert.Equal(1, closeClicks); Assert.Equal(0, bodyClicks);
        Click(ui, new Rect(20, 80, 10, 10));
        Assert.Equal(1, bodyClicks);
        Assert.Equal(WindowRegionRole.Caption, WindowRegionMap.Create(frame).HitTest(20, 10));
        Assert.Equal(WindowRegionRole.Client, WindowRegionMap.Create(frame).HitTest(390, 10));
    }

    [Fact]
    public void HoverReveal_CompletesAt160MillisecondsWithoutMovingContentOrFadingForeground()
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        var body = new SizedElement();
        using var ui = Create(new WindowFrame(title, body) { TitleBarOverContent = true }, time);
        ui.Update();
        var bodyBounds = body.Bounds; var titleBounds = title.Bounds; var closeBounds = title.CloseButton.Bounds;
        var idleMaterial = title.Backdrop!;
        Assert.Equal(0, title.OverlayReveal); Assert.Equal(0, idleMaterial.BlurSigma);
        Assert.Equal((byte)72, idleMaterial.Tint.A);
        Assert.False(ui.NeedsUpdate);
        ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(159)); ui.Update();
        Assert.InRange(title.OverlayReveal, 0.99f, float.BitDecrement(1));
        Assert.True(ui.NeedsUpdate);
        time.Advance(TimeSpan.FromMilliseconds(1)); ui.Update();
        Assert.Equal(1, title.OverlayReveal);
        Assert.Equal(title.Theme.PanelBackdrop, title.Backdrop);
        Assert.Equal(1, title.Opacity); Assert.Equal(1, title.CloseButton.Opacity);
        Assert.Equal(bodyBounds, body.Bounds); Assert.Equal(titleBounds, title.Bounds);
        Assert.Equal(closeBounds, title.CloseButton.Bounds);
        Assert.False(ui.NeedsUpdate);
        time.Advance(TimeSpan.FromHours(1)); Assert.False(ui.Update());
    }

    [Fact]
    public void RepeatedHoverAndInterruptedLeave_ContinueFromCurrentValueAndEventuallyBecomeIdle()
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        using var ui = Create(new WindowFrame(title, new Panel()) { TitleBarOverContent = true }, time);
        ui.Update(); ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.Equal(0.5f, title.OverlayReveal, 5);
        ui.HandleInput(new(InputKind.PointerMove, 40, 10));
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.Equal(1, title.OverlayReveal); // moving within the bar cannot restart its transition

        ui.HandleInput(new(InputKind.PointerMove, 20, 70));
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.Equal(0.5f, title.OverlayReveal, 5);
        ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        Assert.Equal(0.5f, title.OverlayReveal, 5);
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.Equal(0.75f, title.OverlayReveal, 5);
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.Equal(1, title.OverlayReveal); Assert.False(ui.NeedsUpdate);
        ui.HandleInput(new(InputKind.PointerMove, 20, 70));
        time.Advance(TimeSpan.FromSeconds(1)); ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepeatedIdenticalPointerMoves_AfterSettledHoverAndIdleDoNotInvalidateOrRequestFrames(bool active)
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption"); var body = new Panel();
        title.ApplyState(new(active, false, true, true, true));
        using var ui = Create(new WindowFrame(title, body) { TitleBarOverContent = true }, time); ui.Update();
        var bodyBounds = body.Bounds; var titleBounds = title.Bounds; var closeBounds = title.CloseButton.Bounds;
        foreach (var (y, reveal) in new[] { (10f, 1f), (70f, 0f) })
        {
            ui.HandleInput(new(InputKind.PointerMove, 20, y));
            time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
            Assert.Equal(reveal, title.OverlayReveal); Assert.False(title.NeedsFrame); Assert.False(ui.NeedsUpdate);
            for (var i = 0; i < 4; i++)
            {
                time.Advance(TimeSpan.FromMilliseconds(17));
                ui.HandleInput(new(InputKind.PointerMove, 20, y));
                Assert.Equal(reveal, title.OverlayReveal); Assert.False(title.NeedsFrame);
                Assert.False(ui.NeedsUpdate); Assert.False(ui.Update());
            }
        }
        Assert.Equal(bodyBounds, body.Bounds); Assert.Equal(titleBounds, title.Bounds); Assert.Equal(closeBounds, title.CloseButton.Bounds);
        Assert.Equal(1, title.Opacity); Assert.Equal(1, title.CloseButton.Opacity);
    }

    [Fact]
    public void KeyboardFocusWithinTitle_RetainsBackdropAfterPointerLeavesAndReleasesWhenFocusMovesToBody()
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        var body = new Button("body");
        using var ui = Create(new WindowFrame(title, body) { TitleBarOverContent = true }, time);
        ui.Update(); ui.Focus(title.CloseButton);
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(1, title.OverlayReveal);
        ui.HandleInput(new(InputKind.PointerMove, 20, 70));
        time.Advance(TimeSpan.FromSeconds(1)); ui.Update();
        Assert.Equal(1, title.OverlayReveal); Assert.True(title.CloseButton.IsFocused);
        ui.Focus(body); time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.True(body.IsFocused); Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void CustomHeaderActivity_KeepsBackdropVisibleWhilePopupFocusLivesOutsideTitle()
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        var header = new ActiveHeader(); title.SetHeader(header);
        var body = new Button("popup focus");
        using var ui = Create(new WindowFrame(title, body) { TitleBarOverContent = true }, time);
        ui.Update(); ui.Focus(body); header.KeepsTitleBarActive = true;
        Assert.True(title.NeedsFrame);
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(1, title.OverlayReveal); Assert.True(body.IsFocused);
        Assert.False(title.NeedsFrame);
        header.KeepsTitleBarActive = false;
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.True(body.IsFocused);
        Assert.False(ui.NeedsUpdate);
    }

    [Fact]
    public void FocusLossInactiveHoverAndOverlayDisable_RespectPointerActivityAndWindowActions()
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        using var ui = Create(new WindowFrame(title, new Panel()) { TitleBarOverContent = true }, time);
        ui.Update(); ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        ui.HandleInput(new(InputKind.FocusLost));
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(0, title.OverlayReveal);

        title.ApplyState(new(false, true, true, true, false));
        ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(1, title.OverlayReveal);
        Assert.Equal(UiSymbol.Restore, title.MaximizeButton.Symbol);
        Assert.True(title.MinimizeButton.IsEnabled); Assert.True(title.MaximizeButton.IsEnabled);

        ui.HandleInput(new(InputKind.PointerMove, 20, 70));
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(0, title.OverlayReveal);
        ui.Focus(title.CloseButton); time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.False(title.NeedsFrame);
        title.ApplyState(new(true, false, true, true, true));
        ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.InRange(title.OverlayReveal, 0.01f, 0.99f);
        title.OverlayContent = false; ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.Null(title.Backdrop);
        Assert.Equal(title.Theme.Control, title.Background);
        Assert.False(title.NeedsFrame);
    }

    [Fact]
    public void DetachDuringReveal_ResetsAnimationAndDoesNotRetainRemovedHeaderOrWakeSession()
    {
        var time = new ManualTime(); var root = new Panel();
        var title = new WindowTitleBar("caption") { OverlayContent = true }; root.Add(title);
        using var ui = Create(root, time); ui.Update();
        ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(80)); ui.Update();
        Assert.InRange(title.OverlayReveal, 0.01f, 0.99f);
        root.Items.Remove(title);
        Assert.Equal(0, title.OverlayReveal); Assert.False(title.NeedsFrame);
        time.Advance(TimeSpan.FromSeconds(1)); ui.Update(); Assert.False(ui.NeedsUpdate);
        Assert.False(title.IsDisposed);
        root.Add(title); ui.Update();
        Assert.Equal(0, title.OverlayReveal); Assert.False(ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThemeChangeAndOpaqueFallback_RetainGeometryAndHeaderState(bool light)
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        var header = new ActiveHeader { KeepsTitleBarActive = true }; title.SetHeader(header);
        var body = new Button("body");
        using var ui = Create(new WindowFrame(title, body) { TitleBarOverContent = true }, time);
        ui.Update(); time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        var bounds = body.Bounds;
        var theme = light ? Theme.Light : Theme.Dark;
        ui.SetTheme(theme); ui.Update();
        Assert.Equal(theme.PanelBackdrop, title.Backdrop);
        ui.SetTheme(theme with { PanelBackdrop = null }); ui.Update();
        Assert.Null(title.Backdrop); Assert.Equal(theme.Surface, title.Background);
        Assert.Same(header, title.HeaderContent); Assert.Equal(1, title.OverlayReveal);
        Assert.Equal(bounds, body.Bounds); Assert.Equal(1, title.Opacity);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void FrostedCaption_SamplesFullAreaSceneAndKeepsHeaderForegroundSharp(float scale)
    {
        var time = new ManualTime(); var title = new WindowTitleBar("caption");
        title.SetHeader(new WhitePatch());
        var viewport = new UiViewport((int)(400 * scale), (int)(200 * scale), scale);
        var frame = new WindowFrame(title, new SplitScene()) { TitleBarOverContent = true };
        var surface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), viewport);
        using var ui = new UiSession(frame, surface, viewport, null, new UiDispatchOptions(), time);
        ui.SetTheme(Theme.Dark with { PanelBackdrop = new(4, new(0, 0, 255, 64), new(30, 40, 50)) });
        ui.Update(); ui.HandleInput(new(InputKind.PointerMove, 20, 10));
        time.Advance(TimeSpan.FromMilliseconds(160)); ui.Update();
        using var image = ui.SnapshotRasterImage(); using var pixels = SKBitmap.FromImage(image);
        SKColor Pixel(int x, int y) => pixels.GetPixel((int)(x * scale), (int)(y * scale));
        var blurred = Pixel(81, 20);
        Assert.InRange(blurred.Red, (byte)80, (byte)175);
        Assert.InRange(blurred.Blue, (byte)(blurred.Red + 60), (byte)(blurred.Red + 66));
        Assert.Equal(SKColors.White, Pixel(44, 15));
        Assert.Equal(SKColors.White, Pixel(100, 60)); // exposed scene is not tinted
        Assert.Equal(1, title.Opacity); Assert.False(ui.NeedsUpdate);
    }

    private static void Arrange(Element root, float width = 400, float height = 200)
    { root.Measure(new(width, height), new FixedTextLayout()); root.Arrange(new(0, 0, width, height)); }
    private static UiSession Create(Element root, ManualTime time) =>
        new(root, new Surface(), new UiViewport(400, 200), null, new UiDispatchOptions(), time);
    private static void Click(UiSession ui, Rect bounds)
    {
        var x = bounds.X + bounds.Width / 2; var y = bounds.Y + bounds.Height / 2;
        Assert.True(ui.HandleInput(new(InputKind.PointerDown, x, y)).Handled);
        ui.HandleInput(new(InputKind.PointerUp, x, y));
    }
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
    }
    private sealed class PaintProbe(string name, List<string> order) : Element
    {
        protected override void DrawCore(IDrawingContext context) => order.Add(name);
    }
    private sealed class ActiveHeader : Element, IWindowHeaderActivity
    {
        public bool KeepsTitleBarActive { get; set; }
    }
    private sealed class SplitScene : Element
    {
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new(0, 0, 80, Bounds.Height), new(0, 0, 0));
            context.Fill(new(80, 0, Bounds.Width - 80, Bounds.Height), new(255, 255, 255));
        }
    }
    private sealed class WhitePatch : Element
    {
        protected override void DrawCore(IDrawingContext context) =>
            context.Fill(new(Bounds.X + 34, Bounds.Y + 10, 10, 10), new(255, 255, 255));
    }
}
