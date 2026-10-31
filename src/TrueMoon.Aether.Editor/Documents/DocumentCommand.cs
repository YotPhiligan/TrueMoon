namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Document actions shared by buttons, keyboard input and close coordination.</summary>
public enum DocumentCommand { None, New, Open, Save, SaveAs, Close, Exit, OpenProject, CloseProject }

/// <summary>Thread-safe command snapshot; UI models copy it on their owner thread.</summary>
/// <param name="Operation">Accepted action, or None.</param><param name="Busy">Whether work or a decision is pending.</param>
/// <param name="AwaitingDecision">Whether Save/Discard/Cancel is required.</param><param name="Exiting">Whether window close owns admission.</param>
/// <param name="Closed">Whether command admission is terminal.</param><param name="Document">Current project-owned document snapshot.</param>
/// <param name="ScenesDirectory">Canonical project scene root, when available.</param><param name="Error">Last observed operation failure.</param>
/// <param name="RuntimeIdle">Whether document transitions can start without conflicting runtime work.</param>
public sealed record EditorDocumentCommandState(DocumentCommand Operation, bool Busy, bool AwaitingDecision,
    bool Exiting, bool Closed, SceneDocumentState? Document, string? ScenesDirectory, Exception? Error, bool RuntimeIdle)
{
    /// <summary>Borrowed active project identity for coherent presentation.</summary>
    public Projects.ProjectSession? Project { get; init; }
    /// <summary>Whether its runtime build context has been bound successfully.</summary>
    public bool ProjectReady { get; init; } = true;
    /// <summary>Whether this composition supplies project/runtime rebinding.</summary>
    public bool ProjectSwitchEnabled { get; init; }
    /// <summary>Scene game command sharing document admission, or Idle.</summary>
    public TrueMoon.Aether.Editing.GameEditorOperation RuntimeOperation { get; init; }
}
