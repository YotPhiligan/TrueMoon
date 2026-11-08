using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Catalog;

// Rebuildable, bounded domain index; canonical KV records remain the sole authority.
internal sealed class NomDomainIndex
{
    private readonly long maximumBytes;
    internal NomDomainIndex(long maximumBytes=64L*1024*1024)
    { NomBinary.Require(maximumBytes is >= 1 and <= 256L*1024*1024,"Domain index admission configuration."); this.maximumBytes=maximumBytes; }
    internal sealed record Logical(Guid Source, Guid Kind, string[] Tags, bool Deleted);
    internal readonly SortedDictionary<Guid,Logical> Assets = [];
    internal readonly Dictionary<Guid,SortedSet<Guid>> Sources = [];
    internal readonly Dictionary<Guid,SortedSet<Guid>> Kinds = [];
    internal readonly Dictionary<string,SortedSet<Guid>> Tags = new(StringComparer.Ordinal);
    internal readonly Dictionary<string,AssetResourceVersion> Versions = new(StringComparer.Ordinal);
    internal readonly Dictionary<string,AssetResourceVersion[]> Forward = new(StringComparer.Ordinal);
    internal readonly Dictionary<string,SortedSet<string>> Reverse = new(StringComparer.Ordinal);
    private long logicalBytes;
    private int edges;
    private void Account(long bytes)
    { logicalBytes = checked(logicalBytes + bytes); NomBinary.Require(logicalBytes <= maximumBytes,"Domain index logical byte budget."); }
    private static void Add<TKey>(Dictionary<TKey,SortedSet<Guid>> map,TKey key,Guid id) where TKey:notnull
    { if (!map.TryGetValue(key,out var ids)) map.Add(key,ids=[]); ids.Add(id); }
    internal void AddAsset(AssetRecord asset,bool deleted)
    {
        NomBinary.Require(Assets.Count < 100_000,"Domain asset index count budget.");
        var tags = asset.Tags; Account(256 + tags.Sum(t => 96L + 2L * CatalogCodec.Utf8.GetByteCount(t)));
        Assets.Add(asset.Id,new(asset.Source,asset.Kind,tags,deleted)); Add(Sources,asset.Source,asset.Id); Add(Kinds,asset.Kind,asset.Id);
        foreach (var tag in tags) Add(Tags,tag,asset.Id);
    }
    internal void AddRevision(CatalogStoredResource resource)
    {
        NomBinary.Require(Versions.Count < 200_000,"Domain revision index count budget.");
        var asset = CatalogCodec.Decode(resource.Asset); var dependencies = asset.Dependencies;
        NomBinary.Require(Assets.TryGetValue(asset.Id,out var logical) && logical.Source == asset.Source && logical.Kind == asset.Kind,"Revision logical source/kind mismatch.");
        edges = checked(edges + dependencies.Length); NomBinary.Require(edges <= 400_000,"Domain dependency index count budget."); Account(384L + 384L * dependencies.Length);
        Versions.Add(resource.Version.Revision,resource.Version); Forward.Add(resource.Version.Revision,dependencies);
        foreach (var dependency in dependencies)
        { if (!Reverse.TryGetValue(dependency.Revision,out var parents)) Reverse.Add(dependency.Revision,parents=new(StringComparer.Ordinal)); parents.Add(resource.Version.Revision); }
    }
    internal void Validate()
    {
        foreach (var version in Versions.Values) NomBinary.Require(Assets.ContainsKey(version.AssetId),"Dangling revision logical identity.");
        foreach (var dependencies in Forward.Values)
            foreach (var dependency in dependencies) NomBinary.Require(Versions.TryGetValue(dependency.Revision,out var stored) && stored == dependency,"Missing/mismatched exact dependency.");
    }
    internal AssetResourceVersion[] Closure(AssetResourceVersion[] roots,int maximum)
    {
        NomBinary.Require(maximum is > 0 and <= 4096,"Closure member budget.");
        var seen = new SortedDictionary<string,AssetResourceVersion>(StringComparer.Ordinal); var queue = new Queue<AssetResourceVersion>(roots); var work = 0;
        while (queue.TryDequeue(out var version))
        {
            NomBinary.Require(Versions.TryGetValue(version.Revision,out var stored) && stored == version,"Unknown/mismatched closure version.");
            if (seen.ContainsKey(version.Revision)) continue;
            NomBinary.Require(seen.Count < maximum,"Closure member budget exceeded."); seen.Add(version.Revision,version);
            foreach (var dependency in Forward[version.Revision]) { NomBinary.Require(++work <= 32768,"Closure edge work budget."); queue.Enqueue(dependency); }
        }
        return seen.Values.ToArray();
    }
    internal string[] PruneCandidates(HashSet<string> live,int maximum)
    {
        NomBinary.Require(maximum is > 0 and <= 2048,"GC prune slice budget.");
        var dead = Versions.Keys.Where(r => !live.Contains(r)).ToHashSet(StringComparer.Ordinal);
        var incoming = dead.ToDictionary(r => r,r => Reverse.GetValueOrDefault(r)?.Count(dead.Contains) ?? 0,StringComparer.Ordinal);
        var ready = new SortedSet<string>(incoming.Where(p => p.Value==0).Select(p => p.Key),StringComparer.Ordinal);
        var selected = new List<string>();
        while (selected.Count < maximum && ready.Count != 0)
        {
            var revision = ready.Min!; ready.Remove(revision); selected.Add(revision);
            foreach (var dependency in Forward[revision])
                if (incoming.TryGetValue(dependency.Revision,out var count))
                { incoming[dependency.Revision]=count-1; if (count==1) ready.Add(dependency.Revision); }
        }
        NomBinary.Require(dead.Count==0 || selected.Count!=0,"Unreachable dependency cycle cannot be pruned in a bounded slice.");
        return selected.Order(StringComparer.Ordinal).ToArray();
    }
}
