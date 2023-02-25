namespace TrueMoon.Tests.Services;

public class CommonStartableService : IStartable
{
    public bool IsStarted { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        return Task.CompletedTask;
    }
}