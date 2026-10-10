using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Silk.NET.Maths;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Argentis;
using ProbeWindow = AlloyVulkanTest.WindowTransparencyProbe.ProbeWindow;

namespace AlloyVulkanTest;

// Directed HWND geometry checks; physical DPI comes only from actual monitor placement.
internal static partial class WindowDpiProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { internal int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { internal uint Size; internal Rectangle Monitor, Work; internal uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
    { internal Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int MonitorCallback(nint monitor, nint dc, nint rectangle, nint data);
    private sealed record Sample(bool Vulkan, WindowTransparencyMode Mode, bool Custom, int Monitor, uint Dpi,
        UiViewport Viewport, float LogicalWidth, float LogicalHeight, int DirectedScaleRequests, bool RetainedInputState);
    internal static unsafe void Run(string[] args)
    {
        var monitors = new List<MonitorInfo>();
        MonitorCallback callback = (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfoW(monitor, ref info) == 0) return 0;
            monitors.Add(info); return 1;
        };
        Require(EnumDisplayMonitors(0, 0, callback, 0) != 0 && monitors.Count > 0, "Monitor enumeration failed.");
        GC.KeepAlive(callback);
        VerifyThreadAwareness();
        var samples = new List<Sample>();
        var actualDpis = new HashSet<uint>();
        var transitions = 0;
        foreach (var vulkan in new[] { false, true })
        foreach (var mode in Enum.GetValues<WindowTransparencyMode>())
        foreach (var custom in new[] { false, true })
        {
            var clicks = 0;
            var button = new Button("Action") { Width = 100, Height = 36 }.OnClick(() => clicks++);
            var editor = new TextBox { Width = 180, Height = 36, Value = "DPI selection" };
            var header = new HStack { Height = 44, WindowRegion = WindowRegionRole.Caption };
            header.Add(new Text("Caption") { Width = 160 }); header.Add(button);
            using var root = new WindowFrame(header, new VStack().WithChildren(editor));
            using var window = new ProbeWindow(vulkan, mode == WindowTransparencyMode.PerPixel, true,
                new Vector2D<int>(monitors[0].Work.Left + 100, monitors[0].Work.Top + 100), new Vector2D<int>(600, 400),
                args.Contains("--validation") ? Console.Error.WriteLine : null,
                appearance: new WindowAppearance { Decorated = !custom, Transparency = mode,
                    Opacity = mode == WindowTransparencyMode.Opacity ? .5f : 1 },
                chrome: custom ? new WindowChromeOptions { NativeDrag = true, MinimumSize = new Size(300.01f, 180.01f),
                    MaximumSize = new Size(900.99f, 700.99f) } : null, content: root);
            Pump(window);
            var hwnd = window.Window.Native!.Win32!.Value.Hwnd;
            VerifySize(window, hwnd, 600, 400);
            SelectEditor(window.Session, editor);
            uint previous = GetDpiForWindow(hwnd);
            foreach (var pair in monitors.Select((monitor, index) => (monitor, index)).Concat(
                monitors.Count > 1 ? new[] { (monitors[0], 0) } : Array.Empty<(MonitorInfo, int)>()))
            {
                var (monitor, index) = pair;
                window.Window.Position = new Vector2D<int>(monitor.Work.Left + 100, monitor.Work.Top + 100);
                Pump(window);
                var dpi = GetDpiForWindow(hwnd);
                actualDpis.Add(dpi);
                if (dpi != previous) transitions++;
                previous = dpi;
                var native = new WindowPixelScale(dpi);
                Require(window.Host.Viewport.Scale == native.Scale && window.Session.Viewport == window.Host.Viewport,
                    "Native/session viewport scale differs.");
                Require(editor.IsFocused && editor.SelectionStart == 1 && editor.SelectionLength == 3
                    && ReferenceEquals(root.Children[1].Children[0], editor), "DPI move replaced controls or lost input state.");
                var retained = editor.IsFocused;
                // Target-DPI size queries exercise the callback; they do not simulate an OS DPI transition.
                var requests = 0;
                foreach (var targetDpi in new[] { 96u, 144u, 192u })
                {
                    var size = new NativeSize();
                    Require(SendMessageW(hwnd, 0x2E4, targetDpi, (nint)(&size)) == 1, "Scaled-size hook did not answer.");
                    var expected = new WindowPixelScale(targetDpi);
                    var client = new Rectangle { Right = expected.RoundPixels(native.ToLogical(window.Host.Viewport.Width)),
                        Bottom = expected.RoundPixels(native.ToLogical(window.Host.Viewport.Height)) };
                    if (!custom)
                    {
                        Require(AdjustWindowRectExForDpi(ref client, unchecked((uint)GetWindowLongPtrW(hwnd, -16)),
                            0, unchecked((uint)GetWindowLongPtrW(hwnd, -20)), targetDpi) != 0, "Frame adjustment failed.");
                    }
                    Require(size.Width == client.Right - client.Left && size.Height == client.Bottom - client.Top,
                        "Target-DPI client/frame size mismatch.");
                    requests++;
                }
                if (custom)
                {
                    var limits = new MinMaxInfo();
                    SendMessageW(hwnd, 0x24, 0, (nint)(&limits));
                    Require(limits.MinTrackSize.X == native.MinimumPixels(300.01f) && limits.MinTrackSize.Y == native.MinimumPixels(180.01f)
                        && limits.MaxTrackSize.X == native.MaximumPixels(900.99f) && limits.MaxTrackSize.Y == native.MaximumPixels(700.99f),
                        "DPI-scaled native limits mismatch.");
                    var point = new Point { X = native.RoundPixels(40), Y = native.RoundPixels(20) };
                    Require(ClientToScreen(hwnd, ref point) != 0, "Screen conversion failed.");
                    Require(SendMessageW(hwnd, 0x84, 0, Pack(point)) == 2, "Caption map is not scaled.");
                }
                window.Host.Resize(640, 420); Pump(window); VerifySize(window, hwnd, 640, 420);
                Require(editor.IsFocused && editor.SelectionStart == 1 && editor.SelectionLength == 3,
                    "Logical resize lost focus/selection.");
                // Real GLFW mouse position plus directed messages to our own client window.
                var buttonPoint = new Point { X = native.RoundPixels(button.Bounds.X + button.Bounds.Width / 2),
                    Y = native.RoundPixels(button.Bounds.Y + button.Bounds.Height / 2) };
                Require(GetCursorPos(out var oldCursor) != 0 && ClientToScreen(hwnd, ref buttonPoint) != 0, "Pointer query failed.");
                try
                {
                    Require(SetCursorPos(buttonPoint.X, buttonPoint.Y) != 0, "Could not place pointer on owned button.");
                    Pump(window);
                    var clientPoint = new Point { X = native.RoundPixels(button.Bounds.X + button.Bounds.Width / 2),
                        Y = native.RoundPixels(button.Bounds.Y + button.Bounds.Height / 2) };
                    var before = clicks;
                    SendMessageW(hwnd, 0x200, 0, Pack(clientPoint));
                    SendMessageW(hwnd, 0x201, 1, Pack(clientPoint));
                    SendMessageW(hwnd, 0x202, 0, Pack(clientPoint));
                    Pump(window);
                    Require(clicks == before + 1, "Native client input did not activate the logical button exactly once.");
                }
                finally { SetCursorPos(oldCursor.X, oldCursor.Y); }
                SelectEditor(window.Session, editor);
                window.Host.Resize(600, 400); Pump(window); VerifySize(window, hwnd, 600, 400);
                var viewport = window.Host.Viewport;
                samples.Add(new(vulkan, mode, custom, index, dpi, viewport, viewport.Width / viewport.Scale,
                    viewport.Height / viewport.Scale, requests, retained));
            }
            window.Host.Minimize(); Pump(window); window.Host.Restore(); Pump(window);
            window.Dispose();
            Require(window.Released && Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0
                && Win32TransparentFramebuffer.ActiveHooks == 0, "DPI/chrome/alpha hooks retained.");
            Require(window.ValidationErrors == 0 && window.ValidationWarnings == 0, "Validation failed including teardown.");
            Console.WriteLine($"DPI {(vulkan ? "Vulkan" : "OpenGL")}/{mode}/custom={custom}: native scale, logical resize/input, limits, monitor placement, retained focus and hook cleanup passed.");
        }
        var indexOutput = Array.IndexOf(args, "--dpi-output");
        if (indexOutput >= 0 && indexOutput + 1 == args.Length) throw new ArgumentException("--dpi-output requires a path.");
        var path = Path.GetFullPath(indexOutput >= 0 ? args[indexOutput + 1] : "TestResults/AlloyDpi/dpi.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Utc = DateTime.UtcNow, MonitorCount = monitors.Count, ActualDpis = actualDpis.Order().ToArray(),
            ActualDpiTransitions = transitions, DirectedScaleRequests = samples.Sum(s => s.DirectedScaleRequests),
            UnavailableRequiredDpis = new[] { 96u, 144u, 192u }.Except(actualDpis).ToArray(),
            Samples = samples, DpiHooks = Win32WindowDpi.ActiveHooks,
            Note = "WM_GETDPISCALEDSIZE requests are directed contract checks, not physical DPI changes. Windows settings unchanged."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"DPI report: {path}; monitors={monitors.Count}, actual DPI={string.Join(',', actualDpis.Order())}, physical transitions={transitions}.");
    }
    private static void VerifySize(ProbeWindow window, nint hwnd, int width, int height)
    {
        var scale = new WindowPixelScale(GetDpiForWindow(hwnd));
        Require(window.Host.Viewport == new UiViewport(scale.RoundPixels(width), scale.RoundPixels(height), scale.Scale),
            $"Logical resize mismatch: {window.Host.Viewport}, expected logical {width}x{height}.");
    }
    private static void Pump(ProbeWindow window)
    {
        var watch = Stopwatch.StartNew();
        do { window.Window.DoEvents(); window.Render(); Thread.Sleep(5); } while (watch.ElapsedMilliseconds < 60);
    }
    private static void SelectEditor(UiSession session, TextBox editor)
    {
        session.Focus(editor);
        session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Home));
        session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right));
        for (var i = 0; i < 3; i++) session.HandleInput(new UiInput(InputKind.KeyDown, Key: UiKey.Right, Shift: true));
    }
    private static void VerifyThreadAwareness()
    {
        var previous = SetThreadDpiAwarenessContext(-1); // Test caller is DPI-unaware; process settings are untouched.
        Require(previous != 0, "Could not set the test thread's DPI context.");
        try
        {
            using var host = new SilkWindowHost(400, 240, "DPI caller context", true);
            var hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
            Require(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(hwnd), -4) != 0,
                "Created window did not inherit per-monitor-v2 awareness.");
            Require(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), -1) != 0, "Creation changed caller awareness.");
            host.Resize(500, 300);
            var viewport = host.Viewport;
            Require(viewport.Width == (int)Math.Round(500 * viewport.Scale) && viewport.Height == (int)Math.Round(300 * viewport.Scale),
                "Unaware caller received virtualized dimensions.");
            Require(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), -1) != 0, "Resize/viewport changed caller awareness.");
            host.Close(); host.Run(); host.Dispose();
            Require(AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), -1) != 0 && Win32WindowDpi.ActiveHooks == 0,
                "Run/disposal changed caller awareness or retained DPI hooks.");
        }
        finally { Require(SetThreadDpiAwarenessContext(previous) != 0, "Could not restore test DPI context."); }
        Console.WriteLine("DPI-unaware caller: per-monitor-v2 HWND, exact resize/viewport, restored thread context and hooks0 passed.");
    }
    private static nint Pack(Point point) => (nint)((uint)(ushort)point.X | (uint)(ushort)point.Y << 16);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [LibraryImport("user32.dll")] private static partial int EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [LibraryImport("user32.dll")] private static partial int GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(nint hwnd);
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial nint GetWindowLongPtrW(nint hwnd, int index);
    [LibraryImport("user32.dll")] private static partial int AdjustWindowRectExForDpi(ref Rectangle rectangle, uint style, int menu, uint extended, uint dpi);
    [LibraryImport("user32.dll")] private static partial int ClientToScreen(nint hwnd, ref Point point);
    [LibraryImport("user32.dll")] private static partial int GetCursorPos(out Point point);
    [LibraryImport("user32.dll")] private static partial int SetCursorPos(int x, int y);
    [LibraryImport("user32.dll")] private static partial nint SetThreadDpiAwarenessContext(nint context);
    [LibraryImport("user32.dll")] private static partial nint GetThreadDpiAwarenessContext();
    [LibraryImport("user32.dll")] private static partial nint GetWindowDpiAwarenessContext(nint hwnd);
    [LibraryImport("user32.dll")] private static partial int AreDpiAwarenessContextsEqual(nint first, nint second);
}
