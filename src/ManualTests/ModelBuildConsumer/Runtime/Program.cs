using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Player;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Rendering.Resources;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

if (args.Length != 6) return 2;
try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(55));
    var token = timeout.Token;
    Check(BuildIdentity.HostId == args[3], "Player host fingerprint changed.");
    var start = await PlayerStart.CaptureAsync(args[1], args[3], args[5], token: token);
    var manifest = start.Artifact.RenderContent ?? throw new InvalidDataException("Build did not deliver render resources.");
    var scene = start.World!.Scene;
    Check(start.Artifact.BuildId == args[2] && manifest.BuildId == RenderContentHash.Parse(args[2]),
        "Player captured the wrong selected artifact.");
    var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
    {
        ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
    });
    await using var app = App.Builder(builder => { if (args[0] == "microsoft") builder.UseDI(); })
        .Setup(context =>
        {
            context.UseAetherRenderResources();
            context.UseAetherPlayer(start, fonts, options =>
            {
                options.Width = 640;
                options.Height = 360;
                options.Validation = true;
            });
        }).Build();
    var host = app.Services.Resolve<RenderResourceHost>()!;
    var player = app.Services.Resolve<PlayerWindow>()!;
    var session = app.Services.Resolve<PlayerSession>()!;
    var window = app.Services.Resolve<HostedUiWindow>()!;
    var catalog = app.Services.Resolve<IFactory<RenderManifestLease, RenderContentManifest>>()!;
    object[] meshes;
    using (var lease = catalog.Create(manifest)!)
    {
        if (lease.Manifest.BuildId != RenderContentHash.Parse(args[2]) || host.Content.Counts.ScopeCount != 1)
            throw new InvalidOperationException("Wrong build scope in render catalog.");
        meshes = lease.Manifest.Entries.Where(entry => entry.Definition is MaterialBindingSetDefinition).Select(entry =>
        {
            var binding = (MaterialBindingSetDefinition)entry.Definition;
            var mesh = (MeshDefinition)lease.Manifest.Resolve(binding.Mesh).Definition;
            var material = lease.Manifest.Resolve(binding.Bindings.Single().Material);
            return (object)new
            {
                Id = binding.Mesh.AssetId, Slot = mesh.Slots.Single().SlotId, Binding = entry.Key.AssetId,
                Material = material.Key.AssetId, Revision = binding.Mesh.Revision.ToString(),
                MaximumX = mesh.Bounds.Maximum.X, VertexX = mesh.Vertices[1].X,
                Indices = mesh.Indices.ToArray()
            };
        }).ToArray();
    }

    Capture capture;
    try
    {
        await app.StartAsync(token);
        await player.Ready.WaitAsync(token);
        Check(session.Status.State == PlayerState.Playing && session.Status.RenderViewReady &&
            session.Identity.GameBuildId == start.Artifact.BuildId, "Player did not present the selected build.");
        var result = new TaskCompletionSource<Capture>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.FramePresented += Presented;
        try
        {
            window.RequestFrame();
            capture = await result.Task.WaitAsync(token);
        }
        finally { window.FramePresented -= Presented; }

        void Presented(UiSession ui)
        {
            if (result.Task.IsCompleted || ((PlayerView)ui.Root).Canvas.Drawn is not { } drawn ||
                !ReferenceEquals(drawn.Presentation, session.Presentation) ||
                drawn.Presentation.Surface.Generation != session.Surface!.Generation)
                return;
            try
            {
                var stamp = drawn.Metadata.RenderView ?? throw new InvalidDataException("No mesh presentation stamp.");
                Check(drawn.Presentation.Surface.Transport == ViewportTransportKind.Gpu &&
                    RenderContentHash.Parse(stamp.ManifestHash) == manifest.Digest &&
                    stamp.SceneId == scene.Id.Value && stamp.SourceRevision == scene.Revision,
                    "Presented GPU frame has the wrong build manifest or scene stamp.");
                using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using (var file = File.Create(Path.ChangeExtension(args[4], ".png"))) data.SaveTo(file);
                using var pixels = SKBitmap.FromImage(image);
                result.TrySetResult(new(Measure(pixels), drawn.Metadata.FrameId, stamp.Tick, stamp.ManifestHash));
            }
            catch (Exception error) { result.TrySetException(error); }
        }
    }
    finally
    {
        await player.StopAsync();
        window.Close();
        await window.Completion.WaitAsync(TimeSpan.FromSeconds(20));
        await app.StopAsync();
    }

    Check(session.Status.State == PlayerState.Stopped && session.Surface is { IsReleased: true },
        "Player did not release its local GPU surface.");
    var diagnostics = session.Surface!.Diagnostics;
    Check(diagnostics.Errors == 0 && diagnostics.Warnings == 0 && diagnostics.OpenHandles == 0 &&
        diagnostics.LiveSlots == 0 && diagnostics.SurfacesCreated == diagnostics.SurfacesReleased &&
        diagnostics.SlotsCreated == diagnostics.SlotsReleased && player.PresentedFrames > 0,
        "GPU validation, presentation, or native ownership failed.");
    var counts = host.Content.Counts;
    if (counts.ScopeCount != 0 || counts.RevisionCount != 0)
        throw new InvalidOperationException("Runtime render resources were retained after lease disposal.");
    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(value => value.GetName().Name).ToArray();
    if (assemblies.Any(name => name is "TrueMoon.Nom" or "TrueMoon.Aether.Nom" or "TrueMoon.Aether.Tooling" or "AssimpNetter" or "TrueMoon.Aether.Editor"))
        throw new InvalidOperationException("Runtime loaded an authoring dependency.");
    File.WriteAllText(args[4], JsonSerializer.Serialize(new
    {
        start.Artifact.BuildId, manifest.ProjectId, SceneSha256 = start.SceneHash,
        SceneId = scene.Id.Value, SceneRevision = scene.Revision, Meshes = meshes,
        capture.Mask, capture.FrameId, capture.Tick, capture.ManifestHash, player.PresentedFrames,
        Validation = diagnostics, CatalogAfterRelease = counts, LoadedAssemblies = assemblies
    }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}

static PixelMask Measure(SKBitmap pixels)
{
    var mask = new byte[checked(pixels.Width * pixels.Height)];
    var top = 0;
    var bottom = 0;
    var left = pixels.Width;
    var right = -1;
    for (var y = 0; y < pixels.Height; y++)
    for (var x = 0; x < pixels.Width; x++)
    {
        var color = pixels.GetPixel(x, y);
        if (color.Red <= 200 || color.Green >= 25 || color.Blue >= 25) continue;
        left = Math.Min(left, x);
        right = Math.Max(right, x);
        if (y < pixels.Height / 2) top++;
        else bottom++;
        mask[y * pixels.Width + x] = 1;
    }
    Check(top > 100 && bottom > 100, "Expected both placed model instances in the GPU frame.");
    return new(pixels.Width, pixels.Height, left, right, top + bottom, top, bottom,
        Convert.ToHexString(SHA256.HashData(mask)));
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record PixelMask(int Width, int Height, int Left, int Right, int RedPixels, int Upper, int Lower, string Sha256);
internal sealed record Capture(PixelMask Mask, ulong FrameId, ulong Tick, string ManifestHash);
