using TrueMoon.Diagnostics;

namespace TrueMoon.Mithril.Units;

/// <summary>Marks services hosted in the current App graph; it never launches an OS process.</summary>
public class MainProcessUnitHandle : UnitHandleBase
{
    /// <summary>Creates a local unit marker. App owns the services' startup, stop and disposal.</summary>
    /// <param name="configuration">The local unit configuration.</param>
    /// <param name="eventsSource">Diagnostics.</param>
    public MainProcessUnitHandle(IUnitConfiguration configuration, IEventsSource eventsSource) : base(configuration, eventsSource) { }

    /// <inheritdoc />
    public override Task<bool?> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<bool?>(true);
    }
}
