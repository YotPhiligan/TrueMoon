using System.Reflection;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Player;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.PlayerProbe;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        if (args is ["--scenario", var scenarioFile, var scenarioBuild, var scenarioScene, var scenarioOutput, .. var flags] &&
            (flags is [] or ["--legacy-scene"]))
            return await PlayerScenarioCommand.RunAsync(scenarioFile, scenarioBuild, scenarioScene, scenarioOutput, flags.Length != 0);
        if (args is ["--orthographic-route", var artifact, var routeScene, var routeReport])
            return await OrthographicRouteProof.RunAsync(artifact, routeScene, routeReport);
        if (args is ["--outpost-hud", var outpostArtifact, var outpostScene, var outpostReport])
            return await OutpostHudProof.RunAsync(outpostArtifact, outpostScene, outpostReport);
        if (args.Length is <2 or >4 || args.Length>=3 && args[2] is not ("microsoft" or "cobalt")) throw new ArgumentException("PlayerProbe <compiled-game-directory> <report-directory> [cobalt|microsoft] [selected-snapshot], or PlayerProbe --scenario <scenario.json> <build> <scene> <output> [--legacy-scene]");
        var microsoft=args.Length>=3 && args[2]=="microsoft";
        var report = Path.GetFullPath(args[1]); Directory.CreateDirectory(report);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = timeout.Token;
        var build = GameBuildArtifact.Open(Path.GetFullPath(args[0]), BuildIdentity.HostId);
        var scenePath = args.Length == 4 ? Path.GetFullPath(args[3]) : Path.Combine(report, "scene.json");
        if (args.Length != 4) await File.WriteAllTextAsync(scenePath, "{\"formatVersion\":1,\"id\":\"player\",\"revision\":41,\"entities\":[]}", token);
        var start = await PlayerStart.CaptureAsync(build.DirectoryPath, BuildIdentity.HostId, scenePath, true, token);
        // The file is no longer a live input after bootstrap capture.
        await File.WriteAllTextAsync(scenePath, "changed after capture", token);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string,string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Builder(configuration=>{if(microsoft)configuration.UseDI();}).Setup(context => context.UseAetherPlayer(start, fonts,
            options => { options.Width = 640; options.Height = 360; options.Validation = true; })).Build();
        var session = app.Services.Resolve<PlayerSession>()!;
        var player = app.Services.Resolve<PlayerWindow>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var input = app.Services.Resolve<PlayerInput>()!;
        Exception? failure = null; var captures = new List<object>(); var gameSeen = false;
        try
        {
            Check(!Loaded("RenderSurface.Game"), "Selected game loaded before App start.");
            await app.StartAsync(token); await player.Ready.WaitAsync(token);
            Check(session.Status.State == PlayerState.Playing && session.Status.RenderViewReady, "No actual presentation/Playing fence.");
            gameSeen = Loaded("RenderSurface.Game");
            Check(gameSeen && !EditorLoaded() && !Loaded("TrueMoon.Aether.Tooling") && !Loaded("TrueMoon.Aether"), "Wrong role assembly graph.");
            Check(!app.Services.ResolveAll<RuntimeEngine>().Any() && !app.Services.ResolveAll<AetherRuntimeHost>().Any(), "Player created a process host.");
            await Capture("white");
            input.Capture(100, 100); input.Key(UiKey.W, true);
            await Wait(() => session.Status.Tick > 5 && player.CopiedFrames > 8); input.Key(UiKey.W, false);
            await Capture("red");
            input.Key(UiKey.E, true); input.Key(UiKey.E, true);
            var before = player.CopiedFrames; await Wait(() => player.CopiedFrames >= before + 5); input.Key(UiKey.E, false);
            await Capture("small");
            var generation = session.Surface!.Generation; window.Resize(800, 450);
            await Wait(() => session.Surface.Generation > generation && session.Status.State == PlayerState.Playing && session.Status.RenderViewReady);
            await Capture("resized");
            window.Minimize(); await Wait(() => session.Presentation?.Surface.Width == 0);
            var tick = session.Status.Tick; await Task.Delay(100, token); Check(session.Status.Tick == tick, "Suspended Player continued ticking.");
            window.Restore(); await Wait(() => session.Presentation?.Surface.Width > 0 && session.Status.State == PlayerState.Playing);
            await Capture("restored");
            window.Close(); await window.Completion.WaitAsync(token); await app.StopAsync(token);
            Check(session.Status.State == PlayerState.Stopped && session.Surface.IsReleased, "Local owners survived window close.");
            var d = session.Surface.Diagnostics;
            Check(d.Errors == 0 && d.Warnings == 0, "Native validation failure.");
            Check(d.SurfacesCreated == d.SurfacesReleased && d.SlotsCreated == d.SlotsReleased && d.OpenHandles == 0, "Native ownership imbalance.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await player.StopAsync(); window.Close(); await window.Completion.WaitAsync(TimeSpan.FromSeconds(20)); await app.StopAsync(); }
            catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            await File.WriteAllTextAsync(Path.Combine(report, "player.json"), JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, Provider=microsoft?"Microsoft":"Cobalt", HostId = BuildIdentity.HostId, BuildId = build.BuildId, start.SceneHash,
              SceneId = start.Snapshot?.Id, SceneRevision = start.Snapshot?.Revision,
              GameLoaded = gameSeen, GameStillLoadedAfterStop = Loaded("RenderSurface.Game"),
              EditorLoaded = EditorLoaded(), WorkflowLoaded = Loaded("TrueMoon.Aether") || Loaded("TrueMoon.Aether.Tooling"),
              player.CopiedFrames, player.PresentedFrames, State = session.Status, Captures = captures,
              Validation = session.Surface?.Diagnostics, Error = failure?.ToString() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        Console.WriteLine($"Player passed: {captures.Count} captures; {player.CopiedFrames} GPU copies; released."); return 0;

        async Task Wait(Func<bool> condition)
        { while (!condition()) { if (player.Completion.IsFaulted) await player.Completion; token.ThrowIfCancellationRequested(); await Task.Delay(20, token); } }
        async Task Capture(string name)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(TrueMoon.Alloy.Runtime.UiSession ui)
            {
                if (((PlayerView)ui.Root).Canvas.Drawn is not {} drawn ||
                    !ReferenceEquals(drawn.Presentation, session.Presentation) ||
                    drawn.Presentation.Surface.Generation != session.Surface!.Generation) return;
                try
                {
                    using var image = ((SkiaOpenGLSurface)ui.Rendering).ReadbackImage();
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    using var file = File.Create(Path.Combine(report, name + ".png")); data.SaveTo(file);
                    using var pixels = SKBitmap.FromImage(image);
                    var foreground = 0; var red = 0; var white = 0;
                    for (var y = 0; y < pixels.Height; y++) for (var x = 0; x < pixels.Width; x++)
                    { var c = pixels.GetPixel(x, y); if (c.Red > 100) foreground++; if (c.Red > 150 && c.Green < 10 && c.Blue < 10) red++; if (c.Red > 245 && c.Green > 245 && c.Blue > 245) white++; }
                    Check(foreground > 500, "No rendered geometry.");
                    if (name == "white") Check(white > 1000 && red == 0, "Initial white geometry mismatch.");
                    if (name == "red") Check(red > 500 && white > 500, "Independent entity tint/input failed.");
                    captures.Add(new { Name = name, pixels.Width, pixels.Height, Foreground = foreground, Red = red, White = white }); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally { window.FramePresented -= Presented; }
            }
            window.FramePresented += Presented; window.RequestFrame(); await done.Task.WaitAsync(token);
        }
    }
    private static bool Loaded(string name) => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == name);
    private static bool EditorLoaded() => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("TrueMoon.Aether.Editor", StringComparison.Ordinal) == true);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
