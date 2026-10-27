using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Explicit response to unsaved content.</summary>
public enum DocumentCloseChoice
{
    /// <summary>Preserve an unsaved document and reject the transition.</summary>
    Cancel,
    /// <summary>Persist current content before the transition.</summary>
    Save,
    /// <summary>Authorize discarding unsaved memory without changing its saved file.</summary>
    Discard
}

/// <summary>Current document IO/lifecycle operation.</summary>
public enum DocumentOperation
{
    /// <summary>No pending IO.</summary>
    None,
    /// <summary>Preparing a new/loaded candidate.</summary>
    Loading,
    /// <summary>Writing one captured snapshot; edits remain permitted.</summary>
    Saving,
    /// <summary>Fenced close and actual drain.</summary>
    Closing
}

/// <summary>Coherent observable state; the content is the original engine authoring state, not another mutable scene.</summary>
/// <param name="Content">Current committed authoring data, or null.</param><param name="Path">Selected absolute path, or null.</param>
/// <param name="Stamp">Last observed/saved file bytes.</param><param name="SavedContentRevision">Session-local saved checkpoint.</param>
/// <param name="Operation">Pending operation.</param><param name="IsDirty">Current content differs from the saved checkpoint, or has never been saved.</param>
/// <param name="Error">Last operation error.</param><param name="IsClosed">Terminal owner disposal closed admission.</param>
public sealed record SceneDocumentState(SceneAuthoringState? Content, string? Path, SceneFileStamp? Stamp,
    long? SavedContentRevision, DocumentOperation Operation, bool IsDirty, Exception? Error, bool IsClosed);

/// <summary>Receipt identifying the exact snapshot persisted by a successful save.</summary>
/// <param name="Path">Committed file path.</param><param name="Revision">Captured document revision.</param>
/// <param name="ContentRevision">Captured authoring checkpoint.</param><param name="Stamp">Committed file bytes.</param>
public sealed record DocumentSaveResult(string Path, long Revision, long ContentRevision, SceneFileStamp Stamp);
