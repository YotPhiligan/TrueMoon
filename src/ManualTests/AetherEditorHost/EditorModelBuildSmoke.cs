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
        var sceneHash = Hash(File.ReadAllBytes(scenePath));
        Check(oldBuild.BuildId != newBuild.BuildId, "Scene probe requires two distinct build IDs.");
        var compiler = Path.Combine(report, "editor-compiler");
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
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime")
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

            foreach (var (name, artifact) in new[] { ("old", oldBuild), ("new", newBuild), ("old-reopened", oldBuild) })
            {
                var leaseIndex = native.Leases.Count;
                Check(playback.RunSelectedBuild(artifact), "Editor did not admit selected build.");
                await playback.Pending.WaitAsync(token);
                if (playback.Error is { } error) throw new InvalidOperationException("Editor selected build failed.", error);
                var status = engine.Supervisor.Status;
                var identity = status.Identity ?? throw new InvalidOperationException("No selected runtime identity.");
                Check(status.State == SessionState.Playing && identity.GameBuildId == artifact.BuildId &&
                    native.Leases.Count == leaseIndex + 1 && engine.Supervisor.RequiredRenderViewVersion == 1,
                    "Selected build did not reach Playing after the mesh presentation fence.");
                var lease = native.Leases[leaseIndex];
                Check(!playback.RunSelectedBuild(artifact) && !playback.CanExecute(EditorCommand.Replace),
                    "Selected build switching must wait for Stop.");
                var manifest = artifact.RenderContent!;
                var mask = await Capture(name, identity, content.Document.Id.Value, content.Document.Revision, manifest.Digest);
                Check(playback.Execute(EditorCommand.Stop), "Editor did not admit Stop.");
                await playback.Pending.WaitAsync(token);
                if (playback.Error is { } stopError) throw new InvalidOperationException("Editor Stop failed.", stopError);
                Check(lease.Reaped && lease.ResourcesReleased && engine.Supervisor.Status.State == SessionState.Stopped &&
                    engine.Frames.Presentation == null && engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0,
                    "Stop retained owned child or viewport resources.");
                var after = documents.Read().Document!;
                Check(ReferenceEquals(content, after.Content) && before.IsDirty == after.IsDirty &&
                    sceneHash == Hash(File.ReadAllBytes(scenePath)), "Playing changed the open authoring document.");
                using var child = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(report, $"runtime-{identity.Generation}.json"), token));
                var stats = child.RootElement;
                var validation = stats.GetProperty("Validation");
                Check(stats.GetProperty("Pid").GetInt32() == lease.ProcessId && stats.GetProperty("Resources").GetInt32() == 0 &&
                    stats.GetProperty("Handles").GetInt32() == 0 && validation.GetProperty("ValidationEnabled").GetBoolean() &&
                    validation.GetProperty("Errors").GetInt32() == 0 && validation.GetProperty("Warnings").GetInt32() == 0,
                    "Incorrect child validation or resource cleanup.");
                captures.Add(new(name, artifact.BuildId, identity.Generation, lease.ProcessId, manifest.Digest.ToString(), mask));
            }

            var old = captures[0].Mask;
            var updated = captures[1].Mask;
            Check(old == captures[2].Mask && old.Width == updated.Width && old.Height == updated.Height &&
                Math.Abs(old.Left - updated.Left) <= 1 && updated.Right > old.Right + 10 &&
                updated.RedPixels > old.RedPixels * 1.6, "GPU scene does not show the expected stable and changed geometry.");
            Check(!Directory.Exists(Path.Combine(compiler, "jobs")) &&
                AppDomain.CurrentDomain.GetAssemblies().All(assembly => assembly.GetName().Name != "DeliveryGame"),
                "Selecting a published artifact compiled or loaded game code in the parent.");
            window.Close();
            await window.Completion.WaitAsync(token);
        }
        finally
        {
            try { await documents.StopAsync(); }
            finally { await app.StopAsync(); }
        }

        await File.WriteAllTextAsync(Path.Combine(report, "editor-model.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Provider = args[0], SceneSha256 = sceneHash, Captures = captures
        }, new JsonSerializerOptions { WriteIndented = true }), token);

        async Task<PixelMask> Capture(string name, SessionIdentity identity, Guid sceneId, long revision, RenderContentHash digest)
        {
            var result = new TaskCompletionSource<PixelMask>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                    result.TrySetResult(Measure(pixels, clip));
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
    private sealed record FrameProof(string Name, string BuildId, ulong Generation, int Pid, string ManifestHash, PixelMask Mask);
}
