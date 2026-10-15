using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TrueMoon.Diagnostics;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void CompletedSourcesAreReleasedWhileSubscriptionRemainsAlive()
    {
        var name = UniqueName();
        var received = new List<int>();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        configuration.AddEventListener(item => received.Add((int)item.Payload!));
        using var subscription = new DiagnosticSubscription(configuration);
        using var survivor = new DiagnosticListener(name);
        var completed = Enumerable.Range(0, 12).Select(index => CreateCompletedListener(name, index)).ToArray();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(completed, reference => Assert.False(reference.IsAlive));
        Assert.True(survivor.IsEnabled());
        survivor.Write("survivor", DiagnosticEvent.Create(42));
        using var replacement = new DiagnosticListener(name);
        Assert.True(replacement.IsEnabled());
        replacement.Write("replacement", DiagnosticEvent.Create(43));
        Assert.Equal(Enumerable.Range(0, 12).Append(42).Append(43), received);
        GC.KeepAlive(subscription);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedDiagnosticsSetupKeepsOnlyLastRegistration(bool microsoftDI)
    {
        var name = UniqueName();
        var oldEvents = new List<DiagnosticEvent>();
        var newEvents = new List<DiagnosticEvent>();
        await using var app = App.Builder(context => { if (microsoftDI) context.UseDI(); })
            .Setup(context => context
                .UseDiagnostics(configuration => configuration.Filters(name).OnEvent(item =>
                {
                    if (item.Name.StartsWith(name, StringComparison.Ordinal)) oldEvents.Add(item);
                }))
                .UseDiagnostics(configuration => configuration.Filters(name).OnEvent(item =>
                {
                    if (item.Name.StartsWith(name, StringComparison.Ordinal)) newEvents.Add(item);
                })))
            .Build();

        Assert.Single(app.Services.ResolveAll<DiagnosticSubscription>());
        var factory = Assert.Single(app.Services.ResolveAll<IEventsSourceFactory>());
        var source = factory.Create(name);
        source.Write(() => "last", caller: "proof");
        Assert.Empty(oldEvents);
        var received = Assert.Single(newEvents);
        Assert.Equal(name + ".proof", received.Name);
        Assert.Equal("last", received.Payload);
        await app.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => source.Trace());
        using var after = new DiagnosticListener(name);
        Assert.False(after.IsEnabled());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingOneAppKeepsOtherAppDiagnosticsActive(bool microsoftDI)
    {
        var name = UniqueName();
        var firstEvents = new List<string>();
        var secondEvents = new List<string>();
        IApp Build(List<string> received) => App.Builder(context => { if (microsoftDI) context.UseDI(); })
            .Setup(context => context.UseDiagnostics(configuration => configuration.Filters(name).OnEvent(item =>
            {
                if (item.Name.StartsWith(name, StringComparison.Ordinal)) received.Add((string)item.Payload!);
            }))).Build();
        await using var first = Build(firstEvents);
        await using var second = Build(secondEvents);
        Assert.NotSame(first.Configuration.GetDiagnosticsConfiguration(), second.Configuration.GetDiagnosticsConfiguration());
        var firstSource = first.Services.Resolve<IEventsSourceFactory>()!.Create(name + ".first");
        var secondSource = second.Services.Resolve<IEventsSourceFactory>()!.Create(name + ".second");
        firstSource.Write(() => "first");
        secondSource.Write(() => "second");

        await first.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => firstSource.Trace());
        secondSource.Write(() => "survivor");
        using var future = new DiagnosticListener(name + ".future");
        Assert.True(future.IsEnabled());
        future.Write(name + ".future.proof", DiagnosticEvent.Create("future"));

        Assert.Equal(new[] { "first", "second" }, firstEvents);
        Assert.Equal(new[] { "first", "second", "survivor", "future" }, secondEvents);
        await second.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => secondSource.Trace());
        Assert.False(future.IsEnabled());
        using var after = new DiagnosticListener(name + ".after");
        Assert.False(after.IsEnabled());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceCompletingDuringSubscribeIsReleased(bool beforeSubscribe)
    {
        var name = UniqueName();
        var received = new List<int>();
        using var subscription = SubscribeToCompletingListener(name, beforeSubscribe, received, out var completed, out var disposal);
        Assert.Equal(1, disposal.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(completed.IsAlive);
        using var replacement = new DiagnosticListener(name);
        Assert.True(replacement.IsEnabled());
        replacement.Write("replacement", DiagnosticEvent.Create(42));
        Assert.Equal(new[] { 42 }, received);
        GC.KeepAlive(subscription);
    }

    [Fact]
    public void NewFiltersOnlyApplyToSourcesDiscoveredAfterTheChange()
    {
        var name = UniqueName();
        var configuration = new DiagnosticsConfiguration();
        var received = new List<DiagnosticEvent>();
        configuration.AddEventListener(received.Add);
        using var subscription = new DiagnosticSubscription(configuration);
        using var existing = new DiagnosticListener(name);
        Assert.False(existing.IsEnabled());

        configuration.AddFilter(name);
        using var future = new DiagnosticListener(name);
        Assert.False(existing.IsEnabled());
        Assert.True(future.IsEnabled());
        existing.Write("ignored", DiagnosticEvent.Create("old"));
        future.Write("observed", DiagnosticEvent.Create("new"));
        var actual = Assert.Single(received);
        Assert.Equal("observed", actual.Name);
        Assert.Equal("new", actual.Payload);
    }

    [Fact]
    public void DisposingSubscriptionInsideCallbackStopsRemainingCallbacks()
    {
        var name = UniqueName();
        var firstEvents = new List<DiagnosticEvent>();
        var lateEvents = new List<DiagnosticEvent>();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        DiagnosticSubscription? subscription = null;
        configuration.AddEventListener(item =>
        {
            firstEvents.Add(item);
            subscription!.Dispose();
        });
        configuration.AddEventListener(lateEvents.Add);
        using var owned = subscription = new DiagnosticSubscription(configuration);
        using var source = new DiagnosticListener(name);

        source.Write("first", DiagnosticEvent.Create("seen"));
        source.Write("second", DiagnosticEvent.Create("ignored"));

        Assert.False(source.IsEnabled());
        var actual = Assert.Single(firstEvents);
        Assert.Equal("first", actual.Name);
        Assert.Equal("seen", actual.Payload);
        Assert.Empty(lateEvents);
        using var future = new DiagnosticListener(name);
        Assert.False(future.IsEnabled());
    }

    [Fact]
    public async Task ConcurrentSourceDiscoveryCompletionAndSubscriptionDisposalDetachAllSources()
    {
        var name = UniqueName();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        using var subscription = new DiagnosticSubscription(configuration);
        var sources = new ConcurrentBag<DiagnosticListener>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var creators = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            for (var index = 0; index < 32; index++)
            {
                var source = new DiagnosticListener(name);
                sources.Add(source);
                if (index % 2 == 0) source.Dispose();
            }
        })).ToArray();
        var disposal = Task.Run(async () =>
        {
            await start.Task;
            subscription.Dispose();
        });
        try
        {
            start.SetResult();
            await Task.WhenAll(creators.Append(disposal)).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(128, sources.Count);
            Assert.All(sources, source => Assert.False(source.IsEnabled()));
            using var after = new DiagnosticListener(name);
            Assert.False(after.IsEnabled());
        }
        finally
        {
            foreach (var source in sources) source.Dispose();
        }
    }

    [Fact]
    public void DisposingSubscriptionDetachesExistingAndFutureSources()
    {
        var name = UniqueName();
        using var existing = new DiagnosticListener(name);
        var received = new List<DiagnosticEvent>();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        configuration.AddEventListener(received.Add);
        using var subscription = new DiagnosticSubscription(configuration);
        Assert.True(existing.IsEnabled());
        existing.Write("before", DiagnosticEvent.Create("seen"));
        var first = Assert.Single(received);
        Assert.Equal("before", first.Name);
        Assert.Equal("seen", first.Payload);
        using var future = new DiagnosticListener(name + ".future");
        Assert.True(future.IsEnabled());
        subscription.Dispose();
        subscription.Dispose();
        Assert.False(existing.IsEnabled());
        Assert.False(future.IsEnabled());
        existing.Write("after", DiagnosticEvent.Create("ignored"));
        using var after = new DiagnosticListener(name + ".after");
        Assert.False(after.IsEnabled());
        Assert.Single(received);
    }

    [Fact]
    public void ListenerExceptionsDoNotStopDeliveryAndSubscriptionsRespectFilters()
    {
        var name = UniqueName();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        configuration.AddEventListener(_ => throw new InvalidOperationException("observer failed"));
        var received = new List<DiagnosticEvent>();
        configuration.AddEventListener(received.Add);
        using var subscription = new DiagnosticSubscription(configuration);
        using var source = new EventsSource(name);
        using var ignored = new EventsSource(UniqueName());
        var evaluated = false;
        ignored.Write(() => { evaluated = true; return "ignored"; });
        source.Write(() => "payload", category: "category", caller: "caller");
        source.Trace(caller: "trace");
        var error = new InvalidOperationException("original");
        source.Exception(error, caller: "error");
        Assert.False(evaluated);
        Assert.Equal(new[] { DiagnosticEventLevel.Message, DiagnosticEventLevel.Trace, DiagnosticEventLevel.Exception }, received.Select(item => item.EventLevel));
        Assert.Equal(name + ".caller.category", received[0].Name);
        Assert.Equal("payload", received[0].Payload);
        Assert.Same(error, received[2].Payload);
    }

    [Fact]
    public async Task ConfigurationCollectionsAreSnapshotsAndConcurrentChangesDoNotLoseDelivery()
    {
        var name = UniqueName();
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        var payloads = new ConcurrentBag<int>();
        configuration.AddEventListener(item => payloads.Add((int)item.Payload!));
        var filters = configuration.Filters;
        var listeners = configuration.Listeners;
        using var subscription = new DiagnosticSubscription(configuration);
        using var source = new EventsSource(name);
        await Task.WhenAll(
            Task.Run(() => { for (var index = 0; index < 100; index++) source.Write(() => index); }),
            Task.Run(() => { for (var index = 0; index < 100; index++) { configuration.AddFilter(name + index); configuration.AddEventListener(_ => { }); } }));
        Assert.Single(filters);
        Assert.Single(listeners);
        Assert.Equal(101, configuration.Filters.Count);
        Assert.Equal(101, configuration.Listeners.Count);
        Assert.Equal(Enumerable.Range(0, 100), payloads.Order());
    }

    [Fact]
    public void SourceDisposeReleasesListenerAndRejectsFurtherOperations()
    {
        var name = UniqueName();
        DiagnosticListener? listener = null;
        using var all = DiagnosticListener.AllListeners.Subscribe(new ListenerObserver(value =>
        {
            if (value.Name == name) listener = value;
        }));
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        var received = new List<DiagnosticEvent>();
        configuration.AddEventListener(received.Add);
        using var subscription = new DiagnosticSubscription(configuration);
        using var source = new EventsSource(name);
        Assert.True(listener!.IsEnabled());
        using (source.UseActivity("activity", caller: "work")) { }
        Assert.Equal(2, received.Count);
        Assert.Equal("activity", received[0].Payload);
        Assert.Equal(DiagnosticEventLevel.Trace, received[1].EventLevel);
        source.Dispose();
        source.Dispose();
        Assert.False(listener.IsEnabled());
        Assert.Throws<ObjectDisposedException>(() => source.Trace());
        Assert.Throws<ObjectDisposedException>(() => source.StartActivity());
        Assert.Throws<ObjectDisposedException>(() => source.StopActivity(new Activity("after")));
    }

    [Fact]
    public void FactoryOwnsNamedAndTypedSourcesAndRejectsCreationAfterDisposal()
    {
        using var factory = new EventsSourceFactory();
        var named = factory.Create(UniqueName());
        var typed = factory.Create<DiagnosticsTests>();
        Assert.Equal(typeof(DiagnosticsTests).FullName, typed.Name);
        factory.Dispose();
        factory.Dispose();
        Assert.Throws<ObjectDisposedException>(() => named.Trace());
        Assert.Throws<ObjectDisposedException>(() => typed.Trace());
        Assert.Throws<ObjectDisposedException>(() => factory.Create("after"));
        Assert.Throws<ObjectDisposedException>(() => factory.Create<DiagnosticsTests>());
    }

    [Fact]
    public void SetupFailureNeverLeavesAnEagerSubscription()
    {
        var name = UniqueName();
        var received = new List<DiagnosticEvent>();
        var failure = new InvalidOperationException("setup failed");
        var actual = Assert.Throws<InvalidOperationException>(() => App.Build(context =>
        {
            context.UseDiagnostics(configuration => configuration.Filters(name).OnEvent(received.Add));
            throw failure;
        }));
        Assert.Same(failure, actual);
        using var probe = new DiagnosticListener(name);
        Assert.False(probe.IsEnabled());
        probe.Write("after", DiagnosticEvent.Trace());
        Assert.Empty(received);
    }

    [Fact]
    public void AppCreationFailureDisposesResolverOwnedSubscriptions()
    {
        var name = UniqueName();
        var received = new ConcurrentBag<DiagnosticEvent>();
        var eventName = name + ".after";
        var failure = new InvalidOperationException("creation failed");
        var actual = Record.Exception(() => App.Build(context => context
            .UseDiagnostics(configuration => configuration.Filters(name).OnEvent(item =>
            {
                // Default TrueMoon filters also observe parallel Apps; pin this probe only.
                if (item.Name == eventName) received.Add(item);
            }))
            .Services(services => services.RemoveAll<IApp>().Singleton<IApp>(_ => throw failure))));
        Assert.NotNull(actual);
        Assert.Contains("creation failed", actual.ToString());
        Assert.True(ContainsException(actual, failure));
        using var probe = new DiagnosticListener(name);
        Assert.False(probe.IsEnabled());
        probe.Write(eventName, DiagnosticEvent.Trace());
        Assert.Empty(received);
    }

    [Fact]
    public async Task AppDiagnosticsAreObservedAndDisposedEvenWhenAListenerThrows()
    {
        var received = new ConcurrentBag<DiagnosticEvent>();
        await using var app = App.Build(context => context.UseDiagnostics(configuration => configuration
            .OnEvent(_ => throw new InvalidOperationException("listener failed"))
            .OnEvent(received.Add)));
        await app.StartAsync();
        await app.StopAsync();
        var factory = app.Services.Resolve<IEventsSourceFactory>()!;
        var source = factory.Create("TrueMoon.DiagnosticsTests." + Guid.NewGuid().ToString("N"));
        source.Trace(caller: "proof");
        Assert.Contains(received, item => item.Name == source.Name + ".proof");
        Assert.Contains(received, item => item.Name.StartsWith("TrueMoon.DefaultApp", StringComparison.Ordinal));
        Assert.Contains(received, item => item.Name.StartsWith("TrueMoon.App.", StringComparison.Ordinal));
        await app.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => source.Trace());
        using var after = new DiagnosticListener(source.Name + ".after");
        Assert.False(after.IsEnabled());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCompletedListener(string name, int value)
    {
        using var source = new DiagnosticListener(name);
        Assert.True(source.IsEnabled());
        source.Write("completed", DiagnosticEvent.Create(value));
        return new WeakReference(source);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DiagnosticSubscription SubscribeToCompletingListener(string name, bool beforeSubscribe,
        List<int> received, out WeakReference completed, out DisposalCounter disposal)
    {
        disposal = new DisposalCounter();
        using var source = new CompletingListener(name, beforeSubscribe, disposal);
        var configuration = new DiagnosticsConfiguration();
        configuration.AddFilter(name);
        configuration.AddEventListener(item => received.Add((int)item.Payload!));
        var subscription = new DiagnosticSubscription(configuration);
        Assert.False(source.IsEnabled());
        completed = new WeakReference(source);
        return subscription;
    }

    private sealed class CompletingListener(string name, bool beforeSubscribe, DisposalCounter disposal) : DiagnosticListener(name)
    {
        public override IDisposable Subscribe(IObserver<KeyValuePair<string, object?>> observer)
        {
            if (beforeSubscribe) Dispose();
            var handle = base.Subscribe(observer);
            if (!beforeSubscribe) Dispose();
            return new TrackedSubscription(handle, disposal);
        }
    }

    private sealed class DisposalCounter { public int Count; }

    private sealed class TrackedSubscription(IDisposable handle, DisposalCounter disposal) : IDisposable
    {
        public void Dispose()
        {
            disposal.Count++;
            handle.Dispose();
        }
    }

    private static bool ContainsException(Exception actual, Exception expected)
        => ReferenceEquals(actual, expected) || actual.InnerException is { } inner && ContainsException(inner, expected)
            || actual is AggregateException aggregate && aggregate.InnerExceptions.Any(error => ContainsException(error, expected));

    private static string UniqueName() => "DiagnosticsTests." + Guid.NewGuid().ToString("N");
    private sealed class ListenerObserver(Action<DiagnosticListener> action) : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener value) => action(value);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
