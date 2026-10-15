using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

internal static class WorkloadVerification
{
    public static async Task VerifyAsync()
    {
        var scenario = new Scenario();
        var context = new CobaltServicesRegistrationContext();
        scenario.Register(context);
        var handles = context.GetContainer().GetHandles();
        Require(handles.Count == 12 && handles.Select(h => h.ServiceType).Distinct().Count() == 10, "Graph shape changed.");
        var resolver = scenario.CreateResolver();
        Worker worker;
        OwnedResource resource;
        using ((IDisposable)resolver)
        {
            var first = resolver.Resolve<Request>(); var second = resolver.Resolve<Request>();
            worker = first.Worker; resource = worker.Resource;
            Require(!ReferenceEquals(first, second) && !ReferenceEquals(first.Ticket, second.Ticket), "Transient request/ticket identity.");
            Require(ReferenceEquals(worker, second.Worker) && ReferenceEquals(worker, resolver.Resolve<IStartable>()) &&
                ReferenceEquals(worker, resolver.Resolve<IStoppable>()), "Composite singleton aliases.");
            Require(ReferenceEquals(resource, first.Ticket.Resource) && ReferenceEquals(resource, worker.Numbers.Resource) &&
                ReferenceEquals(resource, worker.Strings.Resource) && ReferenceEquals(scenario.Settings, resource.Settings), "Shared factory dependency graph.");
            Require(worker.Numbers.GetType() == typeof(Repository<int>) && worker.Strings.GetType() == typeof(StringRepository), "Open/closed mapping.");
            Require(ReferenceEquals(worker.Numbers, resolver.Resolve<IRepository<int>>()) &&
                ReferenceEquals(worker.Strings, resolver.Resolve<IRepository<string>>()) &&
                !ReferenceEquals(worker.Numbers, resolver.Resolve<IRepository<long>>()), "Closed generic singleton isolation.");
            Require(first.Plugins.Select(p => p.Order).SequenceEqual(new[] { 1, 2, 3 }) && second.Plugins.Length == 3,
                "Ordered multiple registrations.");
            Require(ReferenceEquals(first.Plugins[0], second.Plugins[0]) && ReferenceEquals(first.Plugins[1], second.Plugins[1]) &&
                !ReferenceEquals(first.Plugins[2], second.Plugins[2]), "Multiple registration lifetimes.");
            using var other = (IDisposable)scenario.CreateResolver();
            Require(!ReferenceEquals(worker, ((IServiceResolver)other).Resolve<Worker>()), "Resolver singleton isolation.");
        }
        Require(worker.DisposeCalls == 1 && resource.DisposeCalls == 1 && scenario.Settings.DisposeCalls == 0, "Owned/borrowed cleanup.");

        var app = scenario.CreateApp();
        var services = (IServiceResolver)app.Services;
        var appWorker = services.Resolve<Worker>();
        try
        {
            await app.StartAsync(); await app.StartAsync();
            Require(appWorker.Starts == 1 && appWorker.Stops == 0, "App startup.");
            Require(ReferenceEquals(appWorker, services.Resolve<IStartable>()), "App alias identity.");
            await app.StopAsync(); await app.StopAsync();
            Require(appWorker.Starts == 1 && appWorker.Stops == 1, "App shutdown.");
        }
        finally { await app.DisposeAsync(); }
        Require(appWorker.DisposeCalls == 1 && appWorker.Resource.DisposeCalls == 1 && scenario.Settings.DisposeCalls == 0,
            "App ownership cleanup.");
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
