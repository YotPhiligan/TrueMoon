using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Sessions;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class AetherEditorCompositionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyApp_RegistrationAndStartupDoNotOpenProjectCreateViewOrLaunchRuntime(bool microsoft)
    {
        using var files = new ProjectFixture();
        var store = new EditorProjectsTests.Store(files.Resolved);
        var toolchain = new EditorProjectsTests.Toolchain();
        var runtime = new NeverRuntime(); var style = new ProbeStyle(); var factories = 0;
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IProjectStore>(_ => store).Singleton<IProjectToolchain>(_ => toolchain)
                .Singleton<IViewStyle<EditorView>>(_ => style));
            Assert.Same(context, context.UseAetherEditor(engine =>
            { Configure(engine, files, runtime); engine.RuntimeFactory = _ => { factories++; return runtime; }; }));
        }).Build();
        Assert.Equal(0, factories); Assert.Equal(0, style.Configurations); Assert.Equal(0, store.ReadCalls);
        Assert.False(Directory.Exists(Path.Combine(files.Root, "reports")));
        await app.StartAsync();
        var editor = app.Services.Resolve<EditorSession>()!;
        var engine = app.Services.Resolve<AetherHost>()!;
        Assert.Contains(editor, app.Services.ResolveAll<IStartable>());
        Assert.Contains(editor, app.Services.ResolveAll<IStoppable>());
        Assert.False((object)editor is IDisposable); Assert.False((object)editor is IAsyncDisposable);
        Assert.Equal(1, factories); Assert.Equal(0, runtime.Launches); Assert.Equal(0, style.Configurations);
        Assert.Equal(0, store.ReadCalls); Assert.Empty(toolchain.Paths);
        Assert.Null(app.Services.Resolve<EditorProjects>()!.Active);
        Assert.False(app.Services.Resolve<EditorWorkspace>()!.HasProject);
        Assert.Equal(SessionState.Stopped, engine.Supervisor.Status.State);
        Assert.Equal(0, engine.Gpu.ResourceCount); Assert.Equal(0, engine.Gpu.OpenHandleCount);
        await app.StopAsync();
        Assert.True(app.Services.Resolve<EditorProjects>()!.IsClosed);
        Assert.True(app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed);
        Assert.True(engine.Editor.Status.Closed); Assert.Equal(0, runtime.Disposals);
        await app.DisposeAsync(); await app.DisposeAsync();
        Assert.Equal(1, runtime.Disposals); Assert.Equal(1, store.Disposals); Assert.Equal(1, toolchain.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ViewFactory_ParentOwnsIndependentViewsAndAppPreservesInjectedStyle(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime(); var style = new ProbeStyle();
        using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IViewStyle<EditorView>>(_ => style));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }).Build();
        var factory = app.Services.Resolve<IFactory<EditorView>>()!;
        var editor = app.Services.Resolve<EditorSession>()!;
        var model = app.Services.Resolve<EditorViewModel>()!;
        var first = factory.Create()!; var second = factory.Create()!;
        using var firstUi = DocumentUiFixture.Ui(first); using var secondUi = DocumentUiFixture.Ui(second);
        firstUi.Update(); secondUi.Update(); editor.Present(firstUi, first); editor.Present(secondUi, second);
        Assert.Same(model, first.DataContext); Assert.Same(model, second.DataContext);
        Assert.Same(style, app.Services.Resolve<IViewStyle<EditorView>>()); Assert.Equal(2, style.Configurations);
        Assert.NotSame(first, second); Assert.NotSame(first.Documents, second.Documents);
        Assert.NotSame(first.Inspector, second.Inspector); Assert.NotSame(first.Hierarchy, second.Hierarchy);
        Assert.NotSame(first.Canvas, second.Canvas); Assert.NotNull(first.StatusBar);
        firstUi.Dispose(); Assert.True(first.IsDisposed); Assert.False(second.IsDisposed);
        editor.Present(secondUi, second);
        Assert.IsType<InvalidOperationException>(Complete(Task.Run(() => Record.Exception(() => editor.Present(secondUi, second)))));
        using (var third = factory.Create()!) Assert.Same(model, third.DataContext);
        Assert.Equal(3, style.Configurations);
        Assert.Throws<ObjectDisposedException>(() => editor.Present(firstUi, first));
        Assert.Equal(0, runtime.Launches);
        var status = app.Services.Resolve<AetherHost>()!.Editor.Status;
        app.Dispose(); Assert.False(second.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => model.Refresh(new(SessionState.Stopped), status));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProjectStartup_CopiesOptionsAndUsesSharedDocumentSaveAndClosePolicy(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime();
        var store = new EditorProjectsTests.Store(files.Resolved); var toolchain = new EditorProjectsTests.Toolchain();
        AetherEditorOptions captured = null!; AetherOptions capturedEngine = null!;
        using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IProjectStore>(_ => store).Singleton<IProjectToolchain>(_ => toolchain));
            context.UseAetherEditor(engine => { Configure(engine, files, runtime); capturedEngine = engine; },
                options => { options.ProjectPath = files.Manifest; captured = options; });
        }).Build();
        captured.ProjectPath = "changed.csproj"; capturedEngine.HostContractId = "invalid after setup";
        DocumentUiFixture.Wait(app.StartAsync());
        DocumentUiFixture.Wait(app.StartAsync());
        var projects = app.Services.Resolve<EditorProjects>()!; var project = projects.Active!;
        Assert.Equal(files.Manifest, project.Project.ManifestPath); Assert.Equal(1, store.ReadCalls);
        Assert.Equal(new[] { files.Resolved.GameProjectPath }, toolchain.Paths);
        Assert.True(app.Services.Resolve<EditorWorkspace>()!.HasProject);
        var editor = app.Services.Resolve<EditorSession>()!;
        var documents = app.Services.Resolve<EditorDocumentCommands>()!;
        DocumentUiFixture.Wait(project.Documents!.NewAsync(DocumentData.Scene()));
        var view = app.Services.Resolve<IFactory<EditorView>>()!.Create()!; using var ui = InspectorFixture.Ui(view);
        ui.Update(); editor.Present(ui, view); ui.Update();
        Assert.Equal(2, app.Services.Resolve<EditorWorkspace>()!.EntityCount);
        Assert.True(documents.Read().Document!.IsDirty); Assert.True(view.Documents!.Commands[DocumentCommand.Save].IsEnabled);
        var close = editor.RequestCloseAsync();
        Assert.True(SpinWait.SpinUntil(() => documents.Read().AwaitingDecision, TimeSpan.FromSeconds(5)));
        Assert.True(documents.Choose(DocumentCloseChoice.Cancel)); Assert.False(Complete(close));
        Assert.False(project.Documents.Read().IsClosed); Assert.False(documents.Read().Closed);
        Assert.True(documents.Execute(DocumentCommand.SaveAs, "saved.aetherscene")); DocumentUiFixture.Wait(documents.Pending);
        Assert.Null(documents.Read().Error); Assert.False(project.Documents.Read().IsDirty);
        Assert.True(File.Exists(Path.Combine(project.Project.ScenesDirectory, "saved.aetherscene")));
        Assert.True(Complete(editor.RequestCloseAsync()));
        DocumentUiFixture.Wait(app.StopAsync());
        Assert.True(project.IsClosed); Assert.True(project.Documents.Read().IsClosed); Assert.Null(projects.Active);
        Assert.Equal(0, runtime.Launches);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ProjectStartupFailure_RollsBackEngineAndProjectsAndPreservesOriginalError(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime(); var expected = new IOException("project read failure");
        var store = new EditorProjectsTests.Store(files.Resolved) { Read = (_, _) => throw expected };
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IProjectStore>(_ => store));
            context.UseAetherEditor(engine => Configure(engine, files, runtime), editor => editor.ProjectPath = files.Manifest);
        }).Build();
        Assert.Same(expected, await Record.ExceptionAsync(() => app.StartAsync()));
        Assert.True(app.Services.Resolve<EditorProjects>()!.IsClosed); Assert.Null(app.Services.Resolve<EditorProjects>()!.Active);
        Assert.True(app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed);
        Assert.True(app.Services.Resolve<AetherHost>()!.Editor.Status.Closed);
        Assert.Equal(0, runtime.Launches); await app.DisposeAsync(); Assert.Equal(1, runtime.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FactoryFailure_DisposesUnpublishedTreeAndCanRetryWithSameBorrowedModels(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime(); var style = new ProbeStyle { Fail = true };
        using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IViewStyle<EditorView>>(_ => style));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }).Build();
        var factory = app.Services.Resolve<IFactory<EditorView>>()!;
        Assert.Same(style.Error, Assert.Throws<IOException>(() => factory.Create()));
        Assert.True(style.Unpublished!.IsDisposed); Assert.True(style.Unpublished.Documents!.IsDisposed);
        Assert.True(style.Unpublished.Inspector!.IsDisposed); Assert.True(style.Unpublished.Hierarchy!.IsDisposed);
        style.Fail = false; using var view = factory.Create()!;
        Assert.Same(app.Services.Resolve<EditorViewModel>(), view.DataContext);
        using var ui = DocumentUiFixture.Ui(view); ui.Update(); app.Services.Resolve<EditorSession>()!.Present(ui, view);
        Assert.False(app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed); Assert.Equal(0, runtime.Launches);
    }

    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("game.csproj")]
    public void InvalidOptions_AreRejectedBeforeEngineConfiguration(string path)
    {
        var configured = false;
        Assert.Throws<ArgumentException>(() => App.Build(context => context.UseAetherEditor(_ => configured = true, options => options.ProjectPath = path)));
        Assert.False(configured);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DuplicateOrPreexistingEngine_RejectsAmbiguousOwnership(bool existingEngine)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime();
        Assert.Throws<InvalidOperationException>(() => App.Build(context =>
        {
            if (existingEngine) context.UseAether(engine => Configure(engine, files, runtime));
            else context.UseAetherEditor(engine => Configure(engine, files, runtime));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }));
        Assert.Equal(0, runtime.Launches); Assert.Equal(0, runtime.Disposals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CleanupFailure_RemainsObservableAndCanRetryBeforeAppDisposal(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime(); var expected = new IOException("preview cleanup failed"); var stops = 0;
        var preview = new EditorPreview((_, _) => throw new InvalidOperationException("No preview requested"),
            () => ++stops == 1 ? Task.FromException(expected) : Task.CompletedTask);
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<EditorPreview>(_ => preview));
            context.UseAetherEditor(engine => Configure(engine, files, runtime));
        }).Build();
        await app.StartAsync(); var editor = app.Services.Resolve<EditorSession>()!;
        Assert.Same(preview, app.Services.Resolve<EditorPreview>());
        Assert.Same(expected, await Record.ExceptionAsync(() => editor.StopAsync()));
        Assert.Same(expected, app.Services.Resolve<EditorDocumentCommands>()!.Read().Error);
        Assert.False(app.Services.Resolve<EditorProjects>()!.IsClosed);
        await editor.StopAsync(); Assert.Equal(2, stops);
        Assert.True(app.Services.Resolve<EditorDocumentCommands>()!.Read().Closed);
        await app.StopAsync(); Assert.True(app.Services.Resolve<EditorProjects>()!.IsClosed);
        Assert.Equal(0, runtime.Launches);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CustomViewport_DoesNotWaitForStandardWindowToStartPreview(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime();
        await using var app = Builder(microsoft).Setup(context => context.UseAetherEditor(engine =>
        { Configure(engine, files, runtime); engine.ViewportTransport = _ => new CustomTransport(); })).Build();
        await app.StartAsync();
        using var authoring = DocumentData.Factory().Create(DocumentData.Scene());
        var preview = app.Services.Resolve<EditorPreview>()!;
        Assert.True(preview.Show(authoring.Read().Resolved));
        await preview.Pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, runtime.Launches);
        Assert.Equal("Composition must not start a child.", preview.Read().Error!.Message);
        Assert.False(preview.Read().Busy);
    }

    [Fact]
    public void LibraryArtifact_HasNoEntryPointBootstrapDependencyOrExecutableProbeTypes()
    {
        var assembly = typeof(EditorView).Assembly;
        Assert.Null(assembly.EntryPoint);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name == "TrueMoon");
        Assert.DoesNotContain(assembly.GetTypes(), type => type.Name.EndsWith("Smoke", StringComparison.Ordinal) ||
            type.Name is "EditorApplication" or "EditorLaunchOptions" or "RuntimeEntryPoint" or "ProjectEntryPoint");
    }

    private static IAppBuilder Builder(bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ProjectPicker_RebindsActualBuildAndClearsContextOnCloseInBothProviders(bool microsoft)
    {
        using var first = new ProjectFixture(); using var next = new ProjectFixture(); var runtime = new NeverRuntime();
        var store = new EditorProjectsTests.Store(first.Resolved);
        using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IProjectStore>(_ => store).Singleton<IProjectToolchain>(_ => new EditorProjectsTests.Toolchain()));
            context.UseAetherEditor(engine => Configure(engine, first, runtime));
        }).Build();
        DocumentUiFixture.Wait(app.StartAsync());
        var documents = app.Services.Resolve<EditorDocumentCommands>()!; var projects = app.Services.Resolve<EditorProjects>()!;
        var engine = app.Services.Resolve<AetherHost>()!; var session = app.Services.Resolve<EditorSession>()!;
        var view = app.Services.Resolve<IFactory<EditorView>>()!.Create()!; using var ui = DocumentUiFixture.Ui(view);
        ui.Update(); session.Present(ui, view); Assert.False(view.DataContext!.CanBuild);
        Assert.True(documents.Execute(DocumentCommand.OpenProject, first.Manifest)); DocumentUiFixture.Wait(documents.Pending);
        Assert.Null(documents.Read().Error); Assert.True(documents.Read().ProjectReady);
        Assert.Equal(first.Resolved.GameProjectPath, engine.Editor.Status.Context!.Build.ProjectPath);
        session.Present(ui, view); ui.Update(); Assert.True(view.DataContext.CanBuild);
        var old = projects.Active!; var version = engine.Editor.Status.ContextVersion;
        store.Project = next.Resolved;
        Assert.True(documents.Execute(DocumentCommand.OpenProject, next.Manifest)); DocumentUiFixture.Wait(documents.Pending);
        Assert.True(old.IsClosed); Assert.Equal(next.Resolved.GameProjectPath, engine.Editor.Status.Context!.Build.ProjectPath);
        Assert.Equal(version + 1, engine.Editor.Status.ContextVersion); Assert.Null(engine.Editor.Status.Candidate);
        session.Present(ui, view); ui.Update(); Assert.Same(projects.Active, app.Services.Resolve<EditorWorkspace>()!.Project);
        Assert.True(documents.Execute(DocumentCommand.CloseProject)); DocumentUiFixture.Wait(documents.Pending);
        Assert.Null(engine.Editor.Status.Context); Assert.Null(projects.Active);
        session.Present(ui, view); ui.Update(); Assert.False(view.DataContext.HasProject); Assert.False(view.DataContext.CanBuild);
        Assert.Null(view.DataContext.SelectedId); Assert.Equal(0, runtime.Launches);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ScenePlayback_DefaultCompositionEnablesRunForUnsavedDocumentWithoutCandidate(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new NeverRuntime();
        using var app = Builder(microsoft).Setup(context =>
        {
            context.Services(s => s.Singleton<IProjectStore>(_ => new EditorProjectsTests.Store(files.Resolved))
                .Singleton<IProjectToolchain>(_ => new EditorProjectsTests.Toolchain()));
            context.UseAetherEditor(engine => Configure(engine, files, runtime), editor => editor.ProjectPath = files.Manifest);
        }).Build();
        DocumentUiFixture.Wait(app.StartAsync());
        var project = app.Services.Resolve<EditorProjects>()!.Active!;
        DocumentUiFixture.Wait(project.Documents!.NewAsync(DocumentData.Scene()));
        var playback = app.Services.Resolve<EditorScenePlayback>()!;
        Assert.Same(playback, app.Services.Resolve<EditorScenePlayback>());
        var model = app.Services.Resolve<EditorViewModel>()!; var engine = app.Services.Resolve<AetherHost>()!;
        model.Refresh(engine.Supervisor.Status, engine.Editor.Status);
        Assert.True(model.CanRun); Assert.True(model.CanBuild);
        Assert.Null(app.Services.Resolve<AetherHost>()!.Editor.Status.Candidate);
        Assert.True(project.Documents.Read().IsDirty); Assert.Null(project.Documents.Read().Path); Assert.Equal(0, runtime.Launches);
    }

    // Keep all UI access on the test's owner thread, as the neighboring UI fixtures do.
    private static T Complete<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    private static void Configure(AetherOptions options, ProjectFixture files, NeverRuntime runtime)
    {
        options.HostContractId = new('a', 64); options.GameBuildId = new('0', 64);
        options.ReportDirectory = Path.Combine(files.Root, "reports"); options.RuntimeFactory = _ => runtime;
    }
    private sealed class NeverRuntime : IRuntimeFactory, IDisposable
    {
        public int Launches, Disposals;
        public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("Composition must not start a child."); }
        public void Dispose() => Disposals++;
    }
    private sealed class CustomTransport : IGameViewportTransport
    {
        public ViewportTransportKind Kind => ViewportTransportKind.Gpu;
        public Task<IGameViewportConnection> ConnectAsync(IRuntimeLease runtime, CancellationToken token)
            => throw new InvalidOperationException("The runtime must fail before viewport connection.");
    }
    private sealed class ProbeStyle : IViewStyle<EditorView>
    {
        public int Configurations; public bool Fail; public EditorView? Unpublished;
        public IOException Error { get; } = new("view style failure");
        public void Configure(IViewStyleBuilder<EditorView> styles)
        {
            Configurations++;
            if (Fail) { styles.Part(view => { Unpublished = view; return view.Error; }).FontSize(13); throw Error; }
            new EditorViewStyle().Configure(styles);
        }
    }
}
