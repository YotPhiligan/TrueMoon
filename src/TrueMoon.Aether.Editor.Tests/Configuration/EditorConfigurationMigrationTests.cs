using System.Text.Json;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Runtime;
using TrueMoon.ManualTests.AetherEditorHost;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Configuration;

public sealed class EditorConfigurationMigrationTests
{
    [Fact]
    public async Task EditorSchemasBindSourceTrackingPresentationAndSourceCommandWithoutIo()
    {
        var project = Path.Combine(Path.GetTempPath(), "core75-not-created-" + Guid.NewGuid().ToString("N"), "game.aetherproj");
        var profile = Profile(builder =>
        {
            builder.AddValues("editor-values", ConfigurationSourcePriority.Arguments,
            [
                Entry(Key(EditorConfigurationSchemas.Options, "ProjectPath"), project),
                Entry(Key(EditorConfigurationSchemas.Options, "SourceTracking:Enabled"), false),
                Entry(Key(EditorConfigurationSchemas.Options, "SourceTracking:Debounce"), "00:00:00.050"),
                Entry(Key(EditorConfigurationSchemas.Options, "SourceTracking:MaximumWatchers"), 0),
                Entry(Key(EditorConfigurationSchemas.Diagnostics, "MaximumRecords"), 17)
            ]);
        });
        var snapshot = await profile.LoadAsync();
        var editor = EditorConfigurationSchemas.Options.Bind(snapshot);
        var diagnostics = EditorConfigurationSchemas.Diagnostics.Bind(snapshot);
        var source = EditorConfigurationSchemas.SourceEditor.Bind(snapshot);

        Assert.Equal(Path.GetFullPath(project), editor.ProjectPath);
        Assert.False(editor.SourceTracking.Enabled);
        Assert.Equal(TimeSpan.FromMilliseconds(50), editor.SourceTracking.Debounce);
        Assert.Equal(0, editor.SourceTracking.MaximumWatchers);
        Assert.Equal(17, diagnostics.MaximumRecords);
        Assert.Equal(SourceExecutable, source.ExecutablePath);
        Assert.Equal(new[] { "--file", "{file}:{line}:{column}" }, source.Arguments);
        Assert.False(Directory.Exists(Path.GetDirectoryName(project)));

        editor.SourceTracking.MaximumWatchers = 32;
        diagnostics.MaximumRecords = 900;
        source.Arguments = ["mutated-{file}"];
        Assert.Equal(0, EditorConfigurationSchemas.Options.Bind(snapshot).SourceTracking.MaximumWatchers);
        Assert.Equal(17, EditorConfigurationSchemas.Diagnostics.Bind(snapshot).MaximumRecords);
        Assert.Equal(new[] { "--file", "{file}:{line}:{column}" }, EditorConfigurationSchemas.SourceEditor.Bind(snapshot).Arguments);
    }

