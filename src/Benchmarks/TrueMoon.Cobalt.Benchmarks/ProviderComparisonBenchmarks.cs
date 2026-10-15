using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

[MemoryDiagnoser, CategoriesColumn, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ProviderComparisonBenchmarks
{
    private ComparisonScenario _cobalt = null!;
    private ComparisonScenario _microsoft = null!;
    private IServiceResolver _cobaltResolver = null!;
    private IServiceResolver _microsoftResolver = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cobalt = new(Provider.Cobalt); _microsoft = new(Provider.Microsoft);
        _cobaltResolver = _cobalt.CreateResolver(); _microsoftResolver = _microsoft.CreateResolver();
        _cobaltResolver.Resolve<Request>(); _microsoftResolver.Resolve<Request>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ((IDisposable)_cobaltResolver).Dispose(); ((IDisposable)_microsoftResolver).Dispose();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Singleton")]
    public Worker MicrosoftSingleton() => _microsoftResolver.Resolve<Worker>();
    [Benchmark, BenchmarkCategory("Singleton")]
    public Worker CobaltSingleton() => _cobaltResolver.Resolve<Worker>();
    [Benchmark(Baseline = true), BenchmarkCategory("Transient")]
    public Request MicrosoftTransient() => _microsoftResolver.Resolve<Request>();
    [Benchmark, BenchmarkCategory("Transient")]
    public Request CobaltTransient() => _cobaltResolver.Resolve<Request>();
    [Benchmark(Baseline = true), BenchmarkCategory("OpenGeneric")]
    public IRepository<int> MicrosoftOpenGeneric() => _microsoftResolver.Resolve<IRepository<int>>();
    [Benchmark, BenchmarkCategory("OpenGeneric")]
    public IRepository<int> CobaltOpenGeneric() => _cobaltResolver.Resolve<IRepository<int>>();
    [Benchmark(Baseline = true), BenchmarkCategory("ClosedGeneric")]
    public IRepository<string> MicrosoftClosedGeneric() => _microsoftResolver.Resolve<IRepository<string>>();
    [Benchmark, BenchmarkCategory("ClosedGeneric")]
    public IRepository<string> CobaltClosedGeneric() => _cobaltResolver.Resolve<IRepository<string>>();
    [Benchmark(Baseline = true), BenchmarkCategory("Multiple")]
    public IPlugin[] MicrosoftMultiple() => _microsoftResolver.Resolve<IEnumerable<IPlugin>>().ToArray();
    [Benchmark, BenchmarkCategory("Multiple")]
    public IPlugin[] CobaltMultiple() => _cobaltResolver.Resolve<IEnumerable<IPlugin>>().ToArray();

    [Benchmark(Baseline = true), BenchmarkCategory("EmptyBuild")]
    public IServiceResolver MicrosoftEmptyBuild() => BuildEmpty(_microsoft);
    [Benchmark, BenchmarkCategory("EmptyBuild")]
    public IServiceResolver CobaltEmptyBuild() => BuildEmpty(_cobalt);
    [Benchmark(Baseline = true), BenchmarkCategory("GraphBuild")]
    public IServiceResolver MicrosoftGraphBuild() => BuildGraph(_microsoft);
    [Benchmark, BenchmarkCategory("GraphBuild")]
    public IServiceResolver CobaltGraphBuild() => BuildGraph(_cobalt);
    [Benchmark(Baseline = true), BenchmarkCategory("FirstGraph")]
    public Request MicrosoftFirstGraph() => FirstGraph(_microsoft);
    [Benchmark, BenchmarkCategory("FirstGraph")]
    public Request CobaltFirstGraph() => FirstGraph(_cobalt);
    [Benchmark(Baseline = true), BenchmarkCategory("AppBuild")]
    public IApp MicrosoftAppBuild() => BuildApp(_microsoft);
    [Benchmark, BenchmarkCategory("AppBuild")]
    public IApp CobaltAppBuild() => BuildApp(_cobalt);
    [Benchmark(Baseline = true), BenchmarkCategory("AppLifecycle")]
    public Task<int> MicrosoftAppLifecycle() => RunApp(_microsoft);
    [Benchmark, BenchmarkCategory("AppLifecycle")]
    public Task<int> CobaltAppLifecycle() => RunApp(_cobalt);

    private static IServiceResolver BuildEmpty(ComparisonScenario scenario)
    {
        var resolver = scenario.CreateEmptyResolver(); using var owner = (IDisposable)resolver; return resolver;
    }
    private static IServiceResolver BuildGraph(ComparisonScenario scenario)
    {
        var resolver = scenario.CreateResolver(); using var owner = (IDisposable)resolver; return resolver;
    }
    private static Request FirstGraph(ComparisonScenario scenario)
    {
        var resolver = scenario.CreateResolver(); using var owner = (IDisposable)resolver; return resolver.Resolve<Request>();
    }
    private static IApp BuildApp(ComparisonScenario scenario)
    {
        using var app = scenario.CreateApp(); return app;
    }
    private static async Task<int> RunApp(ComparisonScenario scenario)
    {
        await using var app = scenario.CreateApp(); await app.StartAsync();
        var worker = ((IServiceResolver)app.Services).Resolve<Worker>();
        await app.StopAsync(); return worker.Starts + worker.Stops;
    }
}
