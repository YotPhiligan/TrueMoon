using System.Text.Json;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Services;
using Xunit;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace TrueMoon.Tests.Configuration;

public sealed class ConfigurationReloadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReloadPublishesOneAtomicGenerationAndRetainsOldOptionsServicesAndSnapshots(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        var startupOptions = fixture.RegisteredOptions!;
        var service = fixture.App.Services.GetService(typeof(ReloadService));
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Title", "next"), ConfigurationReloadFixture.Entry("feature:count", "31"));

        var update = await fixture.Reload.ReloadAsync();

        Assert.True(update.Published);
        Assert.False(update.RestartRequired);
        Assert.Equal(2, update.Generation);
        Assert.Equal(new[] { "Feature:Count", "Feature:Title" }, update.ChangedKeys);
        Assert.Equal(31, fixture.Reader.Current.Read<int>("Feature:Count").Value);
        Assert.Equal("next", fixture.Reader.Current.GetOptions(fixture.Schema).Title);
        Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
        Assert.Equal(20, startupOptions.Count);
        Assert.Equal(1, previous.Generation);
        Assert.Same(service, fixture.App.Services.GetService(typeof(ReloadService)));
        Assert.Equal(1, fixture.Registrations);
        Assert.Equal(1, fixture.Factories);
        Assert.Equal(2, fixture.Configurations);
        Assert.Equal(2, fixture.Validations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EquivalentTypedValuesAreNoOpDespiteDifferentJsonRepresentation(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", "20"));
        var update = await fixture.Reload.ReloadAsync();
        Assert.False(update.Published);
        Assert.False(update.RestartRequired);
        Assert.Equal(1, update.Generation);
        Assert.Empty(update.ChangedKeys);
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(2, fixture.Configurations);
        Assert.Equal(2, fixture.Validations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProvenanceChangePublishesEvenWhenTypedValueAndStateStayEqual(bool microsoft)
    {
        var higher = new ReloadMutableSource([]);
        await using var fixture = new ConfigurationReloadFixture(microsoft, (builder, source) =>
        {
            builder.AddSource("mutable", ConfigurationSourcePriority.Files, source);
            builder.AddSource("higher", ConfigurationSourcePriority.Environment, higher);
        });
        higher.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 20));
        var update = await fixture.Reload.ReloadAsync();
        Assert.True(update.Published);
        Assert.Equal(2, update.Generation);
        Assert.Equal(new[] { "Feature:Count" }, update.ChangedKeys);
        Assert.Equal(20, fixture.Reader.Current.Read<int>("Feature:Count").Value);
        Assert.Equal("higher", fixture.Reader.Current.Read<int>("Feature:Count").Origin!.SourceId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupOnlyChangeRejectsWholeCandidateIncludingReloadableChanges(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 35), ConfigurationReloadFixture.Entry("Feature:Startup", "different"));
        var update = await fixture.Reload.ReloadAsync();
        Assert.False(update.Published);
        Assert.True(update.RestartRequired);
        Assert.Equal(1, update.Generation);
        Assert.Contains("Feature:Startup", update.ChangedKeys);
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal("fixed", fixture.Reader.Current.GetOptions(fixture.Schema).Startup);
        Assert.Equal(1, fixture.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupOnlyProvenanceChangeRequiresRestartEvenWithEqualValue(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 20), ConfigurationReloadFixture.Entry("Feature:Startup", "fixed"));
        var update = await fixture.Reload.ReloadAsync();
        Assert.False(update.Published);
        Assert.True(update.RestartRequired);
        Assert.Equal(new[] { "Feature:Startup" }, update.ChangedKeys);
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(ConfigurationSourcePriority.Defaults, previous.Read<string>("Feature:Startup").Origin!.Priority);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateUnregisteredSchemaFailureRollsBackWholeCandidateAndRetryPublishesBoth(bool microsoft)
    {
        var other = ConfigurationSchemas.Create("Other", () => new ReloadOptions(), value => value with { Tags = value.Tags.ToArray() },
            value => { if (value.Count < 0) throw new ArgumentOutOfRangeException(nameof(value.Count)); },
            fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count, new(Change: ConfigurationChangeMode.Reloadable)));
        await using var fixture = new ConfigurationReloadFixture(microsoft, (builder, source) =>
        {
            builder.AddSchema(other);
            builder.AddSource("mutable", ConfigurationSourcePriority.Files, source);
        });
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 35), ConfigurationReloadFixture.Entry("Other:Count", -1));
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Reload.ReloadAsync().AsTask());
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
        Assert.Equal(7, previous.GetOptions(other).Count);
        Assert.Equal(1, previous.Generation);
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 35), ConfigurationReloadFixture.Entry("Other:Count", 8));
        var update = await fixture.Reload.ReloadAsync();
        Assert.True(update.Published);
        Assert.Equal(2, update.Generation);
        Assert.Equal(new[] { "Feature:Count", "Other:Count" }, update.ChangedKeys);
        Assert.Equal(35, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal(8, fixture.Reader.Current.GetOptions(other).Count);
        Assert.Equal(1, fixture.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PureConfigureFailureIsRedactedAndRollbackRemainsRetryable(bool microsoft)
    {
        var fail = false;
        await using var fixture = new ConfigurationReloadFixture(microsoft, configure: _ =>
        {
            if (fail) throw new OperationCanceledException("configure-secret");
        });
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 34));
        fail = true;
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Reload.ReloadAsync().AsTask());
        Assert.All(error.Issues, issue => Assert.DoesNotContain("configure-secret", issue.Message));
        Assert.DoesNotContain("configure-secret", error.ToString());
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
        fail = false;
        Assert.True((await fixture.Reload.ReloadAsync()).Published);
        Assert.Equal(34, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal(1, fixture.Registrations);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InvalidRawOrSemanticCandidateNeverReplacesCurrentSnapshot(bool microsoft, bool semantic)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", semantic ? (object)1200 : "secret-invalid-count"),
            ConfigurationReloadFixture.Entry("Feature:Title", "should-not-publish"));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Reload.ReloadAsync().AsTask());
        Assert.All(error.Issues, issue => Assert.DoesNotContain("secret-invalid-count", issue.Message));
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(1, fixture.Reader.Current.Generation);
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Equal("default", fixture.Reader.Current.GetOptions(fixture.Schema).Title);
        Assert.Equal(1, fixture.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledSourceAwaitRetainsCurrentAndSubsequentReloadCanSucceed(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        var gate = fixture.Source.BlockNextRead();
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 28));
        using var cancellation = new CancellationTokenSource();
        var pending = fixture.Reload.ReloadAsync(cancellation.Token).AsTask();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Same(previous, fixture.Reader.Current);
            Assert.Equal(1, previous.Generation);
        }
        finally { gate.Release.TrySetResult(); }
        var update = await fixture.Reload.ReloadAsync();
        Assert.True(update.Published);
        Assert.Equal(2, update.Generation);
        Assert.Equal(28, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReloadAliasesAreBorrowedAndStopClosesAdmissionWithoutInvalidatingCurrent(bool microsoft)
    {
        var fixture = new ConfigurationReloadFixture(microsoft);
        try
        {
            Assert.False(fixture.Reload is IDisposable);
            Assert.False(fixture.Writer is IDisposable);
            Assert.False(fixture.Changes is IDisposable);
            await fixture.App.StopAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Reload.ReloadAsync().AsTask());
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Writer.WriteAsync("mutable", []).AsTask());
            Assert.Throws<InvalidOperationException>(() => fixture.Changes.Subscribe(_ => { }));
            Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            var previous = fixture.Reader.Current;
            await fixture.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => fixture.Reader.Current);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Reload.ReloadAsync().AsTask());
            Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
        }
        finally { await fixture.DisposeAsync(); }
    }
}

