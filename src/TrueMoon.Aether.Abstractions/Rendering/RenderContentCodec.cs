using System.Buffers.Binary;
using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Strict canonical CPU asset bytes and manifest fingerprints. This is not a Runtime transport capability.</summary>
public static class RenderContentCodec
{
    /// <summary>Encodes validated immutable data. GUIDs use RFC4122 order; scalar values are little-endian.</summary>
    /// <param name="definition">Supported mesh, opaque material, binding set or base-color texture.</param><returns>Detached canonical payload.</returns>
    public static byte[] Encode(RenderAssetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var (tag, length) = definition switch
        {
            MeshDefinition mesh => (1, checked(60 + mesh.Vertices.Length * 12 + mesh.Uv0.Length * 8 + mesh.Indices.Length * 4 + mesh.Slots.Length * 24)),
            OpaqueUnlitMaterialDefinition material => (2, material.BaseColorTexture.HasValue ? 132 : 44),
            MaterialBindingSetDefinition set => (3, checked(100 + set.Bindings.Length * 100)),
            BaseColorTextureDefinition texture => (4, checked(20 + texture.Pixels.Length)),
            _ => throw new RenderContentException(RenderContentError.AssetKindMismatch, "Unknown CPU asset definition.")
        };
        var limit = tag is 1 or 4 ? RenderContentProfile.MaximumMeshBytes : RenderContentProfile.MaximumSmallAssetBytes;
        ContentValidation.Require(length <= limit, RenderContentError.LimitExceeded, "Canonical asset payload exceeds admission.");
        var result = new byte[length]; var writer = new Writer(result);
        writer.Bytes("TMRA"u8); writer.U16(1); writer.U16((ushort)tag); writer.U32((uint)definition.SchemaVersion);
        switch (definition)
        {
            case MeshDefinition mesh:
                writer.U32((uint)mesh.Topology); writer.U32((uint)mesh.VertexLayout); writer.U32((uint)mesh.IndexFormat);
                writer.U32((uint)mesh.Vertices.Length); writer.U32((uint)mesh.Indices.Length); writer.U32((uint)mesh.Slots.Length);
                writer.Vector(mesh.Bounds.Minimum); writer.Vector(mesh.Bounds.Maximum);
                foreach (var vertex in mesh.Vertices) writer.Vector(vertex);
                foreach (var uv in mesh.Uv0) { writer.Float(uv.X); writer.Float(uv.Y); }
                foreach (var index in mesh.Indices) writer.U32(index);
                foreach (var slot in mesh.Slots) { writer.Guid(slot.SlotId); writer.U32(slot.FirstIndex); writer.U32(slot.IndexCount); }
                break;
            case OpaqueUnlitMaterialDefinition material:
                writer.Guid(material.ShaderFamilyId); writer.U32((uint)material.LayoutVersion); writer.Vector(material.BaseColorSrgb);
                if (material.BaseColorTexture is { } textureKey)
                { writer.Key(textureKey); writer.U32((uint)material.Sampler!.Value); }
                break;
            case MaterialBindingSetDefinition set:
                writer.Key(set.Mesh); writer.U32((uint)set.Bindings.Length);
                foreach (var binding in set.Bindings) { writer.Guid(binding.SlotId); writer.Key(binding.Material); }
                break;
            case BaseColorTextureDefinition texture:
                writer.U32((uint)texture.Width); writer.U32((uint)texture.Height); writer.Bytes(texture.Pixels.AsSpan());
                break;
        }
        return result;
    }

