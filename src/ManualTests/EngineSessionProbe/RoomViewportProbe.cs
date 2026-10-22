using System.Security.Cryptography;
using System.Text.Json;
using System.Numerics;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Dependencies;

namespace EngineSessionProbe;

// Uses the ordinary App -> Editor -> runtime -> imported GPU texture -> retained UI image path.
internal static class RoomViewportProbe
{
    internal static async Task<int> RunAsync(string directory, bool interactionProof = false)
    {
        if (Directory.Exists(directory)) throw new IOException("Use a fresh scene proof directory.");
        Directory.CreateDirectory(directory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var factory = new SceneFactory(); var authoring = new AuthoringState(); var before = JsonSerializer.Serialize(authoring);
        var binding = new EditorGpuViewportBinding(runtime => (NativeRuntimeLease)runtime);
        var captures = new List<object>(); var centroids = new List<double>();
        var cases = new List<string>();
        void Case(string name, bool condition) { Probe.Check(condition, name); cases.Add(name); }
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Build(c =>
        {
            c.UseAether(o =>
            {
                o.HostContractId = BuildIdentity.HostId; o.GameBuildId = BuildIdentity.GameId; o.ReportDirectory = directory;
                o.RuntimeFactory = _ => factory; o.ViewportTransport = binding.CreateTransport;
            });
            c.UsePresentation<ViewportView>(o =>
            {
                o.UseSkiaOpenGL(fonts); o.UseSilkWindow(w =>
                { w.Width = 1000; w.Height = 760; w.Title = "TrueMoon ENG5c Room"; w.FrameMode = UiFrameMode.Continuous; });
            });
            c.Services(r => r.Transient<ViewportView>(_ =>
            {
                var result = new ViewportView(command => { }); result.Canvas.PreserveAspectRatio = true; return result;
            }));
        });
        var host = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!; var windowId = window.Id;
        ViewportView? view = null; Pending? pending = null; Exception? failure = null; var passed = false;
        window.FramePresented += ui =>
        {
            var current = (ViewportView)ui.Root;
            Probe.Check(view == null || ReferenceEquals(view, current), "3D recreated the Editor root."); view = current; binding.Observe(ui);
            if (view.Canvas.DrawnViewport is not { } frame || !ReferenceEquals(host.Frames.Presentation, frame.Presentation)) return;
            host.Frames.Presented(frame);
            var check = Volatile.Read(ref pending); var surface = frame.Presentation.Surface;
            if (check == null || frame.Metadata.Tick != check.Tick || surface.Identity.Generation != check.Generation ||
                surface.Width != check.Width || surface.Height != check.Height || Interlocked.CompareExchange(ref pending, null, check) != check) return;
            try
            {
                using var image = ui.ReadbackOpenGLImage(); using var bitmap = SKBitmap.FromImage(image);
                var bounds = view.Canvas.ImageBounds; var scale = ui.Viewport.Scale;
                Probe.Check(Math.Abs(bounds.Width / bounds.Height - (double)surface.Width / surface.Height) < .001,
                    "Viewport distorted the perspective aspect ratio.");
                SKColor At(double x, double y) => bitmap.GetPixel((int)((bounds.X + bounds.Width * x) * scale), (int)((bounds.Y + bounds.Height * y) * scale));
                bool Near(SKColor a, SKColor b) => Math.Abs(a.Red - b.Red) <= 2 && Math.Abs(a.Green - b.Green) <= 2 && Math.Abs(a.Blue - b.Blue) <= 2 && a.Alpha == 255;
                Probe.Check(Near(At(.02, .02), new(10, 15, 26, 255)) && Near(At(.98, .02), new(10, 15, 26, 255)), "3D clear/origin/opacity mismatch.");
                var counts = new List<int>(); double centroid = 0;
                // These world centers/materials are independent expected fixture values, not renderer output.
                // The blue child inherits a 90-degree rotated, 2x scaled parent; its left face faces the camera (.65 light).
                var checks = new[] { (new Vector3(-1.5f, 1, -1), new SKColor(204, 51, 38)),
                    (new Vector3(1.5f, 1, -1), new SKColor(51, 204, 77)),
                    (new Vector3(0, 1, -2), check.Active ? new SKColor(166, 133, 17) : new SKColor(25, 66, 149)) };
                var projection = new TrueMoon.Aether.Rendering.PrimitiveScene(check.Camera, 0, Vector3.One)
                    .CreateViewProjection(check.Width, check.Height);
                foreach (var (position, color) in checks)
                {
                    var clip = Vector4.Transform(new Vector4(position, 1), projection);
                    var xCenter = (clip.X / clip.W + 1) / 2; var yCenter = (clip.Y / clip.W + 1) / 2;
                    Probe.Check(Near(At(xCenter, yCenter), color), $"Room center/material/depth mismatch at {position}: {At(xCenter, yCenter)} expected {color}.");
                    var count = 0; double sum = 0;
                    for (var y = 2; y < 98; y++) for (var x = 2; x < 98; x++)
                        if (Near(At(x / 100d, y / 100d), color)) { count++; sum += x / 100d; }
                    Probe.Check(count is > 20 and < 1500, "Missing or unbounded room object.");
                    counts.Add(count); if (counts.Count == 1) centroid = sum / count;
                }
                Probe.Check(Near(At(.5, .9), new(80, 92, 103)), "Room floor missing.");
                Probe.Check(Near(check.Camera.Pitch == 0 ? At(.5, .42) : At(.38, .35), new(115, 115, 128)), "Room back wall missing.");
                centroids.Add(centroid);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100); var fileName = check.Name + ".png";
                using (var output = File.Create(Path.Combine(directory, fileName))) encoded.SaveTo(output);
                captures.Add(new { check.Name, check.Tick, check.Color, Surface = surface, Frame = frame.Metadata,
                    ObjectPixels = counts, CentroidX = centroid, Image = fileName, Sha256 = Convert.ToHexString(SHA256.HashData(encoded.ToArray())) });
                check.Completion.TrySetResult();
            }
            catch (Exception error) { check.Completion.TrySetException(error); }
        };
        async Task Capture(string name, ulong tick, ulong generation, int width, int height, uint color, bool active = false)
        {
            var state = (await host.Supervisor.CaptureCheckpointAsync(token)).State;
            Probe.Check(state.Tick == tick && state.Objects.Entries.Where(e => e.EntityId == "00000004-0000-0000-0000-000000000000").Any(e => e.Active) == active,
                "Unexpected object state before pixel capture.");
            var check = new Pending(name, tick, generation, width, height, color, state.Camera, active);
            window.PostWindow(_ =>
            {
                view!.Status.Value = $"Vulkan 3D | {host.Supervisor.Status.State} | tick {host.Supervisor.Status.Tick}";
                Volatile.Write(ref pending, check); window.RequestFrame();
            });
            await check.Completion.Task.WaitAsync(token);
        }
        try
        {
            await app.StartAsync(token); await binding.InitializeAsync(window, host, token);
            window.Closing += async _ => { await host.DisposeAsync(); Probe.Check(view is { IsDisposed: false } && host.Gpu.ResourceCount == 0, "Window closed before engine drain."); return true; };
            var source = File.ReadAllText(Path.Combine(CheckpointProbe.FindWorkspace(), "ManualTests/Game.Logic/RoomGame.cs"));
            var a = Project("a", source, 1); var b = Project("b", source.Replace("0xff2080c0", "0xffc06020"), 2);
            if (interactionProof) await Interactions(a, b);
            else
            {
            GameEditorContext Context(GameBuildRequest build) => new(build, RoomScene.Snapshot(), authoring.Selection);
            host.Editor.SetContext(Context(a)); await host.Editor.BuildAsync(token); await host.Editor.StartAsync(640, 360, token);
            await Capture("01-room", 0, 1, 640, 360, 0xff2080c0);
            await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(true, GameButtons.Right, 0, 0, true), token);
            for (var i = 0; i < 3; i++) await host.Editor.StepAsync(token);
            await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, default, token);
            await Capture("02-camera", 3, 1, 640, 360, 0xff2080c0);
            Probe.Check(centroids[1] < centroids[0] - .008, "Camera movement did not shift projected geometry left.");
            await host.Frames.ResizeAsync(800, 450, token); await Capture("03-resize", 3, 1, 800, 450, 0xff2080c0);
            await host.Frames.ResizeAsync(0, 0, token);
            await host.Frames.ResizeAsync(320, 240, token); await Capture("04-resume", 3, 1, 320, 240, 0xff2080c0);
            var state = (await host.Supervisor.CaptureCheckpointAsync(token)).State;
            Probe.Check(state.Tick == 3 && Math.Abs(state.Camera.X - .15f) < .0001f && !state.Interaction, "Room out-of-reach input/checkpoint mismatch.");
            host.Editor.SetContext(Context(b)); await host.Editor.BuildAsync(token); await host.Editor.ReplaceAsync(token);
            await Capture("05-replace-room", 3, 2, 320, 240, 0xffc06020);
            Probe.Check((await host.Supervisor.CaptureCheckpointAsync(token)).State == state, "Room Replace lost supported checkpoint state.");
            Probe.Check(Math.Abs(centroids[4] - centroids[3]) < .005 && host.Supervisor.Status.State == SessionState.Paused,
                "Replace failed to retain camera/tick/mode.");
            await host.Editor.StopAsync();
            }
            Probe.Check(window.Id == windowId && view is { IsDisposed: false } && JsonSerializer.Serialize(authoring) == before, "3D changed Editor ownership/authoring.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { await host.Editor.StopAsync(); await app.StopAsync(); await app.DisposeAsync(); }
            catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
            try
            {
                var cleanup = factory.Inner.Leases.Select(l => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, $"surface-cleanup-{l.Identity.Generation}.json")))).ToArray();
                Probe.Check(cleanup.Length == factory.Inner.Leases.Count && cleanup.All(c => (Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") != "1" || c.GetProperty("LastSurfaceProducer").GetProperty("ValidationEnabled").GetBoolean()) && c.GetProperty("LastSurfaceProducer").GetProperty("Errors").GetInt32() == 0 &&
                    c.GetProperty("LastSurfaceProducer").GetProperty("Warnings").GetInt32() == 0 && c.GetProperty("ResourceCount").GetInt32() == 0 && c.GetProperty("OpenHandleCount").GetInt32() == 0 && c.GetProperty("Error").ValueKind == JsonValueKind.Null), "Runtime validation/cleanup failed.");
                Probe.Check(host.Gpu.ResourceCount == 0 && host.Gpu.OpenHandleCount == 0 && host.Frames.MappingCount == 0 && sessions.ActiveSessionCount == 0 &&
                    factory.Inner.Leases.All(l => l.Reaped && l.ResourcesReleased) && view is { IsDisposed: true } && binding.ImportReadbacks == 0, "3D ownership/readback regression.");
                Probe.Check(!factory.Inner.Diagnostics.Any(d => d.Stage == TrueMoon.Mithril.UnitProcessStage.Killing ||
                    d.Stage == TrueMoon.Mithril.UnitProcessStage.ProcessReleased && d.ExitCode != 0), "3D child was killed/failed.");
                Probe.Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Game loaded in Editor.");
                if (failure == null) Probe.Check(captures.Count == (interactionProof ? 9 : 5) && factory.Inner.Leases.Count == (interactionProof ? 4 : 2) &&
                    (!interactionProof || cases.Count == 10), "Incomplete 3D case matrix.");
                passed = failure == null;
            }
            catch (Exception validation) { failure = failure == null ? validation : new AggregateException(failure, validation); }
            Records.Write(directory, interactionProof ? "interaction-report.json" : "room-report.json", new { Passed = passed, Cases = cases, ValidationRequested = Environment.GetEnvironmentVariable("TRUEMOON_GPU_VALIDATION") == "1", Captures = captures, ImportReadbacks = binding.ImportReadbacks,
                Resources = host.Gpu.ResourceCount, Handles = host.Gpu.OpenHandleCount, Mappings = host.Frames.MappingCount, UiSessions = sessions.ActiveSessionCount,
                Children = factory.Inner.Leases.Select(l => new { l.ProcessId, l.Identity.Generation, l.Reaped, l.ResourcesReleased, l.StandardError }), Error = failure?.ToString() });
        }
        if (failure != null) throw failure;
        return 0;

        async Task Interactions(GameBuildRequest a, GameBuildRequest b)
        {
            async Task<GameCheckpointState> State() => (await host.Supervisor.CaptureCheckpointAsync(token)).State;
            async Task Press(float pitch = 0)
            {
                await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(true, GameButtons.None, 0, pitch, true), token);
                await host.Editor.StepAsync(token);
            }
            async Task Approach()
            {
                for (var i = 0; i < 80; i++)
                {
                    if (i % 8 == 0) await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, new(true, GameButtons.Forward), token);
                    await host.Editor.StepAsync(token);
                }
                await host.Supervisor.SendInputAsync(host.Supervisor.Status.Identity!, default, token);
            }
            Task Image(string name, int width = 640, int height = 360, bool active = false)
                => Capture(name, host.Supervisor.Status.Tick, host.Supervisor.Status.Identity!.Generation, width, height, 0, active);
            host.Editor.SetContext(new(a, RoomScene.Snapshot(), authoring.Selection)); await host.Editor.BuildAsync(token); await host.Editor.StartAsync(640, 360, token);
            await Image("01-far"); await Press();
            Case("out-of-reach-does-not-toggle", !(await State()).Interaction);
            await Approach(); var near = await State();
            Case("fixed-ticks-reach-target", Math.Abs(near.Camera.Z - 2) < .0001f && near.Camera.X == 0 && near.Objects.Entries.Length == 3);
            await Image("02-near"); await Press(-.15f);
            var active = await State();
            Case("only-nearest-object-toggled", active.Interaction && active.Objects.Entries.Count(e => e.Active) == 1 &&
                active.Objects.Entries.Single(e => e.Active).EntityId == "00000004-0000-0000-0000-000000000000" && Math.Abs(active.Camera.Pitch + .15f) < .0001f);
            await host.Editor.StepAsync(token);
            Case("interaction-pulse-consumed-once", (await State()).Objects.Equals(active.Objects));
            await Image("03-active", active: true);
            await host.Frames.ResizeAsync(800, 450, token); await Image("04-resize-active", 800, 450, true);
            await host.Frames.ResizeAsync(0, 0, token); await host.Frames.ResizeAsync(320, 240, token); await Image("05-resume-active", 320, 240, true);
            var beforeReplace = await State(); Case("resize-suspend-preserve-object-state", beforeReplace.Objects.Equals(active.Objects));
            host.Editor.SetContext(new(b, RoomScene.Snapshot(), authoring.Selection)); await host.Editor.BuildAsync(token); await host.Editor.ReplaceAsync(token);
            Case("replace-retains-complete-state", await State() == beforeReplace && host.Supervisor.Status.State == SessionState.Paused);
            await Image("06-replaced-active", 320, 240, true); await host.Editor.StepAsync(token);
            var step = await State(); Case("replace-clears-transient-input", step.Camera == beforeReplace.Camera && step.Objects.Equals(beforeReplace.Objects));
            await Press(); Case("second-press-restores-authored-color", !(await State()).Interaction); await Image("07-off", 320, 240);
            await host.Editor.StopAsync();
            foreach (var variant in new[] { "occluded", "disabled" })
            {
                host.Editor.SetContext(new(b, RoomScene.Snapshot(variant), authoring.Selection)); await host.Editor.BuildAsync(token); await host.Editor.StartAsync(640, 360, token);
                await Approach(); await Press(-.15f);
                var state = await State(); Case(variant + "-target-not-toggled", !state.Interaction && state.Objects.Entries.All(e => !e.Active));
                await Image(variant == "occluded" ? "08-occluded" : "09-disabled"); await host.Editor.StopAsync();
            }
        }

        GameBuildRequest Project(string name, string text, long revision)
        {
            var input = Path.Combine(directory, "input-" + name); Directory.CreateDirectory(input);
            File.WriteAllText(Path.Combine(input, "Game.cs"), text);
            File.WriteAllText(Path.Combine(input, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
            var path = Path.Combine(input, "Game.Logic.csproj");
            File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><Reference Include=\"TrueMoon.Aether\"><HintPath>$(AetherContractPath)</HintPath><Private>false</Private></Reference></ItemGroup></Project>");
            return new(path, "Game.Logic.dll", "Game.Logic.RoomGame", revision);
        }
    }
    private sealed record Pending(string Name, ulong Tick, ulong Generation, int Width, int Height, uint Color, CameraTransform Camera, bool Active)
    { internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    private sealed class SceneFactory : IRuntimeFactory
    {
        internal NativeRuntimeFactory Inner { get; } = new();
        public IRuntimeLease Create(SessionLaunch launch) => Inner.Create(launch with { Behavior = "scene-3d" });
    }
}
