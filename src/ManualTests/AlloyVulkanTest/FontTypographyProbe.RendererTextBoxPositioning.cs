using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
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
    private readonly record struct RendererScrollCase(string Sample, bool Shaped, bool? Subpixel, ScrollStep Step);
    private readonly record struct ScrollGeometry(Rect Field, Rect Line, Rect Caret, float TextX, float GlyphLeft, float GlyphRight);
    private sealed record RendererScrollFrame(byte[] Pixels, ScrollGeometry Geometry, double GlyphCenter, int HitIndex,
        int SelectionStart, int SelectionLength, Rect HitCaretInk, Rect SelectionInk);

    private enum FieldFixture { PlainAndShaped, TextBox, NumberBox }

    internal static void RunRendererTextBoxPositioning(string output, bool validation) =>
        RunRendererScrollMatrix(output, validation, FieldFixture.PlainAndShaped, Theme.Dark);

    internal static void RunEditableFieldPositioning(string output, bool validation)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh editable field output directory.");
        foreach (var theme in new[] { Theme.Light, Theme.Dark })
        foreach (var fixture in new[] { FieldFixture.TextBox, FieldFixture.NumberBox })
            RunRendererScrollMatrix(Path.Combine(output, theme == Theme.Light ? "Light" : "Dark", fixture.ToString()), validation, fixture, theme);
    }

    private static void RunRendererScrollMatrix(string output, bool validation, FieldFixture fixture, Theme theme)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        using var face = MatchDefaultFace(400);
        using var font = new SKFont(face, ScrollFontSize);
        var plainDefault = font.Subpixel;
        var shapedDefault = new TextPaintOptions().SubpixelPositioning;
        bool editable = fixture != FieldFixture.PlainAndShaped;
        ScrollSample[] samples = editable ? [new("draft", "123 456 789 012 345 678 901 234 567  7  ")] : DarkScrollSamples;
        bool[] shapingModes = editable ? [false] : [false, true];
        float[] scales = editable ? [1, 1.25f, 1.75f] : [1.25f, 1.75f];
        (bool? Setting, bool LabelControl)[] modes = [(null, false), (false, false), (true, false)];
        if (editable) modes = [.. modes, (!plainDefault, true)];
        const string labelText = "Inspector · 7 Wg 012";
        var labelBounds = new Rect(16.375f, 60, 204, 24);
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var captures = new List<object>();
        var backendComparisons = new List<object>();
        var directionComparisons = new List<object>();
        var modeComparisons = new List<object>();
        var labelControls = new List<object>();
        var passed = true;

        foreach (var scale in scales)
        {
            var viewport = new UiViewport((int)(ScrollWidth * scale), (int)(ScrollHeight * scale), scale);
            var labelRegion = DeviceRect(labelBounds, scale);
            var raster = Capture("raster", mode => new SkiaRasterRenderBackend { EditorSubpixelPositioning = mode }, new RasterUiTarget(),
                ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
            Dictionary<RendererScrollCase, RendererScrollFrame> openGl;
            using (var window = new SilkWindowHost(320, 240, "TextBox positioning OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                hardware.Add(new { Scale = scale, Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                openGl = Capture("opengl", mode => new SkiaOpenGLRenderBackend { EditorSubpixelPositioning = mode }, OpenGLProbe.Target(window),
                    ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); });
                Require(gl.GetError() == GLEnum.NoError, "TextBox positioning generated an OpenGL error.");
            }
            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            Dictionary<RendererScrollCase, RendererScrollFrame> vulkan;
            using (device)
            {
                hardware.Add(VulkanHardware(device));
                vulkan = Capture("vulkan", mode => new SkiaVulkanRenderBackend { EditorSubpixelPositioning = mode }, SilkVulkanHost.CreateTarget(device),
                    ui => ReadVulkanText(ui, (info, returned) => VulkanHostReadback.Read(device, info, returned)));
            }
            validationReports.Add(new { Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
            passed &= device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0;

            CompareBackends("raster", raster, "opengl", openGl, 1);
            CompareBackends("raster", raster, "vulkan", vulkan, 1);
            CompareBackends("opengl", openGl, "vulkan", vulkan, 0);

            Dictionary<RendererScrollCase, RendererScrollFrame> Capture(string backend, Func<bool?, IRenderBackend> createBackend,
                UiRenderTarget target, Func<UiSession, byte[]> read)
            {
                var frames = new Dictionary<RendererScrollCase, RendererScrollFrame>();
                foreach (var sample in samples)
                foreach (var shaped in shapingModes)
                foreach (var (subpixel, labelControl) in modes)
                {
                    var (root, editor) = fixture == FieldFixture.NumberBox ? CreateScrollNumberBox(sample, theme) : CreateScrollTextBox(sample, theme);
                    editor.UseShapedText = shaped;
                    var renderer = createBackend(editable && !labelControl ? subpixel : null);
                    using var ui = UiSession.Create(root, renderer, target, viewport);
                    var layout = (SkiaDrawingContext)ui.Rendering.TextLayout;
                    if ((!editable || labelControl) && subpixel is bool enabled)
                        layout.SetSubpixelPositioningForProbe(enabled);
                    ui.SetTheme(theme);
                    Text? label = null;
                    if (editable)
                    {
                        label = new Text(labelText)
                        {
                            FontFamily = editor.FontFamily, FontSize = editor.FontSize, FontWeight = editor.FontWeight, FontItalic = editor.FontItalic,
                            Margin = new(labelBounds.X, labelBounds.Y, 0, 0), Width = labelBounds.Width, Height = labelBounds.Height,
                            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start
                        };
                        root.Add(label);
                    }
                    Require(ui.Update(), "Expected the TextBox fixture to render.");
                    if (label != null) Require(label.Bounds == labelBounds, "The label must retain a fixed fractional origin.");
                    if (labelControl)
                    {
                        // Deliberately change the whole surface in a separate session: the stock label must notice.
                        // A blank label in the same bounds also proves the stock crop contains real text ink.
                        var baseline = frames[new(sample.Name, shaped, null, BidirectionalScrollSteps[0])].Pixels;
                        var global = read(ui);
                        label!.Value = "";
                        Require(ui.Update(), "Expected a blank label control frame.");
                        var blank = read(ui);
                        var ink = PixelDifference.Compare(blank, baseline, viewport.Width, viewport.Height, 0, region: labelRegion);
                        var difference = PixelDifference.Compare(baseline, global, viewport.Width, viewport.Height, 0, region: labelRegion);
                        var crop = $"label-global-{PositioningMode(subpixel)}-control-{backend}-{(int)(scale * 100)}-4x.png";
                        WriteEnlargedCrop(Path.Combine(output, crop), global, viewport.Width, labelRegion, FractionalZoom);
                        labelControls.Add(new { Scale = scale, Backend = backend, Label = labelText, Bounds = labelBounds,
                            RegionPx = PixelBounds(labelRegion), StockInkPixels = ink.DifferentPixels, GlobalProbeMode = PositioningMode(subpixel),
                            ChangedLabelPixels = difference.DifferentPixels, Crop = crop });
                        passed &= ink.DifferentPixels > 0 && difference.DifferentPixels > 0;
                        continue;
                    }
                    ui.Focus(editor);
                    MoveToEnd(ui, editor);

                    var glyphEnd = sample.Text.TrimEnd().Length;
                    var glyphStart = glyphEnd - 1;
                    using var line = shaped ? layout.CreateRichTextLayout(RichTextDocument.FromText(sample.Text),
                        new RichTextStyle { FontFamily = editor.FontFamily, FontSize = ScrollFontSize, Foreground = editor.Foreground },
                        null, RichTextAlignment.Left) : null;
                    var lineHeight = line?.Size.Height ?? MeasureEditor("Mg").Height;
                    var prefix = Advance(glyphStart);
                    var glyphAdvance = Advance(glyphEnd);
                    var endAdvance = Advance(sample.Text.Length);

                    foreach (var step in BidirectionalScrollSteps)
                    {
                        var key = new RendererScrollCase(sample.Name, shaped, subpixel, step);
                        var context = $"{backend}/{sample.Name}/{(shaped ? "shaped" : "plain")}/{PositioningMode(subpixel)}/{scale}/{step.Direction}/{step.Phase}";
                        RenderScrollStep(ui, editor, scale, step, fixture == FieldFixture.NumberBox ? editor.Parent : null);
                        var field = editor.Bounds;
                        var content = field.Deflate(editor.Padding);
                        Require(content.Height >= lineHeight && endAdvance > content.Width - 2, $"Expected to scroll at {context}.");
                        var caret = new Rect(content.X + content.Width - 2, content.Y + (content.Height - lineHeight) / 2, 1, lineHeight);
                        var textX = caret.X - endAdvance;
                        var glyphLeft = textX + prefix;
                        if (editable)
                            Require(Math.Abs(content.X - editor.HorizontalOffset - textX) < 0.001f,
                                $"Prepared scroll offset disagrees with the visible line at {context}.");
                        var glyphRight = textX + glyphAdvance;
                        var lineRect = new Rect(content.X, caret.Y, content.Width, lineHeight);
                        var geometry = new ScrollGeometry(field, lineRect, caret, textX, glyphLeft, glyphRight);

                        var pixels = read(ui);
                        var padding = DeviceRect(new Rect(field.X + 4, caret.Y, 2, caret.Height), scale);
                        RequirePadding(pixels, viewport.Width, padding, theme.Input);
                        var glyphCenter = FinalGlyphCenter(pixels, viewport.Width, scale, glyphLeft, caret, theme.Input, isolated: true);
                        var frame = CheckRendererCaretAndSelection(ui, editor, read, viewport, geometry, pixels, glyphCenter, glyphStart, context);
                        frames.Add(key, frame);
                        var firstPhase = step.Returning ? 3 : 0;
                        var start = frames[key with { Step = new(firstPhase, step.Returning) }];
                        var motion = (textX - start.Geometry.TextX) * scale;
                        Require(Math.Abs(motion + (step.Phase - firstPhase) * 0.25f) < 0.001f, $"Wrong scroll displacement at {context}.");

                        var stem = $"{sample.Name}-{step.Direction}-{backend}-{(int)(scale * 100)}-" +
                            $"{(shaped ? "shaped" : "plain")}-{PositioningMode(subpixel)}-x{step.Phase * 25}";
                        var file = stem + ".png";
                        var crop = stem + "-field-4x.png";
                        WritePng(Path.Combine(output, file), viewport.Width, viewport.Height, pixels);
                        WriteEnlargedCrop(Path.Combine(output, crop), pixels, viewport.Width, DeviceRect(field, scale), FractionalZoom);
                        captures.Add(new { Scale = scale, Backend = backend, Sample = sample.Name, Shaped = shaped, Mode = PositioningMode(subpixel),
                            step.Direction, step.PhasePx, ScrollMotionPx = motion, GlyphCenterPx = glyphCenter, GlyphMotionPx = glyphCenter - start.GlyphCenter,
                            frame.Geometry, frame.HitIndex, frame.SelectionStart, frame.SelectionLength, frame.HitCaretInk, frame.SelectionInk,
                            File = file, Crop = crop, viewport.Width, viewport.Height });
                    }

                    Size MeasureEditor(string value) => ui.Rendering.TextLayout.MeasureEditorText(value, editor.FontSize, editor.FontFamily,
                        editor.FontWeight, editor.FontItalic);
                    float Advance(int index) => line?.GetCaretBounds(index).X ?? MeasureEditor(sample.Text[..index]).Width;
                }

                bool sawPositioningDifference = false;
                foreach (var (key, frame) in frames)
                {
                    if (key.Step.Returning)
                    {
                        var outgoing = frames[key with { Step = new(key.Step.Phase, false) }];
                        var sameLayout = SameRendererCoordinates(outgoing, frame);
                        var pixels = PixelDifference.Compare(outgoing.Pixels, frame.Pixels, viewport.Width, viewport.Height, 0);
                        passed &= sameLayout && pixels.Passed;
                        directionComparisons.Add(new { Scale = scale, Backend = backend, key.Sample, key.Shaped, Mode = PositioningMode(key.Subpixel),
                            key.Step.PhasePx, SameLayout = sameLayout, Result = pixels });
                    }
                    if (key.Subpixel is bool enabled)
                    {
                        var baseline = frames[key with { Subpixel = null }];
                        var sameLayout = SameRendererCoordinates(baseline, frame);
                        var pixels = PixelDifference.Compare(baseline.Pixels, frame.Pixels, viewport.Width, viewport.Height, 0);
                        var expectsExact = enabled == (key.Shaped ? shapedDefault : plainDefault);
                        passed &= sameLayout && (!expectsExact || pixels.Passed);
                        var labelPixels = editable ? PixelDifference.Compare(baseline.Pixels, frame.Pixels, viewport.Width, viewport.Height, 0,
                            region: labelRegion) : null;
                        passed &= labelPixels == null || labelPixels.Passed;
                        if (editable && !expectsExact && !sawPositioningDifference)
                            sawPositioningDifference = !PixelDifference.Compare(baseline.Pixels, frame.Pixels, viewport.Width, viewport.Height, 0,
                                region: DeviceRect(frame.Geometry.Line, scale)).Passed;
                        modeComparisons.Add(new { Scale = scale, Backend = backend, key.Sample, key.Shaped, Mode = PositioningMode(key.Subpixel),
                            key.Step.Direction, key.Step.PhasePx, SameLayout = sameLayout, ExpectsExact = expectsExact, Result = pixels, Label = labelPixels });
                    }
                }
                if (editable) Require(sawPositioningDifference,
                    $"Plain positioning never changed the readback for {fixture} on {backend} at {scale}.");
                return frames;
            }

            void CompareBackends(string reference, Dictionary<RendererScrollCase, RendererScrollFrame> expected,
                string backend, Dictionary<RendererScrollCase, RendererScrollFrame> actual, int edgeRadius)
            {
                foreach (var (key, first) in expected)
                {
                    var second = actual[key];
                    var sameLayout = SameRendererCoordinates(first, second);
                    var pixels = PixelDifference.Compare(first.Pixels, second.Pixels, viewport.Width, viewport.Height, 2, edgeRadius,
                        region: DeviceRect(first.Geometry.Line, scale));
                    passed &= sameLayout && pixels.Passed;
                    backendComparisons.Add(new { Scale = scale, Reference = reference, Backend = backend, key.Sample, key.Shaped,
                        Mode = PositioningMode(key.Subpixel), key.Step.Direction, key.Step.PhasePx, SameLayout = sameLayout, Result = pixels });
                }
            }
        }

        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, ValidationEnabled = validation, PhysicalDpi = false, Theme = theme == Theme.Light ? "Light" : "Dark",
            Fixture = fixture.ToString(), FontFamily = face.FamilyName, FontSize = ScrollFontSize, Samples = samples,
            RendererVersion = typeof(SkiaRasterRenderBackend).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Runtime = Environment.Version.ToString(), SkiaSharp = typeof(SKFont).Assembly.FullName,
            DefaultSubpixel = new { Plain = plainDefault, Shaped = shapedDefault },
            PositioningScope = editable ? "Backend.EditorSubpixelPositioning (label sensitivity control is surface-wide)" : "Surface-wide plain and shaped probe",
            LabelControls = labelControls,
            NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),
            Hardware = hardware, ValidationReports = validationReports, Captures = captures,
            BackendComparisons = backendComparisons, DirectionComparisons = directionComparisons, ModeComparisons = modeComparisons
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Renderer TextBox positioning: {captures.Count} frames, {backendComparisons.Count} backend, " +
            $"{directionComparisons.Count} direction and {modeComparisons.Count} mode comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static RendererScrollFrame CheckRendererCaretAndSelection(UiSession ui, TextBox editor, Func<UiSession, byte[]> read,
        UiViewport viewport, ScrollGeometry geometry, byte[] pixels, double glyphCenter, int glyphStart, string context)
    {
        var x = geometry.GlyphLeft;
        var y = geometry.Caret.Y + geometry.Caret.Height / 2;
        ui.HandleInput(new UiInput(InputKind.PointerDown, x, y));
        ui.HandleInput(new UiInput(InputKind.PointerUp, x, y));
        var hit = editor.CaretIndex;
        Require(hit == glyphStart && editor.SelectionLength == 0 && ui.Update(), $"Click missed the glyph boundary at {context}.");
        var clicked = read(ui);
        var caretArea = geometry.Caret with { X = x - 1, Width = 3 };
        var caretInk = ChangedInk(pixels, clicked, viewport.Width, DeviceRect(caretArea, viewport.Scale));
        var endCaretInk = ChangedInk(pixels, clicked, viewport.Width, DeviceRect(geometry.Caret, viewport.Scale));
        var minimumInkHeight = (int)(geometry.Caret.Height * viewport.Scale) - 2;
        Require(caretInk.Width > 0 && caretInk.Height >= minimumInkHeight && endCaretInk.Height >= minimumInkHeight,
            $"Readback lacks a caret move at {context}.");

        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
        Require(editor.CaretIndex == glyphStart + 1, $"Right missed the boundary after the glyph at {context}.");
        ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Left, Shift: true));
        var selection = editor.SelectionStart;
        var length = editor.SelectionLength;
        Require(selection == hit && length == 1 && ui.Update(), $"Expected selection of the last visible glyph at {context}.");
        var glyphArea = caretArea with { X = x + 1, Width = geometry.GlyphRight - x - 2 };
        var selectionInk = ChangedInk(clicked, read(ui), viewport.Width, DeviceRect(glyphArea, viewport.Scale));
        Require(selectionInk.Width > 0 && selectionInk.Height >= minimumInkHeight - 2, $"Readback lacks a painted selection at {context}.");

        MoveToEnd(ui, editor);
        Require(ui.Update() && pixels.AsSpan().SequenceEqual(read(ui)), $"End did not restore the frame at {context}.");
        return new(pixels, geometry, glyphCenter, hit, selection, length,
            new(caretInk.Left, caretInk.Top, caretInk.Width, caretInk.Height),
            new(selectionInk.Left, selectionInk.Top, selectionInk.Width, selectionInk.Height));
    }

    private static string PositioningMode(bool? subpixel) => subpixel switch { true => "on", false => "off", _ => "default" };

    private static bool SameRendererCoordinates(RendererScrollFrame first, RendererScrollFrame second) =>
        first.Geometry == second.Geometry && first.HitIndex == second.HitIndex &&
        first.SelectionStart == second.SelectionStart && first.SelectionLength == second.SelectionLength;
}
