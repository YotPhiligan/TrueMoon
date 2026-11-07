using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Stable scene-load failure categories; identity and inner cause are preserved separately.</summary>
public enum SceneWorldLoadError
{
    /// <summary>Unsupported runtime load envelope version.</summary>
    UnsupportedFormat,
    /// <summary>Invalid authoring graph or catalog.</summary>
    InvalidScene,
    /// <summary>Visible type has no runtime binding.</summary>
    UnknownComponent,
    /// <summary>Type is known, but its exact version is not registered.</summary>
    UnsupportedSchemaVersion,
    /// <summary>Schema changed without changing its stable version.</summary>
    SchemaMismatch,
    /// <summary>Multiplicity is unsupported by singleton storage.</summary>
    UnsupportedMultiplicity,
    /// <summary>Invalid, dangling or unsupported reference.</summary>
    InvalidReference,
    /// <summary>Invalid value, kind, range or numeric conversion.</summary>
    InvalidValue,
    /// <summary>Configured admission limit exceeded.</summary>
    LimitExceeded,
    /// <summary>Factory did not return a fresh empty healthy unowned World.</summary>
    InvalidWorldFactory,
    /// <summary>User importer/provider failed.</summary>
    ImportFailed,
    /// <summary>Cleanup failed; CleanupOwner retains unreleased resources.</summary>
    CleanupFailed
}

/// <summary>Load error with stable location and optional retained cleanup ownership.</summary>
public sealed class SceneWorldLoadException : Exception
{
    internal SceneWorldLoadException(SceneWorldLoadError error, string message, SceneId? scene = null,
        EntityId? entity = null, ComponentId? component = null, string? property = null, Exception? inner = null, SceneWorld? cleanup = null)
        : base(message, inner) { Error = error; SceneId = scene; EntityId = entity; ComponentId = component; PropertyId = property; CleanupOwner = cleanup; }
    /// <summary>Gets the stable error code.</summary>
    public SceneWorldLoadError Error { get; }
    /// <summary>Gets scene identity when available.</summary>
    public SceneId? SceneId { get; }
    /// <summary>Gets entity identity when available.</summary>
    public EntityId? EntityId { get; }
    /// <summary>Gets occurrence identity when available.</summary>
    public ComponentId? ComponentId { get; }
    /// <summary>Gets the ordinal property key when available.</summary>
    public string? PropertyId { get; }
    /// <summary>Gets a failed load owner to Dispose again after cleanup failure.</summary>
    public SceneWorld? CleanupOwner { get; }
}
