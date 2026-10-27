namespace TrueMoon.Aether.Diagnostics;

/// <summary>Copied limits for the optional independent child diagnostic lane.</summary>
public sealed class ChildDiagnosticDeliveryOptions
{
    /// <summary>Enables delivery when the App supplies a bounded diagnostic history/importer.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Records in one packet, 1..32.</summary>
    public int MaxBatchRecords { get; set; } = 16;
    /// <summary>JSON body bytes in one packet, 1024..65536.</summary>
    public int MaxBatchBytes { get; set; } = 32 * 1024;
    /// <summary>Records admitted per monotonic one-second window, at least one batch and at most4096.</summary>
    public int RecordsPerSecond { get; set; } = 128;
    /// <summary>Conservatively accounted bytes per second including1024bytes per envelope, at most4MiB.</summary>
    public int BytesPerSecond { get; set; } = 256 * 1024;
    /// <summary>Polling interval, 10ms..1second.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);
    /// <summary>Local diagnostic call budget, 20ms..2seconds.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMilliseconds(250);
    /// <summary>Maximum public cleanup wait, 50ms..5seconds; half is available for graceful log drain.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(1);

    internal ChildDiagnosticDeliveryOptions Snapshot()
    {
        var copy = (ChildDiagnosticDeliveryOptions)MemberwiseClone();
        if (copy.MaxBatchRecords is < 1 or > 32 || copy.MaxBatchBytes is < 1024 or > 65536 ||
            copy.RecordsPerSecond < copy.MaxBatchRecords || copy.RecordsPerSecond > 4096 ||
            copy.BytesPerSecond < copy.MaxBatchBytes + 1024 || copy.BytesPerSecond > 4 * 1024 * 1024 ||
            copy.PollInterval < TimeSpan.FromMilliseconds(10) || copy.PollInterval > TimeSpan.FromSeconds(1) ||
            copy.RequestTimeout < TimeSpan.FromMilliseconds(20) || copy.RequestTimeout > TimeSpan.FromSeconds(2) ||
            copy.ShutdownTimeout < TimeSpan.FromMilliseconds(50) || copy.ShutdownTimeout > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(ChildDiagnosticDeliveryOptions));
        return copy;
    }
}
