using TrueMoon.Aether.Tooling.SourceTracking;

namespace TrueMoon.Aether.Editor.Tooling;

/// <summary>Initial project selection for an App-owned editor. Configuration performs no IO.</summary>
public sealed class AetherEditorOptions
{
    /// <summary>Optional .aetherproj opened during App startup, before a standard editor window starts.</summary>
    public string? ProjectPath { get; set; }
    /// <summary>Source observation policy, copied at composition; defaults to enabled after successful Build.</summary>
    public EditorSourceWatchOptions SourceTracking { get; set; } = new();

    internal AetherEditorOptions Snapshot()
    {
        ArgumentNullException.ThrowIfNull(SourceTracking);
        if (ProjectPath != null) ArgumentException.ThrowIfNullOrWhiteSpace(ProjectPath);
        if (ProjectPath != null && !ProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Editor ProjectPath requires a .aetherproj manifest.", nameof(ProjectPath));
        return new() { ProjectPath = ProjectPath == null ? null : Path.GetFullPath(ProjectPath), SourceTracking = SourceTracking.Snapshot() };
    }
}
