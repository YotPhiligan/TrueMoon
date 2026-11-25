using TrueMoon.Argentis.Editors;
using TrueMoon.Argentis.Input;
using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Styling;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Silk.NET.OpenGL;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Editing;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace AlloyVulkanTest;

internal static partial class FontTypographyProbe
{
    private const int PerformanceColumns = 3;
    private const int PerformanceRows = 4;
    private const int PerformanceLogicalWidth = 568;
    private const int PerformanceLogicalHeight = 208;

    private readonly record struct FontCacheUsage(long Bytes, int Entries);
    private readonly record struct GpuCacheUsage(int Resources, long Bytes);
    private readonly record struct TextBoxPerformanceFrame(int Phase, PerformanceMeasurement Update, PerformanceMeasurement Render,
        PerformanceMeasurement Completion, PerformanceMeasurement Return);

    internal static void RunTextBoxPerformance(string output, bool validation, bool subpixelOnFirst, bool inspector)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new ArgumentException("Use a fresh font output directory.");
        Directory.CreateDirectory(output);
        using var inspectorApp = inspector ? BuildInspectorPerformanceApp(output) : null;
        var inspectorFactory = inspectorApp?.Services.Resolve<IFactory<EditorInspectorView>>();
        var inspectorProject = inspectorApp?.Services.Resolve<EditorProjects>()?.Active;
        var inspectorDocument = inspectorProject?.Documents;
        var sceneBefore = inspectorDocument?.Read().Content;
        Require(!inspector || inspectorFactory != null && sceneBefore != null, "The inspector fixture is missing its factory or open document.");
        var width = inspector ? InspectorPerformanceWidth : PerformanceLogicalWidth;
        var height = inspector ? InspectorPerformanceHeight : PerformanceLogicalHeight;
        var warmup = validation ? 16 : 192;
        var minimumWarmupMilliseconds = validation ? 0 : 200;
        var samples = validation ? 32 : 512;
        bool[] order = [subpixelOnFirst, !subpixelOnFirst, !subpixelOnFirst, subpixelOnFirst];
        var cases = new List<object>();
        var hardware = new Dictionary<string, object>();
        var validationReports = new List<object>();
        var calibration = PerformanceReport.Summarize(PerformanceClock.Calibrate(samples));

