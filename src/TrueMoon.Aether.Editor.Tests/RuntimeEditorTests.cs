using System.Reflection;
using System.Reflection.Emit;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Ecs.Loading;
using TrueMoon.Aether.Editor.UI;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests;

public sealed class RuntimeEditorTests
{
    [Fact]
    public async Task TerminalStopFromIdle_NotifiesDisabledCapabilities_WithoutStartingRuntime()
    {
        using var files = new Inputs(); var runtime = new NeverRuntime();
        await using var app = App.Build(context => context.UseAetherEditorUi(files.Start, options => files.Configure(options, runtime)));
        var model = app.Services.Resolve<RuntimeEditorViewModel>()!; var session = app.Services.Resolve<RuntimeEditorSession>()!;
        var changed = new List<string?>(); model.PropertyChanged += (_, change) => changed.Add(change.PropertyName);
        Assert.True(model.CanStart); await session.StopAsync(); model.Refresh();
        Assert.False(model.CanStart); Assert.Contains(nameof(model.CanStart), changed); Assert.Contains(nameof(model.CanRun), changed);
        Assert.False(model.CanRun); Assert.False(session.Execute(EditorCommand.Start)); Assert.Equal(0, runtime.Launches);
    }
    [Fact]
    public async Task CapturedWorldInspection_FormatsTypedValues_WithoutMutatingOrRebuildingPresentation()
    {
        using var files = new Inputs();
        var schema = new ComponentSchema(new(Guid.NewGuid()), 1,
        [new("count", PropertyValueKind.Integer, PropertyValue.From(42L)), new("position", PropertyValueKind.Number, PropertyValue.From(1.25)),
            new("name", PropertyValueKind.String, PropertyValue.From("seed")), new("enabled", PropertyValueKind.Boolean, PropertyValue.From(true))]);
        var definition = new EntityDefinition(new(Guid.NewGuid()), new(addedComponents: [new(new(Guid.NewGuid()), schema.TypeId, 1)]));
        var entity = new EntityId(Guid.NewGuid()); var document = new SceneDocument(new(Guid.NewGuid()), 7, [new(entity, definition.Id)], [definition], [schema]);
        var start = PlayerStart.FromWorld(files.Start.Artifact, new SceneWorldData(document));
        await using var app = App.Build(context => context.UseAetherEditorUi(start, options => files.Configure(options, new NeverRuntime())));
        var model = app.Services.Resolve<RuntimeEditorViewModel>()!; model.Select(entity.Value.ToString("D"));
        var values = model.Properties; var rows = values.ToDictionary(row => row.Key, row => row.Value);
        Assert.Equal("42", rows["count"]); Assert.Equal("1.25", rows["position"]); Assert.Equal("\"seed\"", rows["name"]); Assert.Equal("true", rows["enabled"]);
        var notifications = 0; model.PropertyChanged += (_, _) => notifications++;
        model.Refresh(); model.Refresh();
        Assert.Equal(0, notifications); Assert.Same(values, model.Properties); Assert.Same(document, start.World!.Scene); Assert.Equal(7, document.Revision);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task RuntimeUi_RegistersLazyGraphAndReadonlyInspection_WithoutTooling(bool microsoft)
    {
        using var files = new Inputs(); var runtime = new NeverRuntime();
        var app = Builder(microsoft).Setup(context => context.UseAetherEditorUi(files.Start, options => files.Configure(options, runtime))
            .UseAetherEditorUiWindow(SkiaFontCatalog.SystemDefault)).Build();
        try
        {
            var session = app.Services.Resolve<RuntimeEditorSession>()!;
            var model = app.Services.Resolve<RuntimeEditorViewModel>()!;
            var window = app.Services.Resolve<RuntimeEditorWindow>()!;
            Assert.False(window.Ready.IsCompleted); Assert.Equal(SessionState.Stopped, session.Status.State);
            Assert.Empty(app.Services.ResolveAll<AetherHost>()); Assert.Empty(app.Services.ResolveAll<ToolingHost>());
            Assert.Empty(app.Services.ResolveAll<EditorProjects>()); Assert.Empty(app.Services.ResolveAll<GameBuildCompiler>());
            var factory = app.Services.Resolve<IFactory<EditorView>>()!;
            using (var first = factory.Create()!)
            using (var second = factory.Create()!)
            {
                Assert.NotSame(first, second); Assert.NotSame(first.Canvas, second.Canvas); Assert.Same(model, first.DataContext);
                Assert.Null(first.Documents); Assert.Null(first.Diagnostics); Assert.Null(first.Inspector);
                Assert.False(model.CanBuild); Assert.False(model.CanReplace); Assert.False(model.HasSourceContext);
                Assert.False(session.Execute(EditorCommand.Build)); Assert.False(session.Execute(EditorCommand.Replace));
                Assert.Single(model.EntityIds); model.Select("seed");
                Assert.True(model.IsInspectorVisible); Assert.Equal("7", model.Properties.Single(row => row.Key == "X").Value);
                Assert.Throws<ArgumentException>(() => model.Select("foreign")); Assert.Equal("seed", model.SelectedId);
                model.ClearSelection(); Assert.Null(model.SelectedId); Assert.Empty(model.Properties);
            }
            Assert.True(model.CanStart); Assert.Equal(0, runtime.Launches); Assert.False(Directory.Exists(files.Reports));
            Assert.Single(app.Services.ResolveAll<HostedUiWindow>());
        }
        finally { await app.DisposeAsync(); await app.DisposeAsync(); }
        Assert.Equal(1, runtime.Disposals);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PendingViewportStartup_CancelsAndDrains_WithoutLaunchingOrLateAdmission(bool microsoft)
    {
        using var files = new Inputs(); var runtime = new NeverRuntime();
        await using var app = Builder(microsoft).Setup(context => context.UseAetherEditorUi(files.Start, options => files.Configure(options, runtime))).Build();
        var session = app.Services.Resolve<RuntimeEditorSession>()!;
        await app.StartAsync();
        Assert.True(session.Execute(EditorCommand.Start)); Assert.False(session.Pending.IsCompleted);
        Assert.False(session.Execute(EditorCommand.Start)); Assert.True(session.Execute(EditorCommand.Cancel));
        await session.Pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(session.Error); Assert.Equal(0, runtime.Launches); Assert.True(session.CanExecute(EditorCommand.Start));
        Assert.True(session.Execute(EditorCommand.Start)); await session.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(session.Pending.IsCompleted); Assert.False(session.Execute(EditorCommand.Start)); Assert.Equal(0, runtime.Launches);
        Assert.False(Directory.Exists(files.Reports));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ConfiguredRuntimeUi_UsesExactCommonSchemasAndRejectsIdentityMismatch(bool microsoft)
    {
        using var files = new Inputs(); var runtime = new NeverRuntime();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(RuntimeConfigurationSchemas.Engine); builder.AddSchema(EditorUiConfigurationSchemas.Window);
            builder.Override("Aether:ReportDirectory", files.Reports); builder.Override("Editor:RuntimeWindow:Width", 1400);
        });
        await using var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEditorUi(files.Start, new RuntimeComposition { HostContractId = Inputs.Hash, GameBuildId = files.Start.Artifact.BuildId, RuntimeFactory = _ => runtime })
            .UseConfiguredAetherEditorUiWindow(SkiaFontCatalog.SystemDefault)).Build();
        Assert.All(EditorUiConfigurationSchemas.Window.Keys, key => Assert.StartsWith("Editor:RuntimeWindow:", key.Path));
        Assert.Equal(1400, EditorUiConfigurationSchemas.Window.Bind(await profile.LoadAsync()).Width);
        Assert.Equal(SessionState.Stopped, app.Services.Resolve<RuntimeEditorSession>()!.Status.State);
        Assert.Throws<ArgumentException>(() => Builder(microsoft).Setup(context => context.UseAetherEditorUi(files.Start, options =>
        { files.Configure(options, new NeverRuntime()); options.GameBuildId = new('B', 64); })).Build());
        Assert.Throws<InvalidOperationException>(() => Builder(microsoft).Setup(context =>
        {
            context.UseAetherEditorUi(files.Start, options => files.Configure(options, new NeverRuntime()));
            context.UseAetherEditorUi(files.Start, options => files.Configure(options, new NeverRuntime()));
        }).Build());
        Assert.Equal(0, runtime.Launches);
    }

