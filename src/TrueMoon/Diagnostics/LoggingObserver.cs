namespace TrueMoon.Diagnostics;

internal sealed class LoggingObserver(DiagnosticsConfiguration configuration) : IObserver<KeyValuePair<string, object?>>
{
    private int _disabled;
    public void Disable() => Interlocked.Exchange(ref _disabled, 1);

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Value is not DiagnosticEvent payload) return;
        payload.SetName(value.Key);
        Publish(payload);
    }

    private void Publish(DiagnosticEvent payload)
    {
        foreach (var listener in configuration.Listeners)
        {
            if (Volatile.Read(ref _disabled) != 0) return;
            try { listener(payload); }
            catch (Exception) { /* Observers must not disrupt the application or other listeners. */ }
        }
    }

    public void OnCompleted() { }
    public void OnError(Exception error) => Publish(DiagnosticEvent.Exception(error));
}
