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
    private const int FractionalWidth = 368;
    private const int FractionalHeight = 208;
    private const int FractionalZoom = 4;
    private const string EditorSample = "Привет · Alloy 012";
    private const int SelectedLength = 6;
    private static readonly int[] FractionalFontSizes = [11, 12, 14];

    private sealed record FractionalLayout(Rect Text, Rect Editor, Rect Lines);
    private sealed record FractionalCapture(FractionalLayout Layout, byte[] Caret, byte[] Selection,
        SKRectI CaretInk, SKRectI SelectionInk, double CaptureMilliseconds);

    internal static void RunFractional(string output, bool validation)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        var captures = new List<object>();
        var comparisons = new List<object>();
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var passed = true;

        foreach (var scale in new[] { 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(FractionalWidth * scale), (int)(FractionalHeight * scale), scale);
            var percent = (int)(scale * 100);
            var raster = ExerciseFractional(new SkiaRasterRenderBackend(), new RasterUiTarget(), viewport,
                ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
            FractionalCapture openGl;
            using (var window = new SilkWindowHost(320, 240, "Fractional typography OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                hardware.Add(new { Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                openGl = ExerciseFractional(new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window), viewport,
                    ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); });
                Require(gl.GetError() == GLEnum.NoError, "Fractional typography OpenGL generated an error.");
            }
            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            FractionalCapture vulkan;
            using (device)
            {
                hardware.Add(VulkanHardware(device));
                vulkan = ExerciseFractional(new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device), viewport, ui =>
                {
                    var lease = ui.AcquireVulkanTexture();
                    var layout = (ImageLayout)lease.Info.Layout;
                    try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                    finally { lease.Return(layout); }
                });
                device.WaitIdle();
            }
            validationReports.Add(new { Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
            Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Fractional typography Vulkan validation failed.");

            Record("raster", raster);
            Record("opengl", openGl);
            Record("vulkan", vulkan);
            Compare("raster", raster, "opengl", openGl, edgeRadius: 1);
            Compare("raster", raster, "vulkan", vulkan, edgeRadius: 1);
            Compare("opengl", openGl, "vulkan", vulkan, edgeRadius: 0);

            void Record(string backend, FractionalCapture capture)
            {
                Save("caret", capture.Caret);
                Save("selection", capture.Selection);

                void Save(string state, byte[] pixels)
                {
                    var stem = $"{backend}-{percent}-{state}";
                    var file = stem + ".png";
                    WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, pixels);
                    var regions = Regions(capture.Layout, viewport);
                    var crops = new Dictionary<string, string>();
                    foreach (var (name, region) in regions)
                    {
                        var cropFile = $"{stem}-{name}-{FractionalZoom}x.png";
                        WriteEnlargedCrop(Path.Combine(output, cropFile), pixels, viewport.Width, region, FractionalZoom);
                        crops.Add(name, cropFile);
                    }
                    captures.Add(new
                    {
                        Backend = backend, State = state, viewport.Width, viewport.Height, viewport.Scale, File = file, Crops = crops,
                        capture.Layout, Regions = regions.ToDictionary(pair => pair.Key, pair => PixelBounds(pair.Value)),
                        CaretInk = PixelBounds(capture.CaretInk), SelectionInk = PixelBounds(capture.SelectionInk), capture.CaptureMilliseconds
                    });
                }
            }

            void Compare(string reference, FractionalCapture expected, string backend, FractionalCapture actual, int edgeRadius)
            {
                CompareState("caret", expected.Caret, actual.Caret);
                CompareState("selection", expected.Selection, actual.Selection);

                void CompareState(string state, byte[] first, byte[] second)
                {
                    var result = PixelDifference.Compare(first, second, viewport.Width, viewport.Height, 2, edgeRadius);
                    var layoutMatches = expected.Layout == actual.Layout;
                    passed &= layoutMatches && result.Passed;
                    var regions = Regions(expected.Layout, viewport).ToDictionary(pair => pair.Key, pair =>
                        PixelDifference.Compare(first, second, viewport.Width, viewport.Height, 2, edgeRadius, region: pair.Value));
                    var diff = $"diff-{reference}-{backend}-{percent}-{state}.png";
                    WritePng(Path.Combine(output, diff), viewport.Width, viewport.Height, PixelDifference.Diff(first, second));
                    comparisons.Add(new { Scale = scale, State = state, Reference = reference, Backend = backend,
                        LayoutMatches = layoutMatches, Result = result, Regions = regions, Diff = diff });
                }
            }
        }

        var fontDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var hashes = new[] { SkiaNativeLibrary.LoadedPath!, Path.Combine(fontDirectory, "SegUIVar.ttf"), Path.Combine(fontDirectory, "segoeui.ttf") }
            .Where(File.Exists).Select(file => new { File = file, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) });
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, Profile = "fractional-typography", PhysicalDpi = false, ValidationEnabled = validation,
            LogicalWidth = FractionalWidth, LogicalHeight = FractionalHeight, CropMagnification = FractionalZoom,
            Theme = "Light", FontFamily = Theme.Light.FontFamily, FontSizes = FractionalFontSizes, Weights = new[] { 400, 600 },
            EditorFontSize = 12, EditorSample, CaretIndex = SelectedLength, SelectionStart = 0, SelectionLength = SelectedLength,
            LineWeight = 1, Tolerance = new { Flat = 2, CpuGpuEdgeRadius = 1, CpuGpuEdge = 24, GpuGpuEdgeRadius = 0 },
            SkiaSharp = typeof(SKFont).Assembly.FullName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
            Hashes = hashes, Hardware = hardware, Captures = captures, Comparisons = comparisons, ValidationReports = validationReports,
            TimingScope = "One scene construction, input sequence, three readbacks and disposal per scale and backend."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Fractional typography: {captures.Count} frames and {comparisons.Count} comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static FractionalCapture ExerciseFractional(IRenderBackend backend, UiRenderTarget target, UiViewport viewport, Func<UiSession, byte[]> read)
    {
        var started = Stopwatch.GetTimestamp();
        var theme = Theme.Light;
        var root = new Panel { Background = theme.Input };
        var text = new VStack { Margin = new(24, 20, 0, 0), Spacing = 4, Width = 304,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        foreach (var size in FractionalFontSizes)
        {
            var row = new HStack { Spacing = 8 };
            row.Add(new Text($"{size} · Aa 012 Ёй") { FontSize = size, FontWeight = 400, Width = 144 });
            row.Add(new Text($"{size} · Aa 012 Ёй") { FontSize = size, FontWeight = 600, Width = 144 });
            text.Add(row);
        }
        var editor = new TextBox { Value = EditorSample, FontSize = 12, Width = 304, Height = 28, Margin = new(24.5f, 107.5f, 0, 0),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        var lines = new FractionalLines { Width = 248, Height = 32, Margin = new(24, 159, 0, 0),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        root.Add(text); root.Add(editor); root.Add(lines);
        using var ui = UiSession.Create(root, backend, target, viewport);
        ui.SetTheme(theme);
        ui.Update();
        ui.Focus(editor);
        Key(UiKey.Home);
        ui.Update();
        Require(editor.IsFocused && editor.CaretIndex == 0 && editor.SelectionLength == 0, "Caret reference did not start at the beginning of the field.");
        var startCaret = read(ui);
        for (var index = 0; index < SelectedLength; index++) Key(UiKey.Right);
        ui.Update();
        Require(editor.IsFocused && editor.CaretIndex == SelectedLength && editor.SelectionLength == 0, "Caret fixture reached the wrong text state.");
        var caret = read(ui);

        Key(UiKey.Home);
        for (var index = 0; index < SelectedLength; index++) Key(UiKey.Right, shift: true);
        ui.Update();
        Require(editor.IsFocused && editor.CaretIndex == SelectedLength && editor.SelectionStart == 0 && editor.SelectionLength == SelectedLength &&
            editor.Value == EditorSample, "Selection fixture reached the wrong text state.");
        var selection = read(ui);
        Require(!ui.Update(), "Fractional fixture did not become idle.");
        var layout = new FractionalLayout(text.Bounds, editor.Bounds, lines.Bounds);
        var interior = editor.Bounds.Deflate(new Thickness(2));
        // Skip the old caret at the start of the text while keeping the end of the six-letter prefix.
        var afterStart = DeviceRect(interior.Deflate(new Thickness(20, 0, 0, 0)), viewport.Scale);
        var caretInk = ChangedInk(startCaret, caret, viewport.Width, afterStart);
        var selectionInk = ChangedInk(caret, selection, viewport.Width, DeviceRect(interior, viewport.Scale));
        Require(caretInk.Width is >= 1 and <= 4 && caretInk.Height > 5 && selectionInk.Width > 10 && selectionInk.Height > 5,
            $"Missing caret or selection pixels: {caretInk}, {selectionInk}.");
        Require(text.Children.All(row => ContainsInk(startCaret, viewport.Width, DeviceRect(row.Bounds, viewport.Scale))) &&
            ContainsInk(startCaret, viewport.Width, DeviceRect(lines.Bounds, viewport.Scale)), "Missing text or line samples.");
        ui.Dispose();
        Require(root.IsDisposed && editor.IsDisposed && lines.IsDisposed, "Fractional fixture did not dispose its tree.");
        return new(layout, caret, selection, caretInk, selectionInk, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        void Key(UiKey key, bool shift = false) => ui.HandleInput(new(InputKind.KeyDown, Key: key, Shift: shift));
    }

    private static Dictionary<string, SKRectI> Regions(FractionalLayout layout, UiViewport viewport) => new()
    {
        ["text"] = DeviceRect(layout.Text, viewport.Scale),
        ["editor"] = DeviceRect(layout.Editor, viewport.Scale),
        ["lines"] = DeviceRect(layout.Lines, viewport.Scale)
    };

    private static SKRectI DeviceRect(Rect bounds, float scale) =>
        new((int)MathF.Floor(bounds.X * scale), (int)MathF.Floor(bounds.Y * scale),
            (int)MathF.Ceiling((bounds.X + bounds.Width) * scale), (int)MathF.Ceiling((bounds.Y + bounds.Height) * scale));

    private static object PixelBounds(SKRectI bounds) => new { X = bounds.Left, Y = bounds.Top, bounds.Width, bounds.Height };

    private static SKRectI ChangedInk(byte[] before, byte[] after, int width, SKRectI region)
    {
        var left = region.Right; var top = region.Bottom; var right = region.Left; var bottom = region.Top;
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
        {
            var offset = (y * width + x) * 4;
            if (before.AsSpan(offset, 4).SequenceEqual(after.AsSpan(offset, 4))) continue;
            left = Math.Min(left, x); right = Math.Max(right, x + 1);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
        }
        return left < right ? new(left, top, right, bottom) : default;
    }

    private static bool ContainsInk(byte[] pixels, int width, SKRectI region)
    {
        // The fixture draws these samples on an opaque white background.
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
        {
            var offset = (y * width + x) * 4;
            if (pixels[offset] < 128 && pixels[offset + 1] < 128 && pixels[offset + 2] < 128) return true;
        }
        return false;
    }

    private sealed class FractionalLines : Element
    {
        protected override void DrawCore(IDrawingContext context)
        {
            context.Fill(new(Bounds.X + 4, Bounds.Y + 4, 72, 1), Foreground);
            context.Fill(new(Bounds.X + 4, Bounds.Y + 14.5f, 72, 1), Foreground);
            context.Fill(new(Bounds.X + 94, Bounds.Y + 4, 1, 24), Foreground);
            context.Fill(new(Bounds.X + 110.5f, Bounds.Y + 4, 1, 24), Foreground);
            context.Stroke(new(Bounds.X + 132, Bounds.Y + 5, 42, 20), Foreground, 1);
            context.Stroke(new(Bounds.X + 188.5f, Bounds.Y + 5.5f, 42, 20), Foreground, 1);
        }
    }
}
