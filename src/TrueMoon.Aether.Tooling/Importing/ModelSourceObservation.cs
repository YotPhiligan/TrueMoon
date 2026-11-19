namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>A completed advisory comparison against the watch's published import.</summary>
/// <param name="Sequence">Monotonically increasing comparison number, starting at one.</param>
/// <param name="Freshness">Authoring input comparison at the time of capture.</param>
/// <param name="TriggeredByNotification">Whether a native signal woke this pass.</param>
/// <param name="NotificationsActive">Whether native notifications were active when the pass completed.</param>
public sealed record ModelSourceObservation(long Sequence, ModelImportFreshness Freshness,
    bool TriggeredByNotification, bool NotificationsActive);
