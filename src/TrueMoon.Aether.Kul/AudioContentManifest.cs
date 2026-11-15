using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrueMoon.Aether.Kul;

/// <summary>Pure bounded version-one capture codec. Publishing files is the caller's explicit tooling operation.</summary>
public sealed class AudioContentManifest
{
    /// <summary>Only metadata file permitted beside the declared encoded audio files.</summary>
    public const string FileName = "audio-content.json";
    /// <summary>Maximum encoded metadata size.</summary>
    public const int MaximumBytes = 65536;
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false };
    /// <summary>Validated immutable snapshot, ordered by stable identity.</summary>
    public IReadOnlyList<AudioContentAsset> Assets { get; }
    /// <summary>Copies and validates at most64assets with unique IDs and case-insensitive file names.</summary>
    /// <param name="assets">Bounded captured descriptors.</param>
    public AudioContentManifest(IEnumerable<AudioContentAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var copy = assets.Take(65).ToArray();
        if (copy.Length > 64) throw new InvalidDataException("At most64audio assets are supported.");
        var ids = new HashSet<AudioAssetId>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < copy.Length; i++)
        {
            var asset = copy[i];
            if (asset is null || !asset.Id.IsValid || !ids.Add(asset.Id) || !ValidName(asset.FileName) || !names.Add(asset.FileName) ||
                !IsHash(asset.Sha256) || asset.Length <= 0 || !Enum.IsDefined(asset.Kind) || asset.Voices is < 1 or > 16)
                throw new InvalidDataException("Invalid or duplicate captured audio descriptor.");
            copy[i] = asset with { Sha256 = asset.Sha256.ToUpperInvariant() };
        }
        Assets = Array.AsReadOnly(copy.OrderBy(a => a.Id.Value).ToArray());
    }
    /// <summary>Produces deterministic version-one UTF8 metadata.</summary><returns>Bounded bytes.</returns>
    public byte[] Encode()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document(1, Assets.ToArray()), Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Audio metadata is too large.");
        return bytes;
    }
    /// <summary>Rejects unsupported versions, missing/unknown fields and invalid descriptors.</summary>
    /// <param name="bytes">Raw immutable metadata, whose hash identifies the capture.</param><returns>Validated snapshot.</returns>
    public static AudioContentManifest Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > MaximumBytes) throw new InvalidDataException("Invalid audio metadata size.");
        try
        {
            var document = JsonSerializer.Deserialize<Document>(bytes, Json);
            if (document is null || document.Version != 1 || document.Assets is null) throw new InvalidDataException("Unsupported audio metadata.");
            return new(document.Assets);
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid audio metadata.", error); }
    }
    /// <summary>Checks a full SHA256 hexadecimal identity.</summary><param name="value">Hash text.</param><returns>Whether valid.</returns>
    public static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool ValidName(string? value) => value is { Length: > 0 and <= 120 } && value is not "." and not ".." &&
        value.All(c => c > 31 && c < 127 && !"<>:\"/\\|?*".Contains(c)) && !value.EndsWith(' ') && !value.EndsWith('.') &&
        (value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) &&
        !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
            .Contains(value.Split('.')[0], StringComparer.OrdinalIgnoreCase);
    private sealed record Document(int Version, AudioContentAsset[] Assets);
}