internal sealed class ConfigurationReloadFixture : IAsyncDisposable
{
    internal IApp App { get; }
    internal IConfigurationSchema<ReloadOptions> Schema { get; }
    internal IConfigurationProfile Profile { get; }
    internal ReloadMutableSource Source { get; }
    internal IConfigurationReader Reader { get; }
    internal IConfigurationReloader Reload { get; }
    internal IConfigurationWriter Writer { get; }
    internal IConfigurationChanges Changes { get; }
    internal ReloadOptions? RegisteredOptions;
    internal int Registrations;
    internal int Configurations;
    internal int Validations;
    internal int Factories;

    internal ConfigurationReloadFixture(bool microsoft,
        Action<IConfigurationProfileBuilder, ReloadMutableSource>? sources = null,
        Func<IConfigurationProfile, IConfigurationProfile>? wrap = null,
        Action<IAppConfigurationContext>? setup = null,
        Action<ReloadOptions>? configure = null)
    {
        Source = new([Entry("Feature:Count", 20)]);
        Schema = ConfigurationSchemas.Create("Feature", () => new ReloadOptions(), value => value with { Tags = value.Tags.ToArray() },
            value => { Validations++; if (value.Count is < 0 or > 1000) throw new ArgumentOutOfRangeException(nameof(value.Count)); }, fields =>
            {
                fields.Field("Count", value => value.Count, (value, count) => value.Count = count, new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Title", value => value.Title, (value, title) => value.Title = title, new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Note", value => value.Note, (value, note) => value.Note = note, new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Tags", value => value.Tags, (value, tags) => value.Tags = tags, new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Startup", value => value.Startup, (value, startup) => value.Startup = startup);
            });
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema);
            if (sources is null) builder.AddSource("mutable", ConfigurationSourcePriority.Files, Source);
            else sources(builder, Source);
        });
        Profile = wrap is null ? profile : wrap(profile);
        IServiceResolverBuilder resolver = microsoft ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder();
        App = new AppBuilder(resolver).Setup(context =>
        {
            context.UseConfiguration(Profile);
            context.ConfigureModule(Schema, (app, options) =>
            {
                Registrations++; RegisteredOptions = options;
                app.Services(services => services.Singleton<ReloadService>(_ => { Factories++; return new(); }));
            }, options => { Configurations++; configure?.Invoke(options); });
            setup?.Invoke(context);
        }).Build();
        Reader = Required<IConfigurationReader>(); Reload = Required<IConfigurationReloader>();
        Writer = Required<IConfigurationWriter>(); Changes = Required<IConfigurationChanges>();
    }
    internal T Required<T>() => Assert.IsAssignableFrom<T>(App.Services.GetService(typeof(T)));
    internal static ConfigurationEntry Entry(string path, object? value) => new(path, JsonSerializer.SerializeToElement(value));
    public ValueTask DisposeAsync() => App.DisposeAsync();
}

