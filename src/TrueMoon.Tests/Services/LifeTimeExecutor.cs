
namespace TrueMoon.Tests.Services;

public class LifeTimeExecutor(IAppLifetime lifetime) : IStartable, IStoppable, IDisposable
{
    public bool IsStarted { get; private set; }
    public bool IsStopped { get; private set; }
    public int DisposeCount { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        lifetime.Cancel();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsStopped = true;
        return Task.CompletedTask;
    }
    public void Dispose() => DisposeCount++;
}
