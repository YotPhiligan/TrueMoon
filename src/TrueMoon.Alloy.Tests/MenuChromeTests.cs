using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class MenuChromeTests
{
    [Fact]
    public void CustomHeader_PreservesStandardButtonPositionsAndNativeRegions()
    {
        using var title = new WindowTitleBar("Project");
        title.ApplyState(new(true, false, true, true, true)); Arrange(title);
        var minimize = title.MinimizeButton.Bounds; var maximize = title.MaximizeButton.Bounds; var close = title.CloseButton.Bounds;
        var header = new HStack { Spacing = 0, WindowRegion = WindowRegionRole.Caption };
        var action = new IconButton(UiSymbol.Play, "Run") { Width = 32, Height = 32 };
        var caption = new Text("Project name") { Width = 150 };
        header.Add(action); header.Add(caption); title.SetHeader(header); Arrange(title);
        Assert.Same(header, title.HeaderContent); Assert.Same(title, header.Parent);
        Assert.Equal(minimize, title.MinimizeButton.Bounds); Assert.Equal(maximize, title.MaximizeButton.Bounds);
        Assert.Equal(close, title.CloseButton.Bounds);
        Assert.True(header.Bounds.X + header.Bounds.Width <= minimize.X);
        var map = WindowRegionMap.Create(title);
        Assert.Equal(WindowRegionRole.Client, map.HitTest(action.Bounds.X + 10, 18));
        Assert.Equal(WindowRegionRole.Caption, map.HitTest(caption.Bounds.X + 10, 18));
        Assert.Equal(WindowRegionRole.Maximize, map.HitTest(maximize.X + 10, 18));
        Assert.Equal(WindowRegionRole.Client, map.HitTest(close.X + 10, 18));
        title.ApplyState(new(true, true, true, true, true));
        Assert.Equal(UiSymbol.Restore, title.MaximizeButton.Symbol);
        Assert.Same(header, title.HeaderContent); Assert.False(action.IsDisposed);
    }

    [Fact]
    public void HeaderReplacement_ValidatesBeforeRemovingAndTransfersOldContentToCaller()
    {
        using var title = new WindowTitleBar("Project"); var first = new SizedElement();
        title.SetHeader(first);
        using var other = new Panel(); var owned = new SizedElement(); other.Add(owned);
        Assert.Throws<InvalidOperationException>(() => title.SetHeader(owned));
        Assert.Same(first, title.HeaderContent); Assert.Same(title, first.Parent);
        var second = new SizedElement(); title.SetHeader(second);
        Assert.Null(first.Parent); Assert.False(first.IsDisposed); Assert.Same(second, title.HeaderContent);
        title.SetHeader(null); Assert.Null(title.HeaderContent); Assert.Null(second.Parent); Assert.False(second.IsDisposed);
        first.Dispose(); second.Dispose();
        var final = new SizedElement(); title.SetHeader(final); title.Dispose(); Assert.True(final.IsDisposed);
    }

    [Theory]
    [InlineData(UiSymbol.More, 3)]
    [InlineData(UiSymbol.Play, 5)]
    [InlineData(UiSymbol.Pause, 2)]
    [InlineData(UiSymbol.Stop, 1)]
    public void CompactSymbols_UseGeometricMarksWithNoFontGlyphs(UiSymbol symbol, int marks)
    {
        var drawing = new SymbolDrawing();
        UiSymbols.Draw(drawing, symbol, new(30, 40, 24, 18), new(180, 210, 240));
        Assert.Equal(marks, drawing.Marks.Count); Assert.Equal(0, drawing.TextCalls);
        Assert.All(drawing.Marks, mark =>
        {
            Assert.InRange(mark.X, 30, 54); Assert.InRange(mark.Y, 40, 58);
            Assert.True(mark.Width > 0); Assert.True(mark.Height > 0);
            Assert.True(mark.X + mark.Width <= 54); Assert.True(mark.Y + mark.Height <= 58);
        });
    }

    [Fact]
    public void TrueMoonSymbol_PaintsCrescentAndLeavesTheCutoutTransparent()
    {
        var drawing = new SymbolDrawing();
        UiSymbols.Draw(drawing, UiSymbol.TrueMoon, new(0, 0, 12, 12), new(80, 150, 240));
        Assert.Contains(drawing.Marks, mark => mark.Contains(2, 6));
        Assert.DoesNotContain(drawing.Marks, mark => mark.Contains(7, 4));
        Assert.Equal(0, drawing.TextCalls);
        Assert.All(drawing.Marks, mark =>
        {
            Assert.True(mark.Width > 0 && mark.Height > 0);
            Assert.InRange(mark.X, 0, 12); Assert.InRange(mark.Y, 0, 12);
            Assert.True(mark.X + mark.Width <= 12 && mark.Y + mark.Height <= 12);
        });
    }

    [Fact]
    public void EverySymbol_DrawsAtSmallAndLargeScaleAndRejectsUnknownValues()
    {
        foreach (var symbol in Enum.GetValues<UiSymbol>())
        foreach (var size in new[] { 12f, 24f, 48f })
        {
            var drawing = new SymbolDrawing(); UiSymbols.Draw(drawing, symbol, new(0, 0, size, size), new(255, 255, 255));
            Assert.NotEmpty(drawing.Marks); Assert.Equal(0, drawing.TextCalls);
            Assert.All(drawing.Marks, mark => Assert.True(float.IsFinite(mark.X + mark.Y + mark.Width + mark.Height)));
        }
        var invalid = new SymbolDrawing();
        Assert.Throws<ArgumentOutOfRangeException>(() => UiSymbols.Draw(invalid, (UiSymbol)int.MaxValue, new(0, 0, 12, 12), default));
        Assert.Throws<ArgumentOutOfRangeException>(() => UiSymbols.Draw(invalid, UiSymbol.More, new(0, 0, -1, 12), default));
        UiSymbols.Draw(invalid, UiSymbol.More, new(0, 0, 0, 12), default); Assert.Empty(invalid.Marks);
    }

    private static void Arrange(Element root)
    { root.Measure(new(500, 36), new FixedTextLayout()); root.Arrange(new(0, 0, 500, 36)); }

    private sealed class SymbolDrawing : IDrawingContext
    {
        public List<Rect> Marks { get; } = [];
        public int TextCalls { get; private set; }
        public void Save() { }
        public void Restore() { }
        public void Clip(Rect rectangle) { }
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Marks.Add(rectangle);
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Marks.Add(rectangle);
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => TextCalls++;
        public void Image(IImageSource source, Rect destination) { }
    }
}
