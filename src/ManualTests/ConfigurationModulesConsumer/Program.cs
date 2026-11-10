using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Ecs;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Editor;
using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Editor.Projects;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Nom;
using TrueMoon.Nom.Hosting;
using TrueMoon.Services;

// Deliberately compiled outside the checkout, against both source and fresh packages.
var directory = Path.GetFullPath(args.Single());
Directory.CreateDirectory(directory);
var checks = 0;
var providers = new List<object>();
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
}
T Required<T>(IApp app) where T : class => app.Services.Resolve<T>() ?? throw new InvalidOperationException(typeof(T).Name);
void Rejected(Action action, string description)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, description); return; }
    throw new InvalidOperationException(description);
}
foreach (var microsoft in new[] { false, true })
{
    var name = microsoft ? "MicrosoftDI" : "Cobalt";
    var report = Path.Combine(directory, name);
    var settingsFile = Path.Combine(directory, name + ".json");
    await File.WriteAllTextAsync(settingsFile, """
        {"Nom":{"MaximumOpenStores":3},"Aether":{"Ecs":{"World":{"MaximumEntityCount":4}}}}
        """);
    var live = new MutableSource([new("Aether:Ecs:World:MaximumEntityCount", JsonSerializer.SerializeToElement(3))]);
    var profile = ConfigurationProfiles.Create(builder =>
    {
        CoreConfigurationSchemas.AddSchemas(builder);
        builder.AddSchema(DiagnosticsConfigurationSchema.Default);
        builder.AddSchema(AetherConfigurationSchemas.Engine);
        builder.AddSchema(AetherConfigurationSchemas.Authoring);
        builder.AddSchema(AetherConfigurationSchemas.World);
        builder.AddSchema(AetherConfigurationSchemas.Scheduler);
        builder.AddSchema(AetherConfigurationSchemas.SceneLoading);
        builder.AddSchema(AetherConfigurationSchemas.Primitives);
        builder.AddSchema(AetherConfigurationSchemas.RenderResources);
        builder.AddSchema(EditorConfigurationSchemas.Options);
        builder.AddSchema(EditorConfigurationSchemas.Diagnostics);
        builder.AddSchema(NomConfigurationSchema.Default);
        builder.AddSchema(PipesConfigurationSchema.Default);
        builder.AddJsonFile("settings", settingsFile);
        builder.AddSource("mutable", ConfigurationSourcePriority.Files, live);
        builder.AddEnvironment("environment", "TRUEMOON_CORE75_", ["Nom:MaximumOpenStores"]);
        builder.AddArguments("args", ["--Nom:MaximumOpenStores=2", "--Aether:Ecs:World:MaximumEntityCount=2"]);
        builder.Override("Aether:ReportDirectory", report);
        builder.Override("Editor:SourceTracking:Enabled", false);
        builder.Override("Editor:Diagnostics:RefreshInterval", TimeSpan.FromMilliseconds(40));
        builder.Override("Aether:Ecs:World:InitialEntityCapacity", 0);
    });
    var created = 0;
    NeverRuntime? runtime = null;
    var composition = new AetherComposition
    {
        HostContractId = new('a', 64), GameBuildId = new('b', 64),
        RuntimeFactory = _ => { created++; return runtime = new NeverRuntime(); }
    };
    var app = App.Builder(context => { if (microsoft) context.UseDI(); })
        .Configuration(configuration =>
        {
            configuration.RemoveProvider<EnvironmentVariablesProvider>();
            configuration.RemoveProvider<CommandLineArgsProvider>();
        }).Setup(context => context.UseConfiguration(profile)
            .UseCoreConfiguration().UseConfiguredDiagnostics()
            .UseConfiguredAetherEditor(composition)
            .UseConfiguredAetherEcs().UseConfiguredAetherEcsSystems(_ => { })
            .UseConfiguredAetherSceneLoading(_ => new ComponentRegistryBuilder().Build())
            .UseConfiguredAetherPrimitiveEcs().UseConfiguredAetherRenderResources()
            .UseConfiguredNom(value => value.MaximumOpenStores = 1)
            .UseConfiguredPipesServiceTransport()).Build();
    try
    {
        Check(created == 0 && !Directory.Exists(report), "No runtime factory or report IO during binding");
        var reader = Required<IConfigurationReader>(app);
        Check(reader.Current.Generation == 1, "Initial generation");
        Check(reader.Current.Read<int>("Nom:MaximumOpenStores").Value == 2, "Arguments win over file/environment");
        Check(reader.Current.Read<int>("Nom:MaximumOpenStores").Origin!.SourceId == "args", "Winning provenance");
        var options = reader.Current.GetOptions(NomConfigurationSchema.Default);
        Check(options.MaximumOpenStores == 1, "Explicit typed callback wins");
        options.MaximumOpenStores = 4;
        Check(reader.Current.GetOptions(NomConfigurationSchema.Default).MaximumOpenStores == 1, "Detached options copy");
        Check(reader.Current.GetOptions(EditorConfigurationSchemas.Diagnostics).RefreshInterval == TimeSpan.FromMilliseconds(40), "Editor data bound");
        Check(Required<EditorDiagnosticsViewModel>(app).RefreshInterval == TimeSpan.FromMilliseconds(40), "Existing Editor consumer receives prepared data");
        using (var world = Required<IFactory<World>>(app).Create()!)
        {
            world.CreateEntity(); world.CreateEntity();
            Rejected(() => world.CreateEntity(), "Actual World enforces configured entity quota");
            using var scheduler = Required<IFactory<EcsScheduler, World>>(app).Create(world);
            Check(scheduler.Tick(TimeSpan.FromMilliseconds(20)) && scheduler.TickNumber == 1, "Configured scheduler ticks");
        }
        Check(reader.Current.GetOptions(AetherConfigurationSchemas.SceneLoading).MaximumEntities > 0, "Scene loading schema present");
        Check(reader.Current.GetOptions(AetherConfigurationSchemas.Primitives).FixedStep > TimeSpan.Zero, "Simulation data present");
        Check(reader.Current.GetOptions(AetherConfigurationSchemas.RenderResources).MaximumGpuEntries > 0, "Render budgets present without GPU activation");
        Check(reader.Current.GetOptions(PipesConfigurationSchema.Default).MaxPendingRequests > 0, "Transport data present");
        await app.StartAsync();
        Check(created == 1 && runtime!.Launches == 0, "One owned runtime factory, no child launch");
        Check(Required<EditorProjects>(app).Active is null && !Required<EditorWorkspace>(app).HasProject, "Empty Editor startup preserved");
        Check(Required<AetherHost>(app).Gpu.ResourceCount == 0, "No GPU resources");
        Check(Required<AetherHost>(app).Supervisor.Status.State == SessionState.Stopped, "No implicit game session");
        var host = Required<NomStoreHost>(app);
        var factory = Required<IFactory<IProjectAssetStore, OpenAssetStoreArguments>>(app);
        Check(host.ActiveStoreCount == 0, "Nom startup opens no store");
        var root = Path.Combine(directory, name + "-stores");
        OpenAssetStoreArguments Store(string child) => new(Path.Combine(root, child), Guid.NewGuid(), Guid.NewGuid(), AssetStoreOpenMode.Create);
        var second = Store("second");
        await using (var store = factory.Create(Store("first")))
        {
            Check(host.ActiveStoreCount == 1, "Actual store opened");
            Rejected(() => factory.Create(second), "Configured Nom host enforces store quota");
            Check(!Directory.Exists(second.Root), "Rejected store creates no directory");
        }
        Check(host.ActiveStoreCount == 0, "Caller closes owned store");
        await using (var store = factory.Create(second)) Check(host.ActiveStoreCount == 1, "Store slot returned after close");
        var previous = reader.Current;
        // Change a startup field without a higher-priority override.
        live.Entries = [new("Aether:BuildTimeout", JsonSerializer.SerializeToElement("00:00:30"))];
        var update = await Required<IConfigurationReloader>(app).ReloadAsync();
        Check(!update.Published && update.RestartRequired, "Module startup changes require restart");
        Check(ReferenceEquals(previous, reader.Current) && update.Generation == 1, "Rejected reload leaves atomic snapshot intact");
        Check(reader.Current.GetOptions(AetherConfigurationSchemas.Engine).BuildTimeout == TimeSpan.FromSeconds(120), "Running engine settings retained");
        await app.StopAsync();
        Check(host.IsReleased && host.ActiveStoreCount == 0, "Nom lifecycle drained");
        Check(Required<EditorProjects>(app).IsClosed, "Editor lifecycle closed");
        Check(runtime!.Disposals == 0, "Runtime remains App-owned until dispose");
    }
    finally { await app.DisposeAsync(); }
    Check(runtime!.Disposals == 1, "Runtime factory disposed exactly once");
    // Window configuration is verified without starting native presentation.
    var windowProfile = ConfigurationProfiles.Create(builder =>
    {
        builder.AddSchema(WindowConfigurationSchema.Default);
        builder.AddArguments("window-args", ["--Alloy:Window:Width=512"]);
    });
    var roots = 0;
    await using (var windowApp = App.Builder(context => { if (microsoft) context.UseDI(); })
        .Setup(context => context.UseConfiguration(windowProfile).UseConfiguredPresentation(
            _ => { roots++; throw new InvalidOperationException("Unexpected native root"); },
            options => options.UseSkiaVulkan(), value => value.Height = 384)).Build())
    {
        var reader = Required<IConfigurationReader>(windowApp);
        Check(reader.Current.GetOptions(WindowConfigurationSchema.Default).Width == 512, "Window argument bound");
        Check(reader.Current.GetOptions(WindowConfigurationSchema.Default).Height == 384, "Window callback bound");
        Check(!Required<HostedUiWindow>(windowApp).Completion.IsCompleted, "Native window not started");
        Check(Required<HostedUiSessionFactory>(windowApp).ActiveSessionCount == 0 && roots == 0, "Root/session factories remain lazy");
    }
    var flags = RuntimeStartupSettings.Parse(new Dictionary<string, string?>
    { [RuntimeStartupSettings.LegacyValidationKey] = "1", [RuntimeStartupSettings.ValidationKey] = "false" });
    Check(!flags.GpuValidation, "Canonical runtime flag wins over legacy alias");
    Check(RuntimeStartupSettings.Parse(new Dictionary<string, string?> { [RuntimeStartupSettings.LegacyValidationKey] = "1" }).GpuValidation, "Legacy runtime 1 alias retained");
    Check(!typeof(EditorView).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == "TrueMoon"), "Editor has no direct bootstrap dependency");
    providers.Add(new { Provider = name, RuntimeFactoryCreations = created, RuntimeFactoryDisposals = runtime.Disposals });
}
await File.WriteAllTextAsync(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new
{
    Completed = true, Checks = checks, Providers = providers, GpuRun = false, WindowRun = false, ChildProcessRun = false
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Configuration modules acceptance: {checks} checks on both DI providers.");

sealed class NeverRuntime : IRuntimeFactory, IDisposable
{
    public int Launches { get; private set; }
    public int Disposals { get; private set; }
    public IRuntimeLease Create(SessionLaunch arguments) { Launches++; throw new InvalidOperationException("Unexpected child launch"); }
    public void Dispose() => Disposals++;
}
sealed class MutableSource(ConfigurationEntry[] entries) : IConfigurationSource
{
    public ConfigurationEntry[] Entries { get; set; } = entries;
    public ValueTask<IReadOnlyList<ConfigurationEntry>> ReadAsync(IReadOnlyList<ConfigurationKey> keys, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult<IReadOnlyList<ConfigurationEntry>>(Entries.ToArray()); }
}
