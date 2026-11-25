using System.Numerics;

namespace TrueMoon.Aether.Rendering;


/// <summary>Immutable opaque base color using the built-in unlit family.</summary>
public sealed class OpaqueUnlitMaterialDefinition : RenderAssetDefinition
{
    /// <summary>Creates an opaque material using the built-in family.</summary><param name="baseColorSrgb">Finite stored-sRGB RGB in [0,1].</param>
    public OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb) : this(baseColorSrgb, RenderContentProfile.UnlitFamilyId) { }
    /// <summary>Creates an exact supported family/layout version.</summary>
    /// <param name="baseColorSrgb">Stored-sRGB RGB.</param><param name="shaderFamilyId">Built-in unlit family.</param>
    /// <param name="layoutVersion">Exact layout one.</param><param name="schemaVersion">Exact material schema one.</param>
    public OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb, Guid shaderFamilyId, int layoutVersion = 1, int schemaVersion = 1)
        : this(baseColorSrgb, shaderFamilyId, layoutVersion, schemaVersion, null, null) { }

    /// <summary>Creates layout two with one pinned base-color texture multiplied by the supplied color factor.</summary>
    /// <param name="baseColorSrgb">Stored-sRGB RGB factor.</param>
    /// <param name="baseColorTexture">Exact texture key in the same project.</param>
    /// <param name="sampler">Sampling policy for UV0.</param>
    public OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb, RenderAssetKey baseColorTexture,
        RenderTextureSampler sampler = RenderTextureSampler.NearestClamp)
        : this(baseColorSrgb, RenderContentProfile.UnlitFamilyId, 2, 1, baseColorTexture, sampler) { }

    private OpaqueUnlitMaterialDefinition(Vector3 baseColorSrgb, Guid shaderFamilyId, int layoutVersion, int schemaVersion,
        RenderAssetKey? baseColorTexture, RenderTextureSampler? sampler)
        : base(RenderContentProfile.MaterialKindId, schemaVersion)
    {
        ContentValidation.Require(shaderFamilyId == RenderContentProfile.UnlitFamilyId &&
            (baseColorTexture.HasValue ? layoutVersion == 2 && sampler == RenderTextureSampler.NearestClamp : layoutVersion == 1),
            RenderContentError.UnsupportedLayout, "Unsupported opaque material family/layout.");
        baseColorTexture?.Validate(RenderContentProfile.BaseColorTextureKindId);
        BaseColorTexture = baseColorTexture; Sampler = sampler;
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
    /// <summary>Gets the exact layout version.</summary>
    public int LayoutVersion { get; }
    /// <summary>Gets the pinned sRGB texture for layout two, if present.</summary>
    public RenderAssetKey? BaseColorTexture { get; }
    /// <summary>Gets the sampling policy for the pinned texture.</summary>
    public RenderTextureSampler? Sampler { get; }
    /// <summary>Applies a renderer-wide linear tint, validating it even when disabled.</summary>
    /// <param name="tintLinear">Finite multiplier in [0,1].</param><param name="tintEnabled">False selects identity without changing stored tint.</param><returns>Linear shader color.</returns>
    public Vector3 EffectiveLinearColor(Vector3 tintLinear, bool tintEnabled = true)
    { RenderColor.Validate(tintLinear); return BaseColorLinear * (tintEnabled ? tintLinear : Vector3.One); }
}
