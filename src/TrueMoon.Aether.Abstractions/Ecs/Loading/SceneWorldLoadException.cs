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
