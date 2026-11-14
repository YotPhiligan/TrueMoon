using TrueMoon.Aether.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Abstractions.Checkpoints;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Gpu.OpenGl;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class MeshSurfaceData
{
    public string Title => "Aether mesh-view/1 — indexed opaque unlit surface";
}
public sealed class MeshSurfaceCanvas : Element, IDisposable
{
    private SkiaImageSource? _image;
    private RenderSnapshotStamp? _stamp;
    public RenderSnapshotStamp? Drawn { get; private set; }
    public void Upload(byte[] rgba, int width, int height, RenderSnapshotStamp stamp)
    {
        VerifyAccess(); var bgra = rgba.ToArray();
        for (var i = 0; i < bgra.Length; i += 4) (bgra[i], bgra[i + 2]) = (bgra[i + 2], bgra[i]);
        var next = SkiaImageSource.CopyBgraPixels(width, height, bgra);
        var old = _image; _image = next; _stamp = stamp; Drawn = null; old?.Dispose(); Invalidate();
    }
    protected override Size MeasureCore(Size available, ITextLayoutService text) => new(640, 360);
    protected override void DrawCore(IDrawingContext context)
    {
        if (_image != null) { context.Image(_image, Bounds); Drawn = _stamp; }
    }
    public override void Dispose() { VerifyAccess(); _image?.Dispose(); _image = null; base.Dispose(); }
}
public sealed class MeshSurfaceView : View<MeshSurfaceData>
{
    public MeshSurfaceCanvas Canvas { get; } = new();
    public VStack Stack { get; }
    public MeshSurfaceView(MeshSurfaceData data, IViewStyle<MeshSurfaceView> style)
    {
        Stack = VStack.Create().WithChildren(Text.Plain(data.Title), Canvas);
        Content(data, _ => Stack); this.WithViewStyle(style);
    }
}
public sealed class MeshSurfaceStyle : IViewStyle<MeshSurfaceView>
{
    public void Configure(IViewStyleBuilder<MeshSurfaceView> styles) => styles.Part(view => view.Stack).Padding(16).Spacing(8);
}

