using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Silk.NET.Maths;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Argentis;
using ProbeWindow = AlloyVulkanTest.WindowTransparencyProbe.ProbeWindow;

namespace AlloyVulkanTest;

internal static partial class WindowSnapProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput
    { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    { [FieldOffset(0)] internal MouseInput Mouse; [FieldOffset(0)] internal KeyInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeInput
    { internal uint Type; internal InputUnion Data; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { internal uint Size; internal Rectangle Monitor, Work; internal uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private unsafe struct TitleInfo
    { internal uint Size; internal Rectangle Bounds; internal fixed uint State[6]; internal fixed int Rectangles[24]; }
    private sealed record Flyout(string Class, string Process, Rectangle Bounds);
    private sealed record MenuInput(bool Observed, string? Interceptor);
    private static readonly HashSet<ushort> InjectedKeys = [];
    private static bool _injectedButton, _injectedRightButton, _movedPointer;
    internal static unsafe void RunKeyboardControl()
    {
        using var probe = new ProbeWindow(false, false, true, new Vector2D<int>(200, 150), new Vector2D<int>(640, 400), null,
            appearance: new WindowAppearance());
        var hwnd = Hwnd(probe); probe.Window.Focus(); SetForegroundWindow(hwnd); Pump(probe, 150);
        Require(GetForegroundWindow() == hwnd, "Control window not foreground.");
        Console.WriteLine($"Standard physical Alt+Space menu={PhysicalSystemMenu(probe)}.");
        var info = new TitleInfo { Size = 140 }; SendMessageW(hwnd, 0x33f, 0, (nint)(&info));
        GetCursorPos(out var cursor);
        var point = new Point { X = (info.Rectangles[12] + info.Rectangles[14]) / 2, Y = info.Rectangles[15] + 100 };
        Console.WriteLine($"Standard style={(long)GetWindowLongPtrW(hwnd,-16):X}; title={info.State[0]:X}; max rect={info.Rectangles[12]},{info.Rectangles[13]},{info.Rectangles[14]},{info.Rectangles[15]}.");
        try
        {
            Key(0x5b, false); Pump(probe, 100); Key(0x5a, false); Pump(probe, 100); Key(0x5a, true); Key(0x5b, true); Pump(probe, 1500);
            var target = WindowFromPoint(point);
            Console.WriteLine($"Standard Win+Z targets {target} {ClassName(target)} at {point.X},{point.Y}.");
            DesktopCompositionNative.Capture(100, 100, 1100, 600, "TestResults/AlloyTransparency/snap-keyboard-control.png");
            Escape(); MovePointer(Screen(hwnd, new Point { X = 40, Y = 100 })); Pump(probe, 300);
            MovePointer(new Point { X = point.X, Y = (info.Rectangles[13]+info.Rectangles[15])/2 }); Pump(probe, 1800);
            Console.WriteLine($"Standard hover={ClassName(WindowFromPoint(point))}.");
            Escape(); MovePointer(Screen(hwnd, new Point { X = 40, Y = 100 })); Pump(probe, 300);
            Console.WriteLine($"Standard before second Win+Z foreground={GetForegroundWindow()==hwnd}.");
            Key(0x5b, false); Pump(probe, 100); Key(0x5a, false); Pump(probe, 100); Key(0x5a, true); Key(0x5b, true); Pump(probe, 1500);
            Console.WriteLine($"Standard after hover Win+Z={ClassName(WindowFromPoint(point))}.");
            DesktopCompositionNative.Capture(100,100,1100,600,"TestResults/AlloyTransparency/snap-keyboard-control-after-hover.png");
        }
        finally { foreach (var key in InjectedKeys.ToArray()) Key(key, true); Escape(); Pump(probe, 100); SetCursorPos(cursor.X,cursor.Y); _movedPointer=false; }
    }
    internal static void RunBarInput(string[] args)
    {
        Require(Marshal.SizeOf<NativeInput>() == 40, "INPUT ABI mismatch.");
        var index = Array.IndexOf(args, "--snap-output");
        if (index >= 0 && index + 1 == args.Length) throw new ArgumentException("--snap-output requires a JSON path.");
        var path = Path.GetFullPath(index < 0 ? "TestResults/AlloyTransparency/snap-bar.json" : args[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Require(GetCursorPos(out var cursor) != 0, "Cursor unavailable.");
        var results = new List<object>();
        Exception? failure = null;
        try
        {
            foreach (var vulkan in new[] { false, true })
            foreach (var mode in Enum.GetValues<WindowTransparencyMode>())
            {
                using var root = new WindowFrame(new Panel { Height = 44, WindowRegion = WindowRegionRole.Caption }, new Panel
                    { Background = new Color(40, 60, 80, mode == WindowTransparencyMode.PerPixel ? (byte)128 : (byte)255) });
                using var probe = new ProbeWindow(vulkan, mode == WindowTransparencyMode.PerPixel, true,
                    new Vector2D<int>(200, 150), new Vector2D<int>(640, 400), args.Contains("--validation") ? Console.Error.WriteLine : null,
                    appearance: new WindowAppearance { Decorated = false, Transparency = mode, Opacity = mode == WindowTransparencyMode.Opacity ? .5f : 1 },
                    chrome: new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true }, content: root);
                var hwnd = Hwnd(probe); probe.Window.Focus(); SetForegroundWindow(hwnd); Pump(probe, 150);
                var monitor = new MonitorInfo { Size = 40 };
                Require(GetMonitorInfoW(MonitorFromWindow(hwnd, 2), ref monitor) != 0, "Monitor unavailable.");
                var x = (monitor.Monitor.Left + monitor.Monitor.Right) / 2;
                var target = new Point { X = x, Y = monitor.Monitor.Top + 12 };
                var modal = Drag(probe, Screen(hwnd, new Point { X = 40, Y = 20 }), target, 900, () =>
                {
                    target.Y = monitor.Monitor.Top + 60; MovePointer(target); Thread.Sleep(700);
                    if (args.Contains("--capture-snap")) DesktopCompositionNative.Capture(x - 650, monitor.Monitor.Top, 1300, 220,
                        Path.ChangeExtension(path, $"{(vulkan ? "vk" : "gl")}-{mode}.png"));
                });
                Pump(probe, 300);
                var dropped = Bounds(hwnd);
                var workWidth = monitor.Work.Right - monitor.Work.Left; var workHeight = monitor.Work.Bottom - monitor.Work.Top;
                var snapped = new[] { workWidth / 2, workWidth / 3, workWidth * 2 / 3 }.Any(width => Math.Abs(dropped.Right - dropped.Left - width) <= 3)
                    && new[] { workHeight, workHeight / 2 }.Any(height => Math.Abs(dropped.Bottom - dropped.Top - height) <= 3)
                    && dropped.Left >= monitor.Work.Left - 3 && dropped.Right <= monitor.Work.Right + 3
                    && dropped.Top >= monitor.Work.Top - 3 && dropped.Bottom <= monitor.Work.Bottom + 3;
                Escape(); Pump(probe, 100); probe.Dispose();
                var clean = probe.Released && Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0;
                results.Add(new { Vulkan = vulkan, Mode = mode.ToString(), ModalMoveLoop = modal,
                    DroppedBounds = new { dropped.Left, dropped.Top, dropped.Right, dropped.Bottom }, SnapDrop = snapped,
                    VisualConfirmationRequired = true, Cleanup = clean, probe.ValidationErrors, probe.ValidationWarnings });
                Console.WriteLine($"Snap bar {(vulkan ? "Vulkan" : "OpenGL")}/{mode}: modal={modal}, drop={dropped.Left},{dropped.Top},{dropped.Right},{dropped.Bottom}, cleanup={clean}, validation={probe.ValidationErrors}/{probe.ValidationWarnings}.");
                Require(modal && snapped && clean && probe.ValidationErrors == 0 && probe.ValidationWarnings == 0, "Snap bar drop assertion failed.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            if (_injectedButton) MouseButton(4);
            if (_movedPointer) SetCursorPos(cursor.X, cursor.Y);
            _movedPointer = false;
            File.WriteAllText(path, JsonSerializer.Serialize(new { Cases = results, Failure = failure?.Message }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    internal static void RunInput(string[] args)
    {
        Require(Marshal.SizeOf<NativeInput>() == 40, "INPUT ABI mismatch.");
        var outputIndex = Array.IndexOf(args, "--snap-output");
        if (outputIndex >= 0 && outputIndex + 1 == args.Length) throw new ArgumentException("--snap-output requires a JSON path.");
        var path = Path.GetFullPath(outputIndex < 0 ? "TestResults/AlloyTransparency/snap-input.json" : args[outputIndex + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var results = new List<object>();
        var arranging = ReadSetting(0x82); var dock = ReadSetting(0x90); var restoreDrag = ReadSetting(0x8C);
        Require(GetCursorPos(out var oldCursor) != 0, "Cursor unavailable.");
        Exception? failure = null;
        string? altSpaceInterceptor = null;
        try
        {
            foreach (var vulkan in new[] { false, true })
            foreach (var mode in Enum.GetValues<WindowTransparencyMode>())
            {
                var transparent = mode == WindowTransparencyMode.PerPixel;
                var capturePath = Path.ChangeExtension(path, $"{(vulkan ? "vk" : "gl")}-{mode}.png");
                var checks = new Dictionary<string, bool>();
                var flyoutCandidates = new List<object>();
                var skipped = new List<object>();
                ProbeWindow? front = null; var clicks = 0;
                var max = new Button("□") { Width = 40, WindowRegion = WindowRegionRole.Maximize }.OnClick(() =>
                { clicks++; if (front!.Host.State == UiWindowState.Maximized) front.Host.Restore(); else front.Host.Maximize(); });
                var header = new HStack { Height = 44, Padding = new Thickness(8), WindowRegion = WindowRegionRole.Caption };
                header.Add(new Text("Native input") { Width = 180 }); header.Add(max);
                var editor = new TextBox { Width = 180 }; header.Add(editor);
                using var root = new WindowFrame(header, new Panel { Background = new Color(40, 60, 80, transparent ? (byte)128 : (byte)255) });
                using var probe = new ProbeWindow(vulkan, transparent, true, new Vector2D<int>(200, 150), new Vector2D<int>(640, 400),
                    args.Contains("--validation") ? Console.Error.WriteLine : null,
                    appearance: new WindowAppearance { Decorated = false, Transparency = mode, Opacity = mode == WindowTransparencyMode.Opacity ? .5f : 1 },
                    chrome: new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true }, content: root);
                front = probe; probe.Window.Focus(); Pump(probe, 150);
                var hwnd = Hwnd(probe);
                Console.WriteLine($"Native style=0x{(long)GetWindowLongPtrW(hwnd, -16):X}; caption HT={SendMessageW(hwnd, 0x84, 0, Pack(Screen(hwnd, new Point { X = 40, Y = 20 })))}.");
                var foregroundRequest = SetForegroundWindow(hwnd);
                Pump(probe, 150);
                var foreground = GetForegroundWindow();
                GetWindowThreadProcessId(foreground, out var foregroundProcess);
                var processName = ProcessName(foregroundProcess);
                Console.WriteLine($"Foreground request={foregroundRequest}, owned={hwnd}, foreground={foreground}, class={ClassName(foreground)}, process={processName}.");
                checks["foreground_owned"] = foreground == hwnd;
                results.Add(new { Vulkan = vulkan, Mode = mode.ToString(), Dpi = GetDpiForWindow(hwnd), UiScale = probe.Host.Viewport.Scale,
                    Checks = checks, Skipped = skipped, Flyouts = flyoutCandidates });
                Require(foreground == hwnd, "Owned test window is not foreground; real input cannot be verified.");
                Key(0x5b, false); Pump(probe, 100); Key(0x5a, false); Pump(probe, 100); Key(0x5a, true); Key(0x5b, true); Pump(probe, 1500);
                var firstMax = Screen(hwnd, Center(max.Bounds));
                var firstPopup = WindowFromPoint(new Point { X = firstMax.X, Y = firstMax.Y + 100 });
                Console.WriteLine($"Initial Win+Z targets {firstPopup} {ClassName(firstPopup)}.");
                checks["initial_win_z"] = KeyboardFlyout(hwnd, firstMax) != null;
                checks["full_client_area"] = DesktopCompositionNative.HasFullClientArea(probe.Window);
                if (args.Contains("--capture-snap")) DesktopCompositionNative.Capture(100, 100, 1000, 600, Path.ChangeExtension(capturePath, "initial-keyboard.png"));
                Escape(); Pump(probe, 200); SetForegroundWindow(hwnd); Pump(probe, 100);
                var monitor = new MonitorInfo { Size = 40 };
                Require(GetMonitorInfoW(MonitorFromWindow(hwnd, 2), ref monitor) != 0, "Monitor query failed.");
                var dpi = GetDpiForWindow(hwnd);
                void Check(string name, bool passed)
                { checks[name] = passed; Console.WriteLine($"Input {(vulkan ? "Vulkan" : "OpenGL")}/{mode}: {name}={passed}"); }
                var before = Bounds(hwnd);
                var caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                var modal = Drag(probe, caption, new Point { X = caption.X + 80, Y = caption.Y + 70 });
                var moved = Bounds(hwnd);
                Console.WriteLine($"Move modal={modal}, delta={moved.Left-before.Left}/{moved.Top-before.Top}.");
                Check("system_move_loop", modal && Math.Abs(moved.Left - before.Left - 80) <= 2 && Math.Abs(moved.Top - before.Top - 70) <= 2);
                Check("win_z_after_move", CheckKeyboardStage(probe, max));
                var right = ScreenPixels(hwnd, new Point { X = probe.Window.Size.X - 1, Y = probe.Window.Size.Y / 2 });
                before = Bounds(hwnd); modal = Drag(probe, right, new Point { X = right.X + 70, Y = right.Y });
                var resized = Bounds(hwnd);
                Check("system_resize_loop", modal && Math.Abs(resized.Right - before.Right - 70) <= 2 && resized.Left == before.Left);
                Check("win_z_after_resize", CheckKeyboardStage(probe, max));
                before = Bounds(hwnd); var editorPoint = Screen(hwnd, Center(editor.Bounds));
                Drag(probe, editorPoint, new Point { X = editorPoint.X + 30, Y = editorPoint.Y });
                Check("editor_does_not_move_window", Bounds(hwnd).Equals(before) && editor.IsFocused);
                var maxPoint = Screen(hwnd, Center(max.Bounds)); Click(probe, maxPoint); Pump(probe, 200);
                Check("maximize_button_once", clicks == 1 && probe.Host.State == UiWindowState.Maximized);
                var maximized = Bounds(hwnd);
                Check("maximized_work_area", maximized.Left == monitor.Work.Left && maximized.Top == monitor.Work.Top
                    && maximized.Right == monitor.Work.Right && maximized.Bottom == monitor.Work.Bottom);
                if (arranging && restoreDrag)
                {
                    caption = Screen(hwnd, new Point { X = 60, Y = 20 });
                    modal = Drag(probe, caption, new Point { X = caption.X + 180, Y = caption.Y + 220 });
                    Console.WriteLine($"Restore drag modal={modal}, state={probe.Host.State}.");
                    Check("maximized_drag_restore", modal && probe.Host.State == UiWindowState.Normal);
                    Check("win_z_after_drag_restore", CheckKeyboardStage(probe, max));
                }
                else Console.WriteLine("Maximized drag restore not exercised: current Windows setting is disabled.");
                probe.Host.Restore(); probe.Host.Resize(640, 400); probe.Window.Position = new Vector2D<int>(200, 150); Pump(probe, 100);
                caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                Click(probe, caption); Click(probe, caption); Pump(probe, 200);
                Check("caption_double_click_maximize", probe.Host.State == UiWindowState.Maximized && clicks == 1);
                Pump(probe, 600);
                caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                Click(probe, caption); Click(probe, caption); Pump(probe, 200);
                Check("caption_double_click_restore", probe.Host.State == UiWindowState.Normal && clicks == 1);
                probe.Host.Resize(640, 400); probe.Window.Position = new Vector2D<int>(200, 150); Pump(probe, 100);
                if (altSpaceInterceptor == null)
                {
                    var menuInput = PhysicalSystemMenu(probe);
                    if (menuInput.Interceptor == "PowerToys.PowerLauncher") altSpaceInterceptor = menuInput.Interceptor;
                    else Check("alt_space_menu", menuInput.Observed);
                }
                if (altSpaceInterceptor != null) skipped.Add(new { Check = "alt_space_menu", Reason = $"Global shortcut intercepted by {altSpaceInterceptor}; confirmed in standard decorated control." });
                Check("caption_right_click_menu", PhysicalSystemMenu(probe, true).Observed);
                if (arranging && dock)
                {
                    caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                    modal = Drag(probe, caption, new Point { X = monitor.Monitor.Left, Y = monitor.Work.Top + (monitor.Work.Bottom - monitor.Work.Top) / 2 }, 400);
                    Pump(probe, 300); var snapped = Bounds(hwnd); var workWidth = monitor.Work.Right - monitor.Work.Left;
                    Console.WriteLine($"Snap modal={modal}, bounds={snapped.Left},{snapped.Top},{snapped.Right},{snapped.Bottom}, work={monitor.Work.Left},{monitor.Work.Top},{monitor.Work.Right},{monitor.Work.Bottom}.");
                    Check("edge_snap", modal && Math.Abs(snapped.Left - monitor.Work.Left) <= 2 && Math.Abs(snapped.Right - snapped.Left - workWidth / 2) <= 3
                        && Math.Abs(snapped.Top - monitor.Work.Top) <= 2 && Math.Abs(snapped.Bottom - monitor.Work.Bottom) <= 2);
                    probe.Host.Restore(); probe.Host.Resize(640, 400); probe.Window.Position = new Vector2D<int>(200, 150); Pump(probe, 100);
                    foreach (var bottom in new[] { false, true })
                    foreach (var rightSide in new[] { false, true })
                    {
                        var target = new Point { X = rightSide ? monitor.Monitor.Right - 1 : monitor.Monitor.Left,
                            Y = bottom ? monitor.Monitor.Bottom - 1 : monitor.Monitor.Top + 1 };
                        caption = Screen(hwnd, new Point { X = 40, Y = 20 });
                        modal = Drag(probe, caption, target, 400); Pump(probe, 300);
                        snapped = Bounds(hwnd);
                        var halfWidth = (monitor.Work.Right - monitor.Work.Left) / 2;
                        var halfHeight = (monitor.Work.Bottom - monitor.Work.Top) / 2;
                        var left = monitor.Work.Left + (rightSide ? halfWidth : 0);
                        var top = monitor.Work.Top + (bottom ? halfHeight : 0);
                        Console.WriteLine($"Corner {rightSide}/{bottom}: {snapped.Left},{snapped.Top},{snapped.Right},{snapped.Bottom}.");
                        Check($"corner_snap_{(bottom ? "bottom" : "top")}_{(rightSide ? "right" : "left")}", modal
                            && Math.Abs(snapped.Left - left) <= 3 && Math.Abs(snapped.Top - top) <= 3
                            && Math.Abs(snapped.Right - snapped.Left - halfWidth) <= 3 && Math.Abs(snapped.Bottom - snapped.Top - halfHeight) <= 3);
                        Escape(); probe.Host.Restore(); probe.Host.Resize(640, 400);
                        probe.Window.Position = new Vector2D<int>(200, 150); SetForegroundWindow(hwnd); Pump(probe, 150);
                    }
                }
                else Console.WriteLine("Edge Snap not exercised: current Windows arranging/docking setting is disabled.");
                Check("win_z_after_edge_snap", CheckKeyboardStage(probe, max));
                maxPoint = Screen(hwnd, Center(max.Bounds));
                Console.WriteLine($"Maximize point={maxPoint.X},{maxPoint.Y}; HT={SendMessageW(hwnd, 0x84, 0, Pack(maxPoint))}.");
                Console.WriteLine($"After lifecycle native style={(long)GetWindowLongPtrW(hwnd,-16):X}, full client={DesktopCompositionNative.HasFullClientArea(probe.Window)}.");
                MovePointer(maxPoint); Pump(probe, 1800);
                var hover = ShellFlyout(maxPoint);
                RecordFlyout("hover", hover);
                Check("hover_flyout", hover != null);
                Escape();
                var dismissPoint = ScreenPixels(hwnd, new Point { X = probe.Window.Size.X - 30, Y = probe.Window.Size.Y - 30 });
                MovePointer(dismissPoint); Pump(probe, 600);
                Require(WindowFromPoint(dismissPoint) == hwnd, "Hover dismissal point is covered by Shell UI.");
                Require(GetForegroundWindow() == hwnd, "Foreground changed before Win+Z.");
                Key(0x5b, false); Pump(probe, 100); Console.WriteLine($"LWin down={GetAsyncKeyState(0x5b):X}.");
                Key(0x5a, false); Pump(probe, 100); Key(0x5a, true); Pump(probe, 50); Key(0x5b, true); Pump(probe, 1500);
                var keyboard = KeyboardFlyout(hwnd, maxPoint);
                RecordFlyout("keyboard", keyboard);
                Check("win_z_after_hover", keyboard != null); Escape(); Pump(probe, 100);
                void RecordFlyout(string trigger, Flyout? flyout)
                {
                    if (flyout == null) return;
                    var rectangle = flyout.Bounds;
                    flyoutCandidates.Add(new { Trigger = trigger, flyout.Class, flyout.Process,
                        rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom });
                    if (args.Contains("--capture-snap")) DesktopCompositionNative.Capture(rectangle.Left, rectangle.Top,
                        rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, Path.ChangeExtension(capturePath, $"{trigger}-target.png"));
                    if (args.Contains("--capture-snap")) DesktopCompositionNative.Capture(100, 100, 1000, 600,
                        Path.ChangeExtension(capturePath, $"{trigger}.png"));
                }
                probe.Dispose();
                Check("cleanup", probe.Released && Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0);
                Check("validation", probe.ValidationErrors == 0 && probe.ValidationWarnings == 0);
                Console.WriteLine($"DPI={dpi}; hover={hover?.Class}/{hover?.Process}; keyboard={keyboard?.Class}/{keyboard?.Process}; validation={probe.ValidationErrors}/{probe.ValidationWarnings}.");
                Require(checks.Values.All(value => value), "Real input assertion failed; see recorded checks.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            foreach (var key in InjectedKeys.ToArray()) Key(key, true);
            if (_injectedButton) MouseButton(4);
            if (_injectedRightButton) MouseButton(16);
            if (_movedPointer) SetCursorPos(oldCursor.X, oldCursor.Y);
            _movedPointer = false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { Utc = DateTime.UtcNow, OS = Environment.OSVersion.VersionString,
                WindowArranging = arranging, DockMoving = dock, DragFromMaximize = restoreDrag, SettingsUnchanged = ReadSetting(0x82) == arranging && ReadSetting(0x90) == dock && ReadSetting(0x8C) == restoreDrag,
                Cases = results, ChromeHooks = Win32WindowChrome.ActiveHooks, TransparencyHooks = Win32TransparentFramebuffer.ActiveHooks,
                FlyoutDetection = "WindowFromPoint below owned maximize region: Xaml_WindowedPopupClass in ShellHost/ShellExperienceHost/explorer; PNG confirms actual layout UI.",
                Failure = failure?.Message }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        Console.WriteLine($"Real native input checks passed. Report: {path}");
    }
    private static bool Drag(ProbeWindow probe, Point from, Point to, int settle = 80, Action? whileHolding = null)
    {
        var hwnd = Hwnd(probe); Require(GetForegroundWindow() == hwnd && WindowFromPoint(from) == hwnd, "Input start is not the owned window.");
        var modal = false;
        var worker = Task.Run(() =>
        {
            MovePointer(from); Thread.Sleep(50); MouseButton(2);
            try
            {
                Thread.Sleep(60);
                var thread = GetWindowThreadProcessId(hwnd, out _);
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                if (GetGUIThreadInfo(thread, ref info) != 0) modal = (info.Flags & 2) != 0 && info.MoveSize == hwnd;
                for (var i = 1; i <= 8; i++)
                {
                    MovePointer(new Point { X = from.X + (to.X - from.X) * i / 8, Y = from.Y + (to.Y - from.Y) * i / 8 }); Thread.Sleep(30);
                    info.Size = (uint)Marshal.SizeOf<GuiThreadInfo>();
                    if (GetGUIThreadInfo(thread, ref info) != 0) modal |= (info.Flags & 2) != 0 && info.MoveSize == hwnd;
                }
                Thread.Sleep(settle);
                whileHolding?.Invoke(); // Test-only native sampling on worker while the owner is inside system drag loop.
            }
            finally { MouseButton(4); }
        });
        PumpWorker(probe, worker); return modal;
    }
    private static bool CheckKeyboardStage(ProbeWindow probe, Element max)
    {
        var hwnd=Hwnd(probe); MovePointer(Screen(hwnd,new Point { X=40,Y=100 })); Pump(probe,200);
        Require(GetForegroundWindow()==hwnd,"Diagnostic stage window not foreground.");
        Key(0x5b,false); Pump(probe,100); Key(0x5a,false); Pump(probe,100); Key(0x5a,true); Key(0x5b,true); Pump(probe,1000);
        var popup = KeyboardFlyout(hwnd, Screen(hwnd, Center(max.Bounds)));
        Escape(); Pump(probe,200); SetForegroundWindow(hwnd); Pump(probe,50);
        return popup != null;
    }
    private static void Click(ProbeWindow probe, Point point)
    {
        Require(GetForegroundWindow() == Hwnd(probe) && WindowFromPoint(point) == Hwnd(probe), "Click target is not the owned window.");
        var worker = Task.Run(() => { MovePointer(point); Thread.Sleep(40); MouseButton(2); Thread.Sleep(50); MouseButton(4); });
        PumpWorker(probe, worker);
    }
    private static MenuInput PhysicalSystemMenu(ProbeWindow probe, bool rightClick = false)
    {
        var hwnd = Hwnd(probe);
        Require(GetForegroundWindow() == hwnd, "Menu shortcut requires owned foreground window.");
        var observed = false;
        string? interceptor = null;
        var caption = Screen(hwnd, new Point { X = 40, Y = 20 });
        if (rightClick) Require(WindowFromPoint(caption) == hwnd, "Menu caption target is covered.");
        var worker = Task.Run(() =>
        {
            try
            {
                if (rightClick)
                { MovePointer(caption); MouseButton(8); MouseButton(16); }
                else
                { Key(0x12, false); Key(0x20, false); Key(0x20, true); Key(0x12, true); }
                Thread.Sleep(300);
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                observed = GetGUIThreadInfo(GetWindowThreadProcessId(hwnd, out _), ref info) != 0
                    && (info.Flags & 4) != 0 && info.MenuOwner == hwnd;
                Console.WriteLine($"Physical menu flags={info.Flags:X}, owner={info.MenuOwner}, expected={hwnd}.");
                GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundPid);
                if (GetForegroundWindow() != hwnd) interceptor = ProcessName(foregroundPid);
                Console.WriteLine($"After {(rightClick ? "right-click" : "Alt+Space")} foreground={GetForegroundWindow()==hwnd}, interceptor={interceptor}.");
                Escape();
            }
            finally
            {
                if (InjectedKeys.Contains(0x12)) Key(0x12, true);
                if (_injectedRightButton) MouseButton(16);
            }
        });
        PumpWorker(probe, worker);
        return new MenuInput(observed, interceptor);
    }
    private static void PumpWorker(ProbeWindow probe, Task worker)
    {
        var time = Stopwatch.StartNew();
        while (!worker.IsCompleted && time.ElapsedMilliseconds < 7000) { probe.Window.DoEvents(); probe.Render(); Thread.Sleep(5); }
        Require(worker.IsCompleted, "Input worker timed out."); worker.GetAwaiter().GetResult(); Pump(probe, 100);
    }
    private static bool ReadSetting(uint action)
    { Require(SystemParametersInfoW(action, 0, out var value, 0) != 0, "Windows arranging setting query failed."); return value != 0; }
    private static Rectangle Bounds(nint hwnd)
    { Require(GetWindowRect(hwnd, out var rectangle) != 0, "Window rectangle unavailable."); return rectangle; }
    private static void MovePointer(Point point)
    {
        var x = GetSystemMetrics(76); var y = GetSystemMetrics(77); var width = GetSystemMetrics(78); var height = GetSystemMetrics(79);
        Send(new NativeInput { Data = new InputUnion { Mouse = new MouseInput { X = (int)((long)(point.X - x) * 65535 / (width - 1)),
            Y = (int)((long)(point.Y - y) * 65535 / (height - 1)), Flags = 0xC001 } } });
        _movedPointer = true;
    }
    private static void MouseButton(uint flags)
    {
        Send(new NativeInput { Data = new InputUnion { Mouse = new MouseInput { Flags = flags } } });
        if (flags == 2) _injectedButton = true; else if (flags == 4) _injectedButton = false;
        if (flags == 8) _injectedRightButton = true; else if (flags == 16) _injectedRightButton = false;
    }
    private static void Key(ushort key, bool up)
    {
        Send(new NativeInput { Type = 1, Data = new InputUnion { Keyboard = new KeyInput
        { Scan = (ushort)MapVirtualKeyW(key, 0), Flags = 8u | (up ? 2u : 0) | (key is 0x5b or 0x5c ? 1u : 0) } } });
        if (up) InjectedKeys.Remove(key); else InjectedKeys.Add(key);
    }
    private static void Escape() { Key(0x1b, false); Key(0x1b, true); }
    private static unsafe void Send(NativeInput input)
    { Require(SendInput(1, &input, 40) == 1, "SendInput did not insert the event."); }
    private static Flyout? KeyboardFlyout(nint hwnd, Point anchor)
    {
        // Win+Z is aligned to the native window's right edge, independently of our maximize layout.
        // Keep the hover probe tied to the actual custom button; both paths still require a Shell popup.
        return ShellFlyout(anchor) ?? ShellFlyout(new Point { X = Bounds(hwnd).Right - 100, Y = anchor.Y });
    }
    private static Flyout? ShellFlyout(Point anchor)
    {
        var hwnd = WindowFromPoint(new Point { X = anchor.X, Y = anchor.Y + 100 });
        var name = ClassName(hwnd);
        if (name != "Xaml_WindowedPopupClass") return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        var process = ProcessName(pid);
        return process is "ShellHost" or "ShellExperienceHost" or "explorer" ? new Flyout(name, process, Bounds(hwnd)) : null;
    }
    private static string ProcessName(uint pid)
    {
        if (pid == 0) return "none";
        using var process = Process.GetProcessById((int)pid);
        return process.ProcessName;
    }
    private static unsafe string ClassName(nint hwnd)
    { char* name = stackalloc char[256]; var length = GetClassNameW(hwnd, name, 256); return new string(name, 0, length); }
    [LibraryImport("user32.dll", SetLastError = true)] private static unsafe partial uint SendInput(uint count, NativeInput* input, int size);
    [LibraryImport("user32.dll")] private static partial int GetSystemMetrics(int index);
    [LibraryImport("user32.dll")] private static partial uint MapVirtualKeyW(uint code, uint type);
    [LibraryImport("user32.dll")] private static partial short GetAsyncKeyState(int key);
    [LibraryImport("user32.dll")] private static partial int GetCursorPos(out Point point);
    [LibraryImport("user32.dll")] private static partial int SetCursorPos(int x, int y);
    [LibraryImport("user32.dll")] private static partial nint GetForegroundWindow();
    [LibraryImport("user32.dll")] private static partial int SetForegroundWindow(nint hwnd);
    [LibraryImport("user32.dll")] private static partial nint GetWindowLongPtrW(nint hwnd, int index);
    [LibraryImport("user32.dll")] private static partial nint WindowFromPoint(Point point);
    [LibraryImport("user32.dll")] private static partial int GetWindowRect(nint hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(nint hwnd, uint flags);
    [LibraryImport("user32.dll")] private static partial int GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(nint hwnd);
    [LibraryImport("user32.dll")] private static partial int SystemParametersInfoW(uint action, uint parameter, out int value, uint flags);
    [LibraryImport("user32.dll")] private static unsafe partial int GetClassNameW(nint hwnd, char* buffer, int count);
}
