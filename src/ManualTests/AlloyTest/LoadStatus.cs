namespace AlloyTest;

/// <summary>The three explicitly controlled states of the demonstration's status region.</summary>
public enum LoadPhase
{
    /// <summary>Content is ready.</summary>
    Ready,
    /// <summary>A load is in progress.</summary>
    Loading,
    /// <summary>The load failed and can be retried.</summary>
    Error
}

/// <summary>An immutable state/message snapshot, observed by the status widget.</summary>
/// <param name="Phase">The desired branch.</param>
/// <param name="Message">The text to display in that branch.</param>
public readonly record struct LoadStatus(LoadPhase Phase, string Message)
{
    /// <summary>The initial ready state.</summary>
    public static LoadStatus Ready => new(LoadPhase.Ready, "Данные загружены.");
}
