using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5BoundariesTests
{
    private static AssetResourceVersion AddRevision(NomDomainIndex index, int value, AssetResourceVersion[] dependencies)
    {
        var asset = new AssetRecord(Nom5Context.Id(1), Nom5Context.Id(2), Nom5Context.Id(3), "chain", BitConverter.GetBytes(value), dependencies: dependencies);
        var encoded = CatalogCodec.Encode(asset); var digest = new string('a', 64);
        var version = new AssetResourceVersion(asset.Id, asset.Kind, ResourceCodec.Revision(encoded, digest, 0), digest, 0);
        index.AddRevision(new(version, encoded)); return version;
    }
    private static NomDomainIndex Index()
    {
        var index = new NomDomainIndex(); index.AddAsset(new(Nom5Context.Id(1), Nom5Context.Id(2), Nom5Context.Id(3), "chain", []), false); return index;
    }

    [Fact]
    public void DependencyClosure_MemberBudgetAccepts4096AndRejects4097WithoutPartialResult()
    {
        var index = Index(); var versions = new List<AssetResourceVersion>();
        for (var n = 0; n < 4097; n++) versions.Add(AddRevision(index, n, n == 0 ? [] : [versions[^1]])); index.Validate();
        var maximum = index.Closure([versions[4095]], 4096); Assert.Equal(4096, maximum.Length);
        Assert.Equal(versions.Take(4096).OrderBy(v => v.Revision, StringComparer.Ordinal), maximum);
        Assert.Throws<InvalidDataException>(() => index.Closure([versions[4096]], 4096));
        Assert.Equal(maximum, index.Closure([versions[4095]], 4096));
    }

    [Fact]
    public void DependencyClosure_EdgeWorkAccepts32768AndRejects32769WithSharedDagMembers()
    {
        var index = Index(); var versions = new List<AssetResourceVersion>();
        for (var n = 0; n < 1040; n++) versions.Add(AddRevision(index, n, versions.TakeLast(32).ToArray()));
        versions.Add(AddRevision(index, 1040, versions.TakeLast(16).ToArray())); // 496 +1008*32 +16 =32768 traversed edges.
        index.Validate(); var exact = index.Closure([versions[^1]], 4096); Assert.Equal(1041, exact.Length); Assert.Equal(1041, exact.Select(v => v.Revision).Distinct().Count());
        versions.Add(AddRevision(index, 1041, [versions[^1]])); index.Validate();
        Assert.Throws<InvalidDataException>(() => index.Closure([versions[^1]], 4096));
        Assert.Equal(exact, index.Closure([versions[^2]], 4096));
    }

    [Fact]
    public async Task QueryAssets_ResponseBudgetAcceptsExactlyOneMiBAndRejectsNextByteWithoutPartialSuccess()
    {
        await using var context = new Nom5Context(); const int budget = 1024 * 1024; var total = 0; AssetRecord? last = null;
        for (var n = 1; n <= 16; n++)
        {
            var baseAsset = Nom5Context.Asset(n); var overhead = CatalogCodec.Encode(new(baseAsset.Id, baseAsset.Source, baseAsset.Kind, baseAsset.Locator, [])).Length;
            var length = n == 16 ? budget - total - overhead : 64 * 1024;
            last = new(baseAsset.Id, baseAsset.Source, baseAsset.Kind, baseAsset.Locator, new byte[length]);
            total += CatalogCodec.Encode(last).Length; await context.Publish(last, [(byte)n]);
        }
        Assert.Equal(budget, total); var exact = await context.Store.QueryAssetsAsync(new(), pageSize: 16); Assert.Equal(16, exact.Items.Length); Assert.Null(exact.Next);
        Assert.Equal(budget, exact.Items.Sum(a => CatalogCodec.Encode(a).Length));
        var changed = new AssetRecord(last!.Id, last.Source, last.Kind, last.Locator, new byte[last.Metadata.Length + 1]); await context.Publish(changed, [17]);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.QueryAssetsAsync(new(), pageSize: 16));
        var bounded = await context.Store.QueryAssetsAsync(new(), pageSize: 15); Assert.Equal(15, bounded.Items.Length); Assert.NotNull(bounded.Next);
        var rest = await context.Store.QueryAssetsAsync(new(), bounded.Next, 15); Assert.Equal(changed.Id, Assert.Single(rest.Items).Id); Assert.Null(rest.Next); Assert.Equal(17, rest.Generation);
    }

    [Fact]
    public async Task GarbageCollection_ObjectCandidateBudgetProcesses2049InTwoBoundedSlices()
    {
        await using var context = new Nom5Context(); var paths = new List<string>();
        for (var n = 0; n < 2049; n++)
        {
            var bytes = BitConverter.GetBytes(n); var path = Nom5Context.ObjectPath(context.Arguments.Root, NomTestDirectory.Digest(bytes));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); paths.Add(path);
        }
        var before = Nom5Context.Snapshot(context.Arguments.Root); var exact = await context.Store.PlanGarbageCollectionAsync();
        Assert.Equal(2048, exact.ObjectsToDelete); Assert.Equal(8192, exact.BytesToDelete); Assert.Equal(0, exact.RevisionsToPrune); Assert.True(exact.HasMore);
        Nom5Context.AssertSnapshot(before, context.Arguments.Root);
        Assert.Equal(new NomGarbageCollectionReceipt(0, 2048, 8192, false), await context.Store.CollectGarbageAsync(exact));
        Assert.Single(paths, File.Exists); var last = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(1, last.ObjectsToDelete); Assert.Equal(4, last.BytesToDelete); Assert.False(last.HasMore);
        Assert.Equal(new NomGarbageCollectionReceipt(0, 1, 4, false), await context.Store.CollectGarbageAsync(last));
        Assert.All(paths, path => Assert.False(File.Exists(path))); Assert.Equal(0, context.Store.ContentGeneration);
    }

    [Fact]
    public async Task GarbageCollection_RevisionSlicesPruneParentsBeforeDependenciesAndRetainSharedCasUntilFinalSlice()
    {
        await using var context = new Nom5Context(); await context.Store.DisposeAsync(); var oldBytes = new byte[] { 10 }; var currentBytes = new byte[] { 20 };
        var versions = new List<AssetResourceVersion>(); AssetResourceVersion current;
        using (var factory = new NomKvStoreFactory())
        using (var raw = factory.Open(new(context.Arguments.Root, context.Arguments.ProjectId, context.Arguments.StoreId)))
        {
            var catalog = new NomCatalog(raw); catalog.Begin();
            for (var n = 0; n < 2050; n++)
            {
                var asset = new AssetRecord(Nom5Context.Id(1), Nom5Context.Id(1000), Nom5Context.Id(2000), "sliced", BitConverter.GetBytes(n),
                    dependencies: n == 0 ? [] : [versions[^1]]);
                var encoded = CatalogCodec.Encode(asset); var digest = NomTestDirectory.Digest(oldBytes);
                var version = new AssetResourceVersion(asset.Id, asset.Kind, ResourceCodec.Revision(encoded, digest, 1), digest, 1);
                catalog.PutRevision(new(version, encoded)); versions.Add(version);
            }
            var currentAsset = new AssetRecord(Nom5Context.Id(1), Nom5Context.Id(1000), Nom5Context.Id(2000), "sliced", [99]);
            var currentEncoded = CatalogCodec.Encode(currentAsset); var currentDigest = NomTestDirectory.Digest(currentBytes);
            current = new(currentAsset.Id, currentAsset.Kind, ResourceCodec.Revision(currentEncoded, currentDigest, 1), currentDigest, 1);
            catalog.PutAsset(currentAsset); catalog.PutRevision(new(current, currentEncoded)); catalog.SetHead(current.AssetId, current.Revision);
            catalog.SetHeader(new(context.Arguments.ProjectId, context.Arguments.StoreId, 1)); catalog.Commit(); catalog.ValidateAll();
        }
        foreach (var bytes in new[] { oldBytes, currentBytes })
        { var path = Nom5Context.ObjectPath(context.Arguments.Root, NomTestDirectory.Digest(bytes)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes); }
        await context.Reopen(); var first = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(2048, first.RevisionsToPrune); Assert.Equal(0, first.ObjectsToDelete); Assert.True(first.HasMore);
        Assert.Equal(new NomGarbageCollectionReceipt(2048, 0, 0, false), await context.Store.CollectGarbageAsync(first));
        await context.Reopen(); Assert.Equal(new[] { versions[0] }, await context.Store.ReadDependenciesAsync(versions[1]));
        Assert.Equal(new[] { versions[0], versions[1] }.OrderBy(v => v.Revision, StringComparer.Ordinal), await context.Store.GetDependencyClosureAsync([versions[1]]));
        await NomFaultContext.AssertBytes(context.Store, versions[0], oldBytes); await NomFaultContext.AssertBytes(context.Store, versions[1], oldBytes);
        var last = await context.Store.PlanGarbageCollectionAsync(); Assert.Equal(2, last.RevisionsToPrune); Assert.Equal(1, last.ObjectsToDelete); Assert.False(last.HasMore);
        Assert.Equal(new NomGarbageCollectionReceipt(2, 1, 1, false), await context.Store.CollectGarbageAsync(last));
        await context.Reopen(); Assert.Equal(current, await context.Store.FindResourceAsync(current.AssetId)); await NomFaultContext.AssertBytes(context.Store, current, currentBytes);
        Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, versions[0].Digest))); Assert.Equal(1, context.Store.ContentGeneration);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task GarbageCollection_AppendCapacityRejectsOneByteBelowPruneAndAcknowledgementBeforeAnyMutation(bool resume)
    {
        var io = new NomCheckpointIo(); await using var context = new Nom5Context(io);
        var old = await context.Publish(Nom5Context.Asset(1), [10, 20]); var current = await context.Publish(Nom5Context.Asset(1), [30]);
        await context.Store.PruneResourceOperationsAsync(old.Ticket.Sequence);
        if (resume)
        {
            io.Arm("delete", new IOException("leave durable deletion intent"));
            Assert.Equal(new NomGarbageCollectionReceipt(1, 0, 0, true), await context.Store.CollectGarbageAsync(await context.Store.PlanGarbageCollectionAsync())); io.Disarm();
        }
        await context.Store.DisposeAsync(); var path = NomKvTestDirectory.Journal(context.Arguments.Root); var existing = new FileInfo(path).Length;
        // One revision delete52 + GC intent100 + acknowledgement52, plus two144-byte frames.
        var required = resume ? 196L : 492L; await using var factory = new CatalogFactory(1, io);
        var arguments = new OpenArguments(context.Arguments.Root, context.Arguments.ProjectId, context.Arguments.StoreId, false,
            StorageLimits: new() { MaximumSegmentBytes = existing + required - 1 });
        await using (var refused = factory.Create(arguments))
        {
            var plan = resume ? null : await refused.PlanGarbageCollectionAsync(); var before = Nom5Context.Snapshot(context.Arguments.Root); io.Events.Clear();
            await Assert.ThrowsAsync<InvalidDataException>(() => refused.CollectGarbageAsync(plan));
            Assert.Empty(io.Events); Nom5Context.AssertSnapshot(before, context.Arguments.Root);
            Assert.True(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, old.Version.Digest))); Assert.Equal(2, refused.Header.Generation);
        }
        await using (var exact = factory.Create(arguments with { StorageLimits = new() { MaximumSegmentBytes = existing + required } }))
        {
            var plan = resume ? null : await exact.PlanGarbageCollectionAsync();
            Assert.Equal(new NomGarbageCollectionReceipt(resume ? 0 : 1, 1, 2, false), await exact.CollectGarbageAsync(plan));
            Assert.Equal(existing + required, new FileInfo(path).Length); Assert.False(File.Exists(Nom5Context.ObjectPath(context.Arguments.Root, old.Version.Digest)));
            Assert.Equal(current.Version, await exact.FindResourceAsync(current.Version.AssetId)); Assert.Equal(2, exact.Header.Generation);
            await using var bytes = await exact.OpenResourceAsync(current.Version); await Nom5Context.AssertBytes(bytes, [30]);
        }
        Assert.Equal(0, factory.Count); NomTestDirectory.AssertRootReleased(context.Arguments);
    }
}
