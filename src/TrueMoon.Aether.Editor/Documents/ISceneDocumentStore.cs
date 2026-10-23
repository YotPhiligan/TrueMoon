using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Content identity of the exact observed UTF8 file bytes.</summary>
/// <param name="Sha256">SHA256 digest, independent of timestamps.</param>
public sealed record SceneFileStamp(string Sha256);

/// <summary>A completely decoded file and the identity used to protect its next save.</summary>
/// <param name="Document">Validated authoring data.</param><param name="Stamp">Exact observed bytes.</param>
public sealed record SceneFile(SceneDocument Document, SceneFileStamp Stamp);

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
