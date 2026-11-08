#if GLTF
using System.Numerics;
using SharpGLTF.Schema2;

namespace ModelImportResearch;

internal static class Candidate
{
    internal const string Name="SharpGLTF.Core1.0.7";
    internal const string? ExpectedNativeVersion=null;
    internal static int LiveImports {get;private set;}
    internal static bool Supports(string name)=>Path.GetExtension(name) is ".gltf" or ".glb";
    internal static void Activate() { }
    internal static void Shutdown() { }
    internal static Imported Import(string path)
    {
        LiveImports++;
        try
        {
            var model=ModelRoot.Load(path);
            Projection.Require(model.LogicalAnimations.Count==0 && model.LogicalSkins.Count==0,"static glTF");
            var parts=new List<Part>();var map=new Dictionary<Mesh,int[]>();
            foreach(var mesh in model.LogicalMeshes)
            {
                var indexes=new List<int>();
                foreach(var primitive in mesh.Primitives)
                {
                    Projection.Require(primitive.DrawPrimitiveType==PrimitiveType.TRIANGLES && primitive.MorphTargetsCount==0,"triangle/no morph glTF");
                    var positions=primitive.GetVertexAccessor("POSITION").AsVector3Array().ToArray();
                    var indices=primitive.GetIndices()?.ToArray()??Enumerable.Range(0,positions.Length).Select(i=>(uint)i).ToArray();
                    indexes.Add(parts.Count);parts.Add(new(positions,indices,primitive.Material?.LogicalIndex??0));
                }
                map.Add(mesh,indexes.ToArray());
            }
            var nodes=0;var instances=new List<Instance>();
            void Visit(Node node,Matrix4x4 parent,int depth)
            {
                Projection.Require(++nodes<=4096 && depth<=64,"glTF node/depth cap");
                var world=node.LocalMatrix*parent;
                if(node.Mesh!=null)foreach(var i in map[node.Mesh])instances.Add(new(i,world));
                foreach(var child in node.VisualChildren)Visit(child,world,depth+1);
            }
            foreach(var node in model.DefaultScene.VisualChildren)Visit(node,Matrix4x4.Identity,0);
            return new(parts.ToArray(),instances.ToArray(),nodes);
        }
        finally{LiveImports--;}
    }
}
#endif
