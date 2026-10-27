using TrueMoon.Aether.Editor;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.ManualTests.AetherEditorHost;

// Directed HWND notifications only; never calls SetFocus, moves the cursor or changes OS personalization.
internal static partial class EditorBorderSmoke
{
    internal static async Task VerifyAsync(string directory, HostedUiWindow window, UiSession ui, EditorView view, CancellationToken token)
    {
        var frame = (WindowFrame)ui.Root;
        Color? accent = null; var originalActive = false; var settingsSignal = false;
        var captures = new List<string>(); var viewport = view.Canvas.Bounds;
        await OnWindow(host =>
        {
            originalActive = host.IsFocused;
            accent = ((IWindowSystemColors)host).AccentColor;
            Require(accent != null, "Windows UISettings did not expose an accent on this supported desktop.");
            host.DispatchPendingWindowInput();
            var requests = 0;
            void Requested() => requests++;
            host.RenderRequested += Requested;
            try
            {
                SendMessageW(Handle(host), 0x31A, 0, 0); // WM_THEMECHANGED: reread UISettings, never trust a message color payload.
                var beforeDrain = requests;
                host.DispatchPendingWindowInput();
                settingsSignal = requests == beforeDrain + 1 && ((IWindowSystemColors)host).AccentColor == accent;
            }
            finally { host.RenderRequested -= Requested; }
            Require(settingsSignal, "System color notification did not refresh the cache and request presentation.");
            SendMessageW(Handle(host), 7, 0, 0); // WM_SETFOCUS notification to this owned HWND only.
        });
        try
        {
            await Until(() => frame.DefaultTitleBar!.State.Active && !ui.NeedsUpdate);
            Require(frame.UseSystemAccentBorder && frame.SystemAccentColor == accent && frame.BorderColor == accent,
                "Active frame does not use the host accent.");
            var active = frame.BorderColor;
            await Capture("border-active.png", active);
            await OnWindow(host => SendMessageW(Handle(host), 8, 0, 0)); // WM_KILLFOCUS
            await Until(() => !frame.DefaultTitleBar!.State.Active && !ui.NeedsUpdate);
            var inactive = frame.BorderColor;
            var expected = new Color((byte)MathF.Round(active.R * .6f), (byte)MathF.Round(active.G * .6f), (byte)MathF.Round(active.B * .6f), active.A);
            Require(inactive == expected && view.Canvas.Bounds == viewport && frame.BorderWidth == 1 && frame.Opacity == 1,
                "Inactive frame did not dim only its border, or changed scene geometry.");
            await Capture("border-inactive.png", inactive);
            File.WriteAllText(Path.Combine(directory, "border.json"), JsonSerializer.Serialize(new
            {
                Passed = true, AccentRead = true, SettingsNotification = settingsSignal, FocusDimming = true,
                StableViewport = true, NativeBorderPixels = true, Active = active, Inactive = inactive, Captures = captures
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await OnWindow(host => SendMessageW(Handle(host), originalActive ? 7u : 8u, 0, 0)); }
        await Until(() => frame.DefaultTitleBar!.State.Active == originalActive && !ui.NeedsUpdate);

        Task OnWindow(Action<TrueMoon.Alloy.Platform.Silk.SilkWindowHost> action)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(host =>
            {
                try { action(host); done.TrySetResult(); }
                catch (Exception error) { done.TrySetException(error); }
            });
            return done.Task.WaitAsync(token);
        }
        async Task Until(Func<bool> predicate)
        {
            while (true)
            {
                var ready = false; await ui.InvokeAsync(() => ready = predicate()).WaitAsync(token);
                if (ready) return; await Task.Delay(10, token);
            }
        }
        async Task Capture(string name, Color expected)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession current)
            {
                if (current.NeedsUpdate) return;
                window.FramePresented -= Presented;
                try
                {
                    using var image = current.ReadbackOpenGLImage();
                    using var pixels = SKBitmap.FromImage(image); var pixel = pixels.GetPixel(image.Width / 2, 0);
                    Require(Math.Abs(pixel.Red - expected.R) <= 1 && Math.Abs(pixel.Green - expected.G) <= 1 && Math.Abs(pixel.Blue - expected.B) <= 1,
                        "Presented border pixels do not match the active/inactive accent.");
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(Path.Combine(directory, name)); data.SaveTo(output); captures.Add(name); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
    }
    private static nint Handle(TrueMoon.Alloy.Platform.Silk.SilkWindowHost host) => host.NativeWindow.Native!.Win32!.Value.Hwnd;
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint window, uint message, nuint wparam, nint lparam);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
