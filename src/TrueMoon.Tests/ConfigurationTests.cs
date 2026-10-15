using System.Globalization;
using System.Reflection;
using System.Text.Json;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Tests;

public class ConfigurationTests
{
    [Fact]
    public void BuilderAddsProvidersByPriorityAndRemovesByIdentityOrType()
    {
        var first = new TestProvider("one", Section("default", ("number", "11"), ("fallback", "kept")));
        var second = new TestProvider("two", Section("default", ("number", "22")));
        var builder = new ConfigurationBuilder();
        builder.AddProvider(first);
        builder.AddProvider(second);
        var original = builder.Build();
        Assert.Equal(22, original.Get<int>("number"));
        Assert.Equal("kept", original.Get<string>("fallback", "default"));
        Assert.Equal("22", original["number"]);
        builder.RemoveProvider(first);
        Assert.Equal(22, builder.Build().Get<int>("number"));
        Assert.False(builder.Build().Exist("fallback"));
        builder.RemoveProvider<TestProvider>();
        Assert.False(builder.Build().Exist("number"));
        Assert.Equal(22, original.Get<int>("number"));
    }

    [Fact]
    public async Task NamedAndUnqualifiedReadsUseTheSameHighestExistingKey()
    {
        var high = Section("shared", ("key", "high"), ("invalid", "no"), ("nullable", null));
        var low = Section("shared", ("key", "low"), ("missingAbove", "fallback"), ("invalid", "7"), ("nullable", "low"));
        var configuration = new CommonConfiguration([new TestProvider("high", high), new TestProvider("low", low)]);
        Assert.Equal("high", configuration.Get<string>("key"));
        Assert.Equal("high", configuration["key", "shared"]);
        Assert.True(configuration.Exist("key"));
        Assert.True(configuration.Exist("key", "shared"));
        var merged = Assert.IsAssignableFrom<IConfigurationSection>(await configuration.GetSectionAsync("shared"));
        Assert.Equal("fallback", merged.Get<string>("missingAbove"));
        Assert.Equal(new[] { "key", "invalid", "nullable", "missingAbove" }, merged.GetKeys());
        Assert.Equal("high", merged.GetList().Single(entry => entry.key == "key").value);
        Assert.Equal(new object?[] { "high", "no", null, "fallback" }, merged.GetValues());
        Assert.Null(configuration.Get<string>("nullable"));
        Assert.Throws<FormatException>(() => configuration.Get<int>("invalid"));
        Assert.False(merged.TryGetValue<int>("invalid", out _));
        merged.Set("key", "changed");
        Assert.Equal("changed", high.Get<string>("key"));
        Assert.Equal("low", low.Get<string>("key"));
        Assert.Null(configuration.GetSection("absent"));
        Assert.False(configuration.TryGetSection("absent", out _));
        Assert.False(configuration.Exist("absent"));
    }

