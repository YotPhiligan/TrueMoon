namespace TrueMoon;

/// <summary>Forwards App lifecycle calls without taking disposal ownership of the service.</summary>
/// <param name="startable">Borrowed startup service.</param><param name="stoppable">Borrowed shutdown service.</param>
public sealed class AppServiceLifecycle(IStartable? startable = null, IStoppable? stoppable = null) : IStartable, IStoppable
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
        => startable?.StartAsync(cancellationToken) ?? Task.CompletedTask;
    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
        => stoppable?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
