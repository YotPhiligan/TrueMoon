using System.Text;
using System.Text.Json;

namespace ModelImportResearch;

internal static class Fixtures
{
    internal static void Create(string directory)
    {
        Directory.CreateDirectory(directory);
        WriteScene(directory, false);
        WriteScene(directory, true);
        WriteGrid(directory);
        File.WriteAllText(Path.Combine(directory,"scene.obj"), "mtllib scene.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nv 0 0 1\nv 1 0 1\nv 0 1 1\no first\nusemtl Red\nf 1 2 3\no second\nusemtl Green\nf 4 5 6\n");
        File.WriteAllText(Path.Combine(directory,"scene.mtl"), "newmtl Red\nKd 1 0 0\nnewmtl Green\nKd 0 1 0\n");
        File.WriteAllText(Path.Combine(directory,"bad.gltf"), "{broken");
        var valid = File.ReadAllText(Path.Combine(directory,"scene.gltf"));
        File.WriteAllText(Path.Combine(directory,"missing.gltf"), valid.Replace("scene.bin","missing.bin",StringComparison.Ordinal));
        var truncated = File.ReadAllBytes(Path.Combine(directory,"scene.glb"));
        File.WriteAllBytes(Path.Combine(directory,"truncated.glb"),truncated[..32]);
        File.WriteAllText(Path.Combine(directory,"triangle.fbx"), Fbx);
    }

