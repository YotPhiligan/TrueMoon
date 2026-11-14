using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>Explicit headless composition identity plus copied Tooling startup settings.</summary>
public sealed class ToolingOptions : ToolingSettings
{
    /// <summary>Exact target host or host/child pair fingerprint. It is supplied by composition, never ordinary configuration.</summary>
    public string HostContractId { get; set; } = "";
    /// <summary>Explicitly permits concrete Runtime APIs in built games. The default shares only Abstractions and Contracts.</summary>
    public bool RuntimeAccess { get; set; }

    internal new ToolingOptions Snapshot()
    {
        new SessionIdentity(Guid.NewGuid(), 1, HostContractId, HostContractId, HostContractId).Validate();
        var data = base.Snapshot();
        return new() { HostContractId = HostContractId, RuntimeAccess = RuntimeAccess, BuildDirectory = data.BuildDirectory, BuildTimeout = data.BuildTimeout,
            BuildRetention = data.BuildRetention, Authoring = data.Authoring, SourceTracking = data.SourceTracking };
    }
}
