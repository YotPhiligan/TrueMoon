using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class GpuViewportCanvas : Element
{
    private SkiaImageSource? _image;
    public GpuFrameStamp? Stamp { get; private set; }
    public GpuFrameStamp? Drawn { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public int Zoom { get; set; } = 1;
    public void Set(SkiaImageSource image, GpuFrameStamp stamp, int width, int height)
    { VerifyAccess(); var old = _image; _image = image; Stamp = stamp; PixelWidth = width; PixelHeight = height; old?.Dispose(); Invalidate(); }
    public void Clear()
    { VerifyAccess(); _image?.Dispose(); _image = null; Stamp = null; Drawn = null; Invalidate(); }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(available.Width, 160);
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, new(19, 37, 61));
        context.Save();
        try { context.Clip(Bounds); if (_image != null) context.Image(_image, new(Bounds.X + 8, Bounds.Y + 8, PixelWidth * Zoom, PixelHeight * Zoom)); }
        finally { context.Restore(); }
        Drawn = Stamp;
    }
    public override void Dispose() { if (IsDisposed) return; Clear(); base.Dispose(); }
}

public sealed class GpuViewportView : View
{
    public GpuViewportCanvas Canvas { get; } = new() { Height = 160 };
    public GpuViewportView() => Content(() => VStack.Create().Padding(16).WithChildren(
        Text.Plain("TrueMoon — GPU viewport"), Canvas, Text.Plain("Vulkan child / borrowed GL texture / retained Skia GPU image")));
}

public static class GpuViewportProbe
{
    private static nint _readPixels;
    private static int _importReadbacks;
    private static unsafe nint TraceReadPixels(nint original) { _readPixels = original; return (nint)(delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)&ImportReadPixels; }
    [UnmanagedCallersOnly]
    private static unsafe void ImportReadPixels(int x, int y, int w, int h, uint format, uint type, void* pixels)
    { Interlocked.Increment(ref _importReadbacks); ((delegate* unmanaged<int, int, int, int, uint, uint, void*, void>)_readPixels)(x, y, w, h, format, type, pixels); }

