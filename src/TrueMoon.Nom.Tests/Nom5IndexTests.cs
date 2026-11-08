using Xunit;
using TrueMoon.Nom.Catalog;

namespace TrueMoon.Nom.Tests;

public sealed class Nom5IndexTests
{
    [Fact]
    public async Task QueryAssets_IdSourceKindAndOrdinalTagPagesStayOrderedAndRebuildAfterReopen()
    {
        await using var context = new Nom5Context();
        var empty = await context.Store.QueryAssetsAsync(new()); Assert.Empty(empty.Items); Assert.Null(empty.Next); Assert.Equal(0, empty.Generation);
        var source = Nom5Context.Id(101); var kind = Nom5Context.Id(201);
        var assets = new[]
        {
            Nom5Context.Asset(5, source, kind, ["red", "visible"]), Nom5Context.Asset(1, source, kind, ["red"]),
            Nom5Context.Asset(3, source, Nom5Context.Id(202), ["red"]), Nom5Context.Asset(2, Nom5Context.Id(102), kind, ["red"]),
            Nom5Context.Asset(4, source, kind, ["Red"])
        };
        foreach (var asset in assets) await context.Publish(asset, [(byte)asset.Id.GetHashCode(), 42]);
        async Task AssertQueries()
        {
            var all = await Drain(context.Store, new(), 2); Assert.Equal(new[] { 1, 2, 3, 4, 5 }.Select(Nom5Context.Id), all);
            Assert.Equal(new[] { 1, 3, 4, 5 }.Select(Nom5Context.Id), await Drain(context.Store, new(Source: source), 2));
            Assert.Equal(new[] { 1, 2, 4, 5 }.Select(Nom5Context.Id), await Drain(context.Store, new(Kind: kind), 2));
            Assert.Equal(new[] { 1, 2, 3, 5 }.Select(Nom5Context.Id), await Drain(context.Store, new(Tag: "red"), 2));
            Assert.Equal(new[] { 1, 5 }.Select(Nom5Context.Id), await Drain(context.Store, new(Source: source, Kind: kind, Tag: "red"), 1));
            Assert.Equal(new[] { Nom5Context.Id(4) }, await Drain(context.Store, new(Tag: "Red"), 1));
            Assert.Empty(await Drain(context.Store, new(Tag: "missing"), 2));
            Assert.Equal(5, context.Store.ContentGeneration);
        }
        await AssertQueries(); await context.Store.CompactAsync(); await context.Reopen(); await AssertQueries();
    }

    private static async Task<Guid[]> Drain(IProjectAssetStore store, AssetQuery query, int size)
    {
        var ids = new List<Guid>(); AssetPageCursor? next = null;
        do
        {
            var page = await store.QueryAssetsAsync(query, next, size); Assert.InRange(page.Items.Length, 0, size);
            ids.AddRange(page.Items.Select(item => item.Id)); next = page.Next;
            Assert.True(ids.Count < 100, "Paged query must advance.");
        } while (next != null);
        Assert.Equal(ids.Count, ids.Distinct().Count()); return ids.ToArray();
    }

