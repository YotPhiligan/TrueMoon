using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const int CompositionHeight = 100;
    private const byte PanelAlpha = 153;
    private static readonly (SKColor Left, SKColor Right) CompositionColors = (new(224, 238, 250), new(245, 231, 213));

    private enum TextComposition { Opaque, Translucent, Offscreen }

    private sealed record ComposedText(Rect Bounds, byte[] Pixels, byte[]? Texture, byte[] Coverage, object[] Samples,
        int? MaximumBlendError, double CaptureMilliseconds);

    internal static void RunComposition(string output, bool validation)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);

        var captures = new List<object>();
        var comparisons = new List<object>();
        var coverageComparisons = new List<object>();
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var passed = true;
        foreach (var scale in new[] { 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(FractionalWidth * scale), (int)(CompositionHeight * scale), scale);
            var percent = (int)(scale * 100);
            var raster = Capture("raster", new SkiaRasterRenderBackend(), new RasterUiTarget(),
                ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); }, ComposeRasterTypography);
            Dictionary<TextComposition, ComposedText> openGl;
            using (var window = new SilkWindowHost(320, 240, "Text composition OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                hardware.Add(new { Scale = scale, Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                openGl = Capture("opengl", new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window),
                    ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); },
                    ui => OpenGLProbe.ComposeOnFramebuffer(gl, ui, CompositionColors));
                Require(gl.GetError() == GLEnum.NoError, "Text composition OpenGL generated an error.");
            }

            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            Dictionary<TextComposition, ComposedText> vulkan;
            using (device)
            using (var compositor = new VulkanHostCompositor(device, viewport.Width, viewport.Height))
            {
                hardware.Add(VulkanHardware(device));
                vulkan = Capture("vulkan", new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device),
                    ui => ReadVulkanText(ui, (info, returned) => VulkanHostReadback.Read(device, info, returned)),
                    ui => ReadVulkanText(ui, (info, returned) => compositor.Compose(info, CompositionColors, returned)));
            }
            validationReports.Add(new { Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
            passed &= device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0;

            Compare("raster", raster, "opengl", openGl, 1);
            Compare("raster", raster, "vulkan", vulkan, 1);
            Compare("opengl", openGl, "vulkan", vulkan, 0);

            Dictionary<TextComposition, ComposedText> Capture(string backend, IRenderBackend renderer, UiRenderTarget target,
                Func<UiSession, byte[]> read, Func<UiSession, byte[]> compose)
            {
                var result = new Dictionary<TextComposition, ComposedText>();
                foreach (var composition in Enum.GetValues<TextComposition>())
                {
                    var frame = ExerciseComposition(renderer, target, viewport, composition, read, compose);
                    result.Add(composition, frame);
                    var stem = $"{backend}-{percent}-{CompositionName(composition)}";
                    var path = stem + ".png";
                    var crop = $"{stem}-text-{FractionalZoom}x.png";
                    var region = DeviceRect(frame.Bounds, scale);
                    WritePng(Path.Combine(output, path), viewport.Width, viewport.Height, frame.Pixels);
                    WriteEnlargedCrop(Path.Combine(output, crop), frame.Pixels, viewport.Width, region, FractionalZoom);
                    string? texture = null;
                    if (frame.Texture is { } pixels)
                    {
                        texture = stem + "-source.png";
                        WritePng(Path.Combine(output, texture), viewport.Width, viewport.Height, pixels);
                    }
                    captures.Add(new { Scale = scale, viewport.Width, viewport.Height, Backend = backend, Case = CompositionName(composition),
                        File = path, Crop = crop, Texture = texture, Bounds = frame.Bounds, CropBounds = PixelBounds(region),
                        frame.MaximumBlendError, frame.Samples, frame.CaptureMilliseconds });
                }
                var opaque = result[TextComposition.Opaque];
                foreach (var composition in new[] { TextComposition.Translucent, TextComposition.Offscreen })
                {
                    var frame = result[composition];
                    var layoutMatches = opaque.Bounds == frame.Bounds;
                    passed &= layoutMatches;
                    var difference = PixelDifference.Compare(opaque.Coverage, frame.Coverage, viewport.Width, viewport.Height, 2,
                        region: DeviceRect(opaque.Bounds, scale));
                    coverageComparisons.Add(new { Scale = scale, Backend = backend, Case = CompositionName(composition), LayoutMatches = layoutMatches,
                        difference.DifferentPixels, difference.OutsideTolerancePixels, difference.MaximumChannelDifference, difference.MeanChannelDifference });
                }
                return result;
            }

            void Compare(string reference, Dictionary<TextComposition, ComposedText> first,
                string backend, Dictionary<TextComposition, ComposedText> second, int edgeRadius)
            {
                foreach (var (composition, expected) in first)
                {
                    var actual = second[composition];
                    var result = PixelDifference.Compare(expected.Pixels, actual.Pixels, viewport.Width, viewport.Height, 2, edgeRadius);
                    var layoutMatches = expected.Bounds == actual.Bounds;
                    passed &= layoutMatches && result.Passed;
                    var diff = $"diff-{reference}-{backend}-{percent}-{CompositionName(composition)}.png";
                    WritePng(Path.Combine(output, diff), viewport.Width, viewport.Height, PixelDifference.Diff(expected.Pixels, actual.Pixels));
                    comparisons.Add(new { Scale = scale, Case = CompositionName(composition), Reference = reference, Backend = backend, LayoutMatches = layoutMatches, Result = result, Diff = diff });
                }
            }
        }

        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var hashes = new[] { SkiaNativeLibrary.LoadedPath!, Path.Combine(fontDirectory, "SegUIVar.ttf"), Path.Combine(fontDirectory, "segoeui.ttf") }
            .Where(File.Exists).Select(file => new { File = file, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) });
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, Profile = "typography-composition", PhysicalDpi = false, ValidationEnabled = validation,
            FontFamily = Theme.Light.FontFamily, FontSizes = FractionalFontSizes, Weights = new[] { 400, 600 }, PanelAlpha,
            Background = new { Left = CompositionColors.Left.ToString(), Right = CompositionColors.Right.ToString() },
            CropMagnification = FractionalZoom, Tolerance = new { Flat = 2, CpuGpuEdgeRadius = 1, CpuGpuEdge = 24, GpuGpuEdgeRadius = 0, Blend = 1 },
            CoverageMethod = "Offscreen uses texture alpha; opaque and translucent recover glyph coverage from red-channel contrast against the known background. Coverage comparisons use two levels of rounding allowance.",
            SkiaSharp = typeof(SKFont).Assembly.FullName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
            Hashes = hashes, Hardware = hardware, Captures = captures, Comparisons = comparisons, CoverageComparisons = coverageComparisons, ValidationReports = validationReports
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Text composition: {captures.Count} frames, {comparisons.Count} backend comparisons and {coverageComparisons.Count} coverage comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static string CompositionName(TextComposition composition) => composition.ToString().ToLowerInvariant();

    private static ComposedText ExerciseComposition(IRenderBackend backend, UiRenderTarget target, UiViewport viewport,
        TextComposition composition, Func<UiSession, byte[]> read, Func<UiSession, byte[]> compose)
    {
        var started = Stopwatch.GetTimestamp();
        var theme = Theme.Light;
        var samples = CreateFractionalSamples();
        var panel = new Panel { Background = composition == TextComposition.Offscreen ? Color.Transparent : theme.Input };
        panel.Add(samples);
        Panel root = panel;
        if (composition == TextComposition.Translucent)
        {
            panel.Opacity = PanelAlpha / 255f;
            root = new TextCompositionBackdrop();
            root.Add(panel);
        }
        using var ui = UiSession.Create(root, backend, target, viewport, null, new UiDispatchOptions(), configurePresenters: ConfigureTypographyPresenters);
        ui.SetTheme(theme);
        Require(ui.Update() && !ui.Update(), "Text composition must draw exactly one retained frame.");

        byte[]? texture = null;
        int? blendError = null;
        byte[] pixels;
        if (composition == TextComposition.Offscreen)
        {
            pixels = compose(ui);
            texture = read(ui);
            Require(ReadPixel(texture, viewport.Width, 0, 0) == "00000000", "Text texture must be transparent outside the glyphs.");
            Require(texture.Where((_, index) => index % 4 == 3).Any(alpha => alpha is > 0 and < 255), "Text texture must have antialiased edges.");
            blendError = VerifyTextTextureBlend(texture, pixels, viewport.Width);
        }
        else pixels = read(ui);

        Require(pixels.Where((_, index) => index % 4 == 3).All(alpha => alpha == 255), "Text composition must have an opaque output.");
        var white = new SKColor(theme.Input.R, theme.Input.G, theme.Input.B);
        if (composition == TextComposition.Translucent)
        {
            VerifyPanelBackground(0, CompositionColors.Left);
            VerifyPanelBackground(viewport.Width - 1, CompositionColors.Right);
        }
        var coverage = new byte[pixels.Length];
        var measures = samples.Children.SelectMany(row => row.Children).Cast<Text>().Select(Measure).ToArray();
        var bounds = samples.Bounds;
        ui.Dispose();
        Require(root.IsDisposed && samples.IsDisposed, "Text composition fixture did not dispose its tree.");
        return new(bounds, pixels, texture, coverage, measures, blendError, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        void VerifyPanelBackground(int x, SKColor background)
        {
            var offset = (viewport.Width * (viewport.Height - 1) + x) * 4;
            Require(Math.Abs(pixels[offset] - BlendColor(white.Red, background.Red)) <= 1 &&
                Math.Abs(pixels[offset + 1] - BlendColor(white.Green, background.Green)) <= 1 &&
                Math.Abs(pixels[offset + 2] - BlendColor(white.Blue, background.Blue)) <= 1, "Panel opacity must blend the background once.");
        }

        object Measure(Text sample)
        {
            var region = DeviceRect(sample.Bounds, viewport.Scale);
            var ink = 0;
            var peak = 0;
            var left = region.Right;
            var top = region.Bottom;
            var right = region.Left;
            var bottom = region.Top;
            for (var y = region.Top; y < region.Bottom; y++)
            for (var x = region.Left; x < region.Right; x++)
            {
                var offset = (y * viewport.Width + x) * 4;
                var alpha = texture == null ? RecoverCoverage(x, pixels[offset], sample.Foreground.R) : texture[offset + 3];
                coverage[offset] = coverage[offset + 1] = coverage[offset + 2] = (byte)alpha;
                peak = Math.Max(peak, alpha);
                if (alpha <= 2) continue;
                ink++;
                left = Math.Min(left, x); right = Math.Max(right, x + 1);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
            }
            Require(ink > 20 && peak > 128, $"Missing glyph coverage for {sample.FontSize}/{sample.FontWeight} in {composition}.");
            return new { sample.FontSize, sample.FontWeight, sample.Bounds, InkPixels = ink, PeakCoverage = peak, InkBounds = PixelBounds(new(left, top, right, bottom)) };
        }

        int RecoverCoverage(int x, byte actual, byte ink)
        {
            var background = (float)white.Red;
            var foreground = (float)ink;
            if (composition == TextComposition.Translucent)
            {
                var hostRed = (x < viewport.Width / 2 ? CompositionColors.Left : CompositionColors.Right).Red;
                background = BlendColor(white.Red, hostRed);
                foreground = BlendColor(ink, hostRed);
            }
            return Math.Clamp((int)MathF.Round((background - actual) * 255 / (background - foreground)), 0, 255);
        }
    }

    private static int BlendColor(byte source, byte background) => (source * PanelAlpha + background * (255 - PanelAlpha) + 127) / 255;

    private static int VerifyTextTextureBlend(byte[] texture, byte[] pixels, int width)
    {
        var left = new[] { CompositionColors.Left.Red, CompositionColors.Left.Green, CompositionColors.Left.Blue };
        var right = new[] { CompositionColors.Right.Red, CompositionColors.Right.Green, CompositionColors.Right.Blue };
        var maximum = 0;
        for (var offset = 0; offset < texture.Length; offset += 4)
        {
            var colors = offset / 4 % width < width / 2 ? left : right;
            var alpha = texture[offset + 3];
            for (var channel = 0; channel < 3; channel++)
            {
                var source = texture[offset + channel];
                Require(source <= alpha, "The intermediate texture must contain premultiplied RGBA.");
                var expected = source + (colors[channel] * (255 - alpha) + 127) / 255;
                maximum = Math.Max(maximum, Math.Abs(expected - pixels[offset + channel]));
            }
        }
        Require(maximum <= 1, $"Text texture composition differs from the premultiplied blend by {maximum}/255.");
        return maximum;
    }

    private static byte[] ComposeRasterTypography(UiSession ui)
    {
        var width = ui.Viewport.Width;
        var height = ui.Viewport.Height;
        using var source = ui.SnapshotRasterImage();
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var paint = new SKPaint { Color = CompositionColors.Right };
        surface.Canvas.Clear(CompositionColors.Left);
        surface.Canvas.DrawRect(width / 2, 0, width / 2, height, paint);
        surface.Canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        using var composed = surface.Snapshot();
        return RasterProbe.ReadSnapshot(composed);
    }

    private static byte[] ReadVulkanText(UiSession ui, Func<VulkanTextureInfo, Action<ImageLayout>, byte[]> read)
    {
        var lease = ui.AcquireVulkanTexture();
        var layout = (ImageLayout)lease.Info.Layout;
        try { return read(lease.Info, state => layout = state); }
        finally { lease.Return(layout); }
    }

    private sealed class TextCompositionBackdrop : Panel;
}
