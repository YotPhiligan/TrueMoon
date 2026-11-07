using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Gpu.OpenGl;
using TrueMoon.Aether.Gpu.Surfaces;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

public sealed class PrimitiveNativeData
{
    public string Title { get; } = "ECS primitive extraction — Vulkan surface proof";
    public string Description { get; } = "Reading native red/green geometry before and after one movement tick.";
}

public sealed class PrimitiveNativeView : View<PrimitiveNativeData>
{
    public VStack Stack { get; }
    public PrimitiveNativeView(PrimitiveNativeData data, IViewStyle<PrimitiveNativeView> style)
    {
        ArgumentNullException.ThrowIfNull(data); ArgumentNullException.ThrowIfNull(style);
        Stack = VStack.Create().WithChildren(Text.Plain(data.Title), Text.Plain(data.Description));
        Content(data, _ => Stack); this.WithViewStyle(style);
    }
}

public sealed class PrimitiveNativeViewStyle : IViewStyle<PrimitiveNativeView>
{
    public void Configure(IViewStyleBuilder<PrimitiveNativeView> styles)
        => styles.Part(view => view.Stack).Padding(16).Spacing(4);
}

// Uses the existing Vulkan exporter/GL import owner; it introduces no GPU or UI product API.
public static class PrimitiveNativeProbe
{
    private const int Width = 320, Height = 180;

