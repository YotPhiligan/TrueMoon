using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

namespace AetherGuiConsumer;

internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static readonly Guid CubeId = Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000001");

    private static async Task<int> Main(string[] args)
    {
        if (args is not [var runtime, var report, var composition, var provider, var scenario]) return 2;
        runtime = Path.GetFullPath(runtime); report = Path.GetFullPath(report);
        if (Directory.Exists(report)) throw new IOException("Use a fresh GUI report directory.");
        Directory.CreateDirectory(report);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
            await Run(runtime, report, composition == "embedded", provider == "di", scenario, deadline.Token);
            return 0;
        }
        catch (Exception error)
        {
            await File.WriteAllTextAsync(Path.Combine(report, "failure.txt"), error.ToString());
            Console.Error.WriteLine(error); return 1;
        }
    }

    private static async Task Run(string runtime, string report, bool embedded, bool microsoft, string scenario, CancellationToken token)
    {
        var projectRoot = Path.Combine(report, "project");
        Directory.CreateDirectory(Path.Combine(projectRoot, "Game"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Scenes"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Content"));
        File.WriteAllText(Path.Combine(projectRoot, "NuGet.Config"), "<configuration><packageSources><clear /></packageSources></configuration>");
        SceneGameSource.Write(Path.Combine(projectRoot, "Game"));
        var project = new ProjectDescription(1, Guid.NewGuid(), "External consumer · Мир", "Game/Game.csproj",
            "Game.Logic.dll", "Game.Logic.ProbeGame", "Scenes", "Content");
        var manifest = Path.Combine(projectRoot, "consumer.aetherproj");
        File.WriteAllText(manifest, JsonSerializer.Serialize(project, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "NotoSans-Regular.ttf");
        var fonts = SkiaFontCatalog.Load("Noto Sans", new Dictionary<string, string> { ["Noto Sans"] = fontPath });
        using var typeface = SKTypeface.FromFile(fontPath);
        Check(typeface.FamilyName == "Noto Sans" && typeface.GlyphCount > 100, "Delivered font was not loaded by native Skia.");
        using var process = Process.GetCurrentProcess();
        var loadedNative = process.Modules.Cast<ProcessModule>().Single(m => m.ModuleName == "libSkiaSharp.dll").FileName;
        Check(Path.GetFullPath(loadedNative) == Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "truemoon-native", "win-x64", "libSkiaSharp.dll")),
            "Skia loaded the stock DLL instead of the extended native asset.");
        var selectedRuntime = new NativeRuntimeLaunch(runtime, Path.ChangeExtension(runtime, ".dll"), ["--console-diagnostics"]);
        var hostId = BuildIdentity.ForRuntime(selectedRuntime.RuntimeAssemblyPath);
        var capture = new EditorViewReference();
        var ready = new TaskCompletionSource<UiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var app = App.Builder(c => { if (microsoft) c.UseDI(); }).Setup(c =>
        {
            c.UseDiagnostics(new DiagnosticPipelineOptions { JsonDirectory = Path.Combine(report, "diagnostics", "editor"), JsonUseRunDirectories = true });
            c.Services(s =>
            {
                s.Singleton<SceneWriteProbe>(r => new(new JsonSceneDocumentStore(r.Resolve<SceneFileCodec>())));
                s.Singleton<ISceneDocumentStore>(r => r.Resolve<SceneWriteProbe>());
                if (embedded) s.Singleton<ConsumerViewport>(_ => new());
                if (scenario == "failure") s.Singleton<IViewStyle<EditorView>>(_ => new FailingStyle());
            });
            c.UseAetherEditor(o =>
            {
                o.RuntimeLaunch = selectedRuntime; o.HostContractId = hostId; o.GameBuildId = BuildIdentity.PreviewId; o.ReportDirectory = report;
                if (embedded) o.ViewportTransportFactory = r => r.Resolve<ConsumerViewport>();
            }, o => o.ProjectPath = manifest);
            if (!embedded) c.UseAetherEditorWindow(fonts);
            else c.UsePresentation(r =>
            {
                capture.View = r.Resolve<IFactory<EditorView>>().Create()!;
                capture.Parent = new Border { Padding = new(12) };
                capture.Parent.SetContent(capture.View);
                return capture.Parent;
            }, p => p.UseSkiaOpenGL(fonts).UseSilkWindow(w =>
            {
                w.Title = "Consumer-owned UI · embedded Editor"; w.Width = 1280; w.Height = 800;
                w.FrameMode = UiFrameMode.Continuous;
            }));
            // A custom host defines its own presentation readiness, just like EditorWindow.Ready.
            if (embedded) c.Services(s => s.Singleton<IStartable>(_ => new FirstPresentation(ready.Task)));
        }).Build();
        var engine = app.Services.Resolve<AetherHost>()!;
        var editor = app.Services.Resolve<EditorSession>()!;
        var projects = app.Services.Resolve<EditorProjects>()!;
        var sessions = app.Services.Resolve<HostedUiSessionFactory>()!;
        var window = app.Services.Resolve<HostedUiWindow>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        var factory = (NativeRuntimeFactory)app.Services.Resolve<TrueMoon.Aether.Sessions.IRuntimeFactory>()!;
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
        void Presented(UiSession ui)
        {
            capture.View ??= (EditorView)((WindowFrame)ui.Root).Content;
            if (embedded) editor.Present(ui, capture.View);
            if (scenario == "cancel") startup.Cancel();
            ready.TrySetResult(ui);
        }
        window.FramePresented += Presented;
        if (embedded) window.Closing += reason => reason == UiCloseReason.Shutdown
            ? StopEditor() : editor.RequestCloseAsync();
        Exception? expectedStartup = null;
        var observedCleanupFaults = new List<string>();
        object? workflow = null; var recreated = false;
        try
        {
            var store = app.Services.Resolve<ISceneDocumentStore>()!;
            await store.WriteAsync(Path.Combine(projectRoot, "Scenes", "Original.aetherscene"), Scene(), null, token);
            try { await app.StartAsync(startup.Token); }
            catch (Exception error) when (scenario is "failure" or "cancel") { expectedStartup = error; }
            if (scenario is "failure" or "cancel")
            {
                Check(expectedStartup != null, "Injected startup did not fail/cancel.");
                Check(scenario == "failure" ? Contains(expectedStartup!, "Acceptance: view startup failure") : startup.IsCancellationRequested,
                    "Startup failed for an unrelated reason.");
                Check(window.AppearanceCapabilities != null, "Startup case did not reach actual native window creation.");
            }
            else
            {
                var ui = await ready.Task.WaitAsync(token);
                if (!embedded) await app.Services.Resolve<EditorWindow>()!.Ready.WaitAsync(token);
                else
                {
                    var adapter = app.Services.Resolve<ConsumerViewport>()!;
                    await adapter.Initialize(window, engine, ui, capture, hostId, token);
                    await ui.InvokeAsync(() =>
                    {
                        var old = capture.View!; var model = old.DataContext;
                        capture.Parent!.SetContent(null); old.Dispose();
                        capture.View = app.Services.Resolve<IFactory<EditorView>>()!.Create()!;
                        capture.Parent.SetContent(capture.View);
                        Check(old.IsDisposed && old.Inspector!.IsDisposed && ReferenceEquals(model, capture.View.DataContext),
                            "Embedded detach/recreate crossed parent/model ownership.");
                        recreated = true;
                    }).WaitAsync(token);
                }
                try { workflow = await Workflow.Run(report, window, ui, capture.View!, app, token); }
                catch (Exception error)
                {
                    await File.WriteAllTextAsync(Path.Combine(report, "workflow.failure.txt"), error.ToString(), CancellationToken.None);
                    throw;
                }
                await window.Completion.WaitAsync(token);
            }
        }
        finally
        {
            window.FramePresented -= Presented;
            try { await documents.StopAsync(); }
            finally
            {
                try
                {
                    try { await app.StopAsync(); }
                    catch (Exception error) when (scenario == "failure" && IsInjectedFailure(error)) { observedCleanupFaults.Add("Stop: " + error.GetType().FullName); }
                }
                finally
                {
                    if (embedded) app.Services.Resolve<ConsumerViewport>()!.Detach();
                    try { await app.DisposeAsync(); }
                    catch (Exception error) when (scenario == "failure" && IsInjectedFailure(error)) { observedCleanupFaults.Add("Dispose: " + error.GetType().FullName); }
                }
            }
        }
        Check(sessions.ActiveSessionCount == 0 && engine.Gpu.ResourceCount == 0 && engine.Gpu.OpenHandleCount == 0 &&
            engine.Gpu.CleanupError == null && engine.Builds.ProcessId == null && factory.Leases.All(l => l.Reaped && l.ResourcesReleased),
            "Native cleanup retained owned resources.");
        Check(projects.IsClosed && projects.Active == null && (capture.View == null || capture.View.IsDisposed), "UI/project owner did not close.");
        Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Game.Logic"), "Editor loaded Game.Logic.");
        var runtimeReports = Directory.GetFiles(report, "runtime-*.json").Select(p => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(p))).ToArray();
        if (scenario == "workflow")
        {
            Check(runtimeReports.Length == 10 && runtimeReports.Count(child => child.GetProperty("GameLoaded").GetBoolean()) == 2,
                "Missing graceful preview/game validation reports.");
            foreach (var child in runtimeReports)
            {
                var validation = child.GetProperty("Validation");
                Check(validation.GetProperty("ValidationEnabled").GetBoolean() && validation.GetProperty("Errors").GetInt32() == 0 &&
                    validation.GetProperty("Warnings").GetInt32() == 0 && child.GetProperty("Resources").GetInt32() == 0 && child.GetProperty("Handles").GetInt32() == 0 &&
                    !child.GetProperty("EditorLoaded").GetBoolean() && !child.GetProperty("UiLoaded").GetBoolean(),
                    "Runtime validation, cleanup or role isolation failed.");
                if (child.GetProperty("GameLoaded").GetBoolean())
                {
                    var build = child.GetProperty("Build");
                    var prefix = Path.TrimEndingDirectorySeparator(build.GetProperty("DirectoryPath").GetString()!) + Path.DirectorySeparatorChar;
                    Check(child.GetProperty("GameBuildId").GetString() == build.GetProperty("BuildId").GetString() &&
                        child.GetProperty("GameAssemblyPaths").EnumerateArray().All(path => path.GetString()!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
                        "Game child loaded an unselected artifact.");
                }
                Check(validation.GetProperty("LiveSlots").GetInt32() == 0 && validation.GetProperty("OpenHandles").GetInt32() == 0 &&
                    validation.GetProperty("DevicesCreated").GetInt32() == validation.GetProperty("DevicesReleased").GetInt32() &&
                    validation.GetProperty("SlotsCreated").GetInt32() == validation.GetProperty("SlotsReleased").GetInt32() &&
                    validation.GetProperty("SurfacesCreated").GetInt32() == validation.GetProperty("SurfacesReleased").GetInt32(),
                    "Runtime producer retained native surfaces/slots/devices.");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(report, "report.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Scenario = scenario, Embedded = embedded, MicrosoftDI = microsoft, RecreatedView = recreated, Workflow = workflow,
            StartupError = expectedStartup?.GetType().FullName, ObservedCleanupFaults = observedCleanupFaults, NativeWindowCreated = window.AppearanceCapabilities != null,
            WindowCompleted = window.Completion.IsCompleted, ActiveUiSessions = sessions.ActiveSessionCount,
            GpuResources = engine.Gpu.ResourceCount, GpuHandles = engine.Gpu.OpenHandleCount,
            Native = new { Path = loadedNative, Sha256 = Hash(loadedNative), SkiaVersion = SkiaSharpVersion.Native },
            Font = new { typeface.FamilyName, typeface.GlyphCount, Sha256 = Hash(fontPath) },
            Children = factory.Leases.Select(l => new { l.ProcessId, l.Reaped, l.ResourcesReleased }), RuntimeReports = runtimeReports,
            Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).Select(a => new { Name = a.GetName().Name, Path = a.Location }).ToArray()
        }, Json), token);
        async Task<bool> StopEditor() { await editor.StopAsync(); return true; }
    }

    private sealed class FailingStyle : IViewStyle<EditorView>
    { public void Configure(IViewStyleBuilder<EditorView> styles) => throw new IOException("Acceptance: view startup failure"); }
    private sealed class FirstPresentation(Task<UiSession> ready) : IStartable
    {
        public async Task StartAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); await ready.WaitAsync(cancellationToken); }
    }
    private static SceneDocument Scene()
    {
        var definition = new EntityDefinitionId(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000030"));
        var empty = new EntityDefinitionId(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000031"));
        var transform = new ComponentId(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000010"));
        var appearance = new ComponentId(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000011"));
        static KeyValuePair<string, PropertyValue> Value(string key, double value) => KeyValuePair.Create(key, PropertyValue.From(value));
        return new(new(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000100")), 7,
            [new(new(CubeId), definition), new(new(Guid.Parse("a846e4a2-7c35-4f21-92e8-000000000002")), empty)],
            [new(definition, new(addedComponents:
            [new(transform, BuiltInComponents.TransformId, 1, [Value("x", -1.5), Value("y", 1), Value("z", -1)]),
             new(appearance, BuiltInComponents.AppearanceId, 1, [Value("r", .8), Value("g", .2), Value("b", .15)])])), new(empty, EntityPatch.Empty)], BuiltInComponents.Schemas);
    }
    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static bool Contains(Exception error, string text) => error.ToString().Contains(text, StringComparison.Ordinal);
    private static bool IsInjectedFailure(Exception error) => error is AggregateException aggregate
        ? aggregate.Flatten().InnerExceptions.All(IsInjectedFailure)
        : error is IOException { Message: "Acceptance: view startup failure" };
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
