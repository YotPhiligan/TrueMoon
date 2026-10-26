using System.Diagnostics;

namespace TrueMoon.Diagnostics;

internal sealed class DiagnosticObserver : IObserver<DiagnosticListener>, IDisposable
{
    private readonly Lock _lock = new();
    private readonly DiagnosticsConfiguration _configuration;
    private readonly LoggingObserver _loggingObserver;
    private readonly Dictionary<DiagnosticListener, SourceSubscription> _subscriptions = new();
    private bool _disposed;
    private readonly DiagnosticPipeline? _pipeline;

    public DiagnosticObserver(DiagnosticsConfiguration configuration, DiagnosticPipeline? pipeline = null)
    {
        _configuration = configuration;
        _pipeline = pipeline;
        _loggingObserver = new LoggingObserver(configuration, pipeline);
    }

    public void OnNext(DiagnosticListener value)
    {
        if (!_configuration.Filters.Any(filter => value.Name.StartsWith(filter, StringComparison.Ordinal))) return;
        SourceSubscription subscription;
        lock (_lock)
        {
            if (_disposed || _subscriptions.ContainsKey(value)) return;
            subscription = new SourceSubscription(this, value, _loggingObserver);
            // Reserve before Subscribe: a source can complete synchronously or on another thread.
            _subscriptions.Add(value, subscription);
        }
        try
        {
            // Preserve virtual legacy Subscribe dispatch, including third-party listener completion hooks.
            subscription.Attach(_pipeline is null ? value.Subscribe(subscription) :
                value.Subscribe(subscription, (eventName, context, _) => context is not DiagnosticLevel level ||
                    _configuration.ListenerSnapshot.Length != 0 || _pipeline.IsEnabled(level, value.Name, eventName)));
            // A listener disposed before Subscribe can return an empty handle without OnCompleted.
            if (!value.IsEnabled()) Remove(value, subscription);
        }
        catch
        {
            Remove(value, subscription);
            throw;
        }
    }

    private void Remove(DiagnosticListener source, SourceSubscription subscription)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(source, out var current) && ReferenceEquals(current, subscription))
                _subscriptions.Remove(source);
        }
        subscription.Dispose();
    }

    public void OnCompleted() { }
    public void OnError(Exception error) { }

    public void Dispose()
    {
        IDisposable[] subscriptions;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _loggingObserver.Disable();
            subscriptions = _subscriptions.Values.ToArray();
            _subscriptions.Clear();
        }
        foreach (var subscription in subscriptions) subscription.Dispose();
    }

    private sealed class SourceSubscription(DiagnosticObserver owner, DiagnosticListener source, LoggingObserver observer)
        : IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private IDisposable? _subscription;
        private int _stopped;

        public void Attach(IDisposable subscription)
        {
            Volatile.Write(ref _subscription, subscription);
            // Dispose/OnCompleted may have run before Subscribe returned its handle.
            if (Volatile.Read(ref _stopped) != 0)
                Interlocked.Exchange(ref _subscription, null)?.Dispose();
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (Volatile.Read(ref _stopped) == 0) observer.OnNext(source.Name, value);
        }

        public void OnCompleted() => owner.Remove(source, this);

        public void OnError(Exception error)
        {
            try { observer.OnError(error); }
            finally { owner.Remove(source, this); }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _stopped, 1);
            Interlocked.Exchange(ref _subscription, null)?.Dispose();
        }
    }
}
