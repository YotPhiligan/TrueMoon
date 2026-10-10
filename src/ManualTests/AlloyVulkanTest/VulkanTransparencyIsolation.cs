using System.Diagnostics;
using System.Text.Json;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using Rgb = AlloyVulkanTest.WindowTransparencyProbe.Rgb;

namespace AlloyVulkanTest;

// No UiSession, Skia context, native Skia, shaders, or CPU upload. Vulkan clears an
// owned GPU image, and the existing presenter blits it into the GLFW swapchain.
internal static unsafe class VulkanTransparencyIsolation
{
    private sealed record Sample(string Phase, Rgb Background, byte Alpha, Rgb Actual, Rgb Expected, bool Matches);
    private sealed record Case(string Name, bool Decorated, string CompositeAlpha, string SupportedCompositeAlpha, string Format,
        Sample[] Samples, int SourceReadbacks, int Frames, int Generations, int Errors, int Warnings, bool Released);
    private static readonly Rgb Foreground = new(220, 40, 80);
    private static readonly Rgb[] Backgrounds = [new(40, 100, 180), new(180, 140, 40)];

    internal static void Run(string[] args)
    {
        if (!OperatingSystem.IsWindows()) throw new NotSupportedException("Win32 isolation probe only.");
        var outputIndex = Array.IndexOf(args, "--isolation-output");
        if (outputIndex >= 0 && outputIndex + 1 == args.Length) throw new ArgumentException("--isolation-output requires a JSON path.");
        var results = new List<Case>();
        using var backdrop = Window.Create(WindowOptions.Default with
        {
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3)),
            Position = new Vector2D<int>(70, 70), Size = new Vector2D<int>(720, 480),
            Title = "TrueMoon raw GL control background", TopMost = true, ShouldSwapAutomatically = false
        });
        backdrop.Initialize();
        using var gl = GL.GetApi(backdrop);
        var graphics = gl.GetStringS(StringName.Renderer);
        var glVersion = gl.GetStringS(StringName.Version);
        foreach (var decorated in new[] { true, false })
        foreach (var mode in new[] { "opaque-control", "premultiplied-baseline", "premultiplied-clear" })
        {
            var samples = new List<Sample>();
            var reads = 0;
            using var window = Window.Create(WindowOptions.DefaultVulkan with
            {
                Position = new Vector2D<int>(150, 200), Size = new Vector2D<int>(420, 180),
                Title = $"TrueMoon raw Vk {mode}", TopMost = true, IsVisible = false,
                TransparentFramebuffer = true, WindowBorder = decorated ? WindowBorder.Resizable : WindowBorder.Hidden,
                ShouldSwapAutomatically = false, PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8)
            });
            window.Initialize();
            var device = new VulkanDevice(window, args.Contains("--validation") ? Console.Error.WriteLine : null);
            string supportedAlpha;
            using (device)
            {
                var requested = mode == "opaque-control" ? CompositeAlphaFlagsKHR.OpaqueBitKhr : CompositeAlphaFlagsKHR.PreMultipliedBitKhr;
                if (!device.Api.TryGetInstanceExtension<KhrSurface>(device.Instance, out var surface))
                    throw new NotSupportedException("VK_KHR_surface required.");
                using (surface)
                {
                    VulkanDevice.Check(surface.GetPhysicalDeviceSurfaceCapabilities(device.PhysicalDevice, device.Surface, out var caps), "IsolationCapabilities");
                    supportedAlpha = caps.SupportedCompositeAlpha.ToString();
                    if ((caps.SupportedCompositeAlpha & requested) == 0) throw new NotSupportedException($"{requested} not supported; advertised: {caps.SupportedCompositeAlpha}.");
                }
                var presenter = new VulkanWindowPresenter(device, requested);
                using (presenter)
                using (var image = new ClearImage(device))
                {
                    foreach (var phase in new[] { "initial", "resized", "restored" })
                    {
                        if (phase == "resized") { window.Size = new Vector2D<int>(480, 220); presenter.InvalidateSwapchain(); }
                        if (phase == "restored")
                        {
                            window.IsVisible = true; window.WindowState = WindowState.Minimized;
                            Pump(() => { backdrop.DoEvents(); window.DoEvents(); }, 200);
                            window.WindowState = WindowState.Normal; presenter.InvalidateSwapchain();
                        }
                        window.IsVisible = false;
                        foreach (var background in Backgrounds)
                        {
                            window.IsVisible = false;
                            backdrop.Focus();
                            Pump(() => Background(backdrop, gl, window, background), 300);
                            var control = DesktopCompositionNative.Pixel(window, window.Size.X / 2, window.Size.Y / 2);
                            // Restore/show animation and activation can outlast a fixed settling interval.
                            // Wait only for the independently known backdrop, never for the tested alpha result.
                            for (var attempt = 0; !control.Near(background) && attempt < 10; attempt++)
                            {
                                Pump(() => Background(backdrop, gl, window, background), 100);
                                DesktopCompositionNative.Flush();
                                control = DesktopCompositionNative.Pixel(window, window.Size.X / 2, window.Size.Y / 2);
                            }
                            Require(control.Near(background), $"Control backdrop is obstructed: mode={mode}, decorated={decorated}, phase={phase}, actual={control}, expected={background}.");
                            window.IsVisible = true;
                            window.Focus();
                            Pump(() => { backdrop.DoEvents(); window.DoEvents(); }, 100);
                            if (mode == "premultiplied-clear") DesktopCompositionNative.ClearRedirection(window);
                            foreach (byte alpha in new byte[] { 0, 128, 255 })
                            {
                                image.Clear(Foreground, alpha);
                                image.Verify(Foreground, alpha); reads++;
                                Pump(() =>
                                {
                                    // Repeated GL swaps can raise the TopMost backdrop above the foreground.
                                    window.DoEvents(); backdrop.DoEvents();
                                    presenter.PresentImage(image.Info, new UiViewport(window.FramebufferSize.X, window.FramebufferSize.Y), image.SetLayout);
                                }, 300);
                                DesktopCompositionNative.Flush();
                                var actual = DesktopCompositionNative.Pixel(window, window.Size.X / 2, window.Size.Y / 2);
                                var expected = Foreground.Over(mode == "opaque-control" ? new Rgb(0, 0, 0) : background, alpha / 255f);
                                samples.Add(new Sample(phase, background, alpha, actual, expected, actual.Near(expected)));
                            }
                        }
                    }
                    var format = presenter.SelectedSurfaceFormat.ToString();
                    var frames = presenter.PresentedFrames;
                    var generations = presenter.SwapchainGenerations;
                    presenter.Dispose();
                    Require(presenter.Resources.LiveSwapchains == 0 && presenter.Resources.LiveSemaphores == 0 &&
                        presenter.Resources.LiveFences == 0 && presenter.Resources.LiveCommandPools == 0, "Presenter resource leak.");
                    results.Add(new Case(mode, decorated, requested.ToString(), supportedAlpha, format, samples.ToArray(), reads, frames, generations, 0, 0, true));
                }
            }
            Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Vulkan validation failure including teardown.");
            results[^1] = results[^1] with { Errors = device.ValidationErrorCount, Warnings = device.ValidationWarningCount };
            Require(samples.Where(s => s.Alpha == 255).All(s => s.Matches), "Opaque endpoint failed: Vulkan image is not visible.");
            Console.WriteLine($"{mode}, decorated={decorated}: {samples.Count(s => s.Matches)}/{samples.Count} desktop samples; validation=0/0, resources=0.");
        }
        // Managed SkiaSharp.dll metadata can load for VulkanTextureInfo; it is not native Skia.
        using var process = Process.GetCurrentProcess();
        var loadedSkia = process.Modules.Cast<ProcessModule>().Any(m => m.ModuleName.Equals("libSkiaSharp.dll", StringComparison.OrdinalIgnoreCase));
        Require(!loadedSkia, "Isolation unexpectedly loaded native Skia.");
        backdrop.GLContext!.MakeCurrent();
        Require(gl.GetError() == GLEnum.NoError, "Raw GL backdrop error.");
        gl.Dispose(); backdrop.Dispose();
        var report = new { TimeUtc = DateTime.UtcNow, OS = Environment.OSVersion.VersionString,
            Runtime = Environment.Version.ToString(), Graphics = graphics, OpenGL = glVersion,
            ValidationEnabled = args.Contains("--validation"), DesktopTolerance = 3, NativeSkiaLoaded = loadedSkia,
            Transport = "GPU vkCmdClearColorImage then VulkanWindowPresenter blit; readbacks are verification only.", Cases = results };
        if (outputIndex >= 0)
        {
            var path = Path.GetFullPath(args[outputIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Require(results.Where(c => c.Name == "opaque-control").All(c => c.Samples.All(s => s.Matches)), "Opaque negative control failed.");
        Console.WriteLine("Raw Vulkan isolation completed; report records supported/failed composition, native Skia not loaded.");
    }

    private static void Background(IWindow backdrop, GL gl, IWindow window, Rgb color)
    {
        backdrop.DoEvents(); window.DoEvents(); backdrop.GLContext!.MakeCurrent();
        gl.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, 1);
        gl.Clear(ClearBufferMask.ColorBufferBit); backdrop.GLContext.SwapBuffers();
    }
    private static void Pump(Action action, int milliseconds)
    { var timer = Stopwatch.StartNew(); do { action(); Thread.Sleep(10); } while (timer.ElapsedMilliseconds < milliseconds); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class ClearImage : IDisposable
    {
        private readonly VulkanDevice _device;
        private Image _image;
        private DeviceMemory _memory;
        private CommandPool _pool;
        private CommandBuffer _command;
        private ImageLayout _layout;
        internal VulkanTextureInfo Info => new()
        {
            Image = _image.Handle, Width = 32, Height = 32, Format = (uint)Format.R8G8B8A8Unorm,
            Layout = (uint)_layout, QueueFamily = _device.QueueFamily,
            Usage = (uint)(ImageUsageFlags.TransferDstBit | ImageUsageFlags.TransferSrcBit), LevelCount = 1,
            SampleCount = 1, Tiling = (uint)ImageTiling.Optimal, SharingMode = (uint)SharingMode.Exclusive
        };
        internal ClearImage(VulkanDevice device)
        {
            _device = device;
            try
            {
                var vk = device.Api;
                var create = new ImageCreateInfo
                {
                    SType = StructureType.ImageCreateInfo, ImageType = ImageType.Type2D, Format = Format.R8G8B8A8Unorm,
                    Extent = new Extent3D(32, 32, 1), MipLevels = 1, ArrayLayers = 1, Samples = SampleCountFlags.Count1Bit,
                    Tiling = ImageTiling.Optimal, Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.TransferSrcBit,
                    SharingMode = SharingMode.Exclusive, InitialLayout = ImageLayout.Undefined
                };
                VulkanDevice.Check(vk.CreateImage(device.Device, in create, null, out _image), "CreateIsolationImage");
                vk.GetImageMemoryRequirements(device.Device, _image, out var requirements);
                vk.GetPhysicalDeviceMemoryProperties(device.PhysicalDevice, out var properties);
                uint? memoryType = null;
                for (uint i = 0; i < properties.MemoryTypeCount; i++)
                    if ((requirements.MemoryTypeBits & (1u << (int)i)) != 0 &&
                        (properties.MemoryTypes[(int)i].PropertyFlags & MemoryPropertyFlags.DeviceLocalBit) != 0) { memoryType = i; break; }
                var allocate = new MemoryAllocateInfo { SType = StructureType.MemoryAllocateInfo,
                    AllocationSize = requirements.Size, MemoryTypeIndex = memoryType ?? throw new NotSupportedException("No local memory.") };
                VulkanDevice.Check(vk.AllocateMemory(device.Device, in allocate, null, out _memory), "AllocateIsolationImage");
                VulkanDevice.Check(vk.BindImageMemory(device.Device, _image, _memory, 0), "BindIsolationImage");
                var pool = new CommandPoolCreateInfo { SType = StructureType.CommandPoolCreateInfo, QueueFamilyIndex = device.QueueFamily };
                VulkanDevice.Check(vk.CreateCommandPool(device.Device, in pool, null, out _pool), "IsolationPool");
                var commands = new CommandBufferAllocateInfo { SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = _pool, CommandBufferCount = 1, Level = CommandBufferLevel.Primary };
                VulkanDevice.Check(vk.AllocateCommandBuffers(device.Device, in commands, out _command), "IsolationCommand");
            }
            catch (Exception error) { UiCleanup.Complete(error, Dispose); throw; }
        }
        internal void SetLayout(ImageLayout layout) => _layout = layout;
        internal void Clear(Rgb rgb, byte alpha)
        {
            var vk = _device.Api;
            _device.WaitIdle();
            VulkanDevice.Check(vk.ResetCommandPool(_device.Device, _pool, 0), "ResetIsolationPool");
            var begin = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo, Flags = CommandBufferUsageFlags.OneTimeSubmitBit };
            VulkanDevice.Check(vk.BeginCommandBuffer(_command, in begin), "BeginIsolation");
            var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
            var barrier = new ImageMemoryBarrier { SType = StructureType.ImageMemoryBarrier,
                SrcAccessMask = _layout == ImageLayout.Undefined ? 0 : AccessFlags.TransferReadBit,
                DstAccessMask = AccessFlags.TransferWriteBit, OldLayout = _layout, NewLayout = ImageLayout.TransferDstOptimal,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored, DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
                Image = _image, SubresourceRange = range };
            vk.CmdPipelineBarrier(_command, PipelineStageFlags.AllCommandsBit, PipelineStageFlags.TransferBit, 0, 0, null, 0, null, 1, &barrier);
            var a = alpha / 255f;
            var color = new ClearColorValue(rgb.R / 255f * a, rgb.G / 255f * a, rgb.B / 255f * a, a);
            vk.CmdClearColorImage(_command, _image, ImageLayout.TransferDstOptimal, in color, 1, in range);
            VulkanDevice.Check(vk.EndCommandBuffer(_command), "EndIsolation");
            var command = _command;
            var submit = new SubmitInfo { SType = StructureType.SubmitInfo, CommandBufferCount = 1, PCommandBuffers = &command };
            VulkanDevice.Check(vk.QueueSubmit(_device.Queue, 1, in submit, default), "SubmitIsolation");
            _device.WaitIdle(); _layout = ImageLayout.TransferDstOptimal;
        }
        internal void Verify(Rgb rgb, byte alpha)
        {
            var pixels = VulkanHostReadback.Read(_device, Info, SetLayout);
            var expected = rgb.Over(new Rgb(0, 0, 0), alpha / 255f);
            for (var i = 0; i < pixels.Length; i += 4)
                Require(pixels[i + 3] == alpha && new Rgb(pixels[i], pixels[i + 1], pixels[i + 2]).Near(expected), "Raw Vulkan clear RGBA mismatch.");
        }
        public void Dispose()
        {
            _device.WaitIdle();
            var vk = _device.Api;
            if (_pool.Handle != 0) { vk.DestroyCommandPool(_device.Device, _pool, null); _pool = default; }
            if (_image.Handle != 0) { vk.DestroyImage(_device.Device, _image, null); _image = default; }
            if (_memory.Handle != 0) { vk.FreeMemory(_device.Device, _memory, null); _memory = default; }
        }
    }
}
