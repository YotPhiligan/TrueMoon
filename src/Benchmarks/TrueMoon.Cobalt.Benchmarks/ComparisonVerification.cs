using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

internal static class ComparisonVerification
{
    private static void Require(bool value, string message) => WorkloadVerification.Require(value, message);

    public static async Task<object> VerifyAsync(Provider provider)
    {
        var cases = new List<object>();
        cases.Add(new { Name = nameof(VerifyNormalGraph), Detail = VerifyNormalGraph(provider) });
        cases.Add(new { Name = nameof(VerifyNormalAppAsync), Detail = await VerifyNormalAppAsync(provider) });
        cases.Add(new { Name = nameof(VerifyModules), Detail = VerifyModules(provider) });
        cases.Add(new { Name = nameof(VerifyMissingDependency), Detail = VerifyMissingDependency(provider) });
        cases.Add(new { Name = nameof(VerifyFactoryRecovery), Detail = VerifyFactoryRecovery(provider) });
        cases.Add(new { Name = nameof(VerifyConstructorRecovery), Detail = VerifyConstructorRecovery(provider) });
        cases.Add(new { Name = nameof(VerifyCycleRecovery), Detail = VerifyCycleRecovery(provider) });
        cases.Add(new { Name = nameof(VerifyStartupRollbackAsync), Detail = await VerifyStartupRollbackAsync(provider) });
        cases.Add(new { Name = nameof(VerifyRunAsync), Detail = await VerifyRunAsync(provider, false) });
        cases.Add(new { Name = "VerifyExternalCancellationAsync", Detail = await VerifyRunAsync(provider, true) });
        return new { Completed = true, Provider = provider.ToString(), Graph = "cobalt-app-v1", Cases = cases };
    }

    private static int Disposals(Provider provider, int resolvedAliases = 2) => provider == Provider.Cobalt ? 1 : 1 + resolvedAliases;

    private static string VerifyNormalGraph(Provider provider)
    {
        var scenario = new ComparisonScenario(provider);
        var resolver = scenario.CreateResolver();
        var first = resolver.Resolve<Request>(); var second = resolver.Resolve<Request>();
        var worker = first.Worker; var resource = worker.Resource;
        using ((IDisposable)resolver)
        {
            Require(!ReferenceEquals(first, second) && !ReferenceEquals(first.Ticket, second.Ticket), "Transient identity.");
            Require(ReferenceEquals(worker, second.Worker) && ReferenceEquals(worker, resolver.Resolve<IStartable>()) &&
                ReferenceEquals(worker, resolver.Resolve<IStoppable>()), "Composite alias identity.");
            Require(ReferenceEquals(resource, first.Ticket.Resource) && ReferenceEquals(resource, worker.Numbers.Resource) &&
                ReferenceEquals(resource, worker.Strings.Resource) && ReferenceEquals(scenario.Graph.Settings, resource.Settings), "Factory/shared dependencies.");
            Require(worker.Numbers.GetType() == typeof(Repository<int>) && worker.Strings.GetType() == typeof(StringRepository), "Open/exact closed types.");
            Require(ReferenceEquals(worker.Numbers, resolver.Resolve<IRepository<int>>()) &&
                ReferenceEquals(worker.Strings, resolver.Resolve<IRepository<string>>()) &&
                !ReferenceEquals(worker.Numbers, resolver.Resolve<IRepository<long>>()), "Generic singleton identity.");
            Require(first.Plugins.Select(p => p.Order).SequenceEqual(new[] { 1, 2, 3 }) && second.Plugins.Length == 3,
                "Multiple registration order.");
            Require(ReferenceEquals(first.Plugins[0], second.Plugins[0]) && ReferenceEquals(first.Plugins[1], second.Plugins[1]) &&
                !ReferenceEquals(first.Plugins[2], second.Plugins[2]), "Plugin lifetimes.");
            using var other = (IDisposable)scenario.CreateResolver();
            Require(!ReferenceEquals(worker, ((IServiceResolver)other).Resolve<Worker>()), "Resolver isolation.");
        }
        Require(worker.DisposeCalls == Disposals(provider) && resource.DisposeCalls == 1 && scenario.Graph.Settings.DisposeCalls == 0, "Provider cleanup rules.");
        return $"worker.Dispose={worker.DisposeCalls}; owned=1; borrowed=0";
    }

    private static async Task<string> VerifyNormalAppAsync(Provider provider)
    {
        var scenario = new ComparisonScenario(provider); var app = scenario.CreateApp();
        Worker worker;
        try
        {
            await app.StartAsync(); await app.StartAsync();
            worker = ((IServiceResolver)app.Services).Resolve<Worker>();
            Require(worker.Starts == 1 && worker.Stops == 0, "Start repeat semantics.");
            await app.StopAsync(); await app.StopAsync();
            Require(worker.Stops == 1, "Stop repeat semantics.");
        }
        finally { await app.DisposeAsync(); }
        Require(worker.DisposeCalls == Disposals(provider) && worker.Resource.DisposeCalls == 1 && scenario.Graph.Settings.DisposeCalls == 0, "App cleanup.");
        return $"start=1; stop=1; worker.Dispose={worker.DisposeCalls}";
    }