internal sealed record ReloadOptions
{
    public int Count { get; set; } = 7;
    public string Title { get; set; } = "default";
    public string? Note { get; set; }
    public string[] Tags { get; set; } = ["default"];
    public string Startup { get; set; } = "fixed";
}
internal sealed class ReloadService { }
internal sealed class ReloadGate
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
internal sealed class ReloadMutableSource(ConfigurationEntry[] initial) : IConfigurationMutableSource
{
    private ConfigurationEntry[] _entries = initial.ToArray();
    private ReloadGate? _readGate;
    private ReloadGate? _writeGate;
    internal int Reads;
    internal int Writes;
    internal Exception? WriteError;
    internal Action? AfterWrite;
    internal void Replace(params ConfigurationEntry[] entries) => Volatile.Write(ref _entries, entries.ToArray());
    internal ReloadGate BlockNextRead() { var gate = new ReloadGate(); _readGate = gate; return gate; }
    internal ReloadGate BlockNextWrite() { var gate = new ReloadGate(); _writeGate = gate; return gate; }
    public async ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Reads);
        var gate = Interlocked.Exchange(ref _readGate, null);
        if (gate is not null) { gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        return Volatile.Read(ref _entries).ToArray();
    }
    public async ValueTask WriteAsync(IReadOnlyList<ConfigurationKey> keys, IReadOnlyList<ConfigurationEdit> edits, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Writes);
        var gate = Interlocked.Exchange(ref _writeGate, null);
        if (gate is not null) { gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        if (WriteError is { } error) throw error;
        var entries = Volatile.Read(ref _entries).ToDictionary(entry => entry.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var edit in edits)
        {
            if (edit.Value is { } value) entries[edit.Path] = new(edit.Path, value);
            else entries.Remove(edit.Path);
        }
        Volatile.Write(ref _entries, entries.Values.ToArray());
        AfterWrite?.Invoke();
    }
}
