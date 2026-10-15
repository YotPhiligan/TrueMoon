using System.Collections.Concurrent;
using TrueMoon;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Services;

namespace Headless.Consumer;

/// <summary>Per-process evidence shared by the consumer's owned services.</summary>
public sealed class ProbeState
{
    /// <summary>Creates evidence for one bounded scenario.</summary>
    /// <param name="scenario">Normal, cancellation or fault scenario.</param>
    /// <param name="workDirectory">Fresh directory owned by this scenario.</param>
    public ProbeState(string scenario, string workDirectory)
    {
        Scenario = scenario;
        Borrowed = new BorrowedResource();
        JsonPath = Path.Combine(workDirectory, "consumer.json");
        ResourcePath = Path.Combine(workDirectory, "owned-resource.txt");
    }
    /// <summary>Selected fault/lifetime behavior.</summary>
    public string Scenario { get; }
    /// <summary>Caller-owned instance which the resolver must preserve.</summary>
    public BorrowedResource Borrowed { get; }
    internal readonly ConcurrentQueue<string> Trace = new();
    internal readonly ConcurrentQueue<DiagnosticEvent> Diagnostics = new();
    internal readonly ConcurrentQueue<DiagnosticEvent> ObsoleteDiagnostics = new();
    internal readonly List<CancellationToken> ShutdownTokens = [];
    internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly Exception StartError = new InvalidOperationException("consumer startup failure");
    internal readonly Exception StopError = new InvalidOperationException("consumer shutdown failure");
    internal readonly Exception BuildError = new InvalidOperationException("consumer creation failure");
    internal readonly Exception ConfigurationError = new InvalidOperationException("consumer configuration failure");
    internal readonly Exception CleanupError = new InvalidOperationException("consumer cleanup failure");
    internal readonly string JsonPath, ResourcePath;
    internal int ResourceOpens, ResourceReleases;
    internal bool ConfigurationVerified;
    internal IEventsSource? Source;
    internal int WorkerDisposeCalls, WorkerReleases, GateDisposals, FactoryDisposals, TicketDisposals;
}

/// <summary>Simple generic dependency with a separately registered closed form.</summary>
public interface IRepository<T>
{
    /// <summary>The resolved type and converted configuration value.</summary>
    string Value { get; }
}

/// <summary>Constructed by generated open/closed generic resolvers or Microsoft DI.</summary>
/// <param name="configuration">App-owned configuration with higher-priority consumer settings.</param>
public sealed class Repository<T>(IConfiguration configuration) : IRepository<T>
{
    /// <inheritdoc />
    public string Value { get; } = $"{typeof(T).Name}:{configuration.Get<int>("Answer", "consumer")}";
}

/// <summary>Caller-owned instance registration.</summary>
public sealed class BorrowedResource : IDisposable
{
    /// <summary>Number of calls to dispose this borrowed resource.</summary>
    public int Disposals { get; private set; }
    /// <inheritdoc />
    public void Dispose() => Disposals++;
}

/// <summary>Resolver-owned value constructed by an explicit runtime factory.</summary>
/// <param name="state">Scenario evidence.</param>
public sealed class OwnedFactory(ProbeState state) : IDisposable
{
    private readonly FileStream _resource = Open(state);
    private int _released;

    private static FileStream Open(ProbeState state)
    {
        var resource = new FileStream(state.ResourcePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        try
        {
            resource.Write("owned-resource"u8);
            resource.Flush();
        }
        catch { resource.Dispose(); throw; }
        state.ResourceOpens++;
        return resource;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        state.FactoryDisposals++;
        if (Interlocked.Exchange(ref _released, 1) != 0) return;
        _resource.Dispose();
        state.ResourceReleases++;
        if (state.Scenario == "cleanup-failure") throw state.CleanupError;
    }
}

/// <summary>Configuration object created over a JSON null entry.</summary>
public sealed class ConsumerOptions
{
    /// <summary>Nonzero default persisted by GetOrCreate.</summary>
    public int BatchSize { get; set; } = 23;
}

/// <summary>Generated disposable transient used twice by the worker.</summary>
/// <param name="state">Scenario evidence.</param>
public sealed class Ticket(ProbeState state) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => state.TicketDisposals++;
}

