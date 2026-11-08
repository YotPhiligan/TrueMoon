using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

// Macro workload: includes actual Flush(true), replay, CAS verification and owned worker lifetimes.
internal static class NomPerformanceProof
{
    private static readonly Guid Source=Guid.Parse("28e911f4-a504-443d-a7b8-0710cc140b45");
    private static readonly Guid Kind=Guid.Parse("ff394b81-7af4-4d94-b327-5383c14f3609");
    private static Guid Id(int value)=>Guid.Parse($"{value+1:x8}-a539-4000-9bc2-b7fe8be4e219");
    private static void Check(bool value,string reason) { if(!value) throw new InvalidDataException(reason); }
    private sealed record Sample(double Milliseconds,long AllocatedBytes,long WorkingSetBytes,long PrivateBytes);
    private sealed record Measurement(string Name,int Samples,double P50Milliseconds,double P95Milliseconds,long P50AllocatedBytes,long MaximumWorkingSetBytes,long MaximumPrivateBytes,Sample[] Raw);
    private static async Task<Measurement> Measure(string name,int samples,Func<int,Task> body)
    {
        var values=new List<Sample>();
        for(var n=0;n<samples;n++)
        {
            var allocated=GC.GetTotalAllocatedBytes(true); var watch=Stopwatch.StartNew(); await body(n); watch.Stop();
            var process=Process.GetCurrentProcess(); process.Refresh(); values.Add(new(watch.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes(true)-allocated,process.WorkingSet64,process.PrivateMemorySize64));
        }
        var sorted=values.OrderBy(v=>v.Milliseconds).ToArray(); var allocation=values.Select(v=>v.AllocatedBytes).Order().ToArray();
        return new(name,samples,sorted[(samples-1)/2].Milliseconds,sorted[(int)Math.Ceiling(samples*.95)-1].Milliseconds,allocation[(samples-1)/2],values.Max(v=>v.WorkingSetBytes),values.Max(v=>v.PrivateBytes),values.ToArray());
    }
    internal static async Task<int> Run(string report,string profile)
    {
        var results=new List<object>(); var measurements=new List<Measurement>(); string? failure=null;
        var scratch=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(report))!,"performance-"+Path.GetFileNameWithoutExtension(report)); Directory.CreateDirectory(scratch);
        var library=Program.LibraryInfo(); var process=Process.GetCurrentProcess();
        try
        {
            foreach(var scale in profile=="small" ? new[]{(Assets:1000,Revisions:4000,Edges:16000)} : new[]{(Assets:1000,Revisions:4000,Edges:16000),(Assets:50000,Revisions:100000,Edges:300000)})
            {
                var root=Path.Combine(scratch,"catalog-"+scale.Assets); var args=new OpenArguments(root,Guid.NewGuid(),Guid.NewGuid(),false,
                    Limits:new(DomainIndexBytes:scale.Assets==1000 ? 64L*1024*1024 : 256L*1024*1024));
                var seed=Seed(args,scale.Assets,scale.Revisions,scale.Edges); var rows=new List<Measurement>();
                if(profile!="small") rows.Add(await ColdOpen(args));
                rows.Add(await Measure("process-warm-cache-uncontrolled-reopen",5,async _=>
                {
                    using var factory=new CatalogFactory(); await using var owner=factory.Create(args);
                    Check((await owner.QueryAssetsAsync(new(Source,Kind,"mesh"),pageSize:128)).Items.Length==128,"reopen corpus query");
                }));
                using(var factory=new CatalogFactory())
                {
                    var owner=factory.Create(args);
                    rows.Add(await Measure("warm-indexed-query",9,async _=>Check((await owner.QueryAssetsAsync(new(Source,Kind,"mesh"),pageSize:128)).Items.Length==128,"warm query")));
                    rows.Add(await Measure("public-metadata-publication-flush",5,async n=>
                    {
                        var asset=new AssetRecord(Id(0),Source,Kind,"asset/0",BitConverter.GetBytes(900000+n),["mesh","scene"],seed.Versions.TakeLast(3).ToArray());
                        var ticket=await owner.ReserveResourcesAsync(owner.Header.Generation,[new(asset,64,seed.Digest,64)]); using var source=new MemoryStream(new byte[64]);
                        Check((await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,source}})).Resources.Length==1,"measured publication");
                    }));
                    rows.Add(await Measure("checkpoint-compaction-plus-control-append",3,async _=>
                    {
                        // Advance metadata outside a no-op path; the timed operation includes durable checkpoint verification.
                        var compact=await owner.CompactAsync(); Check(!compact.CleanupPending,"checkpoint cleanup");
                        var ticket=await owner.ReserveResourcesAsync(owner.Header.Generation,[new(new AssetRecord(Id(scale.Assets),Source,Kind,"unused",[]),0,null,1)]); await owner.AbortResourceOperationAsync(ticket);
                    }));
                    await owner.DisposeAsync();
                    rows.Add(await Measure("checkpoint-reopen",5,async _=>
                    { await using var reopened=factory.Create(args); Check(await reopened.FindResourceAsync(Id(0))!=null,"checkpoint reopen head"); }));
                }
                using(var factory=new NomKvStoreFactory()) using(var raw=factory.Open(new(root,args.ProjectId,args.StoreId)))
                {
                    long logical=64; byte[]? after=null;
                    do { var page=raw.ReadPage([],after); foreach(var pair in page.Items) logical+=pair.Key.Length+pair.Value.Length; if(!page.HasMore) break; after=page.Items[^1].Key; } while(true);
                    var disk=Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);
                    results.Add(new { scale.Assets,scale.Revisions,scale.Edges,ActualSeedEdges=seed.Edges,DomainIndexBytes=args.Limits!.DomainIndexBytes,SeedDurableBatches=seed.Batches,
                        LiveLogicalKvAndCasBytes=logical,PhysicalBytes=disk,DiskAmplification=(double)disk/logical,Measurements=rows });
                }
            }
            if(profile!="small")
            {
                results.Add(AdmissionLimits());
                foreach(var size in new[]{16*1024*1024,128*1024*1024})
                    measurements.AddRange(await Streamed(Path.Combine(scratch,"stream-"+size),size));
                results.Add(await MaximumStreaming(Path.Combine(scratch,"maximum-stream")));
            }
        }
        catch(Exception error) { failure=error.ToString(); Console.Error.WriteLine(error); }
        process.Refresh(); File.WriteAllText(report,JsonSerializer.Serialize(new { Completed=failure==null,Library=library,Profile=profile,Results=results,Streaming=measurements,Failure=failure,
            PeakWorkingSetBytes=process.PeakWorkingSet64,Runtime=Environment.Version.ToString(),ProcessorCount=Environment.ProcessorCount,
            Method="Empirical macro samples; nearest-rank p95, total process allocations including owner thread; OS cache uncontrolled, no physical cold-cache or speedup claim; all catalog batches and objects Flush(true). Seed uses bounded canonical raw catalog batches, measurements use public owner API." },new JsonSerializerOptions{WriteIndented=true}));
        return failure==null ? 0 : 1;
    }
    private static async Task<Measurement> ColdOpen(OpenArguments arguments)
    {
        var samples=new List<Sample>();var started=0;var reaped=0;
        for(var n=0;n<5;n++)
        {
            var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,"performance-open",arguments.Root,arguments.ProjectId.ToString(),arguments.StoreId.ToString(),arguments.Limits!.DomainIndexBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)})info.ArgumentList.Add(arg);
            using var process=new Process{StartInfo=info};Check(process.Start(),"cold-open process starts");started++;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));reaped++;Check(process.ExitCode==0 && (await error).Length==0,"cold-open child success");samples.Add(JsonSerializer.Deserialize<Sample>(await output)!);}
            finally{if(!process.HasExited){process.Kill(true);await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));reaped++;}}
        }
        Check(started==5 && reaped==5,"cold-open children reaped");var sorted=samples.OrderBy(v=>v.Milliseconds).ToArray();var allocation=samples.Select(v=>v.AllocatedBytes).Order().ToArray();
        return new("fresh-process-open-os-cache-uncontrolled",5,sorted[2].Milliseconds,sorted[4].Milliseconds,allocation[2],samples.Max(s=>s.WorkingSetBytes),samples.Max(s=>s.PrivateBytes),samples.ToArray());
    }
    internal static int OpenChild(string[] args)
    {
        try
        {
            var allocated=GC.GetTotalAllocatedBytes(true);var watch=Stopwatch.StartNew();
            using(var factory=new CatalogFactory())using(var owner=factory.Create(new(args[1],Guid.Parse(args[2]),Guid.Parse(args[3]),false,Limits:new(DomainIndexBytes:long.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture)))))
                Check(owner.QueryAssetsAsync(new(Source,Kind,"mesh"),pageSize:128).GetAwaiter().GetResult().Items.Length==128,"cold-open exact query");
            watch.Stop();var process=Process.GetCurrentProcess();process.Refresh();Console.WriteLine(JsonSerializer.Serialize(new Sample(watch.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes(true)-allocated,process.WorkingSet64,process.PrivateMemorySize64)));return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    private static (AssetResourceVersion[] Versions,string Digest,int Edges,int Batches) Seed(OpenArguments arguments,int assets,int revisions,int edges)
    {
        var versions=new List<AssetResourceVersion>(); var digest=ResourceCodec.Hash(new byte[64]); var edgeCount=0; var batches=0;
        using var factory=new NomKvStoreFactory(); using var raw=factory.Create(new(arguments.Root,arguments.ProjectId,arguments.StoreId)); var catalog=new NomCatalog(raw); catalog.Create(arguments.ProjectId,arguments.StoreId);
        var normal=edges/revisions; var missing=normal*(normal+1)/2;
        for(var start=0;start<revisions;start+=128)
        {
            var mutations=new Dictionary<byte[],byte[]?>(NomKeyComparer.Instance);
            for(var n=start;n<Math.Min(start+128,revisions);n++)
            {
                var deps=Math.Min(n,normal)+(n>=normal+1 && n<normal+1+missing ? 1 : 0); var asset=new AssetRecord(Id(n%assets),Source,Kind,"asset/"+(n%assets),BitConverter.GetBytes(n),["mesh","scene"],versions.TakeLast(deps).ToArray());
                edgeCount+=deps; var encoded=CatalogCodec.Encode(asset); var version=new AssetResourceVersion(asset.Id,asset.Kind,ResourceCodec.Revision(encoded,digest,64),digest,64); versions.Add(version);
                mutations[NomCatalogKeys.Asset(asset.Id)]=encoded; mutations[NomCatalogKeys.Binding(CatalogCodec.Binding(asset))]=NomCatalogRecordCodec.EncodeBinding(asset.Id);
                mutations[NomCatalogKeys.Revision(version.Revision)]=NomCatalogRecordCodec.EncodeRevision(new(version,encoded)); mutations[NomCatalogKeys.Head(asset.Id)]=NomCatalogRecordCodec.EncodeHead(asset.Id,version.Revision);
            }
            mutations[NomCatalogKeys.Header]=NomCatalogRecordCodec.EncodeHeader(new(arguments.ProjectId,arguments.StoreId,raw.Generation+1));
            var batch=new NomJournalBatch(raw.Sequence+1,raw.Generation,raw.Generation+1,Guid.NewGuid(),mutations.Select(p=>new NomJournalMutation(p.Key,p.Value)).ToArray());
            try { raw.WriteBatch(batch); }
            catch(InvalidDataException error) when(!raw.IsFaulted && error.Message.Contains("segment capacity",StringComparison.Ordinal)) { raw.Compact(); raw.WriteBatch(batch); }
            batches++;
        }
        Check(edgeCount==edges,"exact corpus edge count"); var path=Path.Combine(arguments.Root,"objects","sha256",digest[..2],digest+".blob"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(new byte[64]);file.Flush(true);}
        return (versions.ToArray(),digest,edgeCount,batches);
    }
    private static object AdmissionLimits()
    {
        var results=new List<object>();
        foreach(var target in new[]{(Assets:50000,Revisions:100000,Edges:300000),(Assets:100001,Revisions:0,Edges:0)})
        {
            var index=new NomDomainIndex(); var acceptedAssets=0; var acceptedRevisions=0; string? refusal=null;
            try
            {
                for(var n=0;n<target.Assets;n++) { index.AddAsset(new(Id(n),Source,Kind,"asset/"+n,[]),false); acceptedAssets++; }
                var leaves=new List<AssetResourceVersion>();
                for(var n=0;n<target.Revisions;n++)
                {
                    var asset=new AssetRecord(Id(n%target.Assets),Source,Kind,"asset/"+(n%target.Assets),BitConverter.GetBytes(n),dependencies:leaves.TakeLast(3).ToArray()); var bytes=CatalogCodec.Encode(asset);
                    var version=new AssetResourceVersion(asset.Id,Kind,ResourceCodec.Revision(bytes,NomBinary.ZeroDigest,0),NomBinary.ZeroDigest,0); index.AddRevision(new(version,bytes)); leaves.Add(version); acceptedRevisions++;
                }
            }
            catch(InvalidDataException error) { refusal=error.Message; }
            Check(refusal!=null,"default index budget must refuse unsupported admission"); results.Add(new{RequestedAssets=target.Assets,RequestedRevisions=target.Revisions,RequestedEdges=target.Edges,acceptedAssets,acceptedRevisions,Refusal=refusal,DiskWrites=0});
        }
        // Count ceilings are checked with an explicit256MiB logical budget, without disk allocation.
        foreach(var mode in new[]{"revisions","edges"})
        {
            var index=new NomDomainIndex(256L*1024*1024);index.AddAsset(new(Id(0),Source,Kind,"max",[]),false);
            var previous=new List<AssetResourceVersion>();var accepted=0;var edges=0;string? refusal=null;
            try
            {
                var count=mode=="revisions" ? 200001 : 12533;
                for(var n=0;n<count;n++)
                {
                    // Build exactly400000accepted edges, then ask for one additional edge.
                    var depCount=mode=="revisions" ? 0 : Math.Min(32,n);
                    if(mode=="edges" && edges+depCount>400000)depCount=edges==400000 ? 1 : 400000-edges;
                    var asset=new AssetRecord(Id(0),Source,Kind,"max",BitConverter.GetBytes(n),dependencies:previous.TakeLast(depCount).ToArray());var bytes=CatalogCodec.Encode(asset);
                    var version=new AssetResourceVersion(Id(0),Kind,ResourceCodec.Revision(bytes,NomBinary.ZeroDigest,0),NomBinary.ZeroDigest,0);index.AddRevision(new(version,bytes));previous.Add(version);accepted++;edges+=depCount;
                }
            }
            catch(InvalidDataException error){refusal=error.Message;}
            Check(refusal!=null && (mode=="revisions" ? accepted==200000 : edges==400000),"exact maximum index count refusal");results.Add(new{Maximum=mode,AcceptedRevisions=accepted,AcceptedEdges=edges,Refusal=refusal,DiskWrites=0});
        }
        return new { DefaultAdmissionAndMaxima=results };
    }
    private static async Task<object> MaximumStreaming(string root)
    {
        const int size=512*1024*1024;using var factory=new CatalogFactory();await using var owner=factory.Create(new(root,Guid.NewGuid(),Guid.NewGuid(),true));
        var first=new AssetRecord(Id(0),Source,Kind,"maximum/0",[]);var second=new AssetRecord(Id(1),Source,Kind,"maximum/1",[]);
        var ticket=await owner.ReserveResourcesAsync(0,[new(first,size,null,size),new(second,size,null,size)],maximumTotalBytes:1024L*1024*1024);
        using var a=new PatternStream(size,91);using var b=new PatternStream(size,92);var allocated=GC.GetTotalAllocatedBytes(true);var watch=Stopwatch.StartNew();
        var receipt=await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{first.Id,a},{second.Id,b}});watch.Stop();var bytes=GC.GetTotalAllocatedBytes(true)-allocated;
        Check(receipt.Resources.Length==2 && receipt.Resources.All(v=>v.Length==size),"exact512MiBobjects/1GiBbatch admitted");
        foreach(var version in receipt.Resources){await using var stream=await owner.OpenResourceAsync(version);Check(stream.Length==size,"maximum object verified open");}
        try{await owner.ReserveResourcesAsync(owner.Header.Generation,[new(first,MaximumBytes:ResourceCodec.MaximumObject+1)]);throw new InvalidOperationException("Maximum+1 admitted");}catch(InvalidDataException){}
        return new{MaximumObjectBytes=size,MaximumBatchBytes=1024L*1024*1024,CommittedObjects=2,Milliseconds=watch.Elapsed.TotalMilliseconds,AllocatedBytes=bytes,AdjacentOversizeRefused=true};
    }
    private static async Task<Measurement[]> Streamed(string root,int size)
    {
        var args=new OpenArguments(root,Guid.NewGuid(),Guid.NewGuid(),true); using var factory=new CatalogFactory(); await using var owner=factory.Create(args); AssetResourceVersion? version=null;
        var publish=await Measure("stream-publish-"+size,5,async n=>
        {
            var asset=new AssetRecord(Id(0),Source,Kind,"stream",BitConverter.GetBytes(n)); var ticket=await owner.ReserveResourcesAsync(owner.Header.Generation,[new(asset,size,null,size)]);
            using var input=new PatternStream(size,n); version=(await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,input}})).Resources.Single();
        });
        var read=await Measure("verified-open-and-stream-"+size,5,async _=>
        { await using var stream=await owner.OpenResourceAsync(version!); using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer=new byte[64*1024]; int count; long total=0;
            while((count=await stream.ReadAsync(buffer))!=0){hash.AppendData(buffer.AsSpan(0,count));total+=count;} Check(total==size && Convert.ToHexStringLower(hash.GetHashAndReset())==version!.Digest,"stream verification result"); });
        return [publish,read];
    }
    private sealed class PatternStream(long length,int pattern) : Stream
    {
        private long position;
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override int Read(Span<byte> buffer){var n=(int)Math.Min(buffer.Length,length-position);buffer[..n].Fill((byte)pattern);position+=n;return n;}
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default){token.ThrowIfCancellationRequested();return ValueTask.FromResult(Read(buffer.Span));}
        public override void Flush(){} public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
