using TrueMoon.Alloy.Platform.Silk;
using Xunit;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace TrueMoon.Alloy.Tests;

public sealed class WindowDpiTests
{
    [Theory]
    [InlineData(96u, 1f, 600, 400)]
    [InlineData(144u, 1.5f, 900, 600)]
    [InlineData(192u, 2f, 1200, 800)]
    public void Scale_UsesNativeDpiEvenWhenFramebufferEqualsClient(uint dpi, float scale, int width, int height)
    {
        var native = new WindowPixelScale(dpi);
        var viewport = new UiViewport(width, height, native.Scale);
        Assert.Equal(scale, viewport.Scale);
        Assert.Equal(600, native.ToLogical(viewport.Width));
        Assert.Equal(400, native.ToLogical(viewport.Height));
        Assert.Equal(width, native.RoundPixels(600));
        Assert.Equal(height, native.RoundPixels(400));
    }
    [Theory]
    [InlineData(96u, 1f)]
    [InlineData(144u, 1.5f)]
    [InlineData(192u, 2f)]
    public void Pointer_PreservesLogicalHitTargetsAndNegativeCapturedCoordinates(uint dpi, float scale)
    {
        var native = new WindowPixelScale(dpi);
        var button = new TrueMoon.Argentis.Rect(20, 30, 100, 40);
        Assert.True(button.Contains(native.ToLogical(70 * scale), native.ToLogical(50 * scale)));
        Assert.False(button.Contains(native.ToLogical(120 * scale), native.ToLogical(50 * scale)));
        Assert.Equal(-20, native.ToLogical(-20 * scale));
        Assert.Equal(-30, native.ToLogical(-30 * scale));
    }
    [Theory]
    [InlineData(96u, 201, 700, 401, 200, 700)]
    [InlineData(144u, 301, 1051, 601, 300, 1050)]
    [InlineData(192u, 401, 1401, 801, 400, 1400)]
    public void Limits_FractionalSizesRoundInward_ResizeRoundsNearest(uint dpi, int minimum, int maximum, int resized, int exactMinimum, int exactMaximum)
    {
        var native = new WindowPixelScale(dpi);
        Assert.Equal(minimum, native.MinimumPixels(200.01f));
        Assert.Equal(maximum, native.MaximumPixels(700.99f));
        Assert.Equal(resized, native.RoundPixels(400.5f));
        Assert.Equal(exactMinimum, native.MinimumPixels(200));
        Assert.Equal(exactMaximum, native.MaximumPixels(700));
    }
    [Fact]
    public void Session_DpiChangeWithoutPixelResizeRetainsFocusAndSelection()
    {
        var editor = new TextBox { Value = "abcdef", Width = 180, Height = 36,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        var root = new Panel(); root.Add(editor);
        using var session = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new UiViewport(400, 240));
        session.Update(); session.Focus(editor);
        session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
        session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
        for (var i = 0; i < 3; i++) session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right, Shift: true));

        session.Resize(new UiViewport(400, 240, new WindowPixelScale(192).Scale));
        Assert.True(session.Update());
        Assert.Equal(new Rect(0, 0, 200, 120), root.Bounds);
        Assert.Same(editor, root.Children[0]);
        Assert.True(editor.IsFocused);
        Assert.Equal(1, editor.SelectionStart); Assert.Equal(3, editor.SelectionLength);
        session.HandleInput(new UiInput(InputKind.Text, Text: "X"));
        Assert.Equal("aXef", editor.Value);
    }
    [Fact]
    public void Transition_ResizesPhysicalClientAndRetainsLogicalSize()
    {
        var normal = new WindowPixelScale(96);
        var high = new WindowPixelScale(192);
        Assert.Equal(1200, high.RoundPixels(normal.ToLogical(600)));
        Assert.Equal(800, high.RoundPixels(normal.ToLogical(400)));
        Assert.Equal(600, normal.RoundPixels(high.ToLogical(1200)));
        Assert.Equal(400, normal.RoundPixels(high.ToLogical(800)));
    }
    [Fact]
    public void InvalidDpiAndUnrepresentableDimensionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowPixelScale(0));
        var native = new WindowPixelScale(192);
        Assert.Throws<ArgumentOutOfRangeException>(() => native.RoundPixels(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => native.MinimumPixels(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => native.MaximumPixels(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => native.RoundPixels(int.MaxValue));
        Assert.Equal(0, native.RoundPixels(0));
    }
}
