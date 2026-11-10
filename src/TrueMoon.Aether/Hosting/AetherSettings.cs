using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Diagnostics;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Hosting;

/// <summary>Data-only engine startup settings. Runtime identity and service factories are supplied separately.</summary>
public sealed record AetherSettings
{
    /// <summary>Report root, normalized before registration.</summary>
    public string ReportDirectory { get; set; } = "aether-reports";
    /// <summary>Build root, or null to use the report root's builds child.</summary>
    public string? BuildDirectory { get; set; }
    /// <summary>Compiler deadline.</summary>
    public TimeSpan BuildTimeout { get; set; } = TimeSpan.FromSeconds(120);
    /// <summary>Frozen validation request for native runtime launches.</summary>
    public bool GpuValidation { get; set; }
    /// <summary>Copied BuildRetention limits.</summary>
    public GameBuildRetentionOptions BuildRetention { get; set; } = new();
    /// <summary>Copied ChildDiagnostics limits.</summary>
    public ChildDiagnosticDeliveryOptions ChildDiagnostics { get; set; } = new();
    /// <summary>Copied Deadlines limits.</summary>
    public SessionDeadlines Deadlines { get; set; } = new();
    /// <summary>Copied ReplacementDeadlines limits.</summary>
    public ReplacementDeadlines ReplacementDeadlines { get; set; } = new();
    internal AetherSettings Copy() => this with
    {
        BuildRetention = new GameBuildRetentionOptions { MaxJobs = BuildRetention.MaxJobs, MaxTotalBytes = BuildRetention.MaxTotalBytes, MaxAge = BuildRetention.MaxAge },
        ChildDiagnostics = new ChildDiagnosticDeliveryOptions { Enabled = ChildDiagnostics.Enabled, MaxBatchRecords = ChildDiagnostics.MaxBatchRecords, MaxBatchBytes = ChildDiagnostics.MaxBatchBytes, RecordsPerSecond = ChildDiagnostics.RecordsPerSecond, BytesPerSecond = ChildDiagnostics.BytesPerSecond, PollInterval = ChildDiagnostics.PollInterval, RequestTimeout = ChildDiagnostics.RequestTimeout, ShutdownTimeout = ChildDiagnostics.ShutdownTimeout },
        Deadlines = Deadlines with { },
        ReplacementDeadlines = ReplacementDeadlines with { }
    };
    internal void Validate()
    {
        var options = new AetherOptions { HostContractId = new('0',64), GameBuildId = new('0',64) };
        ApplyTo(options);
        var snapshot = options.Snapshot();
        ReportDirectory = snapshot.ReportDirectory; BuildDirectory = snapshot.BuildDirectory;
    }
    /// <summary>Copies prepared data without invoking any factory.</summary>
    /// <param name="options">Engine options with separately supplied identity and factories.</param>
    public void ApplyTo(AetherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options); var copy = Copy();
        options.ReportDirectory = copy.ReportDirectory; options.BuildDirectory = copy.BuildDirectory; options.BuildTimeout = copy.BuildTimeout;
        options.BuildRetention = copy.BuildRetention;
        options.ChildDiagnostics = copy.ChildDiagnostics;
        options.Deadlines = copy.Deadlines;
        options.ReplacementDeadlines = copy.ReplacementDeadlines;
        if (options.RuntimeLaunch is not null) options.RuntimeLaunch = options.RuntimeLaunch.WithStartupSettings(new RuntimeStartupSettings(GpuValidation));
    }
}
