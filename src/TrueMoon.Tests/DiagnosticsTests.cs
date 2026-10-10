using System.Collections.Concurrent;
using System.Diagnostics;
using TrueMoon.Diagnostics;
using TrueMoon.Dependencies;
using TrueMoon.Services;
using Xunit;

namespace TrueMoon.Tests;

public class DiagnosticsTests
{
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
        var received = new List<DiagnosticEvent>();
        var failure = new InvalidOperationException("creation failed");
        var actual = Record.Exception(() => App.Build(context => context
            .UseDiagnostics(configuration => configuration.Filters(name).OnEvent(received.Add))
            .Services(services => services.RemoveAll<IApp>().Singleton<IApp>(_ => throw failure))));
        Assert.NotNull(actual);
        Assert.Contains("creation failed", actual.ToString());
        Assert.True(ContainsException(actual, failure));
        using var probe = new DiagnosticListener(name);
        Assert.False(probe.IsEnabled());
        probe.Write("after", DiagnosticEvent.Trace());
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
