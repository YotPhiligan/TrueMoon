#if NETTERLOW
using System.Numerics;
using Assimp;
using Assimp.Unmanaged;

namespace ModelImportResearch;

internal static unsafe class Candidate
{
    internal const string Name="AssimpNetter6.0.5-low-level";
    internal const string? ExpectedNativeVersion="6.0.5";
    internal static int LiveImports {get;private set;}
    internal static bool Supports(string name)=>true;
    internal static void Activate()=>_ = AssimpLibrary.Instance.GetVersionMajor();
    internal static void Shutdown()=>AssimpLibrary.Instance.FreeLibrary();
    internal static Imported Import(string path, nint fileIo = 0, ImportLimits? limits = null)
    {
        limits ??= new(); limits.Validate();
        var api=AssimpLibrary.Instance;
        var pointer=api.ImportFile(path,PostProcessSteps.Triangulate|PostProcessSteps.ValidateDataStructure,fileIo,IntPtr.Zero);
        if(pointer==IntPtr.Zero)throw new InvalidDataException("Assimp import failed: "+api.GetErrorString());
        LiveImports++;
        try
        {
            var scene=(AiScene*)pointer;
            Projection.Require(scene->NumAnimations==0 && scene->NumMeshes>0 && scene->NumMeshes<=limits.Meshes,"static bounded Netter low scene");
            var parts=new Part[scene->NumMeshes];
            long outputBytes=0;
            for(var i=0;i<parts.Length;i++)
            {
                var mesh=(AiMesh*)((IntPtr*)scene->Meshes)[i];
                Projection.Require(mesh->NumBones==0 && mesh->NumVertices>0 && mesh->NumVertices<=limits.Vertices && mesh->NumFaces>0 && mesh->NumFaces<=limits.Faces,"native mesh counts");
                outputBytes=checked(outputBytes + mesh->NumVertices*12L + mesh->NumFaces*12L);
                Projection.Require(outputBytes<=limits.OutputBytes,"CPU output byte quota before mesh arrays");
                var positions=new ReadOnlySpan<Vector3>((void*)mesh->Vertices,checked((int)mesh->NumVertices)).ToArray();
                var indices=new uint[checked(mesh->NumFaces*3)];
                for(var f=0;f<mesh->NumFaces;f++)
                {
                    var face=((AiFace*)mesh->Faces)[f];Projection.Require(face.NumIndices==3,"native triangle");
                    for(var k=0;k<3;k++)indices[f*3+k]=((uint*)face.Indices)[k];
                }
                parts[i]=new(positions,indices,checked((int)mesh->MaterialIndex));
            }
            var instances=new List<Instance>();var nodes=0;
            void Visit(AiNode* node,Matrix4x4 parent,int depth)
            {
                Projection.Require(node!=null && ++nodes<=limits.Nodes && depth<=limits.Depth,"native node/depth cap");
                Projection.Require(node->NumMeshes<=limits.Instances-instances.Count && node->NumChildren<=limits.Nodes-nodes,"native instance/child cap");
                outputBytes=checked(outputBytes+node->NumMeshes*68L);
                Projection.Require(outputBytes<=limits.OutputBytes,"CPU output byte quota before instances");
                var world=Matrix4x4.Transpose(node->Transformation)*parent;
                for(var i=0;i<node->NumMeshes;i++)instances.Add(new(checked((int)((uint*)node->Meshes)[i]),world));
                for(var i=0;i<node->NumChildren;i++)Visit((AiNode*)((IntPtr*)node->Children)[i],world,depth+1);
            }
            Visit((AiNode*)scene->RootNode,Matrix4x4.Identity,0);
            return new(parts,instances.ToArray(),nodes);
        }
        finally{api.ReleaseImport(pointer);LiveImports--;}
    }
}
#endif