    public static async Task<int> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150)); var token = deadline.Token;
        _importReadbacks = 0; var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o => { o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory; o.RuntimeFactory = _ => factory; });
            c.UsePresentation<GpuViewportView>(o => { o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w => { w.Width = 640; w.Height = 280; w.Title = "TrueMoon ENG4d GPU viewport"; w.EnableDiagnostics = true; }); });
            c.Services(r => r.Transient<GpuViewportView>());
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!; var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); UiSession ui = null!; GpuViewportView view = null!;
        window.FramePresented += s => { ui = s; view = (GpuViewportView)s.Root; shown.TrySetResult(); };
        var cases = new List<string>(); var captures = new List<object>(); var owner = 0; Action current = null!; GpuOpenGlContext context = null!; GpuAdapterId adapter = null!;
        TaskCompletionSource? presented = null; Exception? failure = null; var passed = false; Task shutdown = Task.CompletedTask; var copies = 0; var checkedEarlyReturn = false;
        TaskCompletionSource? dispatchEntered = null; TaskCompletionSource? dispatchRelease = null;
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == owner) { action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { try { current(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } }); return completion.Task;
        }
        async Task InvokeGpu(Action action)
        {
            var release = dispatchRelease;
            if (release != null) { dispatchEntered!.TrySetResult(); await release.Task; }
            await Owner(action);
        }
        async Task Capture(string name)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Owner(() => { presented = completion; view.Canvas.Invalidate(); window.RequestFrame(); });
            await completion.Task.WaitAsync(token); cases.Add(name);
        }
        async Task<GpuSurfaceSession> Start(string behavior = "gpu-surface", int width = 64, int height = 32)
        {
            await host.Editor.StopAsync(); await host.Supervisor.StartAsync(authoring.Snapshot(), behavior, token);
            var session = await host.Gpu.ConnectSurfaceAsync(factory.Leases[^1], adapter, context, token); await session.ConfigureAsync(width, height, token); return session;
        }
        async Task<GpuFrameCompletion> Copy(GpuSurfaceSession session, int slot, bool publish = true)
        {
            var ready = await session.ProduceAsync(slot, token); GpuOpenGlTexture? expired = null;
            var completion = await session.UseTextureAsync(ready, texture =>
            {
                expired = texture; Probe.Check(texture.Stamp == ready.Stamp, "Texture loan stamp mismatch.");
                if (!checkedEarlyReturn)
                {
                    var pendingReturn = session.ReturnAsync(ready, token); Probe.Check(pendingReturn.IsFaulted, "Return queued behind an active texture callback.");
                    var rejected = false; try { pendingReturn.GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }
                    Probe.Check(rejected, "Return accepted before last source draw."); checkedEarlyReturn = true;
                }
                var image = ((SkiaOpenGLSurface)ui.Rendering).CopyTexture(texture.Name, texture.Width, texture.Height); copies++;
                if (publish) view.Canvas.Set(image, texture.Stamp, texture.Width, texture.Height); else image.Dispose();
                window.RequestFrame();
            }, token);
            Probe.Check(completion == ready && session.Slots[slot].Phase == "Complete", "Callback did not observe matching GL completion.");
            var rejected = false; try { _ = expired!.Name; } catch (ObjectDisposedException) { rejected = true; } Probe.Check(rejected, "Texture loan escaped callback.");
            Probe.Check(await session.ReturnAsync(completion, token) == ready, "Texture was not retired after submitted copy."); return ready;
        }
        async Task Stop(GpuSurfaceSession session)
        { await session.DisposeAsync(); await host.Editor.StopAsync(); Probe.Check(session.IsReleased && factory.Leases[^1].Reaped && factory.Leases[^1].ResourcesReleased, "Viewport runtime not released."); }
        try
        {
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            await OwnerInitialize();
            window.FramePresented += s =>
            {
                var completion = presented; if (completion == null) return; presented = null;
                try
                {
                    Probe.Check(view.Canvas.Drawn == view.Canvas.Stamp, "Presentation acknowledged a stale UI draw.");
                    using var image = s.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                    var checkedPixels = Verify(bitmap, view.Canvas, s.Viewport.Scale);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100); var path = $"viewport-{captures.Count + 1:D2}.png"; using (var file = File.Create(Path.Combine(directory, path))) data.SaveTo(file);
                    captures.Add(new { view.Canvas.Stamp, view.Canvas.PixelWidth, view.Canvas.PixelHeight, view.Canvas.Zoom, Bounds = view.Canvas.Bounds,
                        s.Viewport.Scale, CheckedPixels = checkedPixels, Image = path, Sha256 = Convert.ToHexString(SHA256.HashData(data.ToArray())), window.PresentedFrames }); completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
            };
            window.Closing += async _ => { shutdown = host.DisposeAsync().AsTask(); await shutdown; Probe.Check(!view.IsDisposed && host.Gpu.ResourceCount == 0, "UI context closed before GPU drain."); return true; };
            var active = await Start(); var first = await Copy(active, 0); await Capture("first-present-whole-viewport"); cases.Add("return-rejected-during-source-use");
            for (var i = 0; i < 12; i++) await Copy(active, i % 3, false);
            Probe.Check(view.Canvas.Stamp == first.Stamp, "Discarded frames replaced retained UI image."); await Capture("retained-image-survives-slot-reuse");
            await Owner(() => { view.Canvas.Zoom = 2; }); await Capture("scaled-premul-srgb-origin");
            await active.ConfigureAsync(320, 180, token); await Capture("retained-image-survives-surface-resize");
            await Copy(active, 2); await Capture("clipped-resized-frame");
            await active.ConfigureAsync(0, 0, token); await Capture("retained-image-survives-suspend");
            await active.ConfigureAsync(16, 16, token); await Owner(() => view.Canvas.Zoom = 1); var resumed = await Copy(active, 1); await Capture("resume-first-present");
            await Stop(active); await Capture("retained-image-survives-runtime-stop");
            active = await Start(); var replaced = await Copy(active, 0);
            Probe.Check(replaced.Stamp.Identity.Generation > resumed.Stamp.Identity.Generation, "Runtime replacement reused identity.");
            var staleRejected = false; try { await active.UseTextureAsync(resumed, _ => throw new Exception("Stale callback executed"), token); } catch (InvalidDataException) { staleRejected = true; }
            Probe.Check(staleRejected && active.IsAvailable, "Stale use changed active generation."); await Capture("replacement-rejects-old-generation");
            await Stop(active);
            active = await Start("gpu-surface-crash-second"); await Copy(active, 0); await Capture("before-runtime-crash");
            try { await active.ProduceAsync(1, token); throw new Exception("Crash not observed"); } catch (Exception error) when (error is IOException or ObjectDisposedException) { }
            await Stop(active); await Capture("retained-image-survives-runtime-crash");
            active = await Start(); var throwing = await active.ProduceAsync(0, token); var injected = new InvalidOperationException("Injected texture consumer failure");
            GpuOpenGlTexture? failedLoan = null;
            Exception? callbackError = null; try { await active.UseTextureAsync(throwing, texture =>
            { failedLoan = texture; using var image = ((SkiaOpenGLSurface)ui.Rendering).CopyTexture(texture.Name, texture.Width, texture.Height); copies++; throw injected; }, token); } catch (Exception error) { callbackError = error; }
            Probe.Check(ReferenceEquals(injected, callbackError) && !active.IsAvailable, "Texture callback error hidden or session not fenced.");
            var expiredAfterFailure = false; try { _ = failedLoan!.Width; } catch (ObjectDisposedException) { expiredAfterFailure = true; } Probe.Check(expiredAfterFailure, "Failed callback left a live loan.");
            await Stop(active); cases.Add("callback-failure-fences-and-drains");
            active = await Start(); var cancelledReady = await active.ProduceAsync(0, token); var callbackRan = false;
            dispatchEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); dispatchRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using (var cancel = new CancellationTokenSource())
            {
                var use = active.UseTextureAsync(cancelledReady, _ => callbackRan = true, cancel.Token);
                await dispatchEntered.Task.WaitAsync(token); cancel.Cancel(); var release = dispatchRelease; dispatchRelease = null; release.TrySetResult();
                var cancelled = false; try { await use; } catch (OperationCanceledException) { cancelled = true; }
                Probe.Check(cancelled && !callbackRan && !active.IsAvailable, "Cancelled owner dispatch executed late texture callback.");
            }
            await Stop(active); cases.Add("cancel-before-owner-dispatch-fences-late-callback");
            active = await Start(); await Copy(active, 0); await Capture("restart-after-callback-failure");
            await Owner(() => view.Canvas.Clear()); await Capture("clear-releases-last-frame");
            await Copy(active, 2); await Capture("native-close-live-gpu-image");
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) { process.Refresh(); Probe.Check(process.CloseMainWindow(), "GPU viewport WM_CLOSE failed."); }
            await window.Completion.WaitAsync(token); await shutdown; await app.StopAsync(token);
            Probe.Check(_importReadbacks == 0 && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && sessions.ActiveSessionCount == 0 && view.IsDisposed && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "GPU viewport delivery read pixels or leaked owned resources.");
            var cleanups = Directory.GetFiles(directory, "surface-cleanup-*.json"); Probe.Check(cleanups.Length == 5, "GPU viewport cooperative cleanup reports missing.");
            foreach (var path in cleanups)
            {
                var cleanup = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)); var producer = cleanup.GetProperty("LastSurfaceProducer");
                Probe.Check(cleanup.GetProperty("ResourceCount").GetInt32() == 0 && cleanup.GetProperty("OpenHandleCount").GetInt32() == 0 && cleanup.GetProperty("Error").ValueKind == JsonValueKind.Null &&
                    producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 && producer.GetProperty("SurfacesCreated").GetInt32() == producer.GetProperty("SurfacesReleased").GetInt32() &&
                    producer.GetProperty("SlotsCreated").GetInt32() == producer.GetProperty("SlotsReleased").GetInt32() && producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32() &&
                    producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 && producer.GetProperty("ValidationEnabled").GetBoolean() == (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1"), "GPU viewport producer cleanup/validation failed.");
            }
            passed = true;
        }
        catch (Exception error) { failure = error; Console.Error.WriteLine(error); }
        finally
        {
            dispatchRelease?.TrySetResult(); dispatchRelease = null;
            try { await host.Gpu.DisposeAsync(); await host.Editor.StopAsync(); window.Close(); await app.StopAsync(); }
            catch (Exception error) { passed = false; failure = failure == null ? error : new AggregateException(failure, error); Console.Error.WriteLine(error); }
            Records.Write(directory, "gpu-viewport-report.json", new { Completed = passed, Failure = failure?.ToString(), Cases = cases, Captures = captures, Copies = copies, ImportReadbacks = _importReadbacks,
                Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount, ActiveUiSessions = sessions.ActiveSessionCount, EditorPreserved = before == JsonSerializer.Serialize(authoring),
                ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1", CpuViewportDefault = true, Adapter = adapter,
                Children = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased, l.StandardError }) });
        }
        return passed ? 0 : 1;

        async Task OwnerInitialize()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(w =>
            {
                try
                {
                    owner = Environment.CurrentManagedThreadId; var gl = w.NativeWindow.GLContext!; current = gl.MakeCurrent; current();
                    nint Resolve(string name) { var address = gl.TryGetProcAddress(name, out var pointer) ? pointer : 0; return name == "glReadPixels" ? TraceReadPixels(address) : address; }
                    context = new(Resolve, current, InvokeGpu);
                    var capabilities = GpuCapabilityCollector.OpenGl(new(Guid.NewGuid(), 1, BuildIdentity.HostId, BuildIdentity.GameId, new('A', 64)), Resolve).Capabilities;
                    Probe.Check(capabilities.Error == null && capabilities.Devices.Count == 1, "GPU viewport profile unavailable."); adapter = capabilities.Devices[0]; completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
            });
            await completion.Task.WaitAsync(token);
        }
    }

    // Independent scalar formula, not GpuReferencePattern.Create or importer readback. Verify the whole clipped canvas.
    private static int Verify(SKBitmap bitmap, GpuViewportCanvas canvas, float scale)
    {
        var bounds = canvas.Bounds; var stamp = canvas.Drawn; var count = 0;
        var left = bounds.X * scale; var top = bounds.Y * scale; var imageLeft = (bounds.X + 8) * scale; var imageTop = (bounds.Y + 8) * scale;
        for (var y = (int)Math.Ceiling(top); y < (int)((bounds.Y + bounds.Height) * scale); y++)
        for (var x = (int)Math.Ceiling(left); x < (int)((bounds.X + bounds.Width) * scale); x++)
        {
            var actual = bitmap.GetPixel(x, y); double[] expected = [19, 37, 61, 255];
            if (stamp != null && x + .5 >= imageLeft && y + .5 >= imageTop && x + .5 < imageLeft + canvas.PixelWidth * canvas.Zoom * scale && y + .5 < imageTop + canvas.PixelHeight * canvas.Zoom * scale)
            {
                var sx = (x + .5 - imageLeft) / (canvas.Zoom * scale) - .5; var sy = (y + .5 - imageTop) / (canvas.Zoom * scale) - .5;
                var ix = (int)Math.Floor(sx); var iy = (int)Math.Floor(sy); var fx = sx - ix; var fy = sy - iy;
                double Sample(int channel, int dx, int dy)
                {
                    var px = Math.Clamp(ix + dx, 0, canvas.PixelWidth - 1); var py = Math.Clamp(iy + dy, 0, canvas.PixelHeight - 1);
                    var alpha = (px / 8 + py / 8) % 2 == 0 ? 255 : 128; if (channel == 3) return alpha;
                    var marker = 1 + (int)((stamp.FrameId - 1) % 251) * 17 + stamp.Slot * 29 + (int)(stamp.SurfaceGeneration % 251) * 11;
                    var raw = channel switch { 0 => px * 7 + py * 3 + 17 + marker, 1 => px + py * 11 + 31 + marker * 3, _ => px * 13 + py + 73 + marker * 7 };
                    return ((raw & 255) * alpha + 127) / 255;
                }
                var values = Enumerable.Range(0, 4).Select(c => (Sample(c, 0, 0) * (1 - fx) + Sample(c, 1, 0) * fx) * (1 - fy) + (Sample(c, 0, 1) * (1 - fx) + Sample(c, 1, 1) * fx) * fy).ToArray();
                for (var c = 0; c < 3; c++) expected[c] = values[c] + expected[c] * (1 - values[3] / 255);
            }
            int[] channels = [actual.Red, actual.Green, actual.Blue, actual.Alpha];
            for (var c = 0; c < 4; c++) Probe.Check(Math.Abs(channels[c] - expected[c]) <= 2, $"GPU viewport pixel {x},{y} channel{c}: {channels[c]} != {expected[c]:F2}; {stamp}.");
            count++;
        }
        return count;
    }
}
