using System.Numerics;
using System.Security.Cryptography;
using Assimp;
using Assimp.Unmanaged;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

internal static unsafe class NativeProjection
{
    private static readonly Guid SecondCandidateSlotId = new(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    internal static ModelImportCandidate Import(CapturedModelSource source, CapturedIo io, GltfIdentityTags tags, GltfBaseColorTexture? texture)
    {
        var api = AssimpLibrary.Instance;
        var pointer = api.ImportFile(source.RootName, PostProcessSteps.ValidateDataStructure, io.Pointer, IntPtr.Zero);
        if (pointer == IntPtr.Zero) throw new InvalidDataException("Assimp rejected captured input.");
        try
        {
            var scene = (AiScene*)pointer;
            Require(scene->NumMeshes is > 0 and <= 64 && scene->NumMaterials is > 0 and <= 128 && scene->NumAnimations == 0 &&
                scene->NumTextures == 0 && scene->NumLights == 0 && scene->NumCameras == 0, "Static native scene counts.");
            var materials = new OpaqueUnlitMaterialDefinition[scene->NumMaterials];
            Require(scene->NumMaterials<=tags.Materials.Count+1 && scene->NumMeshes<=tags.Meshes.Count,"Native/source table provenance mismatch.");
            var materialLocators=new string?[materials.Length]; var materialSources=new int[materials.Length]; var seenMaterials=new HashSet<string>(); var defaults=0;
            for (var i = 0; i < materials.Length; i++)
            {
                var native = (AiMaterial*)((nint*)scene->Materials)[i]; Require(native != null && native->NumProperties <= 256, "Native material quota.");
                var linear = api.GetMaterialColor(ref *native, "$clr.base", TextureType.None, 0);
                Require(linear.W == 1, "Native opaque material alpha.");
                materials[i] = new(RenderColor.Encode(new Vector3(linear.X, linear.Y, linear.Z)));
                var name=api.GetMaterialString(ref *native,"?mat.name",TextureType.None,0);
                if(tags.Materials.TryGetValue(name,out var info))
                {
                    Require(seenMaterials.Add(name),"Duplicate native material transport tag."); materialLocators[i]=info.Locator; materialSources[i]=info.SourceMaterial;
                }
                else
                {
                    Require(++defaults==1 && linear==Vector4.One,"Unexpected native default material.");
                    materialLocators[i]="builtin/material/default"; materialSources[i]=-1;
                }
            }
            Require(defaults==1,"Missing native default material.");
            ModelImportTexture? importedTexture = null;
            if (texture != null)
            {
                var materialIndex = Array.IndexOf(materialSources, texture.MaterialIndex);
                Require(materialIndex >= 0, "The textured material is missing from the native scene.");
                var locator = "texture/" + Convert.ToHexStringLower(SHA256.HashData(ModelSourcePath.Utf8.GetBytes(texture.Name)));
                importedTexture = new(locator, materialIndex, TrueMoon.Aether.Importing.BaseColorPng.Decode(source.GetFile(texture.Name).Bytes.Span));
            }
            var meshes = new ModelImportMesh[scene->NumMeshes]; long outputBytes = 0;
            var meshInfos=new GltfIdentityTags.MeshInfo[meshes.Length]; var seenMeshes=new HashSet<string>();
            for (var i = 0; i < meshes.Length; i++)
            {
                var mesh = (AiMesh*)((nint*)scene->Meshes)[i];
                Require(mesh != null && mesh->NumBones == 0 && mesh->NumAnimMeshes == 0 && mesh->NumVertices is > 0 and <= 65536 &&
                    mesh->NumFaces is > 0 and <= 65536 && mesh->Vertices != 0 && mesh->Faces != 0, "Native mesh counts.");
                var textured = importedTexture != null && mesh->MaterialIndex == importedTexture.MaterialIndex;
                outputBytes = checked(outputBytes + mesh->NumVertices * (textured ? 20L : 12L) + mesh->NumFaces * 12L + 128);
                Require(outputBytes <= ModelImportProtocol.MaximumOutputBytes, "CPU mesh aggregate quota before copies.");
                var positions = new ReadOnlySpan<Vector3>((void*)mesh->Vertices, checked((int)mesh->NumVertices)).ToArray();
                var minimum = new Vector3(float.PositiveInfinity); var maximum = new Vector3(float.NegativeInfinity);
                foreach (var vertex in positions) { minimum = Vector3.Min(minimum, vertex); maximum = Vector3.Max(maximum, vertex); }
                var indices = new uint[checked(mesh->NumFaces * 3)];
                for (var f = 0; f < mesh->NumFaces; f++)
                {
                    var face = ((AiFace*)mesh->Faces)[f]; Require(face.NumIndices == 3 && face.Indices != 0, "Native triangle list.");
                    for (var k = 0; k < 3; k++) indices[f * 3 + k] = ((uint*)face.Indices)[k];
                }
                MeshSlot[] slots = [new(RenderContentProfile.DefaultSlotId, 0, (uint)indices.Length)];
                var bounds = new RenderBounds(minimum, maximum);
                var definition = textured
                    ? new MeshDefinition(positions, ReadUv0(mesh), indices, slots, bounds)
                    : new MeshDefinition(positions, indices, slots, bounds);
                meshes[i] = new(definition, checked((int)mesh->MaterialIndex));
                var name=mesh->Name.GetString(); Require(tags.Meshes.TryGetValue(name,out var info) && seenMeshes.Add(name),"Unrecognized/duplicate native mesh transport tag.");
                Require(mesh->MaterialIndex<materialSources.Length && materialSources[mesh->MaterialIndex]==info!.Material,"Native material provenance mismatch."); meshInfos[i]=info!;
            }
            var projected = new List<ModelImportMesh>(meshes.Length);
            var meshLocators = new List<string?>(meshes.Length);
            var indicesByPrimitive = new int[meshes.Length];
            var grouped = Enumerable.Range(0, meshes.Length).ToLookup(index => meshInfos[index].SourceMesh);
            for (var i = 0; i < meshes.Length; i++)
            {
                var info = meshInfos[i];
                if (!tags.SlottedMeshes.TryGetValue(info.SourceMesh, out var locator))
                {
                    indicesByPrimitive[i] = projected.Count;
                    projected.Add(meshes[i]);
                    meshLocators.Add(info.Locator);
                    continue;
                }
                var parts = grouped[info.SourceMesh].OrderBy(index => meshInfos[index].InstanceSuffix, StringComparer.Ordinal).ToArray();
                Require(parts.Length == 2, "Missing combined native primitive.");
                if (i != parts.Min()) continue;
                foreach (var part in parts) indicesByPrimitive[part] = projected.Count;
                projected.Add(CombineSlots(meshes[parts[0]], meshes[parts[1]],
                    meshInfos[parts[0]].InstanceSuffix![1..], meshInfos[parts[1]].InstanceSuffix![1..]));
                meshLocators.Add(locator);
            }
            var instances = new List<ModelImportInstance>(); var nodes = 0;
            var instanceLocators=new List<string?>();
            var visited = new HashSet<nint>();
            void Visit(AiNode* node, Matrix4x4 parent, int depth,string? parentTag=null)
            {
                Require(node != null && ++nodes <= 4096 && depth <= 64 && visited.Add((nint)node), "Native node/depth/cycle quota.");
                Require(node->NumMeshes <= 4096 - instances.Count && node->NumChildren <= 4096 - nodes &&
                    (node->NumMeshes == 0 || node->Meshes != 0), "Native instance/child quota.");
                outputBytes = checked(outputBytes + node->NumMeshes * 68L); Require(outputBytes <= ModelImportProtocol.MaximumOutputBytes, "CPU instance aggregate quota.");
                var world = Matrix4x4.Transpose(node->Transformation) * parent;
                var name=node->Name.GetString(); var tagged=tags.Nodes.TryGetValue(name,out var info);
                Require(tagged ? info!.ParentTag==parentTag : depth==0 && node->NumMeshes==0 && name=="ROOT","Native node provenance mismatch.");
                var combined = info?.SourceMesh is { } sourceMesh && tags.SlottedMeshes.ContainsKey(sourceMesh);
                if (combined) Require(node->NumMeshes == 2 && ((uint*)node->Meshes)[0] != ((uint*)node->Meshes)[1],
                    "A combined native node must contain both distinct primitives.");
                for (var i = 0; i < node->NumMeshes; i++)
                {
                    var meshIndex=checked((int)((uint*)node->Meshes)[i]); Require(tagged && meshIndex>=0 && meshIndex<meshInfos.Length,"Native instance provenance mismatch.");
                    var locator = tags.Instance(info!, meshInfos[meshIndex]);
                    if (combined && i != 0) continue;
                    instances.Add(new(indicesByPrimitive[meshIndex], world));
                    instanceLocators.Add(locator);
                }
                for (var i = 0; i < node->NumChildren; i++) Visit((AiNode*)((nint*)node->Children)[i], world, depth + 1,tagged ? name : parentTag);
            }
            Visit((AiNode*)scene->RootNode, Matrix4x4.Identity, 0);
            return new(source.SourceId, source.InputRevision, projected, materials, instances,
                new(meshLocators, materialLocators, instanceLocators), texture: importedTexture);
        }
        finally { api.ReleaseImport(pointer); }
    }

    private static Vector2[] ReadUv0(AiMesh* mesh)
    {
        Require(mesh->NumUVComponents[0] == 2 && mesh->TextureCoords[0] != 0, "Native textured mesh requires 2D UV0.");
        var native = new ReadOnlySpan<Vector3>((void*)mesh->TextureCoords[0], checked((int)mesh->NumVertices));
        var uv = new Vector2[native.Length];
        // The glTF importer flips V into Assimp's convention. Our texture rows use the glTF origin.
        for (var i = 0; i < uv.Length; i++) uv[i] = new(native[i].X, 1 - native[i].Y);
        return uv;
    }

    private static ModelImportMesh CombineSlots(ModelImportMesh first, ModelImportMesh second, string firstLocator, string secondLocator)
    {
        var a = first.Geometry;
        var b = second.Geometry;
        Require(a.Vertices.Length + b.Vertices.Length <= RenderContentProfile.MaximumVertices &&
            a.Indices.Length + b.Indices.Length <= RenderContentProfile.MaximumIndices, "Combined native mesh quota.");
        var offset = (uint)a.Vertices.Length;
        var split = (uint)a.Indices.Length;
        var bounds = new RenderBounds(Vector3.Min(a.Bounds.Minimum, b.Bounds.Minimum), Vector3.Max(a.Bounds.Maximum, b.Bounds.Maximum));
        Vector3[] vertices = [.. a.Vertices, .. b.Vertices];
        uint[] indices = [.. a.Indices, .. b.Indices.Select(index => index + offset)];
        MeshSlot[] slots = [new(RenderContentProfile.DefaultSlotId, 0, split), new(SecondCandidateSlotId, split, (uint)b.Indices.Length)];
        MeshDefinition geometry;
        if (a.Uv0.IsEmpty && b.Uv0.IsEmpty)
            geometry = new(vertices, indices, slots, bounds);
        else
        {
            var uv = new Vector2[vertices.Length];
            a.Uv0.AsSpan().CopyTo(uv);
            b.Uv0.AsSpan().CopyTo(uv.AsSpan(a.Vertices.Length));
            geometry = new(vertices, uv, indices, slots, bounds);
        }
        return new(geometry, first.MaterialIndex)
        {
            SlotMaterials = [new(firstLocator, first.MaterialIndex), new(secondLocator, second.MaterialIndex)]
        };
    }

    private static void Require(bool condition, string message) => ModelImportProtocol.Require(condition, message);
}
