namespace TrueMoon.Titanium;

public interface IUnitsController
{
    Task SpawnUnitAsync(string unitName, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default);
    Task SpawnUnitAsync(int index, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default);
}