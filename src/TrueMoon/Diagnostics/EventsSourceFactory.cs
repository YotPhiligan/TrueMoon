namespace TrueMoon.Diagnostics;

/// <summary>Owns the sources it creates and releases them when the resolver is disposed.</summary>
public class EventsSourceFactory : IEventsSourceFactory, IDisposable
{
    private readonly Lock _lock = new();
    private readonly List<EventsSource> _sources = [];
    private readonly DiagnosticSubscription? _subscription;
    private bool _disposed;

    public EventsSourceFactory() { }

    internal EventsSourceFactory(DiagnosticSubscription subscription) => _subscription = subscription;

    public IEventsSource Create(string name) => CreateCore(() => new EventsSource(name, _subscription));
    public IEventsSource<T> Create<T>() => CreateCore(() => new EventsSource<T>(_subscription));

    private TSource CreateCore<TSource>(Func<TSource> create) where TSource : EventsSource
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var source = create();
            _sources.Add(source);
            return source;
        }
    }

    public void Dispose()
    {
        EventsSource[] sources;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            sources = _sources.ToArray();
            _sources.Clear();
        }
        foreach (var source in sources) source.Dispose();
    }
}
