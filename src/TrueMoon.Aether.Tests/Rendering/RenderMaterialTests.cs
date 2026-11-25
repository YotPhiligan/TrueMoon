using System.Numerics;
using TrueMoon.Aether.Rendering;
using Xunit;

namespace TrueMoon.Aether.Tests.Rendering;

public sealed class RenderMaterialTests
{
    [Fact]
    public void Constructor_DecodesUnequalChannelsOnceAndKeepsSharedMaterialImmutableAcrossTints()
    {
        var material = new OpaqueUnlitMaterialDefinition(new(.5f, .25f, 1));
        var revision = material.Revision;

        var redTint = material.EffectiveLinearColor(new(.5f, 0, 1));
        var greenTint = material.EffectiveLinearColor(new(0, 1, .25f));
        var reset = material.EffectiveLinearColor(Vector3.Zero, tintEnabled: false);

        Near(new(.21404114f, .050876088f, 1), material.BaseColorLinear);
        Near(new(.10702057f, 0, 1), redTint);
        Near(new(0, .050876088f, .25f), greenTint);
        Near(new(.21404114f, .050876088f, 1), reset);
        Assert.Equal(new Vector3(.5f, .25f, 1), material.BaseColorSrgb);
        Assert.Equal(revision, material.Revision);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1202"), material.KindId);
        Assert.Equal(Guid.Parse("6fc9fce0-9547-42f5-b4df-f05f306f1301"), material.ShaderFamilyId);
        Assert.Equal(1, material.LayoutVersion);
        Assert.Equal(1, material.SchemaVersion);
    }

    [Theory]
    [InlineData(0, 1, 0)] [InlineData(1, 1, 1)] [InlineData(.5f, 1, .5f)]
    [InlineData(1, .5f, .735356983f)] [InlineData(.5f, .5f, .360780214f)]
    public void EffectiveLinearColor_AndEncodeMatchIndependentGrayTransferVectors(float basis, float tint, float expectedOutput)
    {
        var material = new OpaqueUnlitMaterialDefinition(new(basis));
        var shaderColor = material.EffectiveLinearColor(new(tint));
        var encoded = RenderColor.Encode(shaderColor);
        Near(new(expectedOutput), encoded);
        if (basis == .5f && tint == .5f)
        {
            Near(new(.10702057f), shaderColor);
            Assert.InRange(encoded.X * 255, 91, 93);
            Assert.NotEqual(new Vector3(.25f), encoded);
        }
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(.01f, .0007739938f)]
    [InlineData(.04f, .0030959752f)] [InlineData(.04045f, .003130805f)]
    [InlineData(.5f, .21404114f)] [InlineData(1, 1)]
    public void Decode_UsesIndependentValuesAcrossBothTransferBranches(float encoded, float expectedLinear)
        => Assert.InRange(MathF.Abs(RenderColor.Decode(encoded) - expectedLinear), 0, .00000003f);

    [Theory]
    [InlineData(0, 0)] [InlineData(.001f, .01292f)]
    [InlineData(.0031308f, .040449936f)] [InlineData(.21404114f, .5f)] [InlineData(1, 1)]
    public void Encode_UsesIndependentValuesAcrossBothTransferBranches(float linear, float expectedEncoded)
        => Assert.InRange(MathF.Abs(RenderColor.Encode(linear) - expectedEncoded), 0, .00000006f);

    [Fact]
    public void ColorVectors_ProcessAllChannelsIndependentlyAndAcceptBlackAndWhite()
    {
        Near(new(.21404114f, .050876088f, 1), RenderColor.Decode(new Vector3(.5f, .25f, 1)));
        Near(new(.735356983f, .5f, 0), RenderColor.Encode(new Vector3(.5f, .21404114f, 0)));
        Assert.Equal(Vector3.Zero, RenderColor.Decode(Vector3.Zero));
        Assert.Equal(Vector3.One, RenderColor.Encode(Vector3.One));
        RenderColor.Validate(Vector3.Zero); RenderColor.Validate(Vector3.One);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Colors_RejectEveryNonfiniteOrAdjacentOutsideChannelWithoutClamping(int axis)
    {
        foreach (var invalid in new[] { MathF.BitDecrement(0), MathF.BitIncrement(1), float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var color = new Vector3(.5f); color[axis] = invalid;
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => RenderColor.Validate(color)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => RenderColor.Decode(color)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => RenderColor.Encode(color)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => RenderColor.Decode(invalid)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => RenderColor.Encode(invalid)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => new OpaqueUnlitMaterialDefinition(color)).Error);
            var material = new OpaqueUnlitMaterialDefinition(Vector3.One);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => material.EffectiveLinearColor(color)).Error);
            Assert.Equal(RenderContentError.InvalidMaterial, Assert.Throws<RenderContentException>(() => material.EffectiveLinearColor(color, tintEnabled: false)).Error);
            Assert.Equal(Vector3.One, material.BaseColorLinear);
        }
    }

    [Theory]
    [InlineData("family", 0)] [InlineData("family", 1)]
    [InlineData("layout", 0)] [InlineData("layout", 2)] [InlineData("layout", -1)]
    [InlineData("schema", 0)] [InlineData("schema", 2)] [InlineData("schema", -1)]
    public void Constructor_RejectsUnsupportedShaderFamilyLayoutAndSchema(string kind, int value)
    {
        var family = kind == "family" ? (value == 0 ? Guid.Empty : Guid.Parse("b30fe1ec-95de-43e9-b062-c30936e55cff")) : RenderContentProfile.UnlitFamilyId;
        var error = Assert.Throws<RenderContentException>(() => new OpaqueUnlitMaterialDefinition(Vector3.One, family,
            layoutVersion: kind == "layout" ? value : 1, schemaVersion: kind == "schema" ? value : 1));
        Assert.Equal(kind == "schema" ? RenderContentError.UnsupportedVersion : RenderContentError.UnsupportedLayout, error.Error);
    }

    private static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.InRange(MathF.Abs(expected.X - actual.X), 0, .0000002f);
        Assert.InRange(MathF.Abs(expected.Y - actual.Y), 0, .0000002f);
        Assert.InRange(MathF.Abs(expected.Z - actual.Z), 0, .0000002f);
    }
}
