using System.Numerics;
using Assimp;
using Assimp.Unmanaged;
using TrueMoon.Aether.Abstractions.Rendering;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

internal static unsafe class NativeProjection
{
    internal static ModelImportCandidate Import(CapturedModelSource source, CapturedIo io, GltfIdentityTags tags)
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
            var meshes = new ModelImportMesh[scene->NumMeshes]; long outputBytes = 0;
            var meshInfos=new GltfIdentityTags.MeshInfo[meshes.Length]; var seenMeshes=new HashSet<string>();
            for (var i = 0; i < meshes.Length; i++)
            {
                var mesh = (AiMesh*)((nint*)scene->Meshes)[i];
                Require(mesh != null && mesh->NumBones == 0 && mesh->NumAnimMeshes == 0 && mesh->NumVertices is > 0 and <= 65536 &&
                    mesh->NumFaces is > 0 and <= 65536 && mesh->Vertices != 0 && mesh->Faces != 0, "Native mesh counts.");
                outputBytes = checked(outputBytes + mesh->NumVertices * 12L + mesh->NumFaces * 12L + 128);
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
                var definition = new MeshDefinition(positions, indices, new[] { new MeshSlot(RenderContentProfile.DefaultSlotId, 0, (uint)indices.Length) }, new(minimum, maximum));
                meshes[i] = new(definition, checked((int)mesh->MaterialIndex));
                var name=mesh->Name.GetString(); Require(tags.Meshes.TryGetValue(name,out var info) && seenMeshes.Add(name),"Unrecognized/duplicate native mesh transport tag.");
                Require(mesh->MaterialIndex<materialSources.Length && materialSources[mesh->MaterialIndex]==info!.Material,"Native material provenance mismatch."); meshInfos[i]=info!;
            }
            var instances = new List<ModelImportInstance>(); var nodes = 0;
            var instanceLocators=new List<string?>();
            var visited = new HashSet<nint>();
            void Visit(AiNode* node, Matrix4x4 parent, int depth,string? parentTag=null)
            {
                Require(node != null && ++nodes <= 4096 && depth <= 64 && visited.Add((nint)node), "Native node/depth/cycle quota.");
                Require(node->NumMeshes <= 4096 - instances.Count && node->NumChildren <= 4096 - nodes, "Native instance/child quota.");
                outputBytes = checked(outputBytes + node->NumMeshes * 68L); Require(outputBytes <= ModelImportProtocol.MaximumOutputBytes, "CPU instance aggregate quota.");
                var world = Matrix4x4.Transpose(node->Transformation) * parent;
                var name=node->Name.GetString(); var tagged=tags.Nodes.TryGetValue(name,out var info);
                Require(tagged ? info!.ParentTag==parentTag : depth==0 && node->NumMeshes==0 && name=="ROOT","Native node provenance mismatch.");
                for (var i = 0; i < node->NumMeshes; i++)
                {
                    var meshIndex=checked((int)((uint*)node->Meshes)[i]); Require(tagged && meshIndex>=0 && meshIndex<meshInfos.Length,"Native instance provenance mismatch.");
                    instances.Add(new(meshIndex,world)); instanceLocators.Add(tags.Instance(info!,meshInfos[meshIndex]));
                }
                for (var i = 0; i < node->NumChildren; i++) Visit((AiNode*)((nint*)node->Children)[i], world, depth + 1,tagged ? name : parentTag);
            }
            Visit((AiNode*)scene->RootNode, Matrix4x4.Identity, 0);
            return new(source.SourceId, source.InputRevision, meshes, materials, instances,new(meshInfos.Select(info=>info.Locator),materialLocators,instanceLocators));
        }
        finally { api.ReleaseImport(pointer); }
    }
    private static void Require(bool condition, string message) => ModelImportProtocol.Require(condition, message);
}
