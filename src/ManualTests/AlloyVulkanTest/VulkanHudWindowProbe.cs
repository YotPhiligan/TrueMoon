using System.Diagnostics;
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

// Game-owned window, device, scene and frame loop; only the retained UI session belongs to Alloy.
internal sealed class VulkanHudWindowProbe
{
    private static readonly Color Overlay = new(40, 120, 200, 128);
    private readonly bool _interactive;
    private readonly bool _rawHandles;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private UiSession _ui = null!;
    private HudView _hud = null!;
    private VulkanHostCompositor? _compositor;
    private UiViewport _sceneSize;
    private float _scale = 1;
    private int _compositions, _uiDraws, _readbacks, _sessions, _clicks, _gameEvents, _sceneMode;
    private int _scriptedFrame = -1;
    private bool _focusPreserved, _suspended, _restored;
    private double? _restoreAt;

    private VulkanHudWindowProbe(bool interactive, bool rawHandles) { _interactive = interactive; _rawHandles = rawHandles; }
    internal static void Run(bool validation, bool interactive, bool rawHandles = false) => new VulkanHudWindowProbe(interactive, rawHandles).RunCore(validation);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private void RunCore(bool validation)
    {
        var builder = App.Builder(b => b.UseDI());
        builder.Setup(app => app.UseAlloy(options => options.UseSkiaVulkan().UseExternalHost()));
        using var app = builder.Build();
        app.StartAsync().GetAwaiter().GetResult();
        var factory = (IUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
        using var window = new SilkWindowHost(640, 360, "TrueMoon retained Vulkan HUD — Escape to close");
        var device = new VulkanDevice(window.NativeWindow, validation ? Console.Error.WriteLine : null);
        using (device)
        {
            using var presenter = new VulkanWindowPresenter(device);
            Console.WriteLine($"Presentation retirement: {device.PresentFenceExtension ?? "replacement-image reacquisition (legacy)"}");
            Console.WriteLine($"UI target: {(_rawHandles ? "raw host Vulkan handles" : "Silk convenience device")}");
            CreateSession(factory, device, window);
            try
            {
                window.Input += input => RouteInput(input, window);
                window.NativeWindow.Update += _ =>
                {
                    if (_restoreAt is { } time && _elapsed.Elapsed.TotalSeconds >= time)
                    {
                        window.NativeWindow.WindowState = WindowState.Normal;
                        _restoreAt = null; _restored = true;
                    }
                    if (!_interactive && _elapsed.Elapsed.TotalSeconds > 30) window.Close();
                };
                window.RenderRequested += () =>
                {
                    if (!_interactive) Script(factory, device, window);
                    var viewport = window.NativeWindow.WindowState == WindowState.Minimized
                        ? new UiViewport(0, 0, _scale) : _interactive ? window.Viewport : window.Viewport with { Scale = _scale };
                    if (viewport != _ui.Viewport) _ui.Resize(viewport);
                    if (viewport.IsEmpty) { Require(!_ui.Update(), "Suspended HUD must not draw."); return; }
                    if (_ui.Update()) _uiDraws++;
                    if (_compositor == null || _sceneSize.Width != viewport.Width || _sceneSize.Height != viewport.Height)
                    {
                        _compositor?.Dispose();
                        _compositor = new VulkanHostCompositor(device, viewport.Width, viewport.Height);
                        _sceneSize = viewport;
                    }
                    var variant = (_sceneMode + _compositions / 15) % 2;
                    var lease = _ui.AcquireVulkanTexture();
                    var layout = (ImageLayout)lease.Info.Layout;
                    try
                    {
                        // Readback only on assertion frames. All other frames stay entirely on the GPU.
                        var inspect = !_interactive && _compositions % 15 == 0;
                        var pixels = _compositor.Compose(lease.Info, variant, state => layout = state, inspect);
                        if (inspect) { AssertScene(pixels, viewport, variant); _readbacks++; }
                    }
                    finally { lease.Return(layout); }
                    _compositions++;
                    presenter.PresentImage(_compositor.Output, viewport, _compositor.OutputStateChanged);
                    if (!_interactive && presenter.PresentedFrames >= 90 && _restored) window.Close();
                };
                window.Run();
                if (!_interactive)
                {
                    Require(presenter.PresentedFrames >= 90 && presenter.SwapchainGenerations >= 3, "Expected sustained presentation and repeated swapchain resize.");
                    Require(_sessions == 2 && _clicks >= 2 && _gameEvents > 0 && _focusPreserved, "Expected repeated sessions, interactive HUD and unhandled game input.");
                    Require(_suspended && _restored && _readbacks >= 6 && _uiDraws < _compositions / 2, "Expected suspend/restore, GPU assertions and UI redraw only on change.");
                    Console.WriteLine($"Retained HUD window: {presenter.PresentedFrames} presented frames, {_compositions} scene compositions, {_uiDraws} UI redraws, {_sessions} sessions, {presenter.SwapchainGenerations} swapchains, {_readbacks} scene readbacks.");
                }
            }
            finally { _ui.Dispose(); _compositor?.Dispose(); }
            app.StopAsync().GetAwaiter().GetResult();
            // The game device remains usable after App and UI teardown.
            device.WaitIdle();
        }
        Require(device.ValidationErrorCount == 0 && device.ValidationWarningCount == 0, "Vulkan validation reported errors/warnings, including disposal.");
        Console.WriteLine(_interactive ? "HUD demo closed; game/UI resources released." :
            "Retained HUD passed: scene/alpha/text pixels, UI input and scene passthrough, dynamic tree/focus, logical DPI scales 100/150/200%, resize/minimize/restore and borrowed device lifetime.");
        if (validation) Console.WriteLine("Vulkan core/synchronization validation: 0 errors, 0 warnings, including window HUD teardown.");
    }

    private void CreateSession(IUiSessionFactory factory, VulkanDevice device, SilkWindowHost window)
    {
        _hud = new HudView();
        _hud.Metrics.Value = $"Actions: {_clicks}";
        _hud.Action.Click += () => { _clicks++; _sceneMode ^= 1; _hud.Metrics.Value = $"Actions: {_clicks}"; };
        var target = _rawHandles ? new VulkanUiTarget(new VulkanHostContext(
            device.Instance.Handle, device.PhysicalDevice.Handle, device.Device.Handle, device.Queue.Handle,
            device.QueueFamily, Vk.Version11, device.GetProcedureAddress, device.WaitIdle,
            device.InstanceExtensions, device.DeviceExtensions)) : SilkVulkanHost.CreateTarget(device);
        _ui = factory.Create(_hud.Root, target, _interactive ? window.Viewport : window.Viewport with { Scale = _scale }, window);
        _sessions++;
    }
    private UiInputResult RouteInput(UiInput input, SilkWindowHost window)
    {
        var result = _ui.HandleInput(input);
        var pointer = input.Kind is InputKind.PointerMove or InputKind.PointerDown or InputKind.PointerUp or InputKind.Wheel;
        if (!result.Handled && !(pointer ? result.PointerCaptured : result.KeyboardFocused))
        {
            _gameEvents++;
            if (input.Kind == InputKind.PointerDown) _sceneMode ^= 1;
        }
        if (input.Kind == InputKind.KeyDown && input.Key == UiKey.Escape) window.Close();
        return result;
    }
    private void Script(IUiSessionFactory factory, VulkanDevice device, SilkWindowHost window)
    {
        if (_scriptedFrame == _compositions) return;
        _scriptedFrame = _compositions;
        switch (_compositions)
        {
            case 2:
                var button = _hud.Action.Bounds;
                var down = RouteInput(new UiInput(InputKind.PointerDown, button.X + 10, button.Y + 10), window);
                Require(down.Handled && down.PointerCaptured && down.KeyboardFocused, "HUD button must capture pointer and keyboard focus.");
                var up = RouteInput(new UiInput(InputKind.PointerUp, button.X + 10, button.Y + 10), window);
                Require(up.Handled && !up.PointerCaptured && _clicks == 1, "Captured click should activate exactly once.");
                break;
            case 4: _hud.List.Add(new Text("Dynamic HUD entry")); break;
            case 5:
                _focusPreserved = _hud.Action.IsFocused;
                Require(_focusPreserved, "Adding another element must preserve button focus.");
                RouteInput(new UiInput(InputKind.KeyDown, Key: UiKey.Enter), window);
                RouteInput(new UiInput(InputKind.KeyUp, Key: UiKey.Enter), window);
                Require(_clicks == 2, "Keyboard activation should work through UiSession.");
                break;
            case 8:
                var before = _gameEvents;
                var point = new UiInput(InputKind.PointerDown, _ui.Root.Bounds.Width - 4, _ui.Root.Bounds.Height - 4);
                Require(!RouteInput(point, window).Handled && _gameEvents == before + 1, "Input outside the HUD must reach the scene.");
                break;
            case 12:
                var removed = _hud.List.Items[^1]; _hud.List.Items.Remove(removed); removed.Dispose();
                break;
            case 18: window.NativeWindow.Size = new global::Silk.NET.Maths.Vector2D<int>(800, 480); break;
            case 24:
                window.NativeWindow.WindowState = WindowState.Minimized;
                _ui.Resize(new UiViewport(0, 0, _scale));
                Require(!_ui.Update(), "Zero-size UI should suspend during minimize.");
                _suspended = true; _restoreAt = _elapsed.Elapsed.TotalSeconds + .25;
                break;
            case 30: _scale = 1.5f; break;
            case 42:
                window.NativeWindow.Size = new global::Silk.NET.Maths.Vector2D<int>(640, 360);
                _scale = 2;
                break;
            case 54:
                var bounds = _hud.Action.Bounds;
                Require(RouteInput(new UiInput(InputKind.PointerDown, bounds.X + 10, bounds.Y + 10), window).PointerCaptured,
                    "Capture should belong to the old session before replacement.");
                break;
            case 55:
                var oldRoot = _ui.Root;
                var oldButton = _hud.Action;
                _ui.Dispose(); Require(oldRoot.IsDisposed && !oldButton.IsPressed, "Replacing a session must dispose its tree and cancel capture.");
                CreateSession(factory, device, window);
                break;
        }
    }

    private void AssertScene(byte[] pixels, UiViewport viewport, int variant)
    {
        var colors = VulkanHostCompositor.SceneColors(variant);
        AssertPixel(pixels, viewport.Width, viewport.Width - 8, viewport.Height - 8, new Color(colors.Right.Red, colors.Right.Green, colors.Right.Blue));
        var bounds = _hud.Panel.Bounds;
        var x = (int)((bounds.X + 5) * viewport.Scale);
        var y = (int)((bounds.Y + 5) * viewport.Scale);
        var background = x < viewport.Width / 2 ? colors.Left : colors.Right;
        static byte Blend(byte front, byte back) => (byte)((front * Overlay.A + back * (255 - Overlay.A) + 127) / 255);
        AssertPixel(pixels, viewport.Width, x, y,
            new Color(Blend(Overlay.R, background.Red), Blend(Overlay.G, background.Green), Blend(Overlay.B, background.Blue)), 1);
        var button = _hud.Action.Bounds;
        AssertPixel(pixels, viewport.Width, (int)((button.X + button.Width - 6) * viewport.Scale),
            (int)((button.Y + button.Height / 2) * viewport.Scale), _hud.Action.Theme.Control, 1);
        var title = _hud.Title.Bounds;
        var glyph = false;
        for (var py = (int)(title.Y * viewport.Scale); py < Math.Min(viewport.Height, (int)((title.Y + title.Height) * viewport.Scale)); py++)
            for (var px = (int)(title.X * viewport.Scale); px < Math.Min(viewport.Width, (int)((title.X + title.Width) * viewport.Scale)); px++)
            {
                var offset = (py * viewport.Width + px) * 4;
                if (pixels[offset] > 210 && pixels[offset + 1] > 210 && pixels[offset + 2] > 210) glyph = true;
            }
        Require(glyph, "Retained HUD title must appear in the GPU-composited scene.");
    }
    private static void AssertPixel(byte[] pixels, int width, int x, int y, Color expected, int tolerance = 0)
    {
        var offset = (y * width + x) * 4;
        Require(Math.Abs(pixels[offset] - expected.R) <= tolerance && Math.Abs(pixels[offset + 1] - expected.G) <= tolerance &&
            Math.Abs(pixels[offset + 2] - expected.B) <= tolerance && pixels[offset + 3] == expected.A,
            $"Unexpected scene pixel ({x}, {y}): {pixels[offset]},{pixels[offset + 1]},{pixels[offset + 2]},{pixels[offset + 3]} expected {expected}.");
    }
    private sealed class HudView
    {
        internal Panel Root { get; } = new();
        internal Border Panel { get; } = new()
        { Width = 250, Height = 230, Margin = new Thickness(12), Padding = new Thickness(12),
            HorizontalAlignment = LayoutAlignment.Start, VerticalAlignment = LayoutAlignment.Start, Background = Overlay };
        internal Text Title { get; } = new("Retained Vulkan HUD");
        internal Text Metrics { get; } = new("Actions: 0");
        internal Button Action { get; } = new("Change scene") { Height = 36 };
        internal VStack List { get; } = new() { Spacing = 4 };
        internal HudView()
        {
            var content = new VStack { Spacing = 8 };
            content.Add(Title).Add(Metrics).Add(Action).Add(List);
            List.Add(new Text("Click scene / HUD button"));
            Panel.SetContent(content); Root.Add(Panel);
        }
    }
}
