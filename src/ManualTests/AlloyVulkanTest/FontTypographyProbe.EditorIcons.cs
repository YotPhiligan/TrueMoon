using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Menus;
using TrueMoon.Argentis.Styling;
using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.OpenGL;
using SkiaSharp;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const int IconsWidth = 592;
    private const int IconsHeight = 136;
    private static readonly string[] IconActions = ["Open", "Save", "Undo", "Redo", "Play", "Pause", "Stop"];
    private sealed record ToolbarFrames(Rect[] Buttons, SKRectI[] IconBounds, Dictionary<string, byte[]> States);
    private sealed record IconInk(SKRectI Bounds, double CenterX, double CenterY);

    internal static void RunEditorIcons(string output, bool validation)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh icon output directory.");
        Directory.CreateDirectory(output);

        var captures = new List<object>();
        var comparisons = new List<object>();
        var hardware = new List<object>();
        var validationReports = new List<object>();
        var passed = true;
        var pixelParityPassed = true;
        foreach (var (name, theme) in new[] { ("light", Theme.Light), ("dark", Theme.Dark) })
        foreach (var scale in new[] { 1f, 1.25f, 1.75f })
        {
            var viewport = new UiViewport((int)(IconsWidth * scale), (int)(IconsHeight * scale), scale);
            var raster = Capture("raster", new SkiaRasterRenderBackend(), new RasterUiTarget(),
                ui => { using var image = ui.SnapshotRasterImage(); return RasterProbe.ReadSnapshot(image); });
            ToolbarFrames openGl;
            using (var window = new SilkWindowHost(320, 240, "Editor icons OpenGL", true))
            using (var gl = GL.GetApi(window.NativeWindow))
            {
                hardware.Add(new { Scale = scale, Backend = "opengl", Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) });
                openGl = Capture("opengl", new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window),
                    ui => { using var image = ui.ReadbackOpenGLImage(); return RasterProbe.ReadSnapshot(image); });
                Require(gl.GetError() == GLEnum.NoError, "Editor icons generated an OpenGL error.");
            }
            var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
            ToolbarFrames vulkan;
            using (device)
            {
                hardware.Add(VulkanHardware(device));
                vulkan = Capture("vulkan", new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device),
                    ui => ReadVulkanText(ui, (info, returned) => VulkanHostReadback.Read(device, info, returned)));
            }
            validationReports.Add(new { Theme = name, Scale = scale, Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
            passed &= device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0;
            Compare("raster", raster, "opengl", openGl, 1);
            Compare("raster", raster, "vulkan", vulkan, 1);
            Compare("opengl", openGl, "vulkan", vulkan, 0);

            ToolbarFrames Capture(string backend, IRenderBackend renderer, UiRenderTarget target, Func<UiSession, byte[]> read)
            {
                var frame = ExerciseEditorIcons(viewport, theme, renderer, target, read);
                var first = frame.Buttons[0];
                var last = frame.Buttons[^1];
                var crop = DeviceRect(new Rect(first.X, first.Y, last.X + last.Width - first.X, first.Height), scale);
                foreach (var (state, pixels) in frame.States)
                {
                    var stem = $"{backend}-{name}-{(int)(scale * 100)}-{state}";
                    var image = stem + ".png";
                    var enlarged = stem + "-icons-4x.png";
                    WritePng(Path.Combine(output, image), viewport.Width, viewport.Height, pixels);
                    WriteEnlargedCrop(Path.Combine(output, enlarged), pixels, viewport.Width, crop, FractionalZoom);
                    captures.Add(new { Theme = name, Scale = scale, Backend = backend, State = state, viewport.Width, viewport.Height,
                        File = image, Crop = enlarged, CropBounds = PixelBounds(crop), Buttons = frame.Buttons,
                        Icons = frame.IconBounds.Select(PixelBounds).ToArray() });
                }
                return frame;
            }

            void Compare(string reference, ToolbarFrames first, string backend, ToolbarFrames second, int edgeRadius)
            {
                var layoutMatches = first.Buttons.SequenceEqual(second.Buttons) && first.IconBounds.SequenceEqual(second.IconBounds);
                passed &= layoutMatches;
                foreach (var (state, pixels) in first.States)
                {
                    var actual = second.States[state];
                    var regions = first.IconBounds.Select((region, index) =>
                    {
                        var expectedInk = MeasureIconInk(pixels, viewport.Width, region);
                        var actualInk = MeasureIconInk(actual, viewport.Width, region);
                        var xShift = Math.Abs(expectedInk.CenterX - actualInk.CenterX);
                        var yShift = Math.Abs(expectedInk.CenterY - actualInk.CenterY);
                        var expectedBounds = expectedInk.Bounds;
                        var actualBounds = actualInk.Bounds;
                        var edgeShift = Math.Max(Math.Max(Math.Abs(expectedBounds.Left - actualBounds.Left), Math.Abs(expectedBounds.Right - actualBounds.Right)),
                            Math.Max(Math.Abs(expectedBounds.Top - actualBounds.Top), Math.Abs(expectedBounds.Bottom - actualBounds.Bottom)));
                        var geometryPassed = xShift <= 0.5 && yShift <= 0.5 && edgeShift <= 1;
                        return new { Action = IconActions[index], GeometryPassed = geometryPassed, CenterDeltaX = xShift, CenterDeltaY = yShift,
                            EdgeDelta = edgeShift, Result = PixelDifference.Compare(pixels, actual, viewport.Width, viewport.Height, 2, edgeRadius, region: region) };
                    }).ToArray();
                    passed &= regions.All(region => region.GeometryPassed);
                    var pixelMatch = regions.All(region => region.Result.Passed);
                    pixelParityPassed &= pixelMatch;
                    string? diff = null;
                    if (!pixelMatch)
                    {
                        diff = $"diff-{reference}-{backend}-{name}-{(int)(scale * 100)}-{state}-4x.png";
                        var firstIcon = first.IconBounds[0];
                        var lastIcon = first.IconBounds[^1];
                        var crop = new SKRectI(firstIcon.Left, firstIcon.Top, lastIcon.Right, lastIcon.Bottom);
                        WriteEnlargedCrop(Path.Combine(output, diff), PixelDifference.Diff(pixels, actual), viewport.Width, crop, FractionalZoom);
                    }
                    comparisons.Add(new { Theme = name, Scale = scale, Reference = reference, Backend = backend, State = state,
                        LayoutMatches = layoutMatches, PixelParityPassed = pixelMatch, Diff = diff, Icons = regions });
                }
            }
        }

        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, PixelParityPassed = pixelParityPassed, ValidationEnabled = validation, PhysicalDpi = false, IconSize = 16,
            GeometryTolerance = new { Center = 0.5, BoundsEdge = 1 }, PixelTolerance = new { Flat = 2, CpuGpuEdgeRadius = 1, CpuGpuEdge = 24, GpuGpuEdgeRadius = 0 },
            EditorVersion = typeof(EditorHeaderView).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),
            KeyboardActions = IconActions, NarrowWidth = 320, Fixture = "EditorHeaderView with controlled presentation models and routed input",
            Hardware = hardware, Captures = captures, Comparisons = comparisons, ValidationReports = validationReports
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Editor icons: {captures.Count} frames, {comparisons.Count} paired comparisons. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;
    }

    private static ToolbarFrames ExerciseEditorIcons(UiViewport viewport, Theme theme, IRenderBackend backend, UiRenderTarget target,
        Func<UiSession, byte[]> read)
    {
        var stack = new VStack { Margin = new(8), Spacing = 4 };
        var root = new OverlayHost(stack) { Background = theme.Surface };
        var ports = new ToolbarProbePorts();
        var model = ports.Create<IEditorViewModel>();
        var documents = ports.Create<IEditorDocumentViewModel>();
        var history = ports.Create<IEditorInspectorViewModel>();
        var args = new EditorHeaderArguments(root, "Aether Editor", () => { });
        var header = new EditorHeaderView(model, documents, history, new EditorHeaderViewStyle(), args) { Height = 36 };
        var legacy = new EditorHeaderView(model, documents, history, new EditorHeaderViewStyle(), args) { Height = 36 };
        stack.Add(new Text("Vector contours") { FontSize = 12, Height = 16 });
        stack.Add(header);
        stack.Add(new Text("Previous geometry · same size") { FontSize = 12, Height = 16 });
        stack.Add(legacy);
        using var ui = UiSession.Create(root, backend, target, viewport, null, new UiDispatchOptions(), configurePresenters: EditorPresenters.Register);
        var buttons = Actions(header);
        var oldButtons = Actions(legacy);
        foreach (var button in oldButtons) button.UseVectorSymbol = false;
        ui.SetTheme(theme);
        Update();
        var normal = read(ui);
        var bounds = buttons.Select(button => button.Bounds).ToArray();
        var regions = buttons.Select(IconRegion).ToArray();
        var states = new Dictionary<string, byte[]> { ["normal"] = normal };
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            Require(button.IconSize == 16 && button.UseVectorSymbol && button.IsEnabled && !string.IsNullOrWhiteSpace(button.AccessibleName) &&
                button.Bounds is { Width: 32, Height: 32 }, $"Unexpected toolbar configuration for {IconActions[i]}.");
            // The enclosing pixel rectangle spans an extra row or column when the logical origin lands between device pixels.
            var extent = 16 * viewport.Scale;
            Require(regions[i].Width >= extent && regions[i].Width <= extent + 1 && regions[i].Height >= extent && regions[i].Height <= extent + 1,
                "Icon footprint disagrees with the selected scale.");
            Require(HasInk(normal, regions[i]) && DifferentGeometry(normal, regions[i], IconRegion(oldButtons[i])), $"Missing contour for {IconActions[i]}.");
            ui.Focus(button);
            ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Enter));
            Require(button.IsFocused && button.IsPressed, $"Keyboard did not press {IconActions[i]}.");
            Update();
            var pressed = read(ui);
            Require(!normal.AsSpan(Index(regions[i])).Slice(0, 4).SequenceEqual(pressed.AsSpan(Index(regions[i]), 4)), "Pressed color did not change.");
            if (button == header.RunButton) states.Add("pressed", pressed);
            ui.HandleInput(new UiInput(InputKind.KeyUp, Key: UiKey.Enter));
            Require(!button.IsPressed, $"Keyboard did not release {IconActions[i]}.");
        }
        Require(ports.Calls.SequenceEqual(IconActions), "Keyboard activation did not dispatch all seven toolbar commands exactly once.");
        ports.SetEnabled(false);
        Update();
        var disabled = read(ui);
        Require(buttons.All(button => !button.IsEnabled && !button.IsFocused) && regions.All(region => HasInk(disabled, region)), "Disabled state lost icons or kept focus.");
        states.Add("disabled", disabled);

        ports.SetEnabled(true);
        ui.Resize(viewport with { Width = (int)(320 * viewport.Scale) });
        Update();
        Require(buttons.Take(4).All(button => button.Parent?.IsVisible == false) && buttons.Skip(4).All(button => button.Bounds.Width > 0) &&
            header.DocumentCommands[DocumentCommand.Open].CanExecute && header.DocumentCommands[DocumentCommand.Save].CanExecute,
            "Compact layout lost the menu commands or kept the document buttons visible.");
        ui.Resize(viewport);
        Update();
        Require(buttons.Select(button => button.Bounds).SequenceEqual(bounds) && buttons.All(button => button.IsEnabled), "Restoring the header width changed button geometry or admission.");
        return new(bounds, regions, states);

        void Update()
        {
            ui.Update();
            for (var pass = 0; pass < 3 && ui.NeedsUpdate; pass++) ui.Update();
        }

        SKRectI IconRegion(IconButton button) => DeviceRect(new Rect(button.Bounds.X + 8, button.Bounds.Y + 8, 16, 16), viewport.Scale);
        int Index(SKRectI region) => (region.Top * viewport.Width + region.Left) * 4;
        bool HasInk(byte[] pixels, SKRectI region) => DifferentGeometry(pixels, region, region, true);
        bool DifferentGeometry(byte[] pixels, SKRectI first, SKRectI second, bool flatReference = false)
        {
            var count = 0;
            for (var y = 0; y < first.Height; y++)
            for (var x = 0; x < first.Width; x++)
            {
                var a = Index(first) + (y * viewport.Width + x) * 4;
                var b = Index(second) + (flatReference ? 0 : (y * viewport.Width + x) * 4);
                if (!pixels.AsSpan(a, 4).SequenceEqual(pixels.AsSpan(b, 4))) count++;
            }
            return count > 8;
        }
    }

    private static IconButton[] Actions(EditorHeaderView header) =>
        [header.OpenButton, header.SaveButton, header.UndoButton, header.RedoButton, header.RunButton, header.PauseButton, header.StopButton];

    private static IconInk MeasureIconInk(byte[] pixels, int width, SKRectI region)
    {
        var background = (region.Top * width + region.Left) * 4;
        var left = region.Right; var right = region.Left;
        var top = region.Bottom; var bottom = region.Top;
        var weight = 0L; var weightedX = 0L; var weightedY = 0L;
        for (var y = region.Top; y < region.Bottom; y++)
        for (var x = region.Left; x < region.Right; x++)
        {
            var offset = (y * width + x) * 4;
            var contrast = Math.Max(Math.Abs(pixels[offset] - pixels[background]),
                Math.Max(Math.Abs(pixels[offset + 1] - pixels[background + 1]), Math.Abs(pixels[offset + 2] - pixels[background + 2])));
            if (contrast <= 2) continue;
            weight += contrast;
            weightedX += contrast * x; weightedY += contrast * y;
            left = Math.Min(left, x); right = Math.Max(right, x + 1);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y + 1);
        }
        Require(weight > 0, "Expected visible icon ink.");
        return new(new(left, top, right, bottom), (double)weightedX / weight + 0.5, (double)weightedY / weight + 0.5);
    }
}

