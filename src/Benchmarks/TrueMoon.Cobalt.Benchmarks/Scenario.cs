using TrueMoon.Configuration;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

// Fixed caller graph v1: 12 registrations / 10 service keys, no external I/O.
public sealed class Scenario
{
    private readonly Action<IConfiguration, IServicesRegistrationContext>[] _registrations;
    private readonly Action<IAppConfigurationContext> _configure;
    public BorrowedSettings Settings { get; } = new();

    public Scenario()
    {
        _registrations = [(_, services) => Register(services)];
        _configure = context => context.Services(Register);
    }

    public IServiceResolver CreateResolver() => new CobaltServiceResolverBuilder().Build(null!, _registrations);
    public IApp CreateApp() => App.Build(_configure);

    public void Register(IServicesRegistrationContext services) => services
        .Instance(Settings)
        .Singleton<OwnedResource>(_ => new OwnedResource(Settings))
        .Singleton(typeof(IRepository<>), typeof(Repository<>))
        .Singleton<IRepository<string>, StringRepository>()
        .Transient<Ticket>()
        .Composite<Worker, IStartable, IStoppable>()
        .Singleton<IPlugin, FirstPlugin>()
        .Singleton<IPlugin, SecondPlugin>()
        .Transient<IPlugin, ThirdPlugin>()
        .Transient<Request>();
}

public sealed class BorrowedSettings : IDisposable
{
    public int Value { get; } = 42;
    public int DisposeCalls { get; private set; }
    public void Dispose() => DisposeCalls++;
}

public sealed class OwnedResource(BorrowedSettings settings) : IDisposable
{
    public BorrowedSettings Settings { get; } = settings;
    public int DisposeCalls { get; private set; }
    public void Dispose() => DisposeCalls++;
}

public interface IRepository<T> { OwnedResource Resource { get; } }
public sealed class Repository<T>(OwnedResource resource) : IRepository<T>
{
    public OwnedResource Resource { get; } = resource;
}
public sealed class StringRepository(OwnedResource resource) : IRepository<string>
{
    public OwnedResource Resource { get; } = resource;
}

public sealed class Worker(OwnedResource resource, IRepository<int> numbers, IRepository<string> strings) : IStartable, IStoppable, IDisposable
{
    public OwnedResource Resource { get; } = resource;
    public IRepository<int> Numbers { get; } = numbers;
    public IRepository<string> Strings { get; } = strings;
    public int Starts { get; private set; }
    public int Stops { get; private set; }
    public int DisposeCalls { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Starts++; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); Stops++; return Task.CompletedTask; }
    public void Dispose() => DisposeCalls++;
}

// Hot transients deliberately have no disposal contract: repeated measurement must not
// grow the root resolver's owned-disposable list over millions of invocations.
public sealed class Ticket(OwnedResource resource) { public OwnedResource Resource { get; } = resource; }
public interface IPlugin { int Order { get; } }
public sealed class FirstPlugin : IPlugin { public int Order => 1; }
public sealed class SecondPlugin : IPlugin { public int Order => 2; }
public sealed class ThirdPlugin : IPlugin { public int Order => 3; }
public sealed class Request(Worker worker, Ticket ticket, IEnumerable<IPlugin> plugins)
{
    public Worker Worker { get; } = worker;
    public Ticket Ticket { get; } = ticket;
    public IPlugin[] Plugins { get; } = plugins.ToArray();
}
