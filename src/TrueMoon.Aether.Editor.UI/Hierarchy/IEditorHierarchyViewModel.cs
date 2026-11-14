namespace TrueMoon.Aether.Editor.UI.Hierarchy;

public interface IEditorHierarchyViewModel : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Whether a new object can be created under the current admission/capacity.</summary>
    bool CanCreate { get; }

    /// <summary>Whether the selected branch may be deleted, duplicated or used as a creation parent.</summary>
    bool CanEditSelected { get; }

    /// <summary>Name/ID search text. Searching does not filter or rebuild the hierarchy.</summary>
    string SearchText { get; set; }

    /// <summary>Number of current case-insensitive name/ID matches.</summary>
    int MatchCount { get; }

    /// <summary>Whether next-match navigation is available.</summary>
    bool CanFindNext { get; }

    /// <summary>Gets immutable hierarchy values, independent of UI controls.</summary>
    IReadOnlyList<HierarchyItem> Items { get; }

    /// <summary>Gets scene caption.</summary>
    string Caption { get; }

    /// <summary>Gets shared selected identity.</summary>
    string? SelectedId { get; }

    /// <summary>Changes for every committed document observation, fencing gestures from an older snapshot.</summary>
    long SnapshotVersion { get; }

    /// <summary>Gets whether structural editing is currently admitted.</summary>
    bool CanMove { get; }

    /// <summary>Gets whether the selected child can be moved to the scene root.</summary>
    bool CanMoveToRoot { get; }

    /// <summary>Gets movement/admission guidance.</summary>
    string Hint { get; }

    /// <summary>Gets the latest rejected mutation diagnostic.</summary>
    string ErrorText { get; }

    /// <summary>Gets whether a complete selectable mutation diagnostic is present.</summary>
    bool HasError { get; }

    /// <summary>Whether the selected occurrence admits an inline name draft.</summary>
    bool CanRename { get; }

    /// <summary>Whether an uncommitted name draft is present.</summary>
    bool IsRenaming { get; }

    /// <summary>The stable identity being renamed, independent of its display name.</summary>
    string? RenameId { get; }

    /// <summary>Validator explanation for the retained name draft; command failures remain general diagnostics.</summary>
    string RenameError { get; }

    /// <summary>Whether the name validator rejected the current draft.</summary>
    bool HasRenameError { get; }

    /// <summary>Editable name; an empty value restores the generated display label.</summary>
    string RenameText { get; set; }

    /// <summary>Selects/reveals the next matching occurrence, cycling in scene hierarchy order.</summary>
    void FindNext();

    /// <summary>Creates a root or selected child and selects it for inspection. One action is one transaction.</summary>
    /// <param name="kind">Transform-only or cube preset.</param><param name="asChild">Use the selected occurrence as parent.</param><returns>Whether committed.</returns>
    bool CreateEntity(EditorEntityKind kind, bool asChild = false);

    /// <summary>Removes the selected occurrence and all descendants, retaining shared catalogs.</summary>
    /// <returns>Whether committed.</returns>
    bool DeleteSelected();

    /// <summary>Copies the selected subtree with fresh entity IDs, selects its root and preserves source data.</summary>
    /// <returns>Whether committed.</returns>
    bool DuplicateSelected();

    /// <summary>Observes the current committed document/admission on its UI owner thread.</summary>
    void Refresh();

    /// <summary>Selects an existing occurrence and updates the borrowed inspector.</summary>
    /// <param name="id">Existing key, or null to clear inspection.</param>
    void Select(string? id);

    /// <summary>Requests an atomic scene parent change, preserving local component values.</summary>
    /// <param name="id">Source occurrence.</param><param name="parentId">New parent, or null for root.</param>
    /// <returns>True only after successful shared-history commit.</returns>
    bool Move(string id, string? parentId);

    /// <summary>Moves the selected child to the scene root through the same guarded operation.</summary>
    void MoveToRoot();

    /// <summary>Starts a draft fenced to the currently admitted document state.</summary>
    bool BeginRename();

    /// <summary>Commits a valid draft as one shared history entry, preserving the entity identity.</summary>
    bool CommitRename();

    /// <summary>Discards an uncommitted name without changing the document or history.</summary>
    void CancelRename();
    bool Place(string id, string? parentId, string? anchorId, EditorEntityPlacement placement);
}
