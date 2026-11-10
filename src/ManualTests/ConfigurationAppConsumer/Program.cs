using System.Text.Json;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Reflection;
using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;
using TrueMoon.Diagnostics;
using TrueMoon.Services;
using MicrosoftBuilder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace ConfigurationAppConsumer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args is ["--migration-child", var childReport, var childProvider])
            return await RunMigrationChildAsync(childReport, bool.Parse(childProvider));
        var report = Path.GetFullPath(args.Single());
        Directory.CreateDirectory(report);
        var checks = 0;
        void Check(bool condition) { if (!condition) throw new InvalidOperationException("App configuration consumer check failed."); checks++; }
        try
        {
            foreach (var microsoft in new[] { false, true })
            {
                await CheckCoreMigrationAsync(report, microsoft, Check);
                await CheckReloadAsync(report, microsoft, Check);
                var first = ConfigurationSchemas.Create("First", () => new FirstOptions(),
                    value => value with { Names = value.Names.ToArray() },
                    value => { if (value.Count < 1) throw new ArgumentOutOfRangeException(nameof(value.Count)); },
                    fields =>
                    {
                        fields.Field("Count", value => value.Count, (value, count) => value.Count = count);
                        fields.Field("Names", value => value.Names, (value, names) => value.Names = names);
                    });
                var second = ConfigurationSchemas.Create("Second", () => new SecondOptions(), value => value with { },
                    value => { if (value.Count < 1) throw new ArgumentOutOfRangeException(nameof(value.Count)); },
                    fields => fields.Field("Count", value => value.Count, (value, count) => value.Count = count));
                var profile = ConfigurationProfiles.Create(builder =>
                {
                    builder.AddSchema(first); builder.AddSchema(second);
                    builder.Override("First:Count", 12); builder.Override("Second:Count", 22);
                });
                Check(profile is IConfigurationProfileSchemas);
                var declarations = 0; var callbacks = 0; var preparedCount = 0;
                FirstOptions? registrationCopy = null;
                var state = new State();
                var builder = NewBuilder(microsoft).Setup(context =>
                {
                    context.UseConfiguration(profile);
                    context.Configuration(configuration => configuration.Set("appName", "legacy-config-name"));
                    context.ConfigureModule(first, (app, options) =>
                    {
                        declarations++; registrationCopy = options;
                        app.AddUnit(unit => unit.Services(services => services.Instance(options)), unit =>
                        { unit.Name = "first"; unit.HostingPolicy = UnitHostingPolicy.MainProcess; });
                        app.Configuration(configuration => { preparedCount = declarations; configuration.Set("post-legacy", "kept"); });
                    }, options => { callbacks++; options.Count++; });
                    context.ConfigureModule(second, (app, options) =>
                    {
                        declarations++;
                        app.AddUnit(unit => unit.Services(services => services.Instance(options)), unit =>
                        { unit.Name = "second"; unit.HostingPolicy = UnitHostingPolicy.MainProcess; });
                    }, options => { callbacks++; options.Count += 2; });
                    context.Services(services => services.Singleton<Worker>(resolver =>
                        new Worker(resolver.Resolve<FirstOptions>(), resolver.Resolve<SecondOptions>(), state)));
                });
                var app = builder.Build();
                var reader = app.Services.Resolve<IConfigurationReader>();
                var snapshot = reader.Current;
                Check(reader is not IDisposable && reader is not IAsyncDisposable);
                Check(snapshot.Generation == 1);
                Check(callbacks == 2 && preparedCount == 2);
                Check(snapshot.Read<int>("First:Count").Value == 12);
                Check(snapshot.GetOptions(first).Count == 13 && snapshot.GetOptions(second).Count == 24);
                Check(app.Name == "legacy-config-name" && app.Configuration.Get<string>("post-legacy") == "kept");
                var units = app.Services.Resolve<IReadOnlyList<IUnitConfiguration>>();
                Check(units.Count == 2 && units.Select(unit => unit.Name).SequenceEqual(["first", "second"]));
                Check(units.All(unit => unit.HostingPolicy == UnitHostingPolicy.MainProcess));
                var worker = app.Services.Resolve<Worker>();
                Check(worker.Sum == 37 && state.Created == 1);
                var copy = snapshot.GetOptions(first); copy.Count = 99; copy.Names[0] = "changed";
                registrationCopy!.Names[0] = "register changed";
                Check(snapshot.GetOptions(first).Count == 13 && snapshot.GetOptions(first).Names[0] == "original");
                await app.StartAsync(); await app.StopAsync();
                Check(reader.Current.GetOptions(first).Count == 13 && callbacks == 2);
                await app.DisposeAsync(); await app.DisposeAsync();
                Check(state.Disposals == 1);
                try { _ = reader.Current; throw new InvalidOperationException("Disposed reader was accepted."); }
                catch (ObjectDisposedException) { Check(true); }
                Check(snapshot.GetOptions(first).Count == 13);

                // A second build reuses immutable schema/profile data, never the first runtime.
                var next = builder.Build(); var nextReader = next.Services.Resolve<IConfigurationReader>();
                Check(!ReferenceEquals(reader, nextReader) && nextReader.Current.GetOptions(second).Count == 24);
                await next.DisposeAsync();
                Check(callbacks == 4);

                var bad = ConfigurationProfiles.Create(configuration =>
                { configuration.AddSchema(first); configuration.AddSchema(second); configuration.Override("Second:Count", 0); });
                var registers = 0; var factories = 0;
                try
                {
                    using var unexpected = NewBuilder(microsoft).Setup(context =>
                    {
                        context.UseConfiguration(bad);
                        context.ConfigureModule(first, (appContext, options) =>
                        {
                            registers++;
                            appContext.Services(services => services.Singleton<Worker>(_ =>
                            { factories++; return new Worker(options, new(), new()); }));
                        });
                    }).Build();
                    throw new InvalidOperationException("Invalid unbound schema was accepted.");
                }
                catch (ConfigurationException) { Check(registers == 0 && factories == 0); }
            }
            await File.WriteAllTextAsync(Path.Combine(report, "verification.json"), JsonSerializer.Serialize(new
            {
                Completed = true, Checks = checks, Providers = new[] { "Cobalt", "Microsoft" },
                GpuRun = false, WindowRun = false, ChildProcessRun = true,
                ConfigurationAssembly = typeof(ConfigurationAppExtensions).Assembly.Location,
                ContractsAssembly = typeof(IConfigurationReader).Assembly.Location
            }));
            Console.WriteLine($"App configuration: {checks} checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static IAppBuilder NewBuilder(bool microsoft) => new AppBuilder(microsoft
        ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder()).Configuration(configuration =>
    {
        // Keep this parent-only fixture independent of any host's inherited legacy child identity.
        configuration.RemoveProvider<EnvironmentVariablesProvider>();
        configuration.RemoveProvider<CommandLineArgsProvider>();
    });

    private static async Task CheckCoreMigrationAsync(string report, bool microsoft, Action<bool> check)
    {
        var childDirectory = Path.Combine(report, microsoft ? "migration-microsoft" : "migration-cobalt");
        Directory.CreateDirectory(childDirectory);
        var root = Path.Combine(childDirectory, "root with spaces");
        var file = Path.Combine(childDirectory, "core.json");
        await File.WriteAllTextAsync(file, "{\"App\":{\"Name\":\"file-name\"},\"Diagnostics\":{\"MemoryCapacity\":23,\"MinimumLevel\":\"Warning\",\"SourceMinimumLevels\":{\"migration\":\"Error\"},\"Telemetry\":{\"MetricsEnabled\":false}}}");
        var builder = NewBuilder(microsoft).Configuration(configuration => configuration.AddProvider(new CommandLineArgsProvider(
            () => ["appName=migration-parent", "-root", root, "--foreign=kept"])));
        var app = builder.Setup(context =>
        {
            context.Configuration(legacy => context.UseConfiguration(ConfigurationProfiles.Create(profile =>
            {
                profile.AddSchemas(); profile.AddLegacySources(legacy);
                profile.AddSchema(DiagnosticsConfigurationSchema.Default);
                profile.AddJsonFile("core-file", file);
                profile.Override("Diagnostics:QueueCapacity", 32);
            })));
            context.UseCoreConfiguration().UseConfiguredDiagnostics(configure: settings => settings.QueueCapacity = 40);
            context.Services(services => services.Singleton<IFactory<IUnitHandle, IUnitConfiguration>>(resolver =>
                new MigrationHandleFactory(childDirectory, microsoft, resolver.Resolve<IEventsSource<UnitsController>>())));
            DeclareMigrationUnits(context);
        }).Build();
        try
        {
            var reader = app.Services.Resolve<IConfigurationReader>();
            check(app.Name == "migration-parent" && reader.Current.GetOptions(CoreConfigurationSchemas.App).Name == app.Name);
            app.Configuration.Set("appName", "legacy-mutated");
            app.Services.Resolve<AppSettings>().Name = "dto-mutated";
            check(app.Name == "migration-parent");
            app.Services.Resolve<PathSettings>().Root = "dto-mutated-root";
            check(app.Services.Resolve<IPathResolver>().ResolvePath(Paths.Root) == root);
            check(reader.Current.Read<string>("App:Name").Origin?.SourceId == "core-legacy-args");
            var effective = app.Services.Resolve<DiagnosticPipeline>().ReadConfiguration();
            check(effective.QueueCapacity == 40 && effective.MemoryCapacity == 23 && effective.MinimumLevel == DiagnosticLevel.Warning);
            check(reader.Current.Read<int>("Diagnostics:QueueCapacity").Value == 32 && reader.Current.GetOptions(DiagnosticsConfigurationSchema.Default).QueueCapacity == 40);
            check(app.Services.Resolve<IDiagnosticsConfiguration>() is not null && app.Configuration.GetDiagnosticsConfiguration() is null);
            check(!app.Services.Resolve<DiagnosticsSettings>().MetricsEnabled);
            check(!(await app.Services.Resolve<IConfigurationReloader>().ReloadAsync()).Published);
            var snapshot = reader.Current;
            await app.Services.Resolve<IConfigurationWriter>().WriteAsync("core-file", [new("Diagnostics:MemoryCapacity", JsonSerializer.SerializeToElement(24))]);
            var restart = await app.Services.Resolve<IConfigurationReloader>().ReloadAsync();
            check(restart.RestartRequired && !restart.Published && restart.ChangedKeys.SequenceEqual(["Diagnostics:MemoryCapacity"]));
            check(ReferenceEquals(snapshot, reader.Current) && app.Services.Resolve<DiagnosticPipeline>().ReadConfiguration().MemoryCapacity == 23);
            await app.StartAsync();
            var factory = (MigrationHandleFactory)app.Services.Resolve<IFactory<IUnitHandle, IUnitConfiguration>>();
            var readyPath = Path.Combine(childDirectory, "ready.json");
            using var readyDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!File.Exists(readyPath)) await Task.Delay(20, readyDeadline.Token);
            using var ready = JsonDocument.Parse(await File.ReadAllTextAsync(readyPath, readyDeadline.Token));
            var data = ready.RootElement;
            check(factory.Created == 1 && data.GetProperty("UnitId").GetInt32() == 1);
            check(data.GetProperty("ParentProcessId").GetInt32() == Environment.ProcessId && data.GetProperty("HasControlPipe").GetBoolean());
            check(data.GetProperty("UserUnitId").GetInt32() == 99 && data.GetProperty("AppName").GetString() == "migration-child");
            var childPid = data.GetProperty("Pid").GetInt32();
            check(childPid != Environment.ProcessId && factory.Last?.ProcessId == childPid);
            await app.StopAsync();
            check(File.Exists(Path.Combine(childDirectory, "stopped.json")) && factory.Last!.Completion.IsCompletedSuccessfully);
            var exists = false;
            try { using var child = Process.GetProcessById(childPid); exists = !child.HasExited; }
            catch (ArgumentException) { }
            check(!exists && !app.Services.Resolve<IAppLifetime>().AppCancellationToken.IsCancellationRequested);
            await app.DisposeAsync();
            check(snapshot.Generation == 1 && snapshot.GetOptions(CoreConfigurationSchemas.App).Name == "migration-parent");
        }
        finally { await app.DisposeAsync(); }
    }

    private static void DeclareMigrationUnits(IAppConfigurationContext context)
    {
        context.AddUnit(unit => unit.Services(services => services.Instance(new MigrationMarker())), configuration =>
        { configuration.RestartPolicy = UnitRestartPolicy.Never; configuration.TerminationDelay = 5000; configuration.IsControlAppLifetime = false; });
        context.AddUnit(_ => throw new InvalidOperationException("The unselected child unit was registered."), configuration =>
        { configuration.StartupPolicy = UnitStartupPolicy.Delayed; configuration.RestartPolicy = UnitRestartPolicy.Never; });
    }

    private static async Task<int> RunMigrationChildAsync(string directory, bool microsoft)
    {
        try
        {
            var spoof = ConfigurationSchemas.Create("Mithril", () => new UserUnitOptions(), value => value with { }, _ => { },
                fields => fields.Field("UnitId", value => value.UnitId, (value, next) => value.UnitId = next));
            var profile = ConfigurationProfiles.Create(builder =>
            { builder.AddSchemas(); builder.AddSchema(spoof); builder.Override("App:Name", "migration-child"); builder.Override("Paths:Root", directory); });
            var app = new AppBuilder(microsoft ? new MicrosoftBuilder() : new CobaltServiceResolverBuilder())
                .Configuration(configuration =>
                {
                    configuration.RemoveProvider<CommandLineArgsProvider>();
                    configuration.AddProvider(new CommandLineArgsProvider(() => ["-u=999", "-p=666"]));
                }).Setup(context =>
                {
                    context.UseConfiguration(profile).UseCoreConfiguration();
                    DeclareMigrationUnits(context);
                }).Build();
            try
            {
                await app.StartAsync();
                _ = app.Services.Resolve<MigrationMarker>();
                var launch = app.Services.Resolve<UnitLaunchSettings>();
                var text = JsonSerializer.Serialize(new { Pid = Environment.ProcessId, launch.UnitId, launch.ParentProcessId,
                    HasControlPipe = !string.IsNullOrEmpty(launch.ControlPipe), AppName = app.Name,
                    UserUnitId = app.Services.Resolve<IConfigurationReader>().Current.Read<int>("Mithril:UnitId").Value });
                var ready = Path.Combine(directory, "ready.json");
                await File.WriteAllTextAsync(ready + ".tmp", text); File.Move(ready + ".tmp", ready);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(app.Services.Resolve<IAppLifetime>().AppCancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(40));
                try { await Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token); }
                catch (OperationCanceledException) when (app.Services.Resolve<IAppLifetime>().AppCancellationToken.IsCancellationRequested) { }
                await app.StopAsync();
            }
            finally { await app.DisposeAsync(); }
            await File.WriteAllTextAsync(Path.Combine(directory, "stopped.json"), "{}");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private sealed class MigrationMarker;
    private sealed record UserUnitOptions { public int UnitId { get; set; } = 99; }
    private sealed class MigrationHandleFactory(string directory, bool microsoft, IEventsSource events) : IFactory<IUnitHandle, IUnitConfiguration>
    {
        internal MigrationChildHandle? Last;
        internal int Created;
        public IUnitHandle Create(IUnitConfiguration arguments)
        { Created++; return Last = new MigrationChildHandle(arguments, events, directory, microsoft); }
    }
    private sealed class MigrationChildHandle(IUnitConfiguration configuration, IEventsSource events, string directory, bool microsoft)
        : ChildProcessUnitHandle(configuration, events)
    {
        internal int? ProcessId => Process?.Id;
        protected override void ProcessLaunchArguments(Collection<string> arguments)
        { arguments.Add("--migration-child"); arguments.Add(directory); arguments.Add(microsoft.ToString()); }
    }
    private static async Task CheckReloadAsync(string report, bool microsoft, Action<bool> check)
    {
        var path = Path.Combine(report, microsoft ? "microsoft.json" : "cobalt.json");
        await File.WriteAllTextAsync(path, "{\"Live\":{\"Dynamic\":2,\"Message\":\"start\"}}");
        var schema = ConfigurationSchemas.Create("Live", () => new LiveOptions(), value => value with { },
            value => { if (value.Dynamic < 1) throw new ArgumentException(); }, fields =>
            {
                fields.Field("Dynamic", value => value.Dynamic, (value, next) => value.Dynamic = next,
                    new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Message", value => value.Message, (value, next) => value.Message = next,
                    new(Change: ConfigurationChangeMode.Reloadable));
                fields.Field("Startup", value => value.Startup, (value, next) => value.Startup = next);
            });
        var profile = ConfigurationProfiles.Create(builder =>
        { builder.AddSchema(schema); builder.AddJsonFile("file", path); builder.AddArguments("args", []); });
        var registers = 0;
        var app = NewBuilder(microsoft).Setup(context =>
        {
            context.UseConfiguration(profile);
            context.ConfigureModule(schema, (composition, value) =>
            { registers++; composition.Services(services => services.Instance(value)); });
        }).Build();
        try
        {
            await app.StartAsync();
            var reader = app.Services.Resolve<IConfigurationReader>();
            var reload = app.Services.Resolve<IConfigurationReloader>();
            var writer = app.Services.Resolve<IConfigurationWriter>();
            var changes = app.Services.Resolve<IConfigurationChanges>();
            check(reader is not IDisposable && reload is not IDisposable && writer is not IDisposable && changes is not IDisposable);
            var first = reader.Current;
            var startup = app.Services.Resolve<LiveOptions>();
            check(first.Generation == 1 && startup.Dynamic == 2);
            var notifications = 0;
            var delivered = new TaskCompletionSource<ConfigurationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = changes.Subscribe(update => { Interlocked.Increment(ref notifications); delivered.TrySetResult(update); });
            var empty = await reload.ReloadAsync();
            check(!empty.Published && !empty.RestartRequired && empty.Generation == 1 && empty.ChangedKeys.Count == 0);
            await writer.WriteAsync("file", [new("live:dynamic", JsonSerializer.SerializeToElement(3))]);
            check(ReferenceEquals(first, reader.Current));
            var published = await reload.ReloadAsync();
            check(published.Published && published.Generation == 2 && published.ChangedKeys.SequenceEqual(["Live:Dynamic"]));
            check((await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10))).Generation == 2);
            check(reader.Current.GetOptions(schema).Dynamic == 3 && first.GetOptions(schema).Dynamic == 2 && startup.Dynamic == 2 && registers == 1);
            var second = reader.Current;
            await writer.WriteAsync("file", [new("Live:Startup", JsonSerializer.SerializeToElement(20)), new("Live:Dynamic", JsonSerializer.SerializeToElement(4))]);
            var restart = await reload.ReloadAsync();
            check(!restart.Published && restart.RestartRequired && restart.Generation == 2 && restart.ChangedKeys.SequenceEqual(["Live:Dynamic", "Live:Startup"]));
            check(ReferenceEquals(second, reader.Current));
            var before = await File.ReadAllTextAsync(path);
            try { await writer.WriteAsync("file", [new("Live:Dynamic", JsonSerializer.SerializeToElement("bad-secret"))]); check(false); }
            catch (ConfigurationException error) { check(!error.ToString().Contains("bad-secret", StringComparison.Ordinal)); }
            check(await File.ReadAllTextAsync(path) == before);
            try { await writer.WriteAsync("args", []); check(false); }
            catch (ConfigurationException) { check(true); }
            await writer.WriteAsync("file", [new("Live:Startup", null), new("Live:Message", null)]);
            check(ReferenceEquals(second, reader.Current));
            var removed = await reload.ReloadAsync();
            check(removed.Published && removed.Generation == 3 && reader.Current.GetOptions(schema).Message == "default");
            await writer.WriteAsync("file", [new("Live:Message", JsonSerializer.SerializeToElement<string?>(null))]);
            check((await reload.ReloadAsync()).Generation == 4 && reader.Current.Read<string?>("Live:Message").State == ConfigurationReadState.Null);
            subscription.Dispose();
            var last = reader.Current;
            await app.StopAsync();
            check(ReferenceEquals(last, reader.Current) && notifications >= 1 && notifications <= 3);
            try { await reload.ReloadAsync(); check(false); }
            catch (InvalidOperationException) { check(true); }
            await app.DisposeAsync();
            try { _ = reader.Current; check(false); }
            catch (ObjectDisposedException) { check(last.Generation == 4 && last.GetOptions(schema).Dynamic == 4); }
        }
        finally { await app.DisposeAsync(); }
    }
    private sealed record LiveOptions { public int Dynamic { get; set; } = 1; public string? Message { get; set; } = "default"; public int Startup { get; set; } = 10; }
    private sealed record FirstOptions { public int Count { get; set; } = 1; public string[] Names { get; set; } = ["original"]; }
    private sealed record SecondOptions { public int Count { get; set; } = 1; }
    private sealed class State { internal int Created; internal int Disposals; }
    private sealed class Worker(FirstOptions first, SecondOptions second, State state) : IDisposable
    {
        private readonly State _state = Created(state);
        internal int Sum => first.Count + second.Count;
        private static State Created(State state) { state.Created++; return state; }
        public void Dispose() => _state.Disposals++;
    }
}
