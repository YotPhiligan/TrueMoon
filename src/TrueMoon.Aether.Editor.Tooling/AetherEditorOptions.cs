using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.SourceTracking;

namespace TrueMoon.Aether.Editor.Tooling;

/// <summary>Initial project selection for an App-owned editor. Configuration performs no IO.</summary>
public sealed class AetherEditorOptions
{
    /// <summary>Optional .aetherproj opened during App startup, before a standard editor window starts.</summary>
    public string? ProjectPath { get; set; }
    /// <summary>Optional scene opened by the initial project session before the window starts.</summary>
    public string? InitialScenePath { get; set; }
    /// <summary>Restricts Build and Play to the configured development project.</summary>
    public bool SingleProject { get; set; }
    /// <summary>Optional frozen assets of the configured development project, used for its first scene profile.</summary>
    public GameBuildRenderContent? ProjectRenderContent { get; set; }
    /// <summary>Source observation policy, copied at composition; defaults to enabled after successful Build.</summary>
    public EditorSourceWatchOptions SourceTracking { get; set; } = new();

    internal AetherEditorOptions Snapshot()
    {
        ArgumentNullException.ThrowIfNull(SourceTracking);
        if (ProjectPath != null) ArgumentException.ThrowIfNullOrWhiteSpace(ProjectPath);
        if (ProjectPath != null && !ProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Editor ProjectPath requires a .aetherproj manifest.", nameof(ProjectPath));
        if (ProjectPath == null && (InitialScenePath != null || SingleProject || ProjectRenderContent != null))
            throw new ArgumentException("Development scene options require a project path.", nameof(ProjectPath));
        if (InitialScenePath != null) ArgumentException.ThrowIfNullOrWhiteSpace(InitialScenePath);
        return new()
        {
            ProjectPath = ProjectPath == null ? null : Path.GetFullPath(ProjectPath),
            InitialScenePath = InitialScenePath,
            SingleProject = SingleProject,
            ProjectRenderContent = ProjectRenderContent,
            SourceTracking = SourceTracking.Snapshot()
        };
    }
}
