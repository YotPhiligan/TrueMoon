#if SILK
using System.Numerics;
using Silk.NET.Assimp;
using Api=Silk.NET.Assimp.Assimp;

namespace ModelImportResearch;

internal static unsafe class Candidate
{
    internal const string Name="Silk.NET.Assimp2.23.0+Ultz6.0.2";
    internal const string? ExpectedNativeVersion="6.0.2";
    internal static int LiveImports {get;private set;}
    internal static bool Supports(string name)=>true;
    private static readonly Api SharedApi=Api.GetApi();
    internal static void Activate()=>_ = SharedApi.GetVersionMajor();
    internal static void Shutdown()=>SharedApi.Dispose();
    internal static Imported Import(string path)
    {
        var api=SharedApi;
        var scene=api.ImportFile(path,(uint)(PostProcessSteps.Triangulate|PostProcessSteps.ValidateDataStructure));
        if(scene==null)throw new InvalidDataException("Assimp import failed: "+api.GetErrorStringS());
        LiveImports++;
        try
        {
            Projection.Require(scene->MNumAnimations==0 && scene->MNumMeshes<=64,"static bounded Silk scene");
            var parts=new Part[scene->MNumMeshes];
            for(var i=0;i<parts.Length;i++)
            {
                var mesh=scene->MMeshes[i];
                Projection.Require(mesh->MNumBones==0 && mesh->MNumVertices<=65536 && mesh->MNumFaces<=65536,"native mesh counts");
                var positions=new Vector3[mesh->MNumVertices];
                for(var v=0;v<positions.Length;v++){var p=mesh->MVertices[v];positions[v]=new(p.X,p.Y,p.Z);}
                var indices=new uint[checked(mesh->MNumFaces*3)];
                for(var f=0;f<mesh->MNumFaces;f++){var face=mesh->MFaces[f];Projection.Require(face.MNumIndices==3,"native triangle");for(var k=0;k<3;k++)indices[f*3+k]=face.MIndices[k];}
                parts[i]=new(positions,indices,checked((int)mesh->MMaterialIndex));
            }
            var instances=new List<Instance>();var nodes=0;
            void Visit(Node* node,Matrix4x4 parent,int depth)
            {
                Projection.Require(node!=null && ++nodes<=4096 && depth<=64,"native node/depth cap");
                var m=node->MTransformation;
                var world=Matrix4x4.Transpose(new(m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44))*parent;
                for(var i=0;i<node->MNumMeshes;i++)instances.Add(new(checked((int)node->MMeshes[i]),world));
                for(var i=0;i<node->MNumChildren;i++)Visit(node->MChildren[i],world,depth+1);
            }
            Visit(scene->MRootNode,Matrix4x4.Identity,0);
            return new(parts,instances.ToArray(),nodes);
        }
        finally{api.ReleaseImport(scene);LiveImports--;}
    }
}
#endif
