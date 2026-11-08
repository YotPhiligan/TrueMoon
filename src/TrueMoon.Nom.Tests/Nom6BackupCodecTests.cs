using System.Buffers.Binary;
using System.Security.Cryptography;
using Xunit;
using TrueMoon.Nom.Backup;

namespace TrueMoon.Nom.Tests;

public sealed class Nom6BackupCodecTests
{
    // Hand-specified RFC GUID / LE counters / ordinal paths; SHA256 computed independently of Nom codecs.
    private const string GoldenHex = "4e4f4d424b303031010080000000000000112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f070000000000000003000000000000000200000000000000000000000000000000000000000000000000000000000000156338d92b68dadc84fd5499af913de2477a1dec7fd385ddd6a48c930006ccea20000000a000000000000000000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f6a6f75726e616c2f30303030303030303030303030303030303030312e6e6f6d080000000001000000000000aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa6e6f6d2e726f6f74ba7223d52148e2da5771f99ca2e1c2fb17b6aef5c7692af7aa8e62b5da8f98ba";
    private static NomBackupManifest Manifest => new(NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, 7, 3,
        [new("journal/00000000000000000001.nom",160,NomCheckpointFixtures.PreviousDigest),new("nom.root",256,new string('a',64))]);
    private static void Authenticate(byte[] bytes)
    { SHA256.HashData(bytes.AsSpan(0,96)).CopyTo(bytes,96); SHA256.HashData(bytes.AsSpan(0,bytes.Length-32)).CopyTo(bytes,bytes.Length-32); }

    [Fact]
    public void BackupManifest_IndependentGoldenPinsWireOrderHeaderAndFooterDigests()
    {
        var bytes=Convert.FromHexString(GoldenHex); Assert.Equal(288,bytes.Length); Assert.Equal(bytes,NomBackupCodec.Encode(Manifest));
        var read=NomBackupCodec.Decode(bytes); Assert.Equal(Manifest.ProjectId,read.ProjectId); Assert.Equal(Manifest.StoreId,read.StoreId);
        Assert.Equal(7,read.Sequence); Assert.Equal(3,read.Generation); Assert.Equal(Manifest.Entries,read.Entries);
    }

    [Fact]
    public void BackupManifest_EveryPrefixSingleByteMutationAndTrailingByteRejects()
    {
        var bytes=Convert.FromHexString(GoldenHex);
        for(var n=0;n<bytes.Length;n++) Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Decode(bytes[..n]));
        for(var n=0;n<bytes.Length;n++) { var changed=bytes.ToArray();changed[n]^=1;Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Decode(changed)); }
        Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Decode([..bytes,0]));
    }

    [Theory]
    [InlineData("version")][InlineData("header-size")][InlineData("flags")][InlineData("reserved")][InlineData("empty-project")]
    [InlineData("empty-store")][InlineData("zero-sequence")][InlineData("counter-overflow")][InlineData("future-generation")]
    [InlineData("zero-count")][InlineData("count-over")][InlineData("path-length")][InlineData("row-reserved")][InlineData("length-over")][InlineData("path-utf8")]
    public void BackupManifest_AuthenticatedInvalidHeaderOrEntryRejectsBeforeNormalization(string invalid)
    {
        var bytes=Convert.FromHexString(GoldenHex);
        switch(invalid)
        {
            case "version": bytes[8]=2; break; case "header-size": bytes[10]=127; break; case "flags": bytes[12]=1; break;
            case "reserved": bytes[68]=1; break; case "empty-project": bytes.AsSpan(16,16).Clear(); break; case "empty-store": bytes.AsSpan(32,16).Clear(); break;
            case "zero-sequence": bytes.AsSpan(48,8).Clear(); break; case "counter-overflow": bytes[55]=128; break;
            case "future-generation": BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(56),8); break;
            case "zero-count": bytes.AsSpan(64,4).Clear(); break; case "count-over": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64),100257); break;
            case "path-length": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(128),161); break; case "row-reserved": bytes[130]=1; break;
            case "length-over": BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(132),1024UL*1024*1024*1024+1); break;
            case "path-utf8": bytes[172]=0xff; break;
        }
        Authenticate(bytes); Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Decode(bytes));
    }

    [Theory]
    [InlineData("../nom.root")][InlineData("/nom.root")][InlineData("C:/nom.root")][InlineData("journal\\00000000000000000001.nom")]
    [InlineData("owner.lock")][InlineData("nom.backup")][InlineData("NOM.ROOT")][InlineData("journal/1.nom")][InlineData("journal/00000000000000000001.pending")]
    [InlineData("objects/sha256/aa/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.blob")]
    public void BackupManifest_UnsupportedEscapingOrNoncanonicalPathsReject(string path)
    {
        var manifest=Manifest with { Entries=[new(path,0,new string('a',64)),..Manifest.Entries] };
        Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Encode(manifest)); Assert.Throws<InvalidDataException>(()=>NomBackupCodec.ValidatePath(path));
    }

    [Theory]
    [InlineData("duplicate")][InlineData("order")][InlineData("root-missing")][InlineData("journal-missing")]
    public void BackupManifest_NoncanonicalOrderDuplicatesAndMissingAuthorityReject(string invalid)
    {
        var rows=Manifest.Entries; var entries=invalid switch { "duplicate"=>new[]{rows[0],rows[0],rows[1]},"order"=>new[]{rows[1],rows[0]},"root-missing"=>new[]{rows[0]},_=>new[]{rows[1]} };
        Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Encode(Manifest with { Entries=entries }));
    }

    [Fact]
    public void BackupOptions_ExactMaximumAcceptedAndImmediatelyAdjacentBoundsReject()
    {
        var maximum=new NomBackupOptions(100256,1024L*1024*1024*1024); Assert.Equal(maximum,maximum.Snapshot()); Assert.NotSame(maximum,maximum.Snapshot());
        Assert.Equal(new NomBackupOptions(1,256),new NomBackupOptions(1,256).Snapshot());
        foreach(var invalid in new[]{new NomBackupOptions(0),new NomBackupOptions(100257),new NomBackupOptions(MaximumBytes:255),new NomBackupOptions(MaximumBytes:1024L*1024*1024*1024+1)})
            Assert.Throws<ArgumentOutOfRangeException>(()=>invalid.Snapshot());
        Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Decode(new byte[NomBackupCodec.MaximumManifestBytes+1]));
    }

    [Fact]
    public void BackupManifest_EntryCountAccepts100256AndRejects100257WithoutTruncatingSelection()
    {
        var entries=Enumerable.Range(1,100255).Select(n=>new NomBackupEntry("journal/"+n.ToString("D20")+".nom",160,new string('a',64)))
            .Append(new("nom.root",256,new string('b',64))).ToArray();
        var manifest=Manifest with { Entries=entries }; var encoded=NomBackupCodec.Encode(manifest); var decoded=NomBackupCodec.Decode(encoded);
        Assert.Equal(100256,decoded.Entries.Length); Assert.Equal(entries[0],decoded.Entries[0]); Assert.Equal(entries[^1],decoded.Entries[^1]);
        Assert.Equal(entries,decoded.Entries); Assert.Equal(NomBackupCodec.Length(entries),encoded.Length);
        Assert.Throws<InvalidDataException>(()=>NomBackupCodec.Encode(manifest with { Entries=[..entries,new("nom.root",256,new string('b',64))] }));
    }
}
