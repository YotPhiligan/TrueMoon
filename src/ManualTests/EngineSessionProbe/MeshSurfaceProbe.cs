using TrueMoon.Argentis.Layout;
using TrueMoon.Argentis.Painting;
using TrueMoon.Argentis.Styling;
using TrueMoon.Argentis.Views;
using TrueMoon.Aether.Runtime;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Abstractions.Checkpoints;
using TrueMoon.Aether.Abstractions.Input;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Nom;
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
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Nom;

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
    internal IImageSource? Image => _image;
    internal RenderSnapshotStamp? Stamp => _stamp;
    internal void ReportDrawn(RenderSnapshotStamp? stamp) => Drawn = stamp;
    public override void Dispose() { VerifyAccess(); _image?.Dispose(); _image = null; base.Dispose(); }
}
public sealed class MeshSurfaceView : View<MeshSurfaceData>
{
    public MeshSurfaceCanvas Canvas { get; } = new();
    public VStack Stack { get; }
    public MeshSurfaceView(MeshSurfaceData data, IViewStyle<MeshSurfaceView> style)
    {
        Stack = new VStack().WithChildren(new Text(data.Title), Canvas);
        DataContext = data;
        SetContent(Stack); this.WithViewStyle(style);
    }
}
public sealed class MeshSurfaceStyle : IViewStyle<MeshSurfaceView>
{
    public void Configure(IViewStyleBuilder<MeshSurfaceView> styles) => styles.Part(view => view.Stack).Padding(16).Spacing(8);
}

// A diagnostic custom host: readback is displayed by Skia, and the exact stamp is
// acknowledged only after the presenter draws the image and a real HostedUiWindow.FramePresented event fires.
public static partial class MeshSurfaceProbe
{
    private static void ConfigurePresenters(UiPresenters presenters) =>
        presenters.Register<MeshSurfaceCanvas>(static (canvas, context) =>
        {
            if (canvas.Image is not { } image) return;
            var stamp = canvas.Stamp;
            context.Image(image, canvas.Bounds);
            canvas.ReportDrawn(stamp);
        });

