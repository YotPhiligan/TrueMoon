using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Preview;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;

namespace TrueMoon.Aether.Editor.Tests.Editing;

internal sealed class EditingFixture : IDisposable
{
    public DocumentUiFixture Owner { get; } = new();
    public List<ResolvedSceneSnapshot> Shown { get; } = [];
    public EditorPreview Preview { get; }
    public EditorTransformViewModel Model { get; }
    public EditingFixture()
    {
        Owner.Run(DocumentCommand.Open, "room.aetherscene");
        Preview = new((scene, _) => { lock (Shown) Shown.Add(scene); return Task.FromResult(Receipt(scene)); }, () => Task.CompletedTask);
        Model = new(Owner.Commands, Preview); Model.Refresh(DocumentData.Child.Value.ToString());
    }
    public TransformField Field(string key) => Model.Fields.Single(f => f.Key == key);
    private static readonly SessionIdentity Runtime = new(DocumentData.Id(500), 1, new('0', 64), new('1', 64), new('2', 64));
    public static ScenePreviewDocument Receipt(ResolvedSceneSnapshot scene) => new(scene.Id, scene.Revision, Runtime);
    public void Dispose() { Model.Dispose(); Preview.Dispose(); Owner.Dispose(); }
}
