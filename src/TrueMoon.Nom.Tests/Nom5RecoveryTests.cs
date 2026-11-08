using System.Buffers.Binary;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5RecoveryTests
{
    [Theory]
    [InlineData("pin-target")][InlineData("member-missing")][InlineData("member-key")][InlineData("member-version")]
    [InlineData("manifest-count")][InlineData("gc-live")][InlineData("gc-key")][InlineData("gc-length")]
    [InlineData("tombstone-head")][InlineData("dangling-exact-dependency")]
    public async Task Open_AuthenticatedRetentionCorruptionRejectsBeforeTailRepairObjectDeleteOrStagingCleanup(string corruption)
    {
        var io = new NomCheckpointIo(); await using var context = new Nom5Context(io); var resource = await context.Publish(Nom5Context.Asset(1), [1, 2, 3]);
        var manifest = await context.Store.CreateSnapshotAsync([resource.Version]); var pin = Nom5Context.Id(700); await context.Store.PinSnapshotAsync(manifest.Id, pin);
        await context.Store.DisposeAsync(); long sequence; long generation;
        using (var factory = new NomKvStoreFactory())
        using (var raw = factory.Open(new(context.Arguments.Root, context.Arguments.ProjectId, context.Arguments.StoreId)))
        {
            var missing = new string('f', 64); byte[] key; byte[]? value;
            switch (corruption)
            {
                case "pin-target": key = NomCatalog.PinKey(pin); value = NomRetentionCodec.EncodeDigest("NOMPIN01", missing); break;
                case "member-missing": key = NomCatalog.MemberKey(manifest.Id, resource.Version.Revision); value = null; break;
                case "member-key": key = NomCatalog.MemberKey(manifest.Id, missing); value = NomRetentionCodec.EncodeMember(resource.Version); break;
                case "member-version": key = NomCatalog.MemberKey(manifest.Id, resource.Version.Revision); value = NomRetentionCodec.EncodeMember(resource.Version with { Length = 4 }); break;
                case "manifest-count": key = NomCatalog.ManifestKey(manifest.Id); value = NomRetentionCodec.EncodeManifest(manifest.Generation, 2); break;
                case "gc-live": key = NomCatalog.GarbageKey(resource.Version.Digest); value = NomRetentionCodec.EncodeGc(resource.Version.Digest, resource.Version.Length); break;
                case "gc-key": key = NomCatalog.GarbageKey(missing); value = NomRetentionCodec.EncodeGc(resource.Version.Digest, resource.Version.Length); break;
                case "gc-length": key = NomCatalog.GarbageKey(missing); value = NomRetentionCodec.EncodeGc(missing, 0); BinaryPrimitives.WriteInt64LittleEndian(value.AsSpan(40), -1); break;
                case "tombstone-head": key = NomCatalog.TombstoneKey(resource.Version.AssetId); value = NomCatalogRecordCodec.EncodeBinding(resource.Version.AssetId); break;
                case "dangling-exact-dependency":
                    var asset = Nom5Context.Asset(1, dependencies: [resource.Version with { Revision = missing }]); var encoded = CatalogCodec.Encode(asset);
                    var version = resource.Version with { Revision = ResourceCodec.Revision(encoded, resource.Version.Digest, resource.Version.Length) };
                    key = NomCatalogKeys.Revision(version.Revision); value = NomCatalogRecordCodec.EncodeRevision(new(version, encoded)); break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            raw.WriteBatch(new(raw.Sequence + 1, raw.Generation, raw.Generation, NomFormatFixtures.OperationId, [new NomJournalMutation(key, value)]));
            sequence = raw.Sequence; generation = raw.Generation;
        }
        var path = NomKvTestDirectory.Journal(context.Arguments.Root); var journal = File.ReadAllBytes(path); var header = NomJournalCodec.DecodeHeader(journal.AsSpan(0, NomJournalCodec.HeaderBytes));
        var frame = NomJournalCodec.EncodeBatch(header, new(sequence + 1, generation, generation, NomFormatFixtures.OperationId, [new NomJournalMutation([0xfe], [1])]));
        using (var append = new FileStream(path, FileMode.Append)) append.Write(frame.AsSpan(0, 60));
        var staging = Path.Combine(context.Arguments.Root, "staging", resource.Ticket.Sequence.ToString("x16")); Directory.CreateDirectory(staging);
        File.WriteAllBytes(Path.Combine(staging, "0000.tmp"), [99]); var before = NomKvTestDirectory.Snapshot(context.Arguments.Root); io.Events.Clear();
        Assert.Throws<InvalidDataException>(() => context.Factory.Create(context.Arguments with { Mode = AssetStoreOpenMode.Open }));
        Assert.DoesNotContain(io.Events, e => e.Point is "truncate" or "delete" || e.Point.StartsWith("write", StringComparison.Ordinal));
        NomKvTestDirectory.AssertSnapshot(before, context.Arguments.Root); NomTestDirectory.AssertRootReleased(context.Arguments);
        Assert.All(io.Opened, entry => Assert.True(entry.Handle.IsClosed));
    }
}
