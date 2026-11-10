using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Aether;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using TrueMoon.Mithril.Units;
using TrueMoon.Services;

namespace ConfigurationBootstrapConsumer;

// Headless process acceptance. Aether's runtime graph is prepared but never started.
internal static class Program
{
    private static async Task<int> Main(string[] arguments)
    {
        if (arguments is ["--bootstrap-child", var childDirectory, var provider])
            return await ChildAsync(childDirectory, bool.Parse(provider));
        var directory = Path.GetFullPath(arguments.Single());
        Directory.CreateDirectory(directory);
        var checks = 0;
        void Check(bool value) { if (!value) throw new InvalidOperationException("Bootstrap delivery check failed."); checks++; }
        var processes = new List<int>();
        foreach (var microsoft in new[] { false, true })
        {
            var root = Path.Combine(directory, microsoft ? "MicrosoftDI" : "Cobalt");
            Directory.CreateDirectory(root);
            var entry = Assembly.GetEntryAssembly()!.Location;
            var launch = new SessionLaunch(new(Guid.NewGuid(), 1, BuildIdentity.ForRuntime(entry), BuildIdentity.PreviewId, new('a', 64)),
                "engine-config-" + Guid.NewGuid().ToString("N"), Path.Combine(root, "runtime"), Role: RuntimeRole.EditPreview)
            { RuntimeIdentity = BuildIdentity.CurrentLaunchIdentity(entry), DiagnosticDelivery = new ChildDiagnosticDeliveryOptions { MaxBatchRecords = 4 } };
            var profile = ConfigurationProfiles.Create(builder => { builder.AddSchemas(); builder.Override("App:Name", "bootstrap-parent"); });
            var app = Builder(microsoft, child: false).Setup(context =>
            {
                context.UseConfiguration(profile).UseCoreConfiguration();
                context.Services(services => services.Singleton<IFactory<IUnitHandle, IUnitConfiguration>>(resolver =>
                    new HandleFactory(root, microsoft, launch, resolver.Resolve<IEventsSource<UnitsController>>())));
                DeclareUnit(context);
            }).Build();
            try
            {
                await app.StartAsync();
                var factory = (HandleFactory)app.Services.Resolve<IFactory<IUnitHandle, IUnitConfiguration>>();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                var readyPath = Path.Combine(root, "ready.json");
                while (!File.Exists(readyPath)) await Task.Delay(20, deadline.Token);
                using var ready = JsonDocument.Parse(await File.ReadAllTextAsync(readyPath, deadline.Token));
                var data = ready.RootElement;
                var pid = data.GetProperty("Pid").GetInt32();
                Check(pid != Environment.ProcessId && factory.Last?.ProcessId == pid);
                Check(factory.Created == 1 && data.GetProperty("UnitId").GetInt32() == 1);
                Check(data.GetProperty("ParentProcessId").GetInt32() == Environment.ProcessId);
                Check(data.GetProperty("ControlPipeCaptured").GetBoolean());
                Check(data.GetProperty("GpuValidation").GetBoolean());
                Check(data.GetProperty("HostContractId").GetString() == launch.Identity.HostContractId);
                Check(data.GetProperty("GameBuildId").GetString() == launch.Identity.GameBuildId);
                Check(data.GetProperty("ReportDirectory").GetString() == launch.ReportDirectory);
                Check(data.GetProperty("MaxBatchRecords").GetInt32() == 4);
                Check(data.GetProperty("BuildTimeoutSeconds").GetInt32() == 7);
                Check(data.GetProperty("OrdinaryUnitId").GetInt32() == 77);
                Check(data.GetProperty("AppName").GetString() == "bootstrap-child");
                Check(!Directory.Exists(launch.ReportDirectory));
                await app.StopAsync();
                Check(File.Exists(Path.Combine(root, "stopped.json")) && factory.Last!.Completion.IsCompletedSuccessfully);
                var alive = false;
                try { using var child = Process.GetProcessById(pid); alive = !child.HasExited; }
                catch (ArgumentException) { }
                Check(!alive);
                processes.Add(pid);
            }
            finally { await app.DisposeAsync(); }
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(new
        {
            Completed = true, Checks = checks, Providers = new[] { "Cobalt", "MicrosoftDI" },
            ChildProcessRun = true, Children = processes, ReapedChildren = processes.Count,
            GpuRun = false, WindowRun = false, NativeRuntimeStarted = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private static IAppBuilder Builder(bool microsoft, bool child) => App.Builder(context => { if (microsoft) context.UseDI(); })
        .Configuration(configuration =>
        {
            configuration.RemoveProvider<CommandLineArgsProvider>();
            if (!child) configuration.RemoveProvider<EnvironmentVariablesProvider>();
            else configuration.AddProvider(new CommandLineArgsProvider(() => ["-u=999", "-p=777"]));
        });

    private static void DeclareUnit(IAppConfigurationContext context)
        => context.AddUnit(unit => unit.Services(services => services.Instance(new Marker())), options =>
        { options.RestartPolicy = UnitRestartPolicy.Never; options.TerminationDelay = 5000; options.IsControlAppLifetime = false; });

    private static async Task<int> ChildAsync(string directory, bool microsoft)
    {
        try
        {
            // This is the real child's OS boundary, before any App/DI graph exists.
            var bootstrap = RuntimeBootstrap.ReadEnvironment();
            var probeProfile = ConfigurationProfiles.Create(builder =>
            {
                builder.AddSchema(AetherConfigurationSchemas.Engine);
                builder.Override("Aether:ReportDirectory", Path.Combine(directory, "spoofed"));
                builder.Override("Aether:ChildDiagnostics:MaxBatchRecords", 12);
                builder.Override("Aether:BuildTimeout", TimeSpan.FromSeconds(7));
            });
            AetherSettings prepared;
            await using (var probe = Builder(microsoft, child: true).Setup(context => context.UseConfiguration(probeProfile)
                .UseConfiguredAetherRuntime(bootstrap, value =>
                { value.ReportDirectory = "callback-spoof"; value.ChildDiagnostics.MaxBatchRecords = 17; value.GpuValidation = false; })).Build())
            {
                prepared = probe.Services.Resolve<IConfigurationReader>().Current.GetOptions(AetherConfigurationSchemas.Engine);
                if (!probe.Services.Resolve<RuntimeStartupSettings>().GpuValidation || !bootstrap.Settings.GpuValidation)
                    throw new InvalidOperationException("Captured startup flags were replaced.");
                await using var parent = probe.Services.Resolve<IFactory<UnitParentProcessEventsHandler>>().Create()!;
                if (!parent.Completion.IsCompletedSuccessfully) throw new InvalidOperationException("Parent monitor was activated during Build.");
            }
            var ordinary = ConfigurationSchemaDefinitions.Create("Mithril", () => new Ordinary(), value => value with { }, _ => { },
                fields => fields.Field("UnitId", value => value.UnitId, (value, next) => value.UnitId = next));
            var profile = ConfigurationProfiles.Create(builder =>
            { builder.AddSchemas(); builder.AddSchema(ordinary); builder.Override("App:Name", "bootstrap-child"); });
            var app = Builder(microsoft, child: true).Setup(context =>
            { context.UseConfiguration(profile).UseCoreConfiguration(); DeclareUnit(context); }).Build();
            try
            {
                await app.StartAsync();
                _ = app.Services.Resolve<Marker>();
                var unit = app.Services.Resolve<UnitLaunchSettings>();
                var data = new
                {
                    Pid = Environment.ProcessId, unit.UnitId, unit.ParentProcessId, AppName = app.Name,
                    ControlPipeCaptured = !string.IsNullOrEmpty(bootstrap.ParentLaunch?.ControlPipe) && bootstrap.ParentLaunch.ControlPipe == unit.ControlPipe,
                    bootstrap.Settings.GpuValidation, bootstrap.Launch.Identity.HostContractId, bootstrap.Launch.Identity.GameBuildId,
                    prepared.ReportDirectory, prepared.ChildDiagnostics.MaxBatchRecords,
                    BuildTimeoutSeconds = (int)prepared.BuildTimeout.TotalSeconds,
                    OrdinaryUnitId = app.Services.Resolve<IConfigurationReader>().Current.Read<int>("Mithril:UnitId").Value
                };
                var ready = Path.Combine(directory, "ready.json");
                await File.WriteAllTextAsync(ready + ".tmp", JsonSerializer.Serialize(data)); File.Move(ready + ".tmp", ready);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(app.Services.Resolve<IAppLifetime>().AppCancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
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

    private sealed class Marker;
    private sealed record Ordinary { public int UnitId { get; set; } = 77; }
    private sealed class HandleFactory(string directory, bool microsoft, SessionLaunch launch, IEventsSource events)
        : IFactory<IUnitHandle, IUnitConfiguration>
    {
        internal ChildHandle? Last;
        internal int Created;
        public IUnitHandle Create(IUnitConfiguration arguments)
        { Created++; return Last = new ChildHandle(arguments, events, directory, microsoft, launch); }
    }
    private sealed class ChildHandle(IUnitConfiguration configuration, IEventsSource events, string directory, bool microsoft, SessionLaunch launch)
        : ChildProcessUnitHandle(configuration, events)
    {
        internal int? ProcessId => Process?.Id;
        protected override void ProcessLaunchArguments(Collection<string> arguments)
        { arguments.Add("--bootstrap-child"); arguments.Add(directory); arguments.Add(microsoft.ToString()); }
        protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
        {
            var info = await base.GetProcessStartInfoAsync(cancellationToken);
            info.Environment[NativeRuntimeFactory.BootstrapKey] = RuntimeBootstrap.Encode(launch);
            info.Environment[RuntimeStartupSettings.LegacyValidationKey] = "0";
            info.Environment[RuntimeStartupSettings.ValidationKey] = "true";
            return info;
        }
    }
}