    public static async Task<int> RunAsync(string runtimePath, string gameDirectory, string reportDirectory)
    {
        Directory.CreateDirectory(reportDirectory);
        var runtime = PrimitiveRuntimeProbe.RuntimeCommand(runtimePath); var hostId = BuildIdentity.ForRuntime(runtime.RuntimeAssemblyPath);
        var factory = new NativeRuntimeFactory(runtime); var captures = new List<object>(); var cleanups = new List<JsonElement>();
        var validation = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1";
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId;
                options.RuntimeLaunch = runtime; options.RuntimeFactory = _ => factory; options.ReportDirectory = reportDirectory;
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(30), Command = TimeSpan.FromSeconds(10) };
            });
            context.UsePresentationFactory<PrimitiveNativeView>(options =>
            {
                options.UseSkiaOpenGL(fonts); options.UseSilkWindow(window =>
                { window.Width = 760; window.Height = 180; window.Title = "TrueMoon ECS primitive Vulkan proof"; window.EnableDiagnostics = true; });
            });
            context.Services(services =>
            {
                services.Singleton<PrimitiveNativeData>(_ => new());
                services.Singleton<IViewStyle<PrimitiveNativeView>>(_ => new PrimitiveNativeViewStyle());
                services.Singleton<IFactory<PrimitiveNativeView>>(resolver =>
                {
                    var data = resolver.Resolve<PrimitiveNativeData>(); var style = resolver.Resolve<IViewStyle<PrimitiveNativeView>>();
                    return new Factory<PrimitiveNativeView>(() => new(data, style));
                });
            });
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var uiSessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += _ => shown.TrySetResult();
        window.Closing += async _ => { await host.DisposeAsync(); return true; };
        var ownerThread = 0; Action makeCurrent = null!;
        Task Owner(Action action)
        {
            if (Environment.CurrentManagedThreadId == ownerThread) { makeCurrent(); action(); return Task.CompletedTask; }
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            window.PostWindow(_ => { try { makeCurrent(); action(); completion.TrySetResult(); } catch (Exception error) { completion.TrySetException(error); } });
            return completion.Task;
        }
        Exception? failure = null; GpuAdapterId? adapter = null;
        try
        {
            Check(validation, "Run this proof with TRUEMOON_GPU_VALIDATION=1 to prove core/synchronization validation.");
            Check(!GameLoaded(), "Native proof parent loaded selected game CLR types.");
            var build = GameBuildArtifact.Publish(gameDirectory, Path.Combine(reportDirectory, "artifacts"), hostId,
                "Game.Logic.dll", "Game.Logic.PrimitiveProbeGame", 31);
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            GpuOpenGlContext glContext = null!;
            window.PostWindow(value =>
            {
                try
                {
                    ownerThread = Environment.CurrentManagedThreadId; var gl = value.NativeWindow.GLContext!;
                    makeCurrent = gl.MakeCurrent; makeCurrent();
                    nint Resolve(string name) => gl.TryGetProcAddress(name, out var address) ? address : 0;
                    glContext = new(Resolve, makeCurrent, Owner);
                    var capabilities = GpuCapabilityCollector.OpenGl(new(Guid.NewGuid(), 1, hostId, build.BuildId, new('A', 64)), Resolve).Capabilities;
                    Check(capabilities.Error == null && capabilities.Devices.Count == 1 && capabilities.Features is { RequiredProcedures: true, OptimalTiling: true },
                        "Current GL owner does not support the existing Vulkan external-surface profile.");
                    adapter = capabilities.Devices[0]; initialized.TrySetResult();
                }
                catch (Exception error) { initialized.TrySetException(error); }
            });
            await initialized.Task.WaitAsync(token);
            Guid? previousWorld = null, previousRegistry = null;
            for (var generation = 1; generation <= 2; generation++)
            {
                await host.Supervisor.StartWorldAsync(PrimitiveRuntimeProbe.CreateScene(), build, token: token);
                var status = host.Supervisor.Status; var lease = factory.Leases[^1];
                Check(status.State == SessionState.Paused && status.SceneEntityCount == PrimitiveRuntimeProbe.LogicalEntityCount &&
                    status.SceneComponentCount == PrimitiveRuntimeProbe.SourceComponentCount && status.SceneRevision == 31,
                    "Native primitive game did not prime the entire logical hierarchy.");
                Check(status.SceneWorldId != null && status.SceneRegistryId != null && status.SceneWorldId != previousWorld && status.SceneRegistryId != previousRegistry,
                    "Native generation reused primitive World/registry identity.");
                previousWorld = status.SceneWorldId; previousRegistry = status.SceneRegistryId;
                await using (var surface = await host.Gpu.ConnectSurfaceAsync(lease, adapter!, glContext, token))
                {
                    await surface.ConfigureAsync(Width, Height, token);
                    var before = await Capture(surface, 0, 0, generation, token);
                    Check((await host.Supervisor.StepAsync(token)).Tick == 1, "Native selected game failed to execute one ECS tick.");
                    var after = await Capture(surface, 1, 1, generation, token);
                    Check(after.RedCentroid > before.RedCentroid + 15,
                        "Actual Vulkan red geometry did not move right after parent-coordinate velocity integration.");
                    Check(Math.Abs(after.GreenCentroid - before.GreenCentroid) < 2,
                        "Movement of red entity changed stationary green geometry.");
                    Check(after.Hash != before.Hash && before.RedPixels > 100 && after.RedPixels > 100 && before.GreenPixels > 100 && after.GreenPixels > 100,
                        "Native frames lost source appearances or did not change geometry.");
                    Check(surface.Slots.All(slot => slot.Phase == "Idle"), "Readback failed to retire a native surface slot.");
                }
                await host.Supervisor.StopAsync();
                Check(lease.Reaped && lease.ResourcesReleased && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0,
                    "Stop retained runtime, imports or external handles.");
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reportDirectory, $"runtime-{generation}.json"), token));
                var cleanup = document.RootElement; var producer = cleanup.GetProperty("Validation");
                Check(cleanup.GetProperty("Error").ValueKind == JsonValueKind.Null && cleanup.GetProperty("Resources").GetInt32() == 0 &&
                    cleanup.GetProperty("Handles").GetInt32() == 0 && !cleanup.GetProperty("UiLoaded").GetBoolean() && !cleanup.GetProperty("EditorLoaded").GetBoolean(),
                    "Runtime cleanup/assembly isolation report failed.");
                Check(producer.GetProperty("ValidationEnabled").GetBoolean() && producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 &&
                    producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 &&
                    producer.GetProperty("SlotsCreated").GetInt32() == producer.GetProperty("SlotsReleased").GetInt32() &&
                    producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32(),
                    "Vulkan core/synchronization validation or native teardown failed.");
                cleanups.Add(cleanup.Clone());
                Check(!GameLoaded(), "Selected game entered parent CLR during native rendering.");
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try
            {
                await host.Gpu.DisposeAsync(); await host.Supervisor.StopAsync();
                window.Close(); await window.Completion.WaitAsync(TimeSpan.FromSeconds(20)); await app.StopAsync();
                Check(uiSessions.ActiveSessionCount == 0 && host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && host.Frames.MappingCount == 0 &&
                    factory.Leases.All(lease => lease.Reaped && lease.ResourcesReleased), "Final GL/UI/runtime ownership was not released.");
            }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            var report = new { Completed = failure == null && captures.Count == 4 && cleanups.Count == 2, Scenario = "ecs-primitive-vulkan-native",
                ValidationRequested = validation, Adapter = adapter, Captures = captures, RuntimeCleanup = cleanups,
                GameLoadedInParent = GameLoaded(), Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount,
                Mappings = host.Frames.MappingCount, ActiveUiSessions = uiSessions.ActiveSessionCount,
                Children = factory.Leases.Select(lease => new { lease.ProcessId, lease.Reaped, lease.ResourcesReleased }), Error = failure?.ToString() };
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        return failure == null && captures.Count == 4 && cleanups.Count == 2 ? 0 : 1;

        async Task<(double RedCentroid, double GreenCentroid, int RedPixels, int GreenPixels, string Hash)> Capture(
            GpuSurfaceSession surface, int slot, ulong tick, int generation, CancellationToken captureToken)
        {
            var produced = await surface.ProduceFrameAsync(slot, captureToken);
            Check(produced.Metadata != null && produced.Metadata.Tick == tick && produced.Metadata.Slot == slot,
                "Native primitive frame metadata does not match the completed simulation tick.");
            var read = await surface.ReadAsync(produced.Completion, captureToken);
            Check(read.Pixels.Length == Width * Height * 4 && read.Completion == produced.Completion, "Native readback length/identity mismatch.");
            var pixels = read.Pixels; var red = 0; var green = 0; double redX = 0, greenX = 0;
            Check(Near(pixels, 0, 10, 15, 26) && Near(pixels, (Width * Height - 1) * 4, 10, 15, 26), "Native clear/opacity mismatch.");
            for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
            {
                var offset = (y * Width + x) * 4;
                if (Near(pixels, offset, 255, 0, 0)) { red++; redX += x; }
                if (Near(pixels, offset, 0, 255, 0)) { green++; greenX += x; }
            }
            Check(red is > 100 and < Width * Height / 3 && green is > 100 and < Width * Height / 3,
                "Expected two bounded primary-color front faces, not a background/fullscreen frame.");
            var retired = await surface.ReturnAsync(read.Completion, captureToken);
            Check(retired == produced.Completion, "Native return acknowledgement changed slot identity.");
            var hash = Convert.ToHexString(SHA256.HashData(pixels)); var file = $"primitive-{generation}-{tick}.rgba";
            await File.WriteAllBytesAsync(Path.Combine(reportDirectory, file), pixels, captureToken);
            captures.Add(new { Generation = generation, Tick = tick, produced.Metadata, produced.Completion,
                RedPixels = red, GreenPixels = green, RedCentroid = redX / red, GreenCentroid = greenX / green, Sha256 = hash, File = file });
            return (redX / red, greenX / green, red, green, hash);
        }
    }
    private static bool Near(byte[] pixels, int offset, int red, int green, int blue)
        => Math.Abs(pixels[offset] - red) <= 2 && Math.Abs(pixels[offset + 1] - green) <= 2 && Math.Abs(pixels[offset + 2] - blue) <= 2 && pixels[offset + 3] == 255;
    private static bool GameLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "Game.Logic");
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
