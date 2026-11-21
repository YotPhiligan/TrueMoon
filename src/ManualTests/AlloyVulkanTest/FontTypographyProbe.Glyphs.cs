using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const string GlyphText = "AVijЁф";
    private const int GlyphCellSize = 32;
    private static readonly int[] GlyphWeights = [400, 600];

    private enum GlyphClip { Unclipped, Advance, HalfAdvance }

    private readonly record struct GlyphCase(bool Subpixel, int HalfPixels, GlyphClip Clip);
    private sealed record GlyphPlacement(string Text, ushort Id, int Size, int Weight, float Advance, float RunOrigin,
        SKRect MetricsBounds, SKRectI Cell, float OriginX, float BaselineY, SKRect AdvanceClip, SKRect HalfClip);
    private sealed record GlyphInk(SKRectI Bounds, int Pixels, long AlphaSum, double? CentroidX, double? CentroidY);
    private sealed record GlyphCoverage(GlyphInk Ink, int RemovedPixels, long RemovedAlpha, int RemovedLeft, int RemovedRight);

    internal static void RunGlyphs(string output, bool validation)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        using var regular = MatchDefaultFace(400);
        using var semibold = MatchDefaultFace(600);
        using var defaults = new SKFont(regular, 12);
        var captures = new List<object>();
        var backendComparisons = new List<object>();
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var passed = true;
        var logicalWidth = GlyphText.Length * GlyphCellSize;
        var logicalHeight = FractionalFontSizes.Length * GlyphWeights.Length * GlyphCellSize;

        foreach (var scale in new[] { 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(logicalWidth * scale), (int)(logicalHeight * scale), scale);
            var placements = new Dictionary<(bool Subpixel, int HalfPixels), GlyphPlacement[]>();
            foreach (var subpixel in new[] { false, true })
            for (var phase = 0; phase < 2; phase++)
                placements.Add((subpixel, phase), PlaceGlyphs(scale, phase * 0.5f, subpixel, regular, semibold));

            var (raster, openGl, vulkan, validationPassed) = CaptureTypographyBackends(viewport, validation, hardware, validationReports, Capture);
            passed &= validationPassed;
            CompareBackends("raster", raster, "opengl", openGl, 1);
            CompareBackends("raster", raster, "vulkan", vulkan, 1);
            CompareBackends("opengl", openGl, "vulkan", vulkan, 0);

            Dictionary<GlyphCase, ModeFrame> Capture(string backend, Func<Action<SKCanvas>, TextComposition, ModeFrame> render)
            {
                var frames = new Dictionary<GlyphCase, ModeFrame>();
                foreach (var ((subpixel, phase), glyphs) in placements)
                {
                    byte[]? full = null;
                    foreach (var clip in Enum.GetValues<GlyphClip>())
                    {
                        var frame = render(canvas => PaintGlyphs(canvas, scale, glyphs, subpixel, clip, regular, semibold), TextComposition.Offscreen);
                        var source = frame.Source ?? throw new InvalidOperationException("Missing glyph texture readback.");
                        Require(frame.Pixels.Where((alpha, index) => index % 4 == 3).All(alpha => alpha == 255), "Expected opaque composition output.");
                        var blendError = VerifyTextTextureBlend(source, frame.Pixels, viewport.Width);
                        full ??= source;
                        var stem = $"{backend}-{(int)(scale * 100)}-subpixel-{(subpixel ? "on" : "off")}-x{phase * 50}-{clip.ToString().ToLowerInvariant()}";
                        var file = stem + ".png";
                        var texture = stem + "-source.png";
                        var crop = $"{stem}-row-4x.png";
                        var firstRow = new SKRectI(0, 0, viewport.Width, (int)(GlyphCellSize * scale));
                        WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, frame.Pixels);
                        WritePng(Path.Combine(output, texture), viewport.Width, viewport.Height, source);
                        WriteEnlargedCrop(Path.Combine(output, crop), frame.Pixels, viewport.Width, firstRow, FractionalZoom);

                        var measurements = glyphs.Select(glyph =>
                        {
                            var coverage = MeasureGlyph(glyph, clip, viewport.Width, full, source);
                            return new
                            {
                                glyph.Text, glyph.Id, glyph.Size, glyph.Weight, glyph.OriginX, glyph.BaselineY,
                                AdvanceLogical = glyph.Advance, AdvancePx = glyph.Advance * scale,
                                RunOriginLogical = glyph.RunOrigin, RunOriginPx = glyph.RunOrigin * scale,
                                MetricsBounds = Edges(glyph.MetricsBounds), Cell = PixelBounds(glyph.Cell),
                                ClipBounds = clip switch { GlyphClip.Advance => Edges(glyph.AdvanceClip), GlyphClip.HalfAdvance => Edges(glyph.HalfClip), _ => null },
                                InkBounds = PixelBounds(coverage.Ink.Bounds), coverage.Ink.Pixels, coverage.Ink.AlphaSum,
                                coverage.Ink.CentroidX, coverage.Ink.CentroidY, coverage.RemovedPixels, coverage.RemovedAlpha,
                                coverage.RemovedLeft, coverage.RemovedRight
                            };
                        }).ToArray();
                        if (clip == GlyphClip.HalfAdvance)
                            Require(measurements.Any(glyph => glyph.RemovedPixels > 0 && glyph.Pixels > 0), "Half-advance clip did not intersect glyph ink.");
                        frames.Add(new(subpixel, phase, clip), frame);
                        captures.Add(new { Scale = scale, Backend = backend, Subpixel = subpixel, OffsetPx = phase * 0.5f,
                            Clip = clip.ToString(), viewport.Width, viewport.Height, File = file, Texture = texture, Crop = crop, BlendError = blendError, Glyphs = measurements });
                    }
                }
                return frames;
            }

            void CompareBackends(string reference, Dictionary<GlyphCase, ModeFrame> expected, string backend,
                Dictionary<GlyphCase, ModeFrame> actual, int edgeRadius)
            {
                foreach (var (key, first) in expected)
                {
                    var second = actual[key];
                    var composed = PixelDifference.Compare(first.Pixels, second.Pixels, viewport.Width, viewport.Height, 2, edgeRadius);
                    var source = PixelDifference.Compare(first.Source!, second.Source!, viewport.Width, viewport.Height, 2, edgeRadius);
                    passed &= composed.Passed && source.Passed;
                    backendComparisons.Add(new { Scale = scale, Reference = reference, Backend = backend,
                        key.Subpixel, OffsetPx = key.HalfPixels * 0.5f, Clip = key.Clip.ToString(), Composed = composed, Source = source });
                }
            }
        }

        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "SegUIVar.ttf");
        var hashes = new[] { SkiaNativeLibrary.LoadedPath!, fontPath }.Where(File.Exists)
            .Select(path => new { File = path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, Profile = "typography-glyphs", PhysicalDpi = false, ValidationEnabled = validation,
            FontFamily = regular.FamilyName, FontSizes = FractionalFontSizes, Weights = GlyphWeights, GlyphText,
            LogicalWidth = logicalWidth, LogicalHeight = logicalHeight, CropMagnification = FractionalZoom,
            FontSettings = new { Edging = defaults.Edging.ToString(), Hinting = defaults.Hinting.ToString(), defaults.BaselineSnap },
            MetricsScope = "SKFont logical widths and default run origins; device values are scaled logical metrics. Ink bounds and centroids are measured from each backend's premultiplied texture.",
            ClipScope = "Hard device clip at the exact advance box or an integer edge at its midpoint. Scale and draw origin are independent of the clip.",
            SkiaSharp = typeof(SKFont).Assembly.FullName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
            Hashes = hashes, Hardware = hardware, Captures = captures, BackendComparisons = backendComparisons, ValidationReports = validationReports
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Typography glyphs: {captures.Count} frames and {backendComparisons.Count} paired backend comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static GlyphPlacement[] PlaceGlyphs(float scale, float offsetPx, bool subpixel, SKTypeface regular, SKTypeface semibold)
    {
        var cellSize = (int)(GlyphCellSize * scale);
        var row = 0;
        var result = new List<GlyphPlacement>();
        foreach (var size in FractionalFontSizes)
        foreach (var weight in GlyphWeights)
        {
            using var font = new SKFont(weight == 400 ? regular : semibold, size) { Subpixel = subpixel };
            var ids = font.GetGlyphs(GlyphText);
            var advances = font.GetGlyphWidths(ids, out var bounds);
            var origins = font.GetGlyphPositions(ids);
            Require(ids.Length == GlyphText.Length && ids.All(id => id != 0) && origins.Length == ids.Length, "A glyph sample is missing from the typeface.");
            Require(Math.Abs(advances.Sum() - font.MeasureText(ids)) < 0.001f, "Isolated advances do not add up to the measured run width.");
            for (var column = 0; column < ids.Length; column++)
            {
                var left = column * cellSize;
                var top = row * cellSize;
                var x = left + 12 * scale + offsetPx;
                var y = top + 20 * scale;
                var advance = advances[column];
                Require(float.IsFinite(advance) && advance > 0, "Glyph sample has an invalid advance.");
                if (column > 0)
                    Require(Math.Abs(origins[column].X - origins[column - 1].X - advances[column - 1]) < 0.001f, "Glyph run origins do not match advance accumulation.");
                var cell = new SKRectI(left, top, left + cellSize, top + cellSize);
                var advanceClip = new SKRect(x, cell.Top, x + advance * scale, cell.Bottom);
                var halfClip = new SKRect(cell.Left, cell.Top, MathF.Floor(x + advance * scale / 2), cell.Bottom);
                result.Add(new(GlyphText[column].ToString(), ids[column], size, weight, advance, origins[column].X,
                    bounds[column], cell, x, y, advanceClip, halfClip));
            }
            row++;
        }
        return result.ToArray();
    }

    private static void PaintGlyphs(SKCanvas canvas, float scale, GlyphPlacement[] glyphs, bool subpixel, GlyphClip clip,
        SKTypeface regular, SKTypeface semibold)
    {
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = ToSkColor(Theme.Light.Foreground), IsAntialias = true };
        foreach (var glyph in glyphs)
        {
            using var font = new SKFont(glyph.Weight == 400 ? regular : semibold, glyph.Size) { Subpixel = subpixel };
            canvas.Save();
            if (clip == GlyphClip.Advance) canvas.ClipRect(glyph.AdvanceClip);
            if (clip == GlyphClip.HalfAdvance) canvas.ClipRect(glyph.HalfClip);
            canvas.Scale(scale);
            canvas.DrawText(glyph.Text, glyph.OriginX / scale, glyph.BaselineY / scale, SKTextAlign.Left, font, paint);
            canvas.Restore();
        }
    }

    private static GlyphCoverage MeasureGlyph(GlyphPlacement glyph, GlyphClip clip, int width, byte[] full, byte[] source)
    {
        var cell = glyph.Cell;
        var left = cell.Right;
        var top = cell.Bottom;
        var right = cell.Left;
        var bottom = cell.Top;
        var pixels = 0;
        var removedLeft = 0;
        var removedRight = 0;
        var alphaSum = 0L;
        var removedAlpha = 0L;
        var weightedX = 0L;
        var weightedY = 0L;
        for (var y = cell.Top; y < cell.Bottom; y++)
        for (var x = cell.Left; x < cell.Right; x++)
        {
            var index = (y * width + x) * 4;
            var alpha = source[index + 3];
            var reference = full[index + 3];
            Require(alpha == 0 || source.AsSpan(index, 4).SequenceEqual(full.AsSpan(index, 4)), "Hard clipping modified a surviving glyph pixel.");
            if (clip == GlyphClip.HalfAdvance)
                Require(alpha == (x < glyph.HalfClip.Right ? reference : 0), "Half-advance clip differed from the integer device edge.");
            if (clip == GlyphClip.Advance)
            {
                // Allow the fractional clip edge to quantize in the adjoining device pixel.
                var center = x + 0.5f;
                if (center < glyph.AdvanceClip.Left - 0.5f || center > glyph.AdvanceClip.Right + 0.5f)
                    Require(alpha == 0, "Ink escaped the advance clip.");
                if (center > glyph.AdvanceClip.Left + 0.5f && center < glyph.AdvanceClip.Right - 0.5f)
                    Require(alpha == reference, "Advance clip removed an interior pixel.");
            }
            if (alpha == 0 && reference != 0)
            {
                removedAlpha += reference;
                if (x + 0.5f < glyph.OriginX) removedLeft++;
                else removedRight++;
            }
            if (alpha == 0) continue;
            pixels++;
            alphaSum += alpha;
            weightedX += x * alpha;
            weightedY += y * alpha;
            left = Math.Min(left, x); top = Math.Min(top, y);
            right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
        }
        if (clip == GlyphClip.Unclipped)
            Require(pixels > 0 && left > cell.Left && top > cell.Top && right < cell.Right && bottom < cell.Bottom,
                $"Glyph '{glyph.Text}' is missing or touches a control cell boundary.");
        var ink = alphaSum == 0 ? new GlyphInk(default, 0, 0, null, null) :
            new GlyphInk(new(left, top, right, bottom), pixels, alphaSum, (double)weightedX / alphaSum + 0.5, (double)weightedY / alphaSum + 0.5);
        return new(ink, removedLeft + removedRight, removedAlpha, removedLeft, removedRight);
    }

    private static object Edges(SKRect rect) => new { rect.Left, rect.Top, rect.Right, rect.Bottom };
}