    [Fact]
    public async Task EditorDefaultDataIsCopiedWithoutPromotingUiStateToConfiguration()
    {
        var snapshot = await Profile().LoadAsync();
        var editor = EditorConfigurationSchemas.Options.Bind(snapshot);
        var diagnostics = EditorConfigurationSchemas.Diagnostics.Bind(snapshot);
        Assert.Null(editor.ProjectPath);
        Assert.True(editor.SourceTracking.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(10), editor.SourceTracking.PollInterval);
        Assert.Equal((512, 64, 16), (diagnostics.MaximumRecords, diagnostics.RecordsPerRefresh, diagnostics.MaximumProcesses));
        var keys = EditorConfigurationSchemas.Options.Keys.Concat(EditorConfigurationSchemas.Diagnostics.Keys).Concat(EditorConfigurationSchemas.SourceEditor.Keys).ToArray();
        Assert.All(keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        Assert.DoesNotContain(keys, key => key.Path.Contains("Selection", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("ViewModel", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("ViewportIdentity", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("Factory", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false, "options", "ProjectPath", "\"invalid.txt\"")]
    [InlineData(true, "options", "SourceTracking:MaximumWatchers", "33")]
    [InlineData(false, "options", "SourceTracking:PollInterval", "\"00:00:00\"")]
    [InlineData(true, "diagnostics", "MaximumRecords", "0")]
    [InlineData(false, "source", "Arguments", "[\"no-placeholder\"]")]
    [InlineData(true, "source", "ExecutablePath", "\"relative-editor\"")]
    public void InvalidEditorWinnerFailsBeforeAnyRegistration(bool microsoft, string schema, string field, string json)
    {
        var keys = schema switch
        {
            "options" => EditorConfigurationSchemas.Options.Keys,
            "diagnostics" => EditorConfigurationSchemas.Diagnostics.Keys,
            "source" => EditorConfigurationSchemas.SourceEditor.Keys,
            _ => throw new ArgumentException("Unknown test schema.")
        };
        var key = keys.Single(key => key.Path.EndsWith(":" + field, StringComparison.Ordinal)).Path;
        using var document = JsonDocument.Parse(json);
        var registrations = 0;
        var profile = Profile(builder => builder.AddValues("invalid-editor", ConfigurationSourcePriority.Arguments,
            [new ConfigurationEntry(key, document.RootElement)]));
        var error = Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(EditorConfigurationSchemas.Options, (_, _) => registrations++);
        }).Build());
        Assert.NotEmpty(error.Issues);
        Assert.Equal(0, registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredEditorPreservesEmptyProjectCaptureDefaultViewportAndOwnedRuntimeFactory(bool microsoft)
    {
        var runtime = new NeverRuntime();
        var activations = 0;
        AetherEditorOptions? configuredEditor = null;
        var report = Path.Combine(Path.GetTempPath(), "core75-editor-" + Guid.NewGuid().ToString("N"));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(AetherConfigurationSchemas.Engine);
            builder.AddSchema(AetherConfigurationSchemas.Authoring);
            builder.AddSchema(EditorConfigurationSchemas.Diagnostics);
            builder.AddSchema(EditorConfigurationSchemas.Options);
            builder.Override("Aether:ReportDirectory", report);
            builder.Override("Editor:SourceTracking:Enabled", false);
            builder.Override("Editor:Diagnostics:RefreshInterval", TimeSpan.FromMilliseconds(40));
        });
        var composition = new AetherComposition
        {
            HostContractId = new('a', 64), GameBuildId = new('b', 64),
            RuntimeFactory = _ => { activations++; return runtime; }
        };
        var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEditor(composition, value => configuredEditor = value)).Build();
        try
        {
            Assert.Equal(0, activations);
            Assert.False(Directory.Exists(report));
            configuredEditor!.ProjectPath = Path.Combine(report, "mutated-after-build.aetherproj");
            configuredEditor.SourceTracking.Enabled = true;
            Assert.Null(Required<IConfigurationReader>(app).Current.GetOptions(EditorConfigurationSchemas.Options).ProjectPath);
            Assert.False(Required<IConfigurationReader>(app).Current.GetOptions(EditorConfigurationSchemas.Options).SourceTracking.Enabled);
            Assert.Same(Required<EditorViewportBinding>(app), Required<IFactory<IGameViewportTransport, GpuResourceHost>>(app));
            Assert.False(Required<EditorViewportBinding>(app).Ready.IsCompleted);
            Assert.Equal(TimeSpan.FromMilliseconds(40), Required<EditorDiagnosticsViewModel>(app).RefreshInterval);
            await app.StartAsync();
            Assert.Equal(1, activations);
            Assert.Equal(0, runtime.Launches);
            Assert.Null(Required<EditorProjects>(app).Active);
            Assert.False(Required<EditorWorkspace>(app).HasProject);
            Assert.Equal(SessionState.Stopped, Required<AetherHost>(app).Supervisor.Status.State);
            Assert.Equal(0, Required<AetherHost>(app).Gpu.ResourceCount);
            await app.StopAsync();
            Assert.True(Required<EditorProjects>(app).IsClosed);
            Assert.Equal(0, runtime.Disposals);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, runtime.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredPresentationBudgetsAndSourceCommandReachExistingConsumers(bool microsoft)
    {
        var profile = Profile(builder =>
        {
            builder.Override("Editor:Diagnostics:MaximumRecords", 2);
            builder.Override("Editor:Diagnostics:RefreshInterval", TimeSpan.FromMilliseconds(30));
        });
        using var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEditorDiagnostics().UseConfiguredAetherEditorSourceNavigation()
            .UseDiagnostics(new DiagnosticPipelineOptions { MemoryCapacity = 32 })).Build();
        var model = Required<EditorDiagnosticsViewModel>(app);
        Assert.Equal(TimeSpan.FromMilliseconds(30), model.RefreshInterval);
        Assert.True(model.CollectorAvailable);
        model.IsOpen = true;
        var source = Required<IEventsSourceFactory>(app).Create("TrueMoon.Core75.EditorConfig");
        for (var index = 0; index < 3; index++) source.Write(DiagnosticLevel.Warning, index, "Core75.Configured", "test");
        model.Refresh();
        Assert.Equal(2, model.RetainedCount);
        var snapshotCopy = Required<IConfigurationReader>(app).Current.GetOptions(EditorConfigurationSchemas.Diagnostics);
        snapshotCopy.MaximumRecords = 20;
        source.Write(DiagnosticLevel.Warning, 4, "Core75.AfterMutation", "test");
        model.Refresh();
        Assert.Equal(2, model.RetainedCount);

        var navigator = Assert.IsType<CommandSourceFileNavigator>(Required<ISourceFileNavigator>(app));
        var location = new EditorSourceLocation(Path.Combine(Path.GetTempPath(), "source with spaces.cs"), 4, 9);
        var info = navigator.StartInfo(location);
        Assert.Equal(SourceExecutable, info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Equal(new[] { "--file", location.FilePath + ":4:9" }, info.ArgumentList);
        Required<IConfigurationReader>(app).Current.GetOptions(EditorConfigurationSchemas.SourceEditor).Arguments = ["changed-{file}"];
        Assert.Equal(new[] { "--file", location.FilePath + ":4:9" }, navigator.StartInfo(location).ArgumentList);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredEditorRejectsInvalidNestedWinnerBeforeRuntimeFactory(bool microsoft)
    {
        var factories = 0;
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(AetherConfigurationSchemas.Engine);
            builder.AddSchema(AetherConfigurationSchemas.Authoring);
            builder.AddSchema(EditorConfigurationSchemas.Diagnostics);
            builder.AddSchema(EditorConfigurationSchemas.Options);
            builder.Override("Editor:SourceTracking:MaximumWatchers", 33);
        });
        var composition = new AetherComposition
        {
            HostContractId = new('a', 64), GameBuildId = new('b', 64),
            RuntimeFactory = _ => { factories++; return new NeverRuntime(); }
        };
        Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEditor(composition)).Build());
        Assert.Equal(0, factories);
    }

