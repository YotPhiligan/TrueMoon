using System.Diagnostics;

namespace TrueMoon.Diagnostics;

internal sealed class DiagnosticObserver : IObserver<DiagnosticListener>, IDisposable
{
    private readonly Lock _lock = new();
    private readonly DiagnosticsConfiguration _configuration;
    private readonly LoggingObserver _loggingObserver;
    private readonly Dictionary<DiagnosticListener, IDisposable> _subscriptions = new();
    private bool _disposed;

    public DiagnosticObserver(DiagnosticsConfiguration configuration)
    {
        _configuration = configuration;
        _loggingObserver = new LoggingObserver(configuration);
    }

    public void OnNext(DiagnosticListener value)
    {
        if (!_configuration.Filters.Any(filter => value.Name.StartsWith(filter, StringComparison.Ordinal))) return;
        lock (_lock)
        {
            if (_disposed || _subscriptions.ContainsKey(value)) return;
            _subscriptions.Add(value, value.Subscribe(_loggingObserver));
        }
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
}
