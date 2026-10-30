using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Hierarchy;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Editor.Tests.Hierarchy;

internal sealed class HierarchyFixture : IDisposable
{
    public DocumentUiFixture Owner { get; }
    public EditorWorkspace Workspace { get; } = new(EditorScene.Demo());
    public EditorPreview Preview { get; }
    public EditorTransformViewModel Transform { get; }
    public EditorHierarchyViewModel Model { get; }
    public List<ResolvedSceneSnapshot> Shown { get; } = [];
    public static string Parent => DocumentData.Parent.Value.ToString("D");
    public static string Child => DocumentData.Child.Value.ToString("D");
    public HierarchyFixture(SceneDocument? scene = null, ISceneDocumentStore? sceneStore = null)
    {
        Owner = new(sceneStore: sceneStore);
        if (sceneStore == null)
        {
            if (scene != null) Owner.Store.File = new(scene, new("hierarchy-file"));
            Owner.Run(DocumentCommand.Open, "room.aetherscene");
        }
        else DocumentUiFixture.Wait(Owner.Document.NewAsync(scene ?? DocumentData.Scene()));
        Preview = new((snapshot, _) => { Shown.Add(snapshot); return Task.FromResult(EditingFixture.Receipt(snapshot)); }, () => Task.CompletedTask);
        Transform = new(Owner.Commands, Preview);
        Workspace.SetDocument(Owner.Document.Read().Content);
        if (Workspace.EntityIds.Contains(Child)) Workspace.Select(Child); else Workspace.ClearSelection();
        Transform.Refresh(Workspace.SelectedId);
        Model = new(Workspace, Owner.Commands, Transform); Model.Refresh();
    }
    public void Synchronize()
    {
        Workspace.SetDocument(Owner.Document.Read().Content);
        Transform.Refresh(Workspace.SelectedId); Model.Refresh();
    }
    public void Dispose() { Model.Dispose(); Transform.Dispose(); Preview.Dispose(); Owner.Dispose(); }
}