    [Fact]
    public void EditorCliCommonGrammarKeepsOldAndCanonicalAliasesAndTreatsFalseFlagsAsDisabled()
    {
        var expected = new EditorLaunchOptions(Path.GetFullPath("fixture.aetherproj"), null, null, null, null, true, false);
        var absent = EditorLaunchOptions.Parse(["--project", "fixture.aetherproj", "--light"]);
        var inlineFalse = EditorLaunchOptions.Parse(["--project=fixture.aetherproj", "--light=true", "--validation=false", "--smoke-viewport=false"]);
        var canonicalFalse = EditorLaunchOptions.Parse(["--EditorLaunch:ProjectPath=fixture.aetherproj", "--EditorLaunch:Light=true", "--EditorLaunch:Validation=false"]);
        Assert.Equal(expected, absent);
        Assert.Equal(expected with { ValidationSpecified = true }, inlineFalse);
        Assert.Equal(expected with { ValidationSpecified = true }, canonicalFalse);

        var environment = new Dictionary<string, string?> { [RuntimeStartupSettings.LegacyValidationKey] = "1" };
        Assert.True(absent.ResolveRuntimeSettings(environment).GpuValidation);
        environment[RuntimeStartupSettings.ValidationKey] = "false";
        Assert.False(absent.ResolveRuntimeSettings(environment).GpuValidation);
        environment[RuntimeStartupSettings.ValidationKey] = "private-invalid-validation";
        var error = Assert.Throws<ConfigurationException>(() => absent.ResolveRuntimeSettings(environment));
        Assert.All(error.Issues, issue => Assert.DoesNotContain("private-invalid-validation", issue.Message));
        Assert.False(inlineFalse.ResolveRuntimeSettings(environment).GpuValidation);
        Assert.False(canonicalFalse.ResolveRuntimeSettings(environment).GpuValidation);
        var explicitTrue = EditorLaunchOptions.Parse(["--validation"]);
        Assert.True(explicitTrue.ValidationSpecified);
        Assert.True(explicitTrue.ResolveRuntimeSettings(environment).GpuValidation);
        Assert.True((expected with { Validation = true }).ResolveRuntimeSettings(environment).GpuValidation);
    }

