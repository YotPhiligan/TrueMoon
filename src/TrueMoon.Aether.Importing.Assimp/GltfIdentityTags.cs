using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using TrueMoon.Aether.Tooling.Importing;

namespace TrueMoon.Aether.Importing.Assimp;

// Temporary ASCII transport names bind native output to authored source metadata. No transport number becomes a locator or ID.
internal sealed class GltfIdentityTags
{
    internal sealed record MeshInfo(int SourceMesh,int Material,string? Locator,string? InstanceSuffix);
    internal sealed record MaterialInfo(int SourceMaterial,string? Locator);
    internal sealed record NodeInfo(int? SourceMesh,string? Locator,string? ParentTag);
    internal CapturedModelSource Input { get; private set; } = null!;
    internal Dictionary<string,MeshInfo> Meshes { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string,MaterialInfo> Materials { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string,NodeInfo> Nodes { get; } = new(StringComparer.Ordinal);
    internal Dictionary<int,string> SlottedMeshes { get; } = [];

    internal static GltfIdentityTags Create(CapturedModelSource source)
    {
        var result=new GltfIdentityTags(); var original=source.GetFile(source.RootName).Bytes;
        var glb=Path.GetExtension(source.RootName).Equals(".glb",StringComparison.OrdinalIgnoreCase);
        var json=glb ? original.Slice(20,checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.Span[12..]))) : original;
        var root=JsonNode.Parse(json.Span,documentOptions:new() { MaxDepth=64 })!.AsObject();
        var meshes=root["meshes"]!.AsArray(); var nodes=root["nodes"]?.AsArray() ?? []; var materials=root["materials"]?.AsArray() ?? [];
        Require(meshes.Count is >0 and <=64 && nodes.Count<=4096 && materials.Count<=127,"Source semantic table limits.");
        var meshLabels = meshes.Select(mesh => Label(mesh!.AsObject())).ToArray();
        for(var m=0;m<meshes.Count;m++)
        {
            var mesh=meshes[m]!.AsObject(); var label=meshLabels[m]; var primitives=mesh["primitives"]!.AsArray();
            Require(primitives.Count>0 && primitives.Count<=64-result.Meshes.Count,"Source primitive budget.");
            if (mesh["extras"] is JsonObject extras && extras["truemoon"] is JsonObject settings &&
                settings["combineMaterialSlots"]?.GetValue<bool>() == true)
            {
                Require(label != null && meshLabels.Count(value => value == label) == 1 && primitives.Count == 2,
                    "Combined material slots require a unique mesh label and exactly two primitives.");
                result.SlottedMeshes.Add(m, "mesh/" + label);
            }
            var slotLabels = result.SlottedMeshes.ContainsKey(m) ? new HashSet<string>(StringComparer.Ordinal) : null;
            for(var p=0;p<primitives.Count;p++)
            {
                var primitive=primitives[p]!.AsObject(); var part=Explicit(primitive); var suffix=part!=null ? "/primitive/"+part : primitives.Count==1 ? "" : null;
                var locator=label==null || suffix==null ? null : Bounded("mesh/"+label+suffix);
                if (slotLabels != null)
                    Require(part != null && locator != null && Bounded("slot/" + locator) != null && slotLabels.Add(part),
                        "Combined material slots require distinct explicit primitive subasset labels.");
                var material=primitive["material"]?.GetValue<int>() ?? -1; Require(material>=-1 && material<materials.Count,"Source primitive material reference.");
                result.Meshes.Add(MeshTag(m,primitives.Count,p),new(m,material,locator,suffix));
            }
            mesh["name"]=MeshTag(m,1,0);
        }
        for(var i=0;i<materials.Count;i++)
        {
            var material=materials[i]!.AsObject(); var label=Label(material);
            result.Materials.Add(MaterialTag(i),new(i,label==null ? null : Bounded("material/"+label))); material["name"]=MaterialTag(i);
        }
        var scenes=root["scenes"]!.AsArray(); var scene=root["scene"]?.GetValue<int>() ?? 0;
        Require(scene>=0 && scene<scenes.Count,"Selected source scene."); var seen=new HashSet<int>();
        string? scenePrefix="node/";
        if(scenes.Count>1)
        {
            var sceneLabel=Label(scenes[scene]!.AsObject());
            scenePrefix=sceneLabel!=null && scenes.Count(value=>Label(value!.AsObject())==sceneLabel)==1 ? "scene/"+sceneLabel+"/node/" : null;
        }
        void Visit(int index,string? parentPath,string? parentTag,int depth)
        {
            Require(index>=0 && index<nodes.Count && depth<=64 && seen.Add(index),"Source node graph/depth/duplicate occurrence.");
            var node=nodes[index]!.AsObject(); var label=Label(node);
            var path=label==null || parentTag!=null && parentPath==null || parentTag==null && scenePrefix==null ? null : Bounded(parentTag==null ? scenePrefix+label : parentPath+"/"+label);
            var mesh=node["mesh"]?.GetValue<int>(); if(mesh!=null) Require(mesh>=0 && mesh<meshes.Count,"Source node mesh reference.");
            result.Nodes.Add(NodeTag(index),new(mesh,path,parentTag));
            if(node["children"] is JsonArray children) foreach(var child in children) Visit(child!.GetValue<int>(),path,NodeTag(index),depth+1);
        }
        foreach(var node in scenes[scene]!["nodes"]!.AsArray()) Visit(node!.GetValue<int>(),null,null,0);
        // A name must be unique in the whole authored namespace, including currently unused source objects.
        var duplicateMeshes=result.Meshes.Values.Where(value=>value.Locator!=null).GroupBy(value=>value.Locator,StringComparer.Ordinal).Where(group=>group.Count()>1).Select(group=>group.Key).ToHashSet(StringComparer.Ordinal);
        foreach(var tag in result.Meshes.Keys.ToArray()) if(result.Meshes[tag].Locator is { } locator && duplicateMeshes.Contains(locator)) result.Meshes[tag]=result.Meshes[tag] with {Locator=null};
        var duplicateMaterials=result.Materials.Values.Where(value=>value.Locator!=null).GroupBy(value=>value.Locator,StringComparer.Ordinal).Where(group=>group.Count()>1).Select(group=>group.Key).ToHashSet(StringComparer.Ordinal);
        foreach(var tag in result.Materials.Keys.ToArray()) if(result.Materials[tag].Locator is { } locator && duplicateMaterials.Contains(locator)) result.Materials[tag]=result.Materials[tag] with {Locator=null};
        var duplicateNodes=result.Nodes.Values.Where(value=>value.Locator!=null).GroupBy(value=>value.Locator,StringComparer.Ordinal).Where(group=>group.Count()>1).Select(group=>group.Key!).ToArray();
        foreach(var tag in result.Nodes.Keys.ToArray()) if(result.Nodes[tag].Locator is { } locator && duplicateNodes.Any(duplicate=>locator==duplicate || locator.StartsWith(duplicate+"/",StringComparison.Ordinal))) result.Nodes[tag]=result.Nodes[tag] with {Locator=null};
        // Preserve the remaining document and all BIN bytes. Names alone are replaced, within the original capture budgets.
        for(var i=0;i<nodes.Count;i++) nodes[i]!["name"]=NodeTag(i);
        var text=Encoding.UTF8.GetBytes(root.ToJsonString()); byte[] bytes;
        if(glb)
        {
            var oldJsonLength=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(original.Span[12..])); var tail=original[(20+oldJsonLength)..]; var padded=checked((text.Length+3)&~3);
            var length=checked(20+padded+tail.Length); Require(length<=16*1024*1024,"Tagged GLB byte budget."); bytes=new byte[length];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes,0x46546c67); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),2);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8),(uint)length); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12),(uint)padded);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16),0x4e4f534a); text.CopyTo(bytes,20); bytes.AsSpan(20+text.Length,padded-text.Length).Fill(32); tail.Span.CopyTo(bytes.AsSpan(20+padded));
        }
        else bytes=text;
        Require(bytes.Length<=16*1024*1024 && source.Files.Sum(file=>(long)file.Length)-original.Length+bytes.Length<=32*1024*1024,"Tagged source closure byte budget.");
        result.Input=new(source.SourceId,source.RootName,source.Files.Select(file=>file.Name==source.RootName ? new CapturedModelFile(file.Name,bytes) : file));
        return result;
    }
    internal string? Instance(NodeInfo node,MeshInfo mesh)
    {
        Require(node.SourceMesh==mesh.SourceMesh,"Native node/primitive provenance mismatch.");
        var suffix = SlottedMeshes.ContainsKey(mesh.SourceMesh) ? "" : mesh.InstanceSuffix;
        return node.Locator==null || suffix==null ? null : Bounded("instance/"+node.Locator+suffix);
    }
    private static string MeshTag(int mesh,int count,int primitive) => "tm-m-"+mesh+(count>1 ? "-"+primitive : "");
    private static string MaterialTag(int material) => "tm-t-"+material;
    private static string NodeTag(int node) => "tm-n-"+node;
    private static string? Label(JsonObject value)
    {
        var authored=Explicit(value); if(authored!=null) return authored;
        return value["name"] is { } name ? Segment(name.GetValue<string>()) : null;
    }
    private static string? Explicit(JsonObject value)
    {
        if(value["extras"] is not JsonObject extras || extras["truemoon"] is not JsonObject settings || !settings.TryGetPropertyValue("subasset",out var key)) return null;
        return Segment(key?.GetValue<string>() ?? "") ?? throw new InvalidDataException("Invalid explicit subasset label.");
    }
    private static string? Segment(string name)
    {
        if(string.IsNullOrWhiteSpace(name) || name.Length>64 || name.Any(char.IsControl) || Encoding.UTF8.GetByteCount(name)>64 || !name.IsNormalized()) return null;
        return Uri.EscapeDataString(name);
    }
    private static string? Bounded(string value) => Encoding.UTF8.GetByteCount(value)<=240 ? value : null;
    private static void Require(bool condition,string message) => ModelImportProtocol.Require(condition,message);
}