    /// <summary>Decodes bounded canonical data, rejecting unknown versions, malformed ranges and alternate encodings.</summary>
    /// <param name="payload">One complete canonical asset without trailing bytes.</param><returns>Validated detached immutable definition.</returns>
    public static RenderAssetDefinition Decode(ReadOnlySpan<byte> payload)
    {
        ContentValidation.Require(payload.Length <= RenderContentProfile.MaximumMeshBytes, RenderContentError.LimitExceeded, "Asset payload admission exceeded.");
        var reader = new Reader(payload);
        ContentValidation.Require(reader.Bytes(4).SequenceEqual("TMRA"u8), RenderContentError.InvalidPayload, "Invalid asset magic.");
        ContentValidation.Require(reader.U16() == 1, RenderContentError.UnsupportedVersion, "Unsupported canonical codec version.");
        var tag = reader.U16();
        ContentValidation.Require(reader.U32() == 1, RenderContentError.UnsupportedVersion, "Unsupported asset schema version.");
        ContentValidation.Require(tag is >= 1 and <= 4, RenderContentError.AssetKindMismatch, "Unknown canonical asset kind.");
        if (tag is 2 or 3) ContentValidation.Require(payload.Length <= RenderContentProfile.MaximumSmallAssetBytes, RenderContentError.LimitExceeded, "Small asset admission exceeded.");
        RenderAssetDefinition result;
        if (tag == 1)
        {
            var topology = (MeshTopology)reader.U32(); var layout = (MeshVertexLayout)reader.U32(); var format = (MeshIndexFormat)reader.U32();
            ContentValidation.Require(topology == MeshTopology.TriangleList &&
                layout is MeshVertexLayout.PositionFloat3 or MeshVertexLayout.PositionFloat3Uv0Float2 && format == MeshIndexFormat.UInt32,
                RenderContentError.UnsupportedLayout, "Unsupported canonical geometry layout.");
            var vertices = Count(reader.U32(), RenderContentProfile.MaximumVertices);
            var indices = Count(reader.U32(), RenderContentProfile.MaximumIndices);
            var slots = Count(reader.U32(), RenderContentProfile.MaximumSlots);
            var bounds = new RenderBounds(reader.Vector(), reader.Vector());
            var hasUv0 = layout == MeshVertexLayout.PositionFloat3Uv0Float2;
            reader.Expect(vertices * (hasUv0 ? 20 : 12) + indices * 4 + slots * 24);
            var positions = new Vector3[vertices]; var elements = new uint[indices]; var ranges = new MeshSlot[slots];
            for (var i = 0; i < vertices; i++) positions[i] = reader.Vector();
            var uv0 = hasUv0 ? new Vector2[vertices] : [];
            for (var i = 0; i < uv0.Length; i++) uv0[i] = new(reader.Float(), reader.Float());
            for (var i = 0; i < indices; i++) elements[i] = reader.U32();
            for (var i = 0; i < slots; i++) ranges[i] = new(reader.Guid(), reader.U32(), reader.U32());
            result = hasUv0 ? new MeshDefinition(positions, uv0, elements, ranges, bounds) : new MeshDefinition(positions, elements, ranges, bounds);
        }
        else if (tag == 2)
        {
            var family = reader.Guid(); var layout = reader.U32(); var color = reader.Vector();
            ContentValidation.Require(family == RenderContentProfile.UnlitFamilyId && layout is 1 or 2,
                RenderContentError.UnsupportedLayout, "Unsupported material family or layout.");
            reader.Expect(layout == 1 ? 0 : 88);
            result = layout == 1 ? new OpaqueUnlitMaterialDefinition(color, family) :
                new OpaqueUnlitMaterialDefinition(color, reader.Key(), (RenderTextureSampler)reader.U32());
        }
        else if (tag == 3)
        {
            var mesh = reader.Key(); var count = Count(reader.U32(), RenderContentProfile.MaximumSlots);
            reader.Expect(count * 100);
            var bindings = new MaterialSlotBinding[count];
            for (var i = 0; i < count; i++) bindings[i] = new(reader.Guid(), reader.Key());
            result = new MaterialBindingSetDefinition(mesh, bindings);
        }
        else
        {
            var width = Count(reader.U32(), RenderContentProfile.MaximumTextureDimension);
            var height = Count(reader.U32(), RenderContentProfile.MaximumTextureDimension);
            var bytes = width * height * 4;
            reader.Expect(bytes);
            result = new BaseColorTextureDefinition(width, height, reader.Bytes(bytes));
        }
        ContentValidation.Require(Encode(result).AsSpan().SequenceEqual(payload), RenderContentError.InvalidPayload, "Asset payload is not canonical.");
        return result;
    }

