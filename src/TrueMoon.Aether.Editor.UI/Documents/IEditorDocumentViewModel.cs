namespace TrueMoon.Aether.Editor.UI.Documents;

public interface IEditorDocumentViewModel : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Gets/sets the input used by Open, SaveAs and first Save. Completion never replaces this draft.</summary>
    string PathDraft { get; set; }

    /// <summary>Retained project manifest draft, separate from scene and first-save paths.</summary>
    string ProjectPathDraft { get; set; }

    /// <summary>Path edited by the shared retained prompt.</summary>
    string RequestedPath { get; set; }

    /// <summary>Accessible path input label.</summary>
    string PathInputLabel { get; }

    /// <summary>Prompt hint without changing the canonical scene-root contract.</summary>
    string PathHint { get; }

    /// <summary>Whether this host supplies dynamic project/runtime rebinding.</summary>
    bool ProjectSwitchEnabled { get; }

    /// <summary>Current portable project name for the window caption.</summary>
    string ProjectName { get; }

    /// <summary>Open/switch project admission.</summary>
    bool CanOpenProject { get; }

    /// <summary>Close/retry project cleanup admission.</summary>
    bool CanCloseProject { get; }

    /// <summary>Whether this host supports choosing a published artifact for the open scene.</summary>
    bool SupportsPublishedBuilds => false;

    /// <summary>Whether choosing a published artifact for the current context is available.</summary>
    bool CanOpenBuild => false;

    /// <summary>Gets/sets a separate untitled-save path while deciding a transition.</summary>
    string SavePathDraft { get; set; }

    /// <summary>Gets file identity, dirty marker and current activity.</summary>
    string Title { get; }

    /// <summary>Gets the canonical scene root used to interpret relative input.</summary>
    string ScenesRoot { get; }

    /// <summary>Gets an actionable error without dropping path drafts or document content.</summary>
    string ErrorText { get; }

    /// <summary>Action awaiting an explicit Open/Save path confirmation; no IO has started.</summary>
    DocumentCommand? PathRequest { get; }

    /// <summary>Whether the retained path editor is requested.</summary>
    bool IsPathPromptOpen { get; }

    /// <summary>Whether contextual document tools need to be shown above the scene.</summary>
    bool IsToolsVisible { get; }

    /// <summary>Heading of the requested path operation.</summary>
    string PathPromptText { get; }

    /// <summary>Fresh admission for the requested path action.</summary>
    bool CanConfirmPath { get; }

    /// <summary>Whether the tools can be dismissed without answering an unsaved-changes decision.</summary>
    bool CanDismissTools { get; }

    /// <summary>Gets the explanation of the pending transition.</summary>
    string DecisionText { get; }

    /// <summary>Gets whether an explicit Save/Discard/Cancel decision is pending.</summary>
    bool AwaitingDecision { get; }

    /// <summary>Gets whether the pending decision needs a first-save path.</summary>
    bool NeedsSavePath { get; }

    /// <summary>Gets whether the path draft can be edited.</summary>
    bool CanEditPath { get; }

    /// <summary>Gets New availability.</summary>
    bool CanNew { get; }

    /// <summary>Gets Open availability.</summary>
    bool CanOpen { get; }

    /// <summary>Gets Save availability.</summary>
    bool CanSave { get; }

    /// <summary>Gets SaveAs availability.</summary>
    bool CanSaveAs { get; }

    /// <summary>Gets Close availability.</summary>
    bool CanClose { get; }

    /// <summary>Gets whether cancellable work is pending.</summary>
    bool CanCancel { get; }

    /// <summary>Whether a document operation or close decision is pending.</summary>
    bool IsBusy { get; }

    /// <summary>Current document action, if any.</summary>
    DocumentCommand? Operation { get; }

    /// <summary>Reads fresh admission state before a game action, including work accepted since the last UI refresh.</summary>
    bool BlocksRuntime { get; }

    /// <summary>Formats the native/header caption from the same copied project/document state.</summary>
    /// <param name="baseTitle">Host-selected base caption.</param><returns>Caption including project, scene, dirty and activity markers.</returns>
    string GetWindowCaption(string baseTitle);

    /// <summary>Formats the shared accessible caption and full document/root path.</summary>
    /// <param name="baseTitle">Host-selected base caption.</param><returns>Caption followed by its path description.</returns>
    string GetWindowCaptionDescription(string baseTitle);

    /// <summary>Copies a thread-safe coordinator snapshot and notifies bindings on the UI owner thread.</summary>
    void Refresh();

    /// <summary>Requests a UI action, collecting paths before Open, SaveAs or a first Save.</summary>
    /// <param name="command">New/Open/Save/SaveAs/Close action.</param>
    void Request(DocumentCommand command);

    /// <summary>Starts the requested operation using the retained path draft and fresh admission.</summary>
    void ConfirmPath();

    /// <summary>Dismisses path tools or a displayed error without changing the document or its history.</summary>
    void CancelPath();

    /// <summary>Invokes the shared action using the current path draft.</summary>
    /// <param name="command">Document action.</param>
    void Execute(DocumentCommand command);

    /// <summary>Cancels pending file work or a transition decision.</summary>
    void Cancel();
    void Choose(EditorDocumentCloseChoice choice);
}
