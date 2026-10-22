namespace TrueMoon.Aether.Scenes;

/// <summary>Scene aggregate and compatibility projection failure categories.</summary>
public enum SceneResolutionError
{
    /// <summary>Two scene instances have the same identity.</summary>
    DuplicateEntity,
    /// <summary>A scene parent is absent from the same document.</summary>
    MissingParent,
    /// <summary>Scene parent links form a cycle.</summary>
    ParentCycle,
    /// <summary>The definition/schema catalog has conflicting identities.</summary>
    InvalidCatalog,
    /// <summary>An instance could not be resolved against the frozen catalog.</summary>
    InvalidEntity,
    /// <summary>A compatibility projector failed or returned an invalid value.</summary>
    ProjectionFailed,
    /// <summary>The existing SceneSnapshot entity-count bound would be exceeded.</summary>
    LegacyEntityLimitExceeded
}

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
