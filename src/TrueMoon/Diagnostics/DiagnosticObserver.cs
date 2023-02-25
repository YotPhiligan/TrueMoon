using System.Diagnostics;

namespace TrueMoon.Diagnostics;

internal class DiagnosticObserver : IObserver<DiagnosticListener>
{
    private readonly DiagnosticsConfiguration _configuration;
    private readonly LoggingObserver _loggingObserver;

    public DiagnosticObserver(DiagnosticsConfiguration configuration)
    {
        _configuration = configuration;
        _loggingObserver = new LoggingObserver(_configuration);
    }

    public void OnNext(DiagnosticListener value)
    {
        if (_configuration.Filters.Any(t=>value.Name.StartsWith(t)))
        {
            value.Subscribe(_loggingObserver);
        }
    }
    public void OnCompleted() {}
    public void OnError(Exception error) {}
}