// A diagnostic custom host: readback is displayed by Skia, and the exact stamp is
// acknowledged only after DrawCore and a real HostedUiWindow.FramePresented event.
public static class MeshSurfaceProbe
{
    public static async Task<int> RunAsync(string runtimePath, string gameDirectory, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var runtime = RuntimeCommand(runtimePath); var hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
        var factory = new NativeRuntimeFactory(runtime); var captures = new List<object>(); var cleanups = new List<JsonElement>();
        var validation = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1";
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        MeshSurfaceView view = null!;
        await using var app = App.Build(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId; options.RuntimeLaunch = runtime;
                options.RuntimeFactory = _ => factory; options.ReportDirectory = reportDirectory;
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(30), Command = TimeSpan.FromSeconds(10) };
            });
            context.UsePresentationFactory<MeshSurfaceView>(options =>
            {
                options.UseSkiaOpenGL(fonts); options.UseSilkWindow(window =>
                { window.Width = 720; window.Height = 460; window.Title = "Aether mesh surface and Playing fence"; window.EnableDiagnostics = true; });
            });
            context.Services(services =>
            {
                services.Singleton<MeshSurfaceData>(_ => new()); services.Singleton<IViewStyle<MeshSurfaceView>>(_ => new MeshSurfaceStyle());
                services.Singleton<IFactory<MeshSurfaceView>>(resolver =>
                {
                    var data = resolver.Resolve<MeshSurfaceData>(); var style = resolver.Resolve<IViewStyle<MeshSurfaceView>>();
                    return new Factory<MeshSurfaceView>(() => view = new(data, style));
                });
            });
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var ui = app.Services.Resolve<HostedUiSessionFactory>()!;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource? presenting = null; RenderSnapshotStamp? expected = null;
        window.FramePresented += _ => { shown.TrySetResult(); if (expected != null && view.Canvas.Drawn == expected) presenting?.TrySetResult(); };
        window.Closing += async _ => { await host.DisposeAsync(); return true; };
        var ownerThread = 0; Action makeCurrent = null!;
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == ownerThread) { makeCurrent(); action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { try { makeCurrent(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } });
            return completion.Task;
        }
        Exception? failure = null; GpuAdapterId? adapter = null; var failedStartup = false; var replaceRejected = false;
        try
        {
            Check(validation, "Enable Vulkan core/synchronization validation for this proof."); Check(!GameLoaded(), "Parent loaded selected game.");
            var build = GameArtifactPublisher.Publish(gameDirectory, Path.Combine(reportDirectory, "artifacts"), hostId,
                "RenderSurface.Game.dll", "RenderSurface.Game.MeshGame", 41, runtimeAccess: true);
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            GpuOpenGlContext glContext = null!; var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(value =>
            {
                try
                {
                    ownerThread = Environment.CurrentManagedThreadId; var gl = value.NativeWindow.GLContext!; makeCurrent = gl.MakeCurrent; makeCurrent();
                    nint Resolve(string name) => gl.TryGetProcAddress(name, out var address) ? address : 0;
                    glContext = new(Resolve, makeCurrent, Owner);
                    var capabilities = GpuCapabilityCollector.OpenGl(new(Guid.NewGuid(), 1, hostId, build.BuildId, new('A', 64)), Resolve).Capabilities;
                    Check(capabilities.Error == null && capabilities.Devices.Count == 1 && capabilities.Features is { RequiredProcedures: true, OptimalTiling: true }, "GL external surface profile unavailable.");
                    adapter = capabilities.Devices[0]; initialized.TrySetResult();
                }
                catch (Exception error) { initialized.TrySetException(error); }
            });
            await initialized.Task.WaitAsync(token);
            Guid? previousWorld = null;
            for (var generation = 1; generation <= 2; generation++)
            {
                await host.Supervisor.StartAsync(new SceneSnapshot("mesh-surface", 41, []), build, token: token);
                Check(host.Supervisor.RequiredRenderViewVersion == 1 && !(await host.Supervisor.GetStatusAsync(token)).RenderViewReady, "CPU activation claimed mesh readiness.");
                var lease = factory.Leases[^1];
                await using (var surface = await host.Gpu.ConnectSurfaceAsync(lease, adapter!, glContext, token))
                {
                    await surface.ConfigureAsync(320, 180, token);
                    var initial = await Capture(surface, "initial", 255, 255, true, token);
                    Check(initial.Stamp.WorldId != previousWorld, "New game generation reused World identity."); previousWorld = initial.Stamp.WorldId;
                    await host.Supervisor.SetPlayingAsync(true, token); Check(host.Supervisor.Status.State == SessionState.Playing, "Presented view did not admit Playing.");
                    await host.Supervisor.SetPlayingAsync(false, token);
                    if (generation == 1)
                    {
                        await Input(GameButtons.Forward); var tint = await Capture(surface, "tint", 188, 255, false, token, redFirst: true);
                        Check(initial.Stamp.ManifestHash == tint.Stamp.ManifestHash && initial.Stamp.BindingRevision == tint.Stamp.BindingRevision,
                            "Per-entity tint created a new shared material binding.");
                        await Input(GameButtons.Backward); await Capture(surface, "reset", 255, 255, false, token);
                        await Input(GameButtons.Left); var grey = await Capture(surface, "material-reload", 128, 128, true, token);
                        Check(grey.Stamp.BindingRevision > tint.Stamp.BindingRevision && grey.Stamp.ManifestHash != tint.Stamp.ManifestHash, "Shared material reload did not publish a fresh binding.");
                        await Input(GameButtons.Right); await Capture(surface, "delete", -1, 128, false, token);
                        await Input(GameButtons.None, interact: true); var small = await Capture(surface, "mesh-reload", -1, 128, true, token);
                        Check(small.RightPixels < grey.RightPixels / 2 && small.RightPixels > 20, "Reloaded indexed geometry did not shrink independently of the material.");
                        await host.Supervisor.SetPlayingAsync(true, token);
                        await surface.ConfigureAsync(384, 216, token); Check(!(await host.Supervisor.GetStatusAsync(token)).RenderViewReady, "Resize kept a retired presentation fence.");
                        await Capture(surface, "resize", -1, 128, true, token);
                        Check(host.Supervisor.Status.State == SessionState.Playing, "Resize did not resume after actual presentation.");
                        await surface.ConfigureAsync(0, 0, token);
                        Check((await host.Supervisor.GetStatusAsync(token)).Phase == "Paused", "Suspended required view kept ticking.");
                        await surface.ConfigureAsync(320, 180, token); await Capture(surface, "resume", -1, 128, true, token);
                        Check(host.Supervisor.Status.State == SessionState.Playing, "Resume ignored the new presentation fence.");
                        await host.Supervisor.SetPlayingAsync(false, token);
                        var identity = host.Supervisor.Status.Identity!;
                        try { await host.Replacement.ReplaceAsync(new(build, identity, new("mesh-surface", 41, []), 41), token); }
                        catch (GameReplacementException error) when (error.ToString().Contains(nameof(CheckpointOutcome.Unsupported), StringComparison.Ordinal)) { replaceRejected = true; }
                        Check(replaceRejected && host.Supervisor.Status.Identity == identity && host.Supervisor.Status.State == SessionState.Paused && !lease.Reaped,
                            "Unsupported mesh checkpoint Replace changed the working generation.");
                        await Capture(surface, "replace-rejected", -1, 128, false, token);
                    }
                    Check(surface.Slots.All(slot => slot.Phase == "Idle"), "Completed readback retained surface use.");
                }
                await host.Supervisor.StopAsync();
                Check(lease.Reaped && lease.ResourcesReleased && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0, "Stop retained native runtime ownership.");
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reportDirectory, $"runtime-{generation}.json"), token));
                var cleanup = document.RootElement; var producer = cleanup.GetProperty("Validation");
                Check(cleanup.GetProperty("Error").ValueKind == JsonValueKind.Null && !cleanup.GetProperty("UiLoaded").GetBoolean() && !cleanup.GetProperty("EditorLoaded").GetBoolean(), "Runtime cleanup or assembly isolation failed.");
                Check(producer.GetProperty("ValidationEnabled").GetBoolean() && producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 &&
                    producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 &&
                    producer.GetProperty("SlotsCreated").GetInt32() == producer.GetProperty("SlotsReleased").GetInt32() &&
                    producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32(), "Vulkan validation or surface teardown failed.");
                var resources = producer.GetProperty("MeshResources").EnumerateArray().ToArray();
                Check(resources.Length > 0 && resources.All(resource => resource.GetProperty("DevicesCreated").GetInt32() == 0 && resource.GetProperty("LiveUploads").GetInt32() == 0 &&
                    resource.GetProperty("BuffersCreated").GetInt32() == resource.GetProperty("BuffersReleased").GetInt32() &&
                    resource.GetProperty("AllocationsCreated").GetInt32() == resource.GetProperty("AllocationsReleased").GetInt32() && resource.GetProperty("Uploads").GetInt32() >= 1),
                    "Mesh resources created a separate VkDevice or retained buffers/allocations.");
                cleanups.Add(cleanup.Clone()); Check(!GameLoaded(), "Selected game leaked into parent CLR.");
            }
            var broken = GameArtifactPublisher.Publish(gameDirectory, Path.Combine(reportDirectory, "artifacts"), hostId,
                "RenderSurface.Game.dll", "RenderSurface.Game.FailedMeshGame", 41, runtimeAccess: true);
            await host.Supervisor.StartAsync(new SceneSnapshot("mesh-surface", 41, []), broken, token: token);
            var failedLease = factory.Leases[^1];
            await using (var failed = await host.Gpu.ConnectSurfaceAsync(failedLease, adapter!, glContext, token))
            {
                await failed.ConfigureAsync(320, 180, token);
                Check(!(await host.Supervisor.GetStatusAsync(token)).RenderViewReady, "Failed startup was already render-ready.");
                try { await failed.ProduceFrameAsync(0, token); }
                catch (InvalidDataException) { failedStartup = true; }
                Check(failedStartup, "Invalid required camera unexpectedly produced a render-ready frame.");
            }
            await host.Supervisor.StopAsync();
            using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reportDirectory, "runtime-3.json"), token)))
            {
                var cleanup = document.RootElement; var producer = cleanup.GetProperty("Validation");
                Check(cleanup.GetProperty("Error").ValueKind == JsonValueKind.String && producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 &&
                    producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 &&
                    producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32() &&
                    failedLease.Reaped && failedLease.ResourcesReleased, "Failed startup lost its primary error or retained GPU/runtime owners.");
                Check(producer.GetProperty("MeshResources").EnumerateArray().Any(resource => resource.GetProperty("Uploads").GetInt32() == 1 &&
                    resource.GetProperty("LiveUploads").GetInt32() == 0 && resource.GetProperty("BuffersCreated").GetInt32() == resource.GetProperty("BuffersReleased").GetInt32()),
                    "Failed-view proof did not exercise cleanup after a completed native upload.");
                cleanups.Add(cleanup.Clone());
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try
            {
                await host.Gpu.DisposeAsync(); await host.Supervisor.StopAsync(); window.Close();
                await window.Completion.WaitAsync(TimeSpan.FromSeconds(20)); await app.StopAsync();
                Check(ui.ActiveSessionCount == 0 && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased), "Final App/native ownership retained.");
            }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            var report = new { Completed = failure == null && cleanups.Count == 3 && failedStartup && replaceRejected, Scenario = "mesh-surface-playing-fence", ValidationRequested = validation,
                FailedStartupObserved = failedStartup, UnsupportedReplacePreservedGeneration = replaceRejected,
                Adapter = adapter, Captures = captures, RuntimeCleanup = cleanups, GameLoadedInParent = GameLoaded(), Resources = host.Gpu.ResourceCount,
                Handles = host.Gpu.OpenHandleCount, ActiveUiSessions = ui.ActiveSessionCount,
                Children = factory.Leases.Select(lease => new { lease.ProcessId, lease.Reaped, lease.ResourcesReleased }), Error = failure?.ToString() };
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        return failure == null && cleanups.Count == 3 && failedStartup && replaceRejected ? 0 : 1;

        async Task Input(GameButtons buttons, bool interact = false)
        {
            await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(true, buttons, InteractPressed: interact), token);
            await host.Supervisor.StepAsync(token); await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(false), token);
        }
        async Task<(RenderSnapshotStamp Stamp, int RightPixels)> Capture(GpuSurfaceSession surface, string phase, int leftValue, int rightValue,
            bool requireFence, CancellationToken captureToken, bool redFirst = false)
        {
            var produced = await surface.ProduceFrameAsync(captures.Count % 3, captureToken);
            var stamp = produced.Metadata?.RenderView ?? throw new InvalidDataException("Missing mesh-view/1 stamp."); stamp.Validate();
            var before = await host.Supervisor.GetStatusAsync(captureToken);
            if (requireFence) Check(!before.RenderViewReady && before.Phase == "Paused", "GPU production claimed actual presentation.");
            var read = await surface.ReadAsync(produced.Completion, captureToken); var offer = surface.Surface!; var width = offer.Slots[0].Width; var height = offer.Slots[0].Height;
            Check(read.Pixels.Length == width * height * 4, "Readback extent mismatch.");
            var left = 0; var right = 0;
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                if (x < width / 2 && leftValue >= 0 && Near(read.Pixels, offset, leftValue, redFirst ? 0 : leftValue, redFirst ? 0 : leftValue)) left++;
                if (x > width / 2 && Near(read.Pixels, offset, rightValue, rightValue, rightValue)) right++;
            }
            Check((leftValue < 0 ? CountGeometry(read.Pixels, width, height, true) == 0 : left > 20) && right > 20 && left + right < width * height / 3,
                $"Native mesh color/geometry mismatch in {phase}: left={left}, right={right}.");
            using (var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque)))
            {
                Marshal.Copy(read.Pixels, 0, bitmap.GetPixels(), read.Pixels.Length);
                using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(reportDirectory, $"mesh-{factory.Leases.Count}-{phase}.png")); png.SaveTo(file);
            }
            var displayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Owner(() => { expected = stamp; presenting = displayed; view.Canvas.Upload(read.Pixels, width, height, stamp); });
            await displayed.Task.WaitAsync(captureToken);
            await host.Supervisor.AcknowledgeRenderViewAsync(stamp, captureToken);
            Check((await host.Supervisor.GetStatusAsync(captureToken)).RenderViewReady, "Actual mesh presentation did not open Playing fence.");
            await surface.ReturnAsync(read.Completion, captureToken);
            captures.Add(new { Generation = factory.Leases.Count, Phase = phase, Width = width, Height = height, produced.Metadata,
                LeftPixels = left, RightPixels = right, RequireFence = requireFence, Presented = true, Sha256 = Convert.ToHexString(SHA256.HashData(read.Pixels)) });
            return (stamp, right);
        }
    }
    private static int CountGeometry(byte[] pixels, int width, int height, bool left)
    {
        var count = 0; for (var y = 0; y < height; y++) for (var x = 0; x < width / 2; x++)
        { var i = (y * width + (left ? x : x + width / 2)) * 4; if (pixels[i] > 50 || pixels[i + 1] > 50 || pixels[i + 2] > 50) count++; }
        return count;
    }
    private static bool Near(byte[] pixels, int offset, int red, int green, int blue) => Math.Abs(pixels[offset] - red) <= 2 && Math.Abs(pixels[offset + 1] - green) <= 2 && Math.Abs(pixels[offset + 2] - blue) <= 2 && pixels[offset + 3] == 255;
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "RenderSurface.Game");
    private static NativeRuntimeLaunch RuntimeCommand(string path)
    {
        path = Path.GetFullPath(path);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return new(path, Path.ChangeExtension(path, ".dll"));
        var dotnet = Environment.ProcessPath ?? throw new InvalidOperationException("Parent executable path missing.");
        if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetFileNameWithoutExtension(dotnet), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Launch the proof through dotnet and select a runtime DLL or apphost.", nameof(path));
        return new(dotnet, path, [path]);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
