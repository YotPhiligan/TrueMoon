namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Explicit module admission/activation/cleanup outcomes. Plugin exceptions never escape as retained InnerExceptions.</summary>
public enum ModLoadError
{
    /// <summary>Operation completed.</summary>
    None,
    /// <summary>Malformed metadata, unsupported policy or unsafe path.</summary>
    InvalidManifest,
    /// <summary>Unknown manifest/API generation.</summary>
    UnsupportedApi,
    /// <summary>Artifact/catalog exceeds configured bounds.</summary>
    LimitExceeded,
    /// <summary>Missing or changed bytes.</summary>
    HashMismatch,
    /// <summary>Required module or managed reference is unavailable.</summary>
    MissingDependency,
    /// <summary>Module range, assembly identity or group membership conflicts.</summary>
    DependencyConflict,
    /// <summary>Host contract identity/hash differs or is not allowed.</summary>
    SharedContractMismatch,
    /// <summary>Invalid entry contract or factory result.</summary>
    InvalidEntry,
    /// <summary>Entry/service activation failed.</summary>
    ActivationFailed,
    /// <summary>Cancellation was observed before publication.</summary>
    Cancelled,
    /// <summary>Resources remain owned; explicitly retry cleanup.</summary>
    CleanupFailed,
    /// <summary>Host/group is stopping or disposed.</summary>
    AdmissionClosed,
    /// <summary>The module does not support the stable-component replacement contract.</summary>
    ReloadNotSupported,
    /// <summary>Neutral system capture/restore or state identity validation failed.</summary>
    StateRejected,
    /// <summary>The old generation could not resume; explicit retry is required.</summary>
    RollbackFailed,
    /// <summary>Neutral World snapshot is corrupt, unsupported or contains untracked data.</summary>
    InvalidSnapshot,
    /// <summary>Required binding/migrator or explicit data-loss admission is missing.</summary>
    MigrationNotSupported,
    /// <summary>Component conversion/import/export failed before commit.</summary>
    MigrationFailed,
    /// <summary>Destructive reset was not explicitly admitted.</summary>
    ResetNotAdmitted
}

/// <summary>Neutral bounded diagnostic; contains no module exception, Type, Assembly or delegate.</summary>
public sealed record ModDiagnostic(ModLoadError Error, string Message);
/// <summary>Published borrowed group on success, or a failed owner retained by ModHost when cleanup needs retry.</summary>
public sealed record ModLoadResult(ModDiagnostic Diagnostic, ModGroup? Group)
{
    /// <summary>Whether activation completed and the group is open.</summary>
    public bool Success => Diagnostic.Error == ModLoadError.None;
}

internal sealed class ModFailure(ModLoadError error, string message) : Exception(message)
{
    internal ModLoadError Error { get; } = error;
}
