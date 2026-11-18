using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Editor.Tooling;
using TrueMoon.Aether.Editor.Tooling.Documents;
using TrueMoon.Aether.Editor.Tooling.Editing;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Editor.UI.Documents;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorModelBuildSmoke
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 6 || args[0] is not ("cobalt" or "microsoft")) throw new ArgumentException("Invalid model delivery probe arguments.");
        var report = args[1];
        var project = args[2];
        var scenePath = args[3];
        var hostId = BuildIdentity.HostId;
        var oldBuild = GameBuildArtifact.Open(args[4], hostId);
        var newBuild = GameBuildArtifact.Open(args[5], hostId);
        var corruptBuild = CorruptCopy(oldBuild, report, hostId);
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(report, "editor-compiler");
        using var cancelReplacement = new CancellationTokenSource();
        SessionIdentity? cancelledCandidate = null;
        var cancelOnLoad = 0;
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        {
            ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
        });
        var configuration = ConfigurationProfiles.Create(profile =>
        {
            profile.AddSchema(AetherConfigurationSchemas.Engine);
            profile.AddSchema(AetherConfigurationSchemas.Authoring);
            profile.AddSchema(EditorConfigurationSchemas.Options);
            profile.AddSchema(EditorConfigurationSchemas.Diagnostics);
            profile.AddSchema(EditorConfigurationSchemas.Window);
            profile.Override("Aether:ReportDirectory", report);
            profile.Override("Aether:BuildDirectory", compiler);
            profile.Override("Aether:GpuValidation", true);
            profile.Override("Editor:ProjectPath", project);
        });
        await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); }).Setup(context =>
        {
            context.UseConfiguration(configuration);
            context.UseConfiguredAetherEditor(new AetherComposition
            {
                HostContractId = hostId, GameBuildId = new string('0', 64),
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime"),
                StatusChanged = status =>
                {
                    if (status is { State: SessionState.Starting, SceneLoaded: true, Identity: { } identity } &&
                        identity.GameBuildId == oldBuild.BuildId && Interlocked.Exchange(ref cancelOnLoad, 0) == 1)
                    {
                        cancelledCandidate = identity;
                        cancelReplacement.Cancel();
                    }
                }
            });
            context.UseConfiguredAetherEditorWindow(fonts, options => { options.Width = 1100; options.Height = 720; });
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        var native = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var captures = new List<FrameProof>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        try
        {
            await app.StartAsync(token);
            await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(token);
            Check(documents.Execute(DocumentCommand.Open, scenePath), "Editor declined opening the placed scene.");
            await documents.Pending.WaitAsync(token);
            var before = documents.Read().Document ?? throw new InvalidOperationException("No active document.");
            var content = before.Content ?? throw new InvalidOperationException("Editor did not load the placed scene.");
            Check(content.Document.Entities.Length == 3 && !before.IsDirty, "Unexpected captured scene.");

            Check(playback.RunSelectedBuild(oldBuild), "Editor did not admit the first build.");
            await Complete();
            while (engine.Supervisor.Status.Tick < 5)
            {
                await engine.Supervisor.GetStatusAsync(token);
                await Task.Delay(16, token);
            }
            var original = await CapturePlaying("old", oldBuild, 5);
            Check(!playback.RunSelectedBuild(newBuild), "Editor admitted a second independent Start.");
            Check(playback.ReplaceSelectedBuild(newBuild), "Editor did not admit hot Replace.");
            await Complete();
            var updated = await CapturePlaying("new", newBuild, original.Tick);
            Check(native.Leases.Count == 2 && updated.Generation > original.Generation,
                "Successful Replace did not activate exactly one new child.");
            await CheckReleased(original.Generation);

            var beforeCorruption = engine.Supervisor.Status.Identity;
            var previousSurface = engine.Frames.Presentation;
            Check(playback.ReplaceSelectedBuild(corruptBuild), "Editor did not admit corrupt candidate preflight.");
            await playback.Pending.WaitAsync(token);
            Check(playback.Error is InvalidDataException corruption &&
                corruption.Message.Contains("render payload hash mismatch", StringComparison.Ordinal) &&
                engine.Supervisor.Status.Identity == beforeCorruption &&
                ReferenceEquals(previousSurface, engine.Frames.Presentation) && native.Leases.Count == 2,
                "Corrupt candidate changed the running child or viewport.");
            var afterCorruption = await CapturePlaying("after-corruption", newBuild, updated.Tick);

            Volatile.Write(ref cancelOnLoad, 1);
            Check(playback.ReplaceSelectedBuild(oldBuild, cancelReplacement.Token), "Editor did not admit cancellable Replace.");
            await playback.Pending.WaitAsync(token);
            Check(cancelledCandidate is not null && playback.Error is GameReplacementException
                { Phase: "Candidate", RollbackAttempted: true, RollbackSucceeded: true, RecoveryError: null, InnerException: OperationCanceledException } &&
                native.Leases.Count == 4, "Cancellation after scene load did not recover the previous build exactly once.");
            var recovered = await CapturePlaying("after-cancel", newBuild, afterCorruption.Tick);
            Check(recovered.Generation > cancelledCandidate!.Generation && cancelledCandidate.Generation > updated.Generation,
                "Cancellation did not advance the runtime generation on recovery.");
            await CheckReleased(updated.Generation);
            // The cancelled candidate had completed scene activation. Restore and surface configuration were still pending.
            Check(native.Leases[2].Identity == cancelledCandidate && native.Leases[2].Reaped && native.Leases[2].ResourcesReleased,
                "Cancelled candidate survived rollback.");

            Check(playback.ReplaceSelectedBuild(oldBuild), "Recovered Editor session could not Replace again.");
            await Complete();
            var reopened = await CapturePlaying("old-reopened", oldBuild, recovered.Tick);
            Check(native.Leases.Count == 5 && reopened.Generation > recovered.Generation, "Unexpected runtime generations.");
            await CheckReleased(recovered.Generation);

            Check(playback.Execute(EditorCommand.Stop), "Editor did not admit final Stop.");
            await Complete();
            Check(engine.Supervisor.Status.State == SessionState.Stopped && engine.Frames.Presentation == null &&
                engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                "Final Stop retained viewport resources.");
            await CheckReleased(reopened.Generation);

            var old = original.Mask;
            var next = updated.Mask;
            Check(old == reopened.Mask && next == afterCorruption.Mask && next == recovered.Mask &&
                old.Width == next.Width && old.Height == next.Height && Math.Abs(old.Left - next.Left) <= 1 &&
                next.Right > old.Right + 10 && next.RedPixels > old.RedPixels * 1.6,
                "GPU scene does not show the expected replacement or recovered geometry.");
            Check(!Directory.Exists(Path.Combine(compiler, "jobs")) &&
                AppDomain.CurrentDomain.GetAssemblies().All(assembly => assembly.GetName().Name != "DeliveryGame"),
                "Selecting a published artifact compiled or loaded game code in the parent.");
            window.Close();
            await window.Completion.WaitAsync(token);

            async Task Complete()
            {
                await playback.Pending.WaitAsync(token);
                if (playback.Error is { } error) throw new InvalidOperationException("Editor scene command failed.", error);
            }

            async Task<FrameProof> CapturePlaying(string name, GameBuildArtifact artifact, ulong minimumTick)
            {
                await engine.Supervisor.GetStatusAsync(token);
                var status = engine.Supervisor.Status;
                var identity = status.Identity ?? throw new InvalidOperationException("No selected runtime identity.");
                Check(status.State == SessionState.Playing && identity.GameBuildId == artifact.BuildId &&
                    status.Tick >= minimumTick && engine.Supervisor.RequiredRenderViewVersion == 1,
                    $"Expected {artifact.BuildId} Playing at tick >= {minimumTick}, observed {identity.GameBuildId} {status.State} at tick {status.Tick}.");
                var manifest = artifact.RenderContent!;
                var frame = await Capture(name, identity, content.Document.Id.Value, content.Document.Revision, manifest.Digest);
                Check(frame.Tick >= minimumTick, "Presented frame predates the checkpoint.");
                var after = documents.Read().Document!;
                Check(ReferenceEquals(content, after.Content) && before.IsDirty == after.IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Replace changed the open authoring document.");
                var proof = new FrameProof(name, artifact.BuildId, identity.Generation, status.ProcessId!.Value,
                    manifest.Digest.ToString(), frame.Tick, frame.Mask);
                captures.Add(proof);
                return proof;
            }

            async Task CheckReleased(ulong generation)
            {
                var lease = native.Leases.Single(lease => lease.Identity.Generation == generation);
                Check(lease.Reaped && lease.ResourcesReleased, "Retired child or handles survived replacement.");
                using var child = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(report, $"runtime-{generation}.json"), token));
                var stats = child.RootElement;
                var validation = stats.GetProperty("Validation");
                Check(stats.GetProperty("Pid").GetInt32() == lease.ProcessId && stats.GetProperty("Resources").GetInt32() == 0 &&
                    stats.GetProperty("Handles").GetInt32() == 0 && validation.GetProperty("ValidationEnabled").GetBoolean() &&
                    validation.GetProperty("Errors").GetInt32() == 0 && validation.GetProperty("Warnings").GetInt32() == 0,
                    "Incorrect child validation or resource cleanup.");
            }
        }
        finally
        {
            try { await documents.StopAsync(); }
            finally { await app.StopAsync(); }
        }

        await File.WriteAllTextAsync(Path.Combine(report, "editor-model.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Provider = args[0], SceneSha256 = sceneHash, Captures = captures,
            CancelledGeneration = cancelledCandidate!.Generation, CorruptRejectedBeforeHandoff = true, RollbackSucceeded = true
        }, new JsonSerializerOptions { WriteIndented = true }), token);

        async Task<(PixelMask Mask, ulong Tick)> Capture(string name, SessionIdentity identity, Guid sceneId, long revision, RenderContentHash digest)
        {
            var result = new TaskCompletionSource<(PixelMask Mask, ulong Tick)>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession ui)
            {
                if (result.Task.IsCompleted) return;
                try
                {
                    var canvas = EditorMainWindow.FromRoot(ui.Root).EditorView.Canvas;
                    if (!canvas.CanAcknowledgeDraw || canvas.DrawnFrame is not { } frame ||
                        frame.Presentation.Surface.Identity != identity ||
                        !ReferenceEquals(frame.Presentation, engine.Frames.Presentation)) return;
                    var stamp = frame.Metadata.RenderView ?? throw new InvalidDataException("Missing mesh viewport stamp.");
                    Check(frame.Presentation.Surface.Transport == ViewportTransportKind.Gpu && stamp.SceneId == sceneId &&
                        stamp.SourceRevision == revision && RenderContentHash.Parse(stamp.ManifestHash) == digest,
                        "GPU draw was not from the captured scene and selected build.");
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    using (var file = File.Create(Path.Combine(report, name + ".png"))) png.SaveTo(file);
                    using var pixels = SKBitmap.FromImage(image);
                    var bounds = canvas.ImageBounds;
                    var scale = ui.Viewport.Scale;
                    var clip = new SKRectI((int)(bounds.X * scale), (int)(bounds.Y * scale),
                        (int)((bounds.X + bounds.Width) * scale), (int)((bounds.Y + bounds.Height) * scale));
                    result.TrySetResult((Measure(pixels, clip), stamp.Tick));
                }
                catch (Exception error) { result.TrySetException(error); }
            }
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                return await result.Task.WaitAsync(token);
            }
            finally { window.FramePresented -= Presented; }
        }
    }

    private static GameBuildArtifact CorruptCopy(GameBuildArtifact artifact, string report, string hostId)
    {
        var directory = Directory.CreateDirectory(Path.Combine(report, "corrupt", artifact.BuildId)).FullName;
        foreach (var path in Directory.EnumerateFiles(artifact.DirectoryPath))
            File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
        var copy = GameBuildArtifact.Open(directory, hostId, artifact.BuildId);
        var payload = Path.Combine(directory, GameBuildArtifact.RenderContentFile);
        var bytes = File.ReadAllBytes(payload);
        bytes[^1] ^= 1;
        File.WriteAllBytes(payload, bytes);
        return copy;
    }

    private static PixelMask Measure(SKBitmap pixels, SKRectI clip)
    {
        Check(clip.Left >= 0 && clip.Top >= 0 && clip.Right <= pixels.Width && clip.Bottom <= pixels.Height &&
            clip.Width > 0 && clip.Height > 0, "Invalid GPU image bounds.");
        var mask = new byte[clip.Width * clip.Height];
        var upper = 0;
        var lower = 0;
        var left = clip.Width;
        var right = -1;
        for (var y = 0; y < clip.Height; y++)
        for (var x = 0; x < clip.Width; x++)
        {
            var color = pixels.GetPixel(clip.Left + x, clip.Top + y);
            if (color.Red <= 200 || color.Green >= 25 || color.Blue >= 25) continue;
            mask[y * clip.Width + x] = 1;
            if (y < clip.Height / 2) upper++;
            else lower++;
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }
        Check(upper > 100 && lower > 100, "Expected both occurrences in the GPU viewport.");
        return new(clip.Width, clip.Height, left, right, upper + lower, Hash(mask));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, string Sha256);
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, ulong Tick, PixelMask Mask);
}
