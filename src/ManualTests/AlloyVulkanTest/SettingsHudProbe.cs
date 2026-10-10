using System.Diagnostics;
using AlloyTest;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyVulkanTest;

// Host owns the window, raw Vulkan handles, scene, compositor and frame loop.
internal static class SettingsHudProbe
{
    internal static void Run(bool validation, bool interactive)
    {
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost()));
        using var app = builder.Build(); app.StartAsync().GetAwaiter().GetResult();
        var factory = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        using var window = new SilkWindowHost(1100, 1100, "TrueMoon shared settings HUD — Escape to close");
        var device = new VulkanDevice(window.NativeWindow, validation ? Console.Error.WriteLine : null);
        using (device)
        {
            using var presenter = new VulkanWindowPresenter(device);
            var target = new VulkanUiTarget(new VulkanHostContext(device.Instance.Handle, device.PhysicalDevice.Handle,
                device.Device.Handle, device.Queue.Handle, device.QueueFamily, Vk.Version11, device.GetProcedureAddress,
                device.WaitIdle, device.InstanceExtensions, device.DeviceExtensions));
            var model = new SettingsModel(); var script = new SettingsFormSmoke();
            var view = new View1(model) { Width = 720, Height = 1000 };
            var metrics = new Text("События сцены: 0"); var sceneButton = new Button("Изменить сцену");
            var overlay = new Border { Width = 280, Height = 120, Margin = new Thickness(740, 12, 0, 0),
                Background = new Color(40, 120, 200, 128), Padding = new Thickness(12) };
            overlay.SetContent(new VStack { Spacing = 8 }.WithChildren(metrics, sceneButton));
            var root = new Panel().WithChildren(view, overlay);
            var ui = factory.Create(root, target, window.Viewport, window);
            VulkanHostCompositor? compositor = null; UiViewport sceneSize = default;
            var frames = 0; var draws = 0; var readbacks = 0; var gameEvents = 0; var sceneMode = 0;
            var sessions = 1; var scriptFinished = false; var suspended = false; var restored = false;
            var elapsed = Stopwatch.StartNew(); double? restoreAt = null; Exception? callbackFailure = null;
            void ChangeScene() { sceneMode ^= 1; metrics.Value = $"События сцены: {gameEvents}; режим: {sceneMode}"; }
            sceneButton.Click += ChangeScene;
            UiInputResult Route(UiInput input)
            {
                var viewport = window.Viewport;
                if (viewport != ui.Viewport) { ui.Resize(viewport); ui.Update(); }
                var result = ui.HandleInput(input);
                var pointer = input.Kind is InputKind.PointerMove or InputKind.PointerDown or InputKind.PointerUp or InputKind.Wheel;
                if (!result.Handled && !(pointer ? result.PointerCaptured : result.KeyboardFocused))
                {
                    gameEvents++;
                    if (input.Kind == InputKind.PointerDown) ChangeScene();
                }
                if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) window.Close();
                return result;
            }
            void Guard(Action action)
            {
                if (callbackFailure != null) return;
                try { action(); }
                catch (Exception error) { callbackFailure = error; window.Close(); }
            }
            Action<UiInput> inputHandler = input => Guard(() => Route(input));
            window.Input += inputHandler;
            window.NativeWindow.Update += _ => Guard(() =>
            {
                if (restoreAt is { } time && elapsed.Elapsed.TotalSeconds >= time)
                { window.NativeWindow.WindowState = WindowState.Normal; restoreAt = null; restored = true; }
                if (!interactive && elapsed.Elapsed.TotalSeconds > 30) throw new TimeoutException("Settings HUD did not finish.");
            });
            window.RenderRequested += () => Guard(() =>
            {
                var viewport = window.NativeWindow.WindowState == WindowState.Minimized ? new UiViewport(0, 0) : window.Viewport;
                ui.Resize(viewport);
                if (viewport.IsEmpty) { Require(!ui.Update(), "Minimized settings HUD drew."); return; }
                if (ui.Update()) draws++;
                if (compositor == null || sceneSize != viewport)
                { compositor?.Dispose(); compositor = new VulkanHostCompositor(device, viewport.Width, viewport.Height); sceneSize = viewport; }
                var lease = ui.AcquireVulkanTexture(); var layout = (ImageLayout)lease.Info.Layout;
                var variant = (sceneMode + frames / 15) % 2;
                try
                {
                    var inspect = !interactive && frames % 20 == 0;
                    var pixels = compositor.Compose(lease.Info, variant, state => layout = state, inspect);
                    if (inspect)
                    {
                        var colors = VulkanHostCompositor.SceneColors(variant);
                        Pixel(pixels, viewport.Width, viewport.Width - 4, viewport.Height - 4,
                            new Color(colors.Right.Red, colors.Right.Green, colors.Right.Blue));
                        var x = (int)((overlay.Bounds.X + 3) * viewport.Scale);
                        var y = (int)((overlay.Bounds.Y + 3) * viewport.Scale);
                        var background = x < viewport.Width / 2 ? colors.Left : colors.Right;
                        static byte Blend(byte front, byte back) => (byte)((front * 128 + back * 127 + 127) / 255);
                        Pixel(pixels, viewport.Width, x, y, new Color(Blend(40, background.Red), Blend(120, background.Green), Blend(200, background.Blue)), 1);
                        readbacks++;
                    }
                }
                finally { lease.Return(layout); }
                presenter.PresentImage(compositor.Output, viewport, compositor.OutputStateChanged);
                if (!interactive)
                {
                    if (frames < 40) script.Frame(ui, view);
                    if (frames == 40) { Require(!ui.NeedsUpdate, "Completed settings script remained dirty."); scriptFinished = true; }
                    if (frames == 43)
                    {
                        var before = gameEvents;
                        Require(!Route(new UiInput(InputKind.PointerDown, 1090, 1090)).Handled && gameEvents == before + 1, "Outside settings input did not reach scene.");
                    }
                    if (frames == 45)
                    {
                        var bounds = sceneButton.Bounds;
                        Require(Route(new UiInput(InputKind.PointerDown, bounds.X + 10, bounds.Y + 10)).PointerCaptured, "HUD scene button did not capture.");
                        Require(Route(new UiInput(InputKind.PointerUp, bounds.X + 10, bounds.Y + 10)).Handled, "HUD scene button did not activate.");
                    }
                    if (frames == 48) window.Resize(1280, 1200);
                    if (frames == 52)
                    {
                        ui.Resize(new UiViewport(0, 0)); Require(!ui.Update(), "Zero viewport did not suspend settings HUD."); suspended = true;
                        window.NativeWindow.WindowState = WindowState.Minimized; restoreAt = elapsed.Elapsed.TotalSeconds + .25;
                    }
                    if (frames == 60) window.Resize(1100, 1100);
                    if (frames == 65)
                    {
                        var editor = view.NameEditor;
                        ui.Focus(editor); ui.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.A, Control: true));
                        ui.HandleInput(new UiInput(InputKind.Text, Text: "Сохранено после пересоздания 🧑‍💻"));
                        model.AddRow(); var count = model.Rows.Count;
                        ui.Dispose(); script.VerifyDisposed();
                        Require(factory.ActiveSessionCount == 0, "Old settings session remained registered.");
                        root = new Panel(); view = new View1(model) { Width = 720, Height = 1000 };
                        metrics = new Text("Новая сессия, прежняя модель"); sceneButton = new Button("Изменить сцену");
                        sceneButton.Click += ChangeScene;
                        overlay = new Border { Width = 280, Height = 120, Margin = new Thickness(740, 12, 0, 0), Background = new Color(40, 120, 200, 128), Padding = new Thickness(12) };
                        overlay.SetContent(new VStack { Spacing = 8 }.WithChildren(metrics, sceneButton)); root.Add(view).Add(overlay);
                        ui = factory.Create(root, target, window.Viewport, window); sessions++;
                        ui.Update(); draws++;
                        Require(view.NameEditor.Value == model.Name && view.RowList.Items.Count == count && !editor.IsFocused && editor.IsDisposed,
                            "Recreated settings did not retain model or cancel old focus.");
                    }
                    if (frames == 70)
                    {
                        var before = gameEvents;
                        Require(!Route(new UiInput(InputKind.PointerDown, 1090, 1090)).Handled && gameEvents == before + 1
                            && metrics.Value.Contains($"{gameEvents}"), "New session did not route scene input to new metrics.");
                    }
                    if (frames >= 89 && restored) window.Close();
                }
                frames++;
            });
            try
            {
                window.Run();
                if (callbackFailure != null) throw new InvalidOperationException("Settings HUD callback failed.", callbackFailure);
                if (!interactive) Require(frames >= 90 && sessions == 2 && scriptFinished && suspended && restored && gameEvents > 0 && readbacks >= 5
                    && draws < frames / 2 && presenter.SwapchainGenerations >= 3, "Settings HUD did not prove retained composition/lifecycle.");
            }
            finally
            {
                window.Input -= inputHandler;
                try { ui.Dispose(); }
                finally { compositor?.Dispose(); }
            }
            if (!interactive) script.VerifyDisposed();
            app.StopAsync().GetAwaiter().GetResult();
            Require(factory.ActiveSessionCount == 0 && root.IsDisposed && view.IsDisposed, "Settings HUD retained sessions/tree.");
            device.WaitIdle(); // Still host-owned and usable after UI/App teardown.
            Console.WriteLine(interactive ? $"Shared settings HUD demo closed: {frames} GPU compositions, {draws} UI draws; registry0 and resources released." :
                $"Shared settings HUD passed: {frames} GPU compositions/presents, {draws} UI draws, {sessions} sessions, {readbacks} readbacks; model/list/input, scene passthrough, alpha, resize/suspend/restore, registry0 and borrowed host verified.");
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Settings HUD validation failed, including teardown.");
        if (validation) Console.WriteLine("Settings HUD Vulkan validation: 0 errors, 0 warnings, including teardown.");
    }
    private static void Pixel(byte[] pixels, int width, int x, int y, Color expected, int tolerance = 0)
    {
        var offset = (y * width + x) * 4;
        Require(Math.Abs(pixels[offset] - expected.R) <= tolerance && Math.Abs(pixels[offset + 1] - expected.G) <= tolerance
            && Math.Abs(pixels[offset + 2] - expected.B) <= tolerance && pixels[offset + 3] == expected.A, $"Settings HUD unexpected scene pixel ({x},{y}).");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
