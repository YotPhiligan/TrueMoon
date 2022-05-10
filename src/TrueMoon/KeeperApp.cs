namespace TrueMoon;

/// <inheritdoc />
public class KeeperApp : IApp
{
    /// <inheritdoc />
    public void Dispose()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        switch (Services)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        GC.SuppressFinalize(this);
    }

    public IServiceProvider Services { get; }
    public IAppParameters Parameters { get; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}