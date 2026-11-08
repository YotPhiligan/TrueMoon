using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace TrueMoon.Nom.Storage.Format;

internal static class NomBinary
{
    internal const string ZeroDigest = "0000000000000000000000000000000000000000000000000000000000000000";
    internal static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    internal static long ReadCounter(ReadOnlySpan<byte> data)
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(data);
        Require(value <= long.MaxValue, "Nom counter overflow.");
        return (long)value;
    }
    internal static void WriteCounter(Span<byte> data, long value) => BinaryPrimitives.WriteInt64LittleEndian(data, value);
    internal static void WriteGuid(Span<byte> data, Guid value) => value.TryWriteBytes(data, bigEndian: true, out _);
    internal static Guid ReadGuid(ReadOnlySpan<byte> data) => new(data[..16], bigEndian: true);
    internal static byte[] DigestBytes(string value)
    {
        Require(value != null && value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Noncanonical Nom digest.");
        return Convert.FromHexString(value!);
    }
    internal static void VerifyHash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)
    {
        Span<byte> actual = stackalloc byte[32]; SHA256.HashData(data, actual);
        Require(CryptographicOperations.FixedTimeEquals(actual, expected), "Nom checksum mismatch.");
    }
    internal static void RequireZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) Require(value == 0, "Unsupported Nom reserved field.");
    }
}

internal sealed class NomKeyComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
{
    internal static readonly NomKeyComparer Instance = new();
    public bool Equals(byte[]? x, byte[]? y) => ReferenceEquals(x, y) || x != null && y != null && x.AsSpan().SequenceEqual(y);
    public int Compare(byte[]? x, byte[]? y) => x == null ? y == null ? 0 : -1 : y == null ? 1 : x.AsSpan().SequenceCompareTo(y);
    public int GetHashCode(byte[] value)
    {
        var hash = new HashCode(); hash.AddBytes(value); return hash.ToHashCode();
    }
}