    [Fact]
    public async Task QueryAssets_CursorsRejectDifferentFilterForeignStoreAndMetadataSequenceChange()
    {
        await using var context = new Nom5Context(); await context.Publish(Nom5Context.Asset(1), [1]); await context.Publish(Nom5Context.Asset(2), [2]);
        var first = await context.Store.QueryAssetsAsync(new(), pageSize: 1); Assert.NotNull(first.Next);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.QueryAssetsAsync(new(Tag: "different"), first.Next, 1));
        await using var other = new Nom5Context(); await Assert.ThrowsAsync<InvalidDataException>(() => other.Store.QueryAssetsAsync(new(), first.Next, 1));
        var version = (await context.Store.FindResourceAsync(Nom5Context.Id(1)))!; var manifest = await context.Store.CreateSnapshotAsync([version]);
        Assert.Equal(2, context.Store.ContentGeneration);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.QueryAssetsAsync(new(), first.Next, 1));
        Assert.Equal(new[] { Nom5Context.Id(1), Nom5Context.Id(2) }, await Drain(context.Store, new(), 1));
        await context.Store.RemoveSnapshotAsync(manifest.Id);
    }

    [Fact]
    public async Task QueryAssets_SeededModelMatchesIntersectedPagesAcrossDeletesAndTagReplacement()
    {
        await using var context = new Nom5Context(); var source = Nom5Context.Id(101); var kind = Nom5Context.Id(201);
        var model = Enumerable.Range(1, 18).Select(n => Nom5Context.Asset(n,
            n % 3 == 0 ? source : Nom5Context.Id(102), n % 2 == 0 ? kind : Nom5Context.Id(202),
            n % 4 == 0 ? ["featured", "red"] : ["red"])).ToDictionary(a => a.Id);
        foreach (var asset in model.Values.OrderByDescending(a => a.Id)) await context.Publish(asset, [(byte)asset.Id.GetHashCode()]);
        var deleted = new[] { Nom5Context.Id(3), Nom5Context.Id(12) }; await context.Store.DeleteResourcesAsync(18, deleted);
        var changed = Nom5Context.Asset(6, source, kind, ["blue"]); await context.Publish(changed, [60]); model[changed.Id] = changed;
        var queries = new[] { new AssetQuery(), new(Source: source), new(Kind: kind), new(Tag: "featured"), new(Source: source, Kind: kind, Tag: "red"), new(IncludeDeleted: true) };
        foreach (var query in queries)
        {
            var expected = model.Values.Where(a => (query.IncludeDeleted || !deleted.Contains(a.Id)) &&
                (query.Source == null || query.Source == a.Source) && (query.Kind == null || query.Kind == a.Kind) &&
                (query.Tag == null || a.Tags.Contains(query.Tag, StringComparer.Ordinal))).Select(a => a.Id).Order().ToArray();
            Assert.Equal(expected, await Drain(context.Store, query, 3));
        }
        await context.Reopen(); Assert.Equal(16, (await Drain(context.Store, new(), 4)).Length); Assert.Equal(20, context.Store.ContentGeneration);
    }

    [Theory]
    [InlineData(0)][InlineData(-1)][InlineData(257)]
    public async Task QueryAssets_InvalidPageSizeFailsWithoutContentMutation(int size)
    {
        await using var context = new Nom5Context();
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.QueryAssetsAsync(new(), pageSize: size));
        Assert.Equal(0, context.Store.ContentGeneration); Assert.Empty(await context.Store.ReadAsync());
    }

    [Fact]
    public async Task DeleteResources_TombstonesHideHeadsRetainBindingsAndPermitSameIdentityRepublish()
    {
        await using var context = new Nom5Context(); var asset = Nom5Context.Asset(1, tags: ["old"]); var first = await context.Publish(asset, [11]);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.DeleteResourcesAsync(0, [asset.Id]));
        Assert.Equal(first.Version, await context.Store.FindResourceAsync(asset.Id));
        await context.Store.DeleteResourcesAsync(1, [asset.Id]); Assert.Equal(2, context.Store.ContentGeneration); Assert.Null(await context.Store.FindResourceAsync(asset.Id));
        Assert.Empty((await context.Store.QueryAssetsAsync(new())).Items);
        Assert.Equal(asset.Id, Assert.Single((await context.Store.QueryAssetsAsync(new(IncludeDeleted: true))).Items).Id);
        await context.Reopen();
        var impostor = new AssetRecord(Nom5Context.Id(2), asset.Source, asset.Kind, asset.Locator, [2]);
        await Assert.ThrowsAnyAsync<IOException>(() => context.Publish(impostor, [22]));
        Assert.Null(await context.Store.FindResourceAsync(impostor.Id)); Assert.Equal(2, context.Store.ContentGeneration);
        var changed = new AssetRecord(asset.Id, asset.Source, asset.Kind, asset.Locator, [9], ["new"]);
        var second = await context.Publish(changed, [33]); Assert.Equal(3, context.Store.ContentGeneration); Assert.NotEqual(first.Version.Revision, second.Version.Revision);
        Assert.Equal(second.Version, await context.Store.FindResourceAsync(asset.Id)); Assert.Empty((await context.Store.QueryAssetsAsync(new(Tag: "old"))).Items);
        Assert.Equal(asset.Id, Assert.Single((await context.Store.QueryAssetsAsync(new(Tag: "new"))).Items).Id);
        await NomFaultContext.AssertBytes(context.Store, first.Version, [11]);
    }

    [Fact]
    public async Task Dependencies_ForwardReverseAndClosureUseExactOldRevisionsAfterHeadReplacement()
    {
        await using var context = new Nom5Context(); var b = await context.Publish(Nom5Context.Asset(2), [20]);
        var a = await context.Publish(Nom5Context.Asset(1, dependencies: [b.Version]), [10]);
        var c = await context.Publish(Nom5Context.Asset(3, dependencies: [a.Version, b.Version]), [30]);
        var b2 = await context.Publish(Nom5Context.Asset(2), [21]);
        Assert.Equal(new[] { b.Version }, await context.Store.ReadDependenciesAsync(a.Version));
        Assert.Equal(new[] { a.Version, c.Version }.OrderBy(v => v.Revision, StringComparer.Ordinal), await context.Store.ReadDependenciesAsync(b.Version, reverse: true));
        Assert.Empty(await context.Store.ReadDependenciesAsync(b2.Version, reverse: true));
        var closure = await context.Store.GetDependencyClosureAsync([c.Version], maximumMembers: 3);
        Assert.Equal(new[] { a.Version, b.Version, c.Version }.OrderBy(v => v.Revision, StringComparer.Ordinal), closure);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.GetDependencyClosureAsync([c.Version], maximumMembers: 2));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Store.ReadDependenciesAsync(b.Version, reverse: true, maximum: 1));
        await context.Store.CompactAsync(); await context.Reopen();
        Assert.Equal(closure, await context.Store.GetDependencyClosureAsync([c.Version, a.Version], maximumMembers: 3));
        Assert.Equal(b2.Version, await context.Store.FindResourceAsync(b.Version.AssetId));
    }

    [Fact]
    public async Task Dependencies_MissingOrMismatchedExactDescriptorCannotPublishDanglingHead()
    {
        await using var context = new Nom5Context(); var first = await context.Publish(Nom5Context.Asset(1), [10]);
        var missing = first.Version with { Revision = new string('f', 64) };
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Publish(Nom5Context.Asset(2, dependencies: [missing]), [20]));
        var mismatch = first.Version with { Length = first.Version.Length + 1 };
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Publish(Nom5Context.Asset(3, dependencies: [mismatch]), [30]));
        Assert.Equal(1, context.Store.ContentGeneration); Assert.Null(await context.Store.FindResourceAsync(Nom5Context.Id(2)));
        Assert.Null(await context.Store.FindResourceAsync(Nom5Context.Id(3))); Assert.Equal(first.Version, await context.Store.FindResourceAsync(first.Version.AssetId));
    }

    [Theory]
    [InlineData("query-source")][InlineData("query-kind")][InlineData("query-tag")][InlineData("closure-empty")]
    [InlineData("closure-duplicate")][InlineData("closure-zero")][InlineData("closure-over")][InlineData("dependencies-zero")]
    [InlineData("delete-empty")][InlineData("delete-duplicate")][InlineData("delete-empty-id")]
    public async Task DomainApis_InvalidIdentitySetsAndBoundsRejectWithoutMutatingGeneration(string invalid)
    {
        await using var context = new Nom5Context(); var resource = await context.Publish(Nom5Context.Asset(1), [1]);
        Func<Task> call = invalid switch
        {
            "query-source" => () => context.Store.QueryAssetsAsync(new(Source: Guid.Empty)),
            "query-kind" => () => context.Store.QueryAssetsAsync(new(Kind: Guid.Empty)),
            "query-tag" => () => context.Store.QueryAssetsAsync(new(Tag: " ")),
            "closure-empty" => () => context.Store.GetDependencyClosureAsync([]),
            "closure-duplicate" => () => context.Store.GetDependencyClosureAsync([resource.Version, resource.Version]),
            "closure-zero" => () => context.Store.GetDependencyClosureAsync([resource.Version], 0),
            "closure-over" => () => context.Store.GetDependencyClosureAsync([resource.Version], 4097),
            "dependencies-zero" => () => context.Store.ReadDependenciesAsync(resource.Version, maximum: 0),
            "delete-empty" => () => context.Store.DeleteResourcesAsync(1, []),
            "delete-duplicate" => () => context.Store.DeleteResourcesAsync(1, [resource.Version.AssetId, resource.Version.AssetId]),
            _ => () => context.Store.DeleteResourcesAsync(1, [Guid.Empty])
        };
        var before = Nom5Context.Snapshot(context.Arguments.Root); await Assert.ThrowsAsync<InvalidDataException>(call);
        Nom5Context.AssertSnapshot(before, context.Arguments.Root); Assert.Equal(1, context.Store.ContentGeneration);
        Assert.Equal(resource.Version, await context.Store.FindResourceAsync(resource.Version.AssetId));
    }
}
