using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderContentCodecTests
{
    [Fact]
    public void EncodeDecode_MaterialMatchesIndependentFortyFourByteGoldenPayloadAndRevision()
    {
        var expected = Convert.FromHexString(RenderContentFixture.MaterialHex);
        var material = RenderContentFixture.Material();
        Assert.Equal(expected, RenderContentCodec.Encode(material));
        Assert.Equal(RenderContentFixture.MaterialHash, material.Revision.ToString());
        var decoded = Assert.IsType<OpaqueUnlitMaterialDefinition>(RenderContentCodec.Decode(expected));
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1202"), decoded.KindId);
        Assert.Equal((1, 1), (decoded.SchemaVersion, decoded.LayoutVersion));
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1301"), decoded.ShaderFamilyId);
        Assert.Equal(new Vector3(.25f, .5f, 1), decoded.BaseColorSrgb);
        Assert.InRange(MathF.Abs(decoded.BaseColorLinear.X - .050876088f), 0, .000001f);
        Assert.InRange(MathF.Abs(decoded.BaseColorLinear.Y - .21404114f), 0, .000001f);
        Assert.Equal(1f, decoded.BaseColorLinear.Z);
        Assert.Equal(RenderContentFixture.MaterialHash, decoded.Revision.ToString());
        Assert.Equal(expected, RenderContentCodec.Encode(decoded));
    }

    [Fact]
    public void EncodeDecode_MeshMatchesIndependentLittleEndianGeometryAndNetworkGuidGoldenPayload()
    {
        var expected = Convert.FromHexString(RenderContentFixture.MeshHex);
        var mesh = RenderContentFixture.Mesh();
        Assert.Equal(132, expected.Length);
        Assert.Equal(expected, RenderContentCodec.Encode(mesh));
        Assert.Equal(RenderContentFixture.MeshHash, mesh.Revision.ToString());
        var decoded = Assert.IsType<MeshDefinition>(RenderContentCodec.Decode(expected));
        Assert.Equal(new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY }, decoded.Vertices);
        Assert.Equal(new uint[] { 0, 1, 2 }, decoded.Indices);
        Assert.Equal(new MeshSlot(RenderContentFixture.Slot, 0, 3), Assert.Single(decoded.Slots));
        Assert.Equal(new RenderBounds(Vector3.Zero, new(1, 1, 0)), decoded.Bounds);
        Assert.Equal((MeshTopology.TriangleList, MeshVertexLayout.PositionFloat3, MeshIndexFormat.UInt32),
            (decoded.Topology, decoded.VertexLayout, decoded.IndexFormat));
        Assert.Equal(RenderContentFixture.MeshHash, decoded.Revision.ToString());
        Array.Fill(expected, (byte)0);
        Assert.Equal(new Vector3(1, 0, 0), decoded.Vertices[1]);
        Assert.Equal(2u, decoded.Indices[2]);
        Assert.Equal(RenderContentFixture.MeshHash, decoded.Revision.ToString());
    }

    [Fact]
    public void EncodeDecode_BindingPayloadPinsAllKeyDimensionsAndMaterialSlotBytes()
    {
        var entries = RenderContentFixture.Entries();
        var set = Assert.IsType<MaterialBindingSetDefinition>(entries[2].Definition);
        var expected = RenderContentFixture.LiteralBindingBytes();
        Assert.Equal(200, expected.Length);
        Assert.Equal(expected, RenderContentCodec.Encode(set));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), set.Revision.ToString());
        var decoded = Assert.IsType<MaterialBindingSetDefinition>(RenderContentCodec.Decode(expected));
        Assert.Equal(entries[0].Key, decoded.Mesh);
        Assert.Equal(RenderContentFixture.Slot, Assert.Single(decoded.Bindings).SlotId);
        Assert.Equal(entries[1].Key, decoded.Bindings[0].Material);
        decoded.ValidateAgainst(entries[0].Key, Assert.IsType<MeshDefinition>(entries[0].Definition));
        Assert.Equal(expected, RenderContentCodec.Encode(decoded));
    }

    [Fact]
    public void EncodeDecode_NonzeroZAndConservativeBoundsHaveIndependentBytesWhileLogicalIdsStayOutsideRevision()
    {
        var bytes = Convert.FromHexString(RenderContentFixture.MeshHex);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(36), -.25f);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(56), 1);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(80), .75f);
        var mesh = new MeshDefinition([Vector3.Zero, new(1, 0, .75f), Vector3.UnitY], [0, 1, 2],
            [new(RenderContentFixture.Slot, 0, 3)], new(new(-.25f, 0, 0), Vector3.One));
        Assert.Equal(bytes, RenderContentCodec.Encode(mesh));
        var decoded = Assert.IsType<MeshDefinition>(RenderContentCodec.Decode(bytes));
        Assert.Equal(new Vector3(1, 0, .75f), decoded.Vertices[1]);
        Assert.Equal(new RenderBounds(new(-.25f, 0, 0), Vector3.One), decoded.Bounds);
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.Equal(expectedHash, decoded.Revision.ToString());
        var keyA = mesh.CreateKey(RenderContentFixture.Project, RenderContentFixture.MeshId);
        var keyB = mesh.CreateKey(RenderContentFixture.Generation, RenderContentFixture.MaterialId);
        Assert.NotEqual(keyA, keyB);
        Assert.Equal(expectedHash, keyA.Revision.ToString());
        Assert.Equal(expectedHash, keyB.Revision.ToString());
    }

    [Theory]
    [InlineData("material")] [InlineData("bounds")] [InlineData("vertex")]
    public void Encode_NormalizesSignedZeroAndDecoderRejectsItsAlternateWireEncoding(string target)
    {
        var negativeZero = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        RenderAssetDefinition positive;
        RenderAssetDefinition negative;
        int offset;
        if (target == "material")
        {
            positive = new OpaqueUnlitMaterialDefinition(new(0, .5f, 1));
            negative = new OpaqueUnlitMaterialDefinition(new(negativeZero, .5f, 1));
            offset = 32;
        }
        else
        {
            positive = RenderContentFixture.Mesh();
            negative = new MeshDefinition([new(target == "vertex" ? negativeZero : 0, 0, 0), Vector3.UnitX, Vector3.UnitY],
                [0, 1, 2], [new(RenderContentFixture.Slot, 0, 3)],
                new(new(target == "bounds" ? negativeZero : 0, 0, 0), new(1, 1, 0)));
            offset = target == "bounds" ? 36 : 60;
        }
        var bytes = RenderContentCodec.Encode(positive);
        Assert.Equal(bytes, RenderContentCodec.Encode(negative));
        Assert.Equal(positive.Revision, negative.Revision);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 0x80000000);
        Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode(bytes));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Decode_RejectsEveryTruncatedPrefixAndTrailingBytesForEveryKind(int index)
    {
        var complete = RenderContentCodec.Encode(RenderContentFixture.Entries()[index].Definition);
        for (var length = 0; length < complete.Length; length++)
        {
            var prefix = complete[..length];
            Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode(prefix));
        }
        Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode([.. complete, 0]));
        Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode([.. complete, 0, 0, 0, 0]));
        Assert.Equal(complete, RenderContentCodec.Encode(RenderContentCodec.Decode(complete)));
    }

    [Theory]
    [InlineData("magic", RenderContentError.InvalidPayload)]
    [InlineData("codec-zero", RenderContentError.UnsupportedVersion)]
    [InlineData("codec", RenderContentError.UnsupportedVersion)]
    [InlineData("tag-zero", RenderContentError.AssetKindMismatch)]
    [InlineData("tag-next", RenderContentError.AssetKindMismatch)]
    [InlineData("tag-high", RenderContentError.AssetKindMismatch)]
    [InlineData("schema-zero", RenderContentError.UnsupportedVersion)]
    [InlineData("schema-high", RenderContentError.UnsupportedVersion)]
    public void Decode_RejectsUnknownHeaderDiscriminators(string field, RenderContentError error)
    {
        var bytes = Convert.FromHexString(RenderContentFixture.MaterialHex);
        switch (field)
        {
            case "magic": bytes[0] = 0; break;
            case "codec-zero": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 0); break;
            case "codec": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2); break;
            case "tag-zero": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 0); break;
            case "tag-next": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 4); break;
            case "tag-high": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), ushort.MaxValue); break;
            case "schema-zero": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0); break;
            default: BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uint.MaxValue); break;
        }
        Error(error, () => RenderContentCodec.Decode(bytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Decode_WellShapedEmptyGeometryOrBindingSetIsSemanticallyRejected(bool bindingSet)
    {
        if (bindingSet)
        {
            var bytes = RenderContentFixture.LiteralBindingBytes()[..100];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(96), 0);
            Error(RenderContentError.InvalidBinding, () => RenderContentCodec.Decode(bytes));
        }
        else
        {
            var bytes = Convert.FromHexString(RenderContentFixture.MeshHex)[..60];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0);
            Error(RenderContentError.InvalidGeometry, () => RenderContentCodec.Decode(bytes));
        }
    }

    [Theory]
    [InlineData("material-color", RenderContentError.InvalidMaterial)]
    [InlineData("material-family", RenderContentError.UnsupportedLayout)]
    [InlineData("material-layout", RenderContentError.UnsupportedLayout)]
    [InlineData("mesh-topology", RenderContentError.UnsupportedLayout)]
    [InlineData("mesh-layout", RenderContentError.UnsupportedLayout)]
    [InlineData("mesh-format", RenderContentError.UnsupportedLayout)]
    [InlineData("mesh-bounds", RenderContentError.InvalidBounds)]
    [InlineData("mesh-vertex", RenderContentError.InvalidGeometry)]
    [InlineData("mesh-index", RenderContentError.InvalidGeometry)]
    [InlineData("mesh-range", RenderContentError.InvalidGeometry)]
    public void Decode_ValidatesSemanticPayloadInsteadOfReturningUncheckedData(string field, RenderContentError error)
    {
        var bytes = Convert.FromHexString(field.StartsWith("material", StringComparison.Ordinal)
            ? RenderContentFixture.MaterialHex : RenderContentFixture.MeshHex);
        switch (field)
        {
            case "material-color": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0x7fc00000); break;
            case "material-family": Array.Clear(bytes, 12, 16); break;
            case "material-layout": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), uint.MaxValue); break;
            case "mesh-topology": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0); break;
            case "mesh-layout": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 2); break;
            case "mesh-format": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), uint.MaxValue); break;
            case "mesh-bounds": BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(36), 2); break;
            case "mesh-vertex": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x7f800000); break;
            case "mesh-index": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(96), 3); break;
            default: BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(124), 3); break;
        }
        Error(error, () => RenderContentCodec.Decode(bytes));
    }

    [Theory]
    [InlineData(24, 65536u)] [InlineData(28, 196608u)] [InlineData(32, 8u)] [InlineData(96, 8u)]
    public void Decode_CountAdmissionIsInclusiveAndRejectsExcessBeforeProportionalAllocation(int offset, uint limit)
    {
        var bytes = offset == 96 ? RenderContentFixture.LiteralBindingBytes() : Convert.FromHexString(RenderContentFixture.MeshHex);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), limit);
        Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode(bytes));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), limit + 1);
        Error(RenderContentError.LimitExceeded, () => RenderContentCodec.Decode(bytes));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), uint.MaxValue);
        Error(RenderContentError.LimitExceeded, () => RenderContentCodec.Decode(bytes));
    }

    [Fact]
    public void Decode_RejectsSmallAndOverallByteAdmissionBeforeReadingTheClaimedBody()
    {
        var tooLarge = new byte[2097153];
        "TMRA"u8.CopyTo(tooLarge);
        Error(RenderContentError.LimitExceeded, () => RenderContentCodec.Decode(tooLarge));
        var small = new byte[4097];
        Convert.FromHexString(RenderContentFixture.MaterialHex).CopyTo(small, 0);
        Error(RenderContentError.LimitExceeded, () => RenderContentCodec.Decode(small));
        Error(RenderContentError.InvalidPayload, () => RenderContentCodec.Decode(small[..4096]));
        Assert.Throws<ArgumentNullException>(() => RenderContentCodec.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => RenderContentCodec.EncodeManifest(null!));
    }

    [Fact]
    public void Decode_MaximumAdmittedGeometryPreservesEveryVertexIndexAndEighthRange()
    {
        var positions = new Vector3[65536];
        var indices = new uint[196608];
        positions[^1] = new(.25f, .5f, .75f);
        indices[^1] = 65535;
        var slots = Enumerable.Range(1, 8).Select(index => new MeshSlot(new Guid($"00000000-0000-0000-0000-{index:000000000000}"),
            (uint)((index - 1) * 24576), 24576)).ToArray();
        var mesh = new MeshDefinition(positions, indices, slots, new(Vector3.Zero, Vector3.One));
        var bytes = RenderContentCodec.Encode(mesh);
        Assert.Equal(1573116, bytes.Length);
        var decoded = Assert.IsType<MeshDefinition>(RenderContentCodec.Decode(bytes));
        Assert.Equal(65536, decoded.Vertices.Length);
        Assert.Equal(196608, decoded.Indices.Length);
        Assert.Equal(new Vector3(.25f, .5f, .75f), decoded.Vertices[^1]);
        Assert.Equal(65535u, decoded.Indices[^1]);
        Assert.Equal(8, decoded.Slots.Length);
        Assert.Equal(new MeshSlot(Guid.Parse("00000000-0000-0000-0000-000000000008"), 172032, 24576), decoded.Slots[7]);
        Assert.Equal(mesh.Revision, decoded.Revision);
        Assert.Equal(bytes, RenderContentCodec.Encode(decoded));
    }

    private static void Error(RenderContentError expected, Action action)
        => Assert.Equal(expected, Assert.Throws<RenderContentException>(action).Error);
}

