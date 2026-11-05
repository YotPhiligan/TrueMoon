using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Inspector;
using TrueMoon.Aether.Editor.Tests.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Argentis;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class EditorWindowCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_StandardFactoryBorrowsInjectedStyleAndViewsAndDoesNotCreateGpuOrOwnProducts(bool microsoft)
    {
        using var files = new ProjectFixture(); using var h = new EditorMainWindowFixture(); var runtime = new EditorWindowNeverRuntime();
        using var app = EditorWindowTestGraph.Build(files, runtime, microsoft, context =>
        {
            context.Services(services => services.Singleton<EditorViewModel>(_ => h.Model)
                .Singleton<EditorDocumentViewModel>(_ => h.Documents).Singleton<IFactory<EditorView>>(_ => h.Views)
                .Singleton<IViewStyle<EditorMainWindow>>(_ => h.RootStyle));
        }, context => context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts));
        Assert.Equal(0, h.ViewCreations); Assert.Equal(0, h.RootStyle.Configurations); Assert.Equal(0, runtime.Launches);
        var factory = app.Services.Resolve<IFactory<EditorMainWindow, EditorMainWindowArguments>>()!;
        Assert.Same(factory, app.Services.Resolve<IFactory<EditorMainWindow, EditorMainWindowArguments>>());
        Assert.Same(h.RootStyle, app.Services.Resolve<IViewStyle<EditorMainWindow>>());
        var first = factory.Create(new("First", new(1280, 800)))!;
        var second = factory.Create(new("Second", new(1500, 900)))!;
        Assert.Same(h.Model, first.DataContext); Assert.Same(h.Model, second.EditorView.DataContext);
        Assert.NotSame(first.EditorView, second.EditorView); Assert.Equal(2, h.ViewCreations); Assert.Equal(2, h.RootStyle.Configurations);
        var engine = app.Services.Resolve<AetherHost>()!;
        Assert.Equal(0, engine.Gpu.ResourceCount); Assert.Equal(0, engine.Gpu.OpenHandleCount); Assert.Equal(0, runtime.Launches);
        Assert.False(Directory.Exists(Path.Combine(files.Root, "reports")));
        first.Dispose(); Assert.False(second.IsDisposed); Assert.Equal(0, h.RootStyle.Disposals);
        app.Dispose(); Assert.False(second.IsDisposed); Assert.Equal(1, h.RootStyle.Disposals);
        second.Dispose(); Assert.True(second.EditorView.IsDisposed); Assert.Equal(1, h.RootStyle.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothProviders_EmbeddedCompositionRetainsRawFactoryAndRegistersNoStandaloneRoot(bool microsoft)
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime();
        using var app = EditorWindowTestGraph.Build(files, runtime, microsoft, after: context => context.Services(services =>
        {
            Assert.False(services.Exist<EditorWindow>());
            Assert.False(services.Exist<IViewStyle<EditorMainWindow>>());
            Assert.False(services.Exist<IFactory<EditorMainWindow, EditorMainWindowArguments>>());
        }));
        var view = app.Services.Resolve<IFactory<EditorView>>()!.Create()!;
        Assert.False((object)view is Window); Assert.Null(view.Parent); Assert.False(view.IsAttached);
        using var ui = InspectorFixture.Ui(view); ui.Update();
        app.Services.Resolve<EditorSession>()!.Present(ui, view);
        Assert.Same(view, ui.Root); Assert.Same(app.Services.Resolve<EditorViewModel>(), view.DataContext);
        Assert.False(view.IsDisposed); Assert.Equal(0, runtime.Launches); Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.ResourceCount);
        ui.Dispose(); Assert.True(view.IsDisposed);
    }

    [Theory]
    [InlineData(false, false, "TrueMoon Aether Editor", 1280, 800)]
    [InlineData(true, false, "TrueMoon Aether Editor", 1280, 800)]
    [InlineData(false, true, "Custom caption", 1500, 900)]
    [InlineData(true, true, "", 1100, 700)]
    public void StandardRootCallback_UsesCopiedEffectiveSettingsAndPreservesCustomFactoryBeforeNativeAllocation(
        bool microsoft, bool custom, string expectedTitle, int width, int height)
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime(); var calls = 0;
        EditorMainWindowArguments? seen = null; SilkUiWindowOptions configured = null!;
        IFactory<EditorMainWindow, EditorMainWindowArguments> factory = new Factory<EditorMainWindow, EditorMainWindowArguments>(arguments =>
        { calls++; seen = arguments; return null!; });
        var app = EditorWindowTestGraph.Build(files, runtime, microsoft,
            context => context.Services(services => services.Singleton<IFactory<EditorMainWindow, EditorMainWindowArguments>>(_ => factory)),
            context => context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts, options =>
            {
                configured = options;
                if (custom) { options.Title = expectedTitle; options.Width = width; options.Height = height; }
            }));
        Exception? startup = null;
        try
        {
            Assert.Same(factory, app.Services.Resolve<IFactory<EditorMainWindow, EditorMainWindowArguments>>()); Assert.Equal(0, calls);
            configured.Title = "changed after setup"; configured.Width = 1; configured.Height = 2;
            var hosted = app.Services.Resolve<HostedUiWindow>()!;
            startup = Record.Exception(() => DocumentUiFixture.Wait(hosted.StartAsync()));
            Assert.IsType<InvalidOperationException>(startup); Assert.Contains("window factory returned null", startup.Message);
            Assert.Same(startup, Record.Exception(() => DocumentUiFixture.Wait(hosted.Completion)));
            Assert.Equal(1, calls); Assert.NotNull(seen); Assert.Equal(expectedTitle, seen.Title); Assert.Equal(new Size(width, height), seen.ClientSize);
            Assert.False(hosted.IsClosed); Assert.Null(hosted.AppearanceCapabilities);
            Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.ResourceCount); Assert.Equal(0, runtime.Launches);
            Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.OpenHandleCount);
        }
        finally { EditorWindowTestGraph.DisposeAfterRejectedStartup(app, startup); }
    }

    [Fact]
    public void DefaultHeaderCallback_ChainingReceivesOriginalEditorAndUsesBaseTitleExactlyOnce()
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime();
        SilkUiWindowOptions configured = null!; Element? callbackContent = null; var callbackCalls = 0;
        var rootFactoryCalls = 0;
        var app = EditorWindowTestGraph.Build(files, runtime, false, context => context.Services(services => services
            .Singleton<IFactory<EditorMainWindow, EditorMainWindowArguments>>(_ => new Factory<EditorMainWindow, EditorMainWindowArguments>(_ => { rootFactoryCalls++; return null!; }))),
            context => context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts, options =>
            {
                options.Title = "Custom Editor"; configured = options;
                var original = options.ConfigureDefaultTitleBar!;
                options.ConfigureDefaultTitleBar = (title, content, close) => { callbackCalls++; callbackContent = content; original(title, content, close); };
            }));
        Exception? startup = null;
        try
        {
            var hosted = app.Services.Resolve<HostedUiWindow>()!;
            startup = Record.Exception(() => DocumentUiFixture.Wait(hosted.StartAsync())); Assert.IsType<InvalidOperationException>(startup);
            var projects = app.Services.Resolve<EditorProjects>()!; DocumentUiFixture.Wait(projects.OpenAsync(files.Manifest));
            DocumentUiFixture.Wait(projects.Active!.Documents!.NewAsync(DocumentData.Scene()));
            var documents = app.Services.Resolve<EditorDocumentViewModel>()!; documents.Refresh();
            var root = new EditorMainWindow(app.Services.Resolve<EditorViewModel>()!, documents, app.Services.Resolve<IFactory<EditorView>>()!,
                app.Services.Resolve<IViewStyle<EditorMainWindow>>()!, new("Custom Editor", new(1280, 800)));
            var close = 0; var frame = WindowFrame.CreateDefault(root.Title, root);
            configured.ConfigureDefaultTitleBar!(frame.DefaultTitleBar!, root, () => close++);
            using var ui = InspectorFixture.Ui(frame); ui.Update();
            var expected = "Custom Editor · " + files.Resolved.Description.Name + " · Новая сцена *";
            Assert.Equal(expected, root.Title); Assert.Equal(expected, root.EditorView.Header!.Caption.Value);
            Assert.Equal(root.AccessibleName, root.EditorView.Header.Caption.AccessibleName);
            Assert.Same(root.EditorView, callbackContent); Assert.Equal(1, callbackCalls); Assert.Equal(1, rootFactoryCalls);
            Assert.Same(root.DataContext, root.EditorView.Header.DataContext);
            Assert.Same(root, frame.Content); Assert.Single(DocumentUiFixture.Tree(frame).OfType<WindowFrame>());
            root.EditorView.Header.Menu.OpenGroup("file", ui); ui.Update();
            Assert.NotNull(root.EditorView.Overlays.Popup); Assert.Same(root.EditorView.Header.Menu, root.EditorView.Overlays.PopupOwner);
            root.EditorView.Header.Menu.Groups.Single(group => group.Id == "file").Actions.Single(action => action.Id == "file.exit").Command.Execute();
            Assert.Equal(1, close); Assert.False(app.Services.Resolve<EditorDocumentCommands>()!.Read().Exiting);
            Assert.Equal(0, runtime.Launches); Assert.Equal(0, app.Services.Resolve<AetherHost>()!.Gpu.ResourceCount);
            ui.Dispose();
        }
        finally { EditorWindowTestGraph.DisposeAfterRejectedStartup(app, startup); }
    }

    [Theory]
    [InlineData(1099, 800)]
    [InlineData(1098, 800)]
    [InlineData(1280, 699)]
    [InlineData(1280, 698)]
    public void StandardChromeMinimum_RejectsInvalidAdjacentAndLaterBlockedSizesBeforeAnyRoot(int width, int height)
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime(); var roots = 0;
        var error = Record.Exception(() => EditorWindowTestGraph.Build(files, runtime, false,
            context => context.Services(services => services.Singleton<IFactory<EditorMainWindow, EditorMainWindowArguments>>(_ =>
                new Factory<EditorMainWindow, EditorMainWindowArguments>(_ => { roots++; return null!; }))),
            context => context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts, window => { window.Width = width; window.Height = height; })));
        Assert.IsType<ArgumentOutOfRangeException>(error); Assert.Equal(0, roots); Assert.Equal(0, runtime.Launches);
        Assert.False(Directory.Exists(Path.Combine(files.Root, "reports")));
    }

    [Theory]
    [InlineData("missing-editor")]
    [InlineData("duplicate")]
    [InlineData("configure-error")]
    public void RegistrationFailures_DoNotCreateWindowOrRuntime(string kind)
    {
        using var files = new ProjectFixture(); var runtime = new EditorWindowNeverRuntime(); var configured = 0;
        var failure = new IOException("configure");
        var error = Record.Exception(() =>
        {
            if (kind == "missing-editor") App.Builder(_ => { }).Setup(context => context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts)).Build();
            else EditorWindowTestGraph.Build(files, runtime, false, after: context =>
            {
                context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts, _ => { configured++; if (kind == "configure-error") throw failure; });
                context.UseAetherEditorWindow(EditorWindowTestGraph.Fonts);
            });
        });
        if (kind == "configure-error") Assert.Same(failure, error); else Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(kind == "missing-editor" ? 0 : 1, configured); Assert.Equal(0, runtime.Launches);
        Assert.False(Directory.Exists(Path.Combine(files.Root, "reports")));
        Assert.Throws<ArgumentNullException>(() => EditorWindowConfigurationExtensions.UseAetherEditorWindow(null!, EditorWindowTestGraph.Fonts));
        Assert.Throws<ArgumentNullException>(() => App.Builder(_ => { }).Setup(context => context.UseAetherEditorWindow(null!)).Build());
    }
}

