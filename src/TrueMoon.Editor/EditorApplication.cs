using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;

namespace TrueMoon.Editor;

internal static class EditorApplication
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Contains("--validation")) Environment.SetEnvironmentVariable("TRUEMOON_GPU_VALIDATION", "1");
        string? Value(string flag) { var i = Array.IndexOf(args, flag); if (i < 0) return null; if (i + 1 == args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException(flag + " requires a value."); return args[i + 1]; }
        var smoke = Value("--smoke"); var project = Value("--project") ?? FindDemoProject();
        project = Path.GetFullPath(project); if (!File.Exists(project)) throw new FileNotFoundException("Game project not found. Use --project <game.csproj>.", project);
        if (smoke != null && Directory.Exists(smoke)) throw new IOException("Use a new smoke directory.");
        var directory = Path.GetFullPath(smoke ?? Path.Combine(Path.GetTempPath(), "TrueMoon", "Editor", Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(directory);
        var workspace = new EditorWorkspace(EditorScene.Demo()); var binding = new EditorViewportBinding();
        var runtimeFactory = new NativeRuntimeFactory();
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        using var end = new CancellationTokenSource();
        await using var app = App.Build(context =>
        {
            context.UseAether(options =>
            {
                options.HostContractId = BuildIdentity.HostId; options.GameBuildId = new('0', 64); options.ReportDirectory = directory;
                options.RuntimeFactory = _ => runtimeFactory; options.ViewportTransport = binding.CreateTransport;
            });
            context.UsePresentation(_ => new EditorView(workspace), options => options.UseSkiaOpenGL(fonts).UseSilkWindow(window =>
            {
                window.Width = 1280; window.Height = 800; window.Title = "TrueMoon Editor · " + Path.GetFileNameWithoutExtension(project);
                window.Appearance = new WindowAppearance { Decorated = false };
                window.Chrome = new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true, MinimumSize = new Size(1100, 700) };
                window.FrameMode = UiFrameMode.Continuous;
            }));
        });
        var engine = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        engine.Editor.SetContext(new(new(project, Value("--assembly") ?? "Game.Logic.dll", Value("--entry") ?? "Game.Logic.RoomGame", workspace.Scene.Revision), workspace.Scene, workspace.SelectedId ?? ""));
        var commands = new EditorCommands(engine.Editor, binding.Ready, end.Token);
        var first = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        EditorView? view = null;
        window.FramePresented += ui =>
        {
            var current = (EditorView)((WindowFrame)ui.Root).Children[1];
            if (view == null) { view = current; current.CommandRequested += commands.Execute; }
            binding.Observe(ui, current);
            if (current.Canvas.DrawnFrame is { } frame) engine.Frames.Presented(frame);
            workspace.Refresh(engine.Supervisor.Status, engine.Editor.Status, commands.Error ?? engine.Frames.TransportError); current.Refresh(); first.TrySetResult(ui);
        };
        window.Closing += async _ => { await commands.StopAsync(); return true; };
        try
        {
            using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(startup.Token); var ui = await first.Task.WaitAsync(startup.Token);
            if (args.Contains("--light")) await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" }));
            await binding.InitializeAsync(window, engine, startup.Token);
            if (smoke != null) await EditorSmoke.RunAsync(directory, window, ui, view!, workspace, commands, engine, runtimeFactory);
            await window.Completion;
        }
        finally
        {
            try { await commands.StopAsync(); } finally { end.Cancel(); await app.StopAsync(); }
        }
        if (smoke != null) EditorSmoke.WriteCleanup(directory, sessions, engine, runtimeFactory, view!);
    }
    private static string FindDemoProject()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var current = new DirectoryInfo(start); current != null; current = current.Parent)
            {
                var path = Path.Combine(current.FullName, "ManualTests/Game.Logic/Game.Logic.csproj"); if (File.Exists(path)) return path;
            }
        throw new FileNotFoundException("Use --project <game.csproj> --assembly <game.dll> --entry <game.type> when running outside the source checkout.");
    }
}
