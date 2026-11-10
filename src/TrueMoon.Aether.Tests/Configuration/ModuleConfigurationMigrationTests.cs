using System.Text.Json;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Rendering.Resources;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Aether.Sessions;
using TrueMoon.Configuration;
using TrueMoon.Extensions.DependencyInjection;
using Xunit;

namespace TrueMoon.Aether.Tests.Configuration;

public sealed class ModuleConfigurationMigrationTests
{
    [Fact]
    public async Task StandardModuleSchemasBindTypedInputsAndReturnIndependentOptions()
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            AddSchemas(builder);
            builder.AddValues("module-values", ConfigurationSourcePriority.Arguments,
            [
                Entry(Key(AetherConfigurationSchemas.World, "InitialEntityCapacity"), 0),
                Entry(Key(AetherConfigurationSchemas.World, "MaximumEntityCount"), 3),
                Entry(Key(AetherConfigurationSchemas.Scheduler, "MaximumSystems"), 7),
                Entry(Key(AetherConfigurationSchemas.SceneLoading, "MaximumEntities"), 2),
                Entry(Key(AetherConfigurationSchemas.Primitives, "FixedStep"), "00:00:00.025"),
                Entry(Key(AetherConfigurationSchemas.RenderResources, "MaximumCpuBytes"), 12345L),
                Entry(Key(AetherConfigurationSchemas.Authoring, "MaxHistoryEntries"), 0)
            ]);
        });
        var snapshot = await profile.LoadAsync();
        var engine = AetherConfigurationSchemas.Engine.Bind(snapshot);
        var world = AetherConfigurationSchemas.World.Bind(snapshot);
        var scheduler = AetherConfigurationSchemas.Scheduler.Bind(snapshot);
        var loading = AetherConfigurationSchemas.SceneLoading.Bind(snapshot);
        var primitives = AetherConfigurationSchemas.Primitives.Bind(snapshot);
        var resources = AetherConfigurationSchemas.RenderResources.Bind(snapshot);
        var authoring = AetherConfigurationSchemas.Authoring.Bind(snapshot);

        Assert.Equal("aether-reports", snapshot.Read<string>(Key(AetherConfigurationSchemas.Engine, "ReportDirectory")).Value);
        Assert.Equal(TimeSpan.FromSeconds(120), engine.BuildTimeout);
        Assert.Equal((0, 3), (world.InitialEntityCapacity, world.MaximumEntityCount));
        Assert.Equal(7, scheduler.MaximumSystems);
        Assert.Equal(2, loading.MaximumEntities);
        Assert.Equal(TimeSpan.FromMilliseconds(25), primitives.FixedStep);
        Assert.Equal(12345L, resources.MaximumCpuBytes);
        Assert.Equal(0, authoring.MaxHistoryEntries);
        Assert.Equal("module-values", snapshot.Read<int>(Key(AetherConfigurationSchemas.World, "MaximumEntityCount")).Origin!.SourceId);

        world.MaximumEntityCount = 9;
        scheduler.MaximumSystems = 9;
        loading.MaximumEntities = 9;
        primitives.Near = 9;
        resources.MaximumCpuBytes = 9;
        authoring.MaxHistoryEntries = 9;
        Assert.Equal(3, AetherConfigurationSchemas.World.Bind(snapshot).MaximumEntityCount);
        Assert.Equal(7, AetherConfigurationSchemas.Scheduler.Bind(snapshot).MaximumSystems);
        Assert.Equal(2, AetherConfigurationSchemas.SceneLoading.Bind(snapshot).MaximumEntities);
        Assert.Equal(.1f, AetherConfigurationSchemas.Primitives.Bind(snapshot).Near);
        Assert.Equal(12345L, AetherConfigurationSchemas.RenderResources.Bind(snapshot).MaximumCpuBytes);
        Assert.Equal(0, AetherConfigurationSchemas.Authoring.Bind(snapshot).MaxHistoryEntries);
    }

    [Fact]
    public void ModuleMetadataContainsOnlyStartupDataAndExcludesCompositionAndCameraIdentity()
    {
        var keys = AllKeys().ToArray();
        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.Equal(ConfigurationChangeMode.StartupOnly, key.Field.Change));
        Assert.DoesNotContain(keys, key => key.Path.Contains("HostContractId", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("GameBuildId", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("Factory", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("RuntimeLaunch", StringComparison.OrdinalIgnoreCase)
            || key.Path.Contains("SessionId", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(AetherConfigurationSchemas.Primitives.Keys, key => key.Path.Contains("Camera", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false, "engine", "BuildTimeout", "\"00:00:00\"")]
    [InlineData(true, "world", "MaximumEntityCount", "0")]
    [InlineData(false, "scheduler", "MaximumSystems", "1025")]
    [InlineData(true, "loading", "MaximumEntities", "4097")]
    [InlineData(false, "primitives", "Near", "0")]
    [InlineData(true, "resources", "MaximumCpuBytes", "-1")]
    [InlineData(false, "authoring", "MaxBatchEdits", "0")]
    public void InvalidModuleWinnerFailsBeforeAnyRegistration(bool microsoft, string module, string field, string json)
    {
        var path = KeysFor(module).Single(key => key.Path.EndsWith(":" + field, StringComparison.Ordinal)).Path;
        using var document = JsonDocument.Parse(json);
        var registrations = 0;
        var profile = ConfigurationProfiles.Create(builder =>
        {
            AddSchemas(builder);
            builder.AddValues("invalid-module", ConfigurationSourcePriority.Arguments, [new ConfigurationEntry(path, document.RootElement)]);
        });
        var error = Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(AetherConfigurationSchemas.Engine, (_, _) => registrations++);
        }).Build());
        Assert.NotEmpty(error.Issues);
        Assert.Equal(0, registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredHeadlessFactoriesEnforcePreparedLimitsAndKeepProductsCallerOwned(bool microsoft)
    {
        var registryCalls = 0;
        var camera = new CameraTransform(2, 3, 8, 0, 0, 0);
        var profile = ConfigurationProfiles.Create(builder =>
        {
            AddSchemas(builder);
            builder.Override(Key(AetherConfigurationSchemas.World, "InitialEntityCapacity"), 0);
            builder.Override(Key(AetherConfigurationSchemas.World, "MaximumEntityCount"), 2);
            builder.Override(Key(AetherConfigurationSchemas.SceneLoading, "MaximumEntities"), 1);
            builder.Override(Key(AetherConfigurationSchemas.RenderResources, "MaximumCpuScopes"), 1);
            builder.Override(Key(AetherConfigurationSchemas.Authoring, "MaxOperations"), 1);
            builder.Override(Key(AetherConfigurationSchemas.Primitives, "Near"), .3f);
        });
        var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEcs().UseConfiguredAetherEcsSystems(_ => { })
            .UseConfiguredAetherSceneLoading(_ => { registryCalls++; return new ComponentRegistryBuilder().Build(); })
            .UseConfiguredAetherPrimitiveEcs(value => value.Camera = camera)
            .UseConfiguredAetherRenderResources().UseConfiguredAetherAuthoring()).Build();
        try
        {
            Assert.Equal(0, registryCalls);
            Assert.Null(app.Services.GetService(typeof(World)));
            Assert.Null(app.Services.GetService(typeof(AetherHost)));
            using var world = Required<IFactory<World>>(app).Create()!;
            world.CreateEntity(); world.CreateEntity();
            Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
            using var scheduledWorld = Required<IFactory<World>>(app).Create()!;
            using var scheduler = Required<IFactory<EcsScheduler, World>>(app).Create(scheduledWorld);
            Assert.True(scheduler.Tick(TimeSpan.FromMilliseconds(25)));
            Assert.Equal(1ul, scheduler.TickNumber);

            var document = EmptyScene();
            var scenes = Required<IFactory<SceneWorld, SceneWorldLoadRequest>>(app);
            using var scene = scenes.Create(new(new(document)));
            Assert.Equal(1, registryCalls);
            using var simulation = Required<IFactory<PrimitiveSimulation, PrimitiveSimulationRequest>>(app).Create(new(scene));
            Assert.Equal(camera, simulation.Snapshot.Scene.Camera);
            Assert.Equal(.3f, simulation.Snapshot.Scene.Near);
            simulation.Tick();
            Assert.Equal(1ul, simulation.Snapshot.Tick);
            var definitionId = new EntityDefinitionId(Guid.NewGuid());
            var tooMany = new SceneDocument(document.Id, 0,
                [new(new(Guid.NewGuid()), definitionId), new(new(Guid.NewGuid()), definitionId)],
                [new(definitionId, EntityPatch.Empty)], []);
            Assert.Equal(SceneWorldLoadError.LimitExceeded,
                Assert.Throws<SceneWorldLoadException>(() => scenes.Create(new(new(tooMany)))).Error);

            using var authoring = Required<IFactory<ISceneAuthoringSession, SceneDocument>>(app).Create(document);
            Assert.Equal(SceneAuthoringError.NoUndo, authoring.Undo(new(Guid.NewGuid(), 0, "core75")).Error);
            Assert.Equal(SceneAuthoringError.OperationLimitReached, authoring.Undo(new(Guid.NewGuid(), 0, "core75")).Error);
            var manifests = Required<IFactory<RenderManifestLease, RenderContentManifest>>(app);
            var hash = RenderContentHash.Parse(new string('a', 64));
            using var lease = manifests.Create(new(Guid.NewGuid(), hash, Guid.NewGuid(), []));
            Assert.Equal(RenderResourceError.LimitExceeded,
                Assert.Throws<RenderResourceException>(() => manifests.Create(new(Guid.NewGuid(), hash, Guid.NewGuid(), []))).Error);
            Assert.Equal(0, Required<RenderResourceHost>(app).DeviceCount);

            simulation.Dispose(); scene.Dispose(); lease.Dispose();
            await app.DisposeAsync();
            Assert.False(world.IsDisposed);
            Assert.False(scheduledWorld.IsDisposed);
            Assert.Equal(document.Id, authoring.Read().Document.Id);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredEngineCopiesNestedSettingsAndKeepsIdentityAndFactoriesInComposition(bool microsoft)
    {
        var runtime = new NeverRuntime();
        var activations = 0;
        var report = Path.Combine(Path.GetTempPath(), "core75-no-report-" + Guid.NewGuid().ToString("N"));
        var descriptor = NativeRuntimeLaunch.CurrentProcess("--runtime");
        var composition = new AetherComposition
        {
            HostContractId = new('a', 64), GameBuildId = new('b', 64), RuntimeLaunch = descriptor,
            RuntimeFactory = _ => { activations++; return runtime; }
        };
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(AetherConfigurationSchemas.Engine);
            builder.Override("Aether:ReportDirectory", report);
            builder.Override("Aether:BuildRetention:MaxJobs", 3);
            builder.Override("Aether:ChildDiagnostics:MaxBatchRecords", 5);
            builder.Override("Aether:Deadlines:Command", TimeSpan.FromMilliseconds(250));
        });
        var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAether(composition, value => value.GpuValidation = true)).Build();
        try
        {
            Assert.Equal(0, activations);
            var reader = Required<IConfigurationReader>(app);
            var options = reader.Current.GetOptions(AetherConfigurationSchemas.Engine);
            Assert.Equal((3, 5, TimeSpan.FromMilliseconds(250)), (options.BuildRetention.MaxJobs, options.ChildDiagnostics.MaxBatchRecords, options.Deadlines.Command));
            Assert.True(options.GpuValidation);
            Assert.False(reader.Current.Read<bool>("Aether:GpuValidation").Value);
            options.BuildRetention.MaxJobs = 9;
            options.ChildDiagnostics.MaxBatchRecords = 9;
            Assert.Equal(3, reader.Current.GetOptions(AetherConfigurationSchemas.Engine).BuildRetention.MaxJobs);
            Assert.Equal(5, reader.Current.GetOptions(AetherConfigurationSchemas.Engine).ChildDiagnostics.MaxBatchRecords);
            Assert.Null(descriptor.StartupSettings);
            Assert.True(Required<NativeRuntimeLaunch>(app).StartupSettings!.GpuValidation);
            Assert.False(Directory.Exists(report));
            await app.StartAsync();
            Assert.Equal(1, activations);
            Assert.Equal(0, runtime.Launches);
            Assert.Equal(SessionState.Stopped, Required<AetherHost>(app).Supervisor.Status.State);
            Assert.Equal(0, Required<AetherHost>(app).Gpu.ResourceCount);
            await app.StopAsync();
            Assert.Equal(0, runtime.Disposals);
        }
        finally { await app.DisposeAsync(); }
        Assert.Equal(1, runtime.Disposals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupModuleChangeRejectsCandidateWithoutRecreatingFactories(bool microsoft)
    {
        var key = Key(AetherConfigurationSchemas.World, "MaximumEntityCount");
        var source = new MutableSource([Entry(key, 2), Entry(Key(AetherConfigurationSchemas.World, "InitialEntityCapacity"), 0)]);
        var profile = ConfigurationProfiles.Create(builder => { builder.AddSchema(AetherConfigurationSchemas.World); builder.AddSource("limits", ConfigurationSourcePriority.Files, source); });
        await using var app = Builder(microsoft).Setup(context => context.UseConfiguration(profile).UseConfiguredAetherEcs()).Build();
        var factory = Required<IFactory<World>>(app);
        var reader = Required<IConfigurationReader>(app);
        var before = reader.Current;
        source.Entries = [Entry(key, 3), Entry(Key(AetherConfigurationSchemas.World, "InitialEntityCapacity"), 0)];
        var update = await Required<IConfigurationReloader>(app).ReloadAsync();
        Assert.False(update.Published);
        Assert.True(update.RestartRequired);
        Assert.Equal(new[] { key }, update.ChangedKeys);
        Assert.Same(before, reader.Current);
        Assert.Equal(1, update.Generation);
        Assert.Same(factory, Required<IFactory<World>>(app));
        using var world = factory.Create()!;
        world.CreateEntity(); world.CreateEntity();
        Assert.Throws<InvalidOperationException>(() => world.CreateEntity());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredEngineRejectsInvalidWinningBudgetBeforeRuntimeFactory(bool microsoft)
    {
        var factories = 0;
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(AetherConfigurationSchemas.Engine); builder.Override("Aether:BuildTimeout", TimeSpan.Zero); });
        var composition = new AetherComposition
        {
            HostContractId = new('a', 64), GameBuildId = new('b', 64),
            RuntimeFactory = _ => { factories++; return new NeverRuntime(); }
        };
        Assert.Throws<ConfigurationException>(() => Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAether(composition)).Build());
        Assert.Equal(0, factories);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfiguredSchedulerUsesThePreparedSystemLimitForItsExplicitGraph(bool microsoft)
    {
        var profile = ConfigurationProfiles.Create(builder =>
        {
            builder.AddSchema(AetherConfigurationSchemas.World);
            builder.AddSchema(AetherConfigurationSchemas.Scheduler);
            builder.Override("Aether:Ecs:Scheduler:MaximumSystems", 1);
        });
        Assert.ThrowsAny<ArgumentException>(() => Builder(microsoft).Setup(context => context.UseConfiguration(profile)
            .UseConfiguredAetherEcs().UseConfiguredAetherEcsSystems(systems => systems
                .Add<NeverSystem>(new("first")).Add<NeverSystem>(new("second")))).Build());
    }

    private static IAppBuilder Builder(bool microsoft) => App.Builder(context => { if (microsoft) context.UseDI(); });
    private static string Key<T>(IConfigurationSchema<T> schema, string suffix) where T : class
        => schema.Keys.Single(key => key.Path.EndsWith(":" + suffix, StringComparison.Ordinal)).Path;
    private static ConfigurationEntry Entry<T>(string key, T value) => new(key, JsonSerializer.SerializeToElement(value));
    private static T Required<T>(IApp app) => Assert.IsAssignableFrom<T>(app.Services.GetService(typeof(T)));
    private static SceneDocument EmptyScene() => new(new(Guid.NewGuid()), 0, [], [], []);
    private sealed class NeverRuntime : IRuntimeFactory, IDisposable
    {
        public int Launches, Disposals;
        public IRuntimeLease Create(SessionLaunch launch) { Launches++; throw new InvalidOperationException("Unexpected runtime launch."); }
        public void Dispose() => Disposals++;
    }
    private sealed class MutableSource(IReadOnlyList<ConfigurationEntry> entries) : IConfigurationSource
    {
        public IReadOnlyList<ConfigurationEntry> Entries = entries;
        public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries);
    }
    private sealed class NeverSystem : IEcsSystem
    {
        public void Execute(EcsSystemContext context) => throw new InvalidOperationException("Unexpected system activation.");
        public void Dispose() { }
    }
    private static void AddSchemas(IConfigurationProfileBuilder builder)
    {
        builder.AddSchema(AetherConfigurationSchemas.Engine);
        builder.AddSchema(AetherConfigurationSchemas.World);
        builder.AddSchema(AetherConfigurationSchemas.Scheduler);
        builder.AddSchema(AetherConfigurationSchemas.SceneLoading);
        builder.AddSchema(AetherConfigurationSchemas.Primitives);
        builder.AddSchema(AetherConfigurationSchemas.RenderResources);
        builder.AddSchema(AetherConfigurationSchemas.Authoring);
    }
    private static IEnumerable<ConfigurationKey> AllKeys() => new[] { "engine", "world", "scheduler", "loading", "primitives", "resources", "authoring" }.SelectMany(KeysFor);
    private static IReadOnlyList<ConfigurationKey> KeysFor(string module) => module switch
    {
        "engine" => AetherConfigurationSchemas.Engine.Keys,
        "world" => AetherConfigurationSchemas.World.Keys,
        "scheduler" => AetherConfigurationSchemas.Scheduler.Keys,
        "loading" => AetherConfigurationSchemas.SceneLoading.Keys,
        "primitives" => AetherConfigurationSchemas.Primitives.Keys,
        "resources" => AetherConfigurationSchemas.RenderResources.Keys,
        "authoring" => AetherConfigurationSchemas.Authoring.Keys,
        _ => throw new ArgumentException("Unknown test module.", nameof(module))
    };
}