/// <summary>Shared Composite service. Dispose supports repeated Microsoft DI alias calls.</summary>
/// <param name="state">Scenario evidence.</param>
/// <param name="lifetime">Application cancellation and lifecycle callbacks.</param>
/// <param name="services">The selected provider's resolver.</param>
/// <param name="configuration">Merged application configuration.</param>
/// <param name="numbers">Open generic dependency.</param>
/// <param name="strings">Explicitly registered closed generic dependency.</param>
/// <param name="events">Factory owning the worker diagnostic source.</param>
public sealed class Worker(ProbeState state, IAppLifetime lifetime, IServiceResolver services,
    IConfiguration configuration, IRepository<int> numbers, IRepository<string> strings, IEventsSourceFactory events)
    : IStartable, IStoppable, IDisposable
{
    private int _released;
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.Trace.Enqueue("worker.Start");
        ConsumerProbe.VerifyConfiguration(configuration, state);
        ConsumerProbe.Require(services.ResolveAll<DiagnosticSubscription>().Count() == 1
            && services.ResolveAll<IEventsSourceFactory>().Count() == 1, "Repeated diagnostics registrations leaked.");
        ConsumerProbe.Require(ReferenceEquals(this, services.Resolve<Worker>())
            && ReferenceEquals(this, services.Resolve<IStoppable>())
            && services.ResolveAll<IStartable>().Any(service => ReferenceEquals(this, service)), "Composite identity failed.");
        ConsumerProbe.Require(ReferenceEquals(lifetime, services.Resolve<IAppLifetimeHandler>()), "Lifetime aliases differ.");
        ConsumerProbe.Require(numbers.Value == "Int32:17" && strings.Value == "String:17"
            && ReferenceEquals(numbers, services.Resolve<IRepository<int>>())
            && ReferenceEquals(strings, services.Resolve<IRepository<string>>()), "Generic/configuration graph failed.");
        ConsumerProbe.Require(configuration.GetName() == "package-headless", "Configuration write/read failed.");
        ConsumerProbe.Require(ReferenceEquals(services.Resolve<OwnedFactory>(), services.Resolve<OwnedFactory>()), "Factory singleton identity failed.");
        ConsumerProbe.VerifyResourceHeld(state);
        ConsumerProbe.Require(ReferenceEquals(services.Resolve<BorrowedResource>(), state.Borrowed), "Borrowed identity failed.");
        var first = services.Resolve<Ticket>(); var second = services.Resolve<Ticket>();
        ConsumerProbe.Require(first != null && second != null && !ReferenceEquals(first, second), "Transient identity failed.");
        lifetime.OnStopping(() => state.Trace.Enqueue("Stopping"));
        lifetime.OnStopped(() => state.Trace.Enqueue("Stopped"));
        state.Source = events.Create("Headless.Consumer.Worker");
        state.Source.Write(() => "worker-start", caller: "Start");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        state.Trace.Enqueue("worker.Stop"); state.ShutdownTokens.Add(cancellationToken);
        ConsumerProbe.VerifyResourceHeld(state);
        state.Source!.Write(() => "worker-stop", caller: "Stop");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        state.WorkerDisposeCalls++;
        if (Interlocked.Exchange(ref _released, 1) == 0) state.WorkerReleases++;
    }
}

/// <summary>Second startup service driving completion, cancellation synchronization or faults.</summary>
/// <param name="state">Scenario evidence.</param>
/// <param name="lifetime">Application lifetime used for normal completion.</param>
public sealed class GateService(ProbeState state, IAppLifetime lifetime) : IStartable, IStoppable, IDisposable
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        state.Trace.Enqueue("gate.Start");
        if (state.Scenario is "start-failure" or "cleanup-failure") throw state.StartError;
        state.Started.TrySetResult();
        if (state.Scenario != "cancel") lifetime.Cancel();
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        state.Trace.Enqueue("gate.Stop"); state.ShutdownTokens.Add(cancellationToken);
        if (state.Scenario == "stop-failure") throw state.StopError;
        return Task.CompletedTask;
    }
    /// <inheritdoc />
    public void Dispose() => state.GateDisposals++;
}

internal sealed class ConsumerPaths(string directory) : IPathResolver
{
    public string ResolvePath(Paths path) => directory;
}

internal sealed class ConsumerConfigurationProvider(string value) : IConfigurationProvider
{
    public string Name => "consumer";
    private readonly IConfigurationSection[] _sections = [new ConfigurationSection("consumer", new() { ["Answer"] = value })];
    public IReadOnlyList<IConfigurationSection> GetSections() => _sections;
}
