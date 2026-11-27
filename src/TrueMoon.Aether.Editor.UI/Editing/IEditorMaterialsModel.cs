using System.ComponentModel;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Project-scoped Unlit material drafts, choices and history actions for the inspector.</summary>
public interface IEditorMaterialsModel : INotifyPropertyChanged
{
    bool IsAvailable { get; }
    bool IsOpen { get; set; }
    bool HasDocument { get; }
    bool HasDraft { get; }
    bool IsReadOnly { get; }
    bool CanEditFields { get; }
    bool CanCreate { get; }
    bool CanReload { get; }
    bool CanSave { get; }
    bool CanCancel { get; }
    bool CanUndo { get; }
    bool CanRedo { get; }
    string StatusText { get; }
    string Name { get; set; }
    string Red { get; set; }
    string Green { get; set; }
    string Blue { get; set; }
    Guid? TextureId { get; set; }
    Guid? SelectedMaterialId { get; }
    IReadOnlyList<InspectorRenderAssetChoice> Items { get; }
    IReadOnlyList<InspectorRenderAssetChoice> Textures { get; }

    void Open(Guid id);
    void New();
    void Reload();
    void Save();
    void Cancel();
    void Undo();
    void Redo();
    void OpenSlot(InspectorRenderAssetSelection selection, Guid slotId);
    void CopyForSlot(InspectorRenderAssetSelection selection, Guid slotId);
    bool CanCopySlot(InspectorRenderAssetSelection selection, Guid slotId);
}
