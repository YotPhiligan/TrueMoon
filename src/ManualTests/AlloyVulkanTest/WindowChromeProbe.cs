using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Directed messages to owned test HWNDs; does not move the user's pointer or change Windows settings.
internal static partial class WindowChromeProbe
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
    { internal Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    { internal uint Size; internal Rectangle Monitor, Work; internal uint Flags; }
    internal static void Run()
    {
        Require(Marshal.SizeOf<MinMaxInfo>() == 40 && Marshal.SizeOf<MonitorInfo>() == 40, "Win32 ABI mismatch.");
        var cases = 0; var hits = 0;
        foreach (var openGL in new[] { true, false })
        foreach (var nativeDrag in new[] { false, true })
        foreach (var resizable in new[] { false, true })
        {
            var options = new WindowChromeOptions { NativeDrag = nativeDrag, Resizable = resizable,
                MinimumSize = new Size(300, 180), MaximumSize = new Size(700, 500) };
            using var host = new SilkWindowHost(600, 300, "TrueMoon custom frame contract", openGL,
                new WindowAppearance { Decorated = false }, options);
            var hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
            using var root = new HStack { Height = 40, WindowRegion = WindowRegionRole.Caption };
            root.Add(new Text("Caption") { Width = 100 });
            root.Add(new Button("Button") { Width = 100, IsEnabled = false });
            root.Add(new TextBox { Width = 200 });
            Arrange(root, host); host.SetWindowRegions(WindowRegionMap.Create(root));
            Require(host.SupportsCustomFrame, "Custom frame capability missing.");
            Require(DesktopCompositionNative.HasFullClientArea(host.NativeWindow), "Custom frame must have no system client inset.");
            var outerResult = GetWindowRect(hwnd, out var outer); var clientResult = GetClientRect(hwnd, out var client);
            Require(outerResult != 0 && clientResult != 0, "Rectangle query failed.");
            Require(outer.Right - outer.Left == client.Right && outer.Bottom - outer.Top == client.Bottom, "System frame is still consuming client area.");
            CheckHit(hwnd, 40, 20, nativeDrag ? 2 : 1); hits++;
            var savedPosition = host.NativeWindow.Position;
            host.NativeWindow.Position = new Vector2D<int>(-700, -500);
            Pump(host); Arrange(root, host); host.SetWindowRegions(WindowRegionMap.Create(root));
            CheckHit(hwnd, 40, 20, nativeDrag ? 2 : 1); hits++;
            host.NativeWindow.Position = savedPosition;
            Pump(host); Arrange(root, host); host.SetWindowRegions(WindowRegionMap.Create(root));
            CheckHit(hwnd, 140, 20, 1); CheckHit(hwnd, 250, 20, 1); hits += 2;
            var points = new[] { (0, 0, 13), (599, 0, 14), (0, 299, 16), (599, 299, 17),
                (0, 150, 10), (599, 150, 11), (300, 0, 12), (300, 299, 15) };
            foreach (var (x, y, code) in points)
            {
                var expected = resizable ? code : nativeDrag && WindowRegionMap.Create(root).HitTest(x, y) == WindowRegionRole.Caption ? 2 : 1;
                CheckHit(hwnd, x, y, expected); hits++;
            }
            var limits = ReadLimits(hwnd);
            var scale = new WindowPixelScale(GetDpiForWindow(hwnd));
            Require(limits.MinTrackSize.X == scale.MinimumPixels(300) && limits.MinTrackSize.Y == scale.MinimumPixels(180)
                && limits.MaxTrackSize.X == scale.MaximumPixels(700) && limits.MaxTrackSize.Y == scale.MaximumPixels(500), "Native size limits mismatch.");
            Reject<ArgumentOutOfRangeException>(() => host.Resize(299, 200));
            Reject<ArgumentOutOfRangeException>(() => host.Resize(600, 501));
            host.Resize(700, 500); Pump(host); Require(host.NativeWindow.Size == new Vector2D<int>(scale.RoundPixels(700), scale.RoundPixels(500)), "Exact client resize mismatch.");
            // Snapshot becomes stale after resize; it must not keep the old caption interactive.
            CheckHit(hwnd, 40, 20, 1); hits++;
            Arrange(root, host); host.SetWindowRegions(WindowRegionMap.Create(root));
            CheckHit(hwnd, 40, 20, nativeDrag ? 2 : 1); hits++;
            Task.Run(() => Reject<InvalidOperationException>(host.Minimize)).GetAwaiter().GetResult();
            Task.Run(() => Reject<InvalidOperationException>(host.Dispose)).GetAwaiter().GetResult();
            host.Minimize(); Pump(host); Require(host.State == UiWindowState.Minimized, "Minimize failed.");
            host.Restore(); Pump(host); Require(host.State == UiWindowState.Normal, "Restore failed.");
            if (resizable)
            {
                host.Maximize(); Pump(host); Require(host.State == UiWindowState.Maximized, "Maximize failed.");
                var info = new MonitorInfo { Size = 40 };
                Require(GetMonitorInfoW(MonitorFromWindow(hwnd, 2), ref info) != 0 && GetWindowRect(hwnd, out outer) != 0, "Monitor query failed.");
                Require(outer.Left >= info.Work.Left && outer.Top >= info.Work.Top
                    && outer.Right <= info.Work.Right && outer.Bottom <= info.Work.Bottom, "Maximized window exceeds work area.");
                Require(outer.Right - outer.Left <= scale.MaximumPixels(700) && outer.Bottom - outer.Top <= scale.MaximumPixels(500), "Maximize exceeded application maximum.");
                CheckHit(hwnd, 0, 0, nativeDrag ? 2 : 1); hits++; // No resize border while maximized.
                host.Restore(); Pump(host);
            }
            else Reject<InvalidOperationException>(host.Maximize);
            host.Close(); host.Run(); host.Dispose();
            Reject<ObjectDisposedException>(host.Minimize);
            Require(Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0, "Native hooks retained.");
            cases++;
        }
        // Invalid configurations fail before HWND allocation; no hooks are retained.
        Reject<ArgumentException>(() => new SilkWindowHost(600, 300, "invalid", true, new WindowAppearance(), new WindowChromeOptions()));
        Require(Win32WindowChrome.ActiveHooks == 0, "Rejected configuration retained a hook.");
        Console.WriteLine($"Custom frame native contract passed: {cases} OpenGL/Vulkan windows, {hits} directed hit tests; title/control exclusions, 8 resize codes, stale geometry, size limits, minimize/maximize/work area/restore/close, owner/disposed guards and hooks zero.");
        foreach (var openGL in new[] { true, false })
        {
            using var host = new SilkWindowHost(400, 240, "Unbounded maximize", openGL,
                new WindowAppearance { Decorated = false }, new WindowChromeOptions());
            host.Maximize(); Pump(host);
            var hwnd = host.NativeWindow.Native!.Win32!.Value.Hwnd;
            var info = new MonitorInfo { Size = 40 };
            Require(GetMonitorInfoW(MonitorFromWindow(hwnd, 2), ref info) != 0, "Monitor query failed.");
            Require(GetWindowRect(hwnd, out var outer) != 0, "Window query failed.");
            Require(outer.Left == info.Work.Left && outer.Top == info.Work.Top
                && outer.Right == info.Work.Right && outer.Bottom == info.Work.Bottom,
                $"Unbounded maximize must fill work area exactly: actual={outer.Left},{outer.Top},{outer.Right},{outer.Bottom}; work={info.Work.Left},{info.Work.Top},{info.Work.Right},{info.Work.Bottom}.");
            host.Restore(); host.Close(); host.Run(); host.Dispose();
            Require(Win32WindowChrome.ActiveHooks == 0, "Unbounded window retained hooks.");
        }
        Console.WriteLine("Unbounded maximize passed for OpenGL/Vulkan: exact current-monitor work area, hooks zero.");
        foreach (var openGL in new[] { true, false })
        {
            using var host = new SilkWindowHost(400, 240, "Fractional limits", openGL,
                new WindowAppearance { Decorated = false }, new WindowChromeOptions
                { MinimumSize = new Size(200.01f, 120.01f), MaximumSize = new Size(700.99f, 500.99f) });
            var limits = ReadLimits(host.NativeWindow.Native!.Win32!.Value.Hwnd);
            var scale = new WindowPixelScale(GetDpiForWindow(host.NativeWindow.Native!.Win32!.Value.Hwnd));
            Require(limits.MinTrackSize.X == scale.MinimumPixels(200.01f) && limits.MinTrackSize.Y == scale.MinimumPixels(120.01f)
                && limits.MaxTrackSize.X == scale.MaximumPixels(700.99f) && limits.MaxTrackSize.Y == scale.MaximumPixels(500.99f), "Fractional native limits must round inward.");
            host.Close(); host.Run(); host.Dispose(); Require(Win32WindowChrome.ActiveHooks == 0, "Fractional window retained hooks.");
        }
        Console.WriteLine("Fractional native limits passed for OpenGL/Vulkan: ceil minimum, floor maximum, hooks zero.");
    }
    internal static async Task RunAsync(bool validation)
    {
        Run();
        foreach (var openGL in new[] { true, false })
        {
            var warnings = 0;
            using var sessions = new HostedUiSessionFactory(openGL ? new SkiaOpenGLRenderBackend() : new SkiaVulkanRenderBackend());
            Element? root = null, title = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var options = new SilkUiWindowOptions { Width = 600, Height = 300, OpenGL = openGL,
                Appearance = new WindowAppearance { Decorated = false }, Chrome = new WindowChromeOptions { NativeDrag = true },
                ValidationMessage = validation ? message => { Interlocked.Increment(ref warnings); Console.Error.WriteLine(message); } : null,
                TitleBarFactory = commands => title = new HStack { Height = 40, WindowRegion = WindowRegionRole.Caption }.WithChildren(
                    new Text("Custom frame") { Width = 200 }, new Button("—").OnClick(commands.Minimize),
                    new Button("□").OnClick(commands.Maximize), new Button("×").OnClick(commands.Close)) };
            await using var window = new HostedUiWindow(options, sessions, () => root = new Panel { Background = new Color(30, 40, 50) });
            UiSession? sessionsForInput = null;
            var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.FramePresented += ui =>
            { if (ui.Root is not WindowFrame) throw new InvalidOperationException("Missing composed frame."); sessionsForInput = ui; rendered.TrySetResult(); };
            await window.StartAsync(timeout.Token); await rendered.Task.WaitAsync(timeout.Token);
            Require(window.ChromeCapabilities is { CustomFrame: true, NativeMove: true, NativeResize: true }, "Hosted frame capabilities missing.");
            // Routed title-bar button remains client input and invokes the independent window command.
            await OnOwner(window, host =>
            {
                var button = title!.Children.OfType<Button>().Single(b => b.Value == "□");
                var session = sessionsForInput ?? throw new InvalidOperationException("Missing session.");
                var x = button.Bounds.X + button.Bounds.Width / 2; var y = button.Bounds.Y + button.Bounds.Height / 2;
                session.HandleInput(new UiInput(InputKind.PointerDown, x, y));
                session.HandleInput(new UiInput(InputKind.PointerUp, x, y));
                Require(host.State == UiWindowState.Maximized, "Title button did not maximize.");
            }, timeout.Token);
            window.Restore(); await OnOwner(window, host => Require(host.State == UiWindowState.Normal, "Queued restore failed."), timeout.Token);
            window.Minimize(); await OnOwner(window, host => Require(host.State == UiWindowState.Minimized, "Queued minimize failed."), timeout.Token);
            window.Restore(); window.Resize(650, 350);
            await OnOwner(window, host =>
            {
                var scale = host.Viewport.Scale;
                Require(host.NativeWindow.Size == new Vector2D<int>((int)Math.Round(650 * scale), (int)Math.Round(350 * scale)), "Queued client resize failed.");
            }, timeout.Token);
            window.Close(); await window.Completion.WaitAsync(timeout.Token);
            Require(root!.IsDisposed && title!.IsDisposed && sessions.ActiveSessionCount == 0, "Composed title/content/session retained.");
            Require(warnings == 0 && Win32WindowChrome.ActiveHooks == 0, "Hosting validation/hooks failed.");
            Console.WriteLine($"Hosted {(openGL ? "OpenGL" : "Vulkan")} title button and queued commands passed; disposed tree/registry/hooks, validation zero.");
            var expectedFailure = new InvalidOperationException("Expected title factory failure");
            options.TitleBarFactory = _ => throw expectedFailure;
            Element? failedRoot = null;
            var failedWindow = new HostedUiWindow(options, sessions, () => failedRoot = new Panel());
            try { await failedWindow.StartAsync(timeout.Token); throw new InvalidOperationException("Factory failure was swallowed."); }
            catch (InvalidOperationException error) when (ReferenceEquals(error, expectedFailure)) { }
            try { await failedWindow.Completion.WaitAsync(timeout.Token); throw new InvalidOperationException("Missing completion failure."); }
            catch (InvalidOperationException error) when (ReferenceEquals(error, expectedFailure)) { }
            Require(failedRoot is { IsDisposed: true } && sessions.ActiveSessionCount == 0
                && Win32WindowChrome.ActiveHooks == 0, "Failed title creation retained root/session/hook.");
            Require(warnings == 0, "Title failure cleanup triggered validation.");
            Console.WriteLine($"Hosted {(openGL ? "OpenGL" : "Vulkan")} title-factory failure cleanup passed; original error, disposed root, registry/hooks zero.");
        }
    }
    private static async Task OnOwner(HostedUiWindow window, Action<SilkWindowHost> operation, CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PostWindow(host =>
        {
            try { operation(host); completion.TrySetResult(); }
            catch (Exception error) { completion.TrySetException(error); throw; }
        });
        await completion.Task.WaitAsync(token);
    }
    private static void Arrange(Element root, SilkWindowHost host)
    {
        var size = host.Viewport;
        root.Measure(new Size(size.Width / size.Scale, size.Height / size.Scale), new TextLayout());
        root.Arrange(new Rect(0, 0, size.Width / size.Scale, size.Height / size.Scale));
    }
    private sealed class TextLayout : ITextLayoutService
    { public Size Measure(string text, float fontSize, string fontFamily) => new(text.Length * fontSize / 2, fontSize); }
    private static void Pump(SilkWindowHost host)
    {
        var time = Stopwatch.StartNew();
        while (time.ElapsedMilliseconds < 100) { host.NativeWindow.DoEvents(); host.VerifyWindowAccess(); Thread.Sleep(5); }
    }
    private static void CheckHit(nint hwnd, int x, int y, int expected)
    {
        var scale = new WindowPixelScale(GetDpiForWindow(hwnd)).Scale;
        var point = new Point { X = (int)Math.Round(x * scale), Y = (int)Math.Round(y * scale) };
        Require(ClientToScreen(hwnd, ref point) != 0, "Coordinate conversion failed.");
        var packed = (nint)((uint)(ushort)point.X | (uint)(ushort)point.Y << 16);
        var actual = SendMessageW(hwnd, 0x84, 0, packed);
        Require(actual == expected, $"Native hit test ({x},{y}): expected {expected}, actual {actual}.");
    }
    private static unsafe MinMaxInfo ReadLimits(nint hwnd)
    { var limits = new MinMaxInfo(); SendMessageW(hwnd, 0x24, 0, (nint)(&limits)); return limits; }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);
    [LibraryImport("user32.dll")] private static partial int ClientToScreen(nint hwnd, ref Point point);
    [LibraryImport("user32.dll")] private static partial int GetWindowRect(nint hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial int GetClientRect(nint hwnd, out Rectangle rectangle);
    [LibraryImport("user32.dll")] private static partial nint MonitorFromWindow(nint hwnd, uint flags);
    [LibraryImport("user32.dll")] private static partial int GetMonitorInfoW(nint monitor, ref MonitorInfo info);
    [LibraryImport("user32.dll")] private static partial uint GetDpiForWindow(nint hwnd);
}
