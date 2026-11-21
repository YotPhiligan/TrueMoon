using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.OpenGL;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private sealed record TextMode(string Name, SKFontEdging? Edging = null, SKFontHinting? Hinting = null, bool? Subpixel = null);
    private static readonly TextMode[] TextModes =
    [
        new("default"),
        new("aliased", Edging: SKFontEdging.Alias),
        new("grayscale", Edging: SKFontEdging.Antialias),
        new("lcd-request", Edging: SKFontEdging.SubpixelAntialias),
        new("hint-none", Hinting: SKFontHinting.None),
        new("hint-slight", Hinting: SKFontHinting.Slight),
        new("hint-full", Hinting: SKFontHinting.Full),
        new("position-integer", Subpixel: false),
        new("position-subpixel", Subpixel: true)
    ];
    private static readonly SKColor CompositionPanel = ToSkColor(Theme.Light.Input);
    private readonly record struct ModeCase(TextComposition Composition, string Mode, int HalfPixels);
    private sealed record TextSample(string Value, Rect Bounds, Rect Content, SKColor Color, float Size, int Weight);
    private sealed record ModeFrame(byte[] Pixels, byte[]? Source = null);

    internal static void RunRenderingModes(string output, bool validation)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        using var regular = MatchDefaultFace(400);
        using var semibold = MatchDefaultFace(600);
        using var defaults = new SKFont(regular, 12);
        var captures = new List<object>();
        var comparisons = new List<object>();
        var backendComparisons = new List<object>();
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var passed = true;

        foreach (var scale in new[] { 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(FractionalWidth * scale), (int)(CompositionHeight * scale), scale);
            var (bounds, samples) = LayoutTextSamples(viewport);
            var region = DeviceRect(bounds, scale);
            var (raster, openGl, vulkan, validationPassed) = CaptureTypographyBackends(viewport, validation, hardware, validationReports, Capture);
            passed &= validationPassed;

            CompareBackends("raster", raster, "opengl", openGl, 1);
            CompareBackends("raster", raster, "vulkan", vulkan, 1);
            CompareBackends("opengl", openGl, "vulkan", vulkan, 0);

            Dictionary<ModeCase, ModeFrame> Capture(string backend, Func<Action<SKCanvas>, TextComposition, ModeFrame> render)
            {
                var frames = new Dictionary<ModeCase, ModeFrame>();
                foreach (var composition in Enum.GetValues<TextComposition>())
                foreach (var mode in TextModes)
                for (var phase = 0; phase <= 1; phase++)
                {
                    var offset = phase * 0.5f;
                    var frame = render(canvas => PaintTextMode(canvas, viewport, composition, mode, offset, samples, regular, semibold), composition);
                    frames.Add(new(composition, mode.Name, phase), frame);
                    var stem = $"{backend}-{(int)(scale * 100)}-{CompositionName(composition)}-{mode.Name}-x{phase * 50}";
                    var file = stem + ".png";
                    var crop = $"{stem}-text-{FractionalZoom}x.png";
                    WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, frame.Pixels);
                    WriteEnlargedCrop(Path.Combine(output, crop), frame.Pixels, viewport.Width, region, FractionalZoom);
                    string? texture = null;
                    int? blendError = null;
                    int? partialAlpha = null;
                    if (frame.Source is { } source)
                    {
                        texture = stem + "-source.png";
                        WritePng(Path.Combine(output, texture), viewport.Width, viewport.Height, source);
                        Require(ReadPixel(source, viewport.Width, 0, 0) == "00000000", "Text texture must have a transparent background.");
                        blendError = VerifyTextTextureBlend(source, frame.Pixels, viewport.Width);
                        partialAlpha = source.Where((alpha, index) => index % 4 == 3 && alpha is > 0 and < 255).Count();
                        Require(mode.Edging == SKFontEdging.Alias ? partialAlpha == 0 : partialAlpha > 0, $"Unexpected alpha edges in {stem}.");
                    }
                    Require(frame.Pixels.Where((alpha, index) => index % 4 == 3).All(alpha => alpha == 255), $"Expected opaque output in {stem}.");
                    var measures = samples.Select(sample => MeasureModeInk(frame, viewport, sample)).ToArray();
                    captures.Add(new { Scale = scale, viewport.Width, viewport.Height, Backend = backend, Case = CompositionName(composition),
                        Mode = mode.Name, OffsetPx = offset, File = file, Crop = crop, Texture = texture, CropBounds = PixelBounds(region),
                        BlendError = blendError, PartialAlphaPixels = partialAlpha, Samples = measures });
                }

                foreach (var (key, frame) in frames)
                {
                    if (key.Mode != "default") Record("default", key with { Mode = "default" });
                    if (key.Mode == "lcd-request") Record("grayscale", key with { Mode = "grayscale" });
                    if (key.Mode == "position-subpixel") Record("position-integer", key with { Mode = "position-integer" });
                    if (key.HalfPixels == 1) Record("offset-zero", key with { HalfPixels = 0 });

                    void Record(string reference, ModeCase expected) => comparisons.Add(new
                    {
                        Scale = scale, Backend = backend, Case = CompositionName(key.Composition), key.Mode, Reference = reference,
                        OffsetPx = key.HalfPixels * 0.5f,
                        Result = PixelDifference.Compare(frames[expected].Pixels, frame.Pixels, viewport.Width, viewport.Height, 0, region: region)
                    });
                }
                return frames;
            }

            void CompareBackends(string first, Dictionary<ModeCase, ModeFrame> expected, string second, Dictionary<ModeCase, ModeFrame> actual, int edgeRadius)
            {
                foreach (var (key, frame) in expected)
                {
                    var difference = PixelDifference.Compare(frame.Pixels, actual[key].Pixels, viewport.Width, viewport.Height, 2, edgeRadius);
                    passed &= difference.Passed;
                    backendComparisons.Add(new { Scale = scale, Case = CompositionName(key.Composition), key.Mode,
                        OffsetPx = key.HalfPixels * 0.5f, Reference = first, Backend = second, Result = difference });
                }
            }
        }

        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var fontPath = Path.Combine(fontDirectory, "SegUIVar.ttf");
        var hashes = new[] { SkiaNativeLibrary.LoadedPath!, fontPath }.Where(File.Exists)
            .Select(path => new { File = path, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, Profile = "typography-rendering-modes", PhysicalDpi = false, ValidationEnabled = validation,
            FontFamily = regular.FamilyName, FontSizes = FractionalFontSizes, Weights = new[] { 400, 600 }, PanelAlpha,
            LogicalWidth = FractionalWidth, LogicalHeight = CompositionHeight,
            Background = new { Left = CompositionColors.Left.ToString(), Right = CompositionColors.Right.ToString() },
            Defaults = new { defaults.Edging, defaults.Hinting, defaults.Subpixel, defaults.BaselineSnap }, Modes = TextModes,
            CropMagnification = FractionalZoom, DifferenceScope = "Variant and phase comparisons are diagnostic; layout uses the default Alloy text metrics.",
            Tolerance = new { Flat = 2, CpuGpuEdgeRadius = 1, CpuGpuEdge = 24, GpuGpuEdgeRadius = 0, Blend = 1 },
            SkiaSharp = typeof(SKFont).Assembly.FullName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
            Hashes = hashes, Hardware = hardware, Captures = captures, Comparisons = comparisons, BackendComparisons = backendComparisons, ValidationReports = validationReports
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
        Console.WriteLine($"Typography modes: {captures.Count} frames, {comparisons.Count} mode and phase comparisons, {backendComparisons.Count} backend comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static (T Raster, T OpenGL, T Vulkan, bool ValidationPassed) CaptureTypographyBackends<T>(UiViewport viewport,
        bool validation, List<object> hardware, List<object> validationReports,
        Func<string, Func<Action<SKCanvas>, TextComposition, ModeFrame>, T> capture)
    {
        var scale = viewport.Scale;
        var info = new SKImageInfo(viewport.Width, viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var raster = CaptureSkia("raster", null);
        T openGl;
        using (var window = new SilkWindowHost(320, 240, "Typography modes OpenGL", true))
        using (var gl = GL.GetApi(window.NativeWindow))
        {
            var nativeContext = window.NativeWindow.GLContext ?? throw new NotSupportedException("Missing host GL context.");
            nativeContext.MakeCurrent();
            using var api = GRGlInterface.CreateOpenGl(name => nativeContext.TryGetProcAddress(name, out var address) ? address : 0)
                            ?? throw new NotSupportedException("Could not load the desktop OpenGL interface.");
            Require(api.Validate(), "Invalid desktop OpenGL interface.");
            using var context = GRContext.CreateGl(api) ?? throw new NotSupportedException("Could not create a Skia OpenGL context.");
            hardware.Add(new { Scale = scale, Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
            openGl = CaptureSkia("opengl", context);
            Require(gl.GetError() == GLEnum.NoError, "Typography modes generated an OpenGL error.");
        }

        var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        T vulkan;
        using (device)
        using (var extensions = GRVkExtensions.Create(device.GetProcedureAddress, device.Instance.Handle, device.PhysicalDevice.Handle,
                   device.InstanceExtensions, device.DeviceExtensions))
        using (var backend = new GRVkBackendContext
               {
                   VkInstance = device.Instance.Handle, VkPhysicalDevice = device.PhysicalDevice.Handle, VkDevice = device.Device.Handle,
                   VkQueue = device.Queue.Handle, GraphicsQueueIndex = device.QueueFamily, MaxAPIVersion = Silk.NET.Vulkan.Vk.Version11,
                   Extensions = extensions, GetProcedureAddress = device.GetProcedureAddress
               })
        using (var context = GRContext.CreateVulkan(backend) ?? throw new NotSupportedException("Could not create a Skia Vulkan context."))
        using (var surface = SilkVulkanHost.CreateSurface(device, context, new(viewport.Width, viewport.Height)))
        using (var compositor = new VulkanHostCompositor(device, viewport.Width, viewport.Height))
        {
            hardware.Add(VulkanHardware(device));
            vulkan = capture("vulkan", (draw, composition) =>
            {
                surface.Draw(draw);
                var lease = surface.Acquire();
                var layout = (Silk.NET.Vulkan.ImageLayout)lease.Info.Layout;
                void SetLayout(Silk.NET.Vulkan.ImageLayout value) => layout = value;
                try
                {
                    var source = lease.Info;
                    if (composition != TextComposition.Offscreen) return new(VulkanHostReadback.Read(device, source, SetLayout));
                    var pixels = compositor.Compose(source, CompositionColors, SetLayout);
                    source.Layout = (uint)layout;
                    return new(pixels, VulkanHostReadback.Read(device, source, SetLayout));
                }
                finally { lease.Return(layout); }
            });
        }
        validationReports.Add(new { Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
        return (raster, openGl, vulkan, device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0);

        T CaptureSkia(string backend, GRContext? context)
        {
            using var surface = CreateSurface();
            using var destination = CreateSurface();
            return capture(backend, (draw, composition) =>
            {
                draw(surface.Canvas);
                using var source = surface.Snapshot();
                Require(source.IsTextureBacked == (context != null), $"Unexpected {backend} text surface backing.");
                if (composition != TextComposition.Offscreen) return new(RasterProbe.ReadSnapshot(source));
                PaintCompositionBackground(destination.Canvas, viewport.Width, viewport.Height);
                destination.Canvas.DrawImage(source, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
                using var composed = destination.Snapshot();
                return new(RasterProbe.ReadSnapshot(composed), RasterProbe.ReadSnapshot(source));
            });

            SKSurface CreateSurface() => (context == null ? SKSurface.Create(info) : SKSurface.Create(context, false, info))
                ?? throw new NotSupportedException($"Could not allocate a {backend} text surface.");
        }
    }

    private static SKTypeface MatchDefaultFace(int weight)
    {
        using var style = new SKFontStyle(weight, 5, SKFontStyleSlant.Upright);
        var face = SKFontManager.Default.MatchFamily(Theme.Light.FontFamily, style);
        if (face != null && string.Equals(face.FamilyName, Theme.Light.FontFamily, StringComparison.OrdinalIgnoreCase)) return face;
        face?.Dispose();
        throw new NotSupportedException($"The typography modes probe requires {Theme.Light.FontFamily}.");
    }

    private static (Rect Bounds, TextSample[] Samples) LayoutTextSamples(UiViewport viewport)
    {
        var theme = Theme.Light;
        var text = CreateFractionalSamples();
        var root = new Panel { Background = theme.Input };
        root.Add(text);
        using var session = UiSession.Create(root, new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport);
        session.SetTheme(theme);
        Require(session.Update(), "The typography layout control did not render.");
        return (text.Bounds, text.Children.SelectMany(row => row.Children).Cast<Text>()
            .Select(sample => new TextSample(sample.Value, sample.Bounds, sample.Bounds.Deflate(sample.Padding),
                ToSkColor(sample.Foreground), sample.FontSize, sample.FontWeight)).ToArray());
    }

    private static void PaintTextMode(SKCanvas canvas, UiViewport viewport, TextComposition composition, TextMode mode,
        float offsetPx, TextSample[] samples, SKTypeface regular, SKTypeface semibold)
    {
        canvas.Clear(composition == TextComposition.Opaque ? CompositionPanel : SKColors.Transparent);
        var layers = canvas.SaveCount;
        if (composition == TextComposition.Translucent)
        {
            PaintCompositionBackground(canvas, viewport.Width, viewport.Height);
            using var opacity = new SKPaint { Color = SKColors.White.WithAlpha(PanelAlpha) };
            canvas.SaveLayer(new SKRect(0, 0, viewport.Width, viewport.Height), opacity);
            canvas.Clear(CompositionPanel);
        }
        canvas.Save();
        canvas.Scale(viewport.Scale);
        using var paint = new SKPaint { IsAntialias = true };
        foreach (var sample in samples)
        {
            using var font = new SKFont(sample.Weight == 400 ? regular : semibold, sample.Size);
            var y = sample.Content.Y - font.Metrics.Ascent;
            font.Edging = mode.Edging ?? font.Edging;
            font.Hinting = mode.Hinting ?? font.Hinting;
            font.Subpixel = mode.Subpixel ?? font.Subpixel;
            paint.Color = sample.Color;
            var clip = sample.Bounds;
            canvas.Save();
            canvas.ClipRect(new SKRect(clip.X, clip.Y, clip.X + clip.Width, clip.Y + clip.Height));
            canvas.DrawText(sample.Value, sample.Content.X + offsetPx / viewport.Scale, y, SKTextAlign.Left, font, paint);
            canvas.Restore();
        }
        canvas.RestoreToCount(layers);
    }

    private static object MeasureModeInk(ModeFrame frame, UiViewport viewport, TextSample sample)
    {
        var region = DeviceRect(sample.Bounds, viewport.Scale);
        var ink = 0;
        var left = region.Right;
        var top = region.Bottom;
        var right = region.Left;
        var bottom = region.Top;
        var totalAlpha = 0L;
        var weightedX = 0L;
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
        {
            var offset = (y * viewport.Width + x) * 4;
            var background = ((viewport.Height - 1) * viewport.Width + x) * 4;
            if (frame.Source is { } source)
            {
                var alpha = source[offset + 3];
                totalAlpha += alpha;
                weightedX += x * alpha;
            }
            if (frame.Pixels.AsSpan(offset, 4).SequenceEqual(frame.Pixels.AsSpan(background, 4))) continue;
            ink++;
            left = Math.Min(left, x); right = Math.Max(right, x + 1);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
        }
        Require(ink > 20, $"Missing glyphs for size {sample.Size} and weight {sample.Weight}.");
        double? alphaCentroidX = null;
        if (frame.Source != null)
        {
            Require(totalAlpha > 0, $"Missing texture alpha for size {sample.Size} and weight {sample.Weight}.");
            alphaCentroidX = (double)weightedX / totalAlpha + 0.5;
        }
        return new { sample.Size, sample.Weight, InkPixels = ink, InkBounds = PixelBounds(new(left, top, right, bottom)), AlphaCentroidX = alphaCentroidX };
    }

    private static void PaintCompositionBackground(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(CompositionColors.Left);
        using var paint = new SKPaint { Color = CompositionColors.Right };
        canvas.DrawRect(width / 2, 0, width - width / 2, height, paint);
    }

    private static SKColor ToSkColor(Color color) => new(color.R, color.G, color.B, color.A);
}
