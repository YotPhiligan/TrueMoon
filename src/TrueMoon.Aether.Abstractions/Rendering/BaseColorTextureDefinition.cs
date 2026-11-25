using System.Collections.Immutable;

namespace TrueMoon.Aether.Rendering;

/// <summary>Supported base-color sampling policies.</summary>
public enum RenderTextureSampler
{
    /// <summary>Nearest texel, clamp to edge, mip level zero and normalized coordinates.</summary>
    NearestClamp = 1
}

/// <summary>Immutable single-level opaque base-color texture in tightly packed sRGB RGBA8.</summary>
public sealed class BaseColorTextureDefinition : RenderAssetDefinition
{
    /// <summary>Copies the supplied texels. The first row corresponds to V=0.</summary>
    /// <param name="width">Width within the profile limit.</param>
    /// <param name="height">Height within the profile limit.</param>
    /// <param name="pixels">Row-major RGBA bytes with alpha 255.</param>
    public BaseColorTextureDefinition(int width, int height, ReadOnlySpan<byte> pixels)
        : base(RenderContentProfile.BaseColorTextureKindId, RenderContentProfile.SchemaVersion)
    {
        ContentValidation.Require(width is > 0 and <= RenderContentProfile.MaximumTextureDimension &&
            height is > 0 and <= RenderContentProfile.MaximumTextureDimension,
            RenderContentError.LimitExceeded, "Base-color texture dimensions exceed the profile bounds.");
        ContentValidation.Require(pixels.Length == width * height * 4, RenderContentError.InvalidPayload, "Invalid RGBA8 texture byte count.");
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
            ContentValidation.Require(pixels[alpha] == 255, RenderContentError.InvalidPayload, "Base-color texture texels must be opaque.");
        Width = width;
        Height = height;
        Pixels = ImmutableArray.Create(pixels);
        Complete();
    }

    /// <summary>Width in texels.</summary>
    public int Width { get; }
    /// <summary>Height in texels.</summary>
    public int Height { get; }
    /// <summary>Copied tightly packed sRGB RGBA8 texels.</summary>
    public ImmutableArray<byte> Pixels { get; }
}
