namespace TrueMoon.Aether.Runtime.Scenes;
/// <summary>Scene failure with an ID-based path and the original entity/projector cause when present.</summary>
public sealed class SceneResolutionException : Exception
{
    internal SceneResolutionException(SceneResolutionError code, string path, string message, EntityId? entityId = null, Exception? innerException = null)
        : base($"{path}: {message}", innerException)
    { Code = code; Path = path; EntityId = entityId; }
    /// <summary>Gets the failure category.</summary>
    public SceneResolutionError Code { get; }
    /// <summary>Gets the scene/entity/parent or underlying authoring path.</summary>
    public string Path { get; }
    /// <summary>Gets the failing instance, or null for a catalog/scene-level failure.</summary>
    public EntityId? EntityId { get; }
}