/// <summary>Supplies controlled admission and command observations to the actual Editor header.</summary>
public class ToolbarProbePorts : DispatchProxy
{
    private ToolbarProbePorts _owner = null!;
    private bool _enabled = true;
    private PropertyChangedEventHandler? _changed;
    internal List<string> Calls { get; } = [];

    internal T Create<T>() where T : class
    {
        var proxy = Create<T, ToolbarProbePorts>();
        ((ToolbarProbePorts)(object)proxy)._owner = this;
        return proxy;
    }

    internal void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _changed?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        switch (targetMethod.Name)
        {
            case "add_PropertyChanged": _owner._changed += (PropertyChangedEventHandler)args![0]!; return null;
            case "remove_PropertyChanged": _owner._changed -= (PropertyChangedEventHandler)args![0]!; return null;
            case "GetWindowCaption":
            case "GetWindowCaptionDescription": return args![0];
            case "RunSimulation": _owner.Calls.Add("Play"); return null;
            case "Execute":
            case "Request": _owner.Calls.Add(args![0]!.ToString()!); return null;
            case "Undo":
            case "Redo": _owner.Calls.Add(targetMethod.Name); return null;
            case "get_CanRun":
            case "get_CanPause":
            case "get_CanStop":
            case "get_CanOpen":
            case "get_CanSave":
            case "get_CanUndo":
            case "get_CanRedo": return _owner._enabled;
            default:
                if (targetMethod.IsSpecialName && targetMethod.ReturnType == typeof(bool)) return false;
                throw new NotSupportedException($"Unexpected header model operation {targetMethod.Name}.");
        }
    }
}
