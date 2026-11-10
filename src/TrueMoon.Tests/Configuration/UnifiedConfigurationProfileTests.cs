using System.Globalization;
using System.Text.Json;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Tests.Configuration;

public sealed class UnifiedConfigurationProfileTests
{
    public static IEnumerable<object[]> PriorityPairs()
    {
        var levels = Enum.GetValues<ConfigurationSourcePriority>();
        for (var low = 0; low < levels.Length; low++)
        for (var high = low + 1; high < levels.Length; high++)
        foreach (var reverse in new[] { false, true })
            yield return [levels[low], levels[high], reverse];
    }

    [Theory]
    [MemberData(nameof(PriorityPairs))]
    public async Task EverySourcePairUsesDeclaredPriorityInsteadOfRegistrationOrder(
        ConfigurationSourcePriority low, ConfigurationSourcePriority high, bool reverse)
    {
        var schema = Schema();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            void Lower() => builder.AddSource("lower", low, new EntriesSource(Entry("Editor:Count", "41")));
            void Higher() => builder.AddSource("higher", high, new EntriesSource(Entry("editor:count", "53")));
            if (reverse) { Higher(); Lower(); } else { Lower(); Higher(); }
        });

        var snapshot = await profile.LoadAsync();
        var value = snapshot.Read<int>("EDITOR:COUNT");

        Assert.Equal(53, value.Value);
        Assert.Equal(ConfigurationReadState.Value, value.State);
        Assert.Equal("higher", value.Origin!.SourceId);
        Assert.Equal(high, value.Origin.Priority);
        Assert.Equal(1, snapshot.Generation);
    }

    [Theory]
    [InlineData(ConfigurationSourcePriority.Defaults)]
    [InlineData(ConfigurationSourcePriority.Files)]
    [InlineData(ConfigurationSourcePriority.Environment)]
    [InlineData(ConfigurationSourcePriority.Arguments)]
    [InlineData(ConfigurationSourcePriority.Overrides)]
    public async Task LastSourceAtSamePriorityWins(ConfigurationSourcePriority level)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("first", level, new EntriesSource(Entry("Editor:Count", "21")));
            builder.AddSource("last", level, new EntriesSource(Entry("Editor:Count", "34")));
        });
        var value = (await profile.LoadAsync()).Read<int>("Editor:Count");
        Assert.Equal(34, value.Value);
        Assert.Equal("last", value.Origin!.SourceId);
    }

    [Fact]
    public async Task DefaultsMissingNullEmptyAndWrongReadTypeRemainDistinct()
    {
        var schema = Schema();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.Override("Editor:Name", "");
        });
        var snapshot = await profile.LoadAsync();

        Assert.Equal(7, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal(ConfigurationSourcePriority.Defaults, snapshot.Read<int>("Editor:Count").Origin!.Priority);
        Assert.Equal(ConfigurationReadState.Null, snapshot.Read<int?>("Editor:Optional").State);
        Assert.Null(snapshot.Read<int?>("Editor:Optional").Value);
        Assert.Equal(ConfigurationReadState.Value, snapshot.Read<string?>("Editor:Name").State);
        Assert.Equal("", snapshot.Read<string?>("Editor:Name").Value);
        var absent = snapshot.Read<int>("Unknown:Count");
        Assert.Equal(ConfigurationReadState.Missing, absent.State);
        Assert.Null(absent.Origin);
        Assert.Equal(ConfigurationReadState.Missing, snapshot.Read<int>("Count").State);
        Assert.Throws<ConfigurationException>(() => snapshot.Read<long>("Editor:Count"));
        Assert.Throws<InvalidOperationException>(() => snapshot.GetOptions(schema));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidWinningValueNeverFallsBackOrLeaksSensitiveContent(bool explicitNull)
    {
        const string secret = "unprintable-credential-47";
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("valid-low", ConfigurationSourcePriority.Files,
                new EntriesSource(Entry("Editor:Count", "19")));
            builder.AddSource("invalid-high", ConfigurationSourcePriority.Arguments,
                new EntriesSource(Entry("Editor:Count", explicitNull ? null : secret)));
        });

        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());

        Assert.Contains(error.Issues, issue => issue.Path == "Editor:Count" && issue.SourceId == "invalid-high");
        Assert.DoesNotContain(secret, error.ToString());
        Assert.All(error.Issues, issue => Assert.DoesNotContain(secret, issue.Message));
    }

    [Fact]
    public async Task OverridesAreCopiedAndLastOverrideWins()
    {
        var names = new[] { "first", "second" };
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.Override("Editor:Count", 12);
            builder.Override("editor:count", 27);
            builder.Override("Editor:Names", names);
        });
        names[0] = "changed-after-build";
        var snapshot = await profile.LoadAsync();
        Assert.Equal(27, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal(ConfigurationSourcePriority.Overrides, snapshot.Read<int>("Editor:Count").Origin!.Priority);
        Assert.Equal(new[] { "first", "second" }, snapshot.Read<string[]>("Editor:Names").Value);
    }

    [Fact]
    public async Task SnapshotDetachesSourceJsonAndEveryReturnedCollection()
    {
        var document = JsonDocument.Parse("[\"one\",\"two\"]");
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("array", ConfigurationSourcePriority.Files,
                new EntriesSource(new ConfigurationEntry("Editor:Names", document.RootElement)));
        });
        var snapshot = await profile.LoadAsync();
        document.Dispose();
        var names = snapshot.Read<string[]>("Editor:Names").Value!;
        names[0] = "consumer-mutation";
        Assert.Equal(new[] { "one", "two" }, snapshot.Read<string[]>("Editor:Names").Value);
        var bound = Schema().Bind(snapshot);
        bound.Names[1] = "bound-mutation";
        Assert.Equal(new[] { "one", "two" }, snapshot.Read<string[]>("Editor:Names").Value);
    }

    [Fact]
    public async Task IndependentLoadsAndFailureCannotMutateEarlierSnapshots()
    {
        var source = new MutableSource { Value = "11" };
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("mutable", ConfigurationSourcePriority.Environment, source);
        });
        var first = await profile.LoadAsync();
        source.Value = "29";
        var second = await profile.LoadAsync();
        source.Value = "invalid";
        await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Equal(11, first.Read<int>("Editor:Count").Value);
        Assert.Equal(29, second.Read<int>("Editor:Count").Value);
        Assert.Equal(1, first.Generation);
        Assert.Equal(1, second.Generation);
    }

    [Fact]
    public async Task ConversionIsInvariantAndSupportsEnumNullableGuidAndTimeSpan()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            var profile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(Schema());
                builder.AddSource("text", ConfigurationSourcePriority.Environment, new EntriesSource(
                    Entry("Editor:Scale", "1.25"), Entry("Editor:Optional", "42"),
                    Entry("Editor:Mode", "friday"), Entry("Editor:Id", "d6ad16cf-4d41-4e73-b7d4-60c44d6c6ba2"),
                    Entry("Editor:Delay", "00:00:03")));
            });
            var snapshot = await profile.LoadAsync();
            Assert.Equal(1.25m, snapshot.Read<decimal>("Editor:Scale").Value);
            Assert.Equal(42, snapshot.Read<int?>("Editor:Optional").Value);
            Assert.Equal(DayOfWeek.Friday, snapshot.Read<DayOfWeek>("Editor:Mode").Value);
            Assert.Equal(Guid.Parse("d6ad16cf-4d41-4e73-b7d4-60c44d6c6ba2"), snapshot.Read<Guid>("Editor:Id").Value);
            Assert.Equal(TimeSpan.FromSeconds(3), snapshot.Read<TimeSpan>("Editor:Delay").Value);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("Enabled", "not-boolean")]
    [InlineData("Mode", "not-an-enum")]
    [InlineData("Mode", "999")]
    [InlineData("Names", "a,b")]
    [InlineData("Count", "2147483648")]
    [InlineData("Count", "-2147483649")]
    public async Task StandardCodecsRejectInvalidBooleanEnumCollectionAndOverflow(string field, string input)
    {
        var path = "Editor:" + field;
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("invalid-typed", ConfigurationSourcePriority.Files, new EntriesSource(Entry(path, input)));
        });
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.Path == path && issue.SourceId == "invalid-typed");
    }

    [Fact]
    public async Task StandardCollectionsConvertEachElementAndDetachArrayListAndNullableViews()
    {
        var schema = ConfigurationSchemas.Create("Arrays", () => new CollectionOptions(),
            value => value with { Numbers = value.Numbers.ToArray(), List = value.List.ToList(), Nullable = value.Nullable.ToArray() },
            _ => { }, fields =>
            {
                fields.Field("Numbers", value => value.Numbers, (value, items) => value.Numbers = items);
                fields.Field("List", value => value.List, (value, items) => value.List = items);
                fields.Field("Nullable", value => value.Nullable, (value, items) => value.Nullable = items);
            });
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.AddSource("arrays", ConfigurationSourcePriority.Files, new EntriesSource(
                Entry("Arrays:Numbers", new object[] { "4", 5 }),
                Entry("Arrays:List", new object[] { 6, "7" }),
                Entry("Arrays:Nullable", new object?[] { null, "8" })));
        });
        var snapshot = await profile.LoadAsync();
        Assert.Equal(new[] { 4, 5 }, snapshot.Read<int[]>("Arrays:Numbers").Value);
        var list = snapshot.Read<List<int>>("Arrays:List").Value!;
        Assert.Equal(new[] { 6, 7 }, list);
        list.Add(99);
        Assert.Equal(new[] { 6, 7 }, snapshot.Read<List<int>>("Arrays:List").Value);
        Assert.Equal(new int?[] { null, 8 }, snapshot.Read<IReadOnlyList<int?>>("Arrays:Nullable").Value);
        Assert.Equal(new[] { 4, 5 }, schema.Bind(snapshot).Numbers);
    }

    [Fact]
    public void UnsupportedComplexValueRequiresAnExplicitCodec()
    {
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationSchemas.Create("Server", () => new PortOptions(), value => value with { }, _ => { },
            fields => fields.Field("Port", value => value.Port, (value, port) => value.Port = port)));
    }

    [Fact]
    public async Task EmptyProfileReturnsAnEmptyGenerationWithoutAppOrProviders()
    {
        var snapshot = await ConfigurationProfiles.Create(_ => { }).LoadAsync();
        Assert.Equal(1, snapshot.Generation);
        var value = snapshot.Read<string>("App:Name");
        Assert.Equal(ConfigurationReadState.Missing, value.State);
        Assert.Null(value.Origin);
    }

    [Fact]
    public async Task ExplicitCustomCodecConvertsDefaultsInputsAndOverrides()
    {
        var codec = new HexCodec();
        var schema = ConfigurationSchemas.Create("Server", () => new PortOptions(), value => value with { },
            _ => { }, fields => fields.Field("Port", value => value.Port, (value, port) => value.Port = port, codec: codec));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.AddSource("wire", ConfigurationSourcePriority.Environment, new EntriesSource(Entry("Server:Port", "0x2a")));
        });
        var snapshot = await profile.LoadAsync();
        Assert.Equal(new Port(42), snapshot.Read<Port>("Server:Port").Value);
        Assert.Equal(new Port(42), schema.Bind(snapshot).Port);
        var metadata = Assert.Single(schema.Keys);
        Assert.Equal(typeof(Port), metadata.Codec!.ValueType);
        Assert.Equal(new Port(42), Assert.IsType<Port>(metadata.Codec.Read(JsonSerializer.SerializeToElement("0x2a"))));
        var explicitProfile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.Override("Server:Port", new Port(255), codec);
        });
        Assert.Equal(new Port(255), (await explicitProfile.LoadAsync()).Read<Port>("Server:Port").Value);
        Assert.Equal("0x50", Assert.Single(schema.GetDefaults()).Value.GetString());
    }

    [Fact]
    public async Task SchemaDefaultsAreFrozenAndValidationRunsAfterConfigureCallback()
    {
        var defaults = new Settings { Name = null, Count = 7, Names = ["initial"] };
        var validations = 0;
        var schema = ConfigurationSchemas.Create("Editor", () => defaults,
            value => value with { Names = value.Names.ToArray() },
            value => { validations++; if (value.Count < 1) throw new ArgumentOutOfRangeException(nameof(value.Count)); },
            fields =>
            {
                fields.Field("Name", value => value.Name, (value, name) => value.Name = name, new(Required: true));
                fields.Field("Count", value => value.Count, (value, count) => value.Count = count);
                fields.Field("Names", value => value.Names, (value, names) => value.Names = names);
            });
        defaults.Count = 99;
        defaults.Names[0] = "external-mutation";
        var profile = ConfigurationProfiles.Create(builder => builder.AddSchema(schema));
        var snapshot = await profile.LoadAsync();
        Assert.Equal(0, validations);
        Assert.Equal(7, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal(new[] { "initial" }, snapshot.Read<string[]>("Editor:Names").Value);
        var bound = schema.Bind(snapshot, value => value.Name = "supplied-by-callback");
        Assert.Equal("supplied-by-callback", bound.Name);
        Assert.Equal(1, validations);
        Assert.Throws<ConfigurationException>(() => schema.Bind(snapshot));
        Assert.Throws<ConfigurationException>(() => schema.Bind(snapshot, value => { value.Name = "valid"; value.Count = -1; }));
    }

    [Fact]
    public void BuildersCannotBeUsedAfterTheirConfigurationCallbackCompletes()
    {
        IConfigurationSchemaBuilder<Settings>? schemaBuilder = null;
        var schema = ConfigurationSchemas.Create("Editor", () => new Settings(), value => value with { }, _ => { }, fields =>
        {
            schemaBuilder = fields;
            fields.Field("Count", value => value.Count, (value, count) => value.Count = count);
        });
        Assert.Throws<InvalidOperationException>(() => schemaBuilder!.Field("Other", value => value.Count, (value, count) => value.Count = count));
        IConfigurationProfileBuilder? profileBuilder = null;
        _ = ConfigurationProfiles.Create(builder => { profileBuilder = builder; builder.AddSchema(schema); });
        Assert.Throws<InvalidOperationException>(() => profileBuilder!.Override("Editor:Count", 9));
    }

    [Theory]
    [InlineData("Editor::Name")]
    [InlineData(":Editor")]
    [InlineData("Editor:")]
    [InlineData("")]
    public void MalformedSectionPathsAreRejectedBeforeLoading(string section)
        => Assert.ThrowsAny<ArgumentException>(() => ConfigurationSchemas.Create(section, () => new Settings(),
            value => value with { }, _ => { }, fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count)));

    [Fact]
    public void ProfileRejectsRepeatedSchemaSourceAndCrossSchemaAliases()
    {
        var schema = Schema();
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.AddSchema(schema); }));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.AddSource("same", ConfigurationSourcePriority.Files, new EntriesSource());
            builder.AddSource("same", ConfigurationSourcePriority.Arguments, new EntriesSource());
        }));
        var second = ConfigurationSchemas.Create("Other", () => new Settings(), value => value with { }, _ => { },
            fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count, new(Aliases: ["--COUNT"])));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.AddSchema(second); }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfileRejectsBranchLeafOverlapAcrossSchemasInEitherOrder(bool reverse)
    {
        var branch = ConfigurationSchemas.Create("Editor", () => new Settings { Name = "dark" }, value => value with { }, _ => { },
            fields => fields.Field("Theme", value => value.Name, (value, theme) => value.Name = theme));
        var leaf = ConfigurationSchemas.Create("Editor:Theme", () => new Settings { Name = "white" }, value => value with { }, _ => { },
            fields => fields.Field("Color", value => value.Name, (value, color) => value.Name = color));
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationProfiles.Create(builder =>
        {
            if (reverse) { builder.AddSchema(leaf); builder.AddSchema(branch); }
            else { builder.AddSchema(branch); builder.AddSchema(leaf); }
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceRejectsUnknownOrDuplicateNormalizedKeys(bool duplicate)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("bad-entries", ConfigurationSourcePriority.Files,
                duplicate ? new EntriesSource(Entry("Editor:Count", 3), Entry("editor:COUNT", 4))
                    : new EntriesSource(Entry("Other:Count", 4)));
        });
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.SourceId == "bad-entries");
    }

    [Fact]
    public async Task CancelledLoadDoesNotReadSources()
    {
        var source = new MutableSource { Value = "12" };
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("source", ConfigurationSourcePriority.Files, source);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => profile.LoadAsync(cancellation.Token).AsTask());
        Assert.Equal(0, source.Reads);
    }

    [Fact]
    public async Task SourceCannotForgeDiagnosticIdentityOrLeakItsExceptionPayload()
    {
        const string secret = "raw-source-credential-483";
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("actual-source", ConfigurationSourcePriority.Files, new LeakySource(secret));
        });
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.All(error.Issues, issue => Assert.Equal("actual-source", issue.SourceId));
        Assert.DoesNotContain(secret, error.ToString());
        Assert.All(error.Issues, issue =>
        {
            Assert.DoesNotContain(secret, issue.Code);
            Assert.DoesNotContain(secret, issue.Path ?? "");
            Assert.DoesNotContain(secret, issue.Message);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomCodecFailuresCannotLeakRawValuesThroughConfigurationExceptions(bool cancellation)
    {
        const string secret = "raw-codec-credential-827";
        var schema = ConfigurationSchemas.Create("Server", () => new PortOptions(), value => value with { }, _ => { },
            fields => fields.Field("Port", value => value.Port, (value, port) => value.Port = port, codec: new LeakyCodec(secret, cancellation)));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.AddSource("wire", ConfigurationSourcePriority.Environment, new EntriesSource(Entry("Server:Port", secret)));
        });
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => profile.LoadAsync().AsTask());
        Assert.Contains(error.Issues, issue => issue.Path == "Server:Port" && issue.SourceId == "wire");
        Assert.DoesNotContain(secret, error.ToString());
        Assert.All(error.Issues, issue => Assert.DoesNotContain(secret, issue.Message));
    }

    [Fact]
    public async Task InvalidSourceDeclarationDoesNotReserveItsIdentifier()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            Assert.ThrowsAny<ArgumentException>(() => builder.AddSource("recover", (ConfigurationSourcePriority)999, new EntriesSource()));
            builder.AddSource("recover", ConfigurationSourcePriority.Files, new EntriesSource(Entry("Editor:Count", 18)));
        });
        var value = (await profile.LoadAsync()).Read<int>("Editor:Count");
        Assert.Equal(18, value.Value);
        Assert.Equal("recover", value.Origin!.SourceId);
    }

    [Fact]
    public async Task InvalidSchemaDeclarationDoesNotReserveItsSectionOrAddDefaults()
    {
        var validOther = ConfigurationSchemas.Create("Other", () => new Settings { Count = 5 }, value => value with { }, _ => { },
            fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            Assert.ThrowsAny<ArgumentException>(() => builder.AddSchema(new ConflictingSchema()));
            builder.AddSchema(validOther);
        });
        var snapshot = await profile.LoadAsync();
        Assert.Equal(7, snapshot.Read<int>("Editor:Count").Value);
        Assert.Equal(5, snapshot.Read<int>("Other:Count").Value);
    }

    [Fact]
    public async Task CancellationAfterSourceAwaitDoesNotReturnPartialOrChangeOldSnapshot()
    {
        var source = new GatedSource();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(Schema());
            builder.AddSource("async", ConfigurationSourcePriority.Environment, source);
        });
        var previous = await profile.LoadAsync();
        source.Block = true;
        source.Value = 33;
        using var cancellation = new CancellationTokenSource();
        var pending = profile.LoadAsync(cancellation.Token).AsTask();
        try
        {
            await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            source.Release.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.Equal(12, previous.Read<int>("Editor:Count").Value);
            Assert.Equal(1, previous.Generation);
            Assert.Equal(2, source.Reads);
        }
        finally { source.Release.TrySetResult(); }
    }

    [Fact]
    public void CodecMetadataMustMatchTheDeclaredFieldType()
    {
        var key = new ConfigurationKey("Editor:Name", typeof(string), new()) { Codec = new WrongTypeCodec() };
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationCommandLine.Parse([], [key]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequiredValidationRejectsFinalNullEmptyOrWhitespace(string? finalName)
    {
        var schema = ConfigurationSchemas.Create("Editor", () => new Settings(), value => value with { }, _ => { },
            fields => fields.Field("Name", value => value.Name, (value, name) => value.Name = name, new(Required: true)));
        var snapshot = await ConfigurationProfiles.Create(builder => builder.AddSchema(schema)).LoadAsync();
        var error = Assert.Throws<ConfigurationException>(() => schema.Bind(snapshot, value => value.Name = finalName));
        Assert.Contains(error.Issues, issue => issue.Path == "Editor:Name");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigureAndValidatorExceptionsAreValueSafe(bool validation)
    {
        const string secret = "semantic-credential-403";
        var schema = ConfigurationSchemas.Create("Editor", () => new Settings(), value => value with { },
            _ => { if (validation) throw new ConfigurationException([new("User", null, null, secret)]); },
            fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count));
        var snapshot = await ConfigurationProfiles.Create(builder => builder.AddSchema(schema)).LoadAsync();
        var error = Assert.Throws<ConfigurationException>(() => schema.Bind(snapshot, _ =>
        {
            if (!validation) throw new ConfigurationException([new("User", null, null, secret)]);
        }));
        Assert.NotEmpty(error.Issues);
        Assert.All(error.Issues, issue => Assert.DoesNotContain(secret, issue.Message));
        Assert.DoesNotContain(secret, error.ToString());
    }

    [Fact]
    public void SchemaRejectsIdentityCopyAndDisposableOptionsBeforeCreatingResources()
    {
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationSchemas.Create("Editor", () => new Settings(),
            value => value, _ => { }, fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count)));
        var creates = 0;
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationSchemas.Create("Resource", () =>
        {
            creates++;
            return new DisposableOptions();
        }, _ => new DisposableOptions(), _ => { }, _ => { }));
        Assert.Equal(0, creates);
    }

    private static IConfigurationSchema<Settings> Schema() => ConfigurationSchemas.Create("Editor", () => new Settings(),
        value => value with { Names = value.Names.ToArray() }, _ => { }, fields =>
        {
            fields.Field("Count", value => value.Count, (value, count) => value.Count = count, new(Aliases: ["--count"], Sensitive: true));
            fields.Field("Name", value => value.Name, (value, name) => value.Name = name);
            fields.Field("Optional", value => value.Optional, (value, number) => value.Optional = number);
            fields.Field("Names", value => value.Names, (value, names) => value.Names = names);
            fields.Field("Scale", value => value.Scale, (value, scale) => value.Scale = scale);
            fields.Field("Mode", value => value.Mode, (value, mode) => value.Mode = mode);
            fields.Field("Id", value => value.Id, (value, id) => value.Id = id);
            fields.Field("Delay", value => value.Delay, (value, delay) => value.Delay = delay);
            fields.Field("Enabled", value => value.Enabled, (value, enabled) => value.Enabled = enabled);
        });

    private static ConfigurationEntry Entry(string path, object? value) => new(path, JsonSerializer.SerializeToElement(value));

    private sealed class EntriesSource(params ConfigurationEntry[] entries) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(entries);
    }
    private sealed class MutableSource : IConfigurationSource
    {
        internal string Value { get; set; } = "";
        internal int Reads { get; private set; }
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>([Entry("Editor:Count", Value)]);
        }
    }
    private sealed class LeakySource(string secret) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
            => throw new ConfigurationException([new(secret, secret, "forged-source", secret)]);
    }
    private sealed class ConflictingSchema : IConfigurationSchema<Settings>
    {
        public string Section => "Other";
        public IReadOnlyList<ConfigurationKey> Keys => [new("Editor:Count", typeof(int), new())];
        public IReadOnlyList<ConfigurationEntry> GetDefaults() => [Entry("Editor:Count", 999)];
        public Settings Bind(IConfigurationSnapshot snapshot, Action<Settings>? configure = null) => throw new NotSupportedException();
        public Settings Copy(Settings value) => throw new NotSupportedException();
    }
    private sealed class GatedSource : IConfigurationSource
    {
        internal int Value { get; set; } = 12;
        internal bool Block { get; set; }
        internal int Reads { get; private set; }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (Block)
            {
                Started.TrySetResult();
                // Deliberately return after cancellation to verify the profile checks
                // cancellation after an awaited, non-cooperative source completes.
                await Release.Task.ConfigureAwait(false);
            }
            return [Entry("Editor:Count", Value)];
        }
    }
    private sealed class WrongTypeCodec : IConfigurationValueCodec
    {
        public Type ValueType => typeof(int);
        public object? Read(JsonElement value) => value.GetInt32();
        public JsonElement Write(object? value) => JsonSerializer.SerializeToElement(value);
    }
    private sealed record Settings
    {
        public int Count { get; set; } = 7;
        public string? Name { get; set; }
        public int? Optional { get; set; }
        public string[] Names { get; set; } = [];
        public decimal Scale { get; set; } = 1.5m;
        public DayOfWeek Mode { get; set; } = DayOfWeek.Monday;
        public Guid Id { get; set; }
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
        public bool Enabled { get; set; }
    }
    private sealed record CollectionOptions
    {
        public int[] Numbers { get; set; } = [1];
        public List<int> List { get; set; } = [2];
        public IReadOnlyList<int?> Nullable { get; set; } = [null, 3];
    }
    private sealed class DisposableOptions : IDisposable
    {
        public void Dispose() { }
    }
    private readonly record struct Port(int Value);
    private sealed record PortOptions { public Port Port { get; set; } = new(80); }
    private sealed class HexCodec : IConfigurationValueCodec<Port>
    {
        public Port Read(JsonElement value)
            => new(int.Parse(value.GetString()![2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        public JsonElement Write(Port value) => JsonSerializer.SerializeToElement("0x" + value.Value.ToString("x", CultureInfo.InvariantCulture));
    }
    private sealed class LeakyCodec(string secret, bool cancellation) : IConfigurationValueCodec<Port>
    {
        public Port Read(JsonElement value) => cancellation
            ? throw new OperationCanceledException(secret)
            : throw new FormatException(secret);
        public JsonElement Write(Port value) => JsonSerializer.SerializeToElement(value.Value);
    }
}
