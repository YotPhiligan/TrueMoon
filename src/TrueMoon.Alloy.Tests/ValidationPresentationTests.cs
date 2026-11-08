using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using Xunit;

namespace TrueMoon.Alloy.Tests;

public sealed class ValidationPresentationTests
{
    [Theory]
    [InlineData(false, 1f)] [InlineData(false, 1.5f)] [InlineData(false, 2f)]
    [InlineData(true, 1f)] [InlineData(true, 1.5f)] [InlineData(true, 2f)]
    public void ErrorIcon_ReservesRightEdgeWithoutChangingDraftSelectionOrFocus(bool rich, float scale)
    {
        using var t = new Fixture(rich, scale); t.Ui.Focus(t.Field);
        t.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.A, Control: true));
        var before = t.Field.Bounds; t.Field.HasError = true; t.Ui.Update();
        var icon = t.Field.ValidationIconBounds;
        Assert.Equal(new Rect(before.X + before.Width - 40, before.Y, 40, 40), icon);
        Assert.Equal(before, t.Field.Bounds); Assert.Equal("rejected draft", t.Value); Assert.Equal(14, t.SelectionLength);
        Assert.True(t.Field.IsFocused);
        t.Ui.HandleInput(new(InputKind.PointerDown, icon.X + 7, icon.Y + 7));
        t.Ui.HandleInput(new(InputKind.PointerUp, icon.X + 7, icon.Y + 7));
        Assert.Equal(14, t.SelectionLength); Assert.True(t.Field.IsFocused);
        t.Ui.Update();
        var drawing = new Drawing(); t.Field.Draw(drawing);
        Assert.True(drawing.TextClip.X + drawing.TextClip.Width <= icon.X - 4);
        Assert.True(drawing.TextClip.Width > 0);
        using var image = t.Ui.SnapshotRasterImage(); using var bitmap = SKBitmap.FromImage(image);
        var count = 0;
        for (var y = (int)(icon.Y * scale); y < (icon.Y + icon.Height) * scale; y++)
            for (var x = (int)(icon.X * scale); x < (icon.X + icon.Width) * scale; x++)
            { var p = bitmap.GetPixel(x, y); if (p.Red > 180 && p.Red - p.Green > 60 && p.Red - p.Blue > 40) count++; }
        Assert.True(count > 12, $"Missing red error icon at scale {scale}: {count} pixels");
        t.Ui.SetTheme(Theme.Light); t.Ui.Update(); Assert.True(t.Field.IsFocused); Assert.Equal(14, t.SelectionLength);
        t.Field.HasError = false; t.Field.ErrorMessage = ""; t.Ui.Update();
        Assert.Equal(default, t.Field.ValidationIconBounds); Assert.Equal(before, t.Field.Bounds); Assert.Equal("rejected draft", t.Value);
    }

    [Fact]
    public void NumberBox_UsesInnerEditorErrorWithoutNormalizingRejectedDraft()
    {
        var box = new NumberBox("X") { Width = 180, Height = 30 };
        box.Editor.Value = "-"; box.Editor.HasError = true; box.Editor.ErrorMessage = "Введите конечное число с точкой";
        using var ui = UiSession.Create(new OverlayHost(box), new SkiaRasterRenderBackend(), new RasterUiTarget(), new(300, 100)); ui.Update();
        Assert.Equal("-", box.Editor.Value); Assert.Equal("Введите конечное число с точкой", box.Editor.ErrorMessage);
        Assert.Equal(box.Editor.Bounds.X + box.Editor.Bounds.Width - 30, box.Editor.ValidationIconBounds.X);
        Assert.True(box.Editor.ValidationIconBounds.Width > 0); Assert.Equal(30, box.Bounds.Height);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 8)] [InlineData(false, 16)]
    [InlineData(true, 0)] [InlineData(true, 8)] [InlineData(true, 16)]
    public void NarrowErrorField_ClampsTextViewportWithoutThrowingOrLosingDraft(bool rich, float width)
    {
        using var t = new Fixture(rich); t.Field.Width = width; t.Field.HasError = true; t.Ui.Focus(t.Field);
        t.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.End)); t.Ui.Update();
        Assert.Equal("rejected draft", t.Value); Assert.True(t.Field.IsFocused);
        var icon = t.Field.ValidationIconBounds; Assert.Equal(width, icon.Width);
        if (width == 0) Assert.Equal(default, icon);
        Assert.False(t.Ui.Update());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ValidationBadge_FillsEntireSquareWithContrastingCenteredSymbolAndSeparateFocus(bool light)
    {
        using var t = new Fixture(); var theme = light ? Theme.Light : Theme.Dark;
        t.Ui.SetTheme(theme); t.Field.HasError = true; t.Ui.Focus(t.Field); t.Ui.Update();
        var drawing = new Drawing(); t.Field.Draw(drawing);
        Assert.Contains((new Rect(160, 0, 40, 40), theme.Error), drawing.Fills);
        var symbols = drawing.Fills.Where(fill => fill.Color == theme.OnError && fill.Bounds.X >= 160).ToArray(); Assert.NotEmpty(symbols);
        Assert.All(symbols, fill => { Assert.InRange(fill.Bounds.X, 173, 187); Assert.InRange(fill.Bounds.Y, 13, 27); });
        Assert.Contains((new Rect(2, 2, 156, 36), theme.Focus, 2f), drawing.Strokes);
        Assert.True(Contrast(theme.Error, theme.OnError) >= 4.5);
        using var image = t.Ui.SnapshotRasterImage(); using var bitmap = SKBitmap.FromImage(image);
        var pixel = bitmap.GetPixel(162, 3); Assert.Equal(new SKColor(theme.Error.R, theme.Error.G, theme.Error.B), pixel);
        // The whole colored block is the tooltip target, including space outside the glyph.
        t.Ui.HandleInput(new(InputKind.PointerMove, 162, 3)); t.Clock.Add(500); t.Ui.Update();
        Assert.NotNull(t.Host.Popup); Assert.True(t.Field.IsFocused);
        static double Contrast(Color a, Color b)
        {
            static double L(Color c)
            {
                static double F(byte x) { var v = x / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
                return .2126 * F(c.R) + .7152 * F(c.G) + .0722 * F(c.B);
            }
            return (Math.Max(L(a), L(b)) + .05) / (Math.Min(L(a), L(b)) + .05);
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void NumberBox_ParentValidationFlowsToEditorWithLocalAndStylePrecedence(bool styled)
    {
        var box = new NumberBox("X") { Width = 180, Height = 30 };
        box.Editor.Value = "-";
        if (styled) box.Style = new Style().With(UiProperties.HasError, true).With(UiProperties.ErrorMessage, "Parent validator");
        else { box.HasError = true; box.ErrorMessage = "Parent validator"; }
        using var ui = UiSession.Create(new OverlayHost(box), new SkiaRasterRenderBackend(), new RasterUiTarget(), new(300, 100)); ui.Update();
        Assert.True(box.Editor.HasError); Assert.Equal("Parent validator", box.Editor.ErrorMessage); Assert.False(box.Prefix.HasError);
        var badge = box.Editor.ValidationIconBounds; Assert.Equal(new Rect(150, 0, 30, 30), badge);
        var drawing = new Drawing(); box.Draw(drawing);
        Assert.Single(drawing.Fills, fill => fill.Color == box.Theme.Error);
        box.Editor.Style = new Style().With(UiProperties.HasError, false).With(UiProperties.ErrorMessage, "Editor style"); ui.Update();
        Assert.Equal(default, box.Editor.ValidationIconBounds); Assert.Equal("Editor style", box.Editor.ErrorMessage);
        box.Editor.HasError = true; box.Editor.ErrorMessage = "Local validator"; ui.Update();
        Assert.Equal(badge, box.Editor.ValidationIconBounds); Assert.Equal("Local validator", box.Editor.ErrorMessage);
        box.Editor.Clear(UiProperties.HasError); box.Editor.Clear(UiProperties.ErrorMessage); box.Editor.Style = null;
        box.HasError = false; ui.Update(); Assert.Equal(default, box.Editor.ValidationIconBounds);
        Assert.Equal("-", box.Editor.Value); Assert.False(ui.Update());
    }

    [Fact]
    public void RichValidationBadge_AutoHeightWrapsAgainstItsOwnHeightAndReturnsToIdleAfterResize()
    {
        var editor = new RichTextEditor { FontSize = 12, MinWidth = 0, MinHeight = 0, Padding = new(4), MinimumContentHeight = 18, MaximumContentHeight = 70,
            Text = "one two three four five six seven eight nine ten", HasError = true, ErrorMessage = "Rejected" };
        using var ui = UiSession.Create(new VStack().WithChildren(editor), new SkiaRasterRenderBackend(), new RasterUiTarget(), new(220, 200));
        ui.Update(); ui.Focus(editor); editor.Select(2, 7); var document = editor.Document;
        foreach (var width in new[] { 220, 160, 500, 160, 220 })
        {
            ui.Resize(new(width, 200)); ui.Update(); var drawing = new Drawing(); editor.Draw(drawing);
            Assert.Equal(Math.Min(70, Math.Max(18, drawing.RichSize.Height)) + 8, editor.Bounds.Height);
            Assert.Equal(new Rect(width - editor.Bounds.Height, 0, editor.Bounds.Height, editor.Bounds.Height), editor.ValidationIconBounds);
            Assert.Equal(Math.Max(0, width - editor.Bounds.Height - 8), drawing.TextClip.Width);
            Assert.Same(document, editor.Document); Assert.Equal(7, editor.SelectionLength); Assert.True(editor.IsFocused); Assert.False(ui.Update());
        }
        editor.Text = "one"; ui.Update(); Assert.Equal(26, editor.Bounds.Height); Assert.Equal(26, editor.ValidationIconBounds.Width);
        editor.Height = 42; ui.Update(); Assert.Equal(42, editor.ValidationIconBounds.Width); Assert.False(ui.Update());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void HoverIcon_ShowsExactWrappedValidatorDetailsWithoutTakingFocusEvenWhenDisabled(bool disabled)
    {
        using var t = new Fixture(); t.Field.HasError = true; t.Ui.Focus(t.Other); t.Field.IsEnabled = !disabled;
        t.Field.ErrorMessage = "Значение отклонено валидатором.\nДопускается конечное число > 0, например 0.25."; t.Ui.Update();
        t.Hover(); t.Clock.Add(499); t.Ui.Update(); Assert.Null(t.Host.Popup);
        t.Clock.Add(1); t.Ui.Update(); var popup = Assert.IsAssignableFrom<Border>(t.Host.Popup);
        var scroll = Assert.IsType<ScrollViewer>(popup.Child); var text = Assert.IsAssignableFrom<Text>(scroll.Child);
        Assert.Equal(t.Field.ErrorMessage, text.Value); Assert.Equal(t.Field.ErrorMessage, text.AccessibleName);
        Assert.True(t.Other.IsFocused); Assert.True(popup.Bounds.Height > 30);
        Assert.InRange(popup.Bounds.X + popup.Bounds.Width, 0, 500); Assert.InRange(popup.Bounds.Y + popup.Bounds.Height, 0, 300);
        Assert.False(t.Host.NeedsFrame); Assert.False(t.Ui.Update());
    }

    [Theory]
    [InlineData("leave")] [InlineData("key")] [InlineData("wheel")] [InlineData("focus")]
    [InlineData("clear")] [InlineData("message")] [InlineData("hide")] [InlineData("detach")]
    [InlineData("dispose")] [InlineData("resize")] [InlineData("root-hide")] [InlineData("root-disable")]
    public void Tooltip_ContextChangeDisposesPopupAndDoesNotRetainStaleDetails(string change)
    {
        using var t = new Fixture(); t.Field.HasError = true; t.Ui.Update(); t.Hover(); t.Clock.Add(500); t.Ui.Update();
        var popup = t.Host.Popup!; Assert.NotNull(popup);
        switch (change)
        {
            case "leave": t.Ui.HandleInput(new(InputKind.PointerMove, 490, 290)); break;
            case "key": t.Ui.HandleInput(new(InputKind.KeyDown, Key: UiKey.Escape)); break;
            case "wheel": t.Ui.HandleInput(new(InputKind.Wheel, 490, 290, WheelDelta: 1)); break;
            case "focus": t.Ui.HandleInput(new(InputKind.FocusLost)); break;
            case "clear": t.Field.HasError = false; break;
            case "message": t.Field.ErrorMessage = "Another validator rejected this value"; break;
            case "hide": t.Content.IsVisible = false; break;
            case "detach": t.Content.Items.Remove(t.Field); break;
            case "dispose": t.Content.Items.Remove(t.Field); t.Field.Dispose(); break;
            case "resize": t.Field.Width = 180; break;
            case "root-hide": t.Host.IsVisible = false; break;
            case "root-disable": t.Host.IsEnabled = false; break;
        }
        t.Ui.Update(); t.Ui.Update(); Assert.Null(t.Host.Popup); Assert.True(popup.IsDisposed); Assert.False(t.Host.NeedsFrame);
        if (change is "detach" or "dispose") Assert.False(t.Field.IsAttached);
    }

    [Fact]
    public void PendingTooltip_MessageChangeAndDetachCannotPublishOldError()
    {
        using var t = new Fixture(); t.Field.HasError = true; t.Ui.Update(); t.Hover();
        t.Field.ErrorMessage = "new validator error"; t.Clock.Add(500); t.Ui.Update(); Assert.Null(t.Host.Popup);
        t.Hover(); t.Clock.Add(500); t.Ui.Update();
        Assert.Equal("new validator error", Assert.IsAssignableFrom<Text>(Assert.IsType<ScrollViewer>(Assert.IsAssignableFrom<Border>(t.Host.Popup).Child).Child).Value);
        t.Content.Items.Remove(t.Field); t.Ui.Update(); Assert.Null(t.Host.Popup); Assert.False(t.Host.NeedsFrame);
    }

    [Fact]
    public void Tooltip_OnlyIconTriggersAndExistingMenuIsNotReplaced()
    {
        using var t = new Fixture(); t.Field.HasError = true; t.Ui.Update();
        t.Ui.HandleInput(new(InputKind.PointerMove, 20, 20)); t.Clock.Add(600); t.Ui.Update(); Assert.Null(t.Host.Popup);
        var menu = new Border { Width = 60, Height = 30 }; t.Host.Open(t.Other, menu, t.Other.Bounds, t.Ui); t.Ui.Update();
        t.Hover(); t.Clock.Add(600); t.Ui.Update(); Assert.Same(menu, t.Host.Popup); Assert.False(menu.IsDisposed);
        t.Host.Close(); t.Field.ErrorMessage = ""; t.Hover(); t.Clock.Add(600); t.Ui.Update(); Assert.Null(t.Host.Popup);
    }

    [Fact]
    public void LongTooltip_ScrollsWithinViewportAndKeepsAllValidatorText()
    {
        using var t = new Fixture(); t.Field.HasError = true; t.Field.ErrorMessage = string.Join('\n', Enumerable.Range(1, 50).Select(i => $"Причина {i}: значение отклонено"));
        t.Ui.Update(); t.Hover(); t.Clock.Add(500); t.Ui.Update();
        var popup = Assert.IsAssignableFrom<Border>(t.Host.Popup); var scroll = Assert.IsType<ScrollViewer>(popup.Child);
        Assert.Equal(t.Field.ErrorMessage, Assert.IsAssignableFrom<Text>(scroll.Child).Value);
        Assert.True(scroll.Extent > scroll.Bounds.Height); Assert.InRange(popup.Bounds.Height, 1, 300);
        var x = popup.Bounds.X + 12; var y = popup.Bounds.Y + 12;
        t.Ui.HandleInput(new(InputKind.Wheel, x, y, WheelDelta: -10)); t.Ui.Update();
        Assert.Same(popup, t.Host.Popup); Assert.True(scroll.Offset > 0); Assert.False(t.Host.NeedsFrame);
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Add(int ms) => _ticks += TimeSpan.FromMilliseconds(ms).Ticks;
    }

    [Fact]
    public void DefaultStyleGallery_RejectedDraftHasFullBadgeAndWorkingValidatorTooltip()
    {
        var gallery = new AlloyTest.DefaultStyleGallery(); var clock = new Clock(); var viewport = new UiViewport(1280, 800);
        var surface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), viewport);
        using var ui = new UiSession(gallery, surface, viewport, null, new UiDispatchOptions(), clock); ui.Update(); ui.Focus(gallery.NameEditor);
        var field = gallery.ErrorEditor; var badge = field.ValidationIconBounds;
        Assert.Equal(field.Bounds.Height, badge.Width); Assert.Equal(field.Bounds.Y, badge.Y);
        Assert.Equal("Имя должно быть уникальным", field.ErrorMessage);
        ui.HandleInput(new(InputKind.PointerMove, badge.X + 2, badge.Y + 3)); clock.Add(500); ui.Update();
        var host = Assert.IsType<OverlayHost>(gallery.Child); var popup = Assert.IsAssignableFrom<Border>(host.Popup);
        var scroll = Assert.IsType<ScrollViewer>(popup.Child); Assert.Equal(field.ErrorMessage, Assert.IsAssignableFrom<Text>(scroll.Child).Value);
        Assert.True(gallery.NameEditor.IsFocused); Assert.Equal("Укажите корректное имя", field.Value); Assert.False(ui.Update());
    }
    private sealed class Drawing : IDrawingContext, IRichTextDrawingContext
    {
        private readonly Stack<Rect> _clips = new(); private Rect _clip;
        internal Rect TextClip;
        internal Size RichSize;
        internal List<(Rect Bounds, Color Color)> Fills { get; } = [];
        internal List<(Rect Bounds, Color Color, float Width)> Strokes { get; } = [];
        public void Save() => _clips.Push(_clip);
        public void Restore() => _clip = _clips.Pop();
        public void Clip(Rect rectangle) => _clip = rectangle;
        public void Translate(float x, float y) { }
        public void Fill(Rect rectangle, Color color, float radius = 0) => Fills.Add((rectangle, color));
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0) => Strokes.Add((rectangle, color, width));
        public void Text(string text, float x, float y, Color color, float fontSize, string fontFamily) => TextClip = _clip;
        public void RichText(IRichTextLayout layout, float x, float y, int selectionStart, int selectionLength, Color selectionColor) { TextClip = _clip; RichSize = layout.Size; }
        public void Image(IImageSource source, Rect destination) { }
    }
    private sealed class Fixture : IDisposable
    {
        internal Element Field { get; }
        internal Panel Content { get; } = new();
        internal TextBox Other { get; } = new() { Width = 200, Height = 30, Margin = new(0, 70, 0, 0), HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        internal OverlayHost Host { get; }
        internal UiSession Ui { get; }
        internal Clock Clock { get; } = new();
        internal string Value => Field is TextBox box ? box.Value : ((RichTextEditor)Field).Text;
        internal int SelectionLength => Field is TextBox box ? box.SelectionLength : ((RichTextEditor)Field).SelectionLength;
        internal Fixture(bool rich = false, float scale = 1)
        {
            Field = rich ? new RichTextEditor { Text = "rejected draft", MinWidth = 0, MinHeight = 0 } : new TextBox { Value = "rejected draft", MinWidth = 0 };
            Field.Width = 200; Field.Height = 40; Field.HorizontalAlignment = LayoutAlignment.Start; Field.VerticalAlignment = LayoutAlignment.Start;
            Field.ErrorMessage = "Rejected by validator"; Content.Add(Field); Content.Add(Other); Host = new(Content);
            var surface = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), new((int)(500 * scale), (int)(300 * scale), scale));
            Ui = new(Host, surface, new((int)(500 * scale), (int)(300 * scale), scale), null, new UiDispatchOptions(), Clock); Ui.Update();
        }
        internal void Hover() { var b = Field.ValidationIconBounds; Ui.HandleInput(new(InputKind.PointerMove, b.X + b.Width / 2, b.Y + b.Height / 2)); }
        public void Dispose() { Ui.Dispose(); if (!Field.IsDisposed && Field.Parent == null) Field.Dispose(); }
    }
}
