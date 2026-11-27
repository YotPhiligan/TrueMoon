using System.Buffers.Binary;
using SkiaSharp;
using TrueMoon.Aether.Rendering;

namespace TrueMoon.Aether.Importing;

internal static class BaseColorPng
{
    internal static BaseColorTextureDefinition Decode(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("Expected a PNG header.");
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
        if (width is 0 or > RenderContentProfile.MaximumTextureDimension ||
            height is 0 or > RenderContentProfile.MaximumTextureDimension || bytes[24] != 8)
            throw new InvalidDataException("Base-color PNG dimensions or sample depth exceed the profile.");

        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Invalid base-color PNG.");
        var source = codec.Info;
        if (codec.EncodedFormat != SKEncodedImageFormat.Png || source.Width != width || source.Height != height ||
            codec.FrameCount > 1)
            throw new InvalidDataException("Expected a single PNG image matching the header.");
        // glTF determines the transfer function from material use. Preserve the encoded RGB values.
        using var colorSpace = source.ColorSpace;
        var alpha = source.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Unpremul;
        var rgba = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, alpha, colorSpace);
        if (codec.GetPixels(rgba, out var pixels) != SKCodecResult.Success)
            throw new InvalidDataException("Incomplete or invalid base-color PNG pixels.");
        return new(source.Width, source.Height, pixels);
    }
}
