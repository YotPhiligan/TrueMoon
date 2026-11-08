using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

internal static class NomResourceMetadataCodec
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static byte[] Encode(AssetRecord asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var locator = Utf8.GetBytes(asset.Locator); var metadata = asset.Metadata;
        NomBinary.Require(locator.Length is > 0 and <= 1024 && metadata.Length <= 64 * 1024, "Nom metadata budget.");
        var tags = asset.Tags; var dependencies = asset.Dependencies;
        var extension = tags.Length == 0 && dependencies.Length == 0 ? [] : NomRetentionCodec.EncodeLinks(tags, dependencies);
        var bytes = new byte[68 + locator.Length + metadata.Length + extension.Length];
        (extension.Length == 0 ? "NOMAST01"u8 : "NOMAST02"u8).CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        NomBinary.WriteGuid(bytes.AsSpan(12), asset.Id); NomBinary.WriteGuid(bytes.AsSpan(28), asset.Source); NomBinary.WriteGuid(bytes.AsSpan(44), asset.Kind);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60), locator.Length); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), metadata.Length);
        locator.CopyTo(bytes, 68); metadata.CopyTo(bytes, 68 + locator.Length);
        extension.CopyTo(bytes, 68 + locator.Length + metadata.Length); return bytes;
    }
    internal static AssetRecord Decode(ReadOnlySpan<byte> bytes)
    {
        NomBinary.Require(bytes.Length <= 128 * 1024,"Nom asset record byte budget.");
        var extended = bytes.Length >= 8 && bytes[..8].SequenceEqual("NOMAST02"u8);
        NomBinary.Require(bytes.Length >= 68 && (extended || bytes[..8].SequenceEqual("NOMAST01"u8)) &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]) == 1, "Unsupported Nom asset format.");
        NomBinary.RequireZero(bytes.Slice(10, 2));
        var locatorLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[60..]); var metadataLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[64..]);
        var baseLength = 68L + locatorLength + metadataLength;
        NomBinary.Require(locatorLength is > 0 and <= 1024 && metadataLength <= 64 * 1024 &&
            (extended ? bytes.Length > baseLength : bytes.Length == baseLength), "Nom asset framing/budget.");
        string locator;
        try { locator = Utf8.GetString(bytes.Slice(68, (int)locatorLength)); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid Nom UTF-8 locator.", error); }
        var links = extended ? NomRetentionCodec.DecodeLinks(bytes[(int)baseLength..].ToArray()) : (Tags: Array.Empty<string>(), Dependencies: Array.Empty<AssetResourceVersion>());
        NomBinary.Require(!extended || links.Tags.Length + links.Dependencies.Length > 0, "Noncanonical empty metadata extension.");
        return new(NomBinary.ReadGuid(bytes[12..]), NomBinary.ReadGuid(bytes[28..]), NomBinary.ReadGuid(bytes[44..]), locator,
            bytes.Slice(68 + (int)locatorLength, (int)metadataLength).ToArray(), links.Tags, links.Dependencies);
    }
    internal static string Revision(AssetRecord asset, string contentDigest, long length)
        => HashCanonicalMetadata(Encode(asset),contentDigest,length);
    // Callers validate canonical metadata once before hashing the exact immutable bytes.
    internal static string HashCanonicalMetadata(ReadOnlySpan<byte> metadata,string contentDigest,long length)
    {
        NomBinary.Require(length is >= 0 and <= 512L * 1024 * 1024, "Nom resource length budget.");
        var digest = NomBinary.DigestBytes(contentDigest);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("TrueMoon.Nom/resource/1\0"u8);
        Span<byte> number = stackalloc byte[8]; BinaryPrimitives.WriteInt32LittleEndian(number, metadata.Length);
        hash.AppendData(number[..4]); hash.AppendData(metadata); hash.AppendData(digest);
        BinaryPrimitives.WriteInt64LittleEndian(number, length); hash.AppendData(number);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
