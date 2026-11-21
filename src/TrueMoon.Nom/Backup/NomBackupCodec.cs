using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Backup;

internal sealed record NomBackupEntry(string Path,long Length,string Digest);
internal sealed record NomBackupManifest(Guid ProjectId,Guid StoreId,long Sequence,long Generation,NomBackupEntry[] Entries);

internal static class NomBackupCodec
{
    internal const int MaximumManifestBytes=32*1024*1024;
    internal static void ValidatePath(string path)
    {
        NomBinary.Require(path != null && path.Length is > 0 and <= 160 && path.All(c => c <= 127),"Backup path budget/encoding.");
        var parts=path.Split('/');
        static bool Hex(string value,int length) => value.Length==length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        var valid=path=="nom.root" ||
            parts.Length==2 && parts[0]=="journal" && parts[1].Length==24 && parts[1].EndsWith(".nom",StringComparison.Ordinal) && parts[1][..20].All(c => c is >= '0' and <= '9') ||
            parts.Length==2 && parts[0]=="checkpoints" && parts[1].Length==68 && parts[1].EndsWith(".nom",StringComparison.Ordinal) && Hex(parts[1][..64],64) ||
            parts.Length==4 && parts[0]=="objects" && parts[1]=="sha256" && Hex(parts[2],2) && parts[3].Length==69 &&
            parts[3].EndsWith(".blob",StringComparison.Ordinal) && Hex(parts[3][..64],64) && parts[3].StartsWith(parts[2],StringComparison.Ordinal);
        NomBinary.Require(valid,"Unsupported/noncanonical backup path.");
    }
    internal static int Length(IEnumerable<NomBackupEntry> entries) => checked(160+entries.Sum(e => 44+CatalogCodec.Utf8.GetByteCount(e.Path)));
    internal static byte[] Encode(NomBackupManifest manifest)
    {
        NomBinary.Require(manifest.ProjectId!=Guid.Empty && manifest.StoreId!=Guid.Empty && manifest.Sequence>0 && manifest.Generation>=0 && manifest.Generation<=manifest.Sequence &&
            manifest.Entries.Length is > 0 and <= 100256,"Backup manifest identity/counters/count.");
        var size=Length(manifest.Entries); NomBinary.Require(size<=MaximumManifestBytes,"Backup manifest byte budget.");
        var bytes=new byte[size]; "NOMBK001"u8.CopyTo(bytes); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8),1); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10),128);
        NomBinary.WriteGuid(bytes.AsSpan(16),manifest.ProjectId); NomBinary.WriteGuid(bytes.AsSpan(32),manifest.StoreId);
        NomBinary.WriteCounter(bytes.AsSpan(48),manifest.Sequence); NomBinary.WriteCounter(bytes.AsSpan(56),manifest.Generation);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64),(uint)manifest.Entries.Length); SHA256.HashData(bytes.AsSpan(0,96)).CopyTo(bytes,96);
        var offset=128; string? previous=null;
        foreach(var entry in manifest.Entries)
        {
            ValidatePath(entry.Path); NomBinary.Require(previous==null || StringComparer.Ordinal.Compare(previous,entry.Path)<0,"Backup path order/duplicates."); previous=entry.Path;
            NomBinary.Require(entry.Length is >= 0 and <= 1024L*1024*1024*1024,"Backup file length budget."); var path=CatalogCodec.Utf8.GetBytes(entry.Path);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset),(ushort)path.Length); NomBinary.WriteCounter(bytes.AsSpan(offset+4),entry.Length);
            NomBinary.DigestBytes(entry.Digest).CopyTo(bytes,offset+12); path.CopyTo(bytes,offset+44); offset+=44+path.Length;
        }
        NomBinary.Require(manifest.Entries.Any(e => e.Path=="nom.root") && manifest.Entries.Any(e => e.Path.StartsWith("journal/",StringComparison.Ordinal)),"Missing backup authority files.");
        SHA256.HashData(bytes.AsSpan(0,offset)).CopyTo(bytes,offset); return bytes;
    }
    internal static NomBackupManifest Decode(byte[] bytes)
    {
        NomBinary.Require(bytes.Length is >= 160 and <= MaximumManifestBytes && bytes.AsSpan(0,8).SequenceEqual("NOMBK001"u8) &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8))==1 && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10))==128,"Unsupported backup format.");
        NomBinary.RequireZero(bytes.AsSpan(12,4)); NomBinary.RequireZero(bytes.AsSpan(68,28));
        NomBinary.Require(CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0,96)),bytes.AsSpan(96,32)) &&
            CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes.AsSpan(0,bytes.Length-32)),bytes.AsSpan(bytes.Length-32)),"Backup checksum.");
        var count=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(64)); NomBinary.Require(count is > 0 and <= 100256 && count<=(bytes.Length-160)/45,"Backup entry count/framing.");
        var entries=new NomBackupEntry[(int)count]; var offset=128;
        for(var n=0;n<entries.Length;n++)
        {
            NomBinary.Require(bytes.Length-32-offset>=44,"Truncated backup entry."); var length=BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
            NomBinary.Require(length is > 0 and <= 160 && bytes.Length-32-offset-44>=length,"Backup path framing."); NomBinary.RequireZero(bytes.AsSpan(offset+2,2));
            string path; try { path=CatalogCodec.Utf8.GetString(bytes,offset+44,length); } catch(DecoderFallbackException error) { throw new InvalidDataException("Invalid backup UTF8.",error); }
            entries[n]=new(path,NomBinary.ReadCounter(bytes.AsSpan(offset+4)),Convert.ToHexStringLower(bytes.AsSpan(offset+12,32))); offset+=44+length;
        }
        NomBinary.Require(offset==bytes.Length-32,"Trailing backup data.");
        var result=new NomBackupManifest(NomBinary.ReadGuid(bytes.AsSpan(16)),NomBinary.ReadGuid(bytes.AsSpan(32)),NomBinary.ReadCounter(bytes.AsSpan(48)),NomBinary.ReadCounter(bytes.AsSpan(56)),entries);
        NomBinary.Require(Encode(result).AsSpan().SequenceEqual(bytes),"Noncanonical backup manifest."); return result;
    }
}
