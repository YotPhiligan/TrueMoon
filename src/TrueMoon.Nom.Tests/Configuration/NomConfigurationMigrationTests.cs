using System.Text.Json;
using TrueMoon.Configuration;
using TrueMoon.Nom.Hosting;
using Xunit;

namespace TrueMoon.Nom.Tests.Configuration;

public sealed class NomConfigurationMigrationTests
{
    [Fact]
    public async Task StandardNomSchemaExposesOnlyAdmissionSettingsAndDetachedDefaults()
    {
        var schema = NomConfigurationSchema.Default;
        Assert.Equal("Nom", schema.Section);
        Assert.Equal(new[] { "Nom:BatchBytes", "Nom:BatchRows", "Nom:DocumentBytes", "Nom:DomainIndexBytes", "Nom:DrainTimeout",
            "Nom:MaximumOpenStores", "Nom:QueueBytes", "Nom:QueueCount" }, schema.Keys.Select(key => key.Path).Order(StringComparer.Ordinal));
        Assert.All(schema.Keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(schema));
        var snapshot = await profile.LoadAsync();
        var first = schema.Bind(snapshot);
        Assert.Equal((4, 32, 256), (first.MaximumOpenStores, first.QueueCount, first.BatchRows));
        Assert.Equal((16 * 1024 * 1024, 4 * 1024 * 1024, 128 * 1024), (first.QueueBytes, first.BatchBytes, first.DocumentBytes));
        Assert.Equal(64L * 1024 * 1024, first.DomainIndexBytes); Assert.Equal(TimeSpan.FromSeconds(10), first.DrainTimeout);
        first.MaximumOpenStores = 99;
        Assert.Equal(4, schema.Bind(snapshot).MaximumOpenStores);
        Assert.DoesNotContain(schema.Keys, key => key.Path.Contains("Root") || key.Path.Contains("ProjectId") || key.Path.Contains("StoreId"));
    }