    [Fact]
    public async Task RuntimeInspection_RejectsForeignPresentationThread_AndRetainsSelection()
    {
        using var files = new Inputs();
        var app = App.Build(context => context.UseAetherEditorUi(files.Start, options => files.Configure(options, new NeverRuntime())));
        try
        {
            var model = app.Services.Resolve<RuntimeEditorViewModel>()!; model.Select("seed");
            Exception? failure = null;
            var thread = new Thread(() => failure = Record.Exception(model.ClearSelection)); thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5))); Assert.IsType<InvalidOperationException>(failure);
            Assert.Equal("seed", model.SelectedId); Assert.True(model.IsInspectorVisible);
        }
        finally { await app.DisposeAsync(); }
    }

    private static IAppBuilder Builder(bool microsoft) => App.Builder(builder => { if (microsoft) builder.UseDI(); });
    private sealed class NeverRuntime : IRuntimeFactory, IDisposable
    {
        internal int Launches, Disposals;
        public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("Unexpected runtime activation."); }
        public void Dispose() => Disposals++;
    }
    private sealed class Inputs : IDisposable
    {
        internal const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "TrueMoon-ARCH35-tests-" + Guid.NewGuid().ToString("N"));
        internal string Reports => Path.Combine(_root, "reports");
        internal PlayerStart Start { get; }
        internal Inputs()
        {
            var output = Path.Combine(_root, "output"); Directory.CreateDirectory(output);
            var assembly = new PersistedAssemblyBuilder(new AssemblyName("RuntimeUi.Logic"), typeof(object).Assembly);
            assembly.DefineDynamicModule("RuntimeUi.Logic").DefineType("RuntimeUi.Game", TypeAttributes.Public).CreateType();
            assembly.Save(Path.Combine(output, "RuntimeUi.Logic.dll"));
            var artifact = GameArtifactPublisher.Publish(output, Path.Combine(_root, "artifacts"), Hash, "RuntimeUi.Logic.dll", "RuntimeUi.Game", 1);
            Start = PlayerStart.FromSnapshot(artifact, new("captured", 1, [new() { Id = "seed", X = 7 }]));
        }
        internal void Configure(RuntimeOptions options, NeverRuntime runtime)
        { options.HostContractId = Hash; options.GameBuildId = Start.Artifact.BuildId; options.ReportDirectory = Reports; options.RuntimeFactory = _ => runtime; }
        public void Dispose()
        {
            var root = Path.GetFullPath(_root); var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cleanup escaped temporary directory.");
            Directory.Delete(root, true);
        }
    }
}
