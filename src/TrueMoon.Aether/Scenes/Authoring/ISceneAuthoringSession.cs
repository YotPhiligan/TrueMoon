namespace TrueMoon.Aether.Scenes.Authoring;

/// <summary>UI-independent, serialized document editing. The caller owns and disposes the factory product.
/// Dispose releases session references; previously returned immutable snapshots remain valid.</summary>
public interface ISceneAuthoringSession : IDisposable
{
    /// <summary>Reads one coherent immutable document/resolved snapshot and history availability.</summary>
    /// <returns>Current session state.</returns>
    SceneAuthoringState Read();
    /// <summary>Validates and commits all Transform replacements together.</summary>
    /// <param name="batch">Frozen request.</param>
    /// <param name="cancellationToken">Checked during preparation and immediately before commit; never after commit.</param>
    /// <returns>Terminal receipt. Exact duplicates return their original receipt even after later edits/cancellation.
    /// Changed payload with an existing ID is rejected. Cancelled/unexpected preparation failures retain no receipt.</returns>
    SceneAuthoringResult Apply(TransformEditBatch batch, CancellationToken cancellationToken = default);
    /// <summary>Restores the previous content with a new, monotonically increasing document revision.</summary>
    /// <param name="operation">New operation identity and expected current revision.</param>
    /// <param name="cancellationToken">Cancellation before commit.</param>
    /// <returns>Terminal receipt, or NoUndo when history is empty.</returns>
    SceneAuthoringResult Undo(AuthoringOperation operation, CancellationToken cancellationToken = default);
    /// <summary>Restores the next content with a new document revision. A fresh Apply clears Redo.</summary>
    /// <param name="operation">New operation identity and expected current revision.</param>
    /// <param name="cancellationToken">Cancellation before commit.</param>
    /// <returns>Terminal receipt, or NoRedo when the redo branch is empty.</returns>
    SceneAuthoringResult Redo(AuthoringOperation operation, CancellationToken cancellationToken = default);
}

/// <summary>Coherent immutable query result. Authoring revision differs from simulation tick.</summary>
/// <param name="Document">Current committed document including definitions, schemas and instance overrides.</param>
/// <param name="Resolved">Complete resolved representation of the same revision.</param>
/// <param name="ContentRevision">Identity of current content within this session; Undo/Redo restores this checkpoint.</param>
/// <param name="UndoCount">Available Undo transactions.</param>
/// <param name="RedoCount">Available Redo transactions.</param>
public sealed record SceneAuthoringState(SceneDocument Document, ResolvedSceneSnapshot Resolved,
    long ContentRevision, int UndoCount, int RedoCount);

/// <summary>Expected command outcomes. No failed result changes document content/history.</summary>
public enum SceneAuthoringError
{
    /// <summary>Committed successfully.</summary>
    None,
    /// <summary>The expected revision differs from the current document.</summary>
    RevisionConflict,
    /// <summary>The same ID was used for different intent, author or expected revision.</summary>
    OperationIdConflict,
    /// <summary>The receipt journal is full; open a new session rather than forgetting prior operations.</summary>
    OperationLimitReached,
    /// <summary>The configured batch bound was exceeded.</summary>
    BatchLimitExceeded,
    /// <summary>The same occurrence was addressed more than once.</summary>
    DuplicateTarget,
    /// <summary>The entity ID is not present.</summary>
    MissingEntity,
    /// <summary>The component ID is not visible on this instance.</summary>
    MissingComponent,
    /// <summary>The component is not a built-in v1 Transform.</summary>
    UnsupportedComponent,
    /// <summary>A value is nonfinite, unrepresentable as float or a scale is not positive.</summary>
    InvalidTransform,
    /// <summary>The candidate document failed shared scene/schema validation.</summary>
    InvalidScene,
    /// <summary>No previous transaction remains in bounded history.</summary>
    NoUndo,
    /// <summary>No transaction remains on the redo branch.</summary>
    NoRedo,
    /// <summary>No larger document revision can be represented.</summary>
    RevisionExhausted
}

/// <summary>Small immutable terminal receipt; it does not retain an old document or resolved scene.</summary>
/// <param name="Operation">Identity, expected revision and actor.</param>
/// <param name="Error">Outcome code.</param>
/// <param name="Revision">Document revision observed/committed by this operation.</param>
/// <param name="ContentRevision">Content checkpoint at completion.</param>
/// <param name="Path">Diagnostic target, or empty on success.</param>
/// <param name="Message">Diagnostic description, or empty on success.</param>
public sealed record SceneAuthoringResult(AuthoringOperation Operation, SceneAuthoringError Error,
    long Revision, long ContentRevision, string Path = "", string Message = "")
{
    /// <summary>Gets whether the operation committed.</summary>
    public bool Succeeded => Error == SceneAuthoringError.None;
}
