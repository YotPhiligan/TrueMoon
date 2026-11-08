using System.Globalization;
using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

internal sealed record Nom3PendingSeed(OpenAssetStoreArguments Arguments, AssetWriteTicket OriginalTicket,
    AssetResourceVersion Original, byte[] OriginalBytes, AssetWriteTicket Pending, AssetRecord Candidate, byte[] CandidateBytes);

internal static class Nom3Fixtures
{
    internal static readonly byte[] OldBytes = [10, 20, 30, 40];
    internal static readonly byte[] NewBytes = [5, 6, 7];
    internal static AssetRecord Candidate => NomFaultContext.Record(Guid.Parse("67ef9347-6782-4a1d-b267-bc0b09685c8b"), "candidate");
    internal static bool Whole(string mode) => mode is "full" or "flush-before" or "flush-after";
    internal static void Arm(NomFaultIo io, string mode, Exception error, int occurrence = 1)
    {
        io.Arm(mode.StartsWith("flush", StringComparison.Ordinal) ? "flush-journal" : "write-journal", error, occurrence,
            prefix: mode == "partial" ? 100 : 0, after: mode is "full" or "flush-after");
    }
    internal static int Writes(NomFaultIo io) => io.Events.Count(e => e.Point == "write-journal");
    internal static async Task<(AssetWriteTicket Ticket, AssetResourceVersion Version)> Publish(IProjectAssetStore owner, AssetRecord asset, byte[] bytes)
    {
        var ticket = await owner.ReserveResourcesAsync(owner.ContentGeneration, [NomFaultContext.Spec(asset, bytes)]);
        using var input = new ShortSource(bytes); var receipt = await owner.PublishResourcesAsync(ticket, new Dictionary<Guid, Stream> { [asset.Id] = input });
        Assert.False(input.WasDisposed); return (ticket, Assert.Single(receipt.Resources));
    }
    internal static async Task AssertOld(IProjectAssetStore store, AssetResourceVersion old, AssetRecord candidate)
    {
        Assert.Equal(1, store.ContentGeneration); Assert.Equal(old, await store.FindResourceAsync(old.AssetId));
        Assert.Null(await store.FindResourceAsync(candidate.Id)); Assert.Equal(old.AssetId, Assert.Single(await store.ReadAsync()).Id);
        await NomFaultContext.AssertBytes(store, old, OldBytes);
    }
    internal static void Stage(OpenAssetStoreArguments args, AssetWriteTicket ticket, byte[] bytes)
    {
        var root = StagePath(args, ticket); Directory.CreateDirectory(root); File.WriteAllBytes(Path.Combine(root, "0000.tmp"), bytes);
    }
    internal static string StagePath(OpenAssetStoreArguments args, AssetWriteTicket ticket) => Path.Combine(args.Root, "staging", ticket.Sequence.ToString("x16", CultureInfo.InvariantCulture));
    internal static async Task<Nom3PendingSeed> SeedPendingRoot(NomTestDirectory directory)
    {
        var args = directory.Arguments(); AssetWriteTicket originalTicket; AssetResourceVersion old;
        await using (var app = App.Build(context => context.UseNom()))
        {
            await using var store = NomTestDirectory.Factory(app).Create(args);
            (originalTicket, old) = await Publish(store, NomTestDirectory.Record("old"), OldBytes);
        }
        var candidate = Candidate; AssetWriteTicket pending;
        using (var factory = new NomKvStoreFactory())
        using (var store = factory.Open(new(args.Root, args.ProjectId, args.StoreId)))
        {
            var catalog = new NomCatalog(store); var control = catalog.Control; var asset = CatalogCodec.Encode(candidate);
            var request = CatalogCodec.Key("TrueMoon.Nom/request/1", args.StoreId.ToString("N"), "1", "1024",
                ResourceCodec.Hash(asset), "3", "3", NomTestDirectory.Digest(NewBytes));
            pending = new(args.StoreId, control.Next, request);
            catalog.Begin(); catalog.SetControl(control with { Next = control.Next + 1 });
            catalog.SetOperation(new(control.Next, request, 1, 1024, 1, "pending", null));
            catalog.SetItem(new(control.Next, 0, asset, 3, 3, NomTestDirectory.Digest(NewBytes), null)); catalog.Commit(); catalog.ValidateAll();
        }
        Stage(args, pending, NewBytes[..1]); return new(args, originalTicket, old, OldBytes, pending, candidate, NewBytes);
    }
    internal static void AssertClosed(NomFaultIo io, OpenAssetStoreArguments args)
    { Assert.All(io.OpenedCatalogFiles, file => Assert.True(file.Handle.IsClosed)); NomTestDirectory.AssertRootReleased(args); }
}
