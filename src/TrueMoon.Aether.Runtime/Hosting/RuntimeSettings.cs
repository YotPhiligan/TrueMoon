using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime.Hosting;

/// <summary>Data-only engine startup settings. Runtime identity and service factories are supplied separately.</summary>
public sealed record RuntimeSettings
{
    /// <summary>Report root, normalized before registration.</summary>
    public string ReportDirectory { get; set; } = "aether-reports";
    /// <summary>Frozen validation request for native runtime launches.</summary>
    public bool GpuValidation { get; set; }
    /// <summary>Copied ChildDiagnostics limits.</summary>
    public ChildDiagnosticDeliveryOptions ChildDiagnostics { get; set; } = new();
    /// <summary>Copied Deadlines limits.</summary>
    public SessionDeadlines Deadlines { get; set; } = new();
    /// <summary>Copied ReplacementDeadlines limits.</summary>
    public ReplacementDeadlines ReplacementDeadlines { get; set; } = new();
    internal RuntimeSettings Copy() => this with
    {
        ChildDiagnostics = new ChildDiagnosticDeliveryOptions { Enabled = ChildDiagnostics.Enabled, MaxBatchRecords = ChildDiagnostics.MaxBatchRecords, MaxBatchBytes = ChildDiagnostics.MaxBatchBytes, RecordsPerSecond = ChildDiagnostics.RecordsPerSecond, BytesPerSecond = ChildDiagnostics.BytesPerSecond, PollInterval = ChildDiagnostics.PollInterval, RequestTimeout = ChildDiagnostics.RequestTimeout, ShutdownTimeout = ChildDiagnostics.ShutdownTimeout },
        Deadlines = Deadlines with { },
        ReplacementDeadlines = ReplacementDeadlines with { }
    };
    internal void Validate()
    {
        var options = new RuntimeOptions { HostContractId = new('0',64), GameBuildId = new('0',64) };
        ApplyTo(options);
        var snapshot = options.Snapshot();
        ReportDirectory = snapshot.ReportDirectory;
    }
    /// <summary>Copies prepared data without invoking any factory.</summary>
    /// <param name="options">Engine options with separately supplied identity and factories.</param>
    public void ApplyTo(RuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options); var copy = Copy();
        options.ReportDirectory = copy.ReportDirectory;
        options.ChildDiagnostics = copy.ChildDiagnostics;
        options.Deadlines = copy.Deadlines;
        options.ReplacementDeadlines = copy.ReplacementDeadlines;
        if (options.RuntimeLaunch is not null) options.RuntimeLaunch = options.RuntimeLaunch.WithStartupSettings(new RuntimeStartupSettings(GpuValidation));
    }
}
