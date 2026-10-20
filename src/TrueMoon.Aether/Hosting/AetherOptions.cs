using TrueMoon.Aether.Contracts;
using TrueMoon.Aether.Sessions;
using TrueMoon.Services;

namespace TrueMoon.Aether.Hosting;

/// <summary>Configures the App-owned engine session without starting a game.</summary>
public sealed class AetherOptions
{
    /// <summary>Gets or sets the SHA256 identity of the matching host/contracts build.</summary>
    public string HostContractId { get; set; } = "";
    /// <summary>Gets or sets the SHA256 identity of the selected game build.</summary>
    public string GameBuildId { get; set; } = "";
    /// <summary>Gets or sets the report directory, resolved during registration.</summary>
    public string ReportDirectory { get; set; } = "";
    /// <summary>Gets or sets bounded command, startup and heartbeat deadlines.</summary>
    public SessionDeadlines Deadlines { get; set; } = new();
    /// <summary>Gets or sets an optional session status observer.</summary>
    public Action<SessionStatus>? StatusChanged { get; set; }
    /// <summary>Gets or sets a factory creating a fresh App-owned runtime factory.</summary>
    /// <remarks>Dependencies may be resolved from the supplied resolver. Do not return
    /// a borrowed or separately registered disposable factory: the App provider owns the result.</remarks>
    public Func<IServiceResolver, IRuntimeFactory> RuntimeFactory { get; set; } = static _ => new NativeRuntimeFactory();

    internal AetherOptions Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ReportDirectory);
        ArgumentNullException.ThrowIfNull(Deadlines);
        ArgumentNullException.ThrowIfNull(RuntimeFactory);
        new SessionIdentity(Guid.NewGuid(), 1, HostContractId, GameBuildId, new string('0', 64)).Validate();
        Deadlines.Validate();
        return new()
        {
            HostContractId = HostContractId, GameBuildId = GameBuildId,
            ReportDirectory = Path.GetFullPath(ReportDirectory), Deadlines = Deadlines with { },
            StatusChanged = StatusChanged, RuntimeFactory = RuntimeFactory
        };
    }
}