    [Fact]
    public async Task ValuesConvertUsingInvariantCultureAndPreserveMissingNullAndInvalidDistinctions()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            var section = Section("values", ("decimal", "1.25"), ("int", "42"), ("bool", "true"),
                ("enum", "friday"), ("guid", "936c1605-73d7-45df-bde7-2b7f7ff147af"), ("null", null), ("bad", "word"), ("overflow", "2147483648"));
            Assert.Equal(1.25m, section.Get<decimal>("decimal"));
            Assert.Equal(42, await section.GetAsync<int>("int"));
            Assert.Equal(42, section.Get<int?>("int"));
            Assert.True(section.Get<bool>("bool"));
            Assert.Equal(DayOfWeek.Friday, section.Get<DayOfWeek>("enum"));
            Assert.Equal(Guid.Parse("936c1605-73d7-45df-bde7-2b7f7ff147af"), section.Get<Guid>("guid"));
            Assert.Equal(0, section.Get<int>("missing"));
            Assert.False(section.TryGetValue<int>("missing", out _));
            Assert.True(section.TryGetValue<string>("null", out var nullValue));
            Assert.Null(nullValue);
            Assert.True(section.TryGetValue<int?>("null", out var nullable));
            Assert.Null(nullable);
            Assert.False(section.TryGetValue<int>("null", out _));
            foreach (var key in new[] { "bad", "overflow", "null" })
            {
                Assert.Throws<FormatException>(() => section.Get<int>(key));
                Assert.False(section.TryGetValue<int>(key, out _));
            }
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => section.GetAsync<int>("int", cancellation.Token));
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    [Fact]
    public void GetOrCreateUsesTypeNameAndRetainsIdentity()
    {
        var configuration = new ConfigurationBuilder().Build();
        var created = configuration.GetOrCreate<Settings>();
        created.Count = 8;
        Assert.Same(created, configuration.GetOrCreate<Settings>());
        Assert.Same(created, configuration.GetSection()!.Get<Settings>(nameof(Settings)));
        Assert.Equal(8, configuration.Get<Settings>()!.Count);
    }

    [Theory]
    [InlineData("default", false)]
    [InlineData("settings", false)]
    [InlineData("default", true)]
    [InlineData("settings", true)]
    public void GetOrCreateReplacesWinningNullAndRetainsIdentity(string sectionName, bool named)
    {
        var key = nameof(Settings);
        var high = Section(sectionName, (key, null));
        var fallback = new Settings { Count = 17 };
        var low = Section("default", (key, fallback));
        var configuration = new CommonConfiguration([new TestProvider("high", high), new TestProvider("low", low)]);
        var requestedSection = named ? sectionName : null;

        var created = configuration.GetOrCreate<Settings>(sectionName: requestedSection);
        created.Count = 23;

        Assert.Same(created, configuration.GetOrCreate<Settings>(sectionName: requestedSection));
        Assert.Same(created, configuration.Get<Settings>(sectionName: requestedSection));
        Assert.Same(created, high.Get<Settings>(key));
        Assert.Equal(23, configuration.Get<Settings>(sectionName: requestedSection)!.Count);
        Assert.Same(fallback, low.Get<Settings>(key));
        Assert.Equal(17, fallback.Count);
    }

    [Fact]
    public void UnqualifiedSetKeepsDefaultTargetWhenNamedSectionMasksTheKey()
    {
        var high = Section("settings", ("key", "high"));
        var low = Section("default", ("key", "low"));
        var configuration = new CommonConfiguration([new TestProvider("high", high), new TestProvider("low", low)]);

        configuration.Set("key", "changed");

        Assert.Equal("changed", low.Get<string>("key"));
        Assert.Equal("high", configuration.Get<string>("key"));
        Assert.Equal("changed", configuration.Get<string>("key", "default"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetOrCreateRejectsInvalidWinningValueWithoutChangingProviders(bool named)
    {
        var key = nameof(Settings);
        var high = Section("default", (key, "invalid"));
        var fallback = new Settings { Count = 17 };
        var low = Section("default", (key, fallback));
        var configuration = new CommonConfiguration([new TestProvider("high", high), new TestProvider("low", low)]);

        Assert.Throws<FormatException>(() => configuration.GetOrCreate<Settings>(sectionName: named ? "default" : null));

        Assert.Equal("invalid", high.Get<string>(key));
        Assert.Same(fallback, low.Get<Settings>(key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BootstrapConfiguresValuesBeforeRegistrationsAndServiceCreation(bool microsoftDI)
    {
        var trace = new List<string>();
        IConfiguration? configured = null;
        IConfiguration? registered = null;
        var builder = App.Builder(context => { if (microsoftDI) context.UseDI(); })
            .Configuration(configuration =>
            {
                trace.Add("providers");
                configuration.AddProvider(new TestProvider("custom", Section("default", ("number", "41"))));
            })
            .Setup(context => context
                .Services((configuration, services) =>
                {
                    trace.Add("registrations");
                    registered = configuration;
                    var number = configuration.Get<int>("number");
                    services.Singleton<Settings>(_ =>
                    {
                        trace.Add("factory");
                        return new Settings { Count = number };
                    });
                })
                .Configuration(configuration =>
                {
                    trace.Add("configuration");
                    configured = configuration;
                    configuration.Set("number", configuration.Get<int>("number") + 1);
                }));

        await using var app = builder.Build();
        var settings = app.Services.Resolve<Settings>();

        Assert.Equal(new[] { "providers", "configuration", "registrations", "factory" }, trace);
        Assert.Same(configured, registered);
        Assert.Same(configured, app.Configuration);
        Assert.Same(configured, app.Services.Resolve<IConfiguration>());
        Assert.Equal(42, settings!.Count);
        await app.StartAsync();
        await app.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BootstrapConfigurationFailurePreservesCauseAndSkipsRegistrations(bool microsoftDI)
    {
        var failure = new InvalidOperationException("configuration validation failed");
        var trace = new List<string>();
        var builder = App.Builder(context => { if (microsoftDI) context.UseDI(); })
            .Setup(context => context
                .Services((_, services) =>
                {
                    trace.Add("registrations");
                    services.Singleton<Settings>(_ =>
                    {
                        trace.Add("factory");
                        return new Settings();
                    });
                })
                .Configuration(_ =>
                {
                    trace.Add("configuration");
                    throw failure;
                }));

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => builder.Build()));
        Assert.Equal(new[] { "configuration" }, trace);
    }

    [Fact]
    public async Task RefreshCapturesArgumentsAndEnvironmentAgainWithoutChangingPreviousSnapshot()
    {
        var arguments = new[] { "-token=a=b=c", "-empty=", "-flag", "-repeat=first", "-repeat=last" };
        var provider = new CommandLineArgsProvider(() => arguments);
        var name = "TRUEMOON_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(name, "before");
        try
        {
            var configuration = new CommonConfiguration([provider, new EnvironmentVariablesProvider()]);
            var old = configuration.GetSection("args")!;
            Assert.Equal("a=b=c", configuration.Get<string>("-token"));
            Assert.Equal("", configuration.Get<string>("-empty"));
            Assert.True(configuration.Exist("-flag"));
            Assert.Null(configuration.Get<string>("-flag"));
            Assert.Equal("last", configuration.Get<string>("-repeat"));
            arguments = ["-token=new"];
            Environment.SetEnvironmentVariable(name, "after");
            Assert.Equal("before", configuration.Get<string>(name));
            await configuration.RefreshAsync();
            Assert.Equal("new", configuration.Get<string>("-token"));
            Assert.Equal("a=b=c", old.Get<string>("-token"));
            Assert.Equal("after", configuration.Get<string>(name));
            Assert.False(configuration.Exist("-repeat"));
            var actualArgs = new CommandLineArgsProvider().GetSections().Single();
            Assert.False(actualArgs.Exist(Environment.GetCommandLineArgs()[0]));
        }
        finally { Environment.SetEnvironmentVariable(name, null); }
    }

    [Fact]
    public void PathResolverParsesExactRootArgumentAndUsesProductMetadata()
    {
        var requested = Path.Combine(Path.GetTempPath(), "TrueMoon=value");
        var paths = new PathResolver(["ignored-root=bad", "-root=previous", "-root=" + requested]);
        Assert.Equal(Path.GetFullPath(requested), paths.ResolvePath(Paths.Root));
        Assert.Equal(Path.Combine(Path.GetFullPath(requested), "Configuration"), paths.ResolvePath(Paths.Configuration));
        Assert.Throws<ArgumentException>(() => new PathResolver(["-root="]));
        var entry = Assembly.GetEntryAssembly()!;
        var company = entry.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
        var product = entry.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? entry.GetName().Name!;
        var defaults = new PathResolver([]);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), company, product), defaults.ResolvePath(Paths.Root));
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), company, product), defaults.ResolvePath(Paths.Secrets));
    }

    [Fact]
    public async Task JsonSectionReadsTypedObjectsAndPersistsWrites()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "settings.json");
        File.WriteAllText(path, "{\"count\":3,\"textNumber\":\"12\",\"enabled\":true,\"empty\":null,\"options\":{\"Count\":9}}");
        var section = new JsonConfigurationSection(path);
        Assert.Equal("settings", section.Name);
        Assert.Equal(Path.GetFullPath(path), section.GetFileInfo().FullName);
        Assert.Equal(3, section.Get<int>("count"));
        Assert.Equal(12, await section.GetAsync<int>("textNumber"));
        Assert.True(section.Get<bool>("enabled"));
        Assert.Equal(9, section.Get<Settings>("options")!.Count);
        Assert.True(section.Exist("empty"));
        Assert.Null(section.Get<string>("empty"));
        Assert.False(section.TryGetValue<int>("missing", out _));
        Assert.Equal(new[] { "count", "textNumber", "enabled", "empty", "options" }, section.GetKeys());
        Assert.Equal("3", section.GetValues()[0]!.ToString());
        Assert.Null(section.GetList().Single(entry => entry.key == "empty").value);
        Assert.Throws<FormatException>(() => section.Get<int>("options"));
        Assert.False(section.TryGetValue<int>("options", out _));
        section.Set("count", 7);
        section.Set("added", new Settings { Count = 14 });
        var reloaded = new JsonConfigurationSection(path);
        Assert.Equal(7, reloaded.Get<int>("count"));
        Assert.Equal(14, reloaded.Get<Settings>("added")!.Count);
        Assert.Equal(6, reloaded.GetList().Count);
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("[]")]
    [InlineData("null")]
    public void JsonInvalidFilesSurfaceErrorsAndFailedRefreshKeepsLastValidSnapshot(string invalidJson)
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "app.json");
        File.WriteAllText(file, "{\"value\":5}");
        var configuration = new CommonConfiguration([new JsonConfigurationProvider(new FixedPaths(folder.Path))]);
        File.WriteAllText(file, invalidJson);
        Assert.ThrowsAny<JsonException>(() => configuration.Refresh());
        Assert.Equal(5, configuration.Get<int>("value", "app"));
        Assert.ThrowsAny<JsonException>(() => new JsonConfigurationSection(file));
    }

    [Fact]
    public void JsonRefreshDiscoversChangesAndRemovalsAndMissingFilesAreDistinct()
    {
        using var folder = new TemporaryFolder();
        var provider = new JsonConfigurationProvider(new FixedPaths(folder.Path));
        Assert.Empty(provider.GetSections());
        var file = Path.Combine(folder.Path, "app.json");
        Assert.Throws<FileNotFoundException>(() => new JsonConfigurationSection(file));
        File.WriteAllText(file, "{\"value\":1}");
        var configuration = new CommonConfiguration([provider]);
        File.WriteAllText(file, "{\"value\":2}");
        File.WriteAllText(Path.Combine(folder.Path, "new.json"), "{\"other\":3}");
        configuration.Refresh();
        Assert.Equal(2, configuration.Get<int>("value", "app"));
        Assert.Equal(3, configuration.Get<int>("other", "new"));
        File.Delete(file);
        configuration.Refresh();
        Assert.Null(configuration.GetSection("app"));
        Assert.Single(configuration.GetSections());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRefreshPreservesAllProvidersAndAllowsRecovery(bool asynchronous)
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "settings.json");
        File.WriteAllText(file, "{\"value\":5}");
        var arguments = new[] { "-value=old" };
        var configuration = new CommonConfiguration([
            new CommandLineArgsProvider(() => arguments),
            new JsonConfigurationProvider(new FixedPaths(folder.Path))]);
        var oldArguments = configuration.GetSection("args");
        var oldSettings = configuration.GetSection("settings");
        arguments = ["-value=new"];
        File.WriteAllText(file, "broken");

        if (asynchronous) await Assert.ThrowsAnyAsync<JsonException>(() => configuration.RefreshAsync());
        else Assert.ThrowsAny<JsonException>(() => configuration.Refresh());

        Assert.Same(oldArguments, configuration.GetSection("args"));
        Assert.Same(oldSettings, configuration.GetSection("settings"));
        Assert.Equal("old", configuration.Get<string>("-value"));
        Assert.Equal(5, configuration.Get<int>("value", "settings"));
        File.WriteAllText(file, "{\"value\":9}");
        if (asynchronous) await configuration.RefreshAsync();
        else configuration.Refresh();
        Assert.Equal("new", configuration.Get<string>("-value"));
        Assert.Equal(9, configuration.Get<int>("value", "settings"));
        Assert.Equal("old", oldArguments!.Get<string>("-value"));
        Assert.Equal(5, oldSettings!.Get<int>("value"));
    }

    [Fact]
    public void JsonSerializationFailurePreservesFileAndSnapshotAndAllowsRetry()
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "settings.json");
        const string original = "{\"value\":5}";
        File.WriteAllText(file, original);
        var section = new JsonConfigurationSection(file);

        Assert.Throws<ArgumentException>(() => section.Set("value", double.NaN));

        Assert.Equal(original, File.ReadAllText(file));
        Assert.Equal(5, section.Get<int>("value"));
        section.Set("value", 9);
        Assert.Equal(9, new JsonConfigurationSection(file).Get<int>("value"));
    }

    [Fact]
    public void GetOrCreatePersistsWinningJsonNullWithoutWritingDefault()
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "settings.json");
        File.WriteAllText(file, "{\"Settings\":null}");
        var fallback = new Settings { Count = 17 };
        var defaults = Section("default", (nameof(Settings), fallback));
        var configuration = new CommonConfiguration([
            new JsonConfigurationProvider(new FixedPaths(folder.Path)), new TestProvider("low", defaults)]);

        var created = configuration.GetOrCreate<Settings>();

        using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Same(created, configuration.GetOrCreate<Settings>());
        Assert.Same(created, configuration.Get<Settings>());
        Assert.Same(fallback, defaults.Get<Settings>(nameof(Settings)));
        Assert.Equal(17, fallback.Count);
        Assert.Equal(0, new JsonConfigurationSection(file).Get<Settings>(nameof(Settings))!.Count);
    }

    [Fact]
    public void JsonWriteFailurePreservesSnapshotAndAllowsRetry()
    {
        using var folder = new TemporaryFolder();
        var file = Path.Combine(folder.Path, "settings.json");
        const string original = "{\"value\":5}";
        File.WriteAllText(file, original);
        var section = new JsonConfigurationSection(file);

        using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<IOException>(() => section.Set("value", 9));
            Assert.Equal(5, section.Get<int>("value"));
            Assert.Equal(original, File.ReadAllText(file));
        }

        section.Set("value", 9);
        Assert.Equal(9, section.Get<int>("value"));
        Assert.Equal(9, new JsonConfigurationSection(file).Get<int>("value"));
    }

    private static ConfigurationSection Section(string name, params (string key, object? value)[] entries)
        => new(name, entries.ToDictionary(entry => entry.key, entry => entry.value));
    private sealed class TestProvider(string name, params IConfigurationSection[] sections) : IConfigurationProvider
    {
        public string Name => name;
        public IReadOnlyList<IConfigurationSection> GetSections() => sections;
    }
    private sealed class FixedPaths(string path) : IPathResolver
    {
        public string ResolvePath(Paths folder) => path;
    }
    public sealed class Settings { public int Count { get; set; } }
    private sealed class TemporaryFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TrueMoon-Configuration-" + Guid.NewGuid().ToString("N"));
        public TemporaryFolder() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