internal static class EditorWindowTestGraph
{
    private static readonly Lazy<SkiaFontCatalog> FontCatalog = new(() => SkiaFontCatalog.Load("EditorWindowTests",
        new Dictionary<string, string> { ["EditorWindowTests"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") }));
    internal static SkiaFontCatalog Fonts => FontCatalog.Value;
    internal static T Complete<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    internal static IApp Build(ProjectFixture files, EditorWindowNeverRuntime runtime, bool microsoft = false,
        Action<IAppConfigurationContext>? before = null, Action<IAppConfigurationContext>? after = null) =>
        App.Builder(configuration => { if (microsoft) configuration.UseDI(); }).Setup(context =>
        {
            context.Services(services => services.Singleton<IProjectStore>(_ => new TrueMoon.Aether.Editor.Tests.Projects.EditorProjectsTests.Store(files.Resolved))
                .Singleton<IProjectToolchain>(_ => new TrueMoon.Aether.Editor.Tests.Projects.EditorProjectsTests.Toolchain())
                .Singleton<ISceneDocumentStore>(_ => new DocumentStore()));
            before?.Invoke(context);
            context.UseAetherEditor(options =>
            {
                options.HostContractId = new('a', 64); options.GameBuildId = new('0', 64);
                options.ReportDirectory = Path.Combine(files.Root, "reports"); options.RuntimeFactory = _ => runtime;
            });
            after?.Invoke(context);
        }).Build();
    internal static void DisposeAfterRejectedStartup(IApp app, Exception? startup)
    {
        var cleanup = Record.Exception(app.Dispose);
        if (cleanup == null) return;
        Assert.NotNull(startup);
        var errors = cleanup is AggregateException aggregate ? aggregate.Flatten().InnerExceptions.ToArray() : [cleanup];
        Assert.All(errors, error => Assert.Same(startup, error));
    }
}
internal sealed class EditorWindowNeverRuntime : IRuntimeFactory, IDisposable
{
    internal int Launches, Disposals;
    public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("No child is expected in window composition tests."); }
    public void Dispose() => Disposals++;
}
