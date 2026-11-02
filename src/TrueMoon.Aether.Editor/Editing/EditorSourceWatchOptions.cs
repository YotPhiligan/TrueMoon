namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Copied source-observation scheduling policy. Tracking never authorizes Build or Start.</summary>
public sealed class EditorSourceWatchOptions
{
    /// <summary>Enables activation after a successful frozen Build.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Uses bounded native notifications in addition to full polling.</summary>
    public bool UseFileSystemNotifications { get; set; } = true;
    /// <summary>Quiet period following a source hint.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(250);
    /// <summary>Maximum delay from the first pending hint, excluding an already running pass's actual drain.</summary>
    public TimeSpan MaximumDebounce { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Full polling interval measured after completion of the previous pass.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Cooperative budget for a complete pass, including transient retries.</summary>
    public TimeSpan ScanBudget { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>Native handle limit, from zero (polling only) through32.</summary>
    public int MaximumWatchers { get; set; } = 32;

    internal EditorSourceWatchOptions Snapshot()
    {
        if (Debounce <= TimeSpan.Zero || Debounce > TimeSpan.FromSeconds(1) ||
            MaximumDebounce < Debounce || MaximumDebounce > TimeSpan.FromSeconds(1) ||
            PollInterval < TimeSpan.FromMilliseconds(1) || PollInterval > TimeSpan.FromMinutes(1) ||
            ScanBudget <= TimeSpan.Zero || ScanBudget > TimeSpan.FromSeconds(5) || MaximumWatchers is < 0 or > 32)
            throw new ArgumentOutOfRangeException(nameof(EditorSourceWatchOptions), "Invalid bounded source-observation policy.");
        return new() { Enabled = Enabled, UseFileSystemNotifications = UseFileSystemNotifications, Debounce = Debounce,
            MaximumDebounce = MaximumDebounce, PollInterval = PollInterval, ScanBudget = ScanBudget, MaximumWatchers = MaximumWatchers };
    }
}
