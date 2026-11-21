using System.Diagnostics;

namespace TrueMoon.Diagnostics;

/// <summary>Owns the App's event routing and borrows its separate pipeline.</summary>
public class DiagnosticSubscription : IDisposable
{
    private readonly DiagnosticObserver? _observer;
    private readonly DiagnosticsConfiguration _configuration;
    private readonly LoggingObserver? _localObserver;
    private readonly DiagnosticPipeline? _pipeline;
    private IDisposable? _allListeners;
    private int _closed;

    public DiagnosticSubscription(DiagnosticsConfiguration configuration) : this(configuration, null) { }

    /// <summary>Creates a bridge borrowing an App-owned asynchronous pipeline.</summary>
    /// <param name="configuration">Legacy listeners and discovery filters.</param>
    /// <param name="pipeline">Optional borrowed pipeline; disposed by its separate owner after this bridge.</param>
    public DiagnosticSubscription(DiagnosticsConfiguration configuration, DiagnosticPipeline? pipeline)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _pipeline = pipeline;
        if (pipeline?.AppLocalSourcesOnly == true)
        {
            _localObserver = new LoggingObserver(configuration, pipeline);
            return;
        }
        _observer = new DiagnosticObserver(configuration, pipeline);
        try { _allListeners = DiagnosticListener.AllListeners.Subscribe(_observer); }
        catch
        {
            _observer.Dispose();
            throw;
        }
    }

    internal DiagnosticSource CreateSource(string name)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        if (_localObserver is null) return new DiagnosticListener(name);

        var matches = _configuration.Filters.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
        return new AppLocalSource(this, name, matches);
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _closed, 1);
        _localObserver?.Disable();
        Interlocked.Exchange(ref _allListeners, null)?.Dispose();
        _observer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class AppLocalSource(DiagnosticSubscription owner, string source, bool matches) : DiagnosticSource
    {
        public override bool IsEnabled(string name)
            => matches && Volatile.Read(ref owner._closed) == 0;

        public override bool IsEnabled(string name, object? arg1, object? arg2 = null)
            => IsEnabled(name) && (arg1 is not DiagnosticLevel level ||
                owner._configuration.ListenerSnapshot.Length != 0 || owner._pipeline!.IsEnabled(level, source, name));

        public override void Write(string name, object? value)
        {
            if (IsEnabled(name)) owner._localObserver!.OnNext(source, new(name, value));
        }
    }
}