    /// <summary>Encodes a sorted manifest fingerprint, excluding process-local catalog generation.</summary>
    /// <param name="manifest">Validated immutable CPU scope.</param><returns>TMRM v1 bytes including project, build and exact keys.</returns>
    public static byte[] EncodeManifest(RenderContentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var length = checked(60 + manifest.Entries.Length * 84);
        ContentValidation.Require(length <= RenderContentProfile.MaximumManifestBytes, RenderContentError.LimitExceeded, "Manifest byte admission exceeded.");
        var result = new byte[length]; var writer = new Writer(result);
        writer.Bytes("TMRM"u8); writer.U16(1); writer.U16(0); writer.Guid(manifest.ProjectId); writer.Hash(manifest.BuildId);
        writer.U32((uint)manifest.Entries.Length);
        foreach (var entry in manifest.Entries) writer.Key(entry.Key);
        return result;
    }
    private static int Count(uint count, int maximum)
    { ContentValidation.Require(count <= maximum, RenderContentError.LimitExceeded, "Declared content count exceeds admission."); return (int)count; }

    private ref struct Writer(Span<byte> buffer)
    {
        private Span<byte> _remaining = buffer;
        private Span<byte> Take(int count) { var result = _remaining[..count]; _remaining = _remaining[count..]; return result; }
        internal void Bytes(ReadOnlySpan<byte> bytes) => bytes.CopyTo(Take(bytes.Length));
        internal void U16(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Take(2), value);
        internal void U32(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Take(4), value);
        internal void Float(float value) => U32((uint)BitConverter.SingleToInt32Bits(value == 0 ? 0 : value));
        internal void Vector(Vector3 value) { Float(value.X); Float(value.Y); Float(value.Z); }
        internal void Guid(Guid value) => value.TryWriteBytes(Take(16), bigEndian: true, out _);
        internal void Hash(RenderContentHash value) => value.WriteBytes(Take(32));
        internal void Key(RenderAssetKey key)
        { Guid(key.ProjectId); Guid(key.AssetId); Guid(key.KindId); U32((uint)key.SchemaVersion); Hash(key.Revision); }
    }
    private ref struct Reader(ReadOnlySpan<byte> buffer)
    {
        private ReadOnlySpan<byte> _remaining = buffer;
        internal ReadOnlySpan<byte> Bytes(int count)
        {
            ContentValidation.Require(count >= 0 && count <= _remaining.Length, RenderContentError.InvalidPayload, "Truncated canonical asset.");
            var result = _remaining[..count]; _remaining = _remaining[count..]; return result;
        }
        internal void Expect(int count) => ContentValidation.Require(_remaining.Length == count, RenderContentError.InvalidPayload, "Asset byte length/count mismatch.");
        internal ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Bytes(2));
        internal uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Bytes(4));
        internal float Float() => BitConverter.Int32BitsToSingle((int)U32());
        internal Vector3 Vector() => new(Float(), Float(), Float());
        internal Guid Guid() => new(Bytes(16), bigEndian: true);
        internal RenderAssetKey Key()
        {
            var project = Guid(); var asset = Guid(); var kind = Guid(); var version = U32(); var hash = RenderContentHash.FromBytes(Bytes(32));
            ContentValidation.Require(version == 1, RenderContentError.UnsupportedVersion, "Unsupported referenced asset schema.");
            var key = new RenderAssetKey(project, asset, kind, (int)version, hash); key.Validate(); return key;
        }
    }
}
