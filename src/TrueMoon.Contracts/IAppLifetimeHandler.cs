namespace TrueMoon;

public interface IAppLifetimeHandler
{
    Task WaitAsync(CancellationToken cancellationToken = default);
    void Stopping();
    void Stopped();

    void SetCancellationTokenSource(CancellationTokenSource source);
}