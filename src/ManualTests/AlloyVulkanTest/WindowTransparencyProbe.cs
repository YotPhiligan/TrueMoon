using System.Diagnostics;
using System.Text.Json;
using System.Runtime.ExceptionServices;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using Color = TrueMoon.Argentis.Color;

namespace AlloyVulkanTest;

// 4.9a only: exercise the actual presentation paths without introducing a public window API.
internal static unsafe class WindowTransparencyProbe
{
    private static readonly Rgb Foreground = new(220, 40, 80);
    private static readonly Rgb[] Backgrounds = [new(40, 100, 180), new(180, 140, 40)];
    internal sealed record Rgb(int R, int G, int B)
    {
        internal Color Color(byte alpha = 255) => new((byte)R, (byte)G, (byte)B, alpha);
        internal Rgb Over(Rgb background, float alpha) => new(
            (int)Math.Round(R * alpha + background.R * (1 - alpha)),
            (int)Math.Round(G * alpha + background.G * (1 - alpha)),
            (int)Math.Round(B * alpha + background.B * (1 - alpha)));
        internal bool Near(Rgb other) => Math.Abs(R - other.R) <= 3 && Math.Abs(G - other.G) <= 3 && Math.Abs(B - other.B) <= 3;
    }
    private sealed record Sample(string Phase, int Background, float Opacity, int LogicalWidth, int LogicalHeight,
        int FramebufferWidth, int FramebufferHeight, Rgb[] Expected, Rgb[] Desktop, bool Matches);
    private sealed record Capability(string Backend, string Mode, bool TransparentAttribute, string? CompositeAlpha,
        string? PresenterAlpha, string? SurfaceFormats, string? SelectedSurfaceFormat, string Graphics, bool SourceAlphaVerified, bool Supported,
        int Frames, int Swapchains, bool Suspended, bool ResourcesReleased, bool ValidationEnabled, int ValidationErrors, int ValidationWarnings,
        int RejectedAlphaRequests, List<Sample> Samples, string? UnavailableReason = null);

