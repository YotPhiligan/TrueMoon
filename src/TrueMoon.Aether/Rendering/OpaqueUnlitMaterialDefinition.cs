using System.Numerics;

namespace TrueMoon.Aether.Rendering;

/// <summary>Immutable opaque base color using the built-in unlit family and layout one.</summary>
public sealed class OpaqueUnlitMaterialDefinition : RenderAssetDefinition
{
    /// <summary>Creates an opaque material using the built-in family.</summary><param name="baseColorSrgb">Finite stored-sRGB RGB in [0,1].</param>
    public OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb) : this(baseColorSrgb, RenderContentProfile.UnlitFamilyId) { }
    /// <summary>Creates an exact supported family/layout version.</summary>
    /// <param name="baseColorSrgb">Stored-sRGB RGB.</param><param name="shaderFamilyId">Built-in unlit family.</param>
    /// <param name="layoutVersion">Exact layout one.</param><param name="schemaVersion">Exact material schema one.</param>
    public OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb, Guid shaderFamilyId, int layoutVersion = 1, int schemaVersion = 1)
        : base(RenderContentProfile.MaterialKindId, schemaVersion)
    {
        ContentValidation.Require(shaderFamilyId == RenderContentProfile.UnlitFamilyId && layoutVersion == 1,
            RenderContentError.UnsupportedLayout, "Unsupported opaque material family/layout.");
        RenderColor.Validate(baseColorSrgb);
        BaseColorSrgb = baseColorSrgb; BaseColorLinear = RenderColor.Decode(baseColorSrgb); ShaderFamilyId = shaderFamilyId; LayoutVersion = layoutVersion;
        Complete();
    }
    /// <summary>Gets the original bounded sRGB color.</summary>
    public Vector3 BaseColorSrgb { get; }
    /// <summary>Gets the decoded immutable linear color.</summary>
    public Vector3 BaseColorLinear { get; }
    /// <summary>Gets the built-in family identity.</summary>
    public Guid ShaderFamilyId { get; }
    /// <summary>Gets exact layout one.</summary>
    public int LayoutVersion { get; }
    /// <summary>Applies a renderer-wide linear tint, validating it even when disabled.</summary>
    /// <param name="tintLinear">Finite multiplier in [0,1].</param><param name="tintEnabled">False selects identity without changing stored tint.</param><returns>Linear shader color.</returns>
    public Vector3 EffectiveLinearColor(Vector3 tintLinear, bool tintEnabled = true)
    { RenderColor.Validate(tintLinear); return BaseColorLinear * (tintEnabled ? tintLinear : Vector3.One); }
}
