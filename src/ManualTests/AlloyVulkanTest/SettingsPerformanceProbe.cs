using System.Diagnostics;
using System.Runtime.InteropServices;
using AlloyTest;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Frame baseline, not a throughput microbenchmark: the real owner thread and sequential GPU handoff matter.
internal static class SettingsPerformanceProbe
{
    internal sealed record Options(int Rows, int Warmup, int WarmupMilliseconds, int Samples, string Output, string? EnvironmentFile)
    {
        internal static Options Parse(string[] args)
        {
            string? Read(string option)
            {
                var index = Array.IndexOf(args, option);
                if (index < 0) return null;
                if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"{option} requires a value.");
                return args[index + 1];
            }
            int Number(string option, int fallback) => Read(option) is { } value ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
            var options = new Options(Number("--baseline-rows", 32), Number("--baseline-warmup", 100), Number("--baseline-warmup-ms", 500), Number("--baseline-samples", 500),
                Read("--baseline-output") ?? $"TestResults/AlloyPerformance/baseline-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json", Read("--baseline-environment"));
            if (options.Rows is < 1 or > 256 || options.Warmup is < 1 or > 10000 || options.WarmupMilliseconds is < 0 or > 5000 || options.Samples is < 2 or > 10000 || options.Samples % 2 != 0)
                throw new ArgumentOutOfRangeException(nameof(args), "Rows1..256, warmup1..10000, warmup-ms0..5000, even samples2..10000 required.");
            return options;
        }
    }

    private static readonly UiViewport Viewport = new(900, 1100);
    private const string NameA = "Замер А 👩‍💻", NameB = "Замер Б 👩‍💻";

    internal static unsafe void Run(string[] args)
    {
        var options = Options.Parse(args); var validation = args.Contains("--validation");
        var cases = new List<PerformanceCase>(); var hardware = new Dictionary<string, object>();
        var calibration = PerformanceClock.Calibrate(options.Samples);
        foreach (var mode in Modes) cases.Add(Exercise("raster", mode, new SkiaRasterRenderBackend(), new RasterUiTarget(), options, _ => default));
        using (var window = new SilkWindowHost(320, 240, "Alloy K9 OpenGL", true))
        using (var gl = GL.GetApi(window.NativeWindow))
        {
            hardware["OpenGL"] = new { Renderer = gl.GetStringS(StringName.Renderer), Vendor = gl.GetStringS(StringName.Vendor), Version = gl.GetStringS(StringName.Version) };
            foreach (var mode in Modes)
                cases.Add(Exercise("opengl", mode, new SkiaOpenGLRenderBackend(), OpenGLProbe.Target(window), options,
                    _ => new HostSample(PerformanceClock.Measure(gl.Finish), default, default, default)));
            Require(gl.GetError() == GLEnum.NoError, "K9 OpenGL generated an error, including UI teardown.");
        }
        using (var window = new SilkWindowHost(Viewport.Width, Viewport.Height, "Alloy K9 Vulkan HUD"))
        {
            var device = new VulkanDevice(window.NativeWindow, validation ? Console.Error.WriteLine : null);
            using (device)
            {
                device.Api.GetPhysicalDeviceProperties(device.PhysicalDevice, out var properties);
                hardware["Vulkan"] = new { Name = Marshal.PtrToStringUTF8((nint)properties.DeviceName), properties.VendorID, properties.DeviceID, properties.DriverVersion, properties.ApiVersion, properties.DeviceType,
                    WindowViewport = window.Viewport };
                var target = SilkVulkanHost.CreateTarget(device);
                foreach (var mode in Modes)
                    cases.Add(Exercise("vulkan", mode, new SkiaVulkanRenderBackend(), target, options, surface => Handoff((SkiaVulkanSurface)surface, null, null, 0)));
                using (var compositor = new VulkanHostCompositor(device, Viewport.Width, Viewport.Height))
                {
                    var presenter = new VulkanWindowPresenter(device);
                    using (presenter)
                    {
                        var frame = 0;
                        foreach (var mode in Modes)
                            cases.Add(Exercise("vulkan-hud", mode, new SkiaVulkanRenderBackend(), target, options,
                                surface => Handoff((SkiaVulkanSurface)surface, compositor, presenter, frame++ & 1)));
                        hardware["Presentation"] = new { presenter.UsesPresentFences, presenter.SwapchainGenerations, presenter.PresentedFrames };
                        Require(presenter.SwapchainGenerations == 1, "Fixed-size K9 HUD recreated swapchains.");
                    }
                    hardware["PresenterAfterDispose"] = presenter.Resources;
                    Require(presenter.Resources.LiveSwapchains == 0 && presenter.Resources.LiveSemaphores == 0 && presenter.Resources.LiveFences == 0 && presenter.Resources.LiveCommandPools == 0, "K9 presenter resources leaked.");
                }
                device.WaitIdle();
            }
            Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "K9 Vulkan validation failed, including teardown.");
            hardware["Validation"] = new { Enabled = validation, device.ValidationErrorCount, device.ValidationWarningCount };
        }
        PerformanceReport.Write(options, Viewport, calibration, hardware, cases);
        Console.WriteLine($"Settings baseline passed: {cases.Count} cases, {options.Rows} retained rows, {options.Samples} samples each; static0 layout/arrange/draw, stable surfaces/tree, bounded list mutation and disposal checked. Validation enabled:{validation}; errors0/warnings0 including teardown.");
    }

    private static readonly string[] Modes = ["static", "property-edit", "list-edit"];

    private static PerformanceCase Exercise(string backendName, string mode, IRenderBackend backend, UiRenderTarget target, Options options, Func<IUiRenderSurface, HostSample> host)
    {
        var model = new SettingsModel { Name = NameB }; model.Rows.Clear();
        for (var i = 0; i < options.Rows; i++) model.Rows.Add(new SettingsRow($"Строка K9 {i:D3}"));
        var originalRows = model.Rows.ToArray(); var view = new View1(model) { Width = 720, Height = 1000 };
        var root = new ProfiledRoot(view); var profiler = new ProfiledBackend(backend);
        using var ui = UiSession.Create(root, profiler, target, Viewport);
        var surface = profiler.Surface!; Require(ui.Update(), "Initial baseline frame was not drawn."); var initialOutput = host(surface.Inner);
        var originalNodes = Descendants(root).ToArray(); var originalRowNodes = view.RowList.Items.ToArray();
        var initialCount = originalNodes.Length; var warmup = Stopwatch.StartNew(); var warmupFrames = 0; var addedElements = 0;
        do
        {
            Frame(warmupFrames++, mode, model, root, ui, surface, host);
            if (warmupFrames == 1 && mode == "list-edit") addedElements = Descendants(view.RowList.Items[^1]).Count();
        }
        while (warmupFrames < options.Warmup || warmup.ElapsedMilliseconds < options.WarmupMilliseconds);
        var warmupMilliseconds = warmup.Elapsed.TotalMilliseconds;
        // Begin each measured batch from exactly the same row count/name, outside the measured interval.
        if (model.Rows.Count > options.Rows) model.Rows.RemoveAt(options.Rows);
        model.Name = NameB; ui.Update(); host(surface.Inner);
        var samples = new FrameSample[options.Samples];
        var gcBefore = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        var callsBefore = (root.MeasureCalls, root.ArrangeCalls, surface.RenderCalls, surface.ResizeCalls, profiler.Creates);
        var batchStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < samples.Length; i++) samples[i] = Frame(i, mode, model, root, ui, surface, host);
        var batchTicks = Stopwatch.GetTimestamp() - batchStart;
        var collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]).ToArray();
        var calls = new PerformanceCalls(root.MeasureCalls - callsBefore.MeasureCalls, root.ArrangeCalls - callsBefore.ArrangeCalls,
            surface.RenderCalls - callsBefore.RenderCalls, surface.ResizeCalls - callsBefore.ResizeCalls, profiler.Creates - callsBefore.Creates);
        Require(!ui.IsFaulted && !ui.NeedsUpdate && model.Rows.Count == options.Rows, "Baseline left a fault/pending update or an unbounded list.");
        Require(profiler.Creates == 1 && surface.ResizeCalls == 0 && calls.SurfaceCreates == 0, "Measured batch created/resized UI surfaces.");
        Require(samples.All(sample => sample.Host.UiImage == initialOutput.UiImage), "Baseline replaced the retained Vulkan UI image.");
        Require(mode == "static" ? calls.Measure == 0 && calls.Arrange == 0 && calls.Draw == 0 : calls.Measure == options.Samples && calls.Arrange == options.Samples && calls.Draw == options.Samples,
            "Baseline layout/draw count disagrees with mutation mode.");
        Require(model.Name == NameB && view.NameEditor.Value == NameB, "Baseline property binding lost its final value.");
        var finalNodes = Descendants(root).ToArray();
        Require(initialCount == finalNodes.Length && originalNodes.Zip(finalNodes).All(pair => ReferenceEquals(pair.First, pair.Second)), "Baseline replaced retained controls.");
        for (var i = 0; i < originalRows.Length; i++)
            Require(ReferenceEquals(originalRows[i], model.Rows[i]) && ReferenceEquals(originalRowNodes[i], view.RowList.Items[i]), "List edit recreated an unchanged row.");
        var result = new PerformanceCase(backendName, mode, options.Rows, initialCount, initialCount + addedElements, warmupFrames, warmupMilliseconds,
            options.Samples, batchTicks, collections, calls, samples);
        ui.Dispose(); Require(root.IsDisposed && !root.IsAttached && surface.Disposals == 1, "Baseline UI cleanup failed.");
        model.Name = "После Dispose"; Require(view.NameEditor.Value == NameB, "Disposed baseline retained model subscriptions.");
        Console.WriteLine($"{backendName}/{mode}: samples{samples.Length}, nodes{initialCount}, warmup{warmupFrames}, layout{calls.Measure}/draw{calls.Draw}, Update median {PerformanceReport.Summarize(samples.Select(s => s.Update)).MedianMicroseconds:F3}us.");
        return result;
    }

    private static FrameSample Frame(int index, string mode, SettingsModel model, ProfiledRoot root, UiSession ui, ProfiledSurface surface, Func<IUiRenderSurface, HostSample> host)
    {
        root.Measurement = root.Arrangement = default; surface.Drawing = default;
        var changeStart = PerformanceClock.Begin();
        if (mode == "property-edit") model.Name = index % 2 == 0 ? NameA : NameB;
        if (mode == "list-edit")
        {
            if (index % 2 == 0) model.Rows.Add(new SettingsRow("Новая строка K9"));
            else model.Rows.RemoveAt(model.Rows.Count - 1);
        }
        var mutation = PerformanceClock.End(changeStart);
        var updateStart = PerformanceClock.Begin(); var rendered = ui.Update(); var update = PerformanceClock.End(updateStart);
        ui.VerifyRendering(); var output = host(surface.Inner);
        return new FrameSample(mutation, update, root.Measurement, root.Arrangement, surface.Drawing, output, rendered);
    }

    private static HostSample Handoff(SkiaVulkanSurface surface, VulkanHostCompositor? compositor, VulkanWindowPresenter? presenter, int variant)
    {
        var acquireStart = PerformanceClock.Begin(); var lease = surface.AcquireTexture(); var acquire = PerformanceClock.End(acquireStart);
        var layout = (ImageLayout)lease.Info.Layout; var leaseImage = lease.Info.Image;
        var composition = default(PerformanceMeasurement); var presentation = default(PerformanceMeasurement);
        var returned = default(PerformanceMeasurement);
        try
        {
            if (compositor != null)
            {
                var composeStart = PerformanceClock.Begin();
                compositor.Compose(lease.Info, variant, state => layout = state, readback: false);
                composition = PerformanceClock.End(composeStart);
            }
        }
        finally { var returnStart = PerformanceClock.Begin(); lease.Return(layout); returned = PerformanceClock.End(returnStart); }
        if (presenter != null)
        {
            var presentStart = PerformanceClock.Begin();
            Require(presenter.PresentImage(compositor!.Output, Viewport, compositor.OutputStateChanged), "Baseline HUD did not present.");
            presentation = PerformanceClock.End(presentStart);
        }
        return new HostSample(acquire, returned, composition, presentation, leaseImage);
    }

    private sealed class ProfiledRoot : Panel
    {
        internal PerformanceMeasurement Measurement, Arrangement;
        internal long MeasureCalls, ArrangeCalls;
        internal ProfiledRoot(View1 view) => Add(view);
        protected override Size MeasureCore(Size available, ITextLayoutService text)
        {
            var start = PerformanceClock.Begin();
            try { return base.MeasureCore(available, text); }
            finally { Measurement = PerformanceClock.End(start); MeasureCalls++; }
        }
        protected override void ArrangeCore(Rect contentBounds)
        {
            var start = PerformanceClock.Begin();
            try { base.ArrangeCore(contentBounds); }
            finally { Arrangement = PerformanceClock.End(start); ArrangeCalls++; }
        }
    }
    private sealed class ProfiledBackend(IRenderBackend inner) : IRenderBackend
    {
        internal ProfiledSurface? Surface;
        internal long Creates;
        public IUiRenderSurface CreateSurface(UiRenderTarget target, UiViewport viewport)
        { Creates++; return Surface = new ProfiledSurface(inner.CreateSurface(target, viewport)); }
    }
    private sealed class ProfiledSurface(IUiRenderSurface inner) : IUiRenderSurface
    {
        internal IUiRenderSurface Inner => inner;
        internal PerformanceMeasurement Drawing;
        internal long RenderCalls, ResizeCalls, Disposals;
        public ITextLayoutService TextLayout => inner.TextLayout;
        public void VerifyAvailable() => inner.VerifyAvailable();
        public void Resize(UiViewport viewport) { ResizeCalls++; inner.Resize(viewport); }
        public void Render(Element root, UiViewport viewport)
        {
            var start = PerformanceClock.Begin();
            try { inner.Render(root, viewport); }
            finally { Drawing = PerformanceClock.End(start); RenderCalls++; }
        }
        public void Dispose() { Disposals++; inner.Dispose(); }
    }
    private static IEnumerable<Element> Descendants(Element root)
    { yield return root; foreach (var child in root.Children) foreach (var node in Descendants(child)) yield return node; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
