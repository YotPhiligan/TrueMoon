using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const int ScrollWidth = 240;
    private const int ScrollHeight = 88;
    private const int ScrollFieldWidth = 160;
    private const float ScrollFontSize = 14;

    private readonly record struct ScrollSample(string Name, string Text);
    private static readonly ScrollSample[] DarkScrollSamples =
    [
        new("latin", "Preview settings · Horizontal scrolling ·  W" + "  "),
        new("cyrillic", "Параметры редактора · Граница строки ·  Д" + "  "),
        new("revision", "Revision 128 · 05:23:41 · build 20261121 ·  7" + "  ")
    ];
    private readonly record struct ScrollStep(int Phase, bool Returning)
    {
        internal string Direction => Returning ? "toward-start" : "toward-end";
        internal float PhasePx => Phase * 0.25f;
    }
    private static readonly ScrollStep[] ForwardScrollSteps = [new(0, false), new(1, false), new(2, false), new(3, false)];
    private static readonly ScrollStep[] BidirectionalScrollSteps =
        [.. ForwardScrollSteps, new(3, true), new(2, true), new(1, true), new(0, true)];
    private readonly record struct ScrollCase(ScrollSample Sample, bool Subpixel, ScrollStep Step);
    private sealed record ScrollFrame(byte[] Pixels, Rect Field, Rect Clip, Rect Caret, float TextX, double GlyphCenter, int HitIndex, int SelectionStart);

    internal static void RunTextBoxPositioning(string output, bool validation, bool darkBidirectional)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        using var face = MatchDefaultFace(400);
        var theme = darkBidirectional ? Theme.Dark : Theme.Light;
        ScrollSample[] samples = darkBidirectional ? DarkScrollSamples : [new("mixed", "AVij · Ёлка · Автор длинной строки · j  Ё")];
        var steps = darkBidirectional ? BidirectionalScrollSteps : ForwardScrollSteps;
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var captures = new List<object>();
        var comparisons = new List<object>();
        var stockComparisons = new List<object>();
        var directionComparisons = new List<object>();
        var passed = true;
        foreach (var scale in new[] { 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(ScrollWidth * scale), (int)(ScrollHeight * scale), scale);
            var (raster, openGl, vulkan, validationPassed) = CaptureTypographyBackends(viewport, validation, hardware, validationReports, Capture);
            passed &= validationPassed;
            Compare("raster", raster, "opengl", openGl, 1);
            Compare("raster", raster, "vulkan", vulkan, 1);
            Compare("opengl", openGl, "vulkan", vulkan, 0);

            foreach (var sample in samples)
            {
                var (root, textbox) = CreateScrollTextBox(sample, theme);
                using var stock = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport);
                stock.SetTheme(theme);
                Require(stock.Update(), "Expected the stock TextBox to render.");
                stock.Focus(textbox);
                MoveToEnd(stock, textbox);
                foreach (var step in steps)
                {
                    RenderScrollStep(stock, textbox, scale, step);
                    using var image = stock.SnapshotRasterImage();
                    var expected = raster[new(sample, false, step)];
                    var region = ScrollTextRegion(expected, scale);
                    var result = PixelDifference.Compare(expected.Pixels, RasterProbe.ReadSnapshot(image), viewport.Width, viewport.Height, 0, region: region);
                    var sameField = textbox.Bounds == expected.Field;
                    passed &= result.Passed && sameField;
                    stockComparisons.Add(new { Scale = scale, Sample = sample.Name, step.Direction, step.PhasePx, SameField = sameField, Result = result });
                }
            }

            Dictionary<ScrollCase, ScrollFrame> Capture(string backend, Func<Action<SKCanvas>, TextComposition, ModeFrame> render)
            {
                using var measurement = new SkiaRasterRenderBackend().CreateSurface(new RasterUiTarget(), viewport);
                var frames = new Dictionary<ScrollCase, ScrollFrame>();
                foreach (var sample in samples)
                foreach (var subpixel in new[] { false, true })
                {
                    using var font = new SKFont(face, ScrollFontSize) { Subpixel = subpixel };
                    var (panel, editor) = CreateScrollTextBox(sample, theme);
                    var surface = new ScrollSurface(measurement.TextLayout, viewport, render, font, theme.Accent with { A = 100 });
                    using var ui = new UiSession(panel, surface, viewport);
                    ui.SetTheme(theme);
                    Require(ui.Update(), "Expected the TextBox fixture to render.");
                    ui.Focus(editor);
                    MoveToEnd(ui, editor);
                    var glyphEnd = sample.Text.TrimEnd().Length;
                    var glyphStart = glyphEnd - 1;
                    var glyphLeft = measurement.TextLayout.Measure(sample.Text[..glyphStart], ScrollFontSize, editor.FontFamily).Width;
                    var glyphRight = measurement.TextLayout.Measure(sample.Text[..glyphEnd], ScrollFontSize, editor.FontFamily).Width;
                    foreach (var step in steps)
                    {
                        RenderScrollStep(ui, editor, scale, step);
                        var draw = surface.Draw;
                        var pixels = surface.Pixels;
                        Require(draw.TextX < draw.Clip.X && draw.Caret.X + draw.Caret.Width <= draw.Clip.X + draw.Clip.Width,
                            "Fixture must scroll and keep the caret inside its viewport.");
                        var padding = DeviceRect(new Rect(editor.Bounds.X + 4, draw.TextY, 2, draw.Caret.Height), scale);
                        RequirePadding(pixels, viewport.Width, padding, theme.Input);
                        var hitX = draw.TextX + glyphLeft;
                        var glyphCenter = FinalGlyphCenter(pixels, viewport.Width, scale, hitX, draw.Caret, theme.Input, darkBidirectional);
                        ui.HandleInput(new UiInput(InputKind.PointerDown, hitX, draw.TextY + draw.Caret.Height / 2));
                        ui.HandleInput(new UiInput(InputKind.PointerUp, hitX, draw.TextY + draw.Caret.Height / 2));
                        var hit = editor.CaretIndex;
                        Require(hit == glyphStart, $"Click reached {hit} at the last visible character boundary of {sample.Name}.");
                        ui.Update();
                        Require(Math.Abs(surface.Draw.Caret.X - hitX) < 0.001f, "Painted caret disagrees with the clicked position.");
                        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
                        Require(editor.CaretIndex == glyphEnd, "Right did not reach the boundary after the glyph.");
                        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
                        ui.Update();
                        var selection = editor.SelectionStart;
                        Require(selection == hit && editor.SelectionLength == 1 && surface.Draw.Selection is { Width: > 0 } selectionBounds &&
                            Math.Abs(selectionBounds.X - hitX) < 0.001f &&
                            Math.Abs(selectionBounds.X + selectionBounds.Width - (draw.TextX + glyphRight)) < 0.001f,
                            "Expected selection of the last visible character.");
                        MoveToEnd(ui, editor);
                        ui.Update();
                        Require(surface.Draw.Caret == draw.Caret && surface.Draw.TextX == draw.TextX, "Caret navigation changed the end-of-line scroll.");

                        var frame = new ScrollFrame(pixels, editor.Bounds, draw.Clip, draw.Caret, draw.TextX, glyphCenter, hit, selection);
                        var firstPhase = step.Returning ? 3 : 0;
                        var start = step.Phase == firstPhase ? frame : frames[new(sample, subpixel, new(firstPhase, step.Returning))];
                        var motion = (frame.TextX - start.TextX) * scale;
                        Require(Math.Abs(motion + (step.Phase - firstPhase) * 0.25f) < 0.001f,
                            $"Wrong fractional scroll displacement for {sample.Name}/{step.Direction} at phase {step.Phase}.");
                        frames.Add(new(sample, subpixel, step), frame);
                        var stem = $"{backend}-{(int)(scale * 100)}-subpixel-{(subpixel ? "on" : "off")}-x{step.Phase * 25}";
                        if (darkBidirectional) stem = $"{sample.Name}-{step.Direction}-{stem}";
                        var file = stem + ".png";
                        var crop = stem + "-field-4x.png";
                        WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, pixels);
                        WriteEnlargedCrop(Path.Combine(output, crop), pixels, viewport.Width, DeviceRect(frame.Field, scale), FractionalZoom);
                        captures.Add(new { Scale = scale, Sample = sample.Name, Backend = backend, Subpixel = subpixel, step.Direction, step.PhasePx, ScrollMotionPx = motion,
                            GlyphCenterPx = frame.GlyphCenter, GlyphMotionPx = frame.GlyphCenter - start.GlyphCenter, frame.Field, frame.Clip, frame.Caret,
                            frame.TextX, frame.HitIndex, frame.SelectionStart, File = file, Crop = crop, viewport.Width, viewport.Height });
                    }
                }

                foreach (var (key, actual) in frames)
                {
                    if (key.Subpixel)
                        Require(SameCoordinates(frames[key with { Subpixel = false }], actual), "Subpixel changed TextBox layout or routed input.");
                    if (!key.Step.Returning) continue;
                    var outgoing = frames[key with { Step = new(key.Step.Phase, false) }];
                    var sameCoordinates = SameCoordinates(outgoing, actual);
                    var result = PixelDifference.Compare(outgoing.Pixels, actual.Pixels, viewport.Width, viewport.Height, 0);
                    passed &= sameCoordinates && result.Passed;
                    directionComparisons.Add(new { Scale = scale, Sample = key.Sample.Name, Backend = backend, key.Subpixel,
                        key.Step.PhasePx, SameCoordinates = sameCoordinates, Result = result });
                }
                return frames;
            }

            void Compare(string reference, Dictionary<ScrollCase, ScrollFrame> first, string backend,
                Dictionary<ScrollCase, ScrollFrame> second, int edgeRadius)
            {
                foreach (var (key, expected) in first)
                {
                    var actual = second[key];
                    var sameCoordinates = SameCoordinates(expected, actual);
                    var result = PixelDifference.Compare(expected.Pixels, actual.Pixels, viewport.Width, viewport.Height, 2, edgeRadius,
                        region: ScrollTextRegion(expected, scale));
                    passed &= sameCoordinates && result.Passed;
                    comparisons.Add(new { Scale = scale, Sample = key.Sample.Name, Reference = reference, Backend = backend, key.Subpixel,
                        key.Step.Direction, key.Step.PhasePx, SameCoordinates = sameCoordinates, Result = result });
                }
            }
        }

        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, ValidationEnabled = validation, PhysicalDpi = false, FontFamily = face.FamilyName,
            FontSize = ScrollFontSize, Theme = darkBidirectional ? "Dark" : "Light", Samples = samples,
            NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),
            Hardware = hardware, ValidationReports = validationReports, Captures = captures, Comparisons = comparisons,
            StockComparisons = stockComparisons, DirectionComparisons = directionComparisons
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"TextBox positioning: {captures.Count} frames, {comparisons.Count} backend comparisons, " +
            $"{stockComparisons.Count} stock comparisons, {directionComparisons.Count} direction comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static (Panel Root, TextBox Field) CreateScrollTextBox(ScrollSample sample, Theme theme)
    {
        var root = new Panel { Background = theme.Input };
        var field = new TextBox { Value = sample.Text, Width = ScrollFieldWidth + 0.75f, Height = 36, FontSize = ScrollFontSize, CornerRadius = 0,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start, Margin = new(16, 20, 0, 0) };
        root.Add(field);
        return (root, field);
    }

    private static void RenderScrollStep(UiSession ui, TextBox field, float scale, ScrollStep step)
    {
        field.Width = ScrollFieldWidth + (0.75f - step.PhasePx) / scale;
        if (step.Returning)
        {
            // Widening retains the current offset. Home resets it on draw, then End uses the new width.
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
            Require(field.CaretIndex == 0 && ui.Update(), "Home did not reset the scroll.");
            MoveToEnd(ui, field);
        }
        Require(ui.Update(), "Expected the scroll phase to render.");
    }

    private static SKRectI ScrollTextRegion(ScrollFrame frame, float scale) =>
        DeviceRect(frame.Clip with { Y = frame.Caret.Y, Height = frame.Caret.Height }, scale);

    private static void MoveToEnd(UiSession ui, TextBox field)
    {
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.End));
        Require(field.CaretIndex == field.Value.Length && field.SelectionLength == 0, "End did not put the caret at the end of the line.");
    }

    private static bool SameCoordinates(ScrollFrame first, ScrollFrame second) => first.Field == second.Field && first.Clip == second.Clip &&
        first.Caret == second.Caret && first.TextX == second.TextX && first.HitIndex == second.HitIndex && first.SelectionStart == second.SelectionStart;

    private static void RequirePadding(byte[] pixels, int width, SKRectI strip, Color background)
    {
        var expected = ToSkColor(background);
        for (var y = strip.Top; y < strip.Bottom; y++)
        for (var x = strip.Left; x < strip.Right; x++)
        {
            var index = (y * width + x) * 4;
            Require(pixels[index] == expected.Red && pixels[index + 1] == expected.Green && pixels[index + 2] == expected.Blue && pixels[index + 3] == 255,
                "Scrolled text escaped the viewport into left padding.");
        }
    }

    private static double FinalGlyphCenter(byte[] pixels, int width, float scale, float glyphX, Rect caret, Color background, bool isolated)
    {
        var left = (int)MathF.Floor((glyphX - 1) * scale);
        var right = (int)MathF.Floor(caret.X * scale);
        long sum = 0, weighted = 0;
        for (var row = (int)MathF.Ceiling(caret.Y * scale); row < (int)MathF.Floor((caret.Y + caret.Height) * scale); row++)
        for (var x = left; x < right; x++)
        {
            var contrast = Math.Abs(pixels[(row * width + x) * 4] - background.R);
            if (isolated && (x == left || x == right - 1))
                Require(contrast == 0, "The final visible glyph must have an empty pixel column on either side.");
            sum += contrast;
            weighted += contrast * x;
        }
        Require(sum > 0, "No ink found at the last character.");
        return (double)weighted / sum + 0.5;
    }

    private sealed class ScrollSurface(ITextLayoutService text, UiViewport viewport, Func<Action<SKCanvas>, TextComposition, ModeFrame> render,
        SKFont font, Color selectionColor) : IUiRenderSurface
    {
        private bool _disposed;
        public ITextLayoutService TextLayout => text;
        internal ScrollDrawing Draw { get; private set; } = null!;
        internal byte[] Pixels { get; private set; } = [];

        public void VerifyAvailable() => ObjectDisposedException.ThrowIf(_disposed, this);
        public void Resize(UiViewport value)
        {
            VerifyAvailable();
            Require(value == viewport, "The positioning probe changes the field width inside a fixed surface.");
        }
        public void Render(Element root, UiViewport value)
        {
            Resize(value);
            Pixels = render(canvas =>
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Save();
                try
                {
                    canvas.Scale(viewport.Scale);
                    using var paint = new SKPaint { IsAntialias = true };
                    Draw = new ScrollDrawing(canvas, paint, font, selectionColor);
                    root.Draw(Draw);
                }
                finally { canvas.Restore(); }
            }, TextComposition.Opaque).Pixels;
        }
        public void Dispose() => _disposed = true;
    }

    private sealed class ScrollDrawing(SKCanvas canvas, SKPaint paint, SKFont font, Color selectionColor) : IDrawingContext
    {
        private Rect _lastClip;
        private bool _textDrawn;
        internal Rect Clip { get; private set; }
        internal Rect Caret { get; private set; }
        internal Rect? Selection { get; private set; }
        internal float TextX { get; private set; }
        internal float TextY { get; private set; }
        void IDrawingContext.Save() => canvas.Save();
        void IDrawingContext.Restore() => canvas.Restore();
        void IDrawingContext.Clip(Rect rectangle) { _lastClip = rectangle; canvas.ClipRect(SkiaRect(rectangle)); }
        void IDrawingContext.Translate(float x, float y) => canvas.Translate(x, y);
        public void Fill(Rect rectangle, Color color, float radius = 0)
        {
            if (_textDrawn && rectangle.Width == 1) Caret = rectangle;
            if (!_textDrawn && color == selectionColor) Selection = rectangle;
            paint.Color = ToSkColor(color);
            canvas.DrawRoundRect(SkiaRect(rectangle), radius, radius, paint);
        }
        public void Stroke(Rect rectangle, Color color, float width = 1, float radius = 0)
        {
            paint.Color = ToSkColor(color);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = width;
            try { canvas.DrawRoundRect(SkiaRect(rectangle), radius, radius, paint); }
            finally { paint.Style = SKPaintStyle.Fill; }
        }
        public void Text(string text, float x, float y, Color color, float fontSize, string family) => Text(text, x, y, color, fontSize, family, 400, false);
        public void Text(string text, float x, float y, Color color, float fontSize, string family, int fontWeight, bool fontItalic)
        {
            Require(fontSize == ScrollFontSize && font.Typeface.FamilyName == family && fontWeight == 400 && !fontItalic,
                "Unexpected typeface in the positioning fixture.");
            Clip = _lastClip;
            TextX = x; TextY = y; _textDrawn = true;
            paint.Color = ToSkColor(color);
            canvas.DrawText(text, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
        }
        public void Image(IImageSource source, Rect destination) => throw new NotSupportedException("The positioning fixture consists of plain text and geometry.");
        private static SKRect SkiaRect(Rect rectangle) => new(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
    }
}
