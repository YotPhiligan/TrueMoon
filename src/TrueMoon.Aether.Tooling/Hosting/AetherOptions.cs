using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;

namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>Workflow configuration extending the standalone Runtime options.</summary>
public sealed class AetherOptions : RuntimeOptions
{
    /// <summary>Gets or sets the artifact/job root; null uses the report directory's builds subdirectory.</summary>
    public string? BuildDirectory { get; set; }
    /// <summary>Gets or sets the compiler budget, greater than zero and at most 120 seconds.</summary>
    public TimeSpan BuildTimeout { get; set; } = TimeSpan.FromSeconds(120);
    /// <summary>Completed build-job count, total bytes and age limits, snapshotted during App setup.</summary>
    public GameBuildRetentionOptions BuildRetention { get; set; } = new();
    internal new AetherOptions Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ReportDirectory);
        ArgumentNullException.ThrowIfNull(Deadlines);
        ArgumentNullException.ThrowIfNull(ReplacementDeadlines);
        ArgumentNullException.ThrowIfNull(RuntimeFactory);
        ArgumentNullException.ThrowIfNull(BuildRetention);
        ArgumentNullException.ThrowIfNull(ChildDiagnostics);
        var buildRetention = BuildRetention.Snapshot();
        var childDiagnostics = ChildDiagnostics.Snapshot();
        if (ViewportTransport != null && ViewportTransportFactory != null)
            throw new ArgumentException("Configure one viewport transport factory.");
        new SessionIdentity(Guid.NewGuid(), 1, HostContractId, GameBuildId, new string('0', 64)).Validate();
        Deadlines.Validate();
        ReplacementDeadlines.Validate();
        if (BuildTimeout <= TimeSpan.Zero || BuildTimeout > TimeSpan.FromSeconds(120)) throw new ArgumentOutOfRangeException(nameof(BuildTimeout));
        if (BuildDirectory != null) ArgumentException.ThrowIfNullOrWhiteSpace(BuildDirectory);
        return new()
        {
            HostContractId = HostContractId, GameBuildId = GameBuildId,
            ReportDirectory = Path.GetFullPath(ReportDirectory), Deadlines = Deadlines with { },
            ReplacementDeadlines = ReplacementDeadlines with { },
            BuildDirectory = Path.GetFullPath(BuildDirectory ?? Path.Combine(ReportDirectory, "builds")), BuildTimeout = BuildTimeout,
            BuildRetention = buildRetention, ChildDiagnostics = childDiagnostics,
            StatusChanged = StatusChanged, RuntimeFactory = RuntimeFactory, RuntimeLaunch = RuntimeLaunch, ViewportTransport = ViewportTransport,
            ViewportTransportFactory = ViewportTransportFactory
        };
    }
}
