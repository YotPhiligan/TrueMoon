using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Aether.Tooling.SourceTracking;

namespace TrueMoon.Aether.Tooling.Hosting;

/// <summary>Startup data for headless authoring and explicit source builds. Contains no runtime identity or UI options.</summary>
public class ToolingSettings
{
    /// <summary>Dedicated artifact and compiler-report directory, resolved once during App setup.</summary>
    public string BuildDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "aether-builds");
    /// <summary>Owned compiler deadline, greater than zero and at most120seconds.</summary>
    public TimeSpan BuildTimeout { get; set; } = TimeSpan.FromSeconds(120);
    /// <summary>Completed-job retention policy, copied during setup.</summary>
    public GameBuildRetentionOptions BuildRetention { get; set; } = new();
    /// <summary>Authoring transaction and history limits.</summary>
    public SceneAuthoringOptions Authoring { get; set; } = new();
    /// <summary>Project-owned source observer policy. No worker starts until a successful tracked Build.</summary>
    public EditorSourceWatchOptions SourceTracking { get; set; } = new();

    internal ToolingSettings Snapshot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(BuildDirectory);
        ArgumentNullException.ThrowIfNull(BuildRetention);
        ArgumentNullException.ThrowIfNull(Authoring);
        ArgumentNullException.ThrowIfNull(SourceTracking);
        if (BuildTimeout <= TimeSpan.Zero || BuildTimeout > TimeSpan.FromSeconds(120))
            throw new ArgumentOutOfRangeException(nameof(BuildTimeout));
        return new()
        {
            BuildDirectory = Path.GetFullPath(BuildDirectory), BuildTimeout = BuildTimeout,
            BuildRetention = BuildRetention.Snapshot(), Authoring = Authoring.Snapshot(), SourceTracking = SourceTracking.Snapshot()
        };
    }
}
