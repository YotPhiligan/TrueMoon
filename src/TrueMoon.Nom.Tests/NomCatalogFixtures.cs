using System.Buffers.Binary;
using System.Globalization;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

internal sealed class NomCatalogTestContext : IAsyncDisposable
{
    internal readonly NomTestDirectory Directory = new();
    internal readonly NomFaultIo Io = new();
    internal readonly CatalogFactory Factory;
    internal readonly OpenAssetStoreArguments Arguments;
    internal readonly AssetRecord Asset = NomFormatFixtures.Asset;
    internal readonly byte[] Payload = [1, 2, 3];
    internal AssetWriteTicket Ticket = null!;
    internal AssetResourceVersion Version = null!;
    internal NomCatalogTestContext() { Arguments = Directory.Arguments(); Factory = new(4, Io); }
    internal async Task SeedAsync()
    {
        await using var owner = Factory.Create(new(Arguments.Root, Arguments.ProjectId, Arguments.StoreId, true));
        Ticket = await owner.ReserveResourcesAsync(0, [new AssetResourceWrite(Asset, Payload.Length, NomTestDirectory.Digest(Payload), Payload.Length)]);
        using var input = new MemoryStream(Payload, writable: false);
        Version = AssertSingle(await owner.PublishResourcesAsync(Ticket, new Dictionary<Guid, Stream> { [Asset.Id] = input }));
        static AssetResourceVersion AssertSingle(AssetWriteReceipt receipt) => Xunit.Assert.Single(receipt.Resources);
    }
    internal void CorruptAndAppendTail(string corruption)
    {
        using var rawFactory = new NomKvStoreFactory();
        long sequence, generation;
        using (var raw = rawFactory.Open(new(Arguments.Root, Arguments.ProjectId, Arguments.StoreId)))
        {
            var assetBytes = CatalogCodec.Encode(Asset); var control = NomCatalogRecordCodec.DecodeControl(raw.Get(NomCatalogKeys.Control)!);
            var operation = NomCatalogRecordCodec.DecodeOperation(raw.Get(NomCatalogKeys.Operation(Ticket.Sequence))!);
            var item = NomCatalogRecordCodec.DecodeItem(raw.Get(NomCatalogKeys.Item(Ticket.Sequence, 0))!);
            var other = Guid.Parse("4662daa7-ac10-4448-8466-f290dc8d4f8d"); var missing = new string('a', 64);
            byte[]? value; byte[] key; var additional = new List<NomJournalMutation>();
            switch (corruption)
            {
                case "header-missing": key = NomCatalogKeys.Header; value = null; break;
                case "control-missing": key = NomCatalogKeys.Control; value = null; break;
                case "control-next-zero": key = NomCatalogKeys.Control; value = NomCatalogRecordCodec.EncodeControl(control); BinaryPrimitives.WriteInt64LittleEndian(value.AsSpan(8), 0); break;
                case "header-project": key = NomCatalogKeys.Header; value = NomCatalogRecordCodec.EncodeHeader(new(other, Arguments.StoreId, 1)); break;
                case "header-generation": key = NomCatalogKeys.Header; value = NomCatalogRecordCodec.EncodeHeader(new(Arguments.ProjectId, Arguments.StoreId, 0)); break;
                case "asset-key": key = NomCatalogKeys.Asset(other); value = assetBytes; break;
                case "binding-missing": key = NomCatalogKeys.Binding(CatalogCodec.Binding(Asset)); value = null; break;
                case "binding-target": key = NomCatalogKeys.Binding(CatalogCodec.Binding(Asset)); value = NomCatalogRecordCodec.EncodeBinding(other); break;
                case "binding-key": key = NomCatalogKeys.Binding(missing); value = NomCatalogRecordCodec.EncodeBinding(Asset.Id); break;
                case "head-id": key = NomCatalogKeys.Head(Asset.Id); value = NomCatalogRecordCodec.EncodeHead(other, Version.Revision); break;
                case "head-revision": key = NomCatalogKeys.Head(Asset.Id); value = NomCatalogRecordCodec.EncodeHead(Asset.Id, missing); break;
                case "head-metadata": key = NomCatalogKeys.Asset(Asset.Id); value = CatalogCodec.Encode(new(Asset.Id, Asset.Source, Asset.Kind, Asset.Locator, [99])); break;
                case "revision-missing": key = NomCatalogKeys.Revision(Version.Revision); value = null; break;
                case "revision-key": key = NomCatalogKeys.Revision(missing); value = NomCatalogRecordCodec.EncodeRevision(new(Version, assetBytes)); break;
                case "operation-key": key = NomCatalogKeys.Operation(Ticket.Sequence + 1); value = NomCatalogRecordCodec.EncodeOperation(operation); break;
                case "operation-result": key = NomCatalogKeys.Operation(Ticket.Sequence); value = NomCatalogRecordCodec.EncodeOperation(operation); BinaryPrimitives.WriteInt64LittleEndian(value.AsSpan(value.Length - 8), 99); break;
                case "operation-request": key = NomCatalogKeys.Operation(Ticket.Sequence); value = NomCatalogRecordCodec.EncodeOperation(operation with { Request = missing }); break;
                case "pending-future":
                case "declared-total":
                    var pendingItem = item with { Revision = null };
                    var pending = operation with { State = "pending", Result = null,
                        Expected = corruption == "pending-future" ? raw.Generation + 1 : operation.Expected,
                        Total = corruption == "declared-total" ? 2 : operation.Total };
                    pending = pending with { Request = RequestHash(pending, pendingItem) };
                    key = NomCatalogKeys.Operation(Ticket.Sequence); value = NomCatalogRecordCodec.EncodeOperation(pending);
                    additional.Add(new(NomCatalogKeys.Item(Ticket.Sequence, 0), NomCatalogRecordCodec.EncodeItem(pendingItem))); break;
                case "item-missing": key = NomCatalogKeys.Item(Ticket.Sequence, 0); value = null; break;
                case "item-owner": key = NomCatalogKeys.Item(999, 0); value = NomCatalogRecordCodec.EncodeItem(item with { Sequence = 999 }); break;
                case "item-index": key = NomCatalogKeys.Item(Ticket.Sequence, 1); value = NomCatalogRecordCodec.EncodeItem(item with { Index = 1 }); break;
                case "item-revision": key = NomCatalogKeys.Item(Ticket.Sequence, 0); value = NomCatalogRecordCodec.EncodeItem(item with { Revision = null }); break;
                case "unsupported-space": key = NomKeyCodec.Encode(NomKeySpace.Manifest, [1]); value = [1]; break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            raw.WriteBatch(new(raw.Sequence + 1, raw.Generation, raw.Generation, NomFormatFixtures.OperationId, [new(key, value), .. additional]));
            sequence = raw.Sequence; generation = raw.Generation;
        }
        var stage = Path.Combine(Arguments.Root, "staging", Ticket.Sequence.ToString("x16", CultureInfo.InvariantCulture));
        System.IO.Directory.CreateDirectory(stage); File.WriteAllBytes(Path.Combine(stage, "0000.tmp"), [77, 88, 99]);
        var path = NomKvTestDirectory.Journal(Arguments.Root); var bytes = File.ReadAllBytes(path);
        var header = NomJournalCodec.DecodeHeader(bytes.AsSpan(0, NomJournalCodec.HeaderBytes));
        var frame = NomJournalCodec.EncodeBatch(header, new(sequence + 1, generation, generation, NomFormatFixtures.OperationId,
            [new NomJournalMutation([0xFE], [1])]));
        using var append = new FileStream(path, FileMode.Append); append.Write(frame.AsSpan(0, 100));
        Io.Events.Clear();
    }
    private string RequestHash(CatalogOperation operation, CatalogOperationItem item) => CatalogCodec.Key(
        "TrueMoon.Nom/request/1", Arguments.StoreId.ToString("N"), operation.Expected.ToString(CultureInfo.InvariantCulture),
        operation.Total.ToString(CultureInfo.InvariantCulture), ResourceCodec.Hash(item.Asset), item.Maximum.ToString(CultureInfo.InvariantCulture),
        item.ExpectedLength?.ToString(CultureInfo.InvariantCulture) ?? "unknown", item.ExpectedDigest ?? "unknown");
    public async ValueTask DisposeAsync() { Io.Disarm(); await Factory.DisposeAsync(); Directory.Dispose(); }
}
