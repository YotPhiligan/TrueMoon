namespace TrueMoon.Titanium.Units;

public interface IUnitHandle
{
    Task<bool?> StartAsync(CancellationToken cancellationToken = default);
    Task<bool?> StopAsync(CancellationToken cancellationToken = default);
    IUnitConfiguration GetConfiguration();
    void OnExit(Action<IUnitHandle> action);
}