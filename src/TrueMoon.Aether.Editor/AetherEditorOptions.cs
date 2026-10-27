namespace TrueMoon.Aether.Editor;

/// <summary>Initial project selection for an App-owned editor. Configuration performs no IO.</summary>
public sealed class AetherEditorOptions
{
    /// <summary>Optional .aetherproj opened during App startup, before a standard editor window starts.</summary>
    public string? ProjectPath { get; set; }

    internal AetherEditorOptions Snapshot()
    {
        if (ProjectPath == null) return new();
        ArgumentException.ThrowIfNullOrWhiteSpace(ProjectPath);
        if (!ProjectPath.EndsWith(".aetherproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Editor ProjectPath requires a .aetherproj manifest.", nameof(ProjectPath));
        return new() { ProjectPath = Path.GetFullPath(ProjectPath) };
    }
}
