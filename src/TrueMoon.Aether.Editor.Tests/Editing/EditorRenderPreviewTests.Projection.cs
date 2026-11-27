using System.Numerics;
using TrueMoon.Aether.Editor.Tests.Documents;
using TrueMoon.Aether.Editor.Tooling.Translation;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Preview;
using Xunit;

namespace TrueMoon.Aether.Editor.Tests.Editing;

public sealed partial class EditorRenderPreviewTests
{
    [Fact]
    public async Task CapturedPreviewPreservesAffineRowsAndOnlyReferencedGeometry()
    {
        var model = new MaterialModelFixture(DocumentData.Id(680));
        var mesh = model.UvMesh;
        var parent = new EntityId(DocumentData.Id(681));
        var child = new EntityId(DocumentData.Id(682));
        var cube = new EntityId(DocumentData.Id(683));
        var builtin = new EntityId(DocumentData.Id(684));
        var source = new EntityDefinitionId(DocumentData.Id(685));
        var document = new SceneDocument(new(DocumentData.Id(686)), 18,
        [
            new(builtin, source, new(addedComponents: [Transform(691), new(new(DocumentData.Id(692)), MeshRenderingComponents.MeshRendererId, 1)])),
            new(cube, source, new(addedComponents: [Transform(693, Number("z", 2)), Appearance(694)]), parent),
            new(child, source, new(addedComponents:
            [
                Transform(695, Number("yaw", .5), Number("x", 1)),
                new(new(DocumentData.Id(696)), MeshRenderingComponents.MeshRendererId, 1,
                [
                    KeyValuePair.Create("mesh", PropertyValue.From(mesh.AssetId.ToString("D"))),
                    KeyValuePair.Create("bindings", PropertyValue.From(mesh.BindingSetId.ToString("D"))),
                    KeyValuePair.Create("enabled", PropertyValue.From(false)),
                    KeyValuePair.Create("layers", PropertyValue.From(0x8000_0002L)),
                    KeyValuePair.Create("tintEnabled", PropertyValue.From(true)),
                    Number("tintR", .25), Number("tintG", .5), Number("tintB", .75)
                ])
            ]), parent),
            new(parent, source, new(addedComponents: [Transform(697, Number("sx", 2), Number("sz", 3), Number("x", 5))]))
        ], [new(source, EntityPatch.Empty)], [.. BuiltInComponents.Schemas, MeshRenderingComponents.MeshRendererSchema]);
        using var session = DocumentData.Factory().Create(document);
        var state = session.Read();
        var captured = new CapturedScenePreview(model.ProjectId, DocumentData.Id(699), state, model.Assets);
        var prepared = await captured.PrepareAsync(default);
        var payload = Assert.IsType<PreviewRenderPayload>(prepared.Render);
        Assert.Null(prepared.Primitive);
        Assert.Equal(document.Id, payload.SceneId);
        Assert.Equal(18, payload.Revision);
        Assert.Equal(new[] { child, cube, builtin }, payload.Rows.Select(row => row.Entity));
        var parentWorld = Matrix4x4.CreateScale(2, 1, 3) * Matrix4x4.CreateTranslation(5, 0, 0);
        var expected = Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromYawPitchRoll(.5f, 0, 0)) *
            Matrix4x4.CreateTranslation(1, 0, 0) * parentWorld;
        Assert.Equal(new PreviewRenderRow(child, expected, mesh.AssetId, mesh.BindingSetId, false, 0x8000_0002u, true, new(.25f, .5f, .75f)), payload.Rows[0]);
        Assert.NotEqual(0f, Vector3.Dot(new(expected.M11, expected.M12, expected.M13), new(expected.M31, expected.M32, expected.M33)));
        Assert.Equal(Matrix4x4.CreateTranslation(0, 0, 2) * parentWorld, payload.Rows[1].World);
        Assert.Equal(new Vector3(.125f, .25f, .75f), payload.Rows[1].Color);
        Assert.Equal(RenderContentProfile.CubeAssetId, payload.Rows[2].MeshId);
        Assert.Equal(RenderContentProfile.CubeBindingSetAssetId, payload.Rows[2].BindingId);
        Guid[] expectedAssets = [mesh.AssetId, mesh.BindingSetId, mesh.Slots[0].MaterialId, mesh.Slots[1].MaterialId, model.TextureId,
            RenderContentProfile.CubeAssetId, RenderContentProfile.CubeBindingSetAssetId, RenderContentProfile.WhiteMaterialAssetId];
        Assert.Equal(expectedAssets.Order(), payload.Content.AssetKeys.Select(key => key.AssetId).Order());
        Assert.DoesNotContain(payload.Content.AssetKeys, key => key.AssetId == model.PlainMesh.AssetId || key.AssetId == model.PlainMesh.MaterialId);

        var picks = ScenePreview.CaptureRenderPicks(payload);
        Assert.True(TranslationTarget.TryCapture(state, picks, cube, out var target));
        Assert.Equal(payload.Rows[1].World, target!.World);
        Assert.Equal(cube, Assert.Single(target.Ghosts).Entity);
        Assert.False(TranslationTarget.TryCapture(state, picks, builtin, out _));
    }

    [Fact]
    public async Task CapturedPreviewRejectsAnEntityWithBothGeometryRoles()
    {
        var id = new EntityId(DocumentData.Id(670));
        var source = new EntityDefinitionId(DocumentData.Id(671));
        using var session = DocumentData.Factory().Create(new(new(DocumentData.Id(672)), 1,
            [new(id, source, new(addedComponents: [Transform(673), Appearance(674), new(new(DocumentData.Id(675)), MeshRenderingComponents.MeshRendererId, 1)]))],
            [new(source, EntityPatch.Empty)], [.. BuiltInComponents.Schemas, MeshRenderingComponents.MeshRendererSchema]));
        var capture = new CapturedScenePreview(DocumentData.Id(676), DocumentData.Id(677), session.Read(), null);
        var error = await Assert.ThrowsAsync<SceneResolutionException>(() => capture.PrepareAsync(default));
        Assert.Equal(id, error.EntityId);
        Assert.Equal(SceneResolutionError.ProjectionFailed, error.Code);
        Assert.Contains("Appearance and MeshRenderer", error.InnerException?.Message);
    }

    private static ComponentData Transform(int id, params KeyValuePair<string, PropertyValue>[] values) =>
        new(new(DocumentData.Id(id)), BuiltInComponents.TransformId, 1, values);

    private static ComponentData Appearance(int id) => new(new(DocumentData.Id(id)), BuiltInComponents.AppearanceId, 1,
        [Number("r", .125), Number("g", .25), Number("b", .75)]);

    private static KeyValuePair<string, PropertyValue> Number(string key, double value) => KeyValuePair.Create(key, PropertyValue.From(value));
}
