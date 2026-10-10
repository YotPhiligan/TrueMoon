using System.Collections.Frozen;
using TrueMoon.Services;

namespace TrueMoon.Cobalt.Tests;

public class ResolverRegressionTests
{
    private static CobaltServiceResolver Build(Action<IServicesRegistrationContext> register)
        => (CobaltServiceResolver)new CobaltServiceResolverBuilder().Build(null!, [(_, context) => register(context)]);

    // Publishing another graph in the same assembly must not activate it here.
    private static void UnusedGraph(IServicesRegistrationContext context) => context.Singleton<IMarker, Poison>();

    [Fact]
    public void RuntimeGraph_SelectsImplementationAndLifetime_WithoutAssemblyPollution()
    {
        using var resolver = Build(c => c.Singleton<IMarker, First>().Transient<IMarker, First>().Singleton<IMarker, Second>());
        var selected = Assert.IsType<Second>(resolver.Resolve<IMarker>());
        var first = resolver.Resolve<IEnumerable<IMarker>>().ToArray();
        var second = resolver.Resolve<IEnumerable<IMarker>>().ToArray();
        Assert.Collection(first, item => Assert.IsType<First>(item), item => Assert.IsType<First>(item), item => Assert.Same(selected, item));
        Assert.Same(first[0], second[0]);
        Assert.NotSame(first[1], second[1]);
        Assert.Same(first[2], second[2]);
    }

    [Fact]
    public void GeneratedSingletons_AreIndependentAcrossResolvers()
    {
        using var first = Build(c => c.Singleton<IMarker, First>());
        using var second = Build(c => c.Singleton<IMarker, First>());
        Assert.Same(first.Resolve<IMarker>(), first.Resolve<IMarker>());
        Assert.NotSame(first.Resolve<IMarker>(), second.Resolve<IMarker>());
    }

