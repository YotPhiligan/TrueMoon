using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Scenes.Authoring;

namespace TrueMoon.Aether.Editor.Hierarchy;

public sealed partial class EditorHierarchyViewModel
{
    private SceneAuthoringState? _renameState;
    private string? _renameId;
    private string _renameText = "", _renameOriginal = "";
    private bool _canRename;
    /// <summary>Whether the selected occurrence admits an inline name draft.</summary>
    public bool CanRename => _canRename;
    /// <summary>Whether an uncommitted name draft is present.</summary>
    public bool IsRenaming => _renameId != null;
    /// <summary>The stable identity being renamed, independent of its display name.</summary>
    public string? RenameId => _renameId;
    /// <summary>Editable name; an empty value restores the generated display label.</summary>
    public string RenameText
    {
        get => _renameText;
        set { VerifyOwner(); ArgumentNullException.ThrowIfNull(value); Set(ref _renameText, value, nameof(RenameText)); }
    }
    /// <summary>Starts a draft fenced to the currently admitted document state.</summary>
    public bool BeginRename()
    {
        VerifyOwner(); Refresh(); if (!CanRename) return false;
        _renameState = _commands.Read().Document!.Content;
        _renameId = _selected; _renameOriginal = _items.Single(item => item.Id == _selected).Name;
        RenameText = _renameOriginal;
        Notify(nameof(RenameId)); Notify(nameof(IsRenaming)); Refresh(); return true;
    }
    /// <summary>Commits a valid draft as one shared history entry, preserving the entity identity.</summary>
    public bool CommitRename()
    {
        VerifyOwner(); var state = _renameState; Refresh();
        if (!IsRenaming || !ReferenceEquals(state, _renameState)) return false;
        if (_renameText == _renameOriginal) { CancelRename(); return true; }
        try
        {
            var name = _renameText.Length == 0 ? null : _renameText;
            var result = _commands.EditHierarchy(state!.Document, new(new(Guid.NewGuid(), state.Document.Revision, "editor"),
                names: [new(new EntityId(Guid.Parse(_renameId!)), name)]));
            if (!result.Succeeded) { Set(ref _error, (result.Error + ": " + result.Message).ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
            ClearRename(); _workspace.SetDocument(_commands.Read().Document!.Content);
            _transform.Refresh(_workspace.SelectedId); Refresh(); return true;
        }
        catch (Exception error) { Set(ref _error, error.Message.ReplaceLineEndings("\n"), nameof(ErrorText)); return false; }
    }
    /// <summary>Discards an uncommitted name without changing the document or history.</summary>
    public void CancelRename() { VerifyOwner(); ClearRename(); Refresh(); }
    private void ClearRename()
    {
        if (!IsRenaming) return;
        _renameId = null; _renameState = null;
        Notify(nameof(RenameId)); Notify(nameof(IsRenaming));
    }
}