internal static class RenderContentFixture
{
    internal const string MaterialHex = "544d524101000200010000006fc9fce0954742f5b4dff05f306f1301010000000000803e0000003f0000803f";
    internal const string MaterialHash = "46350670c072101accf42f5c4836a88086bbd81b7b6119d22aaf7aea66c7c59b";
    internal const string MeshHex = "544d524101000100010000000100000001000000010000000300000003000000010000000000000000000000000000000000803f0000803f000000000000000000000000000000000000803f0000000000000000000000000000803f00000000000000000100000002000000112233445566778899aabbccddeeff000000000003000000";
    internal const string MeshHash = "2f20bee22c0512189eb08271e1f5657fb6f023a7415d2adddd8bd45cb2987dcc";
    internal static readonly Guid Project = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    internal static readonly Guid Generation = Guid.Parse("aabbccdd-eeff-0011-2233-445566778899");
    internal static readonly RenderContentHash Build = RenderContentHash.Parse("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    internal static readonly Guid MeshId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    internal static readonly Guid MaterialId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    internal static readonly Guid BindingsId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    internal static readonly Guid Slot = Guid.Parse("11223344-5566-7788-99aa-bbccddeeff00");
    internal static MeshDefinition Mesh() => new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2], [new(Slot, 0, 3)], new(Vector3.Zero, new(1, 1, 0)));
    internal static OpaqueUnlitMaterialDefinition Material() => new(new(.25f, .5f, 1));
    internal static RenderAssetEntry[] Entries()
    {
        var mesh = Mesh(); var material = Material();
        var meshKey = mesh.CreateKey(Project, MeshId); var materialKey = material.CreateKey(Project, MaterialId);
        var bindings = new MaterialBindingSetDefinition(meshKey, [new(Slot, materialKey)]);
        return [new(meshKey, mesh), new(materialKey, material), new(bindings.CreateKey(Project, BindingsId), bindings)];
    }
    internal static byte[] LiteralBindingBytes() => Convert.FromHexString("544d52410100030001000000" +
        Project.ToString("N") + MeshId.ToString("N") + "6fc9fce0954742f5b4dff05f306f120101000000" + MeshHash + "01000000" +
        Slot.ToString("N") + Project.ToString("N") + MaterialId.ToString("N") + "6fc9fce0954742f5b4dff05f306f120201000000" + MaterialHash);
    internal static RenderContentManifest Manifest(IReadOnlyList<RenderAssetEntry>? entries = null)
        => new(Project, Build, Generation, entries ?? Entries());
}
