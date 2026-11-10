using System.Text.Json;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Services;
using Xunit;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace TrueMoon.Tests.Configuration;

public sealed class AppConfigurationStageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllSetupAndLegacyActionsPrecedeBindingAndAllBindingsPrecedeRegistration(bool microsoft)
    {
        var events = new List<string>();
        var first = Schema("One", value => events.Add("validate-one"));
        var second = Schema("Two", value => events.Add("validate-two"));
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(first); builder.AddSchema(second);
            builder.AddSource("input", ConfigurationSourcePriority.Arguments,
                new CallbackSource(() => events.Add("load"), new("One:Count", JsonSerializer.SerializeToElement("12")),
                    new("Two:Count", JsonSerializer.SerializeToElement("15"))));
        });
        var declarations = new List<int>();
        int[]? preparedDeclarations = null;
        var builder = Builder(microsoft).Setup(context =>
        {
            events.Add("setup-one");
            context.UseConfiguration(profile);
            context.Configuration(_ => events.Add("legacy-one"));
            context.Services(_ => events.Add("services-root"));
            context.ConfigureModule(first, (app, options) =>
            {
                events.Add("register-one");
                Assert.Equal(13, options.Count);
                declarations.Add(1);
                app.Configuration(_ => { events.Add("post-legacy-one"); preparedDeclarations = declarations.ToArray(); });
                app.Services(_ => events.Add("services-one"));
            }, options => { events.Add("configure-one"); options.Count++; });
            context.ConfigureModule(second, (app, options) =>
            {
                events.Add("register-two");
                Assert.Equal(17, options.Count);
                declarations.Add(2);
                app.Configuration(_ => events.Add("post-legacy-two"));
                app.Services(_ => events.Add("services-two"));
            }, options => { events.Add("configure-two"); options.Count += 2; });
        }).Setup(context =>
        {
            events.Add("setup-two");
            context.Configuration(_ => events.Add("legacy-two"));
        });

        using var app = builder.Build();

        Assert.Equal(new[]
        {
            "setup-one", "setup-two", "legacy-one", "legacy-two", "load", "configure-one", "validate-one",
            "configure-two", "validate-two", "register-one", "register-two", "post-legacy-one", "post-legacy-two",
            "services-root", "services-one", "services-two"
        }, events);
        Assert.Equal(new[] { 1, 2 }, preparedDeclarations);
        var snapshot = Reader(app).Current;
        Assert.Equal(12, snapshot.Read<int>("One:Count").Value);
        Assert.Equal(13, snapshot.GetOptions(first).Count);
        Assert.Equal(17, snapshot.GetOptions(second).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyConfigurationCallbackCanDeclareProfileAndBinding(bool microsoft)
    {
        var schema = Schema();
        var configured = 0;
        using var app = Builder(microsoft).Setup(context => context.Configuration(legacy =>
        {
            legacy.Set("legacy-marker", "kept");
            context.UseConfiguration(Profile(schema, 23));
            context.ConfigureModule(schema, (_, options) => configured = options.Count);
        })).Build();
        Assert.Equal(23, configured);
        Assert.Equal(23, Reader(app).Current.GetOptions(schema).Count);
        Assert.Equal("kept", app.Configuration.Get<string>("legacy-marker"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryCatalogSchemaIsValidatedAndPreparedEvenWithoutModuleRegistration(bool microsoft)
    {
        var validations = 0;
        var schema = Schema(validate: _ => validations++);
        using var app = Builder(microsoft).Setup(context => context.UseConfiguration(Profile(schema, 31))).Build();
        Assert.Equal(1, validations);
        var snapshot = Reader(app).Current;
        var first = snapshot.GetOptions(schema);
        first.Count = 999;
        first.Names[0] = "mutated";
        Assert.Equal(31, snapshot.GetOptions(schema).Count);
        Assert.Equal(new[] { "default" }, snapshot.GetOptions(schema).Names);
        Assert.Equal(1, validations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomProfileCatalogPreservesSchemaTypesAndBindsUnregisteredSchemas(bool microsoft)
    {
        var options = Schema();
        var names = ConfigurationSchemas.Create("App", () => new NameOptions { Name = "catalog-name" }, value => value with { }, _ => { },
            fields => fields.Field("Name", value => value.Name, (value, name) => value.Name = name));
        var inner = ConfigurationProfiles.Create(builder => { builder.AddSchema(options); builder.AddSchema(names); builder.Override("Module:Count", 24); });
        var profile = new CatalogProfile(inner, visitor => ((IConfigurationProfileSchemas)inner).VisitSchemas(visitor));
        using var app = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(options, (_, _) => { });
        }).Build();
        var snapshot = Reader(app).Current;
        Assert.Equal(24, snapshot.GetOptions(options).Count);
        Assert.Equal("catalog-name", snapshot.GetOptions(names).Name);
        Assert.Equal(1, profile.Loads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyProfileStillCreatesAReaderWithoutInventingOptions(bool microsoft)
    {
        using var app = Builder(microsoft).Setup(context => context.UseConfiguration(ConfigurationProfiles.Create(_ => { }))).Build();
        var snapshot = Reader(app).Current;
        Assert.Equal(1, snapshot.Generation);
        Assert.Equal(ConfigurationReadState.Missing, snapshot.Read<string>("App:Name").State);
        Assert.ThrowsAny<ArgumentException>(() => snapshot.GetOptions(Schema()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReaderReturnsPreparedCopiesWithoutReplayingCallbacksOrChangingRawProvenance(bool microsoft)
    {
        var configurations = 0;
        var validations = 0;
        Options? registered = null;
        var schema = Schema(validate: _ => validations++);
        using var app = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(Profile(schema, 18));
            context.ConfigureModule(schema, (_, options) => registered = options, options =>
            {
                configurations++;
                options.Count = 44;
                options.Names = ["configured"];
            });
        }).Build();
        var reader = Reader(app);
        Assert.False(reader is IDisposable);
        Assert.False(reader is IAsyncDisposable);
        var snapshot = reader.Current;
        Assert.Equal(18, snapshot.Read<int>("Module:Count").Value);
        Assert.Equal(ConfigurationSourcePriority.Overrides, snapshot.Read<int>("Module:Count").Origin!.Priority);
        Assert.Equal(44, snapshot.GetOptions(schema).Count);
        Assert.Equal(1, configurations);
        Assert.Equal(1, validations);
        registered!.Count = 71;
        registered.Names[0] = "register-mutation";
        var first = snapshot.GetOptions(schema);
        Assert.Equal(44, first.Count);
        Assert.Equal(new[] { "configured" }, first.Names);
        first.Count = 81; first.Names[0] = "consumer-mutation";
        Assert.Equal(44, snapshot.GetOptions(schema).Count);
        Assert.Equal(new[] { "configured" }, snapshot.GetOptions(schema).Names);
        Assert.Equal(1, configurations);
        Assert.Equal(1, validations);
        Assert.ThrowsAny<ArgumentException>(() => snapshot.GetOptions(Schema()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyConfigurationIsSeparateAndStillControlsAppName(bool microsoft)
    {
        var schema = ConfigurationSchemas.Create("App", () => new NameOptions(), value => value with { }, _ => { },
            fields => fields.Field("Name", value => value.Name, (value, name) => value.Name = name));
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.Override("App:Name", "unified-name"); });
        using var app = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.Configuration(legacy => legacy.Set("appName", "legacy-name"));
        }).Build();
        Assert.Equal("legacy-name", app.Name);
        Assert.Equal("legacy-name", app.Configuration.GetName());
        Assert.Same(app.Configuration, app.Services.GetService(typeof(IConfiguration)));
        var snapshot = Reader(app).Current;
        Assert.Equal("unified-name", snapshot.Read<string>("App:Name").Value);
        Assert.Equal("unified-name", snapshot.GetOptions(schema).Name);
        app.Configuration.Set("appName", "legacy-changed");
        Assert.Equal("legacy-changed", app.Name);
        Assert.Equal("unified-name", snapshot.GetOptions(schema).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUnboundSchemaStopsEveryRegisterAndResolverBeforeFactories(bool microsoft)
    {
        var valid = Schema("Valid");
        var invalid = Schema("Invalid", value => { if (value.Count < 1) throw new ArgumentOutOfRangeException(nameof(value.Count)); });
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(valid); builder.AddSchema(invalid); builder.Override("Invalid:Count", 0); });
        var tracking = new TrackingResolver(Resolver(microsoft));
        var registrations = 0;
        var factories = 0;
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.Services(services => services.Singleton<OwnedProbe>(_ => { factories++; return new OwnedProbe(new()); }));
            context.ConfigureModule(valid, (_, _) => registrations++);
        });
        Assert.Throws<ConfigurationException>(() => builder.Build());
        Assert.Equal(0, tracking.Builds);
        Assert.Equal(0, registrations);
        Assert.Equal(0, factories);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedLaterConfigureCallbackPreventsEarlierModuleRegistration(bool microsoft)
    {
        var first = Schema("First"); var second = Schema("Second");
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(first); builder.AddSchema(second); });
        var registrations = 0;
        var tracking = new TrackingResolver(Resolver(microsoft));
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(first, (_, _) => registrations++);
            context.ConfigureModule(second, (_, _) => registrations++, _ => throw new FormatException("credential-hidden"));
        });
        var error = Assert.Throws<ConfigurationException>(() => builder.Build());
        Assert.Equal(0, registrations);
        Assert.Equal(0, tracking.Builds);
        Assert.All(error.Issues, issue => Assert.DoesNotContain("credential-hidden", issue.Message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllRegistrationCopiesArePreparedBeforeAnyRegisterCallback(bool microsoft)
    {
        const string secret = "copy-failure-credential";
        var first = Schema("First");
        var copies = 0;
        var second = new CustomSchema("Second", value =>
        {
            if (++copies == 2) throw new FormatException(secret);
            return value with { Names = value.Names.ToArray() };
        });
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(first); builder.AddSchema(second); });
        var registrations = 0;
        var tracking = new TrackingResolver(Resolver(microsoft));
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(first, (_, _) => registrations++);
            context.ConfigureModule(second, (_, _) => registrations++);
        });
        var error = Assert.Throws<ConfigurationException>(() => builder.Build());
        Assert.Equal(2, copies);
        Assert.Equal(0, registrations);
        Assert.Equal(0, tracking.Builds);
        Assert.All(error.Issues, issue => Assert.DoesNotContain(secret, issue.Message));
        Assert.DoesNotContain(secret, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReaderCopyFailureIsRedactedWithoutInvalidatingPreparedSnapshot(bool microsoft)
    {
        const string secret = "reader-copy-credential";
        var failCopy = false;
        var schema = new CustomSchema("Module", value => failCopy
            ? throw new OperationCanceledException(secret)
            : value with { Names = value.Names.ToArray() });
        using var app = Builder(microsoft).Setup(context => context.UseConfiguration(Profile(schema, 29))).Build();
        var reader = Reader(app);
        var snapshot = reader.Current;
        failCopy = true;
        var error = Assert.Throws<ConfigurationException>(() => snapshot.GetOptions(schema));
        Assert.All(error.Issues, issue => Assert.DoesNotContain(secret, issue.Message));
        Assert.DoesNotContain(secret, error.ToString());
        Assert.Equal(29, snapshot.Read<int>("Module:Count").Value);
        Assert.Same(snapshot, reader.Current);
        failCopy = false;
        Assert.Equal(29, snapshot.GetOptions(schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateCustomCatalogFailsBeforeLoadingOrResolverCreation(bool microsoft)
    {
        var schema = Schema();
        var inner = Profile(schema);
        var profile = new CatalogProfile(inner, visitor => { visitor.Visit(schema); visitor.Visit(schema); });
        var tracking = new TrackingResolver(Resolver(microsoft));
        var builder = new AppBuilder(tracking).Setup(context => context.UseConfiguration(profile));
        Assert.ThrowsAny<ArgumentException>(() => builder.Build());
        Assert.Equal(0, profile.Loads);
        Assert.Equal(0, tracking.Builds);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(2L)]
    public void CustomProfileInvalidInitialGenerationFailsBeforeBindRegisterOrResolver(long generation)
    {
        var validations = 0;
        var schema = Schema(validate: _ => validations++);
        var inner = Profile(schema);
        var profile = new CatalogProfile(inner, visitor => visitor.Visit(schema), generation);
        var tracking = new TrackingResolver(new CobaltServiceResolverBuilder());
        var configurations = 0; var registrations = 0;
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(schema, (_, _) => registrations++, _ => configurations++);
        });
        Assert.Throws<ConfigurationException>(() => builder.Build());
        Assert.Equal(1, profile.Loads);
        Assert.Equal(0, validations);
        Assert.Equal(0, configurations);
        Assert.Equal(0, registrations);
        Assert.Equal(0, tracking.Builds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawLoadFailurePreventsBindingAndRegistrationBeforeResolverCreation(bool microsoft)
    {
        var schema = Schema();
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.Override("Module:Count", "raw-credential-hidden");
        });
        var tracking = new TrackingResolver(Resolver(microsoft));
        var configured = 0; var registered = 0;
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(schema, (_, _) => registered++, _ => configured++);
        });
        var error = Assert.Throws<ConfigurationException>(() => builder.Build());
        Assert.Equal(0, configured);
        Assert.Equal(0, registered);
        Assert.Equal(0, tracking.Builds);
        Assert.All(error.Issues, issue => Assert.DoesNotContain("raw-credential-hidden", issue.Message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoAppsAndRepeatedBuilderBuildHaveIndependentStagesAndReaderLifetimes(bool microsoft)
    {
        var schema = Schema();
        var callbacks = 0;
        var builder = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(Profile(schema, 21));
            context.ConfigureModule(schema, (_, _) => { }, options => { callbacks++; options.Names = ["private"]; });
        });
        using var first = builder.Build();
        using var second = builder.Build();
        var firstReader = Reader(first); var secondReader = Reader(second);
        Assert.NotSame(firstReader, secondReader);
        Assert.NotSame(firstReader.Current, secondReader.Current);
        Assert.Equal(2, callbacks);
        firstReader.Current.GetOptions(schema).Names[0] = "mutation";
        Assert.Equal(new[] { "private" }, secondReader.Current.GetOptions(schema).Names);
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => firstReader.Current);
        Assert.Equal(21, secondReader.Current.GetOptions(schema).Count);
        Assert.Equal(2, callbacks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposingAppInvalidatesReaderButIssuedSnapshotsRemainValues(bool microsoft)
    {
        var schema = Schema();
        var state = new LifecycleState();
        var app = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(Profile(schema, 16));
            context.Services(services => services.Singleton<OwnedProbe>(_ => new(state)));
        }).Build();
        var reader = Reader(app);
        var snapshot = reader.Current;
        _ = app.Services.GetService(typeof(OwnedProbe));
        app.Dispose();
        app.Dispose();
        Assert.Equal(1, state.Disposals);
        Assert.Throws<ObjectDisposedException>(() => reader.Current);
        Assert.Equal(16, snapshot.GetOptions(schema).Count);
        Assert.Equal(16, snapshot.Read<int>("Module:Count").Value);
        Assert.Equal(1, snapshot.Generation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureAfterResolverCreationDisposesRuntimeAndOwnedServices(bool microsoft)
    {
        var schema = Schema();
        var state = new LifecycleState();
        var failure = new InvalidOperationException("create-failure");
        var builder = new FailingAppBuilder(Resolver(microsoft), failure).Setup(context =>
        {
            context.UseConfiguration(Profile(schema, 12));
            context.Services(services => services.Singleton<OwnedProbe>(_ => new(state)));
        });
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Same(failure, error);
        var failing = Assert.IsType<FailingAppBuilder>(builder);
        Assert.NotNull(failing.CapturedReader);
        Assert.Equal(1, state.Disposals);
        Assert.Throws<ObjectDisposedException>(() => failing.CapturedReader!.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFailureRetainsOriginalFailureAndDisposesOwnedGraphOnce(bool microsoft)
    {
        var schema = Schema();
        var state = new LifecycleState();
        var failure = new InvalidOperationException("start-failure");
        var app = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(Profile(schema));
            context.Services(services => services.Singleton<FailingStart>(_ => new(state, failure))
                .Singleton<IStartable>(resolver => new StartAdapter(resolver.Resolve<FailingStart>())));
        }).Build();
        var reader = Reader(app);
        var error = await Record.ExceptionAsync(() => app.StartAsync());
        Assert.NotNull(error);
        Assert.True(Contains(error, failure));
        Assert.Equal(1, state.Starts);
        await app.DisposeAsync();
        await app.DisposeAsync();
        Assert.Equal(1, state.Disposals);
        Assert.Throws<ObjectDisposedException>(() => reader.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlySourceLoadUsesWorkerWithoutCallerSynchronizationContext(bool microsoft)
    {
        var original = SynchronizationContext.Current;
        var synchronization = new RecordingSynchronizationContext();
        var caller = Environment.CurrentManagedThreadId;
        var source = new AsyncSource();
        var bindThread = 0; var registerThread = 0;
        var schema = Schema(validate: _ => bindThread = Environment.CurrentManagedThreadId);
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(schema);
            builder.AddSource("async", ConfigurationSourcePriority.Files, source);
        });
        IApp? app = null;
        try
        {
            SynchronizationContext.SetSynchronizationContext(synchronization);
            app = Builder(microsoft).Setup(context =>
            {
                context.UseConfiguration(profile);
                context.ConfigureModule(schema, (_, _) => registerThread = Environment.CurrentManagedThreadId);
            }).Build();
            Assert.NotEqual(caller, source.BeforeAwaitThread);
            Assert.NotEqual(caller, source.AfterAwaitThread);
            Assert.Null(source.SourceContext);
            Assert.Equal(caller, bindThread);
            Assert.Equal(caller, registerThread);
            Assert.Equal(0, synchronization.Posts);
            Assert.Equal(37, Reader(app).Current.GetOptions(schema).Count);
        }
        finally { SynchronizationContext.SetSynchronizationContext(original); app?.Dispose(); }
    }

    [Fact]
    public void ForeignContextAndProfileWithoutSchemaCatalogAreRejectedExplicitly()
    {
        var schema = Schema();
        var foreign = new ForeignContext();
        Assert.Throws<NotSupportedException>(() => foreign.UseConfiguration(Profile(schema)));
        Assert.Throws<NotSupportedException>(() => foreign.ConfigureModule(schema, (_, _) => { }));
        Assert.Throws<NotSupportedException>(() => new AppConfigurationContext().UseConfiguration(new NoCatalogProfile()));
    }

    [Theory]
    [InlineData("duplicate-profile")]
    [InlineData("duplicate-module")]
    [InlineData("missing-profile")]
    [InlineData("unknown-schema")]
    [InlineData("wrong-instance")]
    public void InvalidStageGraphFailsBeforeResolverAndFactories(string scenario)
    {
        var schema = Schema();
        var tracking = new TrackingResolver(new CobaltServiceResolverBuilder());
        var registrations = 0;
        var loads = 0;
        var profile = ConfigurationProfiles.Create(configuration =>
        {
            configuration.AddSchema(schema);
            configuration.AddSource("counter", ConfigurationSourcePriority.Files, new CallbackSource(() => loads++));
        });
        var builder = new AppBuilder(tracking).Setup(context =>
        {
            if (scenario != "missing-profile") context.UseConfiguration(profile);
            if (scenario == "duplicate-profile") context.UseConfiguration(Profile(schema));
            var selected = scenario == "unknown-schema" ? Schema("Other") : scenario == "wrong-instance" ? Schema() : schema;
            context.ConfigureModule(selected, (_, _) => registrations++);
            if (scenario == "duplicate-module") context.ConfigureModule(schema, (_, _) => registrations++);
        });
        if (scenario is "unknown-schema" or "wrong-instance") Assert.ThrowsAny<ArgumentException>(() => builder.Build());
        else Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Equal(0, tracking.Builds);
        Assert.Equal(0, registrations);
        Assert.Equal(0, loads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrozenStageRejectsNewProfilesAndBindingsInsideRegister(bool microsoft)
    {
        var schema = Schema();
        IAppConfigurationContext? retained = null;
        using var app = Builder(microsoft).Setup(context =>
        {
            retained = context;
            context.UseConfiguration(Profile(schema));
            context.ConfigureModule(schema, (stage, _) =>
            {
                Assert.Throws<InvalidOperationException>(() => stage.UseConfiguration(Profile(schema)));
                Assert.Throws<InvalidOperationException>(() => stage.ConfigureModule(schema, (_, _) => { }));
            });
        }).Build();
        Assert.Throws<InvalidOperationException>(() => retained!.UseConfiguration(Profile(schema)));
        Assert.Throws<InvalidOperationException>(() => retained!.ConfigureModule(schema, (_, _) => { }));
        Assert.Equal(7, Reader(app).Current.GetOptions(schema).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UserRegistrationCannotReplaceReservedReader(bool microsoft)
    {
        var schema = Schema();
        var builder = Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(Profile(schema));
            context.Services(services => services.Instance<IConfigurationReader>(new ForeignReader()));
        });
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    private static AppBuilder Builder(bool microsoft) => new(Resolver(microsoft));
    private static IServiceResolverBuilder Resolver(bool microsoft) => microsoft ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder();
    private static IConfigurationReader Reader(IApp app) => Assert.IsAssignableFrom<IConfigurationReader>(app.Services.GetService(typeof(IConfigurationReader)));
    private static IConfigurationSchema<Options> Schema(string section = "Module", Action<Options>? validate = null)
        => ConfigurationSchemas.Create(section, () => new Options(), value => value with { Names = value.Names.ToArray() },
            validate ?? (_ => { }), fields =>
            {
                fields.Field("Count", value => value.Count, (value, count) => value.Count = count);
                fields.Field("Names", value => value.Names, (value, names) => value.Names = names);
            });
    private static IConfigurationProfile Profile(IConfigurationSchema<Options> schema, int count = 7)
        => ConfigurationProfiles.Create(builder => { builder.AddSchema(schema); builder.Override(schema.Section + ":Count", count); });
    private static bool Contains(Exception error, Exception expected) => ReferenceEquals(error, expected) ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(inner => Contains(inner, expected)) ||
        error.InnerException is { } innerError && Contains(innerError, expected);

    private sealed record Options
    {
        public int Count { get; set; } = 7;
        public string[] Names { get; set; } = ["default"];
    }
    private sealed record NameOptions { public string Name { get; set; } = "default"; }
    private sealed class CallbackSource(Action action, params ConfigurationEntry[] entries) : IConfigurationSource
    {
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        { action(); return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(entries); }
    }
    private sealed class AsyncSource : IConfigurationSource
    {
        internal int BeforeAwaitThread { get; private set; }
        internal int AfterAwaitThread { get; private set; }
        internal SynchronizationContext? SourceContext { get; private set; }
        public async ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
        {
            BeforeAwaitThread = Environment.CurrentManagedThreadId;
            SourceContext = SynchronizationContext.Current;
            await Task.Yield();
            AfterAwaitThread = Environment.CurrentManagedThreadId;
            return [new("Module:Count", JsonSerializer.SerializeToElement("37"))];
        }
    }
    private sealed class TrackingResolver(IServiceResolverBuilder inner) : IServiceResolverBuilder
    {
        internal int Builds { get; private set; }
        public IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration, IServicesRegistrationContext>> registrations)
        { Builds++; return inner.Build(configuration, registrations); }
    }
    private sealed class FailingAppBuilder(IServiceResolverBuilder resolver, Exception failure) : AppBuilder(resolver)
    {
        internal IConfigurationReader? CapturedReader { get; private set; }
        protected override IApp CreateApp(IServiceResolver resolver)
        {
            CapturedReader = resolver.Resolve<IConfigurationReader>();
            _ = resolver.Resolve<OwnedProbe>();
            throw failure;
        }
    }
    private sealed class LifecycleState
    {
        internal int Disposals;
        internal int Starts;
    }
    private sealed class OwnedProbe(LifecycleState state) : IDisposable
    {
        public void Dispose() => state.Disposals++;
    }
    private sealed class FailingStart(LifecycleState state, Exception failure) : IStartable, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) { state.Starts++; throw failure; }
        public void Dispose() => state.Disposals++;
    }
    private sealed class StartAdapter(FailingStart target) : IStartable
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => target.StartAsync(cancellationToken);
    }
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        internal int Posts;
        public override void Post(SendOrPostCallback callback, object? state)
        { Interlocked.Increment(ref Posts); ThreadPool.QueueUserWorkItem(_ => callback(state)); }
    }
    private sealed class ForeignContext : IAppConfigurationContext
    {
        public IAppConfigurationContext Services(Action<IServicesRegistrationContext> action) => this;
        public IAppConfigurationContext Services(Action<IConfiguration, IServicesRegistrationContext> action) => this;
        public IAppConfigurationContext Configuration(Action<IConfiguration> action) => this;
    }
    private sealed class NoCatalogProfile : IConfigurationProfile
    {
        public ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class ForeignReader : IConfigurationReader
    {
        public IConfigurationSnapshot Current => throw new NotSupportedException();
    }
    private sealed class CatalogProfile(IConfigurationProfile inner, Action<IConfigurationSchemaVisitor> visit, long? generation = null)
        : IConfigurationProfile, IConfigurationProfileSchemas
    {
        internal int Loads { get; private set; }
        public void VisitSchemas(IConfigurationSchemaVisitor visitor) => visit(visitor);
        public async ValueTask<IConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            Loads++;
            var snapshot = await inner.LoadAsync(cancellationToken).ConfigureAwait(false);
            return generation is { } value ? new GenerationSnapshot(snapshot, value) : snapshot;
        }
    }
    private sealed class GenerationSnapshot(IConfigurationSnapshot inner, long generation) : IConfigurationSnapshot
    {
        public long Generation => generation;
        public ConfigurationRead<T> Read<T>(string path) => inner.Read<T>(path);
        public T GetOptions<T>(IConfigurationSchema<T> schema) where T : class => inner.GetOptions(schema);
    }
    private sealed class CustomSchema(string section, Func<Options, Options> copy) : IConfigurationSchema<Options>
    {
        public string Section => section;
        public IReadOnlyList<ConfigurationKey> Keys =>
        [
            new(section + ":Count", typeof(int), new()),
            new(section + ":Names", typeof(string[]), new())
        ];
        public IReadOnlyList<ConfigurationEntry> GetDefaults() =>
        [
            new(section + ":Count", JsonSerializer.SerializeToElement(7)),
            new(section + ":Names", JsonSerializer.SerializeToElement(new[] { "default" }))
        ];
        public Options Bind(IConfigurationSnapshot snapshot, Action<Options>? configure = null)
        {
            var result = new Options { Count = snapshot.Read<int>(section + ":Count").Value, Names = snapshot.Read<string[]>(section + ":Names").Value! };
            configure?.Invoke(result);
            return result;
        }
        public Options Copy(Options value) => copy(value);
    }
}