    [Fact]
    public async Task ConfiguredNomAppliesCopiedSourceAndCallbackLimitsToTheActualStoreFactory()
    {
        using var directory = new NomTestDirectory();
        var firstArguments = directory.Arguments("first"); var secondArguments = directory.Arguments("second");
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(NomConfigurationSchema.Default);
            builder.AddValues("file", ConfigurationSourcePriority.Files,
                [Entry("Nom:MaximumOpenStores", 3), Entry("Nom:QueueCount", 7), Entry("Nom:DocumentBytes", 512)]);
            builder.AddArguments("args", ["--Nom:MaximumOpenStores=2"]);
        });
        var callbacks = 0;
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredNom(options =>
        { callbacks++; options.MaximumOpenStores = 1; options.QueueCount = 8; }));
        var reader = Required<IConfigurationReader>(app);
        Assert.Equal(2, reader.Current.Read<int>("Nom:MaximumOpenStores").Value);
        Assert.Equal("args", reader.Current.Read<int>("Nom:MaximumOpenStores").Origin!.SourceId);
        var copy = reader.Current.GetOptions(NomConfigurationSchema.Default);
        Assert.Equal((1, 8, 512), (copy.MaximumOpenStores, copy.QueueCount, copy.DocumentBytes));
        copy.MaximumOpenStores = 4;
        var host = NomTestDirectory.Host(app); var factory = NomTestDirectory.Factory(app);
        Assert.False(factory is IDisposable); Assert.False(factory is IAsyncDisposable);
        await app.StartAsync();
        Assert.Equal(0, host.ActiveStoreCount); Assert.False(Directory.Exists(directory.Root));
        await using (var first = factory.Create(firstArguments))
        {
            Assert.Equal(1, host.ActiveStoreCount);
            Assert.Throws<InvalidOperationException>(() => factory.Create(secondArguments));
            Assert.False(Directory.Exists(secondArguments.Root));
        }
        Assert.Equal(0, host.ActiveStoreCount);
        await using (var second = factory.Create(secondArguments)) Assert.Equal(1, host.ActiveStoreCount);
        await app.StopAsync();
        Assert.True(host.IsReleased); Assert.Equal(0, host.ActiveStoreCount);
        Assert.Equal(1, reader.Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores);
        Assert.Equal(1, callbacks);
    }

    [Theory]
    [InlineData("MaximumOpenStores", "0")]
    [InlineData("MaximumOpenStores", "5")]
    [InlineData("QueueCount", "33")]
    [InlineData("DomainIndexBytes", "268435457")]
    [InlineData("DrainTimeout", "\"00:00:00\"")]
    [InlineData("DrainTimeout", "\"00:00:10.0000001\"")]
    [InlineData("DocumentBytes", "\"private-invalid\"")]
    public void InvalidNomWinnerRejectsBeforeModuleRegistrationAndStoreIo(string field, string json)
    {
        using var directory = new NomTestDirectory();
        using var document = JsonDocument.Parse(json);
        var registered = 0;
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(NomConfigurationSchema.Default); builder.AddSchema(ProbeSchema);
            builder.AddValues("bad", ConfigurationSourcePriority.Overrides, [new("Nom:" + field, document.RootElement)]);
        });
        var error = Assert.Throws<ConfigurationException>(() => App.Build(context =>
        {
            context.UseConfiguration(profile).UseConfiguredNom();
            context.ConfigureModule(ProbeSchema, (_, _) => registered++);
        }));
        Assert.Equal(0, registered); Assert.False(Directory.Exists(directory.Root));
        Assert.DoesNotContain("private-invalid", error.ToString());
    }

    [Fact]
    public async Task NomStartupReloadKeepsCurrentFactoryAndItsOriginalCapacity()
    {
        using var directory = new NomTestDirectory();
        var source = new MutableSource([Entry("Nom:MaximumOpenStores", 1)]);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(NomConfigurationSchema.Default); builder.AddSource("mutable", ConfigurationSourcePriority.Files, source); });
        await using var app = App.Build(context => context.UseConfiguration(profile).UseConfiguredNom());
        var reader = Required<IConfigurationReader>(app); var previous = reader.Current;
        var host = NomTestDirectory.Host(app); var factory = NomTestDirectory.Factory(app);
        source.Entries = [Entry("Nom:MaximumOpenStores", 2)];
        var update = await Required<IConfigurationReloader>(app).ReloadAsync();
        Assert.False(update.Published); Assert.True(update.RestartRequired); Assert.Equal(1, update.Generation);
        Assert.Equal(new[] { "Nom:MaximumOpenStores" }, update.ChangedKeys);
        Assert.Same(previous, reader.Current); Assert.Same(host, NomTestDirectory.Host(app)); Assert.Same(factory, NomTestDirectory.Factory(app));
        await using var store = factory.Create(directory.Arguments("one"));
        Assert.Throws<InvalidOperationException>(() => factory.Create(directory.Arguments("two")));
        Assert.Equal(1, reader.Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores);
    }

    [Fact]
    public async Task ConfiguredAppsKeepIndependentNomHostsAndReaderCopies()
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(NomConfigurationSchema.Default));
        await using var first = App.Build(context => context.UseConfiguration(profile).UseConfiguredNom(options => options.MaximumOpenStores = 1));
        await using var second = App.Build(context => context.UseConfiguration(profile).UseConfiguredNom(options => options.MaximumOpenStores = 2));
        Assert.NotSame(NomTestDirectory.Host(first), NomTestDirectory.Host(second));
        Assert.NotSame(NomTestDirectory.Factory(first), NomTestDirectory.Factory(second));
        Assert.Equal(1, Required<IConfigurationReader>(first).Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores);
        Assert.Equal(2, Required<IConfigurationReader>(second).Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores);
        await first.DisposeAsync();
        Assert.Equal(0, NomTestDirectory.Host(second).ActiveStoreCount);
        Assert.Equal(2, Required<IConfigurationReader>(second).Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores);
    }

    [Fact]
    public void LegacyAndConfiguredNomCannotAttachTwoOwners()
    {
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(NomConfigurationSchema.Default));
        Assert.Throws<InvalidOperationException>(() => App.Build(context => context.UseNom().UseConfiguration(profile).UseConfiguredNom()));
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
