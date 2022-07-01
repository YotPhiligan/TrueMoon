namespace TrueMoon.Enclaves;

public class EnclavesController : IEnclavesController, IStartable, IStoppable
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}