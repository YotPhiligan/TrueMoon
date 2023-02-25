namespace TrueMoon.Diagnostics;

internal class LoggingObserver : IObserver<KeyValuePair<string, object?>>
{
    private readonly DiagnosticsConfiguration _diagnosticsConfiguration;

    public LoggingObserver(DiagnosticsConfiguration configuration)
    {
        _diagnosticsConfiguration = configuration;
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Value is DiagnosticEvent payload)
        {
            var eventName = value.Key;
            payload.SetName(eventName);
            
            foreach (var listener in _diagnosticsConfiguration.Listeners)
            {
                listener(payload);
            }
        }
    }
    public void OnCompleted() {}

    public void OnError(Exception error)
    {
        foreach (var listener in _diagnosticsConfiguration.Listeners)
        {
            listener(DiagnosticEvent.Exception(error));
        }
    }
}