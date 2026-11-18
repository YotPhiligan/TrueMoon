namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>Comparison of current authoring inputs against a selected published import.</summary>
public enum ModelImportFreshnessStatus
{
    /// <summary>The captured bytes and requested import settings match.</summary>
    Current,
    /// <summary>The captured bytes or requested import settings changed.</summary>
    Stale,
    /// <summary>The current source closure could not be captured.</summary>
    Unavailable
}

/// <summary>Point-in-time result of rechecking one published import.</summary>
/// <param name="Status">Whether the selected import matches the current authoring inputs.</param>
/// <param name="CapturedInputRevision">Revision of the current root and dependency bytes, if captured.</param>
/// <param name="InputChanged">Whether the captured bytes changed, or null if the closure was unavailable.</param>
/// <param name="SettingsChanged">Whether the requested geometry scale differs from the published scale.</param>
/// <param name="CaptureError">Source capture failure when the closure was unavailable.</param>
public sealed record ModelImportFreshness(ModelImportFreshnessStatus Status, string? CapturedInputRevision,
    bool? InputChanged, bool SettingsChanged, Exception? CaptureError);
