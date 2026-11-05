using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class RichTextContentHeightTests
{
    private static readonly Lazy<SkiaFontCatalog> Fonts = new(() => SkiaFontCatalog.Load("HeightTests",
        new Dictionary<string, string> { ["HeightTests"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") }));

    [Fact]
    public void AutoHeight_UsesActualWrappedContentWithinStyledBoundsWithoutReplacingDraftHistoryOrSelection()
    {
        var editor = new RichTextEditor(new() { WordWrap = true }) { Text = "one", FontSize = 12, MinHeight = 0, MinWidth = 0, Padding = new(4), MinimumContentHeight = 18, MaximumContentHeight = 70 };
        var row = new VStack().WithChildren(editor);
        using var ui = UiSession.Create(row, new SkiaRasterRenderBackend(Fonts.Value), new RasterUiTarget(), new(500, 200)); ui.Update(); ui.Focus(editor);
        Assert.Equal(26, editor.Bounds.Height); Assert.Equal(26, editor.DesiredSize.Height);
        editor.SelectAll(); editor.InsertText(string.Join("\n", Enumerable.Repeat("line", 12))); ui.Update();
        Assert.Equal(78, editor.Bounds.Height); Assert.True(editor.CanUndo); Assert.Equal(editor.Text.Length, editor.CaretIndex);
        var document = editor.Document; editor.Select(2, 7); var selection = editor.SelectedText;
        ui.SetTheme(Theme.Light); ui.Resize(new(160, 200)); ui.Update();
        Assert.Same(editor, Assert.Single(row.Children)); Assert.Same(document, editor.Document);
        Assert.Equal(78, editor.Bounds.Height); Assert.True(editor.IsFocused); Assert.Equal(selection, editor.SelectedText);
        Assert.True(editor.Undo()); ui.Update(); Assert.Equal("one", editor.Text); Assert.Equal(26, editor.Bounds.Height);
        editor.SelectAll(); editor.InsertText(string.Concat(Enumerable.Repeat("word ", 45))); ui.Resize(new(1000, 200)); ui.Update();
        var wide = editor.Bounds.Height; ui.Resize(new(160, 200)); ui.Update();
        Assert.InRange(wide, 26, 77.99f); Assert.Equal(78, editor.Bounds.Height); Assert.Equal(0, editor.HorizontalOffset);
    }

    [Fact]
    public void ContentCeilingAndAvailableSpace_TakePrecedenceOverMinimumAndExplicitHeightStillWins()
    {
        using var editor = new RichTextEditor { Text = "short", MinHeight = 0, Padding = new(4), MinimumContentHeight = 80, MaximumContentHeight = 18 };
        using var ui = UiSession.Create(new VStack().WithChildren(editor), new SkiaRasterRenderBackend(Fonts.Value), new RasterUiTarget(), new(300, 200)); ui.Update();
        Assert.Equal(26, editor.DesiredSize.Height);
        editor.Height = 42; ui.Update(); Assert.Equal(42, editor.Bounds.Height); Assert.Equal("short", editor.Text);
        editor.Clear(UiProperties.Height); editor.Clear(RichTextEditor.MaximumContentHeightProperty);
        editor.Measure(new(300, 20), ui.Rendering.TextLayout); Assert.Equal(20, editor.DesiredSize.Height);
        editor.Measure(new(300, float.PositiveInfinity), ui.Rendering.TextLayout); Assert.Equal(88, editor.DesiredSize.Height);
        Assert.Equal(float.PositiveInfinity, editor.MaximumContentHeight); Assert.Equal(80, editor.MinimumContentHeight);
    }

    [Theory]
    [InlineData(-1f)] [InlineData(-2f)] [InlineData(float.NaN)] [InlineData(float.NegativeInfinity)]
    public void InvalidHeightBounds_RejectLocalAndStyleWithoutMutationOrInvalidation(float invalid)
    {
        using var editor = new RichTextEditor(); var calls = 0; editor.Invalidated += _ => calls++;
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MinimumContentHeight = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MaximumContentHeight = invalid);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(RichTextEditor.MinimumContentHeightProperty, invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Style().With(RichTextEditor.MaximumContentHeightProperty, invalid));
        Assert.Equal(80, editor.MinimumContentHeight); Assert.Equal(float.PositiveInfinity, editor.MaximumContentHeight); Assert.Equal(0, calls);
    }

    [Fact]
    public void HeightZeroAndInfinityPartitions_AllowZeroMinimumAndUnboundedCeilingButRejectTheirOpposites()
    {
        using var editor = new RichTextEditor { MinimumContentHeight = 0, MaximumContentHeight = float.PositiveInfinity };
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MaximumContentHeight = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.MinimumContentHeight = float.PositiveInfinity);
        Assert.Equal(0, editor.MinimumContentHeight); Assert.Equal(float.PositiveInfinity, editor.MaximumContentHeight);
        Assert.False(editor.IsDisposed); Assert.Equal("", editor.Text);
    }
}