    private static string VerifyModules(Provider provider)
    {
        foreach (var enabled in new[] { false, true })
        {
            var resolver = ProviderSelection.CreateResolverBuilder(provider).Build(null!, [(_, c) => {
                IServicesRegistrationContext? module = enabled ? c : null;
                RegisterModule(module); RegisterModule(module);
            }]);
            using var owner = (IDisposable)resolver;
            var values = resolver.Resolve<IEnumerable<ModuleValue>>().ToArray();
            var generics = resolver.Resolve<IEnumerable<IModuleBox<int>>>().ToArray();
            Require(values.Length == (enabled ? 1 : 0) && generics.Length == (enabled ? 2 : 0), "Conditional/repeated module semantics.");
            if (enabled)
                Require(ReferenceEquals(values[0], resolver.Resolve<ModuleValue>()) && generics.All(x => x is ModuleBox<int>), "Named open registration/Try identity.");
        }
        return "identical registration source; conditional/Try/named Type calls; no extra factories";
    }

    private static void RegisterModule(IServicesRegistrationContext? c)
    {
        c?.TrySingleton<ModuleValue>();
        c?.Singleton(implementation: typeof(ModuleBox<>), service: typeof(IModuleBox<>));
    }

    private static string VerifyMissingDependency(Provider provider)
    {
        var scenario = new ComparisonScenario(provider);
        var resolver = ProviderSelection.CreateResolverBuilder(provider).Build(null!, [(_, c) => { scenario.Graph.Register(c); c.Transient<NeedsMissing>(); }]);
        using var owner = (IDisposable)resolver;
        Require(resolver.TryResolve<Missing>() == null, "Optional missing service.");
        var error = Error(() => resolver.Resolve<NeedsMissing>());
        if (provider == Provider.Cobalt)
            Require(error is ServiceResolvingException { Failure: ServiceResolutionFailure.MissingService } &&
                ServiceResolvingException.GetDependencyPath(error).SequenceEqual(new[] { typeof(NeedsMissing), typeof(Missing) }), "Structured Cobalt missing path.");
        else Require(error is InvalidOperationException && error.Message.Contains(nameof(Missing)), "Microsoft missing diagnostic.");
        Require(resolver.Resolve<Request>().Worker.Resource.Settings == scenario.Graph.Settings, "Missing failure recovery.");
        return error.GetType().FullName!;
    }

    private static string VerifyFactoryRecovery(Provider provider)
    {
        var state = new FailureState(); var scenario = new ComparisonScenario(provider); OwnedResource? resource = null;
        var calls = 0;
        var resolver = ProviderSelection.CreateResolverBuilder(provider).Build(null!, [(_, c) => {
            scenario.Graph.Register(c);
            c.Singleton<FactoryProbe>(r => { resource = r.Resolve<OwnedResource>(); calls++; if (state.Fail) throw state.Failure; return new FactoryProbe(); });
        }]);
        using ((IDisposable)resolver)
        {
            var error = Error(() => resolver.Resolve<FactoryProbe>());
            Require(ReferenceEquals(error, state.Failure) && error.StackTrace != null, "Factory exception identity.");
            state.Fail = false; var recovered = resolver.Resolve<FactoryProbe>();
            Require(calls == 2 && ReferenceEquals(recovered, resolver.Resolve<FactoryProbe>()) && resource!.DisposeCalls == 0, "Failed factory retry/cache/ownership.");
        }
        Require(resource!.DisposeCalls == 1 && scenario.Graph.Settings.DisposeCalls == 0, "Failed factory dependency cleanup.");
        return "original exception; failed singleton retries; completed dependency retained/disposed once";
    }

    private static string VerifyConstructorRecovery(Provider provider)
    {
        var state = new FailureState();
        var resolver = ProviderSelection.CreateResolverBuilder(provider).Build(null!, [(_, c) => c.Instance(state).Singleton<ConstructorProbe>()]);
        using var owner = (IDisposable)resolver;
        var error = Error(() => resolver.Resolve<ConstructorProbe>());
        Require(ReferenceEquals(error, state.Failure) && error.StackTrace!.Contains(nameof(ConstructorProbe)), "Constructor exception identity/throw site.");
        state.Fail = false; var value = resolver.Resolve<ConstructorProbe>();
        Require(ReferenceEquals(value, resolver.Resolve<ConstructorProbe>()), "Constructor singleton recovery.");
        return "original constructor exception and stack; retry succeeds";
    }