    internal static void Run(string[] args)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The desktop composition prototype requires Windows x64.");
        var outputIndex = Array.IndexOf(args, "--transparency-output");
        if (outputIndex >= 0 && outputIndex + 1 == args.Length) throw new ArgumentException("--transparency-output requires a JSON path.");
        var output = outputIndex < 0 ? "TestResults/AlloyTransparency/capabilities.json" : args[outputIndex + 1];
        var results = new List<Capability>();
        using var glfw = Glfw.GetApi(); // Silk owns initialization and its windows; never Init/Terminate here.
        using var background = new ProbeWindow(false, false, true, new Vector2D<int>(70, 70), new Vector2D<int>(720, 480), null);
        foreach (var vulkan in new[] { false, true })
        foreach (var mode in vulkan
            ? (args.Contains("--redirection-clear")
                ? new[] { "opaque", "opacity", "per-pixel", "per-pixel-premultiplied", "per-pixel-premultiplied-clear" }
                : new[] { "opaque", "opacity", "per-pixel", "per-pixel-premultiplied" })
            : new[] { "opaque", "opacity", "per-pixel" })
        {
            var samples = new List<Sample>();
            var perPixel = mode.StartsWith("per-pixel", StringComparison.Ordinal);
            var front = new ProbeWindow(vulkan, perPixel, false,
                new Vector2D<int>(150, 200), new Vector2D<int>(420, 180), args.Contains("--validation") ? Console.Error.WriteLine : null,
                mode.StartsWith("per-pixel-premultiplied", StringComparison.Ordinal) ? CompositeAlphaFlagsKHR.PreMultipliedBitKhr : null);
            Capability result;
            try
            {
                var handle = (WindowHandle*)(front.Window.Native?.Glfw ?? throw new NotSupportedException("GLFW is required."));
                var transparent = glfw.GetWindowAttrib(handle, WindowAttributeGetter.TransparentFramebuffer);
                var sourceChecked = false;
                var suspended = false;
                if (!front.RequestedAlphaAvailable)
                {
                    result = new Capability("Vulkan", mode, transparent, front.CompositeAlpha, null, front.SurfaceFormats, null,
                        front.Graphics, false, false, 0, 0, false, false, front.ValidationEnabled, 0, 0, 0, samples,
                        "PreMultiplied composite alpha is not advertised; no incompatible swapchain or fallback was created.");
                }
                else
                {
                foreach (var phase in new[] { "initial", "resized", "restored" })
                {
                    if (phase == "resized") front.Window.Size = new Vector2D<int>(480, 220);
                    if (phase == "restored")
                    {
                        front.Window.IsVisible = true;
                        front.Window.WindowState = WindowState.Minimized;
                        Pump(background, front, false);
                        Require(front.Window.WindowState == WindowState.Minimized, "Native minimize was not observed.");
                        front.Session.Resize(new UiViewport(0, 0));
                        Require(!front.Session.Update(), "Zero-size UI must suspend rendering.");
                        if (front.Presenter != null) Require(!front.Presenter.Present(front.Session), "Suspended Vulkan must not present.");
                        front.Window.WindowState = WindowState.Normal;
                        Pump(background, front, false); // Complete restore before hiding for the backdrop control.
                        Require(front.Window.WindowState == WindowState.Normal, "Native restore was not observed.");
                        front.Presenter?.InvalidateSwapchain();
                        suspended = true;
                    }
                    for (var b = 0; b < Backgrounds.Length; b++)
                    {
                        front.Window.IsVisible = false;
                        background.Window.Focus();
                        background.Scene.Set(Backgrounds[b], false);
                        Pump(background, front, false);
                        var backdrop = Desktop(front);
                        for (var attempt = 0; !backdrop.All(pixel => pixel.Near(Backgrounds[b])) && attempt < 10; attempt++)
                        { Pump(background, front, false); backdrop = Desktop(front); }
                        Require(backdrop.All(pixel => pixel.Near(Backgrounds[b])), $"Desktop backdrop is occluded or unavailable; actual={string.Join(';', backdrop.Select(p => p.ToString()))}, expected={Backgrounds[b]}, backgroundVisible={background.Window.IsVisible}, backgroundState={background.Window.WindowState}, backgroundTarget={DesktopCompositionNative.ClientHitTargetsWindow(front.Window, background.Window)}.");
                        front.Scene.Set(Foreground, perPixel);
                        front.Render();
                        front.VerifySource(perPixel);
                        sourceChecked = true;
                        front.Window.IsVisible = true;
                        front.Window.Focus();
                        if (mode == "per-pixel-premultiplied-clear")
                        {
                            Pump(background, front, true); // Process first paint/resize before clearing the DWM surface.
                            DesktopCompositionNative.ClearRedirection(front.Window);
                        }
                        foreach (var opacity in mode == "opacity" ? new[] { 0f, .5f, 1f } : new[] { 1f })
                        {
                            // Never combine whole-window opacity with a transparent framebuffer (GLFW undefined behavior).
                            if (mode == "opacity")
                            {
                                Require(!transparent, "Opacity requires a non-transparent framebuffer.");
                                glfw.SetWindowOpacity(handle, opacity);
                                Glfw.ThrowExceptions();
                                Require(Math.Abs(glfw.GetWindowOpacity(handle) - opacity) < .005f, "GLFW opacity did not round-trip.");
                            }
                            Pump(background, front, true);
                            var expected = Enumerable.Range(0, 3).Select(band => Foreground.Over(Backgrounds[b],
                                perPixel ? new[] { 0f, 128f / 255, 1f }[band] : opacity)).ToArray();
                            var actual = Desktop(front);
                            var matches = actual.Zip(expected).All(pair => pair.First.Near(pair.Second));
                            samples.Add(new Sample(phase, b, opacity, front.Window.Size.X, front.Window.Size.Y,
                                front.Window.FramebufferSize.X, front.Window.FramebufferSize.Y, expected, actual, matches));
                            // The opaque endpoint is an independent positive control for foreground presentation/occlusion.
                            if (!perPixel && opacity == 1)
                                Require(matches, "Opaque control failed; desktop capture/presentation is not trustworthy.");
                            if (perPixel) Require(actual[2].Near(Foreground), "Per-pixel opaque endpoint failed.");
                        }
                    }
                }
                result = new Capability(vulkan ? "Vulkan" : "OpenGL", mode, transparent, front.CompositeAlpha,
                    front.Presenter?.SelectedCompositeAlpha.ToString(), front.SurfaceFormats, front.Presenter?.SelectedSurfaceFormat.ToString(),
                    front.Graphics, sourceChecked, samples.All(s => s.Matches),
                    front.Frames, front.Presenter?.SwapchainGenerations ?? 0, suspended, false, front.ValidationEnabled, 0, 0,
                    vulkan && mode == "opaque" ? front.VerifyAlphaRequests() : 0, samples);
                }
            }
            finally { front.Dispose(); }
            result = result with { ResourcesReleased = front.Released };
            results.Add(result);
            Console.WriteLine($"{result.Backend}/{mode}: desktop={(result.Supported ? "SUPPORTED" : "UNSUPPORTED")}; GLFW transparent={result.TransparentAttribute}; compositeAlpha={result.CompositeAlpha ?? "n/a"}; selected={result.PresenterAlpha ?? "n/a"}; source RGBA verified={result.SourceAlphaVerified}; {samples.Count} samples; resources released={result.ResourcesReleased}; {result.UnavailableReason}");
        }
        background.Dispose(); // Include background teardown in the successful report, rather than after saving it.
        Glfw.ThrowExceptions();
        var report = new
        {
            Schema = 1, Utc = DateTimeOffset.UtcNow, OS = Environment.OSVersion.VersionString,
            Runtime = Environment.Version.ToString(), Silk = "2.23.0", Glfw = glfw.GetVersionString(),
            ValidationRequested = args.Contains("--validation"), BackgroundResourcesReleased = background.Released, DesktopTolerance = 3,
            Scope = "Client-area desktop RGB over two known background colors; 3 bands alpha 0/128/255; initial/resize/minimize-restore. Unsupported mode is an explicit capability result, never a backend fallback. No input/chrome/DPI contract.",
            Capabilities = results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        var corrected = results.FirstOrDefault(c => c.Mode == "per-pixel-premultiplied-clear");
        Console.WriteLine($"Desktop capability matrix saved: {Path.GetFullPath(output)}; all controls, source checks and teardown passed. Per-pixel support: GL={results[2].Supported}, Vulkan default={results[5].Supported}, Vulkan premultiplied={results[6].Supported}, Vulkan premultiplied+clear={(corrected == null ? "not tested" : corrected.Supported.ToString())}.");
    }

