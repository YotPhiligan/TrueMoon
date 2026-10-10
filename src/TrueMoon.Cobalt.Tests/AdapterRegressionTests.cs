using Microsoft.Extensions.DependencyInjection;
using TrueMoon.Services;
using Resolver = TrueMoon.Extensions.DependencyInjection.ServiceResolver;
using Builder = TrueMoon.Extensions.DependencyInjection.ServiceResolverBuilder;

namespace TrueMoon.Cobalt.Tests;

public class AdapterRegressionTests
{
    [Fact]
    public async Task Builder_ReturnsSameResolverToFactories_AndDisposesOwnedProviderOnce()
    {
        var owned = new Probe();
        var borrowed = new Probe();
        IServiceResolver? fromFactory = null;
        var resolver = new Builder().Build(null!, [(_, c) => c.Instance<IBorrowed>(borrowed)
            .Singleton<IOwned>(r => { fromFactory = r; return owned; })]);
        Assert.Same(owned, resolver.Resolve<IOwned>());
        Assert.Same(borrowed, resolver.Resolve<IBorrowed>());
        Assert.Same(resolver, fromFactory);
        Assert.Same(resolver, resolver.Resolve<IServiceResolver>());
        Assert.Same(resolver, resolver.Resolve<IServiceProvider>());
        await ((IAsyncDisposable)resolver).DisposeAsync();
        ((IDisposable)resolver).Dispose();
        Assert.Equal(1, owned.DisposeCount);
        Assert.Equal(0, borrowed.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => resolver.Resolve<IOwned>());
    }

    [Fact]
    public void RequiredAndOptionalResolution_HaveDistinctMissingServiceBehavior()
    {
        using var resolver = new Resolver(new ServiceCollection().BuildServiceProvider());
        Assert.Null(resolver.GetService(typeof(IOwned)));
        Assert.Null(resolver.TryResolve<IOwned>());
        var error = Assert.Throws<InvalidOperationException>(() => resolver.Resolve<IOwned>());
        Assert.Contains(typeof(IOwned).ToString(), error.Message);
        Assert.Empty(resolver.Resolve<IEnumerable<IOwned>>());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Wrapper_PrefersMatchingDisposalMode_AndDisposesProviderOnce(bool async)
    {
        var provider = new ProviderProbe();
        var resolver = new Resolver(provider);
        if (async) await resolver.DisposeAsync(); else resolver.Dispose();
        resolver.Dispose();
        await resolver.DisposeAsync();
        Assert.Equal(async ? 0 : 1, provider.SyncCount);
        Assert.Equal(async ? 1 : 0, provider.AsyncCount);
    }

    [Fact]
    public void CompositeAliases_ResolveSameConcreteSingleton()
    {
        var resolver = new Builder().Build(null!, [(_, c) => c.Composite<Probe, IOwned, IBorrowed>()]);
        var concrete = resolver.Resolve<Probe>();
        Assert.Same(concrete, resolver.Resolve<IOwned>());
        Assert.Same(concrete, resolver.Resolve<IBorrowed>());
        Assert.Same(concrete, Assert.Single(resolver.Resolve<IEnumerable<IOwned>>()));
        ((IDisposable)resolver).Dispose();
        // Microsoft DI captures the disposable implementation and each disposable alias separately.
        Assert.Equal(3, concrete.DisposeCount);
    }
    [Fact]
    public async Task ConcurrentAsyncDisposal_WaitsForSameProviderCleanup()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new AsyncProvider(started, release);
        var resolver = new Resolver(provider);
        var first = resolver.DisposeAsync().AsTask();
        await started.Task;
        var second = resolver.DisposeAsync().AsTask();
        Assert.Same(first, second);
        Assert.False(second.IsCompleted);
        Assert.Throws<ObjectDisposedException>(() => resolver.TryResolve<IOwned>());
        release.SetResult();
        await Task.WhenAll(first, second);
        Assert.Equal(1, provider.DisposeCount);
    }

    [Fact]
    public void SyncDisposal_CleansAsyncOnlyProvider()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        release.SetResult();
        var provider = new AsyncProvider(started, release);
        var resolver = new Resolver(provider);
        resolver.Dispose();
        resolver.Dispose();
        Assert.True(started.Task.IsCompletedSuccessfully);
        Assert.Equal(1, provider.DisposeCount);
    }

    [Fact]
    public async Task ProviderCleanupFailure_PreservesErrorAndDoesNotRetry()
    {
        var failure = new InvalidOperationException("provider cleanup");
        var provider = new FailingProvider(failure);
        var resolver = new Resolver(provider);
        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.DisposeAsync().AsTask());
        Assert.Same(failure, first);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => resolver.Dispose()));
        Assert.Equal(1, provider.DisposeCount);
    }

    private class AsyncProvider(TaskCompletionSource started, TaskCompletionSource release) : IServiceProvider, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public object? GetService(Type type) => null;
        public async ValueTask DisposeAsync() { DisposeCount++; started.SetResult(); await release.Task; }
    }
    private class FailingProvider(Exception failure) : IServiceProvider, IDisposable
    {
        public int DisposeCount { get; private set; }
        public object? GetService(Type type) => null;
        public void Dispose() { DisposeCount++; throw failure; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OwnedServiceDisposal_CanReenterWrapperDisposal_WithoutDeadlocking(bool asynchronous)
    {
        var value = new RecursiveDisposable();
        var resolver = new Builder().Build(null!, [(_, c) => c.Singleton<IOwned>(r => { value.Resolver = r; return value; })]);
        Assert.Same(value, resolver.Resolve<IOwned>());
        var cleanup = asynchronous ? ((IAsyncDisposable)resolver).DisposeAsync().AsTask() : Task.Run(() => ((IDisposable)resolver).Dispose());
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, value.DisposeCount);
    }
    private class RecursiveDisposable : IOwned, IDisposable, IAsyncDisposable
    {
        public IServiceResolver Resolver { get; set; } = null!;
        public int DisposeCount { get; private set; }
        public void Dispose() { DisposeCount++; ((IDisposable)Resolver).Dispose(); }
        public async ValueTask DisposeAsync() { DisposeCount++; await Task.Yield(); await ((IAsyncDisposable)Resolver).DisposeAsync(); }
    }
    public interface IOwned;
    public interface IBorrowed;
    public class Probe : IOwned, IBorrowed, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    private class ProviderProbe : IServiceProvider, IDisposable, IAsyncDisposable
    {
        public int SyncCount { get; private set; }
        public int AsyncCount { get; private set; }
        public object? GetService(Type type) => null;
        public void Dispose() => SyncCount++;
        public ValueTask DisposeAsync() { AsyncCount++; return ValueTask.CompletedTask; }
    }
}
