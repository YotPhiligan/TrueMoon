namespace TrueMoon.Aether.Editor.Documents;

/// <summary>Document actions shared by buttons, keyboard input and close coordination.</summary>
public enum DocumentCommand { None, New, Open, Save, SaveAs, Close, Exit }

/// <summary>Thread-safe command snapshot; UI models copy it on their owner thread.</summary>
/// <param name="Operation">Accepted action, or None.</param><param name="Busy">Whether work or a decision is pending.</param>
/// <param name="AwaitingDecision">Whether Save/Discard/Cancel is required.</param><param name="Exiting">Whether window close owns admission.</param>
/// <param name="Closed">Whether command admission is terminal.</param><param name="Document">Current project-owned document snapshot.</param>
/// <param name="ScenesDirectory">Canonical project scene root, when available.</param><param name="Error">Last observed operation failure.</param>
/// <param name="RuntimeIdle">Whether document transitions can start without conflicting runtime work.</param>
public sealed record EditorDocumentCommandState(DocumentCommand Operation, bool Busy, bool AwaitingDecision,
    bool Exiting, bool Closed, SceneDocumentState? Document, string? ScenesDirectory, Exception? Error, bool RuntimeIdle);
