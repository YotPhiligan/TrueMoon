using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

internal static class RuntimeProbe
{
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected an ownership/lease rejection."); }
    public static void Run(bool validation)
    {
        var device = new VulkanDevice(validationMessage: validation ? Console.Error.WriteLine : null);
        using (device)
        {
            var builder = App.Builder(b => b.UseDI());
            builder.Setup(app => app.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost()));
            using var app = builder.Build();
            app.StartAsync().GetAwaiter().GetResult();
            var factory = (IUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
            Require(app.Services.GetService(typeof(HostedUiWindow)) == null, "External hosting must not register/create a window.");
            var root = new Panel();
            var button = new Button("Runtime HUD"); root.Add(button);
            var clicks = 0; button.Click += () => clicks++;
            var session = factory.Create(root, SilkVulkanHost.CreateTarget(device), new UiViewport(320, 180, 2));
            Require(session.Update() && !session.Update(), "Invalidation should skip unchanged UI frames.");
            Require(root.Bounds.Width == 160 && root.Bounds.Height == 90, "DPI layout must use logical dimensions.");
            Require(session.HandleInput(new UiInput(InputKind.PointerDown, 10, 10)).PointerCaptured, "Button should capture input.");
            Require(session.HandleInput(new UiInput(InputKind.PointerUp, 10, 10)).Handled && clicks == 1, "Click should bubble once.");
            session.Update();
            var lease = session.AcquireVulkanTexture();
            var layout = (global::Silk.NET.Vulkan.ImageLayout)lease.Info.Layout;
            try
            {
                Require(lease.Info.Width == 320 && lease.Info.Height == 180 && lease.Info.Image != 0, "Expected GPU UI image.");
                var pixels = VulkanHostReadback.Read(device, lease.Info, state => layout = state);
                var offset = (160 * 320 + 300) * 4;
                var color = button.Theme.Control;
                Require(pixels[offset] == color.R && pixels[offset + 1] == color.G && pixels[offset + 2] == color.B && pixels[offset + 3] == 255,
                    "GPU pixels must contain the retained button's control color.");
                Reject(() => session.Resize(new UiViewport(400, 200)));
                Reject(session.Dispose);
            }
            finally { lease.Return(layout); }
            Task.Run(() => session.Post(() => button.Value = "Posted HUD update")).GetAwaiter().GetResult();
            Require(session.Update(), "Posted changes should invalidate the frame.");
            session.Resize(new UiViewport(0, 0)); Require(!session.Update(), "Zero viewport must suspend rendering.");
            session.Resize(new UiViewport(640, 360)); Require(session.Update(), "Restored viewport must render.");
            app.StopAsync().GetAwaiter().GetResult();
            Require(session.IsDisposed && root.IsDisposed, "App registry must dispose the UI tree.");
            using var next = UiSession.Create(new Panel(), new SkiaVulkanRenderBackend(), SilkVulkanHost.CreateTarget(device), new UiViewport(64, 64));
            Require(next.Update(), "Disposing a session must preserve the host device.");
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Runtime Vulkan validation failed.");
        Console.WriteLine("UI runtime passed: invalidation, DPI, input/capture, posted updates, lease guards, suspend/restore, factory stop and borrowed device lifetime.");
    }
    public static async Task RunWindowAsync(bool validation, bool openGL = false)
    {
        var messages = new List<string>();
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UsePresentation<ProbeView>(options => (openGL ? options.UseSkiaOpenGL() : options.UseSkiaVulkan()).UseSilkWindow(w =>
        { w.Width = 320; w.Height = 180; w.ValidationMessage = validation ? m => { messages.Add(m); Console.Error.WriteLine(m); } : null; })));
        await using var app = builder.Build();
        var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
        var restored = false;
        var resized = false;
        Task? restore = null;
        window.FramePresented += session =>
        {
            if (openGL)
            {
                window.PostWindow(host =>
                {
                    using var gl = global::Silk.NET.OpenGL.GL.GetApi(host.NativeWindow);
                    Require(gl.GetError() == global::Silk.NET.OpenGL.GLEnum.NoError, "Standalone OpenGL generated a GL error.");
                });
            }
            if (session.Viewport.Width == 640 && session.Viewport.Height == 360) resized = true;
            if (window.PresentedFrames == 3) window.Resize(640, 360);
            if (window.PresentedFrames == 5)
            {
                window.PostWindow(w => w.NativeWindow.WindowState = global::Silk.NET.Windowing.WindowState.Minimized);
                restore = Task.Run(async () =>
                {
                    await Task.Delay(250);
                    window.PostWindow(w => { w.NativeWindow.WindowState = global::Silk.NET.Windowing.WindowState.Normal; restored = true; });
                });
            }
            if (window.PresentedFrames == 12) session.Post(() => ((Button)session.Root.Children[0]).Value = "Resized UI");
            if (window.PresentedFrames >= 30 && restored) window.Close();
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await app.StartAsync(timeout.Token);
            await window.Completion.WaitAsync(timeout.Token);
        }
        catch (Exception error) { Console.Error.WriteLine($"Standalone primary failure: {error}"); throw; }
        if (restore != null) await restore;
        await app.StopAsync(timeout.Token);
        Require(window.PresentedFrames >= 30 && resized && restored && (openGL || window.SwapchainGenerations >= 2), "Expected presentation, framebuffer resize and minimize/restore.");
        Require(messages.Count == 0, "Window validation emitted warnings/errors.");
        Console.WriteLine($"Standalone {(openGL ? "OpenGL" : "Vulkan")} UI passed: {window.PresentedFrames} frames, resize/minimize/restore, {window.SwapchainGenerations} swapchain generations; {(openGL ? "GL error checks passed" : "Vulkan validation 0 errors/0 warnings including disposal")}.");
    }
}

public sealed class ProbeView : Panel
{
    public ProbeView() => Add(new Button("Standalone UI"));
}
