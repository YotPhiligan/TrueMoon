using System.Text.Json;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class ConfigurationWriteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonPatchDistinguishesExplicitNullAndDeleteAndNeverReloadsImplicitly(bool microsoft)
    {
        using var directory = new ConfigurationWriteDirectory();
        var file = directory.Write("settings.json", "{\"Feature\":{\"Count\":20,\"Note\":\"present\",\"Tags\":[\"kept\"]}}");
        await using var fixture = new ConfigurationReloadFixture(microsoft, (builder, _) => builder.AddJsonFile("file", file));
        var previous = fixture.Reader.Current;
        await fixture.Writer.WriteAsync("file",
        [
            new("feature:count", JsonSerializer.SerializeToElement(41)),
            new("Feature:Note", JsonSerializer.SerializeToElement<object?>(null))
        ]);
        using (var document = JsonDocument.Parse(File.ReadAllText(file)))
        {
            var section = document.RootElement.GetProperty("Feature");
            Assert.Equal(41, section.GetProperty("Count").GetInt32());
            Assert.Equal(JsonValueKind.Null, section.GetProperty("Note").ValueKind);
            Assert.Equal("kept", section.GetProperty("Tags")[0].GetString());
        }
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
        Assert.Equal("present", previous.GetOptions(fixture.Schema).Note);
        var update = await fixture.Reload.ReloadAsync();
        Assert.True(update.Published);
        Assert.Equal(2, update.Generation);
        Assert.Equal(41, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        Assert.Null(fixture.Reader.Current.GetOptions(fixture.Schema).Note);
        Assert.Equal("file", fixture.Reader.Current.Read<string?>("Feature:Note").Origin!.SourceId);

        await fixture.Writer.WriteAsync("file", [new("Feature:Note", null)]);
        using (var document = JsonDocument.Parse(File.ReadAllText(file)))
            Assert.False(document.RootElement.GetProperty("Feature").TryGetProperty("Note", out _));
        Assert.Equal(2, fixture.Reader.Current.Generation);
        var deleted = await fixture.Reload.ReloadAsync();
        Assert.True(deleted.Published);
        Assert.Equal(3, deleted.Generation);
        Assert.Equal(new[] { "Feature:Note" }, deleted.ChangedKeys);
        Assert.Equal(ConfigurationSourcePriority.Defaults, fixture.Reader.Current.Read<string?>("Feature:Note").Origin!.Priority);
    }

    [Theory]
    [InlineData(false, "unknown")]
    [InlineData(true, "unknown")]
    [InlineData(false, "$defaults")]
    [InlineData(true, "$overrides")]
    [InlineData(false, "readonly")]
    [InlineData(true, "readonly")]
    public async Task UnknownAndReadonlySourcesFailWithoutCallingAnyWriter(bool microsoft, string sourceId)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft, (builder, source) =>
            builder.AddSource("readonly", ConfigurationSourcePriority.Files, new ReadOnlyReloadSource(source)));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync(sourceId,
            [new("Feature:Count", JsonSerializer.SerializeToElement(31))]).AsTask());
        Assert.Contains(error.Issues, issue => issue.Code == (sourceId == "unknown" ? "UnknownSource" : "ReadOnlySource"));
        Assert.Equal(0, fixture.Source.Writes);
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomProfileWithoutWriterCapabilityIsReadonly(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft, wrap: inner => new ReadOnlyReloadProfile(inner));
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync("mutable",
            [new("Feature:Count", JsonSerializer.SerializeToElement(31))]).AsTask());
        Assert.Contains(error.Issues, issue => issue.Code == "ReadOnlyProfile");
        Assert.Equal(0, fixture.Source.Writes);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("wrong-type")]
    [InlineData("null")]
    [InlineData("undefined")]
    [InlineData("duplicate")]
    public async Task EveryPatchEditIsValidatedBeforeAnySourceIo(string failure)
    {
        await using var fixture = new ConfigurationReloadFixture(false);
        var edits = new List<ConfigurationEdit> { new("Feature:Title", JsonSerializer.SerializeToElement("must-not-apply")) };
        switch (failure)
        {
            case "unknown": edits.Add(new("Feature:Unknown", JsonSerializer.SerializeToElement(4))); break;
            case "wrong-type": edits.Add(new("Feature:Count", JsonSerializer.SerializeToElement("secret-invalid"))); break;
            case "null": edits.Add(new("Feature:Count", JsonSerializer.SerializeToElement<object?>(null))); break;
            case "undefined": edits.Add(new("Feature:Count", default(JsonElement))); break;
            case "duplicate": edits.Add(new("feature:TITLE", JsonSerializer.SerializeToElement("duplicate"))); break;
        }
        var previous = fixture.Reader.Current;
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync("mutable", edits).AsTask());
        Assert.All(error.Issues, issue => Assert.DoesNotContain("secret-invalid", issue.Message));
        Assert.Equal(0, fixture.Source.Writes);
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal("default", fixture.Reader.Current.GetOptions(fixture.Schema).Title);
        Assert.False((await fixture.Reload.ReloadAsync()).Published);
    }

    [Fact]
    public async Task FailedValidationLeavesRealJsonUnchanged()
    {
        using var directory = new ConfigurationWriteDirectory();
        var original = "{\"Feature\":{\"Count\":20,\"Title\":\"original\"}}";
        var file = directory.Write("settings.json", original);
        await using var fixture = new ConfigurationReloadFixture(false, (builder, _) => builder.AddJsonFile("file", file));
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync("file",
            [new("Feature:Title", JsonSerializer.SerializeToElement("changed")), new("Feature:Count", JsonSerializer.SerializeToElement("invalid"))]).AsTask());
        Assert.Equal(original, File.ReadAllText(file));
        Assert.Equal("original", fixture.Reader.Current.GetOptions(fixture.Schema).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAndReloadShareOneGateAndReaderStaysReadableDuringIo(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var gate = fixture.Source.BlockNextWrite();
        var write = fixture.Writer.WriteAsync("mutable", [new("Feature:Count", JsonSerializer.SerializeToElement(39))]).AsTask();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var reads = fixture.Source.Reads;
            var reload = fixture.Reload.ReloadAsync().AsTask();
            Assert.False(reload.IsCompleted);
            Assert.Equal(reads, fixture.Source.Reads);
            Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            gate.Release.TrySetResult();
            await write;
            var update = await reload;
            Assert.True(update.Published);
            Assert.Equal(2, update.Generation);
            Assert.Equal(39, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            Assert.Equal(1, fixture.Source.Writes);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedWriteWaitsForReloadAndCopiesCallerEdits(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        fixture.Source.Replace(ConfigurationReloadFixture.Entry("Feature:Count", 36));
        var gate = fixture.Source.BlockNextRead();
        var reload = fixture.Reload.ReloadAsync().AsTask();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var edits = new[] { new ConfigurationEdit("Feature:Count", JsonSerializer.SerializeToElement(48)) };
            var write = fixture.Writer.WriteAsync("mutable", edits).AsTask();
            edits[0] = new("Feature:Count", JsonSerializer.SerializeToElement(99));
            Assert.False(write.IsCompleted);
            Assert.Equal(0, fixture.Source.Writes);
            gate.Release.TrySetResult();
            Assert.True((await reload).Published);
            await write;
            Assert.Equal(36, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
            var next = await fixture.Reload.ReloadAsync();
            Assert.True(next.Published);
            Assert.Equal(3, next.Generation);
            Assert.Equal(48, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledOrFailedWriteNeverPublishesAndErrorsHideValues(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        var previous = fixture.Reader.Current;
        var gate = fixture.Source.BlockNextWrite();
        using var cancellation = new CancellationTokenSource();
        var pending = fixture.Writer.WriteAsync("mutable", [new("Feature:Count", JsonSerializer.SerializeToElement(51))], cancellation.Token).AsTask();
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Same(previous, fixture.Reader.Current);
        }
        finally { gate.Release.TrySetResult(); }
        fixture.Source.WriteError = new IOException("writer-secret-credential");
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync("mutable",
            [new("Feature:Count", JsonSerializer.SerializeToElement(52))]).AsTask());
        Assert.All(error.Issues, issue => Assert.DoesNotContain("writer-secret-credential", issue.Message));
        Assert.DoesNotContain("writer-secret-credential", error.ToString());
        Assert.Same(previous, fixture.Reader.Current);
        Assert.Equal(1, fixture.Reader.Current.Generation);
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        fixture.Source.WriteError = null;
        await fixture.Writer.WriteAsync("mutable", [new("Feature:Count", JsonSerializer.SerializeToElement(53))]);
        Assert.Same(previous, fixture.Reader.Current);
        Assert.True((await fixture.Reload.ReloadAsync()).Published);
        Assert.Equal(53, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
    }

    [Fact]
    public async Task RealJsonIoFailureRetainsSnapshotAndRetryPublishesOnlyAfterExplicitReload()
    {
        using var directory = new ConfigurationWriteDirectory();
        const string original = "{\"Feature\":{\"Count\":20,\"Title\":\"retained\"}}";
        var file = directory.Write("settings.json", original);
        await using var fixture = new ConfigurationReloadFixture(false, (builder, _) => builder.AddJsonFile("file", file));
        var previous = fixture.Reader.Current;
        File.Delete(file);
        Directory.CreateDirectory(file);
        try
        {
            await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Writer.WriteAsync("file",
                [new("Feature:Count", JsonSerializer.SerializeToElement(47))]).AsTask());
            Assert.True(Directory.Exists(file));
            Assert.Same(previous, fixture.Reader.Current);
            Assert.Equal(20, previous.GetOptions(fixture.Schema).Count);
            Assert.Equal("retained", previous.GetOptions(fixture.Schema).Title);
        }
        finally
        {
            Directory.Delete(file);
            File.WriteAllText(file, original);
        }
        await fixture.Writer.WriteAsync("file", [new("Feature:Count", JsonSerializer.SerializeToElement(47))]);
        Assert.Same(previous, fixture.Reader.Current);
        using (var document = JsonDocument.Parse(File.ReadAllText(file)))
        {
            Assert.Equal(47, document.RootElement.GetProperty("Feature").GetProperty("Count").GetInt32());
            Assert.Equal("retained", document.RootElement.GetProperty("Feature").GetProperty("Title").GetString());
        }
        Assert.True((await fixture.Reload.ReloadAsync()).Published);
        Assert.Equal(2, fixture.Reader.Current.Generation);
        Assert.Equal(47, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterSuccessfulSourceCommitDoesNotReportWriteAsCancelled(bool microsoft)
    {
        await using var fixture = new ConfigurationReloadFixture(microsoft);
        using var cancellation = new CancellationTokenSource();
        fixture.Source.AfterWrite = cancellation.Cancel;
        await fixture.Writer.WriteAsync("mutable", [new("Feature:Count", JsonSerializer.SerializeToElement(57))], cancellation.Token);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(1, fixture.Source.Writes);
        Assert.Equal(20, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
        var published = await fixture.Reload.ReloadAsync();
        Assert.True(published.Published);
        Assert.Equal(57, fixture.Reader.Current.GetOptions(fixture.Schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonPatchUsesOneCaseInsensitiveParentForDifferentlyCasedSchemaPaths(bool microsoft)
    {
        using var directory = new ConfigurationWriteDirectory();
        var file = directory.Write("settings.json", "{\"Live\":{\"Inner\":{\"A\":1,\"B\":2}}}");
        var schema = ConfigurationSchemas.Create("Live", () => new ParentCaseOptions(), value => value with { }, _ => { }, fields =>
        {
            fields.Field("Inner:A", value => value.A, (value, number) => value.A = number, new(Change: ConfigurationChangeMode.Reloadable));
            fields.Field("inner:B", value => value.B, (value, number) => value.B = number, new(Change: ConfigurationChangeMode.Reloadable));
        });
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.AddJsonFile("file", file); });
        var resolver = microsoft ? (TrueMoon.Services.IServiceResolverBuilder)new TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder()
            : new TrueMoon.Cobalt.CobaltServiceResolverBuilder();
        await using var app = new AppBuilder(resolver).Setup(context => context.UseConfiguration(profile)).Build();
        var writer = Assert.IsAssignableFrom<IConfigurationWriter>(app.Services.GetService(typeof(IConfigurationWriter)));
        var reader = Assert.IsAssignableFrom<IConfigurationReader>(app.Services.GetService(typeof(IConfigurationReader)));
        var reload = Assert.IsAssignableFrom<IConfigurationReloader>(app.Services.GetService(typeof(IConfigurationReloader)));
        await writer.WriteAsync("file", [new("live:inner:a", JsonSerializer.SerializeToElement(11)), new("LIVE:INNER:B", JsonSerializer.SerializeToElement(12))]);
        using (var document = JsonDocument.Parse(File.ReadAllText(file)))
        {
            var parent = Assert.Single(document.RootElement.GetProperty("Live").EnumerateObject());
            Assert.Equal("inner", parent.Name, ignoreCase: true);
            Assert.Equal(11, parent.Value.GetProperty("A").GetInt32());
            Assert.Equal(12, parent.Value.GetProperty("B").GetInt32());
        }
        Assert.Equal(1, reader.Current.GetOptions(schema).A);
        Assert.True((await reload.ReloadAsync()).Published);
        Assert.Equal(11, reader.Current.GetOptions(schema).A);
        Assert.Equal(12, reader.Current.GetOptions(schema).B);
    }

    private sealed record ParentCaseOptions
    {
        public int A { get; set; } = 1;
        public int B { get; set; } = 2;
    }
}

internal sealed class ReadOnlyReloadSource(IConfigurationSource source) : IConfigurationSource
{
    public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        => source.ReadAsync(keys, cancellationToken);
}
internal sealed class ReadOnlyReloadProfile(IConfigurationProfile profile) : IConfigurationProfile, IConfigurationProfileSchemas
{
    public void VisitSchemas(IConfigurationSchemaVisitor visitor) => ((IConfigurationProfileSchemas)profile).VisitSchemas(visitor);
    public ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default) => profile.LoadAsync(cancellationToken);
}
internal sealed class ConfigurationWriteDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tm-config-write-" + Guid.NewGuid().ToString("N"));
    internal ConfigurationWriteDirectory() => Directory.CreateDirectory(Path);
    internal string Write(string name, string contents)
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, contents);
        return file;
    }
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
