using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.OpenGL;
using SkiaSharp;
using Topten.RichTextKit;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const string EmojiSample = "Готово ✅ · Автор 👩‍💻";
    private const string EmojiControl = "Готово · Автор";
    private const string EmojiFont = "Segoe UI Emoji";
    private const int EmojiFontSize = 18;
    private const int EmojiHeight = 104;
    private sealed record EmojiFrame(byte[] Pixels, Rect Editor, Rect Control, Size LineSize, float Baseline, Size ControlSize, float ControlBaseline,
        int[] Forward, int[] Reverse, float[] CaretXs, int CheckColorPixels, int JoinedColorPixels, PixelDifference Reference);

    internal static void RunEmoji(string output, bool validation)
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
        var fontFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var faces = new[] { FontProvenance(Path.Combine(fontFolder, "SegUIVar.ttf")), FontProvenance(Path.Combine(fontFolder, "seguiemj.ttf")) };
        var theme = Theme.Light;
        var block = ShapeEmojiReference(EmojiSample);
        var control = ShapeEmojiReference(EmojiControl);
        try
        {
            var runeCount = EmojiSample.EnumerateRunes().Count();
            var runs = block.FontRuns.Where(run => run.Start < runeCount)
                .Select(run => new { StartCodePoint = run.Start, LengthCodePoints = Math.Min(run.Length, runeCount - run.Start),
                    Family = run.Typeface.FamilyName, GlyphCount = run.Glyphs.Length }).ToArray();
            var fallback = runs.Where(run => run.Family == EmojiFont).ToArray();
            Require(fallback.Length == 2 && fallback[0] is { StartCodePoint: 7, LengthCodePoints: 1, GlyphCount: 1 } &&
                fallback[1] is { StartCodePoint: 17, LengthCodePoints: 3, GlyphCount: 1 } &&
                runs.All(run => run.Family == theme.FontFamily || run.Family == EmojiFont), "Expected two complete Segoe UI Emoji runs.");

            foreach (var scale in new[] { 1.25f, 1.75f })
            {
                var viewport = new UiViewport((int)(FractionalWidth * scale), (int)(EmojiHeight * scale), scale);
                var raster = Capture("raster", new SkiaRasterRenderBackend(), new RasterUiTarget(),
                    ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
                EmojiFrame openGl;
                using (var window = new SilkWindowHost(320, 240, "Emoji OpenGL", true))
                using (var gl = GL.GetApi(window.NativeWindow))
                {
                    hardware.Add(new { Scale = scale, Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                    openGl = Capture("opengl", new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window),
                        ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); });
                    Require(gl.GetError() == GLEnum.NoError, "Emoji OpenGL generated an error.");
                }
                var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
                EmojiFrame vulkan;
                using (device)
                {
                    hardware.Add(VulkanHardware(device));
                    vulkan = Capture("vulkan", new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device),
                        ui => ReadVulkanText(ui, (info, returned) => VulkanHostReadback.Read(device, info, returned)));
                }
                validationReports.Add(new { Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
                passed &= device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0;
                Compare("raster", raster, "opengl", openGl, 1);
                Compare("raster", raster, "vulkan", vulkan, 1);
                Compare("opengl", openGl, "vulkan", vulkan, 0);

                EmojiFrame Capture(string backend, IRenderBackend renderer, UiRenderTarget target, Func<UiSession, byte[]> read)
                {
                    var stem = $"{backend}-{(int)(scale * 100)}";
                    var frame = ExerciseEmoji(viewport, renderer, target, read, block, control, output, stem);
                    var file = stem + ".png";
                    var crop = stem + "-text-4x.png";
                    WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, frame.Pixels);
                    WriteEnlargedCrop(Path.Combine(output, crop), frame.Pixels, viewport.Width, DeviceRect(frame.Editor, scale), FractionalZoom);
                    passed &= frame.Reference.Passed;
                    captures.Add(new { Scale = scale, Backend = backend, viewport.Width, viewport.Height, File = file, Crop = crop,
                        frame.Editor, frame.Control, frame.LineSize, frame.Baseline, frame.ControlSize, frame.ControlBaseline,
                        frame.Forward, frame.Reverse, frame.CaretXs,
                        frame.CheckColorPixels, frame.JoinedColorPixels, frame.Reference });
                    return frame;
                }

                void Compare(string reference, EmojiFrame expected, string backend, EmojiFrame actual, int edgeRadius)
                {
                    var result = PixelDifference.Compare(expected.Pixels, actual.Pixels, viewport.Width, viewport.Height, 2, edgeRadius);
                    var sameLayout = expected.Editor == actual.Editor && expected.Control == actual.Control && expected.LineSize == actual.LineSize &&
                        expected.ControlSize == actual.ControlSize && expected.Baseline == actual.Baseline &&
                        expected.ControlBaseline == actual.ControlBaseline && expected.CaretXs.SequenceEqual(actual.CaretXs);
                    passed &= result.Passed && sameLayout;
                    comparisons.Add(new { Scale = scale, Reference = reference, Backend = backend, SameLayout = sameLayout, Result = result });
                }
            }

            File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
            {
                Completed = true, Passed = passed, Profile = "typography-emoji", PhysicalDpi = false, ValidationEnabled = validation,
                Text = EmojiSample, Control = EmojiControl, FontSize = EmojiFontSize, Faces = faces, Runs = runs,
                ArgentisVersion = typeof(TextBox).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),
                SkiaSharp = typeof(SKFont).Assembly.FullName, OS = Environment.OSVersion.VersionString, Runtime = Environment.Version.ToString(),
                Hardware = hardware, Captures = captures, Comparisons = comparisons, ValidationReports = validationReports
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Emoji: {captures.Count} frames and {comparisons.Count} backend comparisons. Report in {output}.");
            if (!passed) Environment.ExitCode = 1;
        }
        finally { block.Clear(); control.Clear(); }
    }

    private static EmojiFrame ExerciseEmoji(UiViewport viewport, IRenderBackend backend, UiRenderTarget target, Func<UiSession, byte[]> read,
        TextBlock reference, TextBlock controlReference, string output, string stem)
    {
        var theme = Theme.Light;
        var root = new Panel { Background = theme.Input };
        var stack = new VStack { Margin = new(20, 12, 0, 0), Spacing = 10, Width = 324,
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start };
        var editor = new TextBox { Value = EmojiSample, FontSize = EmojiFontSize, UseShapedText = true };
        var control = new TextBox { Value = EmojiControl, FontSize = EmojiFontSize, UseShapedText = true };
        stack.Add(editor); stack.Add(control); root.Add(stack);
        using var ui = UiSession.Create(root, backend, target, viewport);
        ui.SetTheme(theme);
        Require(ui.Update(), "Expected the mixed TextBox to render.");
        var pixels = read(ui);
        var service = (IRichTextLayoutService)ui.Rendering.TextLayout;
        var style = new RichTextStyle { FontFamily = editor.FontFamily, FontSize = editor.FontSize, FontWeight = editor.FontWeight, Foreground = editor.Foreground };
        using var line = service.CreateRichTextLayout(RichTextDocument.FromText(EmojiSample), style, null, RichTextAlignment.Left);
        using var controlLine = service.CreateRichTextLayout(RichTextDocument.FromText(EmojiControl), style, null, RichTextAlignment.Left);
        Require(line.Size == new Size(reference.MeasuredWidth, reference.MeasuredHeight) &&
            controlLine.Size == new Size(controlReference.MeasuredWidth, controlReference.MeasuredHeight), "Known-font reference metrics disagree with the backend layout.");
        Require(Math.Abs(editor.Bounds.Height - editor.Padding.Vertical - line.Size.Height) < 0.001f &&
            Math.Abs(control.Bounds.Height - control.Padding.Vertical - controlLine.Size.Height) < 0.001f, "TextBox height did not follow the shaped line.");

        var origin = editor.Bounds.Deflate(editor.Padding);
        var textRegion = DeviceRect(new Rect(origin.X, origin.Y, line.Size.Width, line.Size.Height), viewport.Scale);
        using var imageSurface = SKSurface.Create(new SKImageInfo(viewport.Width, viewport.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException("Could not allocate the emoji reference raster.");
        imageSurface.Canvas.Clear(ToSkColor(theme.Input));
        imageSurface.Canvas.Scale(viewport.Scale);
        reference.Paint(imageSurface.Canvas, new SKPoint(origin.X, origin.Y));
        using var image = imageSurface.Snapshot();
        var referencePixels = RasterProbe.ReadSnapshot(image);
        var referenceCompare = PixelDifference.Compare(referencePixels, pixels, viewport.Width, viewport.Height, 2, 1, region: textRegion);
        var checkIndex = EmojiSample.IndexOf('✅');
        var joinedIndex = EmojiSample.IndexOf("👩‍💻", StringComparison.Ordinal);
        var landmarks = new[] { checkIndex, checkIndex + 1, joinedIndex, EmojiSample.Length };
        var checkColor = CountColors(checkIndex, checkIndex + 1, pixels);
        var joinedColor = CountColors(joinedIndex, EmojiSample.Length, pixels);
        Require(checkColor > 20 && joinedColor > 20, $"Missing color emoji pixels ({checkColor}, {joinedColor}).");

        var stops = StringInfo.ParseCombiningCharacters(EmojiSample).Append(EmojiSample.Length).ToArray();
        var caretXs = stops.Select(index => line.GetCaretBounds(index).X).ToArray();
        Require(caretXs.Zip(caretXs.Skip(1)).All(pair => pair.First < pair.Second), "Mixed line must have increasing grapheme positions.");
        var forward = new List<int>();
        var reverse = new List<int>();
        ui.Focus(editor);
        Key(UiKey.Home);
        ui.Update();
        var atHome = read(ui);
        forward.Add(editor.CaretIndex);
        foreach (var stop in stops.Skip(1))
        {
            Key(UiKey.Right);
            forward.Add(editor.CaretIndex);
            Require(editor.CaretIndex == stop, $"Right jumped to UTF-16 index {editor.CaretIndex} instead of {stop}.");
            if (landmarks.Contains(stop))
            {
                ui.Update();
                var frame = read(ui);
                var x = origin.X + line.GetCaretBounds(stop).X;
                var caretRegion = DeviceRect(new Rect(x - 1, origin.Y, 3, line.Size.Height), viewport.Scale);
                var ink = ChangedInk(atHome, frame, viewport.Width, caretRegion);
                Require(ink.Width is >= 1 and <= 4 && ink.Height >= (int)(line.Size.Height * viewport.Scale) - 2,
                    $"Missing caret at shaped insertion index {stop}: {ink}.");
                WritePng(Path.Combine(output, $"{stem}-caret-{stop}.png"), viewport.Width, viewport.Height, frame);
            }
        }
        reverse.Add(editor.CaretIndex);
        foreach (var stop in stops.Reverse().Skip(1))
        {
            Key(UiKey.Left);
            reverse.Add(editor.CaretIndex);
            Require(editor.CaretIndex == stop, $"Left jumped to UTF-16 index {editor.CaretIndex} instead of {stop}.");
        }
        Require(editor.Value == EmojiSample && editor.SelectionLength == 0 && forward.SequenceEqual(stops) && reverse.SequenceEqual(stops.Reverse()),
            "Emoji caret navigation changed the text or selection.");

        editor.UseShapedText = false;
        Require(ui.Update(), "Switching back to the plain line must invalidate layout.");
        var legacy = read(ui);
        Require(CountColors(checkIndex, checkIndex + 1, legacy) == 0 && CountColors(joinedIndex, EmojiSample.Length, legacy) == 0,
            "The plain-layout control unexpectedly displayed the fallback emoji.");
        editor.UseShapedText = true;
        Require(ui.Update() && !ui.Update(), "Shaped layout must return to idle after being enabled again.");

        return new(pixels, editor.Bounds, control.Bounds, line.Size, reference.Lines[0].BaseLine, controlLine.Size, controlReference.Lines[0].BaseLine,
            forward.ToArray(), reverse.ToArray(), caretXs, checkColor, joinedColor, referenceCompare);

        void Key(UiKey key)
        {
            var result = ui.HandleInput(new UiInput(InputKind.KeyDown, Key: key));
            Require(result.Handled, $"TextBox did not handle {key}.");
        }

        int CountColors(int from, int to, byte[] frame)
        {
            var left = line.GetCaretBounds(from).X;
            var right = line.GetCaretBounds(to).X;
            var region = DeviceRect(new Rect(origin.X + left, origin.Y, right - left, line.Size.Height), viewport.Scale);
            var count = 0;
            for (var y = region.Top; y < region.Bottom; y++)
            for (var x = region.Left; x < region.Right; x++)
            {
                var offset = (y * viewport.Width + x) * 4;
                var red = frame[offset]; var green = frame[offset + 1]; var blue = frame[offset + 2];
                if (Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue)) >= 48) count++;
            }
            return count;
        }
    }

    private static TextBlock ShapeEmojiReference(string text)
    {
        var block = new TextBlock();
        var style = new Topten.RichTextKit.Style { FontFamily = Theme.Light.FontFamily, FontSize = EmojiFontSize, FontWeight = 400,
            TextColor = ToSkColor(Theme.Light.Foreground) };
        block.AddText(text, style);
        block.AddText("\u200B", style);
        block.Layout();
        Require(block.Lines.Count == 1, "Expected one reference line.");
        return block;
    }

    private static object FontProvenance(string path)
    {
        using var face = SKTypeface.FromFile(path) ?? throw new NotSupportedException($"The emoji profile requires {path}.");
        var table = face.GetTableData(0x68656164);
        Require(table is { Length: >= 8 }, $"Missing OpenType head table in {path}.");
        var revision = BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(4, 4)) / 65536d;
        return new { File = path, Family = face.FamilyName, Revision = revision, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    }
}
