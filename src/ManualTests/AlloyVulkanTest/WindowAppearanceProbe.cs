using System.Diagnostics;
using System.Text.Json;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Platform.Silk;
using Rgb = AlloyVulkanTest.WindowTransparencyProbe.Rgb;
using ProbeWindow = AlloyVulkanTest.WindowTransparencyProbe.ProbeWindow;

namespace AlloyVulkanTest;

internal static class WindowAppearanceProbe
{
    private sealed record Sample(string Phase, int Background, float Opacity, Rgb[] Expected, Rgb[] Desktop, bool Matches);
    private sealed record Case(bool Vulkan, bool Decorated, WindowTransparencyMode Mode, WindowAppearanceCapabilities Capabilities,
        string? SelectedAlpha, string Graphics, int Frames, int ClearCount, int SourceChecks, int InputTargetChecks,
        int ValidationErrors, int ValidationWarnings, Sample[] Samples, bool Released);
    private static readonly Rgb Foreground = new(220, 40, 80);
    private static readonly Rgb[] Backgrounds = [new(40, 100, 180), new(180, 140, 40)];

    internal static void Run(string[] args)
    {
        var index = Array.IndexOf(args, "--appearance-output");
        if (index >= 0 && index + 1 == args.Length) throw new ArgumentException("--appearance-output requires a path.");
        var output = index < 0 ? "TestResults/AlloyTransparency/appearance.json" : args[index + 1];
        var cases = new List<Case>();
        Require(Win32TransparentFramebuffer.ActiveHooks == 0, "Unexpected live hooks before test.");
        using var backdrop = new ProbeWindow(false, false, true, new Vector2D<int>(70, 70), new Vector2D<int>(720, 480), null);
        foreach (var vulkan in new[] { false, true })
        foreach (var decorated in args.Contains("--custom-chrome") ? new[] { false } : new[] { true, false })
        foreach (var mode in Enum.GetValues<WindowTransparencyMode>())
        {
            var appearance = new WindowAppearance { Transparency = mode, Decorated = decorated,
                Opacity = mode == WindowTransparencyMode.Opacity ? .5f : 1 };
            var front = new ProbeWindow(vulkan, mode == WindowTransparencyMode.PerPixel, false,
                new Vector2D<int>(150, 200), new Vector2D<int>(420, 180), args.Contains("--validation") ? Console.Error.WriteLine : null,
                appearance: appearance, chrome: args.Contains("--custom-chrome") ? new WindowChromeOptions
                { NativeDrag = true, NativeSnapLayouts = args.Contains("--native-snap"), MinimumSize = new TrueMoon.Argentis.Size(200, 100), MaximumSize = new TrueMoon.Argentis.Size(800, 600) } : null);
            var samples = new List<Sample>();
            var inputChecks = 0;
            Case result;
            try
            {
                Require(front.Host.Appearance == appearance, "Creation appearance mismatch.");
                Require(args.Contains("--custom-chrome") ? DesktopCompositionNative.HasFullClientArea(front.Window)
                    : DesktopCompositionNative.IsDecorated(front.Window) == decorated, "Native window decoration geometry/style mismatch.");
                Require(front.Host.AppearanceCapabilities.TransparentFramebuffer == (mode == WindowTransparencyMode.PerPixel), "Transparent framebuffer request mismatch.");
                if (mode != WindowTransparencyMode.Opacity)
                    Reject<InvalidOperationException>(() => front.Host.SetOpacity(.5f));
                else Require(Math.Abs(front.Host.Opacity - .5f) < .005f, "Initial native opacity mismatch.");
                Task.Run(() => Reject<InvalidOperationException>(front.Host.VerifyWindowAccess)).GetAwaiter().GetResult();
                Task.Run(() => Reject<InvalidOperationException>(front.Host.Dispose)).GetAwaiter().GetResult();
                foreach (var phase in new[] { "initial", "paint", "resized", "restored" })
                {
                    if (phase == "resized") { front.Host.Resize(480, 220); front.Presenter?.InvalidateSwapchain(); }
                    if (phase == "restored")
                    {
                        front.Window.IsVisible = true; front.Window.WindowState = WindowState.Minimized;
                        Pump(backdrop, front, false);
                        Require(front.Window.WindowState == WindowState.Minimized, "Native minimize not observed.");
                        front.Session.Resize(new UiViewport(0, 0)); Require(!front.Session.Update(), "Zero-size session did not suspend.");
                        if (front.Presenter != null) Require(!front.Presenter.Present(front.Session), "Zero-size Vulkan did not suspend.");
                        front.Window.WindowState = WindowState.Normal; front.Presenter?.InvalidateSwapchain();
                    }
                    for (var background = 0; background < Backgrounds.Length; background++)
                    {
                        front.Window.IsVisible = false; backdrop.Window.Focus(); backdrop.Scene.Set(Backgrounds[background], false);
                        Pump(backdrop, front, false);
                        Require(Desktop(front).All(p => p.Near(Backgrounds[background])), "Known backdrop is obstructed.");
                        front.Scene.Set(Foreground, mode == WindowTransparencyMode.PerPixel);
                        front.Window.IsVisible = true; front.Window.Focus();
                        Pump(backdrop, front, true);
                        if (phase == "paint")
                        {
                            var before = front.Host.TransparencyClearCount;
                            DesktopCompositionNative.Repaint(front.Window);
                            front.Host.VerifyWindowAccess();
                            if (mode == WindowTransparencyMode.PerPixel)
                                Require(front.Host.TransparencyClearCount > before, "WM_PAINT did not initialize redirection.");
                        }
                        foreach (var opacity in mode == WindowTransparencyMode.Opacity ? new[] { 0f, .5f, 1f } : new[] { 1f })
                        {
                            if (mode == WindowTransparencyMode.Opacity) front.Host.SetOpacity(opacity);
                            Pump(backdrop, front, true); front.VerifySource(mode == WindowTransparencyMode.PerPixel);
                            var expected = Enumerable.Range(0, 3).Select(band => Foreground.Over(Backgrounds[background],
                                mode == WindowTransparencyMode.PerPixel ? new[] { 0f, 128f / 255, 1f }[band] : opacity)).ToArray();
                            var desktop = Desktop(front);
                            var matches = expected.Zip(desktop).All(p => p.First.Near(p.Second));
                            samples.Add(new Sample(phase, background, opacity, expected, desktop, matches));
                            Require(matches, $"Public {mode} desktop alpha mismatch ({vulkan}, {decorated}, {phase}, {background}, {opacity}); " +
                                $"desktop={string.Join(';', desktop.Select(p => p.ToString()))}; expected={string.Join(';', expected.Select(p => p.ToString()))}; " +
                                $"nativeTarget={DesktopCompositionNative.ClientHitTargetsWindow(front.Window)}, " +
                                $"nativeBackgroundTarget={DesktopCompositionNative.ClientHitTargetsWindow(front.Window, backdrop.Window)}, " +
                                $"visible={front.Window.IsVisible}/{backdrop.Window.IsVisible}, nativeOpacity={front.Host.Opacity}.");
                            if (mode == WindowTransparencyMode.PerPixel)
                            {
                                Require(DesktopCompositionNative.ClientHitTargetsWindow(front.Window), "Per-pixel client input unexpectedly passes through.");
                                inputChecks += 3;
                            }
                        }
                    }
                }
                if (mode == WindowTransparencyMode.PerPixel)
                {
                    var clears = front.Host.TransparencyClearCount;
                    // Static frames must reuse the UI and must not cause a native clear on every presentation.
                    for (var frame = 0; frame < 10; frame++) front.Render();
                    Require(front.Host.TransparencyClearCount == clears, "Redirection was cleared during ordinary frame presentation.");
                }
                result = new Case(vulkan, decorated, mode, front.Host.AppearanceCapabilities, front.Presenter?.SelectedCompositeAlpha.ToString(),
                    front.Graphics, front.Frames, front.Host.TransparencyClearCount, front.SourceChecks, inputChecks, 0, 0, samples.ToArray(), false);
            }
            finally { front.Dispose(); }
            Require(Win32TransparentFramebuffer.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0,
                "Native hook/root retained after window teardown.");
            Reject<ObjectDisposedException>(front.Host.VerifyWindowAccess);
            result = result with { Released = front.Released, ValidationErrors = front.ValidationErrors, ValidationWarnings = front.ValidationWarnings }; cases.Add(result);
            Console.WriteLine($"Public {(vulkan ? "Vulkan" : "OpenGL")}/{mode}, decorated={decorated}: {samples.Count}/{samples.Count} samples, hooks=0, clears={result.ClearCount}.");
        }
        backdrop.Dispose();
        var path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { Utc = DateTime.UtcNow, OS = Environment.OSVersion.VersionString,
            Runtime = Environment.Version.ToString(), Silk = "2.23.0", DesktopTolerance = 3, Validation = args.Contains("--validation"),
            CustomChrome = args.Contains("--custom-chrome"), NativeSnapLayouts = args.Contains("--native-snap"), Cases = cases, Hooks = Win32TransparentFramebuffer.ActiveHooks,
            ChromeHooks = Win32WindowChrome.ActiveHooks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Public appearance matrix saved: {path}; validation/source/desktop/lifecycle passed.");
    }
    private static Rgb[] Desktop(ProbeWindow front) => Enumerable.Range(0, 3).Select(band =>
        DesktopCompositionNative.Pixel(front.Window, front.Window.Size.X * (band * 2 + 1) / 6, front.Window.Size.Y / 2)).ToArray();
    private static void Pump(ProbeWindow background, ProbeWindow front, bool renderFront)
    {
        var timer = Stopwatch.StartNew(); do
        {
            background.Window.DoEvents(); front.Window.DoEvents(); front.Host.VerifyWindowAccess();
            background.Render(); if (renderFront) front.Render(); Thread.Sleep(10);
        } while (timer.ElapsedMilliseconds < 300);
        DesktopCompositionNative.Flush();
    }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
