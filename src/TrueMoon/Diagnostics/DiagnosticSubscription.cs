using System.Diagnostics;

namespace TrueMoon.Diagnostics;

/// <summary>Owns both the global listener subscription and all matching source subscriptions.</summary>
public class DiagnosticSubscription : IDisposable
{
    private readonly DiagnosticObserver _observer;
    private IDisposable? _allListeners;

    public DiagnosticSubscription(DiagnosticsConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _observer = new DiagnosticObserver(configuration);
        try { _allListeners = DiagnosticListener.AllListeners.Subscribe(_observer); }
        catch
        {
            _observer.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _allListeners, null)?.Dispose();
        _observer.Dispose();
        GC.SuppressFinalize(this);
    }
}
