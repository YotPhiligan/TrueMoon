using TrueMoon.Aether.Editor.Documents;
using TrueMoon.Aether.Editor.Editing;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tests.DocumentsUi;
using TrueMoon.Aether.Editor.Tests.Editing;
using TrueMoon.Aether.Scenes;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Alloy.Runtime;
using TrueMoon.Argentis;

namespace TrueMoon.Aether.Editor.Tests.Inspector;

internal sealed class InspectorFixture : IDisposable
{
    private static readonly Lazy<SkiaFontCatalog> Fonts = new(() => SkiaFontCatalog.Load("InspectorTests",
        new Dictionary<string, string> { ["InspectorTests"] = Path.Combine(AppContext.BaseDirectory, "Assets/Fonts/NotoSans-Regular.ttf") }));
    public DocumentUiFixture Owner { get; }
    public EditorPreview Preview { get; }
    public EditorInspectorViewModel Model { get; }
    public List<ResolvedSceneSnapshot> Shown { get; } = [];
    public static string Child => DocumentData.Child.Value.ToString("D");
    public static string Parent => DocumentData.Parent.Value.ToString("D");

    public InspectorFixture(SceneDocument? scene = null)
    {
        Owner = new();
        if (scene != null) Owner.Store.File = new(scene, new("inspector-file"));
        Owner.Run(DocumentCommand.Open, "room.aetherscene");
        Preview = new((snapshot, _) => { lock (Shown) Shown.Add(snapshot); return Task.FromResult(EditingFixture.Receipt(snapshot)); }, () => Task.CompletedTask);
        Model = new(Owner.Commands, Preview); Model.Refresh(Child);
    }

    public InspectorProperty Property(ComponentId component, string key) => Model.Components.Single(item => item.Id == component).Properties.Single(item => item.Key == key);
    public void Refresh(string? selected = null) => Model.Refresh(selected ?? Child);
    public void Dispose() { Model.Dispose(); Preview.Dispose(); Owner.Dispose(); }

    // RichTextEditor needs the renderer's real shaping/layout service. This CPU surface opens no native window.
    public static UiSession Ui(Element root, TimeProvider? clock = null, int width = 1280, int height = 800)
    {
        var viewport = new UiViewport(width, height);
        var surface = new SkiaRasterRenderBackend(Fonts.Value).CreateSurface(new RasterUiTarget(), viewport);
        try { return new(root, surface, viewport, null, new UiDispatchOptions(), clock); }
        catch { surface.Dispose(); throw; }
    }

    public static SceneDocument WithIndependentEntity(bool empty)
    {
        var scene = DocumentData.Scene(); var id = new EntityId(DocumentData.Id(3)); var definition = new EntityDefinitionId(DocumentData.Id(33));
        var schema = new ComponentSchema(new(DocumentData.Id(21)), 1, [new("note", PropertyValueKind.String, PropertyValue.From("independent"))]);
        var occurrence = new ComponentData(new(DocumentData.Id(41)), schema.TypeId, 1);
        var source = new EntityDefinition(definition, empty ? EntityPatch.Empty : new(addedComponents: [occurrence]));
        return new(scene.Id, scene.Revision, scene.Entities.Append(new(id, definition)), scene.Definitions.Add(source), scene.Schemas.Add(schema));
    }
}
