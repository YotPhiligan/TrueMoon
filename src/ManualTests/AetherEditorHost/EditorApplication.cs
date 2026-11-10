using TrueMoon.Aether;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;

namespace TrueMoon.ManualTests.AetherEditorHost;

internal static class EditorApplication
{
    internal static async Task RunAsync(EditorLaunchOptions options)
    {
        var runtimeSettings = options.ResolveRuntimeSettings(ConfigurationEnvironment.Capture(
            RuntimeStartupSettings.LegacyValidationKey, RuntimeStartupSettings.ValidationKey));
        options = options with { Validation = runtimeSettings.GpuValidation };
        var smoke = options.SmokeDirectory;
        if (options.LegacyProject && !File.Exists(options.ProjectPath)) throw new FileNotFoundException("Game project not found.", options.ProjectPath);
        if (smoke != null && Directory.Exists(smoke)) throw new IOException("Use a new smoke directory.");
        var directory = Path.GetFullPath(smoke ?? Path.Combine(Path.GetTempPath(), "TrueMoon", "Editor", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var buildDirectory=options.AcceptanceSmoke?Path.Combine(Path.GetTempPath(),"TrueMoon","ViewportAcceptance",Guid.NewGuid().ToString("N")):null;
        if(buildDirectory!=null)File.WriteAllText(Path.Combine(directory,"compiler-location.txt"),buildDirectory);
        var fallbackFonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") });
        var fonts = SkiaFontCatalog.FromSystemFonts([FontDefaults.Family, FontDefaults.FallbackFamily], fallbackFonts);
        await using var app = App.Build(context =>
        {
            context.UseConfiguration(ConfigurationProfiles.Create(profile =>
            {
                profile.AddSchemas(); profile.AddSchema(DiagnosticsConfigurationSchema.Default);
                profile.AddSchema(AetherConfigurationSchemas.Engine); profile.AddSchema(AetherConfigurationSchemas.Authoring);
                profile.AddSchema(EditorConfigurationSchemas.Options); profile.AddSchema(EditorConfigurationSchemas.Diagnostics);
                profile.AddSchema(EditorConfigurationSchemas.Window);
                profile.Override("App:Name", "TrueMoon Aether Editor");
                profile.Override("Diagnostics:JsonDirectory", smoke == null ? Path.Combine(Path.GetTempPath(), "TrueMoon", "Diagnostics", "Editor") : Path.Combine(directory, "diagnostics", "editor"));
                profile.Override("Diagnostics:JsonUseRunDirectories", true);
                profile.Override("Aether:ReportDirectory", directory); profile.Override("Aether:BuildDirectory", buildDirectory);
                profile.Override("Aether:GpuValidation", options.Validation);
                profile.Override("Editor:ProjectPath", options.LegacyProject ? null : options.ProjectPath);
                profile.Override("Editor:Window:Title", "TrueMoon Aether Editor · " + (options.ProjectPath == null ? "No project" : Path.GetFileNameWithoutExtension(options.ProjectPath)));
            }));
            context.UseCoreConfiguration().UseConfiguredDiagnostics();
            if (options.LegacyProject) context.Services(services => services
                .Singleton<EditorWorkspace>(_ => new(EditorScene.Demo()))
                .Singleton<IFactory<EditorView>>(r => new Factory<EditorView>(() => new(r.Resolve<EditorViewModel>(),
                    r.Resolve<IViewStyle<EditorView>>(), r.Resolve<IFactory<EditorDocumentView>>(), null, true,
                    r.Resolve<IFactory<EditorStatusBarView, EditorStatusBarArguments>>(), r.Resolve<IFactory<EditorHierarchyView>>()))));
            context.UseConfiguredAetherEditor(new AetherComposition
            {
                HostContractId = BuildIdentity.HostId, GameBuildId = new('0', 64),
                RuntimeLaunch = NativeRuntimeLaunch.CurrentProcess("--runtime")
            });
            context.UseConfiguredAetherEditorWindow(fonts);
        });
        var projects = app.Services.Resolve<EditorProjects>()!;
        var engine = app.Services.Resolve<AetherHost>()!; var window = app.Services.Resolve<HostedUiWindow>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var workspace = app.Services.Resolve<EditorWorkspace>()!;
        var runtimeFactory = (NativeRuntimeFactory)app.Services.Resolve<IRuntimeFactory>()!;
        if (options.LegacyProject) engine.Editor.SetContext(new(new GameBuildRequest(options.ProjectPath!,
            options.EntryAssembly ?? "Game.Logic.dll", options.EntryType ?? "Game.Logic.RoomGame", workspace.Scene.Revision),
            workspace.Scene, workspace.SelectedId ?? ""));
        var commands = app.Services.Resolve<EditorCommands>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var first = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        EditorView? view = null; ProjectSession? selectedProject = null;
        window.FramePresented += ui => { view ??= EditorMainWindow.FromRoot(ui.Root).EditorView; first.TrySetResult(ui); };
        try
        {
            using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await app.StartAsync(startup.Token);
            var ui = await first.Task.WaitAsync(startup.Token);
            await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(startup.Token);
            selectedProject = projects.Active;
            if (options.Light) await ui.InvokeAsync(() => ui.SetTheme(Theme.Light));
            if (smoke != null)
            {
                using var headerDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await EditorHeaderSmoke.VerifyAsync(directory, window, ui, view!, headerDeadline.Token);
                using var borderDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await EditorBorderSmoke.VerifyAsync(directory, window, ui, view!, borderDeadline.Token);
                using var statusDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await EditorStatusBarSmoke.VerifyAsync(directory, window, ui, view!, statusDeadline.Token);
                if (options.NativeInputSmoke) await EditorNativeInputSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, engine, runtimeFactory, app.Services.Resolve<EditorSession>()!, app.Services.Resolve<EditorPreview>()!, app.Services.Resolve<EditorViewportState>()!);
                else if (options.AcceptanceSmoke) await EditorViewportAcceptanceSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, engine, runtimeFactory, app.Services.Resolve<EditorSession>()!, app.Services.Resolve<EditorPreview>()!, app.Services.Resolve<EditorViewportState>()!, app.Services.Resolve<EditorScenePlayback>()!);
                else if (options.TranslationSmoke) await EditorTranslationSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, engine, runtimeFactory, app.Services.Resolve<EditorSession>()!, app.Services.Resolve<EditorPreview>()!);
                else if (options.NavigationSmoke) await EditorViewportInputSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, engine, runtimeFactory, app.Services.Resolve<EditorSession>()!,
                    app.Services.Resolve<EditorPreview>()!, app.Services.Resolve<EditorViewportState>()!);
                else if (options.CameraSmoke) await EditorPreviewCameraSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, engine, runtimeFactory, app.Services.Resolve<EditorSession>()!);
                else if (options.InspectorSmoke) await EditorInspectorSmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, app.Services.Resolve<ISceneDocumentStore>()!, app.Services.Resolve<EditorPreview>()!, engine, runtimeFactory);
                else if (options.HierarchySmoke) await EditorHierarchySmoke.RunAsync(directory, window, ui, view!, workspace,
                    documents, selectedProject!.Documents!, app.Services.Resolve<ISceneDocumentStore>()!, app.Services.Resolve<EditorPreview>()!, engine, runtimeFactory);
                else if (options.TransformSmoke) await EditorTransformSmoke.RunAsync(directory, window, ui, view!, workspace,
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
            try { await documents.StopAsync(); } finally { await app.StopAsync(); }
        }
        if (smoke != null)
        {
            EditorSmoke.WriteCleanup(directory, sessions, engine, runtimeFactory, view!);
            EditorWorkspaceSmoke.WriteProjectCleanup(directory, projects, selectedProject);
        }
    }
}
