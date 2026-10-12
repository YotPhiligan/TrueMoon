using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using TrueMoon.Diagnostics;

namespace TrueMoon.Mithril.Units;

/// <summary>Owns a child instance of the current application.</summary>
public class ChildProcessUnitHandle : ProcessUnitHandle
{
    /// <summary>Creates a child handle with explicit configuration and diagnostics.</summary>
    /// <param name="configuration">The child configuration.</param>
    /// <param name="eventsSource">The diagnostics source.</param>
    public ChildProcessUnitHandle(IUnitConfiguration configuration, IEventsSource eventsSource) : base(configuration, eventsSource)
    {
        
    }

    /// <inheritdoc />
    protected override async Task<ProcessStartInfo> GetProcessStartInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = await base.GetProcessStartInfoAsync(cancellationToken).ConfigureAwait(false);
        if (string.Equals(Path.GetFileNameWithoutExtension(info.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Entry assembly is not available."));
        return info;
    }
}
