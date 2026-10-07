using System.Runtime.InteropServices;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class RasterRenderingTests
{
    private static UiSession Create(Element root, UiViewport? viewport = null) => UiSession.Create(
        root, new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport ?? new UiViewport(120, 80));

    private static byte[] Pixels(SKImage image)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Assert.True(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        var pixels = new byte[bitmap.RowBytes * bitmap.Height];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return pixels;
    }
    private static byte[] Pixel(SKImage image, int x, int y)
    {
        var offset = (y * image.Width + x) * 4;
        return Pixels(image)[offset..(offset + 4)];
    }
    private static Panel Patch(Color color, float width = 40, float height = 30) => new()
    {
        Background = color, Width = width, Height = height, Margin = new Thickness(10),
        HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
    };
    private static Button ButtonAt(float x = 4) => new("Action")
    {
        Width = 80, Height = 32, Margin = new Thickness(x, 4, 0, 0),
        HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
    };

    [Fact]
    public void Snapshot_PreservesTransparencyAndPremultipliedAlpha()
    {
        var root = new Panel(); root.Add(Patch(new Color(200, 100, 50, 128)));
        using var ui = Create(root);
        Assert.True(ui.Update());
        using var image = ui.SnapshotRasterImage();

        Assert.False(image.IsTextureBacked);
        Assert.Equal(SKAlphaType.Premul, image.AlphaType);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(image, 0, 0));
        Assert.Equal(new byte[] { 100, 50, 25, 128 }, Pixel(image, 20, 20));
    }

    [Fact]
    public void Snapshot_BlendsPremultipliedLayersInPaintOrder()
    {
        var root = new Panel { Background = new Color(20, 40, 60) };
        root.Add(Patch(new Color(200, 100, 50, 128)));
        using var ui = Create(root); ui.Update();
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(new byte[] { 20, 40, 60, 255 }, Pixel(image, 0, 0));
        var blended = Pixel(image, 20, 20);
        Assert.InRange(blended[0], (byte)109, (byte)111);
        Assert.InRange(blended[1], (byte)69, (byte)71);
        Assert.InRange(blended[2], (byte)54, (byte)56);
        Assert.Equal(255, blended[3]);
    }

    [Fact]
    public void Render_ClipsOverflowAndClearsHiddenContent()
    {
        var child = new OverflowFill { Width = 20, Height = 15, Margin = new Thickness(10),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        var root = new Panel(); root.Add(child);
        using var ui = Create(root); ui.Update();
        using var visible = ui.SnapshotRasterImage();
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(visible, 15, 15));
        Assert.Equal(0, Pixel(visible, 5, 15)[3]);
        Assert.Equal(0, Pixel(visible, 35, 15)[3]);

        child.IsVisible = false;
        Assert.True(ui.Update());
        using var hidden = ui.SnapshotRasterImage();
        Assert.Equal(0, Pixel(hidden, 15, 15)[3]);
        Assert.Equal(255, Pixel(visible, 15, 15)[3]);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void Dpi_UsesLogicalLayoutAndPhysicalPixels(float scale)
    {
        var root = new Panel(); root.Add(Patch(new Color(0, 0, 255), 20, 15));
        using var ui = Create(root, new UiViewport(120, 80, scale)); ui.Update();
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(new Rect(0, 0, 120 / scale, 80 / scale), root.Bounds);
        Assert.Equal(120, image.Width); Assert.Equal(80, image.Height);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(image, (int)(15 * scale), (int)(14 * scale)));
        Assert.Equal(0, Pixel(image, (int)(35 * scale), (int)(14 * scale))[3]);
    }

    [Fact]
    public void Update_DrawsOnlyOnInvalidationAndKeepsPreviousSnapshotImmutable()
    {
        var root = new CountingFill { Foreground = new Color(255, 0, 0) };
        using var ui = Create(root);
        Assert.Throws<InvalidOperationException>(() => ui.SnapshotRasterImage());
        Assert.True(ui.Update());
        using var first = ui.SnapshotRasterImage();
        Assert.False(ui.Update()); Assert.False(ui.Update());
        Assert.Equal(1, root.Draws);

        root.Foreground = new Color(0, 0, 255);
        Assert.Throws<InvalidOperationException>(() => ui.SnapshotRasterImage());
        Assert.True(ui.Update());
        using var second = ui.SnapshotRasterImage();
        Assert.Equal(2, root.Draws);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(first, 20, 20));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(second, 20, 20));
    }

    [Fact]
    public void Resize_SuspendsAndRestoresWhileSnapshotsOutliveSession()
    {
        var root = new CountingFill { Foreground = new Color(255, 0, 0) };
        using var ui = Create(root, new UiViewport(64, 48)); ui.Update();
        using var first = ui.SnapshotRasterImage();
        ui.Resize(new UiViewport(96, 64));
        Assert.Throws<InvalidOperationException>(() => ui.SnapshotRasterImage());
        Assert.True(ui.Update());
        using var resized = ui.SnapshotRasterImage();
        Assert.Equal(96, resized.Width); Assert.Equal(64, resized.Height);

        ui.Resize(new UiViewport(0, 64));
        Assert.False(ui.Update());
        Assert.Throws<InvalidOperationException>(() => ui.SnapshotRasterImage());
        ui.Resize(new UiViewport(32, 16)); Assert.True(ui.Update());
        using var restored = ui.SnapshotRasterImage();
        ui.Dispose(); ui.Dispose();
        Assert.True(root.IsDisposed);
        Assert.Throws<ObjectDisposedException>(ui.Rendering.VerifyAvailable);
        Assert.Throws<ObjectDisposedException>(() => ui.SnapshotRasterImage());
        Assert.Equal(64, first.Width); Assert.Equal(48, first.Height);
        Assert.Equal(32, restored.Width); Assert.Equal(16, restored.Height);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(first, 20, 20));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(resized, 20, 20));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, Pixel(restored, 10, 10));
    }

    [Fact]
    public void ScaleChange_WithSamePixelSizeRedrawsLogicalGeometry()
    {
        var root = new Panel(); root.Add(Patch(new Color(0, 0, 255), 20, 15));
        using var ui = Create(root); ui.Update();
        using var first = ui.SnapshotRasterImage();
        Assert.Equal(0, Pixel(first, 45, 25)[3]);
        ui.Resize(new UiViewport(120, 80, 2));
        Assert.True(ui.Update());
        using var second = ui.SnapshotRasterImage();
        Assert.Equal(new Rect(0, 0, 60, 40), root.Bounds);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(second, 45, 25));
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(120, 0)]
    public void InitialEmptyViewport_CanRestoreWithoutReplacingSession(int width, int height)
    {
        var root = new CountingFill { Foreground = new Color(0, 0, 255) };
        using var ui = Create(root, new UiViewport(width, height));
        Assert.False(ui.Update()); Assert.Equal(0, root.Draws);
        Assert.Throws<InvalidOperationException>(() => ui.SnapshotRasterImage());
        ui.Resize(new UiViewport(40, 30));
        Assert.True(ui.Update()); Assert.Equal(1, root.Draws);
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(40, image.Width); Assert.Equal(30, image.Height);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(image, 20, 20));
    }

    [Theory]
    [InlineData(-1, 10, 1f)]
    [InlineData(10, -1, 1f)]
    [InlineData(10, 10, 0f)]
    [InlineData(10, 10, float.NaN)]
    public void InvalidViewport_IsRejectedBeforeSurfaceMutation(int width, int height, float scale)
    {
        using var ui = Create(new Panel { Background = new Color(0, 0, 255) }); ui.Update();
        var invalid = new UiViewport(width, height, scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => ui.Resize(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), invalid));
        Assert.Equal(new UiViewport(120, 80), ui.Viewport);
        Assert.False(ui.Update());
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(image, 20, 20));
    }

    [Fact]
    public void Backend_RejectsForeignTargetAndUnrenderedOrMismatchedSurface()
    {
        var backend = new SkiaRasterRenderBackend();
        Assert.Throws<ArgumentException>(() => backend.CreateSurface(new ForeignTarget(), new UiViewport(10, 10)));
        using var surface = (SkiaRasterSurface)backend.CreateSurface(new RasterUiTarget(), new UiViewport(10, 10));
        using var root = new Panel();
        Assert.Throws<InvalidOperationException>(() => surface.Snapshot());
        Assert.Throws<ArgumentException>(() => surface.Render(root, new UiViewport(20, 20)));
        Assert.Throws<ArgumentNullException>(() => surface.Render(null!, new UiViewport(10, 10)));
    }

    [Fact]
    public void Drawing_ReentrantOperationsAreRejectedAndFailureCanBeRetried()
    {
        var root = new CallbackFill();
        using var ui = Create(root, new UiViewport(120, 80, 2));
        var surface = (SkiaRasterSurface)ui.Rendering;
        var failure = new InvalidOperationException("draw failure");
        root.Callback = () =>
        {
            Assert.Throws<InvalidOperationException>(() => surface.Resize(new UiViewport(60, 40)));
            Assert.Throws<InvalidOperationException>(surface.Dispose);
            Assert.Throws<InvalidOperationException>(() => surface.Snapshot());
            throw failure;
        };
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => ui.Update()));
        Assert.True(ui.NeedsUpdate);
        Assert.Throws<InvalidOperationException>(() => surface.Snapshot());
        root.Callback = null;
        Assert.True(ui.Update());
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(image, 100, 60));
    }

    [Fact]
    public void Input_PointerCaptureAndKeyboardNavigationUseSameSession()
    {
        var root = new Panel(); var first = ButtonAt(); var second = ButtonAt(100);
        root.Add(first).Add(second);
        var firstClicks = 0; var secondClicks = 0;
        first.Click += () => firstClicks++; second.Click += () => secondClicks++;
        using var ui = Create(root, new UiViewport(200, 80)); ui.Update();
        Assert.Equal(new UiInputResult(true, true, true), ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)));
        Assert.True(first.IsPressed); Assert.True(first.IsFocused);
        Assert.Equal(new UiInputResult(true, false, true), ui.HandleInput(new UiInput(InputKind.PointerUp, 190, 70)));
        Assert.False(first.IsPressed); Assert.Equal(0, firstClicks);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Enter));
        ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Enter));
        Assert.Equal(1, firstClicks);
        Assert.True(ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab)).Handled);
        Assert.False(first.IsFocused); Assert.True(second.IsFocused);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Space));
        ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Space));
        Assert.Equal(1, secondClicks);
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Tab, Shift: true));
        Assert.True(first.IsFocused); Assert.False(second.IsFocused);
    }

    [Fact]
    public void Input_DetachCancelsFocusAndCapture()
    {
        using var button = ButtonAt(); var root = new Panel(); root.Add(button);
        var clicks = 0; button.Click += () => clicks++;
        using var ui = Create(root); ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10));
        Assert.True(button.IsPressed); Assert.True(button.IsFocused);
        root.Items.Remove(button); ui.Update();
        Assert.False(button.IsPressed); Assert.False(button.IsFocused);
        Assert.Equal(new UiInputResult(false, false, false), ui.HandleInput(new UiInput(InputKind.PointerUp, 10, 10)));
        Assert.Equal(0, clicks); Assert.False(button.IsDisposed);
    }

    [Fact]
    public void Input_OverlapHitsTopmostAndDisabledChildPassesThrough()
    {
        var root = new Panel(); var lower = ButtonAt(); var upper = ButtonAt(); root.Add(lower).Add(upper);
        var lowerClicks = 0; var upperClicks = 0;
        lower.Click += () => lowerClicks++; upper.Click += () => upperClicks++;
        using var ui = Create(root); ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)); ui.HandleInput(new UiInput(InputKind.PointerUp, 10, 10));
        Assert.Equal(0, lowerClicks); Assert.Equal(1, upperClicks);
        upper.IsEnabled = false; ui.Update();
        ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)); ui.HandleInput(new UiInput(InputKind.PointerUp, 10, 10));
        Assert.Equal(1, lowerClicks); Assert.Equal(1, upperClicks); Assert.True(lower.IsFocused);
    }

    [Fact]
    public void ThemeAndPressedState_ChangeRasterButtonPixels()
    {
        var root = new Panel(); var button = ButtonAt(); root.Add(button);
        using var ui = Create(root); ui.Update();
        using var dark = ui.SnapshotRasterImage();
        var color = Theme.Dark.Control;
        Assert.Equal(new byte[] { color.R, color.G, color.B, color.A }, Pixel(dark, 74, 20));
        ui.SetTheme(Theme.Light); ui.Update();
        using var light = ui.SnapshotRasterImage();
        color = Theme.Light.Control;
        Assert.Equal(new byte[] { color.R, color.G, color.B, color.A }, Pixel(light, 74, 20));
        ui.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)); ui.Update();
        using var pressed = ui.SnapshotRasterImage(); color = Theme.Light.Accent;
        Assert.Equal(new byte[] { color.R, color.G, color.B, color.A }, Pixel(pressed, 74, 20));
        ui.HandleInput(new UiInput(InputKind.FocusLost)); ui.Update();
        Assert.False(button.IsPressed); Assert.False(button.IsFocused);
        using var cancelled = ui.SnapshotRasterImage(); color = Theme.Light.Control;
        Assert.Equal(new byte[] { color.R, color.G, color.B, color.A }, Pixel(cancelled, 74, 20));
    }

    [Theory]
    [InlineData("Hello")]
    [InlineData("Привет")]
    public void Text_MeasuresAndDrawsLatinAndCyrillic(string value)
    {
        var root = new Panel(); var text = new Text(value) { FontFamily = "Segoe UI", FontSize = 20,
            Foreground = new Color(255, 255, 255), Margin = new Thickness(8),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        root.Add(text);
        using var ui = Create(root, new UiViewport(180, 60)); ui.Update();
        Assert.InRange(text.Bounds.Width, 30, 160); Assert.InRange(text.Bounds.Height, 15, 40);
        using var image = ui.SnapshotRasterImage(); var pixels = Pixels(image);
        var opaqueGlyphPixels = 0;
        for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] > 200) opaqueGlyphPixels++;
        Assert.True(opaqueGlyphPixels > 20, "Expected visible Latin/Cyrillic glyphs on a transparent raster surface.");
        Assert.Equal(0, Pixel(image, 179, 59)[3]);
    }

    [Fact]
    public void Thread_ForeignAccessIsRejectedAndPostRunsAtNextUpdate()
    {
        var root = new CountingFill { Foreground = new Color(255, 0, 0) };
        using var ui = Create(root); ui.Update();
        Exception? snapshotError = null, resizeError = null, disposeError = null;
        var worker = new Thread(() =>
        {
            snapshotError = Record.Exception(() => ui.SnapshotRasterImage());
            resizeError = Record.Exception(() => ui.Rendering.Resize(new UiViewport(60, 40)));
            disposeError = Record.Exception(ui.Rendering.Dispose);
            ui.Post(() => root.Foreground = new Color(0, 0, 255));
        });
        worker.Start(); worker.Join();
        Assert.IsType<InvalidOperationException>(snapshotError);
        Assert.IsType<InvalidOperationException>(resizeError);
        Assert.IsType<InvalidOperationException>(disposeError);
        Assert.Equal(new Color(255, 0, 0), root.Foreground);
        Assert.True(ui.Update());
        using var image = ui.SnapshotRasterImage();
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(image, 20, 20));
    }

    private sealed class ForeignTarget : UiRenderTarget;
    private sealed class OverflowFill : Element
    { protected override void DrawCore(IDrawingContext context) => context.Fill(new Rect(-100, -100, 300, 300), new Color(255, 0, 0)); }
    private sealed class CountingFill : Element
    {
        internal int Draws { get; private set; }
        protected override void DrawCore(IDrawingContext context) { Draws++; context.Fill(Bounds, Foreground); }
    }
    private sealed class CallbackFill : Element
    {
        internal Action? Callback { get; set; }
        protected override void DrawCore(IDrawingContext context) { Callback?.Invoke(); context.Fill(Bounds, new Color(0, 0, 255)); }
    }
}
