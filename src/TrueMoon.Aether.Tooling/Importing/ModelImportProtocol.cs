using System.Numerics;
using System.Text;
using TrueMoon.Aether.Abstractions.Rendering;

namespace TrueMoon.Aether.Tooling.Importing;

// The worker emits v2. Parent-published candidates with explicit scaling persist as v3.
internal static class ModelImportProtocol
{
    private const int NativeCandidate = 0x32434d54;
    private const int ScaledCandidate = 0x33434d54;
    internal const int MaximumOutputBytes = 8 * 1024 * 1024;
    private const int MaximumInputBytes = 32 * 1024 * 1024 + 32768;

    internal static void WriteInput(string path, CapturedModelSource source)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(file, Encoding.UTF8);
        writer.Write(0x31494d54); writer.Write(source.SourceId.ToByteArray()); WriteText(writer, source.RootName);
        WriteText(writer, source.InputRevision); writer.Write(source.Files.Count);
        foreach (var entry in source.Files) { WriteText(writer, entry.Name); writer.Write(entry.Length); writer.Write(entry.Bytes.Span); }
        if (file.Length > MaximumInputBytes) throw new InvalidDataException("Input packet limit exceeded.");
    }

    internal static CapturedModelSource ReadInput(string path)
    {
        using var reader = Open(path, MaximumInputBytes);
        Require(reader.ReadInt32() == 0x31494d54, "Input packet version.");
        var id = new Guid(Bytes(reader, 16)); Require(id != Guid.Empty, "Source identity.");
        var root = ModelSourcePath.Normalize(Text(reader, 256)); var revision = Text(reader, 64);
        var count = Count(reader, 32); Require(count > 0, "Empty input closure.");
        var entries = new List<CapturedModelFile>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var total = 0;
        for (var i = 0; i < count; i++)
        {
            var name = Text(reader, 256); Require(ModelSourcePath.Normalize(name) == name && names.Add(name), "Noncanonical or duplicate input name.");
            var length = Count(reader, 16 * 1024 * 1024); total = checked(total + length);
            Require(total <= 32 * 1024 * 1024, "Input byte limit."); entries.Add(new(name, Bytes(reader, length)));
        }
        End(reader); Require(entries.Any(entry => entry.Name == root), "Missing root.");
        var result = new CapturedModelSource(id, root, entries); Require(result.InputRevision == revision, "Input revision mismatch.");
        return result;
    }

    internal static byte[] Encode(ModelImportCandidate candidate)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8);
        var scaled = candidate.GeometryScale != 1;
        writer.Write(scaled ? ScaledCandidate : NativeCandidate); WriteText(writer, ModelImportCandidate.Recipe);
        writer.Write(candidate.SourceId.ToByteArray()); WriteText(writer, candidate.InputRevision);
        if (scaled) writer.Write(candidate.GeometryScale);
        writer.Write(candidate.Materials.Count);
        foreach (var material in candidate.Materials) Asset(writer, material);
        writer.Write(candidate.Meshes.Count);
        foreach (var mesh in candidate.Meshes) { writer.Write(mesh.MaterialIndex); Asset(writer, mesh.Geometry); Budget(stream); }
        writer.Write(candidate.Instances.Count);
        foreach (var instance in candidate.Instances)
        {
            writer.Write(instance.MeshIndex); var m = instance.World;
            foreach (var value in new[] { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 })
                writer.Write(value == 0 ? 0f : value);
            Budget(stream);
        }
        foreach (var hints in new[] { candidate.Semantics.Meshes, candidate.Semantics.Materials, candidate.Semantics.Instances })
        {
            writer.Write(hints.Count);
            foreach (var hint in hints) { writer.Write((byte)(hint == null ? 0 : 1)); if (hint != null) WriteText(writer, hint); Budget(stream); }
        }
        return stream.ToArray();
    }

    internal static ModelImportCandidate ReadOutput(string path, CapturedModelSource source)
    {
        try { using var reader = Open(path, MaximumOutputBytes); return ReadOutputCore(reader, source.SourceId, source.InputRevision, allowScaled: false); }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated candidate packet.", error); }
    }

    internal static ModelImportCandidate Decode(byte[] bytes, Guid sourceId, string inputRevision)
    {
        Require(bytes.Length <= MaximumOutputBytes, "Candidate packet quota.");
        try { using var reader = new BinaryReader(new MemoryStream(bytes, writable: false)); return ReadOutputCore(reader, sourceId, inputRevision, allowScaled: true); }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated candidate packet.", error); }
    }

    private static ModelImportCandidate ReadOutputCore(BinaryReader reader, Guid sourceId, string inputRevision, bool allowScaled)
    {
        var version = reader.ReadInt32();
        Require((version == NativeCandidate || allowScaled && version == ScaledCandidate) &&
            Text(reader, 128) == ModelImportCandidate.Recipe, "Candidate recipe/version.");
        Require(new Guid(Bytes(reader, 16)) == sourceId && Text(reader, 64) == inputRevision, "Candidate input identity mismatch.");
        var geometryScale = version == ScaledCandidate ? reader.ReadSingle() : 1;
        Require(ModelImportCandidate.IsValidGeometryScale(geometryScale) &&
            (version != ScaledCandidate || geometryScale != 1), "Invalid candidate geometry scale.");
        var materials = new OpaqueUnlitMaterialDefinition[Count(reader, 128)];
        for (var i = 0; i < materials.Length; i++) materials[i] = ReadAsset(reader, RenderContentProfile.MaximumSmallAssetBytes) as OpaqueUnlitMaterialDefinition
            ?? throw new InvalidDataException("Expected opaque material.");
        var meshes = new ModelImportMesh[Count(reader, 64)];
        for (var i = 0; i < meshes.Length; i++)
        {
            var material = reader.ReadInt32(); var mesh = ReadAsset(reader, RenderContentProfile.MaximumMeshBytes) as MeshDefinition
                ?? throw new InvalidDataException("Expected triangle mesh."); meshes[i] = new(mesh, material);
        }
        var instances = new ModelImportInstance[Count(reader, 4096)];
        for (var i = 0; i < instances.Length; i++) instances[i] = new(reader.ReadInt32(), new Matrix4x4(
            reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),
            reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));
        string?[] Hints(int maximum)
        {
            var hints = new string?[Count(reader, maximum)];
            for (var i = 0; i < hints.Length; i++) { var tag = reader.ReadByte(); Require(tag <= 1, "Invalid nullable locator tag."); if (tag == 1) hints[i] = Text(reader, 256); }
            return hints;
        }
        var semantics = new ModelImportSemantics(Hints(64), Hints(128), Hints(4096));
        End(reader); return new(sourceId, inputRevision, meshes, materials, instances, semantics, geometryScale);
    }

    private static void Asset(BinaryWriter writer, RenderAssetDefinition asset) { var bytes = RenderContentCodec.Encode(asset); writer.Write(bytes.Length); writer.Write(bytes); }
    private static RenderAssetDefinition ReadAsset(BinaryReader reader, int maximum) => RenderContentCodec.Decode(Bytes(reader, Count(reader, maximum)));
    private static void Budget(Stream stream) => Require(stream.Length <= MaximumOutputBytes, "CPU output byte limit.");
    private static BinaryReader Open(string path, int maximum)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximum) { stream.Dispose(); throw new InvalidDataException("Worker packet byte limit."); }
        return new(stream, new UTF8Encoding(false, true));
    }
    private static int Count(BinaryReader reader, int maximum) { var value = reader.ReadInt32(); Require(value >= 0 && value <= maximum, "Worker packet count/length limit."); return value; }
    private static byte[] Bytes(BinaryReader reader, int count)
    {
        Require(reader.BaseStream.Length - reader.BaseStream.Position >= count, "Truncated worker packet.");
        var bytes = reader.ReadBytes(count); Require(bytes.Length == count, "Truncated worker packet."); return bytes;
    }
    private static string Text(BinaryReader reader, int maximum) => new UTF8Encoding(false, true).GetString(Bytes(reader, Count(reader, maximum)));
    private static void WriteText(BinaryWriter writer, string value) { var bytes = Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static void End(BinaryReader reader) => Require(reader.BaseStream.Position == reader.BaseStream.Length, "Trailing worker packet bytes.");
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
