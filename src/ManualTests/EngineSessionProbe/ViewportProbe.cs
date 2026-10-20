using TrueMoon.Aether;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Aether.Contracts;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class ViewportCanvas : Element
{
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private SkiaImageSource? _image;
    private (FrameDisplay Display, FrameMetadata Metadata)? _imageFrame;
    public (FrameDisplay Display, FrameMetadata Metadata)? Drawn { get; private set; }
    public void Upload(FrameDisplay display, FrameLease frame)
    {
        if (_owner != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Upload requires the UI owner.");
        VerifyAccess();
        var next = SkiaImageSource.CopyBgraPixels(display.Surface.Width, display.Surface.Height, frame.Pixels);
        var old = _image; _image = next; _imageFrame = (display, frame.Metadata); old?.Dispose(); Invalidate();
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(available.Width, 420);
    protected override void DrawCore(IDrawingContext context)
    {
        context.Fill(Bounds, new(15, 19, 28));
        if (_image != null) context.Image(_image, Bounds);
        Drawn = _imageFrame;
    }
    public override void Dispose()
    {
        if (IsDisposed) return;
        if (_owner != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Dispose requires the UI owner.");
        base.Dispose(); _image?.Dispose(); _image = null; _imageFrame = null; Drawn = null;
    }
}

public sealed class ViewportView : View
{
    public ViewportCanvas Canvas { get; } = new() { Height = 420 };
    public Text Status { get; } = Text.Plain("Runtime stopped");
    public ViewportView(Action<string> command)
    {
        Content(() => VStack.Create().Padding(12).WithChildren(
            Text.Plain("TrueMoon — child process / CPU frame viewport"),
            HStack.Create().WithChildren(
                Button.Simple("Start").OnClick(() => command("start")),
                Button.Simple("Play").OnClick(() => command("play")),
                Button.Simple("Pause").OnClick(() => command("pause")),
                Button.Simple("Step").OnClick(() => command("step")),
                Button.Simple("Stop").OnClick(() => command("stop"))),
            Status, Canvas, Text.Plain("ENG2 pattern. Authoring document stays in Editor; simulation runs in owned child.")));
    }
}

public static class ViewportProbe
{
    public static async Task<int> RunAsync(string? reportDirectory)
    {
        var smoke = reportDirectory != null;
        var directory = reportDirectory ?? Path.Combine(Path.GetTempPath(), "tm-viewport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var factory = new NativeRuntimeFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        SessionSupervisor supervisor = null!;
        FrameSession session = null!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(smoke ? 180 : 3600));
        var uploads = new LatencySamples(); var latency = new LatencySamples(); var cases = new List<string>();
        (ulong Child, ulong Surface, ulong Frame)? lastPresentation = null;
        var pixelChecks = 0; var uploadCount = 0; var skipUploads = 0; long producerDrops = 0; string? commandError = null;
        Task command = Task.CompletedTask;
        var commandGate = new object();
        void Dispatch(string kind)
        {
            lock (commandGate)
            {
                if (kind == "stop")
                {
                    var pending = command;
                    command = Task.Run(async () =>
                    {
                        try { await session.StopAsync(); await pending; }
                        catch (Exception e) { Volatile.Write(ref commandError, e.Message); }
                    });
                    return;
                }
                if (!command.IsCompleted) return;
                command = Task.Run(async () =>
                {
                    try
                    {
                        switch (kind)
                        {
                            case "start": await session.StartAsync(authoring.Snapshot(), 1280, 720, token: deadline.Token); break;
                            case "stop": await session.StopAsync(); break;
                            case "play": await supervisor.SetPlayingAsync(true, deadline.Token); break;
                            case "pause": await supervisor.SetPlayingAsync(false, deadline.Token); break;
                            case "step": await supervisor.StepAsync(deadline.Token); break;
                        }
                        Volatile.Write(ref commandError, null);
                    }
                    catch (Exception e) { Volatile.Write(ref commandError, e.Message); }
                });
            }
        }
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Builder(_ => { }).Setup(c =>
        {
            c.UseAether(o =>
            {
                o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId;
                o.ReportDirectory = directory; o.RuntimeFactory = _ => factory;
            });
            c.UsePresentation<ViewportView>(o =>
            {
                o.UseSkiaOpenGL(fonts);
                o.UseSilkWindow(w => { w.Width = 900; w.Height = 590; w.Title = "TrueMoon ENG2 viewport"; w.EnableDiagnostics = true; w.FrameMode = UiFrameMode.Continuous; });
            });
            c.Services(r => r.Singleton<Action<string>>(_ => Dispatch).Transient<ViewportView>());
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        supervisor = engine.Supervisor; session = engine.Frames;
        var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
        var sessions = (HostedUiSessionFactory)app.Services.GetService(typeof(HostedUiSessionFactory))!;
        ViewportView? view = null; Exception? failure = null; var completed = false; var screenshot = false;
        var observedSize = (Width: 0, Height: 0); var sizeChanged = Stopwatch.GetTimestamp(); Task resize = Task.CompletedTask;
        window.FramePresented += ui =>
        {
            view = (ViewportView)ui.Root;
            if (view.Canvas.Drawn is { } drawn && ReferenceEquals(session.Display, drawn.Display))
            {
                var key = (drawn.Display.Surface.Identity.Generation, drawn.Display.Surface.Generation, drawn.Metadata.FrameId);
                if (lastPresentation != key)
                {
                    lastPresentation = key;
                    if (smoke)
                    {
                        using var image = ui.ReadbackOpenGLImage();
                        using var bitmap = SKBitmap.FromImage(image);
                        VerifyPixels(bitmap, view.Canvas.Bounds, ui.Viewport.Scale, drawn.Display.Surface, drawn.Metadata);
                        pixelChecks++;
                        if (!screenshot)
                        {
                            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                            using var output = File.Create(Path.Combine(directory, "viewport.png")); data.SaveTo(output); screenshot = true;
                        }
                    }
                    latency.Add(Stopwatch.GetElapsedTime(drawn.Metadata.ProducedTimestamp).TotalMilliseconds);
                    session.Presented(drawn.Display, drawn.Metadata);
                }
            }
            var display = session.Display;
            if (display != null && Volatile.Read(ref skipUploads) == 0)
            {
                using var frame = display.Receiver.TakeLatest();
                if (frame != null && ReferenceEquals(session.Display, display))
                {
                    var start = Stopwatch.GetTimestamp(); view.Canvas.Upload(display, frame);
                    window.RequestFrame(); // Explicit redraw of dynamic pixels; Continuous supplies receiver polling.
                    uploads.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds); uploadCount++;
                }
            }
            var status = supervisor.Status;
            view.Status.Value = $"{status.State} | child {status.Identity?.Generation} | surface {display?.Surface.Generation} | tick {status.Tick}" +
                (display == null ? " | viewport unavailable" : "") + (status.Failure is { } f ? $" | {f.Phase}: {f.Cause.Message}" : "") +
                (Volatile.Read(ref commandError) is { } error ? " | " + error : "");
            // UI resize only schedules work; it never opens a mapping or waits on the mutex.
            if (!smoke && session.Surface is { } activeSurface)
            {
                var bounds = view.Canvas.Bounds;
                var size = (Width: Math.Clamp((int)(bounds.Width * ui.Viewport.Scale), 16, 1920), Height: Math.Clamp((int)(bounds.Height * ui.Viewport.Scale), 16, 1080));
                if (size != observedSize) { observedSize = size; sizeChanged = Stopwatch.GetTimestamp(); }
                if (resize.IsCompleted && size != (activeSurface.Width, activeSurface.Height) && Stopwatch.GetElapsedTime(sizeChanged).TotalMilliseconds > 150)
                    resize = Task.Run(() => ResizeQuietlyAsync(size.Width, size.Height));
            }
        };
        async Task ResizeQuietlyAsync(int width, int height)
        {
            try { await session.ResizeAsync(width, height, deadline.Token); }
            catch (Exception e) { Volatile.Write(ref commandError, e.Message); }
        }
        try
        {
            await app.StartAsync(deadline.Token);
            Task shutdown = Task.CompletedTask;
            window.Closing += async _ =>
            {
                shutdown = Task.Run(async () => { await session.StopAsync(); await Task.WhenAll(command, resize); });
                await shutdown;
                Probe.Check(!view!.IsDisposed && !view.Canvas.IsDisposed && session.MappingCount == 0,
                    "UI was disposed before engine shutdown drained.");
                return true;
            };
            window.PostWindow(host =>
            {
                host.NativeWindow.Update += _ =>
                {
                    if (!smoke && (host.State == UiWindowState.Minimized || host.Viewport.IsEmpty) && session.Surface is { Suspended: false } && resize.IsCompleted)
                        resize = Task.Run(() => ResizeQuietlyAsync(0, 0));
                };
            });
            if (smoke)
            {
                await session.StartAsync(authoring.Snapshot(), 1280, 720, token: deadline.Token); cases.Add("first-presented-1280x720");
                var first = supervisor.Status.Identity!;
                await supervisor.StepAsync(deadline.Token);
                await WaitAsync(() => session.Display?.Receiver.Received >= 3, deadline.Token);
                await supervisor.SetPlayingAsync(true, deadline.Token);
                await Task.Delay(300, deadline.Token); await supervisor.SetPlayingAsync(false, deadline.Token); cases.Add("pause-play-step");
                foreach (var size in new[] { (320, 180), (1920, 1080), (16, 16), (1280, 720) })
                { await session.ResizeAsync(size.Item1, size.Item2, deadline.Token); cases.Add($"resize-{size.Item1}x{size.Item2}"); }
                window.Resize(1100, 650); await Task.Delay(100, deadline.Token); window.Resize(900, 590);
                await session.ResizeAsync(0, 0, deadline.Token);
                Probe.Check(session.Display == null && session.MappingCount == 0, "Zero-size did not suspend/release surface.");
                await supervisor.GetStatusAsync(deadline.Token);
                await session.ResizeAsync(640, 360, deadline.Token); cases.Add("suspend-resume");
                var receiver = session.Display!.Receiver; var receivedBefore = receiver.Received;
                Volatile.Write(ref skipUploads, 1); await Task.Delay(700, deadline.Token); Volatile.Write(ref skipUploads, 0);
                Probe.Check(receiver.Received > receivedBefore && receiver.Replaced > 0, "Slow UI did not retain bounded latest frames."); cases.Add("slow-ui");
                producerDrops += (await supervisor.GetStatusAsync(deadline.Token)).DroppedFrames;
                await session.StopAsync(); Probe.Check(session.MappingCount == 0, "Stop retained mappings.");
                for (var cycle = 1; cycle < 20; cycle++)
                { await session.StartAsync(authoring.Snapshot(), 320, 180, token: deadline.Token); await session.StopAsync(); }
                Probe.Check(supervisor.Status.Identity!.Generation == first.Generation + 19, "Child generation reused."); cases.Add("20-start-stop");
                await session.StartAsync(authoring.Snapshot(), 320, 180, "crash-status", deadline.Token);
                try { await supervisor.GetStatusAsync(deadline.Token); throw new InvalidOperationException("Crash accepted."); }
                catch (Exception) when (supervisor.Status.State == SessionState.Faulted) { }
                await WaitAsync(() => session.Display == null, deadline.Token); await session.StopAsync(); cases.Add("crash-eof-cleanup");
                await session.StartAsync(authoring.Snapshot(), 320, 180, "frame-eof", deadline.Token);
                await supervisor.GetStatusAsync(deadline.Token);
                await WaitAsync(() => supervisor.Status.State == SessionState.Faulted, deadline.Token);
                Probe.Check(supervisor.Status.Failure?.Phase == "FrameTransport", "EOF was not reported as a frame transport failure.");
                await session.StopAsync(); cases.Add("frame-eof-live-child");
                using (var cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
                {
                    cancel.CancelAfter(TimeSpan.FromSeconds(2));
                    try { await session.StartAsync(authoring.Snapshot(), 320, 180, "no-frames", cancel.Token); throw new InvalidOperationException("Missing frame accepted."); }
                    catch (OperationCanceledException) { }
                }
                Probe.Check(supervisor.Status.State == SessionState.Faulted && session.MappingCount == 0, "First-frame failure leaked resources.");
                await session.StopAsync(); cases.Add("first-frame-cancel");
                var timeout = Stopwatch.StartNew();
                try { await session.StartAsync(authoring.Snapshot(), 320, 180, "no-frames", deadline.Token); throw new InvalidOperationException("Missing first frame deadline."); }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { }
                Probe.Check(timeout.Elapsed >= TimeSpan.FromSeconds(14) && timeout.Elapsed < TimeSpan.FromSeconds(23) &&
                    supervisor.Status.Failure?.Phase == "FirstFrame" && session.MappingCount == 0, "Total startup/first frame deadline not enforced.");
                await session.StopAsync(); cases.Add("first-frame-15s-deadline");
                await session.StartAsync(authoring.Snapshot(), 320, 180, token: deadline.Token);
                using (var editorProcess = Process.GetCurrentProcess())
                { editorProcess.Refresh(); Probe.Check(editorProcess.CloseMainWindow(), "Could not request native Editor close."); }
                await window.Completion.WaitAsync(deadline.Token); await shutdown; cases.Add("native-close-drains-before-ui");
                Probe.Check(JsonSerializer.Serialize(authoring) == before, "Authoring state changed.");
                Probe.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor loaded Game.Logic.");
                Probe.Check(factory.Leases.All(l => l.ResourcesReleased && l.Exited.IsCompleted), "Owned child not reaped.");
                Probe.Check(session.PeakMappings <= 2 && session.PeakMappingBytes <= 50L * 1024 * 1024, "Resize mapping bound exceeded.");
            }
            await window.Completion.WaitAsync(deadline.Token);
            await session.StopAsync(); await Task.WhenAll(command, resize, shutdown);
            await app.StopAsync(deadline.Token);
            Probe.Check(sessions.ActiveSessionCount == 0 && view is { IsDisposed: true } && view.Canvas.IsDisposed, "UI cleanup incomplete.");
            var diagnostics = window.Diagnostics?.Snapshot ?? throw new InvalidOperationException("UI diagnostics missing.");
            Probe.Check(diagnostics.WindowId == window.Id && diagnostics.State == UiSessionState.Disposed
                && diagnostics.Queue.Pending == 0 && diagnostics.Render.Completed > 0
                && diagnostics.Measure.Completed > 0 && diagnostics.Arrange.Completed > 0
                && diagnostics.Present.Completed == window.PresentedFrames && diagnostics.Present.Failed == 0,
                "UI diagnostic phases, identity or cleanup state mismatch.");
            completed = true;
        }
        catch (Exception e) { failure = e; Console.Error.WriteLine(e); }
        finally
        {
            await session.StopAsync(); window.Close(); await app.StopAsync();
            if (smoke) Records.Write(directory, "viewport-report.json", new
            {
                Completed = completed, Failure = failure?.ToString(), Cases = cases, PixelChecks = pixelChecks, Uploads = uploadCount,
                Presented = window.PresentedFrames, Copy = session.CopyLatency, Upload = uploads.Report(), Frame = latency.Report(),
                Diagnostics = window.Diagnostics?.Snapshot,
                ProducerDropsObserved = producerDrops,
                session.Received, session.Replaced, session.PeakMappings, session.PeakMappingBytes, Mappings = session.MappingCount,
                ChildCount = factory.Leases.Count, ChildrenReleased = factory.Leases.All(l => l.ResourcesReleased && l.Exited.IsCompleted),
                ActiveUiSessions = sessions.ActiveSessionCount, EditorPreserved = JsonSerializer.Serialize(authoring) == before,
                GameLogicLoadedInEditor = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"),
                Host = Assembly.GetExecutingAssembly().Location, Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            });
        }
        return completed ? 0 : 1;
    }
    private static async Task WaitAsync(Func<bool> ready, CancellationToken token)
    { while (!ready()) await Task.Delay(10, token); }
    private static void VerifyPixels(SKBitmap bitmap, Rect rect, float scale, FrameSurface surface, FrameMetadata frame)
    {
        // Pixel centers inside the four solid 6x6 corners survive viewport scaling.
        var samples = new[] { (3, 3), (surface.Width - 3, 3), (3, surface.Height - 3), (surface.Width - 3, surface.Height - 3) };
        foreach (var (x, y) in samples)
        {
            var px = Math.Clamp((int)((rect.X + x * rect.Width / surface.Width) * scale), 0, bitmap.Width - 1);
            var py = Math.Clamp((int)((rect.Y + y * rect.Height / surface.Height) * scale), 0, bitmap.Height - 1);
            var actual = (uint)bitmap.GetPixel(px, py); var expected = FramePattern.Pixel(x, y, surface.Width, surface.Height, frame.FrameId, frame.Tick, 0xff2080c0);
            Probe.Check(actual == expected, $"Presented BGRA/origin mismatch at {x},{y}: {actual:X8} != {expected:X8}.");
        }
        // Verify frame/tick bits in GPU pixels when the one-pixel rows are enlarged.
        // Linear sampling is accounted for at the actual framebuffer pixel center.
        if (rect.Width * scale / surface.Width < 2 || rect.Height * scale / surface.Height < 2) return;
        foreach (var y in new[] { 7, surface.Height - 8 })
        for (var x = 0; x < Math.Min(64, surface.Width); x++)
        {
            var px = (int)MathF.Round((rect.X + (x + .5f) * rect.Width / surface.Width) * scale - .5f);
            var py = (int)MathF.Round((rect.Y + (y + .5f) * rect.Height / surface.Height) * scale - .5f);
            var sx = (px + .5 - rect.X * scale) * surface.Width / (rect.Width * scale) - .5;
            var sy = (py + .5 - rect.Y * scale) * surface.Height / (rect.Height * scale) - .5;
            var ix = (int)Math.Floor(sx); var iy = (int)Math.Floor(sy); var fx = sx - ix; var fy = sy - iy;
            var actual = (uint)bitmap.GetPixel(px, py);
            foreach (var shift in new[] { 0, 8, 16 })
            {
                double Channel(int dx, int dy) => (FramePattern.Pixel(Math.Clamp(ix + dx, 0, surface.Width - 1),
                    Math.Clamp(iy + dy, 0, surface.Height - 1), surface.Width, surface.Height, frame.FrameId, frame.Tick, 0xff2080c0) >> shift) & 255;
                var expected = (Channel(0, 0) * (1 - fx) + Channel(1, 0) * fx) * (1 - fy) +
                    (Channel(0, 1) * (1 - fx) + Channel(1, 1) * fx) * fy;
                Probe.Check(Math.Abs(((actual >> shift) & 255) - expected) <= 3, "Presented frame/tick counter mismatch.");
            }
        }
    }
}
