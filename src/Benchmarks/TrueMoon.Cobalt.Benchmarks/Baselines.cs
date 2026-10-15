using BenchmarkDotNet.Attributes;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Benchmarks;

[MemoryDiagnoser]
public class ResolverLifecycleBenchmarks
{
    private Scenario _scenario = null!;
    private CobaltServiceResolverBuilder _builder = null!;

    [GlobalSetup]
    public void Setup() { _scenario = new Scenario(); _builder = new CobaltServiceResolverBuilder(); }

    [Benchmark]
    public IServiceResolver BuildAndDisposeEmpty()
    {
        var resolver = _builder.Build(null!, []);
        using var owner = (IDisposable)resolver;
        return resolver;
    }

    [Benchmark]
    public IServiceResolver BuildAndDisposeGraph()
    {
        var resolver = _scenario.CreateResolver();
        using var owner = (IDisposable)resolver;
        return resolver;
    }

    [Benchmark]
    public Request BuildResolveAndDisposeGraph()
    {
        var resolver = _scenario.CreateResolver();
        using var owner = (IDisposable)resolver;
        return resolver.Resolve<Request>();
    }
}

[MemoryDiagnoser]
public class ResolutionBenchmarks
{
    private IServiceResolver _resolver = null!;

    [GlobalSetup]
    public void Setup()
    {
        _resolver = new Scenario().CreateResolver();
        _resolver.Resolve<Request>(); // Warm every requested singleton/closure before measuring.
    }

    [GlobalCleanup]
    public void Cleanup() => ((IDisposable)_resolver).Dispose();

    [Benchmark]
    public Worker Singleton() => _resolver.Resolve<Worker>();

    [Benchmark]
    public Request TransientRequest() => _resolver.Resolve<Request>();

    [Benchmark]
    public IRepository<int> OpenGenericSingleton() => _resolver.Resolve<IRepository<int>>();

    [Benchmark]
    public IRepository<string> ClosedGenericSingleton() => _resolver.Resolve<IRepository<string>>();

    [Benchmark]
    public IPlugin[] MaterializeMultiplePlugins() => _resolver.Resolve<IEnumerable<IPlugin>>().ToArray();
}

[MemoryDiagnoser]
public class AppLifecycleBenchmarks
{
    private Scenario _scenario = null!;

    [GlobalSetup]
    public void Setup() => _scenario = new Scenario();

    [Benchmark]
    public IApp BuildAndDispose()
    {
        using var app = _scenario.CreateApp();
        return app;
    }

    [Benchmark]
    public async Task<int> BuildStartStopDispose()
    {
        await using var app = _scenario.CreateApp();
        await app.StartAsync();
        var worker = ((IServiceResolver)app.Services).Resolve<Worker>();
        await app.StopAsync();
        return worker.Starts + worker.Stops;
    }
}
