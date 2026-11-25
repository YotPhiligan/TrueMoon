using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Rendering;

namespace ModelImportResearch;

internal sealed record Part(Vector3[] Positions, uint[] Indices, int Material);
internal sealed record Instance(int Part, Matrix4x4 World);
internal sealed record Imported(Part[] Parts, Instance[] Instances, int Nodes);

internal static class Projection
{
    internal static string Fingerprint(Imported model)
    {
        Require(model.Parts.Length is > 0 and <= 64 && model.Nodes is > 0 and <= 4096, "scene counts");
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        foreach (var part in model.Parts)
        {
            Require(part.Positions.Length > 0 && part.Indices.Length % 3 == 0 && part.Material >= 0, "part shape");
            var min = part.Positions.Aggregate(new Vector3(float.PositiveInfinity), Vector3.Min);
            var max = part.Positions.Aggregate(new Vector3(float.NegativeInfinity), Vector3.Max);
            // The real production CPU validator and canonical codec, not a parallel validation model.
            var mesh = new MeshDefinition(part.Positions, part.Indices,
                [new MeshSlot(RenderContentProfile.DefaultSlotId, 0, (uint)part.Indices.Length)], new(min, max));
            writer.Write(RenderContentCodec.Encode(mesh));
            writer.Write(part.Material);
        }
        foreach (var instance in model.Instances)
        {
            Require(instance.Part >= 0 && instance.Part < model.Parts.Length, "instance index");
            writer.Write(instance.Part);
            var m = instance.World;
            foreach (var value in new[] { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 })
            { Require(float.IsFinite(value), "finite matrix"); writer.Write(value == 0 ? 0f : value); }
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    internal static void Verify(Imported model, string name)
    {
        Require(model.Parts.All(p => p.Indices.All(i => i < p.Positions.Length)), "indices");
        if (name == "grid.glb")
        {
            Require(model.Parts.Length == 1 && model.Parts[0].Indices.Length == 96774 && model.Instances.Length == 1, "grid cardinality");
            var positions = model.Parts[0].Positions;
            Require(positions.Aggregate(Vector3.Min) == Vector3.Zero && positions.Aggregate(Vector3.Max) == new Vector3(127,127,0), "grid bounds");
            Require(positions.Length == 16384, "grid retained vertices");
            var expected=new HashSet<(int,int,int)>();
            for(var y=0;y<127;y++)for(var x=0;x<127;x++){var a=y*128+x;expected.Add((a,a+1,a+128));expected.Add((a+1,a+129,a+128));}
            var actual=new HashSet<(int,int,int)>();var part=model.Parts[0];
            int Vertex(uint index){var v=positions[index];Require(v.X==MathF.Truncate(v.X)&&v.Y==MathF.Truncate(v.Y)&&v.Z==0,"literal grid vertex");return (int)v.X+128*(int)v.Y;}
            for(var i=0;i<part.Indices.Length;i+=3)
            {
                var a=Vertex(part.Indices[i]);var b=Vertex(part.Indices[i+1]);var c=Vertex(part.Indices[i+2]);
                var triangle=a<=b&&a<=c?(a,b,c):b<=a&&b<=c?(b,c,a):(c,a,b);
                Require(actual.Add(triangle),"no duplicated grid triangle");
            }
            Require(actual.SetEquals(expected),"complete grid topology and winding despite vertex/index reordering");
        }
        else if (name is "scene.glb" or "scene.gltf")
        {
            Require(model.Parts.Length == 2 && model.Parts.All(p => p.Indices.Length == 3), "two triangles");
            Require(model.Parts.Select(p => p.Material).Distinct().Count() == 2, "two material assignments");
            Require(model.Instances.Length == 4, "two nodes share both parts");
            Require(model.Instances.GroupBy(i => i.Part).All(g => g.Count() == 2), "shared geometry references");
            var origins = model.Instances.Select(i => Vector3.Transform(Vector3.Zero, i.World)).Distinct().OrderBy(v => v.X).ToArray();
            Require(origins.SequenceEqual(new[] { new Vector3(10,2,0), new Vector3(13,0,0) }), "literal parent/child translations");
            var triangle = model.Parts[0];
            var a=triangle.Positions[triangle.Indices[0]]; var b=triangle.Positions[triangle.Indices[1]]; var c=triangle.Positions[triangle.Indices[2]];
            Require(Vector3.Cross(b-a,c-a).Z == 1, "right-handed triangle winding");
        }
        else if (name == "scene.obj")
        {
            Require(model.Parts.Length == 2 && model.Parts.All(p => p.Indices.Length == 3), "OBJ triangles");
            Require(model.Parts.Select(p => p.Material).Distinct().Count() == 2 && model.Instances.Length == 2, "OBJ materials/instances");
        }
        else if (name == "triangle.fbx")
        {
            Require(model.Parts.Length == 1 && model.Parts[0].Indices.Length == 3 && model.Instances.Length == 1, "FBX triangle");
            Require(model.Parts[0].Positions.Aggregate(Vector3.Min) == Vector3.Zero && model.Parts[0].Positions.Aggregate(Vector3.Max) == new Vector3(1,1,0), "FBX literal geometry");
        }
        _ = Fingerprint(model);
    }

    internal static void Require(bool condition, string detail)
    { if (!condition) throw new InvalidDataException("Research assertion failed: " + detail); }
}
