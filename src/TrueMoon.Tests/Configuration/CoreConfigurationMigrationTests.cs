using System.Text.Json;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using Xunit;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace TrueMoon.Tests.Configuration;

public sealed class CoreConfigurationMigrationTests
{
    [Fact]
    public async Task LegacyAdapterUsesNamedSourcesAndCapturesTheirValuesWithNormalPriorities()
    {
        var defaults = Section("default", ("appName", "default"), ("-root", "ignored-default"));
        var environment = Section("env", ("appName", "environment"), ("-root", "ignored-env"));
        var arguments = Section("args", ("appName", "arguments"), ("-root", "core74-args"));
        var custom = Section("foreign", ("appName", "foreign"), ("Paths:Root", "foreign-root"), ("secret", "never-imported"));
        var legacy = Legacy(defaults, environment, arguments, custom);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchemas(); builder.AddLegacySources(legacy); });
        arguments.Set("appName", "mutated");
        arguments.Set("-root", "mutated-root");

        var snapshot = await profile.LoadAsync();

        Assert.Equal("arguments", CoreConfigurationSchemas.App.Bind(snapshot).Name);
        Assert.Equal(Path.GetFullPath("core74-args"), CoreConfigurationSchemas.Paths.Bind(snapshot).Root);
        Assert.Equal("core-legacy-args", snapshot.Read<string>("App:Name").Origin!.SourceId);
        Assert.Equal(ConfigurationSourcePriority.Arguments, snapshot.Read<string>("Paths:Root").Origin!.Priority);
        Assert.Equal("arguments", CoreConfigurationSchemas.App.Bind(await profile.LoadAsync()).Name);
    }

    [Theory]
    [InlineData(false, false, "default", "core-legacy-default")]
    [InlineData(true, false, "environment", "core-legacy-env")]
    [InlineData(true, true, "arguments", "core-legacy-args")]
    public async Task LegacyAppNameFallbackRetainsSourcePriority(bool env, bool args, string expected, string source)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchemas();
            builder.AddLegacySources(Legacy(Section("default", ("appName", "default")),
                Section("env", env ? [("appName", (object?)"environment")] : []),
                Section("args", args ? [("appName", (object?)"arguments")] : [])));
        });
        var snapshot = await profile.LoadAsync();
        Assert.Equal(expected, snapshot.Read<string>("App:Name").Value);
        Assert.Equal(source, snapshot.Read<string>("App:Name").Origin!.SourceId);
        Assert.Null(CoreConfigurationSchemas.Paths.Bind(snapshot).Root);
    }

    [Fact]
    public async Task ExplicitSourcesAndOverridesCanReplaceLegacyValues()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchemas();
            builder.AddLegacySources(Legacy(Section("args", ("appName", "legacy"))));
            builder.AddArguments("new-args", ["--App:Name=canonical", "-root=canonical-root"]);
            builder.Override("App:Name", "explicit");
        });
        var snapshot = await profile.LoadAsync();
        Assert.Equal("explicit", CoreConfigurationSchemas.App.Bind(snapshot).Name);
        Assert.Equal(ConfigurationSourcePriority.Overrides, snapshot.Read<string>("App:Name").Origin!.Priority);
        Assert.Equal(Path.GetFullPath("canonical-root"), CoreConfigurationSchemas.Paths.Bind(snapshot).Root);
    }

    [Theory]
    [InlineData(false, "old-name")]
    [InlineData(true, "canonical-name")]
    public async Task OriginalArgumentsPreserveSeparatedRootAndAppNameAliasOrdering(bool canonicalLast, string expected)
    {
        string[] names = canonicalLast ? ["appName=old-name", "--App:Name", "canonical-name"]
            : ["--App:Name=canonical-name", "appName=old-name"];
        var arguments = names.Concat(["-root", "core74 root with spaces", "--foreign=ignored"]).ToList();
        var captures = 0;
        var legacyBuilder = new ConfigurationBuilder();
        legacyBuilder.RemoveProvider<EnvironmentVariablesProvider>();
        legacyBuilder.RemoveProvider<CommandLineArgsProvider>();
        legacyBuilder.AddProvider(new CommandLineArgsProvider(() => { captures++; return arguments; }));
        var legacy = legacyBuilder.Build();
        arguments.Clear();
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchemas(); builder.AddLegacySources(legacy); });

        var snapshot = await profile.LoadAsync();

        Assert.Equal(expected, CoreConfigurationSchemas.App.Bind(snapshot).Name);
        Assert.Equal(Path.GetFullPath("core74 root with spaces"), CoreConfigurationSchemas.Paths.Bind(snapshot).Root);
        Assert.Equal("core-legacy-args", snapshot.Read<string>("App:Name").Origin!.SourceId);
        Assert.Equal(1, captures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardCoreCompositionFreezesAppNameAndPathsWhileReaderReturnsCopies(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchemas();
            builder.AddArguments("args", ["--App:Name=raw-name", "-root=raw-root"]);
        });
        await using var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseCoreConfiguration(value => value.Name = "configured-name", value => value.Root = "configured-root")).Build();
        var reader = Required<IConfigurationReader>(app);
        Required<PathSettings>(app).Root = "mutated-before-resolver-creation";
        var resolver = Required<IPathResolver>(app);
        var options = reader.Current.GetOptions(CoreConfigurationSchemas.App);
        var registered = Required<AppSettings>(app);

        Assert.Equal("configured-name", app.Name);
        Assert.Equal("configured-name", registered.Name);
        Assert.Equal(Path.GetFullPath("configured-root"), resolver.ResolvePath(Paths.Root));
        Assert.Equal("raw-name", reader.Current.Read<string>("App:Name").Value);
        Assert.Equal("args", reader.Current.Read<string>("App:Name").Origin!.SourceId);
        options.Name = "reader-copy";
        registered.Name = "registered-mutated";
        app.Configuration.Set("appName", "legacy-mutated");
        Required<PathSettings>(app).Root = "path-mutated";
        Assert.Equal("configured-name", app.Name);
        Assert.Equal("configured-name", reader.Current.GetOptions(CoreConfigurationSchemas.App).Name);
        Assert.Equal(Path.GetFullPath("configured-root"), resolver.ResolvePath(Paths.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentAppsHaveIndependentStandardSettingsAndResolvers(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchemas());
        await using var first = Builder(microsoft).Setup(c => c.UseConfiguration(profile)
            .UseCoreConfiguration(value => value.Name = "one", value => value.Root = "one-root")).Build();
        await using var second = Builder(microsoft).Setup(c => c.UseConfiguration(profile)
            .UseCoreConfiguration(value => value.Name = "two", value => value.Root = "two-root")).Build();
        Assert.Equal("one", first.Name);
        Assert.Equal("two", second.Name);
        Assert.NotSame(Required<AppSettings>(first), Required<AppSettings>(second));
        Assert.Equal(Path.GetFullPath("one-root"), Required<IPathResolver>(first).ResolvePath(Paths.Root));
        Assert.Equal(Path.GetFullPath("two-root"), Required<IPathResolver>(second).ResolvePath(Paths.Root));
    }

    [Theory]
    [InlineData(false, "App:Name", " ")]
    [InlineData(true, "App:Name", " ")]
    [InlineData(false, "Paths:Root", "\u0000")]
    [InlineData(true, "Paths:Root", "\u0000")]
    public void InvalidWinningCoreValueFailsBeforeRegistrationOrFactory(bool microsoft, string key, string value)
    {
        var registrations = 0;
        var factories = 0;
        var probe = ConfigurationSchemas.Create("Probe", () => new Probe(), value => value with { }, _ => { },
            fields => fields.Field("Value", value => value.Value, (value, next) => value.Value = next));
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchemas(); builder.AddSchema(probe); builder.Override(key, value); });
        var error = Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(c =>
        {
            c.UseConfiguration(profile).UseCoreConfiguration();
            c.ConfigureModule(probe, (_, _) => registrations++);
            c.Services(s => s.Singleton<object>(_ => { factories++; return new(); }));
        }).Build());
        Assert.NotEmpty(error.Issues);
        Assert.Equal(0, registrations);
        Assert.Equal(0, factories);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CoreStartupChangesRequireRestartAndKeepTheCurrentGeneration(bool microsoft)
    {
        var source = new MutableSource([Entry("App:Name", "before"), Entry("Paths:Root", "before-root")]);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchemas(); builder.AddSource("mutable", ConfigurationSourcePriority.Files, source); });
        await using var app = Builder(microsoft).Setup(c => c.UseConfiguration(profile).UseCoreConfiguration()).Build();
        var reader = Required<IConfigurationReader>(app);
        var previous = reader.Current;
        source.Entries = [Entry("App:Name", "after"), Entry("Paths:Root", "after-root")];

        var result = await Required<IConfigurationReloader>(app).ReloadAsync();

        Assert.False(result.Published);
        Assert.True(result.RestartRequired);
        Assert.Equal(new[] { "App:Name", "Paths:Root" }, result.ChangedKeys);
        Assert.Equal(1, result.Generation);
        Assert.Same(previous, reader.Current);
        Assert.Equal("before", app.Name);
        Assert.Equal(Path.GetFullPath("before-root"), Required<IPathResolver>(app).ResolvePath(Paths.Root));
        Assert.All(CoreConfigurationSchemas.App.Keys.Concat(CoreConfigurationSchemas.Paths.Keys),
            key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredDiagnosticsUsesDiWithoutLegacyMirrorAndOwnsSinkProducts(bool microsoft)
    {
        var factory = new SinkFactory();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(DiagnosticsConfigurationSchema.Default);
            builder.Override("Diagnostics:QueueCapacity", 19);
            builder.Override("Diagnostics:ReservedErrorCapacity", 2);
            builder.Override("Diagnostics:MinimumLevel", DiagnosticLevel.Warning);
        });
        var app = Builder(microsoft).Setup(c => c.UseConfiguration(profile).UseConfiguredDiagnostics(
            action: value => value.AddFilter("core74"),
            configure: value => { value.MemoryCapacity = 23; value.ShutdownTimeout = TimeSpan.FromSeconds(2); },
            sinkFactories: [factory])).Build();
        try
        {
            var settings = Required<DiagnosticsSettings>(app);
            var effective = Required<DiagnosticPipeline>(app).ReadConfiguration();
            Assert.Equal((19, 2, 23), (effective.QueueCapacity, effective.ReservedErrorCapacity, effective.MemoryCapacity));
            Assert.Equal(DiagnosticLevel.Warning, effective.MinimumLevel);
            Assert.Equal(TimeSpan.FromSeconds(2), effective.ShutdownTimeout);
            Assert.Equal(1, effective.CustomSinkCount);
            Assert.Equal(1, factory.Creates);
            Assert.Contains("core74", Required<IDiagnosticsConfiguration>(app).Filters);
            Assert.Null(app.Configuration.Get<IDiagnosticsConfiguration>(TrueMoon.Diagnostics.ConfigurationExtensions.DiagnosticsConfigurationName));
            settings.QueueCapacity = 999;
            Assert.Equal(19, Required<DiagnosticPipeline>(app).ReadConfiguration().QueueCapacity);
            Assert.Equal(19, Required<IConfigurationReader>(app).Current.GetOptions(DiagnosticsConfigurationSchema.Default).QueueCapacity);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, factory.Sink.Disposals);
        Assert.Equal(0, factory.Disposals);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 0)]
    [InlineData(false, 4, 4)]
    [InlineData(true, 4, 4)]
    public void InvalidDiagnosticBudgetsFailBeforeSinkCreation(bool microsoft, int queue, int reserved)
    {
        var factory = new SinkFactory();
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(DiagnosticsConfigurationSchema.Default));
        Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(c => c.UseConfiguration(profile)
            .UseConfiguredDiagnostics(configure: value => { value.QueueCapacity = queue; value.ReservedErrorCapacity = reserved; }, sinkFactories: [factory])).Build());
        Assert.Equal(0, factory.Creates);
        Assert.Equal(0, factory.Sink.Disposals);
        Assert.Equal(0, factory.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiagnosticRulesAndNestedSettingsBindIntoPipelineAndRemainDeepCopied(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(DiagnosticsConfigurationSchema.Default);
            builder.AddValues("diagnostics", ConfigurationSourcePriority.Files,
            [
                Entry("Diagnostics:SourceMinimumLevels", new Dictionary<string, string> { ["Core74.Service"] = "Warning" }),
                Entry("Diagnostics:SinkMinimumLevels", new Dictionary<string, string> { ["custom-0"] = "Error" }),
                Entry("Diagnostics:DisabledEvents", new[] { "Core74.Disabled" }),
                Entry("Diagnostics:JsonRetention:MaxRuns", 3),
                Entry("Diagnostics:JsonRetention:MaxTotalBytes", 123456L),
                Entry("Diagnostics:JsonRetention:MaxAge", "2.00:00:00"),
                Entry("Diagnostics:Emergency:Enabled", false),
                Entry("Diagnostics:Emergency:QueueCapacity", 3),
                Entry("Diagnostics:Emergency:MaxReports", 4),
                Entry("Diagnostics:Emergency:MaxMessageChars", 256),
                Entry("Diagnostics:Telemetry:TracingEnabled", false),
                Entry("Diagnostics:Telemetry:MetricsEnabled", false),
                Entry("Diagnostics:Telemetry:MaxOperationNames", 7),
                Entry("Diagnostics:Telemetry:MaxProperties", 5),
                Entry("Diagnostics:Telemetry:MaxPropertyChars", 37),
                Entry("Diagnostics:Telemetry:MaxNameChars", 43)
            ]);
        });
        await using var app = Builder(microsoft).Setup(c => c.UseConfiguration(profile).UseConfiguredDiagnostics()).Build();
        var reader = Required<IConfigurationReader>(app);
        var options = reader.Current.GetOptions(DiagnosticsConfigurationSchema.Default);
        var pipeline = Required<DiagnosticPipeline>(app).ReadConfiguration();
        Assert.Equal(DiagnosticLevel.Warning, options.SourceMinimumLevels["Core74.Service"]);
        Assert.Equal(DiagnosticLevel.Error, options.SinkMinimumLevels["custom-0"]);
        Assert.Equal(new[] { "Core74.Disabled" }, options.DisabledEvents);
        Assert.Equal((3, 123456L, TimeSpan.FromDays(2)), (pipeline.JsonMaxRuns, pipeline.JsonMaxTotalBytes, pipeline.JsonMaxAge));
        Assert.Equal((1, 1, 1), (pipeline.SourceRuleCount, pipeline.SinkRuleCount, pipeline.DisabledEventCount));
        Assert.False(pipeline.EmergencyEnabled);
        Assert.False(pipeline.TracingEnabled);
        Assert.False(pipeline.MetricsEnabled);
        Assert.Equal(7, pipeline.MaxOperationNames);
        Assert.Equal((3, 4, 256), (options.EmergencyQueueCapacity, options.EmergencyMaxReports, options.EmergencyMaxMessageChars));
        Assert.Equal((5, 37, 43), (options.TelemetryMaxProperties, options.TelemetryMaxPropertyChars, options.TelemetryMaxNameChars));
        var telemetry = Assert.IsType<DiagnosticTelemetry>(Required<IDiagnosticTelemetry>(app));
        Assert.Null(telemetry.StartOperation(new string('x', 44)));
        Assert.Equal(1, telemetry.Read().RejectedOperations);

        Assert.IsAssignableFrom<IDictionary<string, DiagnosticLevel>>(options.SourceMinimumLevels)["Core74.Service"] = DiagnosticLevel.Trace;
        options.DisabledEvents[0] = "mutated-copy";
        var registered = Required<DiagnosticsSettings>(app);
        Assert.IsAssignableFrom<IDictionary<string, DiagnosticLevel>>(registered.SinkMinimumLevels)["custom-0"] = DiagnosticLevel.Trace;
        registered.DisabledEvents[0] = "mutated-registration";
        var again = reader.Current.GetOptions(DiagnosticsConfigurationSchema.Default);
        Assert.Equal(DiagnosticLevel.Warning, again.SourceMinimumLevels["Core74.Service"]);
        Assert.Equal(DiagnosticLevel.Error, again.SinkMinimumLevels["custom-0"]);
        Assert.Equal(new[] { "Core74.Disabled" }, again.DisabledEvents);
        Assert.All(DiagnosticsConfigurationSchema.Default.Keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
    }

    [Theory]
    [InlineData(false, "Diagnostics:JsonRetention:MaxRuns", "0")]
    [InlineData(true, "Diagnostics:Emergency:QueueCapacity", "129")]
    [InlineData(false, "Diagnostics:Telemetry:MaxOperationNames", "257")]
    [InlineData(true, "Diagnostics:SourceMinimumLevels", "{\"private-rule\":999}")]
    [InlineData(false, "Diagnostics:SinkMinimumLevels", "{\"private-rule\":\"Warning\",\"private-rule\":\"Error\"}")]
    public void InvalidNestedDiagnosticsAndRulesCannotCreateResources(bool microsoft, string key, string json)
    {
        using var document = JsonDocument.Parse(json);
        var factory = new SinkFactory();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(DiagnosticsConfigurationSchema.Default);
            builder.AddValues("diagnostics", ConfigurationSourcePriority.Files, [new ConfigurationEntry(key, document.RootElement)]);
        });
        var error = Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(c => c.UseConfiguration(profile)
            .UseConfiguredDiagnostics(sinkFactories: [factory])).Build());
        Assert.NotEmpty(error.Issues);
        Assert.All(error.Issues, issue => Assert.DoesNotContain("private-rule", issue.Message));
        Assert.Equal(0, factory.Creates);
        Assert.Equal(0, factory.Sink.Disposals);
    }

    private static ConfigurationSection Section(string name, params (string Key, object? Value)[] values)
        => new(name, values.ToDictionary(value => value.Key, value => value.Value));
    private static CommonConfiguration Legacy(params IConfigurationSection[] sections) => new([new Provider(sections)]);
    private sealed class Provider(IConfigurationSection[] sections) : IConfigurationProvider
    { public string Name => "fixed"; public IReadOnlyList<IConfigurationSection> GetSections() => sections; }
    private sealed record Probe { public int Value { get; set; } }
    private static AppBuilder Builder(bool microsoft) => new(microsoft ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder());
    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));
    private static ConfigurationEntry Entry<T>(string path, T value) => new(path, JsonSerializer.SerializeToElement(value));
    private sealed class MutableSource(IReadOnlyList<ConfigurationEntry> entries) : IConfigurationSource
    {
        public IReadOnlyList<ConfigurationEntry> Entries = entries;
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries);
    }
    private sealed class SinkFactory : IFactory<IDiagnosticSink>, IDisposable
    {
        public int Creates, Disposals;
        public CountingSink Sink { get; } = new();
        public IDiagnosticSink Create() { Creates++; return Sink; }
        public IDiagnosticSink Create<TData>(TData? data = default) => Create();
        public void Dispose() => Disposals++;
    }
    private sealed class CountingSink : IDiagnosticSink
    {
        public int Disposals;
        public ValueTask WriteAsync(DiagnosticRecord record, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
