#if NETTER
using System.Numerics;
using Assimp;

namespace ModelImportResearch;

internal static class Candidate
{
    internal const string Name="AssimpNetter6.0.5";
    internal const string? ExpectedNativeVersion="6.0.5";
    internal static int LiveImports {get;private set;}
    internal static bool Supports(string name)=>true;
    internal static void Activate()=>_ = Assimp.Unmanaged.AssimpLibrary.Instance.GetVersionMajor();
    internal static void Shutdown()=>Assimp.Unmanaged.AssimpLibrary.Instance.FreeLibrary();
    internal static Imported Import(string path)
    {
        LiveImports++;
        try
        {
            using var context=new AssimpContext();
            var scene=context.ImportFile(path,PostProcessSteps.Triangulate|PostProcessSteps.ValidateDataStructure);
            Projection.Require(scene.AnimationCount==0 && scene.MeshCount<=64,"static bounded Netter scene");
            var parts=scene.Meshes.Select(m=>
            {
                Projection.Require(!m.HasBones && m.Faces.All(f=>f.IndexCount==3),"static triangles");
                return new Part(m.Vertices.ToArray(),m.Faces.SelectMany(f=>f.Indices.Select(i=>checked((uint)i))).ToArray(),m.MaterialIndex);
            }).ToArray();
            var nodes=0;var instances=new List<Instance>();
            void Visit(Node node,Matrix4x4 parent,int depth)
            {
                Projection.Require(++nodes<=4096 && depth<=64,"node/depth cap");
                // Assimp stores column-vector transforms; the managed wrapper copies their raw fields.
                var world=Matrix4x4.Transpose(node.Transform)*parent;
                foreach(var index in node.MeshIndices)instances.Add(new(index,world));
                foreach(var child in node.Children)Visit(child,world,depth+1);
            }
            Visit(scene.RootNode,Matrix4x4.Identity,0);
            return new(parts,instances.ToArray(),nodes);
        }
        finally{LiveImports--;}
    }
}
#endif
