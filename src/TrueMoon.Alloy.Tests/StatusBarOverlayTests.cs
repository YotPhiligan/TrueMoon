using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class StatusBarOverlayTests
{
    [Fact]
    public void Hover_RevealsBackdropIn160MillisecondsWithoutMovingSceneFadingActionsOrStealingFocus()
    {
        using var test = new Fixture(); test.Ui.Update(); test.Ui.Focus(test.Scene);
        var scene = test.Scene.Bounds; var bar = test.Bar.Bounds; var details = test.Details.Bounds;
        Assert.Equal(new Rect(0, 0, 400, 200), scene);
        Assert.Equal(new Rect(0, 172, 400, 28), bar);
        Assert.Equal(0, test.Bar.OverlayReveal);
        Assert.Equal(0, test.Bar.Backdrop!.BlurSigma); Assert.Equal((byte)72, test.Bar.Backdrop.Tint.A);
        test.Move(199); test.Advance(159);
        Assert.InRange(test.Bar.OverlayReveal, 0.99f, float.BitDecrement(1));
        Assert.True(test.Bar.NeedsFrame);
        test.Advance(1);
        Assert.Equal(1, test.Bar.OverlayReveal); Assert.Equal(test.Bar.Theme.PanelBackdrop, test.Bar.Backdrop);
        Assert.True(test.Scene.IsFocused); Assert.False(test.Details.IsFocused);
        Assert.Equal(scene, test.Scene.Bounds); Assert.Equal(bar, test.Bar.Bounds); Assert.Equal(details, test.Details.Bounds);
        Assert.Equal(1, test.Bar.Opacity); Assert.Equal(1, test.Primary.Opacity);
        Assert.False(test.Ui.NeedsUpdate);
        for (var i = 0; i < 4; i++)
        {
            test.Move(199); test.Advance(17);
            Assert.False(test.Bar.NeedsFrame); Assert.False(test.Ui.NeedsUpdate);
        }
        test.Move(80); test.Advance(160);
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.False(test.Ui.NeedsUpdate);
        var clicks = 0; test.Details.Click += () => clicks++;
        test.Ui.HandleInput(new(InputKind.PointerDown, details.X + 4, details.Y + 4));
        test.Ui.HandleInput(new(InputKind.PointerUp, details.X + 4, details.Y + 4));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void KeyboardFocusAndExplicitActivity_HoldMaterialUntilReleasedAndFocusLossClearsHover()
    {
        using var test = new Fixture(); test.Ui.Update(); test.Ui.Focus(test.Details); test.Advance(160);
        test.Move(80); test.Advance(160);
        Assert.Equal(1, test.Bar.OverlayReveal); Assert.True(test.Details.IsFocused);
        test.Bar.KeepBackdropVisible = true; test.Ui.Focus(test.Scene); test.Advance(160);
        Assert.Equal(1, test.Bar.OverlayReveal); Assert.True(test.Scene.IsFocused);
        test.Bar.KeepBackdropVisible = false; test.Advance(160);
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.False(test.Bar.NeedsFrame);
        test.Move(199); test.Advance(160);
        test.Ui.HandleInput(new(InputKind.FocusLost)); test.Advance(160);
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.False(test.Details.IsFocused); Assert.False(test.Scene.IsFocused);
        Assert.False(test.Ui.NeedsUpdate);
    }

    [Fact]
    public void InterruptedHover_DetachAndOverlayDisableCancelAnimationAndRetainOwnedParts()
    {
        using var test = new Fixture(); test.Ui.Update(); test.Move(199); test.Advance(80);
        Assert.Equal(0.5f, test.Bar.OverlayReveal, 5);
        test.Move(80); test.Advance(80);
        Assert.Equal(0.25f, test.Bar.OverlayReveal, 5);
        test.Root.Items.Remove(test.Bar);
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.False(test.Bar.NeedsFrame);
        Assert.False(test.Primary.IsDisposed); Assert.Same(test.Bar, test.Primary.Parent);
        test.Advance(160); Assert.False(test.Ui.NeedsUpdate);
        test.Root.Add(test.Bar); test.Advance(160);
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.True(test.Primary.IsAttached);
        test.Move(199); test.Advance(80); Assert.InRange(test.Bar.OverlayReveal, 0.01f, 0.99f);
        test.Bar.OverlayContent = false; test.Ui.Update();
        Assert.Equal(0, test.Bar.OverlayReveal); Assert.Null(test.Bar.Backdrop);
        Assert.False(test.Bar.NeedsFrame); Assert.False(test.Ui.NeedsUpdate);
        test.Bar.KeepBackdropVisible = true; test.Ui.Update();
        Assert.Null(test.Bar.Backdrop); Assert.False(test.Bar.NeedsFrame);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ThemeAndOpaqueFallback_UpdateMaterialWhileRetainingGeometryFocusAndParts(bool light)
    {
        using var test = new Fixture(); test.Ui.Update(); test.Ui.Focus(test.Details); test.Advance(160);
        var scene = test.Scene.Bounds; var bar = test.Bar.Bounds; var details = test.Details.Bounds;
        var theme = light ? Theme.Light : Theme.Dark;
        test.Ui.SetTheme(theme); test.Ui.Update();
        Assert.Equal(theme.PanelBackdrop, test.Bar.Backdrop);
        test.Ui.SetTheme(theme with { PanelBackdrop = null }); test.Ui.Update();
        Assert.Null(test.Bar.Backdrop); Assert.Equal(theme.Surface, test.Bar.Background);
        Assert.Equal(scene, test.Scene.Bounds); Assert.Equal(bar, test.Bar.Bounds); Assert.Equal(details, test.Details.Bounds);
        Assert.True(test.Details.IsFocused); Assert.Equal(1, test.Primary.Opacity);
        Assert.False(test.Ui.NeedsUpdate);
    }

    [Theory]
    [InlineData(1f)] [InlineData(1.5f)]
    public void FrostedFooter_SamplesSceneUnderneathAndKeepsForegroundSharp(float scale)
    {
        var time = new ManualTime();
        var root = new Panel(); root.Add(new SplitScene());
        var bar = new StatusBar(new WhitePatch(), new Panel(), [], new Panel())
            { OverlayContent = true, Height = 28, VerticalAlignment = LayoutAlignment.End };
        root.Add(bar);
        var viewport = new UiViewport((int)(400 * scale), (int)(200 * scale), scale);
        var surface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), viewport);
        using var ui = new UiSession(root, surface, viewport, null, new UiDispatchOptions(), time);
        ui.SetTheme(Theme.Dark with { PanelBackdrop = new(4, new(0, 0, 255, 64), new(30, 40, 50)) });
        ui.Update(); ui.HandleInput(new(InputKind.PointerMove, 300, 199)); time.Advance(160); ui.Update();
        using var image = ui.SnapshotRasterImage(); using var pixels = SKBitmap.FromImage(image);
        SKColor Pixel(int x, int y) => pixels.GetPixel((int)(x * scale), (int)(y * scale));
        var blurred = Pixel(81, 186);
        Assert.InRange(blurred.Red, (byte)80, (byte)175);
        Assert.InRange(blurred.Blue, (byte)(blurred.Red + 60), (byte)(blurred.Red + 66));
        Assert.Equal(SKColors.White, Pixel(46, 188));
        Assert.Equal(SKColors.White, Pixel(100, 60));
        Assert.Equal(new Rect(0, 172, 400, 28), bar.Bounds);
        Assert.Equal(1, bar.Opacity); Assert.False(ui.NeedsUpdate);
    }

    private sealed class Fixture : IDisposable
    {
        public ManualTime Time { get; } = new();
        public Panel Root { get; } = new();
        public Button Scene { get; } = new("scene");
        public Button Primary { get; } = new("ready");
        public Button Details { get; } = new("info") { Width = 24 };
        public StatusBar Bar { get; }
        public UiSession Ui { get; }
        public Fixture()
        {
            Root.Add(Scene);
            Bar = new(Primary, new Panel(), [], Details)
                { OverlayContent = true, Height = 28, VerticalAlignment = LayoutAlignment.End };
            Root.Add(Bar); Ui = new(Root, new Surface(), new(400, 200), null, new UiDispatchOptions(), Time);
        }
        public void Move(float y) => Ui.HandleInput(new(InputKind.PointerMove, 300, y));
        public void Advance(int milliseconds) { Time.Advance(milliseconds); Ui.Update(); }
        public void Dispose() => Ui.Dispose();
    }
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(int milliseconds) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }
    private sealed class Surface : IUiRenderSurface
    {
        public ITextLayoutService TextLayout { get; } = new FixedTextLayout();
        public void VerifyAvailable() { }
        public void Resize(UiViewport viewport) { }
        public void Render(Element root, UiViewport viewport) { }
        public void Dispose() { }
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
