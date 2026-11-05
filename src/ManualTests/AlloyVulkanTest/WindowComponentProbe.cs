using System.Collections.Concurrent;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Platform.Silk;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace AlloyVulkanTest;

internal static class WindowComponentProbe
{
    internal static async Task RunAsync(string[] args)
    {
        var index = Array.IndexOf(args, "--component-output");
        if (index < 0 || index + 1 == args.Length) throw new ArgumentException("--component-output requires a fresh evidence directory.");
        var output = Path.GetFullPath(args[index + 1]);
        if (Directory.Exists(output)) throw new IOException("Evidence directory already exists.");
        Directory.CreateDirectory(output);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = deadline.Token;
        var validation = new ConcurrentQueue<string>(); var reports = new List<object>();
        foreach (var api in new[] { UiWindowGraphicsApi.OpenGL, UiWindowGraphicsApi.Vulkan })
        {
            using var sessions = new HostedUiSessionFactory(api == UiWindowGraphicsApi.OpenGL
                ? new SkiaOpenGLRenderBackend() : new SkiaVulkanRenderBackend());
            await using var dispatcher = new HostedUiDispatcher();
            ProbeWindow? view = null; UiSession? ui = null; var owner = 0;
            var presented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var host = new HostedUiWindow(new SilkUiWindowOptions
            {
                GraphicsApi = api, Width = 240, Height = 160, Title = "host fallback",
                Appearance = new WindowAppearance { Decorated = false },
                Chrome = new WindowChromeOptions { NativeDrag = true }, FrameMode = UiFrameMode.Continuous,
                ValidationMessage = args.Contains("--validation") ? validation.Enqueue : null
            }, sessions, () => { owner = Environment.CurrentManagedThreadId; return view = new ProbeWindow(); }, null, dispatcher);
            host.FramePresented += session => { ui = session; presented.TrySetResult(); };
            await host.StartAsync(token); await presented.Task.WaitAsync(token);
            var root = view!; var session = ui!;
            await session.InvokeAsync(() =>
            {
                Require(root.IsHosted && root.ActualClientSize.Width > 0, "Component was not connected/observed.");
                Require(session.Root is WindowFrame { Content: var content } && ReferenceEquals(content, root), "Expected exactly one native frame around the Window component.");
                Require(ReferenceEquals(root.Child, root.Input) && ReferenceEquals(root.Input.Parent, root), "Content ownership changed.");
                root.HostStateChanged += _ => Require(Environment.CurrentManagedThreadId == owner, "Host-state notification escaped owner.");
                session.Focus(root.Input); root.Input.Value = "retained draft";
            }, token);
            await UntilNative(host, native => native.NativeWindow.Title == "Component window" && native.Viewport.Width / native.Viewport.Scale > 400, token);
            await session.InvokeAsync(() =>
            {
                root.Title = "Updated component"; root.ClientSize = new(640, 400); session.SetTheme(Theme.Light);
            }, token);
            await UntilNative(host, native => native.NativeWindow.Title == "Updated component" && Math.Abs(native.Viewport.Width / native.Viewport.Scale - 640) < 2, token);
            await session.InvokeAsync(() =>
            {
                Require(((WindowFrame)session.Root).DefaultTitleBar!.Title == root.Title, "Painted and native captions diverged.");
                Require(root.Input.IsFocused && root.Input.Value == "retained draft", "Resize/theme rebuilt or reset content.");
            }, token);
            if (api == UiWindowGraphicsApi.OpenGL)
            {
                var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void Capture(UiSession current)
                {
                    host.FramePresented -= Capture;
                    try
                    {
                        using var image = current.ReadbackOpenGLImage(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                        using var file = File.Create(Path.Combine(output, api + ".png")); data.SaveTo(file); captured.TrySetResult();
                    }
                    catch (Exception error) { captured.TrySetException(error); }
                }
                try
                {
                    await session.InvokeAsync(() => { host.FramePresented += Capture; session.RequestFrame(); }, token);
                    await captured.Task.WaitAsync(token);
                }
                finally { host.FramePresented -= Capture; }
            }
            host.PostWindow(_ => root.Maximize());
            await UntilNative(host, native => native.State == UiWindowState.Maximized && root.State == WindowPresentationState.Maximized, token);
            host.PostWindow(_ => root.Restore());
            await UntilNative(host, native => native.State == UiWindowState.Normal && root.State == WindowPresentationState.Normal, token);
            host.PostWindow(_ => root.Minimize());
            await UntilNative(host, native => native.State == UiWindowState.Minimized && root.State == WindowPresentationState.Minimized, token);
            host.PostWindow(_ => root.Restore());
            await UntilNative(host, native => native.State == UiWindowState.Normal && root.State == WindowPresentationState.Normal, token);
            await session.InvokeAsync(() => Require(root.ClientSize == new Size(640, 400) && root.Input.Value == "retained draft",
                "Native state changes overwrote requested size or document content."), token);
            var mode = 0; var calls = 0; var hostCalls = 0;
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var reasons = new ConcurrentQueue<WindowCloseReason>();
            root.Closing += reason =>
            {
                Require(Environment.CurrentManagedThreadId == owner, "Closing escaped owner.");
                reasons.Enqueue(reason); Interlocked.Increment(ref calls);
                if (Volatile.Read(ref mode) == 0) return Task.FromResult(false);
                entered.TrySetResult(); return release.Task;
            };
            host.Closing += _ => { Interlocked.Increment(ref hostCalls); return Task.FromResult(true); };
            Task<bool>? close = null;
            await session.InvokeAsync(() => close = root.RequestCloseAsync(token), token);
            Require(!await close!, "Component close veto was ignored.");
            Require(hostCalls == 0 && !host.Completion.IsCompleted && !root.IsDisposed, "Veto reached terminal host work.");
            Volatile.Write(ref mode, 1);
            await session.InvokeAsync(() => close = root.RequestCloseAsync(token), token);
            await entered.Task.WaitAsync(token);
            try
            {
                await session.InvokeAsync(() => Require(!root.IsDisposed && root.IsHosted, "Content disposed before async drain."), token);
                Require(!close!.IsCompleted && hostCalls == 0, "Close completed before drain.");
            }
            finally { release.TrySetResult(true); }
            Require(await close!, "Accepted close did not complete.");
            Require(host.IsClosed && root.IsDisposed && !root.IsHosted && root.Input.IsDisposed, "Window/tree connection did not clean up.");
            Require(calls == 2 && hostCalls == 1 && reasons.All(r => r == WindowCloseReason.Application), "Close callbacks were duplicated or reason changed.");
            Require(sessions.ActiveSessionCount == 0, "Session registry retained closed Window.");
            reports.Add(new { Backend = api.ToString(), Passed = true, Owner = owner, Frames = host.PresentedFrames,
                CloseCalls = calls, HostCloseCalls = hostCalls, Sessions = sessions.ActiveSessionCount,
                Device = (session.Rendering as IUiRenderDiagnostics)?.DeviceInfo });
        }
        var providers = await WindowComponentAppProbe.RunAsync(token);
        if (!validation.IsEmpty) throw new InvalidOperationException(string.Join(Environment.NewLine, validation));
        Require(Win32WindowChrome.ActiveHooks == 0 && Win32WindowDpi.ActiveHooks == 0, "Native hooks survived cleanup.");
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        { Passed = true, Reports = reports, FactoryProviders = providers, ValidationRequested = args.Contains("--validation"), ValidationMessages = validation.Count, ChromeHooks = Win32WindowChrome.ActiveHooks, DpiHooks = Win32WindowDpi.ActiveHooks }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Window component: OpenGL/Vulkan initial settings, one frame, title/resize/state commands, retained focus/draft/theme, async close veto/drain and cleanup passed.");
    }
    private static async Task UntilNative(HostedUiWindow window, Func<SilkWindowHost, bool> check, CancellationToken token)
    {
        while (true)
        {
            var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(host => { try { answer.SetResult(check(host)); } catch (Exception error) { answer.SetException(error); } });
            if (await answer.Task.WaitAsync(token)) return;
            await Task.Delay(10, token);
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class ProbeWindow : Window<object>
    {
        internal TextBox Input { get; } = new() { Value = "draft" };
        internal ProbeWindow() { this.WithTitle("Component window").WithClientSize(520, 320).Content(Input); }
    }
}
