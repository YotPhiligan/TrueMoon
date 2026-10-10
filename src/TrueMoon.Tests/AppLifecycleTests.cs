using System.Runtime.CompilerServices;
using TrueMoon.Configuration;
using TrueMoon.Dependencies;
using TrueMoon.Diagnostics;
using TrueMoon.Exceptions;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public class AppLifecycleTests
{
    private static IAppBuilder Builder(bool microsoftDI)
        => App.Builder(context => { if (microsoftDI) context.UseDI(); });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFailure_RollsBackReverseOrderAndPreservesOriginalError(bool microsoftDI)
    {
        var trace = new List<string>();
        var failure = new InvalidOperationException("startup failure");
        var first = new RecordingService("first", trace);
        var second = new RecordingService("second", trace);
        var failing = new RecordingService("failing", trace) { StartError = failure };
        var idle = new RecordingService("idle", trace);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Builder(microsoftDI).RunAsync(context => context
            .Services(services => services.Singleton<IStartable>(_ => first).Singleton<IStartable>(_ => second)
                .Singleton<IStartable>(_ => failing).Singleton<IStartable>(_ => idle))));
        Assert.Same(failure, error);
        Assert.Equal(new[] { "first.Start", "second.Start", "failing.Start", "second.Stop", "first.Stop",
            "idle.Dispose", "failing.Dispose", "second.Dispose", "first.Dispose" }, trace);
        Assert.False(first.StopToken.IsCancellationRequested);
        Assert.Equal(1, idle.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupAndCleanupFailures_PreserveEveryErrorAndContinueCleanup(bool microsoftDI)
    {
        var trace = new List<string>();
        var startup = new InvalidOperationException("start");
        var stop = new ArgumentException("stop");
        var dispose = new ApplicationException("dispose");
        var first = new RecordingService("first", trace) { StopError = stop, DisposeError = dispose };
        var failing = new RecordingService("failing", trace) { StartError = startup };
        var error = await Assert.ThrowsAsync<AggregateException>(() => Builder(microsoftDI).RunAsync(context => context
            .Services(services => services.Singleton<IStartable>(_ => first).Singleton<IStartable>(_ => failing))));
        var causes = error.Flatten().InnerExceptions;
        Assert.Contains(startup, causes);
        Assert.Contains(stop, causes);
        Assert.Contains(dispose, causes);
        Assert.Contains("first.Dispose", trace);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, failing.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalCancellation_StopsWithIndependentTokenAndDisposes(bool microsoftDI)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trace = new List<string>();
        var service = new RecordingService("service", trace) { OnStart = () => started.TrySetResult() };
        var run = Builder(microsoftDI).RunAsync(context => context.Services(services => services
            .Singleton<IStartable>(resolver =>
            {
                var lifetime = resolver.Resolve<IAppLifetime>();
                lifetime.OnStopping(() => trace.Add("Stopping"));
                lifetime.OnStopped(() => trace.Add("Stopped"));
                return service;
            })), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "service.Start", "Stopping", "service.Stop", "Stopped", "service.Dispose" }, trace);
        Assert.True(service.StopToken.CanBeCanceled);
        Assert.False(service.StopToken.IsCancellationRequested);
        Assert.Equal(1, service.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstanceRun_UsesSameShutdownOrderAsBuilderRun(bool microsoftDI)
    {
        var trace = new List<string>();
        var service = new RecordingService("service", trace);
        var app = Builder(microsoftDI).Setup(context => context.Services(services => services.Singleton<IStartable>(resolver =>
        {
            var lifetime = resolver.Resolve<IAppLifetime>();
            lifetime.OnStopping(() => trace.Add("Stopping"));
            lifetime.OnStopped(() => trace.Add("Stopped"));
            service.OnStart = lifetime.Cancel;
            return service;
        }))).Build();
        await app.RunAsync();
        Assert.Equal(new[] { "service.Start", "Stopping", "service.Stop", "Stopped", "service.Dispose" }, trace);
        Assert.Equal(1, service.DisposeCount);
        await app.DisposeAsync();
        Assert.Equal(1, service.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopFailure_ContinuesReverseShutdownAndIsObservable(bool microsoftDI)
    {
        var trace = new List<string>();
        var stopError = new InvalidOperationException("second stop");
        var first = new RecordingService("first", trace);
        var second = new RecordingService("second", trace) { StopError = stopError };
        await using var app = Builder(microsoftDI).Setup(context => context.Services(services => services
            .Singleton<IStartable>(_ => first).Singleton<IStartable>(_ => second))).Build();
        await app.StartAsync();
        Assert.Same(stopError, await Assert.ThrowsAsync<InvalidOperationException>(() => app.StopAsync()));
        await app.StopAsync();
        Assert.Equal(new[] { "first.Start", "second.Start", "second.Stop", "first.Stop" }, trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentStartStopDispose_AreIdempotentAndRestartIsRejected(bool microsoftDI)
    {
        var trace = new List<string>();
        var service = new RecordingService("service", trace);
        var app = Builder(microsoftDI).Setup(context => context.Services(services => services.Singleton<IStartable>(_ => service))).Build();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => app.StartAsync()));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => app.StopAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync());
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => app.DisposeAsync().AsTask()));
        Assert.Equal(new[] { "service.Start", "service.Stop", "service.Dispose" }, trace);
        Assert.Equal(1, service.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => app.StartAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => app.StopAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeRunningApp_StopsAndDisposesServicesOnce(bool microsoftDI)
    {
        var trace = new List<string>();
        var service = new RecordingService("service", trace);
        var app = Builder(microsoftDI).Setup(context => context.Services(services => services.Singleton<IStartable>(_ => service))).Build();
        await app.StartAsync();
        await app.DisposeAsync();
        app.Dispose();
        Assert.Equal(new[] { "service.Start", "service.Stop", "service.Dispose" }, trace);
        Assert.Equal(1, service.DisposeCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopOnlyService_IsStoppedEvenWhenNotStartable(bool microsoftDI)
    {
        var service = new StopOnlyService();
        await using var app = Builder(microsoftDI).Setup(context => context.Services(services => services.Singleton<IStoppable>(_ => service))).Build();
        await app.StartAsync();
        await app.StopAsync();
        await app.StopAsync();
        Assert.Equal(1, service.StopCount);
    }

    [Fact]
    public async Task MissingLifetime_StillDisposesOwnedApplication()
    {
        var app = new StubApp();
        await Assert.ThrowsAsync<AppCreationException>(() => app.RunAsync());
        Assert.Equal(0, app.StartCount);
        Assert.Equal(0, app.StopCount);
        Assert.Equal(1, app.DisposeCount);
    }

    [Fact]
    public async Task AlreadyCancelledRun_DisposesWithoutStarting()
    {
        var app = new StubApp { Lifetime = new StubLifetime() };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.RunAsync(cancellation.Token));
        Assert.Equal(0, app.StartCount);
        Assert.Equal(0, app.StopCount);
        Assert.Equal(1, app.DisposeCount);
    }

    [Fact]
    public async Task CustomApp_StartupFailureStillAttemptsStopAndDispose()
    {
        var startup = new InvalidOperationException("custom startup");
        var app = new StubApp { Lifetime = new StubLifetime(), StartupError = startup };
        Assert.Same(startup, await Assert.ThrowsAsync<InvalidOperationException>(() => app.RunAsync()));
        Assert.Equal(1, app.StartCount);
        Assert.Equal(1, app.StopCount);
        Assert.Equal(1, app.DisposeCount);
    }

    [Fact]
    public void BuilderCreationFailure_DisposesResolverAndPreservesOriginalError()
    {
        var resolver = new TrackingResolver();
        var failure = new InvalidOperationException("app creation");
        var builder = new FailingBuilder(new TrackingResolverBuilder(resolver), failure);
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => builder.Build()));
        Assert.Equal(1, resolver.DisposeCount);
    }

    [Fact]
    public void Configurator_MustHaveExactSingleParameterVoidMethod()
    {
        Assert.Throws<AppCreationException>(() => { _ = App.RunAsync<WrongConfigurator>(); });
    }

    [Fact]
    public async Task ConfiguratorFailure_PreservesInnerException()
    {
        Assert.Same(ThrowingConfigurator.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => App.RunAsync<ThrowingConfigurator>()));
    }

    [Fact]
    public void StoppedAndDisposedApplication_IsCollectibleAfterConsoleUnsubscription()
    {
        var app = CreateCollectibleApp();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(app.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleApp()
    {
        var app = App.Build();
        app.StartAsync().GetAwaiter().GetResult();
        app.StopAsync().GetAwaiter().GetResult();
        app.Dispose();
        return new WeakReference(app);
    }

    public sealed class RecordingService(string name, List<string> trace) : IStartable, IStoppable, IDisposable
    {
        public Exception? StartError { get; init; }
        public Exception? StopError { get; init; }
        public Exception? DisposeError { get; init; }
        public Action? OnStart { get; set; }
        public CancellationToken StopToken { get; private set; }
        public int DisposeCount { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            trace.Add(name + ".Start");
            if (StartError != null) return Task.FromException(StartError);
            OnStart?.Invoke();
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopToken = cancellationToken;
            trace.Add(name + ".Stop");
            if (StopError != null) return Task.FromException(StopError);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            DisposeCount++;
            trace.Add(name + ".Dispose");
            if (DisposeError != null) throw DisposeError;
        }
    }
    private sealed class StopOnlyService : IStoppable
    {
        public int StopCount { get; private set; }
        public Task StopAsync(CancellationToken cancellationToken = default) { StopCount++; return Task.CompletedTask; }
    }
    private sealed class StubApp : IApp, IServiceProvider
    {
        public IAppLifetimeHandler? Lifetime { get; init; }
        public Exception? StartupError { get; init; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public string Name => "stub";
        public IServiceProvider Services => this;
        public IConfiguration Configuration => new CommonConfiguration([]);
        public object? GetService(Type serviceType) => serviceType == typeof(IAppLifetimeHandler) ? Lifetime : null;
        public Task StartAsync(CancellationToken token = default) { StartCount++; return StartupError == null ? Task.CompletedTask : Task.FromException(StartupError); }
        public Task StopAsync(CancellationToken token = default) { StopCount++; return Task.CompletedTask; }
        public void Dispose() => DisposeCount++;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
    private sealed class StubLifetime : IAppLifetimeHandler
    {
        public Task WaitAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Stopping() { }
        public void Stopped() { }
    }
    private sealed class TrackingResolver : IServiceResolver, IDisposable
    {
        public int DisposeCount { get; private set; }
        public object? GetService(Type type) => null;
        public T Resolve<T>() => throw new InvalidOperationException();
        public T? TryResolve<T>() => default;
        public void Dispose() => DisposeCount++;
    }
    private sealed class TrackingResolverBuilder(TrackingResolver resolver) : IServiceResolverBuilder
    {
        public IServiceResolver Build(IConfiguration configuration, IEnumerable<Action<IConfiguration, IServicesRegistrationContext>> registrations) => resolver;
    }
    private sealed class FailingBuilder(IServiceResolverBuilder resolver, Exception failure) : AppBuilder(resolver)
    {
        protected override IApp CreateApp(IServiceResolver serviceResolver) => throw failure;
    }
    public sealed class WrongConfigurator
    {
        public void Configure(IAppConfigurationContext context, int extra) { }
    }
    public sealed class ThrowingConfigurator
    {
        public static readonly Exception Failure = new InvalidOperationException("configuration");
        public void Configure(IAppConfigurationContext context) => throw Failure;
    }
}
