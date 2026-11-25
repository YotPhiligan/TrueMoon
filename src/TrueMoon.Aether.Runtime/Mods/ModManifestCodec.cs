using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrueMoon.Aether.Mods;

namespace TrueMoon.Aether.Runtime.Mods;

/// <summary>Canonical host-side manifest serialization shared by publisher and loader; no activation occurs.</summary>
public static class ModManifestCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true
    };
    /// <summary>Serializes one data-only manifest. Runtime admission performs semantic validation.</summary>
    /// <param name="manifest">Complete description.</param><returns>Deterministic UTF8 JSON.</returns>
    public static byte[] Encode(ModManifest manifest) => JsonSerializer.SerializeToUtf8Bytes(manifest, Options);
    /// <summary>Parses strict UTF8 JSON without activating code.</summary><param name="bytes">Bounded input bytes.</param><returns>Manifest.</returns>
    public static ModManifest Decode(ReadOnlySpan<byte> bytes)
    {
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        Check(document.RootElement);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("formatVersion", out _) || !document.RootElement.TryGetProperty("apiVersion", out _))
            throw new InvalidDataException("Manifest/API versions must be explicit.");
        return JsonSerializer.Deserialize<ModManifest>(bytes, Options) ?? throw new InvalidDataException("Null module manifest.");
    }
    private static void Check(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON member.");
                Check(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var element in value.EnumerateArray()) Check(element);
    }
    /// <summary>Computes a lowercase content address.</summary><param name="bytes">Exact bytes.</param><returns>SHA256 hex.</returns>
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
