namespace TrueMoon;

public interface IAppLifetime
{
    void Cancel();
    void OnStopped(Action action);
    void OnStopping(Action action);
    
    CancellationToken AppCancellationToken { get; }
}