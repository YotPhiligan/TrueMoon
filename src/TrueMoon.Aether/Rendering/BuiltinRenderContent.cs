using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Pure CPU construction of the versioned cube, white material and exact binding-set defaults.</summary>
public static class BuiltinRenderContent
{
    /// <summary>Creates a frozen three-asset manifest, with no registration, GPU activation or borrowed ownership.</summary>
    /// <param name="projectId">Project scope.</param><param name="buildId">Pinned build digest.</param><param name="generation">Catalog generation.</param><returns>Detached CPU defaults.</returns>
    public static RenderContentManifest Create(Guid projectId, RenderContentHash buildId, Guid generation)
    {
        var mesh = new MeshDefinition(
            [new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f),
             new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f)],
            [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3,
             1, 2, 6, 1, 6, 5, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2],
            [new(RenderContentProfile.DefaultSlotId, 0, 36)], new(new Vector3(-.5f), new Vector3(.5f)));
        var material = new OpaqueUnlitMaterialDefinition(Vector3.One);
        var meshKey = mesh.CreateKey(projectId, RenderContentProfile.CubeAssetId);
        var materialKey = material.CreateKey(projectId, RenderContentProfile.WhiteMaterialAssetId);
        var bindings = new MaterialBindingSetDefinition(meshKey, [new(RenderContentProfile.DefaultSlotId, materialKey)]);
        return new(projectId, buildId, generation,
            [new(meshKey, mesh), new(materialKey, material), new(bindings.CreateKey(projectId, RenderContentProfile.CubeBindingSetAssetId), bindings)]);
    }
}
