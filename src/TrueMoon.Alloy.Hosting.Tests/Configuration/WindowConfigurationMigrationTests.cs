using System.Text.Json;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Alloy.Hosting.Tests.Configuration;

public sealed class WindowConfigurationMigrationTests
{
    [Fact]
    public async Task WindowSchemaDeclaresScalarStartupDataWithoutRendererOrViewFactories()
    {
        var schema = WindowConfigurationSchema.Default;
        Assert.Equal("Alloy:Window", schema.Section);
        Assert.Equal(new[] { "Alloy:Window:EnableDiagnostics", "Alloy:Window:FrameMode", "Alloy:Window:FramesPerSecond", "Alloy:Window:GraphicsApi",
            "Alloy:Window:Height", "Alloy:Window:Title", "Alloy:Window:TitleBarOverContent", "Alloy:Window:UseDefaultWindowFrame",
            "Alloy:Window:UseSystemAccentBorder", "Alloy:Window:Width" }, schema.Keys.Select(key => key.Path).Order(StringComparer.Ordinal));
        Assert.All(schema.Keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(schema));
        var snapshot = await profile.LoadAsync(); var settings = schema.Bind(snapshot);
        Assert.Equal((960, 540, 60), (settings.Width, settings.Height, settings.FramesPerSecond));
        Assert.Equal("TrueMoon UI", settings.Title); Assert.Equal(UiFrameMode.Automatic, settings.FrameMode); Assert.Null(settings.GraphicsApi);
        Assert.True(settings.UseDefaultWindowFrame); Assert.False(settings.EnableDiagnostics);
        Assert.False(settings.TitleBarOverContent); Assert.False(settings.UseSystemAccentBorder);
        settings.Width = 1;
        Assert.Equal(960, schema.Bind(snapshot).Width);
    }

