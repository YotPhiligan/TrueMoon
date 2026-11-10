using System.Text.Json;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Services;
using Xunit;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace TrueMoon.Enerit.Tests.Configuration;

public sealed class TransportConfigurationMigrationTests
{
    [Fact]
    public async Task StandardTransportSchemaDeclaresAllDataLimitsAndDetachedDefaults()
    {
        var schema = PipesConfigurationSchema.Default;
        Assert.Equal("Enerit:Pipes", schema.Section);
        Assert.Equal(new[] { "CaptureRequestEvents", "MaxBufferedRequestBytes", "MaxConcurrentRequests", "MaxConnections",
            "MaxPendingRequests", "MaxQueuedRequests", "MetricsEnabled", "PropagateTraceContext", "RequestTimeout" },
            schema.Keys.Select(key => key.Path[13..]).Order(StringComparer.Ordinal));
        Assert.All(schema.Keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(schema));
        var snapshot = await profile.LoadAsync();
        var first = schema.Bind(snapshot);
        Assert.Equal((64, 64, 64, 4), (first.MaxPendingRequests, first.MaxQueuedRequests, first.MaxConcurrentRequests, first.MaxConnections));
        Assert.Equal(4 * 1024 * 1024, first.MaxBufferedRequestBytes);
        Assert.Equal(TimeSpan.FromSeconds(50), first.RequestTimeout);
        Assert.True(first.MetricsEnabled); Assert.False(first.PropagateTraceContext); Assert.False(first.CaptureRequestEvents);
        first.MaxPendingRequests = 999;
        Assert.Equal(64, schema.Bind(snapshot).MaxPendingRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredTransportUsesPreparedCopiedLimitsForActualClientAdmission(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(PipesConfigurationSchema.Default);
            builder.AddValues("file", ConfigurationSourcePriority.Files,
                [Entry("Enerit:Pipes:MaxPendingRequests", 3), Entry("Enerit:Pipes:MaxQueuedRequests", 7),
                    Entry("Enerit:Pipes:MaxConcurrentRequests", 5), Entry("Enerit:Pipes:MaxBufferedRequestBytes", 2048L),
                    Entry("Enerit:Pipes:MaxConnections", 2), Entry("Enerit:Pipes:RequestTimeout", "00:00:05"),
                    Entry("Enerit:Pipes:PropagateTraceContext", true), Entry("Enerit:Pipes:CaptureRequestEvents", true),
                    Entry("Enerit:Pipes:MetricsEnabled", false)]);
            builder.AddArguments("args", ["--Enerit:Pipes:MaxPendingRequests=2"]);
        });
        var configured = 0; var endpoints = 0;
        var endpoint = "core75-transport-" + Guid.NewGuid().ToString("N");
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.UseDiagnostics();
            context.UseConfiguration(profile).UseConfiguredPipesServiceTransport(_ => { endpoints++; return endpoint; }, options =>
            { configured++; options.MaxPendingRequests = 1; options.MaxQueuedRequests = 0; });
        }).Build();
        var reader = Required<IConfigurationReader>(app);
        Assert.Equal(0, endpoints);
        Assert.Equal(2, reader.Current.Read<int>("Enerit:Pipes:MaxPendingRequests").Value);
        Assert.Equal("args", reader.Current.Read<int>("Enerit:Pipes:MaxPendingRequests").Origin!.SourceId);
        var copy = reader.Current.GetOptions(PipesConfigurationSchema.Default);
        Assert.Equal((1, 0), (copy.MaxPendingRequests, copy.MaxQueuedRequests));
        Assert.Equal((5, 2048L, 2), (copy.MaxConcurrentRequests, copy.MaxBufferedRequestBytes, copy.MaxConnections));
        Assert.Equal(TimeSpan.FromSeconds(5), copy.RequestTimeout);
        Assert.True(copy.PropagateTraceContext); Assert.True(copy.CaptureRequestEvents); Assert.False(copy.MetricsEnabled);
        copy.MaxPendingRequests = 99; copy.MaxQueuedRequests = 99;
        Assert.Equal(1, reader.Current.GetOptions(PipesConfigurationSchema.Default).MaxPendingRequests);
        var factory = Required<IInvocationClientFactory>(app);
        await using var client = Assert.IsType<PipesInvocationClient<Contract>>(factory.Create<Contract>());
        Assert.Equal("tm_" + endpoint, client.Name);
        Assert.Equal(1, endpoints);
        using var pendingCancellation = new CancellationTokenSource();
        var pending = client.InvokeAsync(1, null, pendingCancellation.Token);
        try
        {
            await UntilAsync(() => client.PendingRequestCount == 1);
            var serialized = 0;
            await Assert.ThrowsAsync<PipesInvocationOverloadedException>(() => client.InvokeAsync(2, _ => serialized++));
            Assert.Equal(0, serialized);
            Assert.Equal(1, client.PendingRequestCount);
            Assert.Equal(0, client.QueuedRequestCount);
        }
        finally { pendingCancellation.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await client.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(client.Completion.IsCompletedSuccessfully);
        Assert.Equal(0, client.PendingRequestCount);
        Assert.Equal(1, configured);
    }

    [Theory]
    [InlineData(false, "MaxConnections", "65")]
    [InlineData(true, "RequestTimeout", "\"00:00:00\"")]
    [InlineData(false, "MaxPendingRequests", "0")]
    [InlineData(true, "MaxQueuedRequests", "-1")]
    [InlineData(false, "MaxConcurrentRequests", "\"private-invalid\"")]
    public void InvalidTransportWinnerFailsBeforeAnyModuleRegistrationOrEndpointFactory(bool microsoft, string field, string json)
    {
        var registered = 0; var endpoints = 0;
        using var document = JsonDocument.Parse(json);
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(PipesConfigurationSchema.Default); builder.AddSchema(ProbeSchema);
            builder.AddValues("bad", ConfigurationSourcePriority.Overrides, [new("Enerit:Pipes:" + field, document.RootElement)]);
        });
        var error = Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile).UseConfiguredPipesServiceTransport(_ => { endpoints++; return "unused"; });
            context.ConfigureModule(ProbeSchema, (_, _) => registered++);
        }).Build());
        Assert.Equal(0, registered); Assert.Equal(0, endpoints);
        Assert.DoesNotContain("private-invalid", error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportStartupReloadRetainsFactoryAndPreparedOptionsWithoutReattachment(bool microsoft)
    {
        var source = new MutableSource([Entry("Enerit:Pipes:MaxPendingRequests", 2)]);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(PipesConfigurationSchema.Default); builder.AddSource("mutable", ConfigurationSourcePriority.Files, source); });
        var callbacks = 0;
        await using var app = Builder(microsoft).Setup(context =>
        {
            context.UseDiagnostics(); context.UseConfiguration(profile).UseConfiguredPipesServiceTransport(configure: _ => callbacks++);
        }).Build();
        var factory = Required<IInvocationClientFactory>(app);
        var reader = Required<IConfigurationReader>(app); var previous = reader.Current;
        source.Entries = [Entry("Enerit:Pipes:MaxPendingRequests", 3)];
        var update = await Required<IConfigurationReloader>(app).ReloadAsync();
        Assert.False(update.Published); Assert.True(update.RestartRequired); Assert.Equal(1, update.Generation);
        Assert.Equal(new[] { "Enerit:Pipes:MaxPendingRequests" }, update.ChangedKeys);
        Assert.Same(previous, reader.Current); Assert.Same(factory, Required<IInvocationClientFactory>(app));
        Assert.Equal(2, reader.Current.GetOptions(PipesConfigurationSchema.Default).MaxPendingRequests);
        Assert.Equal(2, callbacks);
    }

    [Fact]
    public void DuplicateConfiguredTransportAttachmentIsRejectedWithoutBuildingResources()
    {
        var context = new AppConfigurationContext();
        context.UseConfiguredPipesServiceTransport();
        Assert.Throws<InvalidOperationException>(() => context.UseConfiguredPipesServiceTransport());
        Assert.Empty(context.GetServicesRegistrations());
    }

    private static IAppBuilder Builder(bool microsoft) => new AppBuilder(microsoft ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder());
    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));
    private static ConfigurationEntry Entry(string key, object? value) => new(key, JsonSerializer.SerializeToElement(value));
    private static readonly IConfigurationSchema<Probe> ProbeSchema = ConfigurationSchemas.Create("Probe", () => new Probe(), value => value with { }, _ => { },
        fields => fields.Field("Value", value => value.Value, (value, next) => value.Value = next));
    private sealed record Probe { public int Value { get; set; } }
    private sealed class Contract;
    private sealed class MutableSource(ConfigurationEntry[] entries) : IConfigurationSource
    {
        internal ConfigurationEntry[] Entries = entries;
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(Entries.ToArray()); }
    }
    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(5, timeout.Token);
    }
}