    private static void Pump(ProbeWindow background, ProbeWindow front, bool renderFront)
    {
        var elapsed = Stopwatch.StartNew();
        do
        {
            background.Window.DoEvents(); front.Window.DoEvents();
            Require(!background.Window.IsClosing && !front.Window.IsClosing, "Prototype window closed before completion.");
            background.Render();
            if (renderFront) front.Render();
            Thread.Sleep(10); // Bounded settling, not a performance measurement.
        } while (elapsed.ElapsedMilliseconds < 300);
        DesktopCompositionNative.Flush();
    }
    private static Rgb[] Desktop(ProbeWindow front) => Enumerable.Range(0, 3).Select(band =>
        DesktopCompositionNative.Pixel(front.Window, front.Window.Size.X * (band * 2 + 1) / 6, front.Window.Size.Y / 2)).ToArray();
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    internal sealed class Scene : Element
    {
        private Rgb _color = WindowTransparencyProbe.Foreground;
        private bool _bands;
        internal void Set(Rgb color, bool bands)
        { _color = color; _bands = bands; Invalidate(Invalidation.Render); }
        protected override Size MeasureCore(Size available, ITextLayoutService text) => available;
        protected override void DrawCore(IDrawingContext drawing)
        {
            for (var band = 0; band < 3; band++)
                drawing.Fill(new Rect(Bounds.X + Bounds.Width * band / 3, Bounds.Y, Bounds.Width / 3, Bounds.Height),
                    _color.Color(_bands ? new byte[] { 0, 128, 255 }[band] : (byte)255));
        }
    }

