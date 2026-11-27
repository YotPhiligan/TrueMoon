using System.Collections.Immutable;
using System.Numerics;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Ecs.Rendering;
using TrueMoon.Aether.Runtime.Preview;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Tooling.Preview;

namespace TrueMoon.Aether.Tooling.Documents;

internal sealed partial class SceneRenderAssets
{
    internal static async Task<PreparedScenePreview> PreparePreviewAsync(CapturedScenePreview source, CancellationToken token)
    {
        var scene = source.Scene;
        token.ThrowIfCancellationRequested();
        if (!HasMeshRenderers(scene)) return new(source, PrimitiveSceneAdapter.Project(scene, token));

        var path = PrimitiveSceneAdapter.ValidateProjection(scene, token);
        foreach (var schema in scene.ComponentSchemas)
            if (schema.TypeId == MeshRenderingComponents.MeshRendererId && !BuiltInComponents.HasExactSchema(schema, MeshRenderingComponents.MeshRendererSchema))
                throw new InvalidDataException($"Unsupported mesh renderer schema: {schema.TypeId.Value:D}, version {schema.Version}.");
        var assets = source.Assets ?? new SceneRenderAssets(source.ProjectId, null, null, null, null, null, null, null);
        if (assets.Collision is { } collision) throw new InvalidOperationException(collision.Message);
        var closure = new ExactClosure(assets, keepBase: false);
        var worlds = new Dictionary<EntityId, Matrix4x4>(scene.Entities.Count);
        var rows = ImmutableArray.CreateBuilder<PreviewRenderRow>(scene.Entities.Count);
        foreach (var id in scene.HierarchyOrder)
        {
            token.ThrowIfCancellationRequested();
            var entity = scene.Entities[id];
            ResolvedComponent? renderer;
            Matrix4x4 world;
            try
            {
                world = PrimitiveSceneAdapter.CalculateWorld(entity, worlds);
                worlds.Add(id, world);
                var appearance = PrimitiveSceneAdapter.Role(entity, BuiltInComponents.AppearanceId);
                renderer = PrimitiveSceneAdapter.Role(entity, MeshRenderingComponents.MeshRendererId);
                if (appearance != null && renderer != null)
                    throw new InvalidDataException("Appearance and MeshRenderer are ambiguous on one entity.");
                var primitive = PrimitiveSceneAdapter.ProjectPrimitive(world, appearance);
                if (appearance != null) rows.Add(new(id, world, default, default, false, 0, false, primitive.Color));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                throw new SceneResolutionException(SceneResolutionError.ProjectionFailed, $"{path}/entities/{id.Value:D}/preview",
                    "Edit Preview projection failed.", id, error);
            }
            if (renderer == null) continue;

            var (enabled, layers, tintEnabled, tint) = MeshRenderingComponents.PreviewValues(renderer);
            var references = await MaterializeRendererAsync(assets, closure, entity, renderer, requireAll: true, preview: true, token)
                .ConfigureAwait(false);
            if (references.Definition == null) closure.AddBuiltinCube();
            rows.Add(new(id, world, references.Mesh ?? RenderContentProfile.CubeAssetId, references.Binding, enabled, layers, tintEnabled, tint));
        }
        token.ThrowIfCancellationRequested();
        return new(source, new PreviewRenderPayload(scene.Id, scene.Revision, closure.Freeze()!, rows.ToImmutable()));
    }

    private static bool HasMeshRenderers(ResolvedSceneSnapshot scene)
    {
        foreach (var entity in scene.Entities.Values)
            foreach (var component in entity.Components.Values)
                if (component.TypeId == MeshRenderingComponents.MeshRendererId) return true;
        return false;
    }
}
