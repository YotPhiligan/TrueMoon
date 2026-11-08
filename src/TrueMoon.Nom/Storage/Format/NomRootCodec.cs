using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TrueMoon.Nom.Storage.Format;

internal static class NomRootCodec
{
    internal const int RootBytes = 256;
    internal static byte[] Encode(NomRootDescriptor root)
    {
        ArgumentNullException.ThrowIfNull(root);
        NomBinary.Require(root.ProjectId != Guid.Empty && root.StoreId != Guid.Empty && root.Epoch > 0 &&
            root.CheckpointSequence >= 0 && root.CheckpointGeneration >= 0 && root.CheckpointGeneration <= root.CheckpointSequence &&
            root.MinimumSequence >= root.CheckpointSequence && root.FirstSegmentId > 0 &&
            root.ActiveSegmentId >= root.FirstSegmentId && root.ActiveSegmentId - root.FirstSegmentId < 32, "Invalid Nom root counters/identity.");
        var checkpoint = NomBinary.DigestBytes(root.CheckpointDigest);
        var previous = NomBinary.DigestBytes(root.PreviousSegmentHash);
        NomBinary.Require(root.CheckpointSequence == 0
            ? root.CheckpointDigest == NomBinary.ZeroDigest && root.FirstSegmentId == 1 && root.PreviousSegmentHash == NomBinary.ZeroDigest
            : root.CheckpointDigest != NomBinary.ZeroDigest && root.FirstSegmentId > 1 && root.PreviousSegmentHash != NomBinary.ZeroDigest,
            "Invalid Nom checkpoint authority.");
        var bytes = new byte[RootBytes]; "NOMROOT1"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), RootBytes);
        NomBinary.WriteGuid(bytes.AsSpan(16), root.ProjectId); NomBinary.WriteGuid(bytes.AsSpan(32), root.StoreId);
        NomBinary.WriteCounter(bytes.AsSpan(48), root.Epoch); NomBinary.WriteCounter(bytes.AsSpan(56), root.CheckpointSequence);
        NomBinary.WriteCounter(bytes.AsSpan(64), root.CheckpointGeneration); checkpoint.CopyTo(bytes, 72);
        NomBinary.WriteCounter(bytes.AsSpan(104), root.FirstSegmentId); NomBinary.WriteCounter(bytes.AsSpan(112), root.ActiveSegmentId);
        NomBinary.WriteCounter(bytes.AsSpan(120), root.MinimumSequence); previous.CopyTo(bytes, 128);
        SHA256.HashData(bytes.AsSpan(0, 224), bytes.AsSpan(224)); return bytes;
    }
    internal static NomRootDescriptor Decode(ReadOnlySpan<byte> bytes, Guid projectId, Guid storeId)
    {
        NomBinary.Require(bytes.Length == RootBytes && bytes[..8].SequenceEqual("NOMROOT1"u8) &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]) == 1 && BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]) == RootBytes,
            "Unsupported Nom root format.");
        NomBinary.VerifyHash(bytes[..224], bytes[224..]); NomBinary.RequireZero(bytes.Slice(12, 4)); NomBinary.RequireZero(bytes.Slice(160, 64));
        var root = new NomRootDescriptor(NomBinary.ReadGuid(bytes[16..]), NomBinary.ReadGuid(bytes[32..]),
            NomBinary.ReadCounter(bytes[48..]), NomBinary.ReadCounter(bytes[56..]), NomBinary.ReadCounter(bytes[64..]),
            Convert.ToHexStringLower(bytes.Slice(72, 32)), NomBinary.ReadCounter(bytes[104..]), NomBinary.ReadCounter(bytes[112..]),
            NomBinary.ReadCounter(bytes[120..]), Convert.ToHexStringLower(bytes.Slice(128, 32)));
        NomBinary.Require(root.ProjectId == projectId && root.StoreId == storeId, "Nom root identity mismatch.");
        NomBinary.Require(Encode(root).AsSpan().SequenceEqual(bytes), "Noncanonical Nom root."); return root;
    }
}
