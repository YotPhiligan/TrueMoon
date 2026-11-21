using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Player;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;

namespace TrueMoon.ManualTests.PlayerProbe;

internal static class OrthographicRouteProof
{
    private static readonly (string Name, SKColor Color)[] Landmarks =
    [
        ("Street", new(99, 120, 148)),
        ("Steps", new(87, 176, 209)),
        ("Porch", new(219, 153, 79)),
        ("Room", new(112, 178, 148))
    ];

    internal static async Task<int> RunAsync(string artifact, string scenePath, string report)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        Directory.CreateDirectory(report);
        var start = await PlayerStart.CaptureAsync(artifact, BuildIdentity.HostId, scenePath, token: token);
        var scene = start.World!.Scene;
        var manifest = start.Artifact.RenderContent ?? throw new InvalidDataException("The build has no render content.");
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        {
            ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf")
        });
        await using var app = App.Build(context => context.UseAetherPlayer(start, fonts, options =>
        {
            options.Width = 800;
            options.Height = 540;
            options.Title = "TrueMoon.Tales isometric route";
            options.Validation = true;
        }));
        var player = app.Services.Resolve<PlayerWindow>()!;
        var session = app.Services.Resolve<PlayerSession>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!;
        Frame before, after;
        try
        {
            await app.StartAsync(token);
            await player.Ready.WaitAsync(token);
            before = await CaptureAsync("before-resize", 0);
            window.Resize(1040, 540);
            after = await CaptureAsync("after-resize", before.SurfaceGeneration);
            var expectedWidth = before.Width * (1040d / 800);
            Check(after.Width > before.Width && Math.Abs(after.Width - expectedWidth) <= 2 && after.Height == before.Height,
                $"Unexpected physical capture extent: {before.Width}×{before.Height} → {after.Width}×{after.Height}.");
            Check(after.SurfaceGeneration > before.SurfaceGeneration && after.Ticks >= before.Ticks,
                "Resize did not present a new current surface generation.");
            Check(Array.Exists(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
                    assembly.GetName().Name == "TrueMoon.Tales.Gameplay") &&
                !Array.Exists(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
                    assembly.GetName().Name is "TrueMoon.Aether.Tooling" or "TrueMoon.Aether.Editor"),
                "Unexpected Player assembly graph.");
            for (var i = 0; i < Landmarks.Length; i++)
            {
                var first = before.Landmarks[i];
                var last = after.Landmarks[i];
                Check(first.Count >= 100 && Math.Abs(last.Count - first.Count) <= Math.Max(12, first.Count / 100) &&
                    Math.Abs(last.Left - first.Left - (after.Width - before.Width) / 2) <= 1 &&
                    Math.Abs(last.Right - first.Right - (after.Width - before.Width) / 2) <= 1 &&
                    Math.Abs(last.Top - first.Top) <= 1 && Math.Abs(last.Bottom - first.Bottom) <= 1,
                    $"The {Landmarks[i].Name} landmark did not retain its scale and placement.");
            }
        }
        finally
        {
            await player.StopAsync();
            window.Close();
            await window.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            await app.StopAsync();
        }

        var diagnostics = session.Surface!.Diagnostics;
        Check(session.Status.State == PlayerState.Stopped && session.Surface.IsReleased &&
            diagnostics is { Errors: 0, Warnings: 0, OpenHandles: 0, LiveSlots: 0 } &&
            diagnostics.SurfacesCreated == diagnostics.SurfacesReleased && diagnostics.SlotsCreated == diagnostics.SlotsReleased,
            "Vulkan validation or surface cleanup failed.");

        await File.WriteAllTextAsync(Path.Combine(report, "verification.json"), JsonSerializer.Serialize(new
        {
            Completed = true, start.Artifact.BuildId, start.SceneHash, Scene = scene.Id.Value, scene.Revision,
            Manifest = manifest.Digest.ToString(), Before = before, After = after, Diagnostics = diagnostics
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;

        async Task<Frame> CaptureAsync(string name, ulong previousGeneration)
        {
            var completed = new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously);
            window.FramePresented += Presented;
            try
            {
                window.RequestFrame();
                var first = await Task.WhenAny(completed.Task, player.Completion).WaitAsync(token);
                if (first == player.Completion)
                {
                    await player.Completion;
                    throw new InvalidOperationException("Player closed before presenting the requested frame.");
                }
                return await completed.Task;
            }
            finally { window.FramePresented -= Presented; }

            void Presented(UiSession ui)
            {
                if (completed.Task.IsCompleted || ((PlayerView)ui.Root).Canvas.Drawn is not { } frame ||
                    !ReferenceEquals(frame.Presentation, session.Presentation) ||
                    frame.Presentation.Surface.Generation <= previousGeneration ||
                    session.Status.State != PlayerState.Playing || !session.Status.RenderViewReady)
                    return;
                try
                {
                    var stamp = frame.Metadata.RenderView ?? throw new InvalidDataException("Frame has no render stamp.");
                    var surface = frame.Presentation.Surface;
                    Check(surface.Transport == ViewportTransportKind.Gpu && RenderContentHash.Parse(stamp.ManifestHash) == manifest.Digest &&
                        stamp.SceneId == scene.Id.Value && stamp.SourceRevision == scene.Revision &&
                        session.Identity.GameBuildId == start.Artifact.BuildId, "Wrong Vulkan surface, scene or build stamp.");
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    var png = data.ToArray();
                    File.WriteAllBytes(Path.Combine(report, name + ".png"), png);
                    using var pixels = SKBitmap.FromImage(image);
                    Check(pixels.Width == surface.Width && pixels.Height == surface.Height, "Frame capture and surface extent differ.");
                    completed.TrySetResult(new(pixels.Width, pixels.Height, surface.Generation, stamp.Tick,
                        Convert.ToHexString(SHA256.HashData(png)), MeasureLandmarks(pixels)));
                }
                catch (Exception error) { completed.TrySetException(error); }
            }
        }
    }

    private static Landmark[] MeasureLandmarks(SKBitmap pixels)
    {
        var result = new Landmark[Landmarks.Length];
        for (var i = 0; i < Landmarks.Length; i++)
        {
            var (name, target) = Landmarks[i];
            var left = pixels.Width;
            var top = pixels.Height;
            var right = -1;
            var bottom = -1;
            var count = 0;
            for (var y = 0; y < pixels.Height; y++)
                for (var x = 0; x < pixels.Width; x++)
                {
                    var color = pixels.GetPixel(x, y);
                    if (Math.Abs(color.Red - target.Red) > 3 || Math.Abs(color.Green - target.Green) > 3 ||
                        Math.Abs(color.Blue - target.Blue) > 3)
                        continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                    count++;
                }
            result[i] = new(name, count, left, top, right, bottom);
        }
        return result;
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }

    private sealed record Frame(int Width, int Height, ulong SurfaceGeneration, ulong Ticks, string PngSha256, Landmark[] Landmarks);
    private sealed record Landmark(string Name, int Count, int Left, int Top, int Right, int Bottom);
}
