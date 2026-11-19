namespace TrueMoon.Aether.Tooling.Importing;

/// <summary>One authoring selection to observe against an immutable published import.</summary>
/// <param name="Source">The root file and capture policy for the selected model.</param>
/// <param name="Published">The exact published import to compare against.</param>
/// <param name="GeometryScale">The scale requested for a subsequent import.</param>
public sealed record ModelSourceWatchArguments(ModelSourceCaptureArguments Source, PublishedModelImport Published,
    float GeometryScale = 1)
{
    /// <summary>Whether to use native notifications in addition to complete polling.</summary>
    public bool UseFileSystemNotifications { get; init; } = true;

    /// <summary>Time between complete polling checks, from 100 milliseconds to one minute.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(10);
}
