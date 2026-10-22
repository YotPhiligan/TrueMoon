namespace TrueMoon.Mithril;

// Public interface forwarding does not create another provider-owned disposable alias.
internal sealed class BorrowedUnitsController(UnitsController owner) : IUnitsController
{
    public Task SpawnUnitAsync(string unitName, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
        => owner.SpawnUnitAsync(unitName, parameters, cancellationToken);
    public Task SpawnUnitAsync(int index, IReadOnlyList<object>? parameters = default, CancellationToken cancellationToken = default)
        => owner.SpawnUnitAsync(index, parameters, cancellationToken);
}
