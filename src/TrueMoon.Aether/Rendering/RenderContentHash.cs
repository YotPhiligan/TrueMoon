using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TrueMoon.Aether.Rendering;

/// <summary>Unmanaged 32-byte SHA256 value; byte and text representations use standard digest order.</summary>
public readonly record struct RenderContentHash
{
    private readonly ulong _a, _b, _c, _d;
    private RenderContentHash(ReadOnlySpan<byte> bytes)
    {
        _a = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        _b = BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64BigEndian(bytes[16..]);
        _d = BinaryPrimitives.ReadUInt64BigEndian(bytes[24..]);
    }
    /// <summary>Gets whether this is the invalid default digest sentinel.</summary>
    public bool IsEmpty => (_a | _b | _c | _d) == 0;
    /// <summary>Hashes independently owned input bytes.</summary><param name="data">Canonical payload.</param><returns>SHA256 value.</returns>
    public static RenderContentHash Compute(ReadOnlySpan<byte> data) => FromBytes(SHA256.HashData(data));
    /// <summary>Copies exactly 32 digest bytes.</summary><param name="bytes">Standard-order digest.</param><returns>Unmanaged copy.</returns>
    public static RenderContentHash FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) throw new ArgumentException("A content hash requires 32 bytes.", nameof(bytes));
        return new(bytes);
    }
    /// <summary>Parses exactly 64 hexadecimal digits.</summary><param name="text">Case-insensitive digest text.</param><returns>Digest value.</returns>
    public static RenderContentHash Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length != 64) throw new FormatException("A content hash requires 64 hexadecimal digits.");
        return FromBytes(Convert.FromHexString(text));
    }
    /// <summary>Writes the digest without retaining the destination.</summary><param name="destination">At least 32 bytes.</param>
    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < 32) throw new ArgumentException("A content hash requires 32 bytes.", nameof(destination));
        BinaryPrimitives.WriteUInt64BigEndian(destination, _a);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], _b);
        BinaryPrimitives.WriteUInt64BigEndian(destination[16..], _c);
        BinaryPrimitives.WriteUInt64BigEndian(destination[24..], _d);
    }
    /// <summary>Returns a detached digest byte array.</summary><returns>New 32-byte array.</returns>
    public byte[] ToArray() { var result = new byte[32]; WriteBytes(result); return result; }
    /// <summary>Returns lowercase hexadecimal digest text.</summary><returns>Exactly 64 characters.</returns>
    public override string ToString() => Convert.ToHexString(ToArray()).ToLowerInvariant();
}
