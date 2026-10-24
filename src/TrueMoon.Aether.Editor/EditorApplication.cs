using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor;

internal static class EditorApplication
{
    internal static async Task RunAsync(EditorLaunchOptions options)
    {
        if (options.Validation) Environment.SetEnvironmentVariable("TRUEMOON_GPU_VALIDATION", "1");
        var smoke = options.SmokeDirectory;
        if (options.LegacyProject && !File.Exists(options.ProjectPath)) throw new FileNotFoundException("Game project not found.", options.ProjectPath);
        if (smoke != null && Directory.Exists(smoke)) throw new IOException("Use a new smoke directory.");
        var directory = Path.GetFullPath(smoke ?? Path.Combine(Path.GetTempPath(), "TrueMoon", "Editor", Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(directory);
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        using var end = new CancellationTokenSource();
        IFactory<EditorHeaderView, EditorHeaderArguments>? headerFactory = null;
        await using var app = App.Build(context =>
        {
            context.UseAetherAuthoring(); context.UseAetherEditorDocuments();
            context.UseAetherEditorProjects();
            context.Services(services => services
                .Singleton<EditorWorkspace>(resolver =>
                {
                    var selected = resolver.Resolve<EditorProjects>().Active;
                    var scene = options.LegacyProject ? EditorScene.Demo() : new SceneSnapshot(selected == null ? "Проект не открыт" : "Сцена не открыта", 0, []);
                    return new(scene, hasProject: selected != null || options.LegacyProject);
                })
                .Singleton<EditorViewportBinding>(_ => new())
                .Singleton<EditorPreview>(resolver =>
                {
                    var host = resolver.Resolve<AetherHost>()!; var viewport = resolver.Resolve<EditorViewportBinding>()!;
                    return new(async (scene, token) =>
                    {
                        await viewport.Ready.WaitAsync(token).ConfigureAwait(false);
                        return await host.Preview.ShowAsync(scene, 640, 360, token).ConfigureAwait(false);
                    }, host.Preview.StopAsync);
                })
                .Singleton<IViewStyle<EditorView>>(_ => new EditorViewStyle())
                .Singleton<EditorDocumentCommands>(resolver =>
                {
                    var engine = resolver.Resolve<AetherHost>()!; var commands = resolver.Resolve<EditorCommands>()!;
                    var preview = resolver.Resolve<EditorPreview>()!;
                    return new(resolver.Resolve<EditorProjects>()!, () => commands.Pending.IsCompleted &&
                        (engine.Supervisor.Status.State == SessionState.Stopped || preview.Read().Active) &&
                        engine.Editor.Status.Operation == GameEditorOperation.Idle,
                        commands.StopAsync, end.Token, preview.StopAsync);
                })
                .Singleton<EditorDocumentViewModel>(resolver => new(resolver.Resolve<EditorDocumentCommands>()!))
                .Singleton<IViewStyle<EditorDocumentView>>(_ => new EditorDocumentViewStyle())
                .Singleton<IFactory<EditorDocumentView>>(resolver => new Factory<EditorDocumentView>(() =>
                    new(resolver.Resolve<EditorDocumentViewModel>()!, resolver.Resolve<IViewStyle<EditorDocumentView>>()!, compactHeader: true)))
                .Singleton<EditorViewModel>(resolver => new(resolver.Resolve<EditorWorkspace>(), resolver.Resolve<EditorCommands>(), resolver.Resolve<EditorDocumentViewModel>(), resolver.Resolve<EditorTransformViewModel>()))
                .Singleton<IFactory<EditorView>>(resolver =>
                    new Factory<EditorView>(() => new(resolver.Resolve<EditorViewModel>(), resolver.Resolve<IViewStyle<EditorView>>(), resolver.Resolve<IFactory<EditorDocumentView>>(), resolver.Resolve<IFactory<EditorTransformView>>(), compactHeader: true,
                        resolver.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>())))
                .Singleton<EditorCommands>(resolver => new(resolver.Resolve<AetherHost>().Editor,
                    resolver.Resolve<EditorViewportBinding>().Ready, end.Token)));
            context.UseAetherEditorEditing(previewInStatusBar: true);
            context.UseAetherEditorHeader();
            context.UseAetherEditorStatusBar();
            context.UseAether(options =>
            {
                options.HostContractId = BuildIdentity.HostId; options.GameBuildId = new('0', 64); options.ReportDirectory = directory;
                options.ViewportTransportFactory = resolver => resolver.Resolve<EditorViewportBinding>();
            });
            context.UsePresentationFactory<EditorView>(presentation => presentation.UseSkiaOpenGL(fonts).UseSilkWindow(window =>
            {
                window.Width = 1280; window.Height = 800; window.Title = "TrueMoon Aether Editor · " +
                    (options.ProjectPath == null ? "No project" : Path.GetFileNameWithoutExtension(options.ProjectPath));
                window.Appearance = new WindowAppearance { Decorated = false };
                window.Chrome = new WindowChromeOptions { NativeDrag = true, NativeSnapLayouts = true, MinimumSize = new Size(1100, 700) };
                window.FrameMode = UiFrameMode.Continuous;
                window.ConfigureDefaultTitleBar = (title, content, requestClose) =>
                {
                    var editor = (EditorView)content;
                    var header = headerFactory!.Create(new(editor.Overlays, title.Title, requestClose));
                    try { title.SetHeader(header); editor.Header = header; }
                    catch { if (header.Parent == null && !header.IsDisposed) header.Dispose(); throw; }
                };
            }));
        });
        headerFactory = app.Services.Resolve<IFactory<EditorHeaderView, EditorHeaderArguments>>()!;
        var projects = app.Services.Resolve<EditorProjects>()!;
        var selectedProject = options.ProjectPath != null && !options.LegacyProject
            ? await projects.OpenAsync(options.ProjectPath) : null;
        var engine = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var binding = app.Services.Resolve<EditorViewportBinding>()!;
        var runtimeFactory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        var build = selectedProject?.CreateBuildRequest(workspace.Scene.Revision) ?? (options.LegacyProject
            ? new GameBuildRequest(options.ProjectPath!, options.EntryAssembly ?? "Game.Logic.dll", options.EntryType ?? "Game.Logic.RoomGame", workspace.Scene.Revision)
            : null);
        if (build != null) engine.Editor.SetContext(new(build, workspace.Scene, workspace.SelectedId ?? ""));
        var commands = app.Services.Resolve<EditorCommands>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var model = app.Services.Resolve<EditorViewModel>()!;
        var first = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        EditorView? view = null;
        window.FramePresented += ui =>
        {
            var current = (EditorView)((WindowFrame)ui.Root).Children[1];
            view ??= current;
            binding.Observe(ui, current);
            if (current.Canvas.DrawnFrame is { } frame)
            {
                if (!engine.Preview.Presented(frame)) engine.Frames.Presented(frame);
            }
            model.Refresh(engine.Supervisor.Status, engine.Editor.Status, commands.Error ?? engine.Frames.TransportError);
            current.Canvas.EmptyText = selectedProject?.Documents?.Read().Content == null
                ? "Соберите проект и запустите сцену" : "Откройте Edit Preview в строке состояния";
            current.Documents?.SynchronizeFocus(ui); first.TrySetResult(ui);
        };
        window.Closing += async reason =>
        {
            if (reason == UiCloseReason.Shutdown) { await documents.StopAsync(); return true; }
            return await documents.RequestExitAsync();
        };
        try
        {
            using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(startup.Token); var ui = await first.Task.WaitAsync(startup.Token);
            if (options.Light) await ui.InvokeAsync(() => ui.SetTheme(Theme.Light with { FontFamily = "Noto Sans" }));
            if (build != null) await binding.InitializeAsync(window, engine, startup.Token);
            if (smoke != null)
            {
                using var headerDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await EditorHeaderSmoke.VerifyAsync(directory, window, ui, view!, headerDeadline.Token);
                using var statusDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await EditorStatusBarSmoke.VerifyAsync(directory, window, ui, view!, statusDeadline.Token);
                if (options.TransformSmoke) await EditorTransformSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, app.Services.Resolve<ISceneDocumentStore>()!, app.Services.Resolve<EditorPreview>()!, engine, runtimeFactory);
                else if (options.DocumentSmoke) await EditorDocumentSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, app.Services.Resolve<ISceneDocumentStore>()!, engine, runtimeFactory);
                else if (options.LegacyProject) await EditorSmoke.RunAsync(directory, window, ui, view!, workspace, commands, engine, runtimeFactory);
                else await EditorWorkspaceSmoke.RunAsync(directory, window, ui, view!, workspace, engine, runtimeFactory);
            }
            await window.Completion;
        }
        finally
        {
            try { await documents.StopAsync(); } finally { end.Cancel(); await app.StopAsync(); }
        }
        if (smoke != null)
        {
            EditorSmoke.WriteCleanup(directory, sessions, engine, runtimeFactory, view!);
            EditorWorkspaceSmoke.WriteProjectCleanup(directory, projects, selectedProject);
        }
    }
}