    [Fact]
    public async Task SingletonFactory_ConcurrentResolution_CreatesOneOwnedInstance()
    {
        var creations = 0;
        var probe = new DisposalProbe("factory", []);
        var resolver = Build(c => c.Singleton<IMarker>(_ => { Interlocked.Increment(ref creations); return probe; }));
        var instances = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() => resolver.Resolve<IMarker>())));
        Assert.All(instances, item => Assert.Same(probe, item));
        Assert.Equal(1, creations);
        await resolver.DisposeAsync();
        resolver.Dispose();
        Assert.Equal(1, probe.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => resolver.Resolve<IMarker>());
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public async Task Composite_AllAliasesShareOneOwnedInstance(int arity)
    {
        var resolver = Build(c =>
        {
            switch (arity)
            {
                case 2: c.Composite<Composite, IA, IB>(); break;
                case 3: c.Composite<Composite, IA, IB, IC>(); break;
                case 4: c.Composite<Composite, IA, IB, IC, ID>(); break;
                case 5: c.Composite<Composite, IA, IB, IC, ID, IE>(); break;
                case 6: c.Composite<Composite, IA, IB, IC, ID, IE, IF>(); break;
                case 7: c.Composite<Composite, IA, IB, IC, ID, IE, IF, IG>(); break;
                case 8: c.Composite<Composite, IA, IB, IC, ID, IE, IF, IG, IH>(); break;
            }
        });
        var instance = resolver.Resolve<Composite>();
        foreach (var alias in new[] { typeof(IA), typeof(IB), typeof(IC), typeof(ID), typeof(IE), typeof(IF), typeof(IG), typeof(IH) }.Take(arity))
            Assert.Same(instance, resolver.GetService(alias));
        Assert.Same(instance, Assert.Single(resolver.Resolve<IEnumerable<IA>>()));
        await resolver.DisposeAsync();
        Assert.Equal(1, instance.DisposeCount);
    }

    [Fact]
    public async Task OwnedFactories_DisposeUniqueInstancesInReverseOrder_AndPreserveBorrowedInstances()
    {
        List<string> order = [];
        var borrowed = new DisposalProbe("borrowed", order);
        var dependency = new DisposalProbe("dependency", order);
        var dependent = new DisposalProbe("dependent", order);
        var resolver = Build(c => c.Instance<IA>(borrowed).Singleton<IB>(_ => dependency)
            .Singleton<IMarker>(r => { r.Resolve<IB>(); return dependent; }).Singleton<IC>(r => (IC)r.Resolve<IMarker>()));
        Assert.Same(borrowed, resolver.Resolve<IA>());
        Assert.Same(dependent, resolver.Resolve<IMarker>());
        Assert.Same(dependent, resolver.Resolve<IC>());
        await resolver.DisposeAsync();
        await resolver.DisposeAsync();
        Assert.Equal(new[] { "dependent", "dependency" }, order);
        Assert.Equal(0, borrowed.DisposeCount);
        Assert.Equal(1, dependency.DisposeCount);
        Assert.Equal(1, dependent.DisposeCount);
    }

    [Fact]
    public async Task CleanupFailure_StillDisposesRemainingServices_AndIsNotRetried()
    {
        List<string> order = [];
        var original = new InvalidOperationException("cleanup");
        var first = new DisposalProbe("first", order);
        var failure = new DisposalProbe("failure", order, original);
        var resolver = Build(c => c.Singleton<IA>(_ => first).Singleton<IB>(_ => failure));
        resolver.Resolve<IA>();
        resolver.Resolve<IB>();
        var error = await Assert.ThrowsAsync<AggregateException>(() => resolver.DisposeAsync().AsTask());
        Assert.Same(original, Assert.Single(error.InnerExceptions));
        var repeated = Assert.Throws<AggregateException>(() => resolver.Dispose());
        Assert.Same(error, repeated);
        Assert.Equal(new[] { "failure", "first" }, order);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, failure.DisposeCount);
    }

    [Fact]
    public void SyncDisposal_HandlesAsyncOnlyFactoryValues()
    {
        var owned = new AsyncProbe();
        var borrowed = new AsyncProbe();
        var resolver = Build(c => c.Singleton<IA>(_ => owned).Instance<IB>(borrowed));
        Assert.Same(owned, resolver.Resolve<IA>());
        Assert.Same(borrowed, resolver.Resolve<IB>());
        resolver.Dispose();
        resolver.Dispose();
        Assert.Equal(1, owned.DisposeCount);
        Assert.Equal(0, borrowed.DisposeCount);
    }

    [Fact]
    public void TransientFactories_OwnEachCreatedDisposable()
    {
        List<string> order = [];
        var resolver = Build(c => c.Transient<IMarker>(_ => new DisposalProbe(order.Count.ToString(), order)));
        var first = Assert.IsType<DisposalProbe>(resolver.Resolve<IMarker>());
        var second = Assert.IsType<DisposalProbe>(resolver.Resolve<IMarker>());
        Assert.NotSame(first, second);
        resolver.Dispose();
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
        Assert.Equal(2, order.Count);
    }

    [Fact]
    public async Task OpenGenerics_ResolveClosedDependenciesAndTypedEnumerables_WithConcurrentSingletonIdentity()
    {
        using var resolver = Build(c => c.Singleton(typeof(IBox<>), typeof(Box<>))
            .Singleton(typeof(IRepository<>), typeof(Repository<>)).Transient(typeof(IRepository<>), typeof(OtherRepository<>)));
        var repositories = resolver.Resolve<IEnumerable<IRepository<string>>>().ToArray();
        var first = Assert.IsType<Repository<string>>(repositories[0]);
        Assert.Same(resolver.Resolve<IBox<string>>(), first.Box);
        Assert.IsType<OtherRepository<string>>(repositories[1]);
        var repeated = resolver.Resolve<IEnumerable<IRepository<string>>>().ToArray();
        Assert.Same(first, repeated[0]);
        Assert.NotSame(repositories[1], repeated[1]);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => resolver.Resolve<IBox<string>>())));
        Assert.All(concurrent, box => Assert.Same(first.Box, box));
        Assert.NotSame(first.Box, resolver.Resolve<IBox<int>>());
    }

    [Fact]
    public void MissingServices_AreOptionalOrRequired_AndOnlyTypedEnumerableIsSynthesized()
    {
        using var resolver = Build(_ => { });
        Assert.Null(resolver.GetService(typeof(IMarker)));
        Assert.Null(resolver.TryResolve<IMarker>());
        Assert.Throws<ServiceResolvingException<IMarker>>(() => resolver.Resolve<IMarker>());
        Assert.Empty(resolver.Resolve<IEnumerable<IMarker>>());
        Assert.Null(resolver.TryResolve<List<IMarker>>());
        Assert.Same(resolver, resolver.Resolve<IServiceResolver>());
        Assert.Same(resolver, resolver.Resolve<IServiceProvider>());
    }

    [Fact]
    public async Task TypeContainer_InitializesAllFactoriesOnce_WhenSingleAndMultipleResolutionMix()
    {
        var creations = 0;
        var container = new TypeContainer(typeof(IMarker));
        container.Add(() => { Interlocked.Increment(ref creations); return new InstanceResolver<IMarker>(new First()); });
        container.Add(() => { Interlocked.Increment(ref creations); return new InstanceResolver<IMarker>(new Second()); });
        var selected = container.GetResolver();
        var all = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(container.GetResolvers)));
        Assert.Equal(2, creations);
        Assert.All(all, resolvers => { Assert.Equal(2, resolvers.Length); Assert.NotNull(resolvers[0]); Assert.Same(selected, resolvers[1]); });
        Assert.Throws<InvalidOperationException>(() => container.Add(() => new InstanceResolver<IMarker>(new First())));
    }

    [Fact]
    public void RegistrationQueries_ReflectRemovalAndExactImplementation()
    {
        var context = new CobaltServicesRegistrationContext();
        context.Singleton<IMarker, First>().Singleton<IMarker, Second>();
        Assert.True(context.Exist<IMarker>());
        Assert.True(context.Exist<IMarker, First>());
        Assert.False(context.Exist<IMarker, Poison>());
        context.Remove<IMarker, First>();
        Assert.False(context.Exist<IMarker, First>());
        Assert.True(context.Exist<IMarker, Second>());
        context.RemoveAll<IMarker>();
        Assert.False(context.Exist<IMarker>());
    }

    [Fact]
    public void TypeOverload_ForClosedServicesUsesGeneratedConstruction()
    {
        using var resolver = Build(c => c.Singleton(typeof(IMarker), typeof(First)));
        Assert.IsType<First>(resolver.Resolve<IMarker>());
        Assert.Same(resolver.Resolve<IMarker>(), resolver.Resolve<IMarker>());
    }

    [Fact]
    public void TypedOnlyResolvers_WorkThroughProviderAndEnumerable_AndPreserveOriginalErrors()
    {
        var instance = new First();
        var container = new TypeContainer(typeof(IMarker));
        container.Add(() => new TypedOnlyResolver(instance));
        using var resolver = new CobaltServiceResolver(new[] { container }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
        Assert.Same(instance, resolver.Resolve<IMarker>());
        Assert.Same(instance, resolver.GetService(typeof(IMarker)));
        Assert.Same(instance, Assert.Single(resolver.Resolve<IEnumerable<IMarker>>()));
        var failure = new InvalidOperationException("typed resolver failure");
        var failingContainer = new TypeContainer(typeof(IMarker));
        failingContainer.Add(() => new TypedOnlyResolver(null, failure));
        using var failing = new CobaltServiceResolver(new[] { failingContainer }.ToFrozenDictionary(c => c.Type, c => (ITypeContainer)c));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => failing.Resolve<IMarker>()));
    }

    [Fact]
    public async Task FactoryResolver_DirectConcurrentSingletonCalls_RunFactoryOnce()
    {
        var calls = 0;
        var instance = new First();
        var factory = new FactoryResolver<IMarker>(new FactoryContainer<IMarker>(_ => { Interlocked.Increment(ref calls); return instance; }), ServiceLifetime.Singleton);
        using var services = Build(_ => { });
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => factory.Resolve(services))));
        Assert.All(results, result => Assert.Same(instance, result));
        Assert.Equal(1, calls);
    }

    private class TypedOnlyResolver(IMarker? value, Exception? exception = null) : IResolver<IMarker>
    {
        public bool IsServiceDisposable => false;
        public ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;
        public IMarker? Resolve(IServiceResolver services)
        {
            if (exception != null) throw exception;
            return value;
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DisposablesContainer_PrefersMatchingMode_ForDualDisposableValues(bool asynchronous)
    {
        var value = new DualDisposable();
        var container = new DisposablesContainer();
        container.Add(value);
        container.Add(value);
        if (asynchronous) await container.DisposeAsync(); else container.Dispose();
        await container.DisposeAsync();
        Assert.Equal(asynchronous ? 0 : 1, value.SyncCount);
        Assert.Equal(asynchronous ? 1 : 0, value.AsyncCount);
        Assert.Throws<ObjectDisposedException>(() => container.Add(new DualDisposable()));
    }

    [Fact]
    public async Task DisposablesContainer_ConcurrentAsyncCleanup_SharesCompletion()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var value = new PausedDisposable(started, release);
        var container = new DisposablesContainer();
        container.Add(value);
        var first = container.DisposeAsync().AsTask();
        await started.Task;
        var second = container.DisposeAsync().AsTask();
        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, value.DisposeCount);
    }

    private class DualDisposable : IDisposable, IAsyncDisposable
    {
        public int SyncCount { get; private set; }
        public int AsyncCount { get; private set; }
        public void Dispose() => SyncCount++;
        public ValueTask DisposeAsync() { AsyncCount++; return ValueTask.CompletedTask; }
    }
    private class PausedDisposable(TaskCompletionSource started, TaskCompletionSource release) : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public async ValueTask DisposeAsync() { DisposeCount++; started.SetResult(); await release.Task; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OwnedServiceDisposal_CanReenterResolverDisposal_WithoutDeadlocking(bool asynchronous)
    {
        var value = new RecursiveDisposable();
        var resolver = Build(c => c.Singleton<IMarker>(r => { value.Resolver = r; return value; }));
        Assert.Same(value, resolver.Resolve<IMarker>());
        var cleanup = asynchronous ? resolver.DisposeAsync().AsTask() : Task.Run(resolver.Dispose);
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, value.DisposeCount);
    }
    private class RecursiveDisposable : IMarker, IDisposable, IAsyncDisposable
    {
        public IServiceResolver Resolver { get; set; } = null!;
        public int DisposeCount { get; private set; }
        public void Dispose() { DisposeCount++; ((IDisposable)Resolver).Dispose(); }
        public async ValueTask DisposeAsync() { DisposeCount++; await Task.Yield(); await ((IAsyncDisposable)Resolver).DisposeAsync(); }
    }
    public interface IMarker;
    public class First : IMarker;
    public class Second : IMarker;
    public class Poison : IMarker { public Poison() => throw new InvalidOperationException("unregistered graph"); }
    public interface IA; public interface IB; public interface IC; public interface ID;
    public interface IE; public interface IF; public interface IG; public interface IH;
    public class Composite : IA, IB, IC, ID, IE, IF, IG, IH, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    public class DisposalProbe(string name, List<string> order, Exception? error = null) : IMarker, IA, IB, IC, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() { DisposeCount++; order.Add(name); if (error != null) throw error; }
    }
    public class AsyncProbe : IA, IB, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }
    public interface IBox<T>;
    public class Box<T> : IBox<T>;
    public interface IRepository<T>;
    public class Repository<T>(IBox<T> box) : IRepository<T> { public IBox<T> Box { get; } = box; }
    public class OtherRepository<T> : IRepository<T>;
}
