using System.Security.Cryptography;
using System.Text;

namespace TrueMoon.Aether.Tooling.Importing;

internal static class ModelImportLedgerCodec
{
    internal const int MaximumBytes = 12 * 1024 * 1024;
    internal static byte[] Encode(PublishedModelImport record)
    {
        using var body = new MemoryStream(); using var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true);
        writer.Write(record.Generation); writer.Write(record.Ledger.SourceId.ToByteArray()); Text(writer,record.Candidate.InputRevision);
        var candidate = ModelImportProtocol.Encode(record.Candidate); writer.Write(candidate.Length); writer.Write(candidate);
        writer.Write(record.Ledger.Entries.Count);
        foreach (var entry in record.Ledger.Entries) { Key(writer,entry.Key); writer.Write(entry.Id.ToByteArray()); writer.Write((byte)(entry.Active ? 1 : 0)); }
        writer.Write(record.Bindings.Count);
        foreach (var binding in record.Bindings) { writer.Write((int)binding.Kind); writer.Write(binding.Index); Key(writer,binding.Key); writer.Write(binding.Id.ToByteArray()); }
        if (body.Length > MaximumBytes - 40) throw new InvalidDataException("Published model byte budget exceeded.");
        var payload = body.ToArray(); using var output = new MemoryStream(); using var envelope = new BinaryWriter(output);
        envelope.Write(0x314c4d54); envelope.Write(payload.Length); envelope.Write(SHA256.HashData(payload)); envelope.Write(payload);
        return output.ToArray();
    }
    internal static PublishedModelImport Decode(byte[] bytes, Guid sourceId)
    {
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Published model byte budget exceeded.");
        try
        {
            using var reader = new BinaryReader(new MemoryStream(bytes,writable:false),new UTF8Encoding(false,true));
            Require(reader.ReadInt32() == 0x314c4d54,"Unsupported model ledger envelope.");
            var length = Count(reader,MaximumBytes-40); var hash = Bytes(reader,32); var payload = Bytes(reader,length); End(reader);
            Require(CryptographicOperations.FixedTimeEquals(hash,SHA256.HashData(payload)),"Model ledger checksum mismatch.");
            using var data = new BinaryReader(new MemoryStream(payload,writable:false),new UTF8Encoding(false,true));
            var generation = data.ReadInt64(); Require(new Guid(Bytes(data,16)) == sourceId,"Changed model source identity.");
            var revision = Text(data,64); var candidate = ModelImportProtocol.Decode(Bytes(data,Count(data,ModelImportProtocol.MaximumOutputBytes)),sourceId,revision);
            var entries = new ModelSubassetEntry[Count(data,ModelSubassetLedger.MaximumEntries)];
            for (var i = 0; i < entries.Length; i++)
            {
                var key = Key(data); var id = new Guid(Bytes(data,16)); var active = data.ReadByte(); Require(active <= 1,"Invalid tombstone tag."); entries[i] = new(key,id,active==1);
            }
            var bindings = new ModelSubassetBinding[Count(data,ModelSubassetLedger.MaximumActive)];
            for (var i = 0; i < bindings.Length; i++) bindings[i] = new((ModelSubassetKind)data.ReadInt32(),data.ReadInt32(),Key(data),new Guid(Bytes(data,16)));
            End(data); var result = new PublishedModelImport(generation,candidate,new(sourceId,entries),bindings);
            Require(Encode(result).AsSpan().SequenceEqual(bytes),"Noncanonical model ledger encoding."); return result;
        }
        catch (EndOfStreamException error) { throw new InvalidDataException("Truncated model ledger.",error); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Invalid ledger UTF8.",error); }
    }
    private static ModelSubassetKey Key(BinaryReader reader) => new((ModelSubassetKind)reader.ReadInt32(),Text(reader,256));
    private static void Key(BinaryWriter writer,ModelSubassetKey key) { writer.Write((int)key.Kind); Text(writer,key.Locator); }
    private static void Text(BinaryWriter writer,string value) { var bytes=Encoding.UTF8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static string Text(BinaryReader reader,int maximum) => new UTF8Encoding(false,true).GetString(Bytes(reader,Count(reader,maximum)));
    private static int Count(BinaryReader reader,int maximum) { var count=reader.ReadInt32(); Require(count>=0 && count<=maximum,"Ledger count/length budget exceeded."); return count; }
    private static byte[] Bytes(BinaryReader reader,int count) { Require(count<=reader.BaseStream.Length-reader.BaseStream.Position,"Truncated model ledger."); return reader.ReadBytes(count); }
    private static void End(BinaryReader reader) => Require(reader.BaseStream.Position==reader.BaseStream.Length,"Trailing model ledger bytes.");
    private static void Require(bool condition,string message) => ModelImportProtocol.Require(condition,message);
}
