namespace TrueMoon.Aether.Scenes;

/// <summary>Scene persistence boundary. A null expected stamp means create-only, never blind overwrite.</summary>
public interface ISceneDocumentStore
{
    /// <summary>Reads a bounded file and validates its complete authoring graph.</summary>
    /// <param name="path">Scene file path.</param><param name="cancellationToken">Read cancellation.</param>
    /// <returns>Document and observed file identity.</returns>
    Task<SceneFile> ReadAsync(string path, CancellationToken cancellationToken = default);
    /// <summary>Atomically publishes a snapshot after verifying the previously observed file identity.</summary>
    /// <param name="path">Target file in an existing directory.</param><param name="document">Captured snapshot.</param>
    /// <param name="expected">Observed identity for an existing file, or null for a new file.</param>
    /// <param name="cancellationToken">Cancellation checked before file commit, never after it.</param>
    /// <returns>Identity of the committed bytes.</returns>
    Task<SceneFileStamp> WriteAsync(string path, SceneDocument document, SceneFileStamp? expected, CancellationToken cancellationToken = default);
}