    private static string VerifyCycleRecovery(Provider provider)
    {
        var scenario = new ComparisonScenario(provider);
        var resolver = ProviderSelection.CreateResolverBuilder(provider).Build(null!, [(_, c) => { scenario.Graph.Register(c); c.Singleton<CycleA>().Singleton<CycleB>(); }]);
        using var owner = (IDisposable)resolver;
        var error = Error(() => resolver.Resolve<CycleA>());
        if (provider == Provider.Cobalt)
            Require(error is ServiceResolvingException { Failure: ServiceResolutionFailure.CircularDependency } &&
                ServiceResolvingException.GetDependencyPath(error).SequenceEqual(new[] { typeof(CycleA), typeof(CycleB), typeof(CycleA) }), "Cobalt cycle path.");
        else Require(error is InvalidOperationException && error.Message.Contains(nameof(CycleA)) && error.Message.Contains(nameof(CycleB)), "Microsoft cycle diagnostic.");
        Require(resolver.Resolve<Request>() is not null, "Cycle failure recovery.");
        return error.GetType().FullName!;
    }

    private static async Task<string> VerifyStartupRollbackAsync(Provider provider)
    {
        var scenario = new ComparisonScenario(provider); var state = new RunState();
        var app = scenario.CreateAppBuilder().Setup(ctx => ctx.Services(c => c.Singleton<IStartable>(r => {
            state.Worker = r.Resolve<Worker>(); return new Gate(state, null, true);
        }))).Build();
        try
        {
            var error = await ErrorAsync(() => app.StartAsync());
            Require(ReferenceEquals(error, state.Failure) && state.Worker!.Starts == 1 && state.Worker.Stops == 1 && state.GateStops == 0,
                "Original startup failure and rollback of successfully started services.");
        }
        finally { await app.DisposeAsync(); }
        Require(state.Worker!.DisposeCalls == Disposals(provider, 1) && state.Worker.Resource.DisposeCalls == 1 &&
            scenario.Graph.Settings.DisposeCalls == 0 && state.GateDisposals == 1, "Startup rollback cleanup.");
        return $"original failure; worker rolled back; failing gate not stopped; worker.Dispose={state.Worker.DisposeCalls}";
    }

    private static async Task<string> VerifyRunAsync(Provider provider, bool cancelExternally)
    {
        var scenario = new ComparisonScenario(provider); var state = new RunState();
        using var cancellation = new CancellationTokenSource();
        var run = App.RunAsync(scenario.CreateAppBuilder(), ctx => ctx.Services(c => c.Singleton<IStartable>(r => {
            state.Worker = r.Resolve<Worker>(); return new Gate(state, cancelExternally ? null : r.Resolve<IAppLifetime>(), false);
        })), cancellation.Token);
        await state.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (cancelExternally)
        {
            cancellation.Cancel();
            var error = await ErrorAsync(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
            Require(error is OperationCanceledException canceled && canceled.CancellationToken == cancellation.Token, "External cancellation token/result.");
        }
        else await run.WaitAsync(TimeSpan.FromSeconds(10));
        Require(state.Worker!.Starts == 1 && state.Worker.Stops == 1 && state.GateStops == 1 && state.GateDisposals == 1 &&
            state.Worker.DisposeCalls == Disposals(provider) && state.Worker.Resource.DisposeCalls == 1 && scenario.Graph.Settings.DisposeCalls == 0,
            "RunAsync start/stop/ownership cleanup.");
        return $"{(cancelExternally ? "original external cancellation" : "normal lifetime completion")}; cleanup completed";
    }

    private static Exception Error(Action action)
    {
        try { action(); } catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected failure.");
    }
    private static async Task<Exception> ErrorAsync(Func<Task> action)
    {
        try { await action(); } catch (Exception error) { return error; }
        throw new InvalidOperationException("Expected asynchronous failure.");
    }

    public class ModuleValue;
    public interface IModuleBox<T>;
    public class ModuleBox<T> : IModuleBox<T>;
    public class Missing;
    public class NeedsMissing(Missing dependency) { public Missing Dependency { get; } = dependency; }
    public class FactoryProbe;
    public class FailureState { public bool Fail = true; public Exception Failure { get; } = new ApplicationException("COB7 activation failed"); }
    public class ConstructorProbe { public ConstructorProbe(FailureState state) { if (state.Fail) throw state.Failure; } }
    public class CycleA(CycleB dependency) { public CycleB Dependency { get; } = dependency; }
    public class CycleB(CycleA dependency) { public CycleA Dependency { get; } = dependency; }
    private sealed class RunState
    {
        public Worker? Worker; public int GateStops; public int GateDisposals;
        public Exception Failure { get; } = new ApplicationException("COB7 startup failed");
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class Gate(RunState state, IAppLifetime? lifetime, bool fail) : IStartable, IStoppable, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (fail) return Task.FromException(state.Failure);
            state.Started.TrySetResult(); lifetime?.Cancel(); return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken = default) { state.GateStops++; return Task.CompletedTask; }
        public void Dispose() => state.GateDisposals++;
    }
}
