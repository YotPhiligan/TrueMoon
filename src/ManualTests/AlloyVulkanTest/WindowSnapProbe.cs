using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Argentis;
using ProbeWindow = AlloyVulkanTest.WindowTransparencyProbe.ProbeWindow;

namespace AlloyVulkanTest;

internal static partial class WindowSnapProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiThreadInfo
    { internal uint Size, Flags; internal nint Active, Focus, Capture, MenuOwner, MoveSize, Caret; internal Rectangle CaretRect; }
    internal static void Run(bool validation)
    {
        foreach (var vulkan in new[] { false, true })
        foreach (var snap in new[] { false, true })
        foreach (var menu in new[] { false, true })
        {
            ProbeWindow? front = null; var clicks = 0;
            var maximize = new Button("□") { Width = 40, WindowRegion = WindowRegionRole.Maximize }.OnClick(() =>
            {
                clicks++;
                if (front!.Host.State == UiWindowState.Maximized) front.Host.Restore(); else front.Host.Maximize();
            });
            var header = new HStack { Height = 44, Padding = new Thickness(8), WindowRegion = WindowRegionRole.Caption };
            header.Add(new Text("Native caption") { Width = 180 }); header.Add(maximize);
            var editor = new TextBox { Width = 180 }; header.Add(editor);
            using var root = new WindowFrame(header, new Panel { Background = new Color(40, 60, 80) });
            using var probe = new ProbeWindow(vulkan, false, true, new Vector2D<int>(200, 150), new Vector2D<int>(640, 400),
                validation ? Console.Error.WriteLine : null, appearance: new WindowAppearance { Decorated = false },
                chrome: new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = snap, SystemMenu = menu }, content: root);
            front = probe; Pump(probe);
            var hwnd = Hwnd(probe); var maxPoint = Center(maximize.Bounds); var screen = Screen(hwnd, maxPoint);
            Require(SendMessageW(hwnd, 0x84, 0, Pack(screen)) == (snap ? 9 : 1), "Maximize hit test mismatch.");
            if (snap)
            {
                SendMessageW(hwnd, 0xA0, 9, Pack(screen)); probe.Host.DispatchPendingWindowInput();
                Require(maximize.IsHovered, "Native hover did not reach UI outside the callback.");
                SendMessageW(hwnd, 0x2A2, 0, 0); probe.Host.DispatchPendingWindowInput(); Require(!maximize.IsHovered, "Hover was not cleared.");
                SendMessageW(hwnd, 0xA1, 9, Pack(screen)); Require(!maximize.IsPressed, "UI was invoked from native callback.");
                probe.Host.DispatchPendingWindowInput(); Require(maximize.IsPressed && GetCapture() == hwnd, "Native press/capture missing.");
                SendMessageW(hwnd, 0x200, 1, Pack(new Point { X = 40, Y = 80 }));
                SendMessageW(hwnd, 0x202, 0, Pack(new Point { X = 40, Y = 80 })); probe.Host.DispatchPendingWindowInput();
                Require(!maximize.IsPressed && clicks == 0 && GetCapture() == 0, "Release outside activated or retained capture.");
                SendMessageW(hwnd, 0xA1, 9, Pack(screen)); probe.Host.DispatchPendingWindowInput();
                SendMessageW(hwnd, 0x202, 0, Pack(Pixels(hwnd, maxPoint))); probe.Host.DispatchPendingWindowInput(); Pump(probe);
                Require(clicks == 1 && probe.Host.State == UiWindowState.Maximized, "Exactly-once native button click failed.");
                probe.Host.Restore(); Pump(probe); screen = Screen(hwnd, maxPoint);
                SendMessageW(hwnd, 0xA1, 9, Pack(screen)); probe.Host.DispatchPendingWindowInput();
                SendMessageW(hwnd, 0x1F, 0, 0); probe.Host.DispatchPendingWindowInput();
                Require(!maximize.IsPressed && clicks == 1 && GetCapture() == 0, "Cancelled press retained activation/capture.");
                maximize.IsEnabled = false; Pump(probe);
                Require(SendMessageW(hwnd, 0x84, 0, Pack(screen)) == 1, "Disabled maximize region remains native.");
                maximize.IsEnabled = true; Pump(probe);
            }
            // Non-client double-click delegates to the system instead of simulating maximize in UI.
            var caption = Screen(hwnd, new Point { X = 40, Y = 20 });
            SendMessageW(hwnd, 0xA3, 2, Pack(caption)); Pump(probe);
            Require(probe.Host.State == UiWindowState.Maximized, "Caption double-click did not maximize.");
            caption = Screen(hwnd, new Point { X = 40, Y = 20 });
            SendMessageW(hwnd, 0xA3, 2, Pack(caption)); Pump(probe);
            Require(probe.Host.State == UiWindowState.Normal, "Caption double-click did not restore.");
            if (menu)
            {
                VerifyMenu(probe, () => probe.Host.ShowSystemMenu(40, 20));
                VerifyMenu(probe, () => SendMessageW(hwnd, 0x112, 0xf100, 32));
                VerifyMenu(probe, () => SendMessageW(hwnd, 0x104, 32, 0x20000000));
                caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                VerifyMenu(probe, () => SendMessageW(hwnd, 0xA5, 2, Pack(caption)));
            }
            else
            {
                try { probe.Host.ShowSystemMenu(40, 20); throw new InvalidOperationException("Disabled system menu was accepted."); }
                catch (NotSupportedException) { }
                SendMessageW(hwnd, 0x112, 0xf100, 32);
                SendMessageW(hwnd, 0x104, 32, 0x20000000);
                SendMessageW(hwnd, 0xA5, 2, Pack(Screen(hwnd, new Point { X = 40, Y = 20 })));
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                Require(GetGUIThreadInfo(GetWindowThreadProcessId(hwnd, out _), ref info) != 0 && (info.Flags & 4) == 0,
                    "Disabled system menu entered its modal loop.");
            }
            SendMessageW(hwnd, 0x104, 32, 0x60000000); // Alt+Space held-key repeat must not reopen the menu.
            var expectedClicks = clicks;
            probe.Session.HandleInput(new UiInput(InputKind.PointerDown, maxPoint.X, maxPoint.Y));
            probe.Session.HandleInput(new UiInput(InputKind.PointerUp, maxPoint.X, maxPoint.Y));
            probe.Host.Restore(); Pump(probe); expectedClicks++;
            foreach (var key in new[] { UiKey.Space, UiKey.Enter })
            {
                probe.Session.HandleInput(new UiInput(InputKind.KeyDown, Key: key));
                Require(clicks == expectedClicks, "Keyboard down activated caption button prematurely.");
                probe.Session.HandleInput(new UiInput(InputKind.KeyUp, Key: key)); expectedClicks++;
                Require(clicks == expectedClicks && probe.Host.State == UiWindowState.Maximized, "Caption keyboard click was lost or duplicated.");
                probe.Host.Restore(); Pump(probe);
            }
            if (snap)
            {
                screen = Screen(hwnd, maxPoint); SendMessageW(hwnd, 0xA1, 9, Pack(screen)); probe.Host.DispatchPendingWindowInput();
                Require(GetCapture() == hwnd, "Expected capture before teardown.");
            }
            probe.Dispose(); Require(GetCapture() != hwnd && Win32WindowChrome.ActiveHooks == 0, "Press teardown retained capture/hook.");
            Console.WriteLine($"Native caption {(vulkan ? "Vulkan" : "OpenGL")}, Snap={snap}, Menu={menu}: hit/hover/click/cancel/disabled/keyboard/menu/double-click/lifetime passed; validation0/0.");
        }
    }
    private static void VerifyMenu(ProbeWindow probe, Action open)
    {
        var hwnd = Hwnd(probe); var thread = GetWindowThreadProcessId(hwnd, out _);
        var seen = false;
        var task = Task.Run(() =>
        {
            var time = Stopwatch.StartNew();
            while (time.ElapsedMilliseconds < 3000)
            {
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                if (GetGUIThreadInfo(thread, ref info) != 0 && (info.Flags & 4) != 0 && info.MenuOwner == hwnd)
                { seen = true; PostMessageW(hwnd, 0x100, 0x1b, 0); break; }
                Thread.Sleep(10);
            }
            PostMessageW(hwnd, 0x1F, 0, 0); // bounded modal-loop cleanup even on failure
        });
        open(); Require(task.Wait(TimeSpan.FromSeconds(5)), "Menu cancellation worker timed out.");
        probe.Host.VerifyWindowAccess(); Require(seen, "Owned native system menu was not observed."); Pump(probe);
    }
    private static void Pump(ProbeWindow probe, int milliseconds = 50)
    {
        var time = Stopwatch.StartNew();
        while (time.ElapsedMilliseconds < milliseconds)
        { probe.Window.DoEvents(); probe.Render(); Thread.Sleep(5); }
    }
    private static nint Hwnd(ProbeWindow probe) => probe.Window.Native!.Win32!.Value.Hwnd;
    private static Point Center(Rect bounds) => new() { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + bounds.Height / 2) };
    private static Point Screen(nint hwnd, Point point)
    { return ScreenPixels(hwnd, Pixels(hwnd, point)); }
    private static Point Pixels(nint hwnd, Point point)
    {
        var scale = new WindowPixelScale(GetDpiForWindow(hwnd)).Scale;
        return new Point { X = (int)Math.Round(point.X * scale), Y = (int)Math.Round(point.Y * scale) };
    }
    private static Point ScreenPixels(nint hwnd, Point point)
    { Require(ClientToScreen(hwnd, ref point) != 0, "Client coordinate conversion failed."); return point; }
    private static nint Pack(Point point) => (nint)((uint)(ushort)point.X | (uint)(ushort)point.Y << 16);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial int PostMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial int ClientToScreen(nint hwnd, ref Point point);
    [LibraryImport("user32.dll")] private static partial nint GetCapture();
    [LibraryImport("user32.dll")] private static partial uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [LibraryImport("user32.dll")] private static partial int GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
}
