using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Complete neutral authoring payload, not a primitive projection or runtime checkpoint.</summary>
public sealed class SceneWorldData
{
    /// <summary>The supported runtime load envelope version.</summary>
    public const int CurrentFormatVersion = 1;
    /// <summary>Captures immutable scene data. Unknown positive versions are rejected by loader/codec, without migration.</summary>
    /// <param name="scene">Complete immutable document.</param><param name="formatVersion">Positive envelope version.</param>
    public SceneWorldData(SceneDocument scene, int formatVersion = CurrentFormatVersion)
    { ArgumentNullException.ThrowIfNull(scene); if (formatVersion < 1) throw new ArgumentOutOfRangeException(nameof(formatVersion)); Scene = scene; FormatVersion = formatVersion; }
    /// <summary>Gets the immutable source document, including definitions/overrides and schemas.</summary>
    public SceneDocument Scene { get; }
    /// <summary>Gets the exact envelope version.</summary>
    public int FormatVersion { get; }
}

/// <summary>Runtime-only creation arguments; cancellation is never serialized.</summary>
/// <param name="Data">Neutral payload.</param><param name="CancellationToken">Cooperative preparation cancellation.</param>
public sealed record SceneWorldLoadRequest(SceneWorldData Data, CancellationToken CancellationToken = default);
