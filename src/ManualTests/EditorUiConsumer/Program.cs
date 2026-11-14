using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;

namespace TrueMoon.ManualTests.EditorUiConsumer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--runtime" })) { await Child(); return 0; }
            if (args.Length is < 2 or > 3 || args.Length == 3 && args[2] != "microsoft") throw new ArgumentException("EditorUiConsumer <artifact-directory> <report-directory> [microsoft]");
            await Probe(args[0], args[1], args.Length == 3); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task Child()
    {
        var bootstrap = RuntimeBootstrap.ReadEnvironment(); var launch = bootstrap.Launch;
        await using var app = App.Build(context =>
        {
            context.UseConfiguration(ConfigurationProfiles.Create(profile => profile.AddSchema(RuntimeConfigurationSchemas.Engine)));
            context.UseConfiguredAetherRuntime(bootstrap); context.UseAetherSurfaceRendering();
        });
        await app.StartAsync(); var engine = app.Services.Resolve<RuntimeEngine>()!; var runtime = app.Services.Resolve<AetherRuntimeHost>()!;
        try { await runtime.RunAsync(); }
        finally
        {
            await runtime.DisposeAsync(); await app.StopAsync();
            await File.WriteAllTextAsync(Path.Combine(launch.ReportDirectory, "child.json"), JsonSerializer.Serialize(new
            { Pid = Environment.ProcessId, Validation = engine.Gpu.LastSurfaceProducer, Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount, Forbidden = Forbidden() }));
        }
    }
    private static async Task Probe(string artifactDirectory, string reportDirectory, bool microsoft)
    {
        var report = Path.GetFullPath(reportDirectory); Directory.CreateDirectory(report);
        var artifact = GameBuildArtifact.Open(Path.GetFullPath(artifactDirectory), BuildIdentity.HostId);
        var start = PlayerStart.FromSnapshot(artifact, new("captured-runtime-ui", 41, [new() { Id = "seed", X = 7 }]));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(RuntimeConfigurationSchemas.Engine); builder.AddSchema(EditorUiConfigurationSchemas.Window);
            builder.Override("Aether:ReportDirectory", report); builder.Override("Aether:GpuValidation", true);
        });
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string>
        { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf") });
        await using var app = App.Builder(builder => { if (microsoft) builder.UseDI(); }).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEditorUi(start, new RuntimeComposition { HostContractId = BuildIdentity.HostId, GameBuildId = artifact.BuildId, RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime") })
            .UseConfiguredAetherEditorUiWindow(fonts)).Build();
        var session = app.Services.Resolve<RuntimeEditorSession>()!; var engine = app.Services.Resolve<RuntimeEngine>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!; var adapter = app.Services.Resolve<RuntimeEditorWindow>()!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = deadline.Token;
        var checks = 0; int? pid = null; var captures = new List<object>(); Exception? failure = null; UiSession? windowSession = null;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        try
        {
            Check(session.Status.State == SessionState.Stopped && engine.Gpu.ResourceCount == 0 && !Loaded("RenderSurface.Game"), "Eager activation.");
            await app.StartAsync(token); await adapter.Ready.WaitAsync(token);
            await OnUi(view =>
            {
                var model = (RuntimeEditorViewModel)view.DataContext!;
                Check(!model.CanBuild && !model.CanReplace && view.Documents == null && view.Diagnostics == null, "Authoring capabilities leaked.");
                model.Execute(EditorCommand.Build); model.Execute(EditorCommand.Replace);
                Check(session.Status.State == SessionState.Stopped, "Unsupported action started runtime.");
                model.Select("seed"); Check(model.Properties.Single(item => item.Key == "X").Value == "7" && model.IsInspectorVisible, "Read-only inspection failed.");
            });
            await OnUi(view => { Check(view.InspectorIsland.IsOpen, "Inspector did not observe selection."); view.DataContext!.ClearSelection(); Check(!view.DataContext.IsInspectorVisible, "Selection was not cleared."); });
            await Command(EditorCommand.Start); pid = session.Status.ProcessId;
            Check(pid.HasValue && session.Status.State == SessionState.Paused, "Start did not await first presentation.");
            await Capture("paused");
            var before = session.Status.Tick; await Command(EditorCommand.Step);
            Check(session.Status.Tick == before + 1, "Step did not execute exactly once.");
            await Command(EditorCommand.Play); await Wait(async () => { await engine.Supervisor.GetStatusAsync(token); return session.Status.Tick > before + 3; });
            await Command(EditorCommand.Pause); var paused = session.Status.Tick;
            await Task.Delay(100, token); await engine.Supervisor.GetStatusAsync(token);
            Check(session.Status.State == SessionState.Paused && session.Status.Tick == paused, "Pause did not fence ticks.");
            await Capture("after-play");
            await Command(EditorCommand.Stop);
            Check(session.Status.State == SessionState.Stopped && engine.Frames.Presentation == null && engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0, "Runtime resources survived Stop.");
            Check(!Alive(pid!.Value), "Child survived reap.");
            Check(!Loaded("RenderSurface.Game") && Forbidden().Length == 0, "Parent loaded game or Tooling.");
            window.Close(); await window.Completion.WaitAsync(token); await app.StopAsync(token);
            Check(!session.Execute(EditorCommand.Start), "Close did not fence command admission.");
            Check(Directory.GetFiles(report, "*compiler*", SearchOption.AllDirectories).Length == 0, "Compiler work was started.");
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            await app.StopAsync();
            await File.WriteAllTextAsync(Path.Combine(report, "runtime-ui.json"), JsonSerializer.Serialize(new
            { Completed = failure == null, Provider = microsoft ? "Microsoft" : "Cobalt", Checks = checks, ProcessId = pid, Captures = captures,
                Resources = engine.Gpu.ResourceCount, Handles = engine.Gpu.OpenHandleCount, Forbidden = Forbidden(), GameLoadedInParent = Loaded("RenderSurface.Game"), Error = failure?.ToString() }));
        }

        async Task OnUi(Action<EditorView> action)
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Presented(UiSession ui)
            {
                window.FramePresented -= Presented;
                try { windowSession = ui; action((EditorView)ui.Root); done.TrySetResult(); } catch (Exception error) { done.TrySetException(error); }
            }
            window.FramePresented += Presented; window.RequestFrame();
            try { await done.Task.WaitAsync(token); } finally { window.FramePresented -= Presented; }
        }
        async Task Command(EditorCommand command)
        {
            await OnUi(view => { Check(session.CanExecute(command), "Action not admitted: " + command); view.DataContext!.Execute(command); });
            await session.Pending.WaitAsync(token); Check(session.Error == null, "Command failed: " + session.Error);
        }
        async Task Wait(Func<Task<bool>> condition) { while (!await condition()) await Task.Delay(20, token); }
        async Task Capture(string name)
        {
            await OnUi(view =>
            {
                Check(view.Canvas.CanAcknowledgeDraw && view.Canvas.DrawnFrame?.Metadata.RenderView != null, "No actual mesh viewport draw.");
                using var image = ((SkiaOpenGLSurface)windowSession!.Rendering).ReadbackImage();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100); using var file = File.Create(Path.Combine(report, name + ".png")); data.SaveTo(file);
                using var pixels = SKBitmap.FromImage(image); var bounds = view.Canvas.ImageBounds; var scale = windowSession.Viewport.Scale; var white = 0;
                for (var y = (int)(bounds.Y * scale); y < Math.Min(pixels.Height, (bounds.Y + bounds.Height) * scale); y++)
                    for (var x = (int)(bounds.X * scale); x < Math.Min(pixels.Width, (bounds.X + bounds.Width) * scale); x++)
                    { var color = pixels.GetPixel(x, y); if (color.Red > 245 && color.Green > 245 && color.Blue > 245) white++; }
                Check(white > 100, "No rendered mesh pixels."); captures.Add(new { Name = name, White = white, pixels.Width, pixels.Height });
            });
        }
    }
    private static string[] Forbidden() => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name!)
        .Where(name => name is "TrueMoon.Aether.Tooling" or "TrueMoon.Aether.Editor.Tooling" or "TrueMoon.Aether" || name.Contains("CodeAnalysis", StringComparison.Ordinal)).ToArray();
    private static bool Loaded(string name) => AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == name);
    private static bool Alive(int pid) { try { using var process = Process.GetProcessById(pid); return !process.HasExited; } catch (ArgumentException) { return false; } }
}