    [Fact]
    public async Task ConfiguredPresentationAppliesPreparedDataToExistingWindowAndKeepsConstructionLazy()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(WindowConfigurationSchema.Default);
            builder.AddValues("file", ConfigurationSourcePriority.Files, [Entry("Alloy:Window:Width", 320), Entry("Alloy:Window:Height", 240)]);
            builder.AddArguments("args", ["--Alloy:Window:Width=512", "--Alloy:Window:Title=prepared"]);
        });
        var composed = 0; var roots = 0;
        SilkUiWindowOptions? captured = null;
        var appearance = new WindowAppearance { Decorated = false };
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(
            _ => { roots++; return new Panel(); }, options =>
            {
                composed++; options.UseSkiaVulkan().UseSilkWindow(window => { captured = window; window.Appearance = appearance; });
            }, settings => { settings.Height = 384; settings.FramesPerSecond = 90; settings.FrameMode = UiFrameMode.OnDemand; }));
        var reader = Required<IConfigurationReader>(app);
        Assert.Equal(512, reader.Current.Read<int>("Alloy:Window:Width").Value);
        Assert.Equal(240, reader.Current.Read<int>("Alloy:Window:Height").Value);
        Assert.Equal("args", reader.Current.Read<int>("Alloy:Window:Width").Origin!.SourceId);
        Assert.NotNull(captured);
        Assert.Equal((512, 384, 90), (captured.Width, captured.Height, captured.FramesPerSecond));
        Assert.Equal("prepared", captured.Title); Assert.Same(appearance, captured.Appearance); Assert.Equal(UiFrameMode.OnDemand, captured.FrameMode);
        Assert.Equal(1, composed); Assert.Equal(0, roots);
        var copy = reader.Current.GetOptions(WindowConfigurationSchema.Default); copy.Width = 999;
        captured.FrameMode = (UiFrameMode)999; captured.FramesPerSecond = 0;
        var windowService = Required<HostedUiWindow>(app);
        Assert.False(windowService.Completion.IsCompleted); Assert.Equal(0, windowService.PresentedFrames);
        Assert.Equal(0, Required<HostedUiSessionFactory>(app).ActiveSessionCount);
        Assert.Equal(512, reader.Current.GetOptions(WindowConfigurationSchema.Default).Width);
        Assert.Equal(0, roots);
    }

    [Fact]
    public async Task ConfiguredChromeFlagsPreserveBorrowedTitlebarHookWithoutExecutingIt()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(WindowConfigurationSchema.Default);
            builder.Override("Alloy:Window:TitleBarOverContent", true);
            builder.Override("Alloy:Window:UseSystemAccentBorder", true);
            builder.Override("Alloy:Window:EnableDiagnostics", true);
        });
        SilkUiWindowOptions? captured = null;
        var hooks = 0;
        Action<WindowTitleBar, Element, Action> titlebar = (_, _, _) => hooks++;
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(_ => new Panel(),
            options => options.UseSkiaVulkan().UseSilkWindow(window =>
            {
                captured = window; window.Appearance = new WindowAppearance { Decorated = false };
                window.Chrome = new WindowChromeOptions(); window.ConfigureDefaultTitleBar = titlebar;
            })));
        Assert.NotNull(captured);
        Assert.True(captured.TitleBarOverContent); Assert.True(captured.UseSystemAccentBorder); Assert.True(captured.EnableDiagnostics);
        Assert.True(captured.UseDefaultWindowFrame); Assert.Same(titlebar, captured.ConfigureDefaultTitleBar); Assert.Equal(0, hooks);
        Assert.False(Required<HostedUiWindow>(app).Completion.IsCompleted);
        Assert.Equal(0, Required<HostedUiSessionFactory>(app).ActiveSessionCount);
    }

    [Fact]
    public async Task ConfiguredPresentationCreatesMissingWindowWithoutReplacingExplicitRenderer()
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(WindowConfigurationSchema.Default));
        var roots = 0;
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(
            _ => { roots++; return new Panel(); }, options => options.UseSkiaOpenGL()));
        var window = Required<HostedUiWindow>(app);
        Assert.Equal(UiRendererKind.OpenGL, Required<HostedUiSessionFactory>(app).Renderer);
        Assert.False(window.Completion.IsCompleted); Assert.Equal(0, roots);
    }

    [Theory]
    [InlineData("Width", "0")]
    [InlineData("Height", "-1")]
    [InlineData("FramesPerSecond", "0")]
    [InlineData("FramesPerSecond", "241")]
    [InlineData("FrameMode", "999")]
    [InlineData("GraphicsApi", "999")]
    [InlineData("Title", "\" \"")]
    [InlineData("Title", "\"secret-invalid\\n\"")]
    public void InvalidWinningWindowDataFailsBeforeCompositionRootOrNativeStartup(string field, string json)
    {
        using var document = JsonDocument.Parse(json);
        var composed = 0; var roots = 0; var otherRegistration = 0;
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(WindowConfigurationSchema.Default); builder.AddSchema(ProbeSchema);
            builder.AddValues("bad", ConfigurationSourcePriority.Overrides, [new("Alloy:Window:" + field, document.RootElement)]);
        });
        var error = Assert.Throws<ConfigurationException>(() => App.Build(context =>
        {
            context.UseConfiguration(profile).UseConfiguredPresentation(_ => { roots++; return new Panel(); },
                options => { composed++; options.UseSkiaVulkan(); });
            context.ConfigureModule(ProbeSchema, (_, _) => otherRegistration++);
        }));
        Assert.Equal(0, composed); Assert.Equal(0, roots); Assert.Equal(0, otherRegistration);
        Assert.DoesNotContain("secret-invalid", error.ToString());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    public async Task FrameRateBoundaryValuesBindWithoutCreatingNativeResources(int rate)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(WindowConfigurationSchema.Default); builder.Override("Alloy:Window:FramesPerSecond", rate); });
        var rootCalls = 0;
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(
            _ => { rootCalls++; return new Panel(); }, options => options.UseSkiaVulkan()));
        Assert.Equal(rate, Required<IConfigurationReader>(app).Current.GetOptions(WindowConfigurationSchema.Default).FramesPerSecond);
        Assert.False(Required<HostedUiWindow>(app).Completion.IsCompleted); Assert.Equal(0, rootCalls);
    }

    [Fact]
    public async Task WindowStartupReloadRejectsWholeCandidateAndDoesNotRecomposeTheWindow()
    {
        var source = new MutableSource([Entry("Alloy:Window:Width", 640)]);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(WindowConfigurationSchema.Default); builder.AddSource("mutable", ConfigurationSourcePriority.Files, source); });
        var composed = 0; var roots = 0;
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(
            _ => { roots++; return new Panel(); }, options => { composed++; options.UseSkiaVulkan(); }));
        var window = Required<HostedUiWindow>(app); var reader = Required<IConfigurationReader>(app); var previous = reader.Current;
        source.Entries = [Entry("Alloy:Window:Width", 800)];
        var update = await Required<IConfigurationReloader>(app).ReloadAsync();
        Assert.False(update.Published); Assert.True(update.RestartRequired); Assert.Equal(1, update.Generation);
        Assert.Equal(new[] { "Alloy:Window:Width" }, update.ChangedKeys);
        Assert.Same(previous, reader.Current); Assert.Same(window, Required<HostedUiWindow>(app));
        Assert.Equal(640, reader.Current.GetOptions(WindowConfigurationSchema.Default).Width);
        Assert.Equal(1, composed); Assert.Equal(0, roots); Assert.Equal(0, window.PresentedFrames);
    }

    [Fact]
    public void WindowDataCannotOverrideExplicitRendererCompatibilityOrAttachTwice()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(WindowConfigurationSchema.Default); builder.Override("Alloy:Window:GraphicsApi", UiWindowGraphicsApi.OpenGL); });
        var roots = 0;
        Assert.Throws<InvalidOperationException>(() => App.Build(context => context.UseConfiguration(profile).UseConfiguredPresentation(
            _ => { roots++; return new Panel(); }, options => options.UseSkiaVulkan())));
        Assert.Equal(0, roots);
        var context = new AppConfigurationContext();
        context.UseConfiguredPresentation(_ => new Panel(), options => options.UseSkiaVulkan());
        Assert.Throws<InvalidOperationException>(() => context.UseConfiguredPresentation(_ => new Panel(), options => options.UseSkiaVulkan()));
        Assert.Empty(context.GetServicesRegistrations());
    }

    [Fact]
    public void ApplyingScalarWindowDataPreservesBorrowedAppearanceChromeAndFactories()
    {
        var appearance = new WindowAppearance { Decorated = false }; var chrome = new WindowChromeOptions();
        var callbacks = 0;
        var options = new SilkUiWindowOptions { Appearance = appearance, Chrome = chrome, TitleBarFactory = _ => { callbacks++; return new Panel(); } };
        var titleBar = options.TitleBarFactory;
        var settings = new WindowSettings { Width = 800, Height = 600, Title = "configured", EnableDiagnostics = true,
            FrameMode = UiFrameMode.OnDemand, FramesPerSecond = 75, GraphicsApi = UiWindowGraphicsApi.OpenGL, UseDefaultWindowFrame = false };
        settings.ApplyTo(options);
        Assert.Equal((800, 600, 75), (options.Width, options.Height, options.FramesPerSecond));
        Assert.Equal("configured", options.Title); Assert.True(options.EnableDiagnostics); Assert.Equal(UiFrameMode.OnDemand, options.FrameMode);
        Assert.Equal(UiWindowGraphicsApi.OpenGL, options.GraphicsApi); Assert.False(options.UseDefaultWindowFrame);
        Assert.Same(appearance, options.Appearance); Assert.Same(chrome, options.Chrome); Assert.Same(titleBar, options.TitleBarFactory);
        Assert.Equal(0, callbacks);
        Assert.Throws<ArgumentNullException>(() => settings.ApplyTo(null!));
    }

    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));
    private static ConfigurationEntry Entry(string key, object? value) => new(key, JsonSerializer.SerializeToElement(value));
    private static readonly IConfigurationSchema<Probe> ProbeSchema = ConfigurationSchemas.Create("Probe", () => new Probe(), value => value with { }, _ => { },
        fields => fields.Field("Value", value => value.Value, (value, next) => value.Value = next));
    private sealed record Probe { public int Value { get; set; } }
    private sealed class MutableSource(ConfigurationEntry[] entries) : IConfigurationSource
    {
        internal ConfigurationEntry[] Entries = entries;
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(Entries.ToArray()); }
    }
}
