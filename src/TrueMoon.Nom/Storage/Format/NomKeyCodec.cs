using System.Buffers.Binary;

namespace TrueMoon.Nom.Storage.Format;

internal enum NomKeySpace : byte
{
    Control = 1, Asset = 16, Head = 17, Revision = 18, Binding = 19,
    Tombstone = 20, Operation = 32, OperationItem = 33, Manifest = 48, Member = 49, Pin = 50,
    Garbage = 51, Dependency = 64, ReverseDependency = 65
}

internal static class NomKeyCodec
{
    internal static byte[] Encode(NomKeySpace space, params byte[][] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        NomBinary.Require(Enum.IsDefined(space) && fields.Length is >= 1 and <= 8, "Invalid Nom key shape.");
        long size = 4;
        foreach (var field in fields)
        {
            NomBinary.Require(field is { Length: > 0 and <= 1024 }, "Invalid Nom key field."); size += 4L + field.Length;
        }
        NomBinary.Require(size <= 1024, "Nom key budget.");
        var bytes = new byte[(int)size]; bytes[0] = 1; bytes[1] = (byte)space;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)fields.Length); var offset = 4;
        foreach (var field in fields)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), field.Length); offset += 4;
            field.CopyTo(bytes, offset); offset += field.Length;
        }
        return bytes;
    }
    internal static void Validate(ReadOnlySpan<byte> bytes)
    {
        NomBinary.Require(bytes.Length is >= 4 and <= 1024 && bytes[0] == 1 && Enum.IsDefined((NomKeySpace)bytes[1]), "Unsupported Nom key format.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]); NomBinary.Require(count is >= 1 and <= 8, "Nom key field count.");
        var offset = 4;
        for (var i = 0; i < count; i++)
        {
            NomBinary.Require(bytes.Length - offset >= 4, "Truncated Nom key field.");
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]); offset += 4;
            NomBinary.Require(length > 0 && length <= bytes.Length - offset, "Nom key field length."); offset += (int)length;
        }
        NomBinary.Require(offset == bytes.Length, "Trailing Nom key data.");
    }
}
