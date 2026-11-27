using System.ComponentModel;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Detached metadata for an independently authored project texture.</summary>
public sealed record InspectorTexture(Guid Id, string Name, string SourcePath, int Width, int Height, string Revision);

/// <summary>Project texture catalog, independent of scene selection and history.</summary>
public interface IEditorTexturesModel : INotifyPropertyChanged
{
    bool IsAvailable { get; }
    bool IsOpen { get; set; }
    bool HasEditor { get; }
    bool HasDraft { get; }
    bool CanSelect { get; }
    bool CanEditName { get; }
    bool CanEditSource { get; }
    bool CanCreate { get; }
    bool CanImport { get; }
    bool CanRename { get; }
    bool CanReplace { get; }
    bool CanReload { get; }
    bool CanCancel { get; }
    string StatusText { get; }
    string Name { get; set; }
    string SourcePath { get; set; }
    InspectorTexture? Selected { get; }
    IReadOnlyList<InspectorTexture> Items { get; }

    void Select(Guid id);
    void New();
    void Import();
    void Rename();
    void ReplaceSource();
    void Reload();
    void Cancel();
}
