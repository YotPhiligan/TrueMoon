namespace TrueMoon.Tests.Services;

public class LifeTimeExecutor : IStartable
{
    private readonly IAppLifetime _lifetime;

    public LifeTimeExecutor(IAppLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            _lifetime.Cancel();
        });
        return Task.CompletedTask;
    }
}