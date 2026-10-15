using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using TrueMoon;
using TrueMoon.Cobalt;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Extensions.DependencyInjection;

namespace Headless.Consumer;

internal static class ConsumerProbe
{
    internal static readonly string[] Scenarios = ["normal", "cancel", "start-failure", "stop-failure", "build-failure", "config-failure", "cleanup-failure"];

    internal static bool Supports(string provider, string scenario)
        => Scenarios.Contains(scenario) && (scenario != "cleanup-failure" || provider == "cobalt");

    internal static async Task<int> RunAsync(string provider, string scenario, string reportPath)
    {
        var workDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "work", Path.GetFileNameWithoutExtension(reportPath));
        if (Directory.Exists(workDirectory)) throw new IOException("Use a fresh report/work directory.");
        Directory.CreateDirectory(workDirectory);
        var state = new ProbeState(scenario, workDirectory);
        string? failure = null;
        Exception? observed = null;
        try
        {
            File.WriteAllText(state.JsonPath, "{\"Checkpoint\":5,\"ConsumerOptions\":null}");
#if COBALT_GENERATION
            // This compile-time reference also makes the negative analyzer control deterministic.
            var generated = typeof(RHeadlessConsumer.Cobalt.Generated.CobaltResolversRegistration);
            Require(generated.Assembly == typeof(Worker).Assembly, "Consumer generation must occur in its own assembly.");
            var registrations = ServiceResolvers.Shared.GetResolvers();
            Require(registrations.ContainsKey(typeof(Worker)) && registrations.ContainsKey(typeof(IRepository<>))
                && registrations.ContainsKey(typeof(IRepository<string>)) && registrations.ContainsKey(typeof(Ticket)),
                "Consumer module initializer did not publish the generated graph.");
#else
            Require(provider == "microsoft", "The Cobalt consumer requires its generator package.");
#endif

            using var external = new CancellationTokenSource();
            var builder = App.Builder(context => { if (provider == "microsoft") context.UseDI(); })
                .Configuration(configuration =>
                {
                    configuration.AddProvider(new JsonConfigurationProvider(new ConsumerPaths(workDirectory)));
                    configuration.AddProvider(new ConsumerConfigurationProvider("7"));
                    configuration.AddProvider(new ConsumerConfigurationProvider("17"));
                });
            var run = builder.RunAsync(context => context
                .UseDiagnostics(diagnostics => diagnostics.AddEventListener(state.ObsoleteDiagnostics.Enqueue))
                .UseDiagnostics(diagnostics =>
                {
                    diagnostics.AddFilter("Headless.Consumer");
                    diagnostics.AddEventListener(_ => throw new InvalidOperationException("isolated observer failure"));
                    diagnostics.AddEventListener(state.Diagnostics.Enqueue);
                })
                .Configuration(configuration =>
                {
                    configuration.Set("appName", "package-headless");
                    if (scenario == "config-failure") throw state.ConfigurationError;
                })
                .Services(services =>
                {
                    services.Instance(state).Instance(state.Borrowed)
                        .Singleton<OwnedFactory>(_ => new OwnedFactory(state))
                        .Singleton(typeof(IRepository<>), typeof(Repository<>))
                        .Singleton<IRepository<string>, Repository<string>>()
                        .Transient<Ticket>()
                        .Composite<Worker, IStartable, IStoppable>()
                        .Singleton<IStartable, GateService>();
                    if (scenario == "build-failure")
                        services.RemoveAll<IApp>().Singleton<IApp>(resolver =>
                        {
                            resolver.Resolve<OwnedFactory>();
                            VerifyResourceHeld(state);
                            VerifyConfiguration(resolver.Resolve<IConfiguration>()!, state);
                            state.Source = resolver.Resolve<IEventsSourceFactory>()!.Create("Headless.Consumer.Build");
                            state.Source.Exception(state.BuildError, caller: "failure");
                            throw state.BuildError;
                        });
                }), external.Token);
            try
            {
                if (scenario == "cancel")
                {
                    await state.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    external.Cancel();
                }
                await run.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (Exception error) { observed = error; }

            if (scenario == "cancel")
                Require(observed is OperationCanceledException cancellation && cancellation.CancellationToken == external.Token,
                    "External cancellation must reach the caller with its original token.");
            else if (scenario == "start-failure") Require(ReferenceEquals(observed, state.StartError), "Original startup error lost.");
            else if (scenario == "stop-failure") Require(ReferenceEquals(observed, state.StopError), "Original shutdown error lost.");
            else if (scenario == "build-failure") Require(observed != null && Contains(observed, state.BuildError), "Original creation error lost.");
            else if (scenario == "config-failure") Require(ReferenceEquals(observed, state.ConfigurationError), "Original configuration error lost.");
            else if (scenario == "cleanup-failure") Require(observed is AggregateException aggregate
                && aggregate.Flatten().InnerExceptions.Count == 2 && Contains(observed, state.StartError)
                && Contains(observed, state.CleanupError), "Startup/cleanup errors were not both preserved.");
            else Require(observed == null, "Normal lifetime cancellation must complete successfully.");

            string[] expected = scenario switch
            {
                "build-failure" or "config-failure" => [],
                "start-failure" or "cleanup-failure" => ["worker.Start", "gate.Start", "worker.Stop", "Stopping", "Stopped"],
                _ => ["worker.Start", "gate.Start", "Stopping", "gate.Stop", "worker.Stop", "Stopped"]
            };
            Require(state.Trace.ToArray().SequenceEqual(expected), "Unexpected startup/rollback/shutdown order.");
            Require(state.FactoryDisposals == (scenario == "config-failure" ? 0 : 1) && state.Borrowed.Disposals == 0,
                "Owned factory / borrowed instance ownership failed.");
            if (scenario is not ("build-failure" or "config-failure"))
            {
                Require(state.WorkerReleases == 1 && state.GateDisposals == 1 && state.TicketDisposals == 2,
                    "Owned service/transient cleanup did not finish exactly once.");
                Require(provider == "cobalt" ? state.WorkerDisposeCalls == 1 : state.WorkerDisposeCalls >= 1,
                    "Cobalt must dispose shared aliases once; Microsoft DI follows its own alias semantics.");
                Require(state.ShutdownTokens.All(token => token.CanBeCanceled && !token.IsCancellationRequested),
                    "Rollback/shutdown reused the canceled run token.");
                Require(state.Diagnostics.Any(item => item.Name.StartsWith("TrueMoon.DefaultApp.", StringComparison.Ordinal)),
                    "No framework lifecycle diagnostics observed.");
            }
            else Require(state.WorkerReleases == 0 && state.TicketDisposals == 0, "Build failure started unrelated services.");
            Require(scenario == "config-failure" || state.Diagnostics.Any(item => item.Name.StartsWith("Headless.Consumer.", StringComparison.Ordinal)),
                "No consumer diagnostics observed.");
            Require(state.ObsoleteDiagnostics.IsEmpty, "Superseded diagnostics configuration observed events.");
            Require(state.ResourceOpens == (scenario == "config-failure" ? 0 : 1)
                && state.ResourceReleases == state.ResourceOpens, "File resource ownership failed.");
            if (scenario == "config-failure")
                Require(!File.Exists(state.ResourcePath) && !state.ConfigurationVerified, "Configuration failure acquired external resources.");
            else
            {
                Require(state.ConfigurationVerified, "JSON configuration path was not verified.");
                using var reopened = new FileStream(state.ResourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var reader = new StreamReader(reopened);
                Require(reader.ReadToEnd() == "owned-resource", "Resource was not released with its contents intact.");
            }
            if (scenario == "build-failure") Require(state.Diagnostics.Any(item => ReferenceEquals(item.Payload, state.BuildError)), "Diagnostic error identity lost.");
            var before = state.Diagnostics.Count;
            using var after = new DiagnosticListener("Headless.Consumer.AfterShutdown");
            Require(!after.IsEnabled(), "Resolver-owned diagnostics subscription leaked.");
            after.Write("after", DiagnosticEvent.Create("must not be observed"));
            Require(state.Diagnostics.Count == before, "Diagnostics delivered after shutdown.");
            if (scenario != "config-failure")
            {
                try { state.Source!.Trace(); throw new InvalidOperationException("Owned source was not disposed."); }
                catch (ObjectDisposedException) { }
            }
        }
        catch (Exception error) { failure = error.ToString(); }

        var report = new
        {
            Completed = failure == null, Provider = provider, Scenario = scenario,
            Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
            Configuration = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
            FrameworkVersion = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
#if COBALT_GENERATION
            ConsumerGeneration = true,
#else
            ConsumerGeneration = false,
#endif
            ObservedError = observed?.GetType().FullName, Trace = state.Trace.ToArray(),
            state.WorkerDisposeCalls, state.WorkerReleases, state.GateDisposals, state.FactoryDisposals, state.TicketDisposals,
            BorrowedDisposals = state.Borrowed.Disposals,
            state.ConfigurationVerified, state.ResourceOpens, state.ResourceReleases,
            JsonPath = state.JsonPath, ResourcePath = state.ResourcePath,
            ObsoleteDiagnosticCount = state.ObsoleteDiagnostics.Count,
            DiagnosticNames = state.Diagnostics.Select(item => item.Name).ToArray(), Failure = failure
        };
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(failure == null ? $"Headless {provider}/{scenario}: passed." : failure);
        return failure == null ? 0 : 1;
    }

    internal static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    internal static void VerifyResourceHeld(ProbeState state)
    {
        try
        {
            using var unexpected = new FileStream(state.ResourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            throw new InvalidOperationException("Owned resource was released before shutdown/disposal.");
        }
        catch (IOException) { }
    }

    internal static void VerifyConfiguration(IConfiguration configuration, ProbeState state)
    {
        var options = configuration.GetOrCreate<ConsumerOptions>();
        Require(options.BatchSize == 23 && ReferenceEquals(options, configuration.GetOrCreate<ConsumerOptions>()),
            "GetOrCreate did not replace the winning JSON null.");
        var persisted = new JsonConfigurationSection(state.JsonPath).Get<ConsumerOptions>(nameof(ConsumerOptions));
        Require(persisted?.BatchSize == 23, "Created configuration was not persisted to JSON.");
        var original = File.ReadAllText(state.JsonPath);
        var refreshable = (CommonConfiguration)configuration;
        File.WriteAllText(state.JsonPath, "broken");
        try
        {
            try { refreshable.Refresh(); throw new InvalidOperationException("Malformed JSON refresh succeeded."); }
            catch (JsonException) { }
            Require(configuration.Get<int>("Checkpoint", "consumer") == 5
                && ReferenceEquals(options, configuration.Get<ConsumerOptions>()), "Failed refresh changed the last valid snapshot.");
        }
        finally { File.WriteAllText(state.JsonPath, original); }
        refreshable.Refresh();
        Require(configuration.Get<int>("Answer", "consumer") == 17
            && configuration.Get<ConsumerOptions>()?.BatchSize == 23, "Configuration recovery/priority failed.");
        state.ConfigurationVerified = true;
    }

    private static bool Contains(Exception actual, Exception expected)
        => ReferenceEquals(actual, expected) || actual.InnerException is { } inner && Contains(inner, expected)
            || actual is AggregateException aggregate && aggregate.InnerExceptions.Any(error => Contains(error, expected));
}
