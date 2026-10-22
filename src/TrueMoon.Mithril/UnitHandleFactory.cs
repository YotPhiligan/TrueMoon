using TrueMoon.Diagnostics;
using TrueMoon.Mithril.Units;

namespace TrueMoon.Mithril;

/// <summary>Creates controller-owned handles for snapshotted unit hosting policies.</summary>
/// <param name="events">Borrowed controller diagnostics.</param>
public sealed class UnitHandleFactory(IEventsSource<UnitsController> events) : IFactory<IUnitHandle, IUnitConfiguration>
{
    /// <inheritdoc />
    public IUnitHandle Create(IUnitConfiguration arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.HostingPolicy switch
        {
            UnitHostingPolicy.ChildProcess => new ChildProcessUnitHandle(arguments, events),
            UnitHostingPolicy.MainProcess => new MainProcessUnitHandle(arguments, events),
            UnitHostingPolicy.External => new ExternalUnitHandle(arguments, events),
            _ => throw new ArgumentException("Unknown unit hosting mode.", nameof(arguments))
        };
    }
}
