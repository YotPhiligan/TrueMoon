using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Aether.Scenes;

namespace TrueMoon.Aether.Ecs.Loading;

/// <summary>Bounded versioned envelope over the existing strict authoring JSON codec. No game code or registry is required.</summary>
public sealed class SceneWorldCodec
{
    private readonly SceneFileCodec _scenes;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    /// <summary>Envelope header size: magic4, version4, body length4, SHA25632.</summary>
    public const int HeaderBytes = 44;
    /// <summary>Maximum complete envelope size; old control/checkpoint budgets remain independent.</summary>
    public const int MaximumBytes = SceneFileCodec.MaximumBytes + HeaderBytes;
    /// <summary>Creates a codec with a borrowed existing scene codec.</summary><param name="scenes">Strict authoring codec.</param>
    public SceneWorldCodec(SceneFileCodec scenes) { ArgumentNullException.ThrowIfNull(scenes); _scenes = scenes; }
    /// <summary>Encodes complete validated data into a new owned byte array; checksum is integrity, not authentication.</summary>
    /// <param name="data">Versioned scene payload.</param><param name="cancellationToken">Preparation cancellation.</param><returns>Independent bounded bytes.</returns>
    public byte[] Encode(SceneWorldData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data); cancellationToken.ThrowIfCancellationRequested();
        if (data.FormatVersion != SceneWorldData.CurrentFormatVersion) throw new InvalidDataException("Unsupported World scene format.");
        ValidateStrings(data.Scene, cancellationToken);
        var body = _scenes.Encode(data.Scene, cancellationToken);
        var result = new byte[HeaderBytes + body.Length];
        "TMWL"u8.CopyTo(result); BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), data.FormatVersion);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), body.Length);
        SHA256.HashData(body, result.AsSpan(12, 32)); body.CopyTo(result, HeaderBytes);
        cancellationToken.ThrowIfCancellationRequested(); return result;
    }
    /// <summary>Rejects corrupt/unknown/trailing input before returning a complete immutable document.</summary>
    /// <param name="bytes">Borrowed envelope, unchanged by decoding.</param><param name="cancellationToken">Preparation cancellation.</param><returns>Complete neutral data.</returns>
    public SceneWorldData Decode(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length < HeaderBytes || bytes.Length > MaximumBytes || !bytes.Span[..4].SequenceEqual("TMWL"u8))
            throw new InvalidDataException("Invalid World scene envelope.");
        if (BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]) != SceneWorldData.CurrentFormatVersion)
            throw new InvalidDataException("Unsupported World scene format.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[8..]);
        if (length < 0 || length > SceneFileCodec.MaximumBytes || length != bytes.Length - HeaderBytes)
            throw new InvalidDataException("World scene envelope length mismatch.");
        Span<byte> hash = stackalloc byte[32]; SHA256.HashData(bytes.Span[HeaderBytes..], hash);
        if (!CryptographicOperations.FixedTimeEquals(hash, bytes.Span.Slice(12, 32))) throw new InvalidDataException("World scene checksum mismatch.");
        var document = _scenes.Decode(bytes[HeaderBytes..], cancellationToken);
        cancellationToken.ThrowIfCancellationRequested(); return new(document);
    }

    internal static void ValidateStrings(SceneDocument scene, CancellationToken token)
    {
        void Check(PropertyValue value)
        {
            token.ThrowIfCancellationRequested();
            if (value.Kind != PropertyValueKind.String) return;
            try { _ = StrictUtf8.GetByteCount(value.GetString()); }
            catch (EncoderFallbackException error) { throw new InvalidDataException("World scene strings require well-formed UTF16 for lossless UTF8 encoding.", error); }
        }
        foreach (var schema in scene.Schemas)
        {
            token.ThrowIfCancellationRequested();
            foreach (var property in schema.Properties.Values) Check(property.DefaultValue);
        }
        foreach (var patch in scene.Definitions.Select(d => d.Changes).Concat(scene.Entities.Select(e => e.Changes)))
        {
            token.ThrowIfCancellationRequested();
            foreach (var component in patch.AddedComponents) foreach (var value in component.Properties.Values) Check(value);
            foreach (var value in patch.PropertyOverrides) Check(value.Value);
        }
    }
}
