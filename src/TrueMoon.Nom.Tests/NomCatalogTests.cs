using System.Buffers.Binary;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomCatalogTests
{
    [Theory]
    [InlineData("header-missing")][InlineData("control-missing")][InlineData("control-next-zero")]
    [InlineData("header-project")][InlineData("header-generation")][InlineData("asset-key")]
    [InlineData("binding-missing")][InlineData("binding-target")][InlineData("binding-key")]
    [InlineData("head-id")][InlineData("head-revision")][InlineData("head-metadata")][InlineData("revision-missing")][InlineData("revision-key")]
    [InlineData("operation-key")][InlineData("operation-result")][InlineData("operation-request")]
    [InlineData("pending-future")][InlineData("declared-total")]
    [InlineData("item-missing")][InlineData("item-owner")][InlineData("item-index")][InlineData("item-revision")][InlineData("unsupported-space")]
    public async Task CatalogOpen_AuthenticatedLogicalCorruption_FailsBeforeTailRepairOrStagingCleanup(string corruption)
    {
        await using var context = new NomCatalogTestContext(); await context.SeedAsync(); context.CorruptAndAppendTail(corruption);
        var args = context.Arguments; var snapshot = NomKvTestDirectory.Snapshot(args.Root);
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(new(args.Root, args.ProjectId, args.StoreId, false)));
        Assert.Equal(0, context.Factory.Count); NomKvTestDirectory.AssertReleased(args.Root); NomKvTestDirectory.AssertSnapshot(snapshot, args.Root);
        Assert.DoesNotContain(context.Io.Events, entry => entry.Point is "truncate" or "delete-file" or "delete-directory" or "write-journal" or "flush-journal");
        Assert.All(context.Io.OpenedCatalogFiles, file => Assert.True(file.Handle.IsClosed));
    }

    [Fact]
    public void RecordCodecs_IndependentHeaderControlAndHeadGoldens_PinBinaryFields()
    {
        var header = new CatalogHeader(NomFormatFixtures.ProjectId, NomFormatFixtures.StoreId, 3);
        var headerBytes = Convert.FromHexString("4e4f4d434154303100112233445566778899aabbccddeeff102132435465768798a9bacbdcedfe0f0300000000000000");
        Assert.Equal(headerBytes, NomCatalogRecordCodec.EncodeHeader(header)); Assert.Equal(header, NomCatalogRecordCodec.DecodeHeader(headerBytes));
        var controlBytes = Convert.FromHexString("4e4f4d43544c303102000000000000000100000000000000");
        Assert.Equal(controlBytes, NomCatalogRecordCodec.EncodeControl(new(2, 1))); Assert.Equal(new CatalogResourceControl(2, 1), NomCatalogRecordCodec.DecodeControl(controlBytes));
        var head = NomCatalogRecordCodec.DecodeHead(Convert.FromHexString("4e4f4d484544303100112233445566778899aabbccddeeff" + new string('a', 64)));
        Assert.Equal(NomFormatFixtures.ProjectId, head.Asset); Assert.Equal(new string('a', 64), head.Revision);
        Assert.Equal(Convert.FromHexString("4e4f4d424e44303100112233445566778899aabbccddeeff"), NomCatalogRecordCodec.EncodeBinding(NomFormatFixtures.ProjectId));
    }

    [Theory]
    [InlineData("pending", null)][InlineData("aborted", null)][InlineData("committed", 7L)]
    public void OperationCodec_StatesAndResult_AreCanonical(string state, long? result)
    {
        var operation = new CatalogOperation(9, new string('b', 64), 6, 1024, 2, state, result);
        var bytes = NomCatalogRecordCodec.EncodeOperation(operation); var decoded = NomCatalogRecordCodec.DecodeOperation(bytes);
        Assert.Equal(operation, decoded); Assert.Equal(80, bytes.Length);
        Assert.Equal(state == "pending" ? 0 : state == "committed" ? 1 : 2, bytes[68]);
        Assert.Equal(new byte[] { 0, 0, 0 }, bytes[69..72]); Assert.Equal(result ?? 0, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(72)));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ItemAndRevisionCodecs_OptionalFieldsAndExactResourceMetadata_ArePreserved(bool present)
    {
        var asset = NomFormatFixtures.MetadataGolden;
        var item = new CatalogOperationItem(7, 2, asset, 9, present ? 3 : null,
            present ? NomFormatFixtures.ContentDigest : null, present ? NomFormatFixtures.ResourceRevision : null);
        var bytes = NomCatalogRecordCodec.EncodeItem(item); var decoded = NomCatalogRecordCodec.DecodeItem(bytes);
        Assert.Equal(7, decoded.Sequence); Assert.Equal(2, decoded.Index); Assert.Equal(9, decoded.Maximum); Assert.Equal(asset, decoded.Asset);
        Assert.Equal(item.ExpectedLength, decoded.ExpectedLength); Assert.Equal(item.ExpectedDigest, decoded.ExpectedDigest); Assert.Equal(item.Revision, decoded.Revision);
        Assert.Equal(present ? 1 : 0, bytes[28]); Assert.Equal(present ? 1 : 0, bytes[37]); Assert.Equal(present ? 1 : 0, bytes[70]);
        var version = new AssetResourceVersion(NomFormatFixtures.Asset.Id, NomFormatFixtures.Asset.Kind, NomFormatFixtures.ResourceRevision, NomFormatFixtures.ContentDigest, 3);
        var resource = NomCatalogRecordCodec.DecodeRevision(NomCatalogRecordCodec.EncodeRevision(new(version, asset)));
        Assert.Equal(version, resource.Version); Assert.Equal(asset, resource.Asset); resource.Asset[0] = 0; Assert.Equal((byte)'N', asset[0]);
    }

    [Theory]
    [InlineData("magic")][InlineData("truncated")][InlineData("trailing")]
    [InlineData("state")][InlineData("reserved")][InlineData("count-zero")][InlineData("count-above-limit")]
    [InlineData("sequence-zero")][InlineData("negative-counter")][InlineData("result")]
    public void OperationCodec_RejectsMalformedSchemaAndFraming(string corruption)
    {
        var bytes = NomCatalogRecordCodec.EncodeOperation(new(1, new string('a', 64), 0, 1024, 1, "pending", null));
        switch (corruption)
        {
            case "magic": bytes[0] = 0; break; case "truncated": bytes = bytes[..^1]; break; case "trailing": bytes = NomFormatFixtures.Join(bytes, [0]); break;
            case "state": bytes[68] = 3; break; case "reserved": bytes[69] = 1; break;
            case "count-zero": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 0); break;
            case "count-above-limit": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(64), 257); break;
            case "sequence-zero": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), 0); break;
            case "negative-counter": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(48), -1); break;
            case "result": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(72), 1); break;
        }
        Assert.Throws<InvalidDataException>(() => NomCatalogRecordCodec.DecodeOperation(bytes));
    }

    [Theory]
    [InlineData("length-flag")][InlineData("digest-flag")][InlineData("revision-flag")]
    [InlineData("absent-length")][InlineData("absent-digest")][InlineData("absent-revision")]
    [InlineData("index")][InlineData("maximum")][InlineData("embedded-length")][InlineData("asset-schema")]
    public void ItemCodec_RejectsInvalidFlagsAbsentValuesAndEmbeddedAsset(string corruption)
    {
        var bytes = NomCatalogRecordCodec.EncodeItem(new(1, 0, NomFormatFixtures.MetadataGolden, 10, null, null, null));
        switch (corruption)
        {
            case "length-flag": bytes[28] = 2; break; case "digest-flag": bytes[37] = 2; break; case "revision-flag": bytes[70] = 2; break;
            case "absent-length": bytes[29] = 1; break; case "absent-digest": bytes[38] = 1; break; case "absent-revision": bytes[71] = 1; break;
            case "index": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 256); break;
            case "maximum": BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(20), 0); break;
            case "embedded-length": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(103), uint.MaxValue); break;
            case "asset-schema": bytes[107] = 0; break;
        }
        Assert.Throws<InvalidDataException>(() => NomCatalogRecordCodec.DecodeItem(bytes));
    }

    [Fact]
    public void CatalogTransaction_BindingConflictRollsBackEntireOverlayThenAllowsAtomicRetry()
    {
        using var context = new NomKvTestContext(); var args = context.Directory.Arguments(); using var store = context.Factory.Create(args);
        var catalog = new NomCatalog(store); catalog.Create(args.ProjectId, args.StoreId); var asset = NomFormatFixtures.Asset;
        var duplicate = new AssetRecord(Guid.Parse("9c6de934-8900-4912-b2a8-889dd8967864"), asset.Source, asset.Kind, asset.Locator, [9]);
        catalog.Begin(); catalog.SetHeader(new(args.ProjectId, args.StoreId, 1)); catalog.PutAsset(asset);
        Assert.Throws<InvalidOperationException>(catalog.Begin); Assert.Throws<IOException>(() => catalog.PutAsset(duplicate)); catalog.Rollback();
        Assert.Equal(1, store.Sequence); Assert.Equal(0, store.Generation); Assert.Equal(0, catalog.Header.Generation);
        Assert.Null(catalog.Asset(asset.Id)); Assert.Null(catalog.Asset(duplicate.Id)); Assert.Empty(catalog.Assets()); Assert.Throws<InvalidOperationException>(catalog.Commit);
        catalog.Begin(); catalog.SetHeader(new(args.ProjectId, args.StoreId, 1)); catalog.PutAsset(asset); catalog.Commit(); catalog.ValidateAll();
        Assert.Equal(2, store.Sequence); Assert.Equal(1, store.Generation); Assert.Equal(asset.Id, Assert.Single(catalog.Assets()).Id);
        Assert.Equal(asset.Metadata, catalog.Asset(asset.Id)!.Metadata); Assert.Null(catalog.Asset(duplicate.Id));
    }
}