    private static void WriteScene(string directory, bool glb)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        foreach (var v in new float[] {0,0,0,1,0,0,0,1,0,0,0,1,1,0,1,0,1,1}) writer.Write(v);
        foreach(var i in new uint[]{0,1,2,0,1,2}) writer.Write(i);
        var bytes=data.ToArray();
        var root=new Dictionary<string,object?>
        {
            ["asset"]=new {version="2.0",generator="TrueMoon owned research fixtures"},
            ["scene"]=0,["scenes"]=new[]{new {nodes=new[]{0}}},
            ["nodes"]=new object[]{new{name="parent",translation=new[]{10,0,0},children=new[]{1,2}},new{name="a",mesh=0,translation=new[]{0,2,0}},new{name="b",mesh=0,translation=new[]{3,0,0}}},
            ["meshes"]=new[]{new{name="shared",primitives=new[]{new{attributes=new{POSITION=0},indices=2,material=0,mode=4},new{attributes=new{POSITION=1},indices=3,material=1,mode=4}}}},
            ["materials"]=new[]{new{name="Red",pbrMetallicRoughness=new{baseColorFactor=new[]{1,0,0,1},metallicFactor=0,roughnessFactor=1}},new{name="Green",pbrMetallicRoughness=new{baseColorFactor=new[]{0,1,0,1},metallicFactor=0,roughnessFactor=1}}},
            ["buffers"]=new[]{glb ? new Dictionary<string,object>{{"byteLength",bytes.Length}} : new Dictionary<string,object>{{"byteLength",bytes.Length},{"uri","scene.bin"}}},
            ["bufferViews"]=new[]{new{buffer=0,byteOffset=0,byteLength=36,target=34962},new{buffer=0,byteOffset=36,byteLength=36,target=34962},new{buffer=0,byteOffset=72,byteLength=12,target=34963},new{buffer=0,byteOffset=84,byteLength=12,target=34963}},
            ["accessors"]=new object[]{new{bufferView=0,componentType=5126,count=3,type="VEC3",min=new[]{0,0,0},max=new[]{1,1,0}},new{bufferView=1,componentType=5126,count=3,type="VEC3",min=new[]{0,0,1},max=new[]{1,1,1}},new{bufferView=2,componentType=5125,count=3,type="SCALAR"},new{bufferView=3,componentType=5125,count=3,type="SCALAR"}}
        };
        var json=JsonSerializer.Serialize(root);
        if(glb) WriteGlb(Path.Combine(directory,"scene.glb"),json,bytes);
        else {File.WriteAllText(Path.Combine(directory,"scene.gltf"),json);File.WriteAllBytes(Path.Combine(directory,"scene.bin"),bytes);}
    }

    private static void WriteGrid(string directory)
    {
        using var data=new MemoryStream();using var writer=new BinaryWriter(data);
        for(var y=0;y<128;y++)for(var x=0;x<128;x++){writer.Write((float)x);writer.Write((float)y);writer.Write(0f);}
        for(uint y=0;y<127;y++)for(uint x=0;x<127;x++){var a=y*128+x;foreach(var i in new[]{a,a+1,a+128,a+1,a+129,a+128})writer.Write(i);}
        var bytes=data.ToArray();
        var root=new{asset=new{version="2.0"},scene=0,scenes=new[]{new{nodes=new[]{0}}},nodes=new[]{new{mesh=0}},meshes=new[]{new{primitives=new[]{new{attributes=new{POSITION=0},indices=1,mode=4}}}},
            buffers=new[]{new{byteLength=bytes.Length}},bufferViews=new[]{new{buffer=0,byteOffset=0,byteLength=196608,target=34962},new{buffer=0,byteOffset=196608,byteLength=387096,target=34963}},
            accessors=new object[]{new{bufferView=0,componentType=5126,count=16384,type="VEC3",min=new[]{0,0,0},max=new[]{127,127,0}},new{bufferView=1,componentType=5125,count=96774,type="SCALAR"}}};
        WriteGlb(Path.Combine(directory,"grid.glb"),JsonSerializer.Serialize(root),bytes);
    }

    private static void WriteGlb(string path,string json,byte[] data)
    {
        var text=Encoding.UTF8.GetBytes(json);var jsonLength=(text.Length+3)&~3;var binLength=(data.Length+3)&~3;
        using var file=File.Create(path);using var writer=new BinaryWriter(file);
        writer.Write(0x46546c67u);writer.Write(2u);writer.Write((uint)(12+8+jsonLength+8+binLength));
        writer.Write((uint)jsonLength);writer.Write(0x4e4f534au);writer.Write(text);for(var i=text.Length;i<jsonLength;i++)writer.Write((byte)32);
        writer.Write((uint)binLength);writer.Write(0x004e4942u);writer.Write(data);for(var i=data.Length;i<binLength;i++)writer.Write((byte)0);
    }

    // Created here from literal geometry, no upstream test models or asset licenses are inherited.
    private const string Fbx="""
; FBX 7.4.0 project file
FBXHeaderExtension:  {
    FBXHeaderVersion: 1003
    FBXVersion: 7400
}
GlobalSettings:  {
    Version: 1000
    Properties70:  {
        P: "UpAxis", "int", "Integer", "",1
        P: "UpAxisSign", "int", "Integer", "",1
        P: "FrontAxis", "int", "Integer", "",2
        P: "FrontAxisSign", "int", "Integer", "",-1
        P: "CoordAxis", "int", "Integer", "",0
        P: "CoordAxisSign", "int", "Integer", "",1
        P: "UnitScaleFactor", "double", "Number", "",100
    }
}
Definitions:  {
    Version: 100
    Count: 2
    ObjectType: "Geometry" { Count: 1 }
    ObjectType: "Model" { Count: 1 }
}
Objects:  {
    Geometry: 1001, "Geometry::triangle", "Mesh" {
        Vertices: *9 { a: 0,0,0,1,0,0,0,1,0 }
        PolygonVertexIndex: *3 { a: 0,1,-3 }
        GeometryVersion: 124
        Layer: 0 { Version: 100 }
    }
    Model: 1002, "Model::triangle", "Mesh" {
        Version: 232
        Properties70:  {
            P: "Lcl Translation", "Lcl Translation", "", "A",0,0,0
            P: "Lcl Rotation", "Lcl Rotation", "", "A",0,0,0
            P: "Lcl Scaling", "Lcl Scaling", "", "A",1,1,1
        }
        Shading: T
        Culling: "CullingOff"
    }
}
Connections:  {
    C: "OO",1001,1002
    C: "OO",1002,0
}
""";
}
