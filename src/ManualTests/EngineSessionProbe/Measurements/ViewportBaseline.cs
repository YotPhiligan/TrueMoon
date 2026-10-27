using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;

namespace EngineSessionProbe.Measurements;

internal static class ViewportBaseline
{
    internal static async Task<int> RunAsync(string directory, string mode, int warmup, int interval)
    {
        if (mode is not ("cpu" or "gpu") || warmup is < 1 or > 30 || interval is < 1 or > 30)
            throw new ArgumentException("Use cpu/gpu and warmup/interval seconds in1..30.");
        if (Directory.Exists(directory)) throw new IOException("Use a fresh baseline directory.");
        Directory.CreateDirectory(directory);
        // Persist the experiment and advisory budgets BEFORE any native allocations or measurements.
        Records.Write(directory, "experiment.json", new
        {
            Schema = 1, Mode = mode, WarmupSeconds = warmup, MeasurementSeconds = interval,
            Extents = new[] { new[] { 640, 360 }, new[] { 1280, 720 }, new[] { 1280, 720 } },
            ProducerHz = 30, SimulationHz = 60, UiHz = 60, ResourcePollMilliseconds = 200,
            Budgets = new { MinimumPresentedFps = 24, ProduceToPresentP95Ms = 100, EditorAllocatedMiBPerSecond = 32, RuntimeAllocatedMiBPerSecond = 256, LifecycleOperationMs = 15000 },
            BudgetPolicy = "Advisory: report breaches; Completed means a valid experiment and released ownership, not performance acceptance.",
            NativeCleanup = "Normal cases require cooperative exit0, no Killing diagnostics, and zero local GPU handles/resources/mappings/UI sessions.",
            Pattern = "Same Game.Logic DLL, FramePattern/color, two distinct artifact revisions; CPU BGRA+CRC/MMF versus CPU RGBA+Vulkan staging/GL retained copy.",
            Clock = "Windows system-wide QPC; cross-process frequency equality is required.",
            Instrumentation = "Opt-in synchronous EventSource, bounded in-memory samples,200ms process polling; included in allocations/CPU. No per-frame file IO or measured pixel readback.",
            GpuTimestampQueries = new { Available = false, Reason = "No GPU timestamp query provider is installed by this baseline; stage times are CPU-observed." },
            GpuUtilizationAndVram = new { Available = false, Reason = "No device-scoped utilization/VRAM sampler; process private bytes are not VRAM." }
        });
        using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = limit.Token;
        using var capture = new BaselineCapture(); var gpu = mode == "gpu";
        var previousEnvironment = Environment.GetEnvironmentVariable(BaselineCapture.EnvironmentKey);
        Environment.SetEnvironmentVariable(BaselineCapture.EnvironmentKey, "1");
        var factory = new NativeRuntimeFactory(NativeRuntimeLaunch.CurrentProcess("--runtime")); var authoring = new AuthoringState();
        var authoringBefore = JsonSerializer.Serialize(authoring);
        var binding = gpu ? new EditorGpuViewportBinding(runtime => (NativeRuntimeLease)runtime) : null;
        var windows = new List<MeasurementWindow>(); var operations = new List<OperationSample>();
        var artifacts = new List<object>();
        var gpuOrigins = new Dictionary<ulong, ulong>();
        TaskCompletionSource? verification = null; string? lastPresented = null; var pixelChecks = 0;
        ViewportView? view = null; AetherHost host = null!; Exception? failure = null; var completed = false;
        WindowResult[] results = [];
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o =>
            {
                o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId;
                o.ReportDirectory = directory; o.RuntimeFactory = _ => factory;
                if (binding != null) o.ViewportTransport = binding.CreateTransport;
            });
            c.UsePresentation<ViewportView>(o =>
            {
                o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w =>
                { w.Width = 1000; w.Height = 760; w.Title = "TrueMoon ENG4e viewport baseline"; w.FrameMode = UiFrameMode.Continuous; w.FramesPerSecond = 60; });
            });
            c.Services(r => r.Singleton<Action<string>>(_ => _ => { }).Transient<ViewportView>());
        });
        host = app.Services.Resolve<AetherHost>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!; var windowId = window.Id;
        window.FramePresented += ui =>
        {
            var current = (ViewportView)ui.Root;
            Probe.Check(view == null || ReferenceEquals(view, current), "Baseline recreated the Editor root."); view = current;
            binding?.Observe(ui);
            var presentation = host.Frames.Presentation;
            FrameMetadata? metadata = null; ulong surface = 0; int width = 0, height = 0;
            if (view.Canvas.DrawnViewport is { } ticket && ReferenceEquals(presentation, ticket.Presentation))
            {
                metadata = ticket.Metadata; var descriptor = ticket.Presentation.Surface;
                // GPU allocation generations restart at1 per runtime; FrameSession generations remain monotonic.
                if (!gpuOrigins.TryGetValue(descriptor.Identity.Generation, out var origin)) gpuOrigins.Add(descriptor.Identity.Generation, origin = descriptor.Generation);
                surface = descriptor.Generation - origin + 1; width = descriptor.Width; height = descriptor.Height;
                host.Frames.Presented(ticket);
            }
            if (view.Canvas.Drawn is { } drawn && ReferenceEquals(host.Frames.Display, drawn.Display))
            {
                metadata = drawn.Metadata; surface = drawn.Display.Surface.Generation;
                width = drawn.Display.Surface.Width; height = drawn.Display.Surface.Height;
                host.Frames.Presented(drawn.Display, drawn.Metadata);
            }
            if (metadata != null && presentation != null)
            {
                var identity = presentation.Surface.Identity;
                var key = $"{identity.SessionId:N}/{identity.Generation}/{surface}/{metadata.FrameId}";
                if (key != lastPresented)
                {
                    lastPresented = key; var now = Stopwatch.GetTimestamp();
                    capture.Record("present", identity, surface, metadata.FrameId, now, now);
                    if (Interlocked.Exchange(ref verification, null) is { } check)
                    {
                        try
                        {
                            using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                            ViewportProbe.VerifyPixels(bitmap, view.Canvas.Bounds, ui.Viewport.Scale, width, height, metadata);
                            pixelChecks++; check.TrySetResult();
                        }
                        catch (Exception error) { check.TrySetException(error); }
                    }
                }
            }
            var display = host.Frames.Display;
            using var frame = display?.Receiver.TakeLatest();
            if (frame != null && ReferenceEquals(display, host.Frames.Display))
            {
                var started = Stopwatch.GetTimestamp(); view.Canvas.Upload(display!, frame);
                capture.Record("retained-copy", display!.Surface.Identity, display.Surface.Generation, frame.Metadata.FrameId, started, Stopwatch.GetTimestamp());
                window.RequestFrame();
            }
        };
        try
        {
            await app.StartAsync(token);
            if (binding != null) await binding.InitializeAsync(window, host, token);
            var hostId = BuildIdentity.HostId;
            GameBuildArtifact Artifact(long revision) => GameBuildArtifact.Publish(AppContext.BaseDirectory, Path.Combine(directory, "artifacts"),
                hostId, "Game.Logic.dll", "Game.Logic.ProbeGame", revision);
            var a = Artifact(1); var b = Artifact(2);
            foreach (var artifact in new[] { a, b }) artifacts.Add(new { artifact.BuildId, artifact.SourceRevision,
                DllSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(artifact.DirectoryPath, artifact.EntryAssembly)))) });
            void Context(long revision) => host.Editor.SetContext(new(new(Path.Combine(directory, "fixed-game.csproj"), "Game.Logic.dll", "Game.Logic.ProbeGame", revision),
                authoring.Snapshot(), authoring.Selection));
            Context(1); await host.Editor.SelectBuildAsync(a, token);
            await Operation("Start", () => host.Editor.StartAsync(640, 360, token));
            await host.Editor.PlayAsync(token); await Measure("initial", 640, 360);
            await Operation("Resize", () => host.Frames.ResizeAsync(1280, 720, token));
            await Measure("resized", 1280, 720);
            Context(2); await host.Editor.SelectBuildAsync(b, token);
            await Operation("Replace", async () => { await host.Editor.ReplaceAsync(token); });
            Probe.Check(host.Supervisor.Status.State == SessionState.Playing, "Replacement lost the playing state.");
            await Measure("replaced", 1280, 720);
            await Operation("Stop", host.Editor.StopAsync);
            Probe.Check(factory.Leases.Count == 2 && factory.Leases.All(l => l.Reaped && l.ResourcesReleased), "Baseline child ownership incomplete.");
            Probe.Check(window.Id == windowId && view is { IsDisposed: false } && JsonSerializer.Serialize(authoring) == authoringBefore, "Baseline changed Editor/App/authoring ownership.");

            async Task Operation(string name, Func<Task> action)
            {
                var started = Stopwatch.GetTimestamp();
                try { await action(); operations.Add(new(name, started, Stopwatch.GetTimestamp(), true)); }
                catch (Exception error) { operations.Add(new(name, started, Stopwatch.GetTimestamp(), false, error.ToString())); throw; }
            }
            async Task Measure(string name, int width, int height)
            {
                var check = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.Exchange(ref verification, check); await check.Task.WaitAsync(token);
                await Task.Delay(TimeSpan.FromSeconds(warmup), token);
                var started = Stopwatch.GetTimestamp(); await Task.Delay(TimeSpan.FromSeconds(interval), token);
                windows.Add(new(name, width, height, started, Stopwatch.GetTimestamp()));
                Probe.Check(host.Frames.TransportError == null && !window.Completion.IsCompleted, "Viewport faulted during measurement.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await host.Editor.StopAsync(); await app.StopAsync(); await app.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            Environment.SetEnvironmentVariable(BaselineCapture.EnvironmentKey, previousEnvironment);
            await capture.SaveAsync(directory, "baseline-editor.json");
            try
            {
                var childFiles = Directory.GetFiles(directory, "baseline-runtime-*.json").Where(path => !path.EndsWith(".state.json", StringComparison.Ordinal)).ToArray();
                var children = childFiles.Select(path => JsonSerializer.Deserialize<CaptureData>(File.ReadAllText(path))!).ToArray();
                if (failure == null)
                {
                    Probe.Check(children.Length == 2 && windows.Count == 3 && pixelChecks == 3 && (binding?.ImportReadbacks ?? 0) == 0, "Missing baseline data or unexpected import readback.");
                    results = windows.Select(w => BaselineAnalysis.Analyze(w, capture.Snapshot(), children, gpu)).ToArray();
                    Probe.Check(results.All(r => r.Produced >= 3 && r.Presented >= 3 && r.Received >= r.Presented && r.EditorResources.Available && r.RuntimeResources.Length == 1), "Insufficient matched frames/resource samples.");
                    Probe.Check(host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && host.Frames.MappingCount == 0 && sessions.ActiveSessionCount == 0 &&
                        factory.Leases.All(l => l.ResourcesReleased && l.Reaped) && view is { IsDisposed: true }, "Baseline cleanup incomplete.");
                    Probe.Check(!factory.Diagnostics.Any(d => d.Stage == TrueMoon.Mithril.UnitProcessStage.Killing ||
                        d.Stage == TrueMoon.Mithril.UnitProcessStage.ProcessReleased && d.ExitCode != 0), "Normal baseline child required a forced/failed exit.");
                    Probe.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor loaded game code.");
                    completed = true;
                }
            }
            catch (Exception analysis) { failure = failure == null ? analysis : new AggregateException(failure, analysis); }
            Records.Write(directory, "baseline-report.json", new
            {
                Completed = completed, Mode = mode, Windows = results, Operations = operations, Artifacts = artifacts,
                LifecycleBudgets = operations.Select(o => new { o.Name, Milliseconds = (o.Ended - o.Started) * 1000d / Stopwatch.Frequency,
                    WithinBudget = o.Succeeded && (o.Ended - o.Started) * 1000d / Stopwatch.Frequency <= 15000 }).ToArray(),
                Budgets = results.Select(r => new { r.Window.Name, Fps = r.PresentedFps >= 24, Latency = r.ProduceToPresent.P95Ms <= 100,
                    EditorAllocation = r.EditorResources.AllocatedMiBPerSecond <= 32, RuntimeAllocation = r.RuntimeResources.All(p => p.AllocatedMiBPerSecond <= 256) }).ToArray(),
                Import = BaselineAnalysis.Summarize(capture.Snapshot().Frames.Where(f => f.Phase == "import").Select(f => (f.Ended - f.Started) * 1000d / Stopwatch.Frequency)),
                GpuResources = host.Gpu.ResourceCount, GpuHandles = host.Gpu.OpenHandleCount, Mappings = host.Frames.MappingCount, ActiveUiSessions = sessions.ActiveSessionCount,
                ImportReadbacks = binding?.ImportReadbacks ?? 0, PixelChecks = pixelChecks,
                GpuAdapter = binding?.Adapter,
                Children = factory.Leases.Select(l => new { l.ProcessId, l.Identity.Generation, l.Reaped, l.ResourcesReleased, l.StandardError }).ToArray(),
                Diagnostics = factory.Diagnostics.Select(d => new { Stage = d.Stage.ToString(), d.ProcessId, d.Reason, d.ExitCode }).ToArray(),
                GameLoadedInEditor = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"),
                Error = failure?.ToString()
            });
        }
        if (failure != null) throw failure;
        return 0;
    }
}