    public static async Task<int> RunAsync(string runtimePath, string gameDirectory, string reportDirectory,
        string? textureGameProject = null, string? workerDirectory = null)
    {
        var textureProof = textureGameProject != null;
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
            if (workerDirectory != null)
            {
                context.UseNom();
                context.UseAetherModelSourceCapture();
                context.UseAetherModelImport();
                context.UseAetherNomContent();
            }
            context.UseAether(options =>
            {
                options.HostContractId = hostId; options.GameBuildId = BuildIdentity.PreviewId; options.RuntimeLaunch = runtime;
                options.RuntimeFactory = _ => factory; options.ReportDirectory = reportDirectory;
                options.BuildDirectory = Path.Combine(reportDirectory, "artifacts");
                options.Deadlines = new() { Startup = TimeSpan.FromSeconds(30), Command = TimeSpan.FromSeconds(10) };
            });
            context.UsePresentationFactory<MeshSurfaceView>(options =>
            {
                options.ConfigurePresenters(ConfigurePresenters).UseSkiaOpenGL(fonts); options.UseSilkWindow(window =>
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
        GpuOpenGlContext glContext = null!;
        TextureBuild[]? textureBuilds = null;
        object? importEvidence = null;
        var textureFrames = new List<string>();
        bool Complete() => failure == null && cleanups.Count == 3 && (textureProof ? textureFrames.Count == 3 : failedStartup && replaceRejected);
        try
        {
            Check(validation, "Enable Vulkan core/synchronization validation for this proof."); Check(!GameLoaded(), "Parent loaded selected game.");
            await app.StartAsync(token); await shown.Task.WaitAsync(token);
            if (textureGameProject != null)
            {
                if (workerDirectory == null) textureBuilds = await BuildTextureCases(textureGameProject, host.Builds, token);
                else (textureBuilds, importEvidence) = await BuildImportedTextureCases(
                    textureGameProject, workerDirectory, reportDirectory, app.Services, host.Builds, token);
            }
            var build = textureBuilds?[0].Artifact ??
                GameArtifactPublisher.Publish(gameDirectory, Path.Combine(reportDirectory, "artifacts"), hostId,
                    "RenderSurface.Game.dll", "RenderSurface.Game.MeshGame", 41, runtimeAccess: true);
            var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            for (var generation = 1; generation <= (textureProof ? 3 : 2); generation++)
            {
                var textureCase = textureBuilds?[generation == 2 ? 1 : 0];
                var activeBuild = textureCase?.Artifact ?? build;
                await host.Supervisor.StartAsync(new SceneSnapshot("mesh-surface", 41, []), activeBuild, token: token);
                Check(host.Supervisor.RequiredRenderViewVersion == 1 && !(await host.Supervisor.GetStatusAsync(token)).RenderViewReady, "CPU activation claimed mesh readiness.");
                var lease = factory.Leases[^1];
                await using (var surface = await host.Gpu.ConnectSurfaceAsync(lease, adapter!, glContext, token))
                {
                    await surface.ConfigureAsync(320, 180, token);
                    var initial = await Capture(surface, "initial", 255, 255, true, token, textureCase: textureCase);
                    Check(initial.Stamp.WorldId != previousWorld, "New game generation reused World identity."); previousWorld = initial.Stamp.WorldId;
                    if (textureCase != null) textureFrames.Add(initial.FrameHash);
                    await host.Supervisor.SetPlayingAsync(true, token); Check(host.Supervisor.Status.State == SessionState.Playing, "Presented view did not admit Playing.");
                    await host.Supervisor.SetPlayingAsync(false, token);
                    if (!textureProof && generation == 1)
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
                Check(cleanup.GetProperty("GameBuildId").GetString() == activeBuild.BuildId, "Runtime started the wrong build.");
                Check(producer.GetProperty("ValidationEnabled").GetBoolean() && producer.GetProperty("Errors").GetInt32() == 0 && producer.GetProperty("Warnings").GetInt32() == 0 &&
                    producer.GetProperty("LiveSlots").GetInt32() == 0 && producer.GetProperty("OpenHandles").GetInt32() == 0 &&
                    producer.GetProperty("SlotsCreated").GetInt32() == producer.GetProperty("SlotsReleased").GetInt32() &&
                    producer.GetProperty("DevicesCreated").GetInt32() == producer.GetProperty("DevicesReleased").GetInt32(), "Vulkan validation or surface teardown failed.");
                var resources = producer.GetProperty("MeshResources").EnumerateArray().ToArray();
                Check(resources.Length > 0 && resources.All(resource => resource.GetProperty("DevicesCreated").GetInt32() == 0 && resource.GetProperty("LiveUploads").GetInt32() == 0 &&
                    resource.GetProperty("BuffersCreated").GetInt32() == resource.GetProperty("BuffersReleased").GetInt32() &&
                    resource.GetProperty("AllocationsCreated").GetInt32() == resource.GetProperty("AllocationsReleased").GetInt32() && resource.GetProperty("Uploads").GetInt32() >= 1),
                    "Mesh resources created a separate VkDevice or retained buffers/allocations.");
                if (textureCase != null)
                {
                    Check(resources.Sum(resource => resource.GetProperty("TextureUploads").GetInt32()) == 1 &&
                        resources.Where(resource => resource.GetProperty("TextureUploads").GetInt32() != 0).All(resource =>
                            resource.GetProperty("LastTextureRevision").GetString() == textureCase.Key.Revision.ToString() &&
                            resource.GetProperty("TextureImagesCreated").GetInt32() == 1 && resource.GetProperty("TextureImagesReleased").GetInt32() == 1 &&
                            resource.GetProperty("TextureSamplersCreated").GetInt32() == 1 && resource.GetProperty("TextureSamplersReleased").GetInt32() == 1),
                        "Vulkan upload used the wrong texture revision or retained the image/sampler.");
                }
                cleanups.Add(cleanup.Clone()); Check(!GameLoaded(), "Selected game leaked into parent CLR.");
            }
            if (textureProof)
                Check(textureFrames[0] == textureFrames[2] && textureFrames[0] != textureFrames[1], "A/B/A frame hashes did not follow the selected build.");
            else
                await CheckFailedStartup();
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
            var report = new { Completed = Complete(), Scenario = workerDirectory != null ? "imported-base-color-texture" :
                    textureProof ? "base-color-texture" : "mesh-surface-playing-fence", ValidationRequested = validation, Import = importEvidence,
                FailedStartupObserved = failedStartup, UnsupportedReplacePreservedGeneration = replaceRejected,
                Adapter = adapter, Captures = captures, RuntimeCleanup = cleanups, GameLoadedInParent = GameLoaded(), Resources = host.Gpu.ResourceCount,
                Handles = host.Gpu.OpenHandleCount, ActiveUiSessions = ui.ActiveSessionCount,
                Children = factory.Leases.Select(lease => new { lease.ProcessId, lease.Reaped, lease.ResourcesReleased }), Error = failure?.ToString() };
            await File.WriteAllTextAsync(Path.Combine(reportDirectory, "verification.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
        return Complete() ? 0 : 1;

        async Task CheckFailedStartup()
        {
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

        async Task Input(GameButtons buttons, bool interact = false)
        {
            await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(true, buttons, InteractPressed: interact), token);
            await host.Supervisor.StepAsync(token); await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(false), token);
        }
        async Task<(RenderSnapshotStamp Stamp, int RightPixels, string FrameHash)> Capture(GpuSurfaceSession surface, string phase, int leftValue, int rightValue,
            bool requireFence, CancellationToken captureToken, bool redFirst = false, TextureBuild? textureCase = null)
        {
            var produced = await surface.ProduceFrameAsync(captures.Count % 3, captureToken);
            var stamp = produced.Metadata?.RenderView ?? throw new InvalidDataException("Missing mesh-view/1 stamp."); stamp.Validate();
            var before = await host.Supervisor.GetStatusAsync(captureToken);
            if (requireFence) Check(!before.RenderViewReady && before.Phase == "Paused", "GPU production claimed actual presentation.");
            var read = await surface.ReadAsync(produced.Completion, captureToken); var offer = surface.Surface!; var width = offer.Slots[0].Width; var height = offer.Slots[0].Height;
            Check(read.Pixels.Length == width * height * 4, "Readback extent mismatch.");
            var left = 0; var right = 0;
            object? samples = null;
            if (textureCase != null)
            {
                Check(RenderContentHash.Parse(stamp.ManifestHash) == textureCase.Artifact.RenderContent!.Digest,
                    "Presented frame did not use the selected build manifest.");
                samples = CheckTexturePixels(read.Pixels, width, height, textureCase.Pixels);
            }
            else
            {
                for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
                {
                    var offset = (y * width + x) * 4;
                    if (x < width / 2 && leftValue >= 0 && Near(read.Pixels, offset, leftValue, redFirst ? 0 : leftValue, redFirst ? 0 : leftValue)) left++;
                    if (x > width / 2 && Near(read.Pixels, offset, rightValue, rightValue, rightValue)) right++;
                }
                Check((leftValue < 0 ? CountGeometry(read.Pixels, width, height, true) == 0 : left > 20) && right > 20 && left + right < width * height / 3,
                    $"Native mesh color/geometry mismatch in {phase}: left={left}, right={right}.");
            }
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
            var hash = Convert.ToHexString(SHA256.HashData(read.Pixels));
            captures.Add(new { Generation = factory.Leases.Count, Phase = phase, Width = width, Height = height, produced.Metadata,
                LeftPixels = left, RightPixels = right, RequireFence = requireFence, Presented = true, Sha256 = hash,
                BuildId = textureCase?.Artifact.BuildId, TextureAssetId = textureCase?.Key.AssetId, TextureRevision = textureCase?.Key.Revision.ToString(), Samples = samples });
            return (stamp, right, hash);
        }
    }

    private static async Task<TextureBuild[]> BuildTextureCases(string project, GameBuildCompiler compiler, CancellationToken token)
    {
        var projectId = new Guid("4716d534-193a-4b12-a058-73a71d1fb342");
        var textureId = new Guid("733e1928-391e-49f7-84cb-d39b96dc39ec");
        var mesh = new MeshDefinition(
            [new(-1.2f, 1.2f, 0), new(1.2f, 1.2f, 0), new(1.2f, -1.2f, 0), new(-1.2f, -1.2f, 0)],
            [new(-.25f, -.25f), new(1.25f, -.25f), new(1.25f, 1.25f), new(-.25f, 1.25f)],
            [0, 1, 2, 0, 2, 3], [new(RenderContentProfile.DefaultSlotId, 0, 6)],
            new(new(-1.2f, -1.2f, 0), new(1.2f, 1.2f, 0)));
        var meshKey = mesh.CreateKey(projectId, RenderContentProfile.CubeAssetId);
        var original = Entries(TexturePixels(revised: false));
        var revised = Entries(TexturePixels(revised: true));
        try
        {
            _ = new GameBuildRenderContent(projectId, [revised[0], original[1], original[2], original[3]]);
            throw new InvalidOperationException("Build accepted a texture with a different revision than the material pins.");
        }
        catch (RenderContentException error) when (error.Error == RenderContentError.RevisionMismatch) { }

        var a = await Build(original);
        var b = await Build(revised);
        Check(a.Artifact.BuildId != b.Artifact.BuildId && a.Key.AssetId == b.Key.AssetId && a.Key.Revision != b.Key.Revision,
            "The texture revision did not produce a distinct build for the same logical asset.");
        return [a, b];

        RenderAssetEntry[] Entries(byte[] pixels)
        {
            var texture = new BaseColorTextureDefinition(2, 2, pixels);
            var textureKey = texture.CreateKey(projectId, textureId);
            var material = new OpaqueUnlitMaterialDefinition(Vector3.One, textureKey, RenderTextureSampler.NearestClamp);
            var materialKey = material.CreateKey(projectId, RenderContentProfile.WhiteMaterialAssetId);
            var bindings = new MaterialBindingSetDefinition(meshKey, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
            return [new(textureKey, texture), new(meshKey, mesh), new(materialKey, material),
                new(bindings.CreateKey(projectId, RenderContentProfile.CubeBindingSetAssetId), bindings)];
        }

        async Task<TextureBuild> Build(RenderAssetEntry[] entries)
        {
            return await BuildTexture(project, compiler, new(projectId, entries), entries[0].Key,
                RenderContentProfile.WhiteMaterialAssetId, ((BaseColorTextureDefinition)entries[0].Definition).Pixels.ToArray(), token);
        }
    }

    private static byte[] TexturePixels(bool revised) =>
        [..(revised ? new byte[] { 25, 206, 48, 255 } : new byte[] { 193, 32, 71, 255 }),
            39, 117, 224, 255, 82, 151, 65, 255, 220, 164, 92, 255];

    private static async Task<TextureBuild> BuildTexture(string project, GameBuildCompiler compiler, GameBuildRenderContent renderContent,
        RenderAssetKey expected, Guid materialId, byte[] pixels, CancellationToken token)
    {
        var result = await compiler.BuildAsync(new(project, "RenderSurface.Game.dll", "RenderSurface.Game.TexturedMeshGame", 41)
        {
            RuntimeAccess = true,
            RenderContent = renderContent
        }, token);
        var content = result.Artifact.RenderContent ?? throw new InvalidDataException("Build omitted render content.");
        var texture = (BaseColorTextureDefinition)content.Resolve(expected).Definition;
        var material = (OpaqueUnlitMaterialDefinition)content.Resolve(materialId, RenderContentProfile.MaterialKindId).Definition;
        Check(material.BaseColorTexture == expected && material.Sampler == RenderTextureSampler.NearestClamp &&
            texture.Pixels.AsSpan().SequenceEqual(pixels), "Published artifact changed the texture bytes, sampler or referenced key.");
        return new(result.Artifact, expected, pixels);
    }

    private static object CheckTexturePixels(byte[] rgba, int width, int height, byte[] expected)
    {
        var samples = new List<object>();
        foreach (var (dx, dy, texel) in new[] { (-20, -20, 0), (20, -20, 1), (-20, 20, 2), (20, 20, 3), (-40, -40, 0), (40, 40, 3) })
        {
            var x = width / 2 + dx;
            var y = height / 2 + dy;
            var reference = expected.AsSpan(texel * 4, 4);
            var offset = (y * width + x) * 4;
            var actual = rgba.AsSpan(offset, 4);
            Check(Near(rgba, offset, reference[0], reference[1], reference[2]),
                $"Texture pixel ({x},{y}) expected {Convert.ToHexString(reference)}, got {Convert.ToHexString(actual)}.");
            samples.Add(new { X = x, Y = y, ExpectedRgba = Convert.ToHexString(reference), ActualRgba = Convert.ToHexString(actual) });
        }
        return samples;
    }

    private sealed record TextureBuild(GameBuildArtifact Artifact, RenderAssetKey Key, byte[] Pixels);

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
