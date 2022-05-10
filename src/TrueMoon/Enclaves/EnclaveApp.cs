namespace TrueMoon.Enclaves;

public class EnclaveApp : IApp
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public IServiceProvider Services { get; }
    public IAppParameters Parameters { get; }
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}