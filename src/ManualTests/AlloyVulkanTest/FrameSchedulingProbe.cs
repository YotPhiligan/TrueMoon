using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

// Bounded scheduling checks on owned windows; no desktop input injection or global settings.
internal static partial class FrameSchedulingProbe
{
    internal static async Task RunAsync(string[] args)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var cases = new List<object>(); var messages = new List<string>();
        foreach (var siblingGL in new[] { true, false })
        {
            using var gl = new HostedUiSessionFactory(new SkiaOpenGLRenderBackend());
            using var vk = new HostedUiSessionFactory(new SkiaVulkanRenderBackend());
            await using var scope = new HostedUiDispatcher();
            await using var first = new Live(gl, true, scope, 40, args.Contains("--validation") ? messages.Add : null);
            await using var second = new Live(siblingGL ? gl : vk, siblingGL, scope, 460, args.Contains("--validation") ? messages.Add : null);
            await first.Start(deadline.Token); await second.Start(deadline.Token);
            await Task.Delay(120, deadline.Token); // Let initial WM_PAINT/placement settle.
            var before = first.Window.Diagnostics!.Snapshot; var siblingBefore = second.Window.Diagnostics!.Snapshot;
            await Task.Delay(160, deadline.Token);
            var idle = first.Window.Diagnostics.Snapshot;
            Require(idle.Update.Attempts == before.Update.Attempts && idle.Render.Attempts == before.Render.Attempts, "Clean OnDemand UI kept updating/drawing.");
            Require(idle.Present.Completed - before.Present.Completed < 4, "Clean OnDemand UI kept presenting periodically.");
            var redraw = idle.Render.Completed;
            await first.Session!.InvokeAsync(() => first.Session.Root.Background = new Color(17, 41, 97)).WaitAsync(deadline.Token);
            await Until(() => first.Window.Diagnostics.Snapshot.Render.Completed > redraw, deadline.Token);
            await first.Query(host =>
            {
                using var image = first.Session.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                var pixel = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
                Require(pixel.Red == 17 && pixel.Green == 41 && pixel.Blue == 97 && pixel.Alpha == 255, "Posted update did not reach GL pixels.");
                return true;
            }, deadline.Token);
            var drawing = first.Window.Diagnostics.Snapshot.Render.Completed;
            for (var i = 0; i < 1000; i++) first.Window.RequestFrame();
            await Until(() => first.Window.Diagnostics.Snapshot.Render.Completed > drawing, deadline.Token);
            await Task.Delay(60, deadline.Token);
            Require(first.Window.Diagnostics.Snapshot.Render.Completed - drawing < 6, "Frame request flood did not coalesce.");

            await first.Query(host =>
            {
                var frames = first.Frames; var renders = first.Window.Diagnostics.Snapshot.Render.Completed;
                SendMessageW(first.Handle, 0xF, 0, 0);
                Require(first.Frames > frames && first.Window.Diagnostics.Snapshot.Render.Completed == renders, "Native expose did not present cached pixels.");
                frames = second.Frames; SendMessageW(second.Handle, 0xF, 0, 0);
                Require(second.Frames > frames, "Sibling expose failed while another GL context was current.");
                return true;
            }, deadline.Token);
            first.Window.Resize(320, 200);
            await Until(() => first.Window.Diagnostics.Snapshot.Viewport.Width != before.Viewport.Width, deadline.Token);
            first.Window.Minimize();
            await Until(() => first.Window.Diagnostics.Snapshot.State == UiSessionState.Suspended, deadline.Token);
            await first.Session.InvokeAsync(() => { }).WaitAsync(deadline.Token); // Work must drain while suspended.
            var suspended = first.Window.Diagnostics.Snapshot;
            await Task.Delay(50, deadline.Token);
            Require(first.Window.Diagnostics.Snapshot.Render.Completed == suspended.Render.Completed, "Suspended window drew pixels.");
            first.Window.Restore();
            await Until(() => first.Window.Diagnostics.Snapshot.State == UiSessionState.Active && first.Window.Diagnostics.Snapshot.Present.Completed > suspended.Present.Completed, deadline.Token);
            var delivered = 0;
            for (var i = 0; i < 200; i++) first.Session.Post(() => delivered++);
            var siblingDraws = second.Window.Diagnostics.Snapshot.Render.Completed; second.Window.RequestFrame();
            await Until(() => Volatile.Read(ref delivered) == 200 && second.Window.Diagnostics.Snapshot.Render.Completed > siblingDraws, deadline.Token);
            await first.Window.StopAsync(deadline.Token);
            Require(first.Window.Diagnostics.Snapshot.State == UiSessionState.Disposed, "Stopped window lost its final snapshot.");
            var next = second.Window.Diagnostics.Snapshot.Render.Completed; second.Window.RequestFrame();
            await Until(() => second.Window.Diagnostics.Snapshot.Render.Completed > next, deadline.Token);
            await second.Window.StopAsync(deadline.Token);
            Require(gl.ActiveSessionCount == 0 && vk.ActiveSessionCount == 0, "Scheduling retained UI sessions.");
            cases.Add(new { SiblingGL = siblingGL, Idle = idle, First = first.Window.Diagnostics.Snapshot, Second = second.Window.Diagnostics.Snapshot,
                Delivered = delivered, Pixels = true, NativeExpose = true, MinimizeResume = true, Registry = 0 });
        }
        Require(messages.Count == 0 && Win32WindowDpi.ActiveHooks == 0 && Win32WindowChrome.ActiveHooks == 0 && Win32TransparentFramebuffer.ActiveHooks == 0,
            "Scheduling retained hooks or reported Vulkan validation messages.");
        var index = Array.IndexOf(args, "--scheduling-output");
        var output = Path.GetFullPath(index < 0 ? "TestResults/AlloyScheduling/report.json" : args[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { Completed = true, Cases = cases, ValidationMessages = messages.Count,
            DpiHooks = Win32WindowDpi.ActiveHooks, Scope = "OnDemand GL/GL and GL/Vulkan scheduling, not hardware/performance acceptance." }));
        Console.WriteLine("Frame scheduling passed: idle/posted/request/expose/resize/minimize/resume/fairness/cleanup, GL/GL and GL/Vulkan.");
    }

    private static async Task Until(Func<bool> condition, CancellationToken token)
    { while (!condition()) await Task.Delay(5, token); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [LibraryImport("user32.dll")] private static partial nint SendMessageW(nint hwnd, uint message, nuint wparam, nint lparam);

    private sealed class Live : IAsyncDisposable
    {
        internal readonly HostedUiWindow Window;
        internal UiSession? Session;
        internal nint Handle;
        internal int Frames;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Live(HostedUiSessionFactory factory, bool openGL, HostedUiDispatcher scope, int x, Action<string>? validation)
        {
            Window = new HostedUiWindow(new SilkUiWindowOptions { OpenGL = openGL, FrameMode = UiFrameMode.OnDemand, EnableDiagnostics = true,
                Width = 260, Height = 160, Title = "INF3 scheduling", ValidationMessage = validation }, factory,
                () => new Panel { Background = new Color(70, 80, 90) }, null, scope);
            Window.PostWindow(host => { host.NativeWindow.Position = new(x, 40); Handle = host.NativeWindow.Native!.Win32!.Value.Hwnd; });
            Window.FramePresented += session => { Session = session; Interlocked.Increment(ref Frames); _ready.TrySetResult(); };
        }
        internal async Task Start(CancellationToken token) { await Window.StartAsync(token); await _ready.Task.WaitAsync(token); }
        internal async Task<T> Query<T>(Func<SilkWindowHost, T> operation, CancellationToken token)
        {
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Window.PostWindow(host => { try { result.TrySetResult(operation(host)); } catch (Exception error) { result.TrySetException(error); throw; } });
            return await result.Task.WaitAsync(token);
        }
        public async ValueTask DisposeAsync() => await Window.DisposeAsync();
    }
}
