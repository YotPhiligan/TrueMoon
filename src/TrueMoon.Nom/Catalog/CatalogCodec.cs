using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;


namespace TrueMoon.Nom.Catalog;

internal sealed record CatalogLimits(int QueueCount = 32, int QueueBytes = 16 * 1024 * 1024,
    int BatchRows = 256, int BatchBytes = 4 * 1024 * 1024, int DocumentBytes = 128 * 1024,long DomainIndexBytes=64L*1024*1024)
{
    internal void Validate()
    {
        if (QueueCount is < 1 or > 32 || QueueBytes is < 1 or > 16 * 1024 * 1024 || BatchRows is < 1 or > 256 ||
            BatchBytes is < 1 or > 4 * 1024 * 1024 || DocumentBytes is < 1 or > 128 * 1024 || DomainIndexBytes is < 1 or > 256L*1024*1024)
            throw new InvalidDataException("Invalid catalog limits.");
    }
}

internal static class CatalogCodec
{
    internal const int MaximumMetadata = 64 * 1024;
    internal const int MaximumDocument = 128 * 1024;
    internal static readonly UTF8Encoding Utf8 = new(false, true);

    internal static string Key(params string[] fields)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (var field in fields)
        {
            var bytes = Utf8.GetBytes(field); BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    internal static string Binding(AssetRecord row) => Key("TrueMoon.Nom/binding/1", row.Source.ToString("N"), row.Kind.ToString("N"), row.Locator);
    internal static string Revision(AssetRecord row) => Key("TrueMoon.Nom/metadata/1", row.Kind.ToString("N"), Convert.ToHexStringLower(row.Metadata));

    internal static byte[] Encode(AssetRecord row, int maximum = MaximumDocument)
    {
        var bytes = NomResourceMetadataCodec.Encode(row);
        if (maximum is < 1 or > MaximumDocument || bytes.Length > maximum) throw new InvalidDataException("Serialized metadata budget.");
        return bytes;
    }
    internal static AssetRecord Decode(byte[] bytes) => NomResourceMetadataCodec.Decode(bytes);
}
