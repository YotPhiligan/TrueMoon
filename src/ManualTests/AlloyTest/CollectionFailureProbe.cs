using System.Collections.ObjectModel;
using System.Collections.Specialized;
using TrueMoon;
using TrueMoon.Alloy;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Argentis;
using TrueMoon.Extensions.DependencyInjection;

namespace AlloyTest;

internal static class CollectionFailureProbe
{
    internal static async Task RunAsync()
    {
        foreach (var initial in new[] { true, false })
        {
            var builder = App.Builder(context => context.UseDI());
            builder.Setup(context => context.UsePresentation<CollectionFailureView>(options => options.UseSkiaOpenGL().UseSilkWindow())
                .Services(services => services.Singleton<CollectionFailureState>()));
            var app = builder.Build();
            var state = (CollectionFailureState)app.Services.GetService(typeof(CollectionFailureState))!;
            state.Initial = initial;
            var window = (HostedUiWindow)app.Services.GetService(typeof(HostedUiWindow))!;
            window.FramePresented += _ => { if (window.PresentedFrames == 1) state.AddFailure(); };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                await app.StartAsync(timeout.Token);
                await ExpectFailure(window.Completion.WaitAsync(timeout.Token));
                await ExpectFailure(window.StopAsync(timeout.Token));
                var sessions = (HostedUiSessionFactory)app.Services.GetService(typeof(IUiSessionFactory))!;
                Require(sessions.ActiveSessionCount == 0, "Factory failure retained its UI session.");
                Require(state.Root is { IsDisposed: true, IsAttached: false }, "Factory failure retained its tree.");
                Require(state.Rows.Subscribers == 0, "Factory failure retained the collection subscription.");
                Require(state.Nodes.Count == (initial ? 1 : 2) && state.Nodes.All(node => node.IsDisposed), "Factory drafts or committed rows were not released.");
                Require(state.Resources.All(resource => resource.Disposals == 1), "Factory failure leaked or repeated cleanup.");
                Require(window.PresentedFrames == (initial ? 0 : 1), "Unexpected presentation after factory failure.");
            }
            finally { await ExpectFailure(app.DisposeAsync().AsTask()); }
        }
        Console.WriteLine("Collection factory failure propagation passed: initial/update failures reach Completion/StopAsync, registry/subscriptions zero, drafts and committed tree disposed once.");
    }

    private static async Task ExpectFailure(Task operation)
    {
        try { await operation; }
        catch (InvalidOperationException error) when (error.Message == CollectionFailureState.Error) { return; }
        throw new InvalidOperationException("Expected the collection factory failure to reach the host.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

/// <summary>The state of the bounded standalone collection failure probe.</summary>
public sealed class CollectionFailureState
{
    internal const string Error = "Collection factory failure probe";
    internal bool Initial;
    internal Element? Root;
    internal readonly ProbeCollection Rows = new() { new("kept") };
    internal readonly List<Element> Nodes = [];
    internal readonly List<Resource> Resources = [];
    internal void AddFailure() { Rows.Add(new SettingsRow("draft")); Rows.Add(new SettingsRow("fail")); }
    internal Element CreateRow(SettingsRow row)
    {
        if (row.Name == "fail") throw new InvalidOperationException(Error);
        var resource = new Resource(); Resources.Add(resource);
        var node = new Text(row.Name); node.Own(resource); Nodes.Add(node); return node;
    }

    internal sealed class Resource : IDisposable
    {
        internal int Disposals;
        public void Dispose() => Disposals++;
    }
    internal sealed class ProbeCollection : ObservableCollection<SettingsRow>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        internal int Subscribers;
        event NotifyCollectionChangedEventHandler? INotifyCollectionChanged.CollectionChanged
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        { base.OnCollectionChanged(args); _changed?.Invoke(this, args); }
    }
}

/// <summary>A view used only by the collection factory failure probe.</summary>
public sealed class CollectionFailureView : View
{
    /// <summary>Creates a bound tree which fails initially or after its first presented frame.</summary>
    /// <param name="state">The caller-owned probe state.</param>
    public CollectionFailureView(CollectionFailureState state)
    {
        state.Root = this;
        if (state.Initial) state.Rows.Add(new SettingsRow("fail"));
        SetContent(VStack.Create().BindItems(state.Rows, state.CreateRow));
    }
}
