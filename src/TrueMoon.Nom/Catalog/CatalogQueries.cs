using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Retention;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    internal Task<AssetPage> QueryAssetsAsync(AssetQuery query,AssetPageCursor? cursor=null,int pageSize=64,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(query); Require(pageSize is > 0 and <= 256 && query.Source != Guid.Empty && query.Kind != Guid.Empty,"Query identity/page budget.");
        if (query.Tag != null) _ = NomRetentionCodec.CaptureTags([query.Tag]);
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); var index = owner.Catalog.Index;
            if (cursor != null) Require(cursor.Store == owner.Header.StoreId && cursor.Sequence == owner.kvStore!.Sequence && cursor.Query == query,"Stale/foreign/query-mismatched cursor.");
            IEnumerable<Guid> ids = index.Assets.Keys;
            var filters = new List<SortedSet<Guid>>();
            if (query.Source is { } source) filters.Add(index.Sources.GetValueOrDefault(source) ?? []);
            if (query.Kind is { } kind) filters.Add(index.Kinds.GetValueOrDefault(kind) ?? []);
            if (query.Tag is { } tag) filters.Add(index.Tags.GetValueOrDefault(tag) ?? []);
            if (filters.Count != 0) ids = filters.MinBy(set => set.Count)!;
            var rows = new List<AssetRecord>(); var bytes = 0; var more = false;
            foreach (var id in ids)
            {
                token.ThrowIfCancellationRequested(); if (cursor != null && id.CompareTo(cursor.After) <= 0) continue;
                if ((!query.IncludeDeleted && index.Assets[id].Deleted) || filters.Any(set => !set.Contains(id))) continue;
                if (rows.Count == pageSize) { more = true; break; }
                var asset = owner.Catalog.Asset(id)!; bytes = checked(bytes + CatalogCodec.Encode(asset).Length);
                Require(bytes <= 1024 * 1024,"Detached query response byte budget."); rows.Add(asset);
            }
            return new AssetPage(rows.ToArray(),more ? new(owner.Header.StoreId,owner.kvStore!.Sequence,query,rows[^1].Id) : null,owner.Header.Generation);
        },token:token);
    }
    internal Task<AssetResourceVersion[]> ReadDependenciesAsync(AssetResourceVersion exact,bool reverse=false,int maximum=256,CancellationToken token=default)
    {
        NomRetentionCodec.ValidateVersion(exact); Require(maximum is > 0 and <= 4096,"Dependency response budget.");
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); var index = owner.Catalog.Index;
            Require(index.Versions.TryGetValue(exact.Revision,out var stored) && stored == exact,"Unknown/mismatched dependency version.");
            if (reverse) Require((index.Reverse.GetValueOrDefault(exact.Revision)?.Count ?? 0) <= maximum,"Dependency response count budget.");
            var result = reverse ? index.Reverse.GetValueOrDefault(exact.Revision)?.Select(r => index.Versions[r]).ToArray() ?? [] : index.Forward[exact.Revision].ToArray();
            Require(result.Length <= maximum,"Dependency response count budget."); return result;
        },token:token);
    }
    internal Task<AssetResourceVersion[]> GetDependencyClosureAsync(IReadOnlyList<AssetResourceVersion> roots,int maximumMembers=4096,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(roots); var captured = NomRetentionCodec.CaptureVersions(roots,256);
        Require(captured.Length > 0 && maximumMembers is > 0 and <= 4096,"Closure root/member budget.");
        return Enqueue(owner => { owner.RequireResourceReady(); token.ThrowIfCancellationRequested(); return owner.Catalog.Index.Closure(captured,maximumMembers); },captured.Length*104,token);
    }
    internal Task<long> DeleteResourcesAsync(long expectedGeneration,IReadOnlyList<Guid> assetIds,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(assetIds); var ids = assetIds.Take(257).ToArray();
        Require(ids.Length is > 0 and <= 256 && ids.Length == assetIds.Count && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length,"Delete identity budget/set.");
        return Enqueue(owner =>
        {
            owner.RequireResourceReady(); owner.RequireNoGarbage();
            lock(owner.admission) Require(owner.activeResourceWrite == null,"Resource write is active.");
            Require(owner.Header.Generation == expectedGeneration,"Stale delete generation."); var changed = false; owner.Begin();
            try
            {
                foreach (var id in ids) { token.ThrowIfCancellationRequested(); changed |= owner.Catalog.DeleteAsset(id); }
                if (!changed) { owner.Catalog.Rollback(); return expectedGeneration; }
                var next = checked(expectedGeneration+1); owner.Catalog.SetHeader(owner.Header with { Generation=next }); token.ThrowIfCancellationRequested(); owner.Commit(); owner.Header = owner.Header with { Generation=next }; return next;
            }
            catch(Exception error) { owner.Rollback(error); throw; }
        },ids.Length*16,token);
    }
}