    [Theory]
    [InlineData("--project=first.aetherproj", "--project=second.aetherproj")]
    [InlineData("--project=first.aetherproj", "--EditorLaunch:ProjectPath=second.aetherproj")]
    [InlineData("--light", "--EditorLaunch:Light=false")]
    [InlineData("--validation=false", "--validation=true")]
    [InlineData("--light=false", "--light=false")]
    public void EditorCliRejectsDuplicatesIncludingCanonicalAliasesAndRepeatedEqualValues(params string[] arguments)
        => Assert.ThrowsAny<ArgumentException>(() => EditorLaunchOptions.Parse(arguments));

    [Theory]
    [InlineData("--unknown=private-secret")]
    [InlineData("private-secret-positional")]
    [InlineData("--project=private-secret.txt")]
    [InlineData("--validation=private-secret")]
    public void EditorCliInvalidAndUnsupportedInputKeepsValuesOutOfErrors(params string[] arguments)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() => EditorLaunchOptions.Parse(arguments));
        Assert.DoesNotContain("private-secret", error.ToString());
    }

    [Fact]
    public async Task EditorWindowSchemaPreservesStandardDefaultsAndIndependentScalarCopies()
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(EditorConfigurationSchemas.Window));
        var snapshot = await profile.LoadAsync();
        var window = EditorConfigurationSchemas.Window.Bind(snapshot);
        Assert.Equal((1280, 800, "TrueMoon Aether Editor"), (window.Width, window.Height, window.Title));
        Assert.Equal(UiFrameMode.Continuous, window.FrameMode);
        Assert.True(window.TitleBarOverContent);
        Assert.True(window.UseSystemAccentBorder);
        Assert.All(EditorConfigurationSchemas.Window.Keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        window.Width = 1400;
        window.Title = "changed";
        Assert.Equal(1280, EditorConfigurationSchemas.Window.Bind(snapshot).Width);
        Assert.Equal("TrueMoon Aether Editor", EditorConfigurationSchemas.Window.Bind(snapshot).Title);
    }

    [Fact]
    public async Task EditorWindowSchemaAcceptsItsExactMinimumWithoutOpeningAWindow()
    {
        var profile = WindowProfile(1100, 700);
        var options = EditorConfigurationSchemas.Window.Bind(await profile.LoadAsync());
        Assert.Equal((1100, 700), (options.Width, options.Height));
    }

    [Theory]
    [InlineData(1099, 700)]
    [InlineData(1100, 699)]
    public async Task EditorWindowSchemaRejectsImmediatelyBelowEachMinimum(int width, int height)
    {
        var snapshot = await WindowProfile(width, height).LoadAsync();
        var error = Assert.Throws<ConfigurationException>(() => EditorConfigurationSchemas.Window.Bind(snapshot));
        Assert.NotEmpty(error.Issues);
    }

    private static string SourceExecutable => Path.Combine(Path.GetTempPath(), "core75-source-editor.exe");
    private static IAppBuilder Builder(bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); });
    private static IConfigurationProfile Profile(Action<IConfigurationProfileBuilder>? configure = null)
        => ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(EditorConfigurationSchemas.Options);
            builder.AddSchema(EditorConfigurationSchemas.Diagnostics);
            builder.AddSchema(EditorConfigurationSchemas.SourceEditor);
            builder.AddValues("source-defaults", ConfigurationSourcePriority.Files,
            [
                Entry(Key(EditorConfigurationSchemas.SourceEditor, "ExecutablePath"), SourceExecutable),
                Entry(Key(EditorConfigurationSchemas.SourceEditor, "Arguments"), new[] { "--file", "{file}:{line}:{column}" })
            ]);
            configure?.Invoke(builder);
        });
    private static string Key<T>(IConfigurationSchema<T> schema, string suffix) where T : class
        => schema.Keys.Single(key => key.Path.EndsWith(":" + suffix, StringComparison.Ordinal)).Path;
    private static ConfigurationEntry Entry<T>(string path, T value) => new(path, JsonSerializer.SerializeToElement(value));
    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));
    private static IConfigurationProfile WindowProfile(int width, int height) => ConfigurationProfiles.Create(builder =>
    {
        builder.AddSchema(EditorConfigurationSchemas.Window);
        builder.Override("Editor:Window:Width", width);
        builder.Override("Editor:Window:Height", height);
    });
    private sealed class NeverRuntime : IRuntimeFactory, IDisposable
    {
        public int Launches, Disposals;
        public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("Unexpected child launch."); }
        public void Dispose() => Disposals++;
    }
}