        Exercise("raster", new SkiaRasterRenderBackend(), new RasterUiTarget(), _ => default,
            surface => { using var image = ((SkiaRasterSurface)surface).Snapshot(); return RasterProbe.ReadSnapshot(image); });
        using (var window = new SilkWindowHost(320, 240, "TextBox performance OpenGL", true))
        using (var gl = GL.GetApi(window.NativeWindow))
        {
            hardware["OpenGL"] = new { Renderer = gl.GetStringS(StringName.Renderer), Version = gl.GetStringS(StringName.Version) };
            Exercise("opengl", new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window),
                _ => new HostSample(PerformanceClock.Measure(gl.Finish), default, default, default),
                surface => { using var image = ((SkiaOpenGLSurface)surface).ReadbackImage(); return RasterProbe.ReadSnapshot(image); });
            Require(gl.GetError() == GLEnum.NoError, "TextBox performance generated an OpenGL error.");
        }
        var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        using (device)
        {
            hardware["Vulkan"] = VulkanHardware(device);
            Exercise("vulkan", new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device),
                surface => SettingsPerformanceProbe.Handoff((SkiaVulkanSurface)surface, null, null, 0),
                surface =>
                {
                    var lease = ((SkiaVulkanSurface)surface).AcquireTexture();
                    var layout = (Silk.NET.Vulkan.ImageLayout)lease.Info.Layout;
                    try { return VulkanHostReadback.Read(device, lease.Info, state => layout = state); }
                    finally { lease.Return(layout); }
                });
        }
        validationReports.Add(new { Backend = "vulkan", Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount });
        var passed = device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0;
        Require(!inspector || ReferenceEquals(sceneBefore!.Document, inspectorDocument!.Read().Content!.Document) &&
            !inspectorApp!.Services.Resolve<EditorInspectorViewModel>()!.HasDraft, "The inspector workload changed the document or property draft.");
        var assembly = Assembly.GetExecutingAssembly();
        var systemFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "SegUIVar.ttf");
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Passed = passed, ValidationEnabled = validation, PhysicalDpi = false, CreatedUtc = DateTime.UtcNow,
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            Runtime = Environment.Version.ToString(), OS = Environment.OSVersion.VersionString, SkiaSharp = typeof(SKFont).Assembly.FullName,
            Fixture = inspector ? "EditorInspectorView" : "TextBox grid", Theme = "Dark", FontFamily = Theme.Dark.FontFamily,
            FontSize = inspector ? (float?)null : ScrollFontSize, Columns = inspector ? (int?)null : PerformanceColumns,
            Rows = inspector ? (int?)null : PerformanceRows, LogicalWidth = width, LogicalHeight = height, Text = inspector ? null : DarkScrollSamples,
            Inspector = inspector ? new { SceneRevision = sceneBefore!.Document.Revision, UndoCount = sceneBefore.UndoCount,
                Field = "Transform.x", ProjectSdk = inspectorProject!.Toolchain.SdkVersion, ShapedSubpixelOverridden = false } : null,
            MinimumWarmupFrames = warmup, MinimumWarmupMilliseconds = minimumWarmupMilliseconds, SamplesPerCase = samples,
            Order = order, TimerFrequency = Stopwatch.Frequency, Calibration = calibration,
            FontFile = File.Exists(systemFont) ? FontProvenance(systemFont) : null,
            FontCacheLimits = new FontCacheUsage(SKGraphics.GetFontCacheLimit(), SKGraphics.GetFontCacheCountLimit()),
            NativeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SkiaNativeLibrary.LoadedPath!))),
            Hardware = hardware, ValidationReports = validationReports, Cases = cases
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(inspector ? "Editor inspector" : "TextBox")} performance: {cases.Count} batches, {cases.Count * samples} measured frames. Report in {output}.");
        if (!passed) Environment.ExitCode = 1;

        void Exercise(string backend, IRenderBackend renderer, UiRenderTarget target,
            Func<IUiRenderSurface, HostSample> complete, Func<IUiRenderSurface, byte[]> read)
        {
            foreach (var scale in new[] { 1.25f, 1.75f })
            foreach (var scroll in new[] { false, true })
            {
                var viewport = new UiViewport((int)(width * scale), (int)(height * scale), scale);
                var scenario = scroll ? "scroll" : "stationary";
                var frameHashes = new Dictionary<bool, string>();
                for (var batch = 0; batch < order.Length; batch++)
                {
                    var subpixel = order[batch];
                    SKGraphics.PurgeFontCache();
                    var startCache = ObserveFontCache();
                    (Element root, TextBox[] fields) = inspectorFactory != null ? CreateInspectorPerformanceRoot(inspectorFactory) : CreatePerformanceTextBoxes(scale);
                    var profiler = new SettingsPerformanceProbe.ProfiledBackend(renderer);
                    using var ui = UiSession.Create(root, profiler, target, viewport, null, new UiDispatchOptions(), configurePresenters: inspector ? EditorPresenters.Register : null);
                    var surface = profiler.Surface!;
                    var layout = (SkiaDrawingContext)surface.TextLayout;
                    if (inspector) layout.SetPlainSubpixelPositioningForProbe(subpixel);
                    else layout.SetSubpixelPositioningForProbe(subpixel);
                    var gpuAfterCreate = inspector ? ObserveGpuCache(surface.Inner) : null;
                    ui.SetTheme(Theme.Dark);
                    Require(ui.Update(), "Expected the performance fixture to lay out.");
                    complete(surface.Inner);
                    PositionCarets(atEnd: true);
                    Require(ui.Update(), "Expected the anchored text to render.");
                    var initialHost = complete(surface.Inner);
                    var gpuAfterFirstFrame = inspector ? ObserveGpuCache(surface.Inner) : null;
                    var fieldWidths = fields.Select(field => field.Bounds.Width).ToArray();
                    var fieldBounds = new Rect[scroll ? ForwardScrollSteps.Length : 1];
                    for (var field = 0; field < fields.Length; field++)
                    {
                        var editor = fields[field];
                        var textWidth = layout.Measure(editor.Value, editor.FontSize, editor.FontFamily, editor.FontWeight, editor.FontItalic).Width;
                        Require(editor.Bounds.Height > 0 && textWidth > fieldWidths[field] - editor.Padding.Horizontal,
                            "The measured editor must show a horizontally clipped value.");
                    }

                    var warmupClock = Stopwatch.StartNew();
                    var warmupFrames = 0;
                    do
                    {
                        for (var phase = 0; phase < ForwardScrollSteps.Length; phase++) Advance(warmupFrames++);
                    }
                    while (warmupFrames < warmup || warmupClock.ElapsedMilliseconds < minimumWarmupMilliseconds);
                    var warmupMilliseconds = warmupClock.Elapsed.TotalMilliseconds;
                    var warmCache = ObserveFontCache();
                    var gpuAfterWarmup = inspector ? ObserveGpuCache(surface.Inner) : null;
                    var measured = new TextBoxPerformanceFrame[samples];
                    var beforeGc = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
                    var beforeDraws = surface.RenderCalls;
                    for (var frame = 0; frame < samples; frame++) measured[frame] = Advance(frame);
                    var finalCache = ObserveFontCache();
                    var gpuAfterSamples = inspector ? ObserveGpuCache(surface.Inner) : null;
                    var collections = beforeGc.Select((count, generation) => GC.CollectionCount(generation) - count).ToArray();
                    var resets = scroll ? samples / ForwardScrollSteps.Length : 0;
                    Require(!ui.IsFaulted && !ui.NeedsUpdate && fields.All(field => field.CaretIndex == field.Value.Length && field.SelectionLength == 0),
                        "Performance batch left pending work or changed the text editor state.");
                    Require(profiler.Creates == 1 && surface.ResizeCalls == 0 && surface.RenderCalls - beforeDraws == samples + resets,
                        "Performance batch did not draw the expected retained surface.");

                    var gpuByPhase = new GpuCacheUsage?[fieldBounds.Length];
                    for (var phase = 0; inspector && phase < gpuByPhase.Length; phase++)
                    {
                        Advance(phase);
                        gpuByPhase[phase] = ObserveGpuCache(surface.Inner);
                        fieldBounds[phase] = fields[0].Bounds;
                        Require(Math.Abs((fields[0].Bounds.Width - fieldWidths[0]) * scale + phase * 0.25f) < 0.001f,
                            "The retained inspector field did not resize by a quarter device pixel.");
                    }
                    var pixels = read(surface.Inner);
                    var finalFrameSha256 = Convert.ToHexString(SHA256.HashData(pixels));
                    if (frameHashes.TryGetValue(subpixel, out var previous))
                        Require(finalFrameSha256 == previous, $"Repeated {backend}/{scale}/{scenario} frame changed.");
                    else if (inspector && validation)
                        WritePng(Path.Combine(output, $"{backend}-{(int)(scale * 100)}-{scenario}-{PositioningMode(subpixel)}.png"),
                            viewport.Width, viewport.Height, pixels);
                    frameHashes[subpixel] = finalFrameSha256;

                    ui.Dispose();
                    Require(root.IsDisposed && fields.All(field => field.IsDisposed) && surface.Disposals == 1,
                        "Performance session did not release its tree and surface.");
                    cases.Add(new
                    {
                        Backend = backend, Scale = scale, Scenario = scenario, Subpixel = subpixel, Batch = batch + 1, viewport.Width, viewport.Height,
                        WarmupFrames = warmupFrames, WarmupMilliseconds = warmupMilliseconds, Samples = samples,
                        UntimedResetFrames = resets, Collections = collections, FinalFrameSha256 = finalFrameSha256,
                        FontCache = new { AfterPurge = startCache, AfterWarmup = warmCache, AfterSamples = finalCache },
                        GpuCache = inspector && backend != "raster" ? new { AfterCreate = gpuAfterCreate, AfterFirstFrame = gpuAfterFirstFrame,
                            AfterWarmup = gpuAfterWarmup, AfterSamples = gpuAfterSamples, AfterSamplesByPhase = gpuByPhase } : null,
                        InspectorFieldBoundsByPhase = inspector ? fieldBounds : null,
                        Update = PerformanceReport.Summarize(measured.Select(frame => frame.Update)),
                        Render = PerformanceReport.Summarize(measured.Select(frame => frame.Render)),
                        Completion = backend == "raster" ? null : PerformanceReport.Summarize(measured.Select(frame => frame.Completion)),
                        Return = backend == "vulkan" ? PerformanceReport.Summarize(measured.Select(frame => frame.Return)) : null,
                        Frames = measured
                    });

                    TextBoxPerformanceFrame Advance(int index)
                    {
                        var phase = scroll ? index % ForwardScrollSteps.Length : 0;
                        if (scroll)
                        {
                            for (var field = 0; field < fields.Length; field++)
                                fields[field].Width = inspector ? fieldWidths[field] - ForwardScrollSteps[phase].PhasePx / scale : PerformanceFieldWidth(scale, phase);
                            if (phase == 0)
                            {
                                // TextBox retains scroll on width increase. Render Home before returning to End.
                                PositionCarets(atEnd: false);
                                Require(ui.Update(), "Expected the untimed scroll reset to render.");
                                complete(surface.Inner);
                                PositionCarets(atEnd: true);
                            }
                        }
                        else root.Invalidate();

                        var started = PerformanceClock.Begin();
                        var rendered = ui.Update();
                        var update = PerformanceClock.End(started);
                        var draw = surface.Drawing;
                        Require(rendered && draw.Calls == 1, "Performance sample skipped drawing.");
                        var host = complete(surface.Inner);
                        Require(host.UiImage == initialHost.UiImage, "Performance batch replaced its Vulkan image.");
                        return new(phase, update, draw, host.ExportOrCompletion, host.Return);
                    }

                    void PositionCarets(bool atEnd)
                    {
                        foreach (var field in fields)
                        {
                            ui.Focus(field);
                            if (atEnd) MoveToEnd(ui, field);
                            else ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
                        }
                    }
                }
                Require(frameHashes[false] != frameHashes[true], $"Subpixel made no pixel difference in {backend}/{scale}/{scenario}.");
                Console.WriteLine($"{backend}/{scale}/{scenario}: {order.Length} batches of {samples} frames.");
            }
        }
    }

    private static (Panel Root, TextBox[] Fields) CreatePerformanceTextBoxes(float scale)
    {
        var root = new Panel { Background = Theme.Dark.Input };
        var fields = new TextBox[PerformanceColumns * PerformanceRows];
        for (var row = 0; row < PerformanceRows; row++)
        for (var column = 0; column < PerformanceColumns; column++)
        {
            var field = new TextBox
            {
                Value = DarkScrollSamples[column].Text, Width = PerformanceFieldWidth(scale, 0),
                Height = 36, FontSize = ScrollFontSize, CornerRadius = 0,
                HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start,
                Margin = new(16 + column * 184, 16 + row * 44, 0, 0)
            };
            fields[row * PerformanceColumns + column] = field;
            root.Add(field);
        }
        return (root, fields);
    }

    private static float PerformanceFieldWidth(float scale, int phase) => ScrollFieldWidth + (0.75f - ForwardScrollSteps[phase].PhasePx) / scale;

    private static FontCacheUsage ObserveFontCache() => new(SKGraphics.GetFontCacheUsed(), SKGraphics.GetFontCacheCountUsed());

    private static GpuCacheUsage? ObserveGpuCache(IUiRenderSurface surface)
    {
        if (surface is SkiaRasterSurface) return null;
        var (resources, bytes) = surface switch
        {
            SkiaOpenGLSurface gl => gl.GetGpuResourceCacheUsageForProbe(),
            SkiaVulkanSurface vk => vk.GetGpuResourceCacheUsageForProbe(),
            _ => throw new ArgumentException("Expected a Skia surface.", nameof(surface))
        };
        return new(resources, bytes);
    }
}
