using System.Diagnostics;

namespace TrueMoon.Diagnostics;

public class DiagnosticSubscription : IDisposable
{
    private readonly DiagnosticsConfiguration _configuration;
    private readonly IDisposable? _loggingListener;

    public DiagnosticSubscription(DiagnosticsConfiguration configuration)
    {
        _configuration = configuration;
        _loggingListener = DiagnosticListener.AllListeners.Subscribe(new DiagnosticObserver(_configuration));
    }

    public void Dispose()
    {
        _loggingListener?.Dispose();
        GC.SuppressFinalize(this);
    }
}