    internal sealed class ProbeWindow : IDisposable
    {
        internal IWindow Window { get; }
        internal Scene Scene { get; } = new();
        internal UiSession Session { get; } = null!;
        internal VulkanWindowPresenter? Presenter { get; }
        private readonly VulkanDevice? _device;
        private readonly GL? _gl;
        private readonly SilkWindowHost? _host;
        internal SilkWindowHost Host => _host ?? throw new InvalidOperationException("This probe uses a raw window.");
        internal string? CompositeAlpha { get; }
        internal string? SurfaceFormats { get; }
        internal bool ValidationEnabled => _device?.ValidationEnabled ?? false;
        internal int ValidationErrors => _device?.ValidationErrorCount ?? 0;
        internal int ValidationWarnings => _device?.ValidationWarningCount ?? 0;
        internal int SourceChecks { get; private set; }
        internal bool RequestedAlphaAvailable { get; } = true;
        internal string Graphics { get; } = "";
        internal int Frames { get; private set; }
        internal bool Released { get; private set; }
        private bool _disposed;
        private ExceptionDispatchInfo? _inputFailure;
        internal ProbeWindow(bool vulkan, bool transparent, bool visible, Vector2D<int> position, Vector2D<int> size, Action<string>? validation,
            CompositeAlphaFlagsKHR? requestedAlpha = null, WindowAppearance? appearance = null, WindowChromeOptions? chrome = null,
            Element? content = null)
        {
            if (appearance != null)
            {
                _host = new SilkWindowHost(size.X, size.Y, "TrueMoon public window appearance", !vulkan, appearance, chrome);
                Window = _host.NativeWindow;
                Window.Position = position; Window.TopMost = true; Window.IsVisible = visible;
            }
            else Window = Silk.NET.Windowing.Window.Create((vulkan ? WindowOptions.DefaultVulkan : WindowOptions.Default) with
            {
                Size = size, Position = position, Title = $"TrueMoon 4.9a {(vulkan ? "Vulkan" : "OpenGL")} {(transparent ? "alpha" : "opaque")}",
                IsVisible = visible, TopMost = true, TransparentFramebuffer = transparent, ShouldSwapAutomatically = false,
                PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8), PreferredStencilBufferBits = 8
            });
            try
            {
                if (_host == null) Window.Initialize();
                if (vulkan)
                {
                    _device = new VulkanDevice(Window, validation);
                    if (!_device.Api.TryGetInstanceExtension<KhrSurface>(_device.Instance, out var surface))
                        throw new NotSupportedException("VK_KHR_surface is required.");
                    VulkanDevice.Check(surface.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _device.Surface, out var caps), "TransparencySurfaceCapabilities");
                    CompositeAlpha = caps.SupportedCompositeAlpha.ToString();
                    RequestedAlphaAvailable = !requestedAlpha.HasValue || (caps.SupportedCompositeAlpha & requestedAlpha.Value) != 0;
                    uint count = 0;
                    VulkanDevice.Check(surface.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _device.Surface, ref count, null), "TransparencySurfaceFormats");
                    var formats = new SurfaceFormatKHR[count];
                    fixed (SurfaceFormatKHR* ptr = formats)
                        VulkanDevice.Check(surface.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _device.Surface, ref count, ptr), "TransparencySurfaceFormats");
                    SurfaceFormats = string.Join(", ", formats.Select(f => $"{f.Format}/{f.ColorSpace}"));
                    _device.Api.GetPhysicalDeviceProperties(_device.PhysicalDevice, out var properties);
                    Graphics = System.Text.Encoding.UTF8.GetString(properties.DeviceName, (int)Vk.MaxPhysicalDeviceNameSize).TrimEnd('\0') + $"; driverVersion={properties.DriverVersion}";
                    Presenter = appearance == null ? new VulkanWindowPresenter(_device, requestedAlpha)
                        : new VulkanWindowPresenter(_device, appearance.Transparency);
                    Session = UiSession.Create(content ?? Scene, new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(_device), Viewport());
                }
                else
                {
                    _gl = GL.GetApi(Window);
                    _gl.GetFramebufferAttachmentParameter(GLEnum.Framebuffer, GLEnum.BackLeft, GLEnum.FramebufferAttachmentAlphaSize, out int alphaBits);
                    Graphics = $"{_gl.GetStringS(StringName.Version)} / {_gl.GetStringS(StringName.Renderer)}; alphaBits={alphaBits}";
                    var context = Window.GLContext ?? throw new NotSupportedException("OpenGL context is required.");
                    Session = UiSession.Create(content ?? Scene, new SkiaOpenGLRenderBackend(),
                        new OpenGLUiTarget(name => context.TryGetProcAddress(name, out var address) ? address : 0, context.MakeCurrent), Viewport());
                }
                if (_host != null && content != null) _host.Input += HandleInput;
            }
            catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
        }
        private UiViewport Viewport()
        {
            if (_host != null) return _host.Viewport;
            var size = Window.FramebufferSize;
            return new UiViewport(size.X, size.Y, (float)size.X / Window.Size.X);
        }
        internal void Render()
        {
            _host?.VerifyWindowAccess();
            _host?.DispatchPendingWindowInput(); _inputFailure?.Throw();
            if (_gl != null) Window.GLContext!.MakeCurrent();
            var viewport = Viewport();
            Session.Resize(viewport); var updated = Session.Update();
            if (_host is { SupportsCustomFrame: true } && updated) _host.SetWindowRegions(WindowRegionMap.Create(Session.Root));
            if (viewport.IsEmpty) return;
            if (Presenter != null) { if (Presenter.Present(Session)) Frames++; }
            else { Session.PresentOpenGL(); Window.GLContext!.SwapBuffers(); Frames++; }
        }
        private void HandleInput(UiInput input)
        {
            try
            {
                var viewport = Viewport();
                if (viewport != Session.Viewport)
                {
                    Session.Resize(viewport);
                    if (Session.Update() && _host is { SupportsCustomFrame: true })
                        _host.SetWindowRegions(WindowRegionMap.Create(Session.Root));
                }
                Session.HandleInput(input);
            }
            catch (Exception error) { _inputFailure ??= ExceptionDispatchInfo.Capture(error); }
        }
        internal void VerifySource(bool bands)
        {
            byte[] pixels;
            if (_device == null)
            { using var image = Session.ReadbackOpenGLImage(); pixels = RasterProbe.ReadSnapshot(image); }
            else
            {
                var lease = Session.AcquireVulkanTexture();
                var layout = (ImageLayout)lease.Info.Layout;
                try { pixels = VulkanHostReadback.Read(_device, lease.Info, state => layout = state); }
                finally { lease.Return(layout); }
            }
            var viewport = Viewport();
            for (var band = 0; band < 3; band++)
            {
                var offset = (viewport.Height / 2 * viewport.Width + viewport.Width * (band * 2 + 1) / 6) * 4;
                var alpha = bands ? new byte[] { 0, 128, 255 }[band] : (byte)255;
                Require(Math.Abs(pixels[offset + 3] - alpha) <= 1, "GPU source alpha mismatch.");
                var expected = Foreground.Over(new Rgb(0, 0, 0), alpha / 255f);
                Require(new Rgb(pixels[offset], pixels[offset + 1], pixels[offset + 2]).Near(expected), "GPU source premultiplied RGB mismatch.");
            }
            SourceChecks++;
        }
        internal int VerifyAlphaRequests()
        {
            var invalid = new[] { (CompositeAlphaFlagsKHR)0,
                CompositeAlphaFlagsKHR.OpaqueBitKhr | CompositeAlphaFlagsKHR.PreMultipliedBitKhr, (CompositeAlphaFlagsKHR)0x10 };
            foreach (var alpha in invalid)
            {
                VulkanWindowPresenter? probe = null;
                var rejected = false;
                try { probe = new VulkanWindowPresenter(_device!, alpha); probe.Present(Session); }
                catch (NotSupportedException error) when (error.Message.Contains("requested composite alpha", StringComparison.Ordinal)) { rejected = true; }
                finally { probe?.Dispose(); }
                Require(rejected, $"Invalid composite alpha {alpha} was not rejected.");
                var resources = probe?.Resources ?? default;
                Require(resources.LiveSwapchains == 0 && resources.LiveSemaphores == 0 && resources.LiveFences == 0 && resources.LiveCommandPools == 0,
                    "Rejected alpha request retained presenter resources.");
                Require(!Session.IsDisposed && Session.RenderingFailure == null, "Invalid configuration must not fault the UI session.");
            }
            Require(Presenter!.Present(Session), "Existing opaque presenter must remain usable after rejected requests.");
            return invalid.Length;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_host != null) _host.Input -= HandleInput;
            var glError = GLEnum.NoError;
            UiCleanup.Complete(null, () => { if (_gl != null) Window.GLContext!.MakeCurrent(); }, () => Session?.Dispose(), Scene.Dispose,
                () => Presenter?.Dispose(), () => _device?.Dispose(), () => { if (_gl != null) glError = _gl.GetError(); }, () => _gl?.Dispose(),
                () => { if (_host != null) _host.Dispose(); else Window.Dispose(); });
            Require(glError == GLEnum.NoError, $"OpenGL reported {glError} including teardown.");
            if (_device != null)
                Require(_device.ValidationErrorCount == 0 && _device.ValidationWarningCount == 0, "Vulkan validation reported errors/warnings including teardown.");
            if (Presenter != null)
            {
                var resources = Presenter.Resources;
                Require(resources.LiveSwapchains == 0 && resources.LiveSemaphores == 0 && resources.LiveFences == 0 && resources.LiveCommandPools == 0,
                    "Presenter resources retained after teardown.");
            }
            Released = true;
        }
    }
}
