namespace TrueMoon.Aether.Editor.Editing;

/// <summary>Last observed source state, independent of scene dirty state and runtime identity.</summary>
public enum EditorSourceState
{
    /// <summary>No successful Build has been bound.</summary>
    NoBuild,
    /// <summary>A baseline or relevant hint awaits a complete observation.</summary>
    Checking,
    /// <summary>The last completed bounded pass found no difference.</summary>
    ObservedUnchanged,
    /// <summary>A proven difference is latched until a new successful Build.</summary>
    Changed,
    /// <summary>Coverage or input identity cannot currently be established.</summary>
    Unavailable,
    /// <summary>The project owner has fenced publication and is draining or closed.</summary>
    Stopped
}

/// <summary>Acceleration mode; complete polling covers all descriptor targets in either mode.</summary>
public enum EditorSourceObservationMode
{
    /// <summary>Polling supplies coverage; notifications are disabled, incomplete or unavailable.</summary>
    Polling,
    /// <summary>Bounded notifications accelerate complete polling.</summary>
    NotificationsAndPolling
}

/// <summary>Immutable value-only publication, pulled by presentation on its owner thread.</summary>
public sealed record EditorSourceSnapshot(Guid OwnerKey, long BaselineVersion, string? BuildId,
    string? InputFingerprint, string? SourceFingerprint, long ObservationVersion, long SignalVersion,
    EditorSourceState State, DateTimeOffset? ObservedAt, bool IsScanning, EditorSourceObservationMode Mode,
    int WatcherCount, string? Reason, IReadOnlyList<string> ChangedPaths);
