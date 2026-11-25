using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Styling;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Alloy.Runtime.Semantics;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class TextBoxViewportTests
{
    [Theory]
    [InlineData(1f, false)] [InlineData(1.5f, false)] [InlineData(2f, false)]
    [InlineData(1f, true)] [InlineData(1.5f, true)] [InlineData(2f, true)]
    public void CompactTextBox_PreservesCompleteGlyphsAndSelectionLikeSpaciousField(float scale, bool numeric)
    {
        foreach (var light in new[] { false, true })
        foreach (var error in new[] { false, true })
        foreach (var selected in new[] { false, true })
        {
            using var compact = Capture(32, scale, numeric, light, error, selected);
            using var spacious = Capture(48, scale, numeric, light, error, selected);
            // Both lines are vertically centered. Eight extra logical pixels above
            // and below the spacious field must not change glyphs or selection.
            var shift = (int)(8 * scale); var different = 0; (int X, int Y)? first = null;
            for (var y = (int)(4 * scale); y < (int)(28 * scale); y++)
            for (var x = (int)((compact.X + 8) * scale); x < (int)((compact.X + 200) * scale); x++)
                if (compact.Bitmap.GetPixel(x, y) != spacious.Bitmap.GetPixel(x, y + shift))
                { different++; first ??= (x, y); }
            Assert.True(different == 0, $"Clipped/shifted line: scale={scale}, numeric={numeric}, light={light}, error={error}, selected={selected}, pixels={different}, first={first}");
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, true)]
    public void ResizeBeforeClick_UsesNewScrolledLineWithoutPainting(bool shaped, bool? editorSubpixel)
    {
        var field = new TextBox { Value = new string('M', 32), UseShapedText = shaped, SemanticId = "field", Height = 32 };
        using var ui = UiSession.Create(field, new SkiaRasterRenderBackend { EditorSubpixelPositioning = editorSubpixel }, new RasterUiTarget(), new(800, 32));
        ui.Update(); ui.Focus(field);
        ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.End)); ui.Update();
        Assert.Equal(0, field.HorizontalOffset);
        var old = ui.CaptureSemantics();
        var diagnostics = ui.EnableDiagnostics();
        ui.Resize(new(120, 32));
        var click = new UiInput(InputKind.PointerDown, 119, 16);
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, ui.HandleSemanticInput(old, "field", click).Status);
        // The caret was visible without scrolling at the old width. The new right edge is
        // still the end of the line only if arrange has revealed the caret before hit testing.
        Assert.True(ui.HandleInput(click).Handled);
        Assert.Equal(120, field.Bounds.Width);
        Assert.Equal(field.Value.Length, field.CaretIndex);
        Assert.True(field.HorizontalOffset > 0);
        Assert.Equal(UiSemanticTargetStatus.StaleSnapshot, ui.HandleSemanticInput(old, "field", click).Status);
        Assert.True(ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Left, Shift: true)).Handled);
        var offset = field.HorizontalOffset;
        AssertSelection();
        Assert.Equal(0, diagnostics.Snapshot.Render.Attempts);
        Assert.Equal(0, diagnostics.Snapshot.Update.Attempts);
        Assert.True(ui.NeedsRender);
        Assert.True(ui.Update());
        using var first = Pixels();
        ui.RequestFrame(); Assert.True(ui.Update());
        using var repeated = Pixels();
        Assert.Equal(first.Pixels, repeated.Pixels);
        Assert.Equal(1, diagnostics.Snapshot.Measure.Attempts);
        Assert.Equal(1, diagnostics.Snapshot.Arrange.Attempts);
        AssertSelection();

        ui.HandleInput(new(InputKind.FocusLost)); ui.Update();
        using var unfocused = Pixels();
        // Ignore borders: selection and caret must change actual pixels inside the text viewport.
        bool TextPixelsDiffer(int x) => Enumerable.Range(5, 22).Any(y => unfocused.GetPixel(x, y) != first.GetPixel(x, y));
        Assert.Contains(Enumerable.Range(80, 32), TextPixelsDiffer);
        Assert.DoesNotContain(Enumerable.Range(8, 64), TextPixelsDiffer);
        ui.Focus(field); ui.Update();
        using var refocused = Pixels();
        Assert.Equal(first.Pixels, refocused.Pixels);
        AssertSelection();
        Assert.False(ui.Update());

        SKBitmap Pixels() { using var image = ui.SnapshotRasterImage(); return SKBitmap.FromImage(image); }
        void AssertSelection()
        {
            Assert.Equal(field.Value.Length - 1, field.CaretIndex);
            Assert.Equal(field.CaretIndex, field.SelectionStart);
            Assert.Equal(1, field.SelectionLength);
            Assert.Equal(offset, field.HorizontalOffset);
        }
    }

    [Fact]
    public void DefaultAutoHeight_StillReservesFontLineAndConfiguredPadding()
    {
        var field = new TextBox { Value = "Agpqy дцру Йё" };
        using var ui = UiSession.Create(new VStack().WithChildren(field), new SkiaRasterRenderBackend(), new RasterUiTarget(), new(400, 200));
        ui.Update();
        var line = ui.Rendering.TextLayout.Measure("Mg", field.FontSize, field.FontFamily, field.FontWeight, field.FontItalic).Height;
        Assert.Equal(line + field.Padding.Vertical, field.Bounds.Height);
        Assert.Equal(new Thickness(8), field.Padding); Assert.Equal("Agpqy дцру Йё", field.Value); Assert.False(ui.Update());
    }

    private static Snapshot Capture(float height, float scale, bool numeric, bool light, bool error, bool selected)
    {
        Element root; TextBox field;
        if (numeric) { var box = new NumberBox("X") { Width = 320, Height = height }; root = box; field = box.Editor; }
        else { field = new TextBox { Width = 320, Height = height }; root = field; }
        field.Value = "Agpqy дцру Йё"; field.HasError = error; field.ErrorMessage = error ? "Rejected draft" : "";
        root.HorizontalAlignment = LayoutAlignment.Start; root.VerticalAlignment = LayoutAlignment.Start;
        using var ui = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), new((int)(380 * scale), (int)(80 * scale), scale));
        ui.SetTheme(light ? Theme.Light : Theme.Dark); ui.Update(); ui.Focus(field);
        if (selected) ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        ui.Update();
        Assert.Equal("Agpqy дцру Йё", field.Value); Assert.Equal(selected ? field.Value.Length : 0, field.SelectionLength); Assert.True(field.IsFocused);
        Assert.Equal(height, field.Bounds.Height); Assert.False(ui.Update());
        using var image = ui.SnapshotRasterImage();
        return new(SKBitmap.FromImage(image), field.Bounds.X);
    }

    private sealed record Snapshot(SKBitmap Bitmap, float X) : IDisposable
    { public void Dispose() => Bitmap.Dispose(); }
}
