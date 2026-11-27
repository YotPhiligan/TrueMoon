using System.Collections.Immutable;
using System.ComponentModel;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Editor.UI.Editing;

/// <summary>Committed mesh metadata displayed by the project inspector.</summary>
public sealed record InspectorMesh(Guid Id, string Name, string SourcePath, string SourceLocator, RenderBounds Bounds,
    bool HasUv0, ImmutableArray<InspectorMeshSlot> Slots);

/// <summary>A stable slot in geometry order.</summary>
public sealed record InspectorMeshSlot(Guid Id, string Name);

/// <summary>A registered model or a geometry choice in the current source receipt.</summary>
public sealed record InspectorMeshChoice(Guid Id, string Label);

/// <summary>Project mesh authoring independently of the selected scene entity.</summary>
public interface IEditorMeshesModel : INotifyPropertyChanged
{
    bool IsAvailable { get; }
    bool IsOpen { get; set; }
    bool HasEditor { get; }
    bool IsGeometryAvailable { get; }
    bool HasDraft { get; }
    bool CanSelect { get; }
    bool CanEditName { get; }
    bool CanEditGeometry { get; }
    bool CanCreateDraft { get; }
    bool CanCreate { get; }
    bool CanRename { get; }
    bool CanReplace { get; }
    bool CanReload { get; }
    bool CanCancel { get; }
    string StatusText { get; }
    string Name { get; set; }
    InspectorMesh? Selected { get; }
    Guid? SelectedSource { get; }
    Guid? SelectedGeometry { get; }
    IReadOnlyList<InspectorMesh> Items { get; }
    IReadOnlyList<InspectorMeshChoice> Sources { get; }
    IReadOnlyList<InspectorMeshChoice> Geometries { get; }
    void Select(Guid id);
    void New();
    void SelectSource(InspectorMeshChoice source);
    void SelectGeometry(InspectorMeshChoice geometry);
    void Create();
    void Rename();
    void ReplaceGeometry();
    void Reload();
    void Cancel();
}
