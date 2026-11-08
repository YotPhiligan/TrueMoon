using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;
using TrueMoon.Nom.Storage.Format;

namespace TrueMoon.Nom.Probe;

internal static class StreamingProof
{
    private static readonly List<object> observations = [];
    internal static IReadOnlyList<object> Observations => observations;
    private static readonly Guid CrashAsset = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static void Check(bool condition,string reason) => CatalogProof.Check(condition,reason);
    private static T Throws<T>(Action action) where T : Exception
    { try { action(); } catch(T ex) { return ex; } throw new InvalidDataException("Expected "+typeof(T).Name); }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch(T ex) { return ex; } throw new InvalidDataException("Expected "+typeof(T).Name); }
    private static OpenArguments Args(string scratch,string name,TimeSpan? drain = null) =>
        new(Path.Combine(scratch,"stream-"+name),Guid.NewGuid(),Guid.NewGuid(),true,DrainTimeout:drain);
    private static Dictionary<Guid,Stream> Inputs(params (AssetRecord Row,Stream Input)[] items) => items.ToDictionary(x=>x.Row.Id,x=>x.Input);
    private static AssetResourceVersion Version(AssetWriteReceipt receipt,AssetRecord row) => receipt.Resources.Single(r=>r.AssetId==row.Id);
    private static async Task WaitClosed(CatalogOwner owner)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while(!owner.IsClosed) await Task.Delay(10,timeout.Token);
    }
    private static AssetResourceWrite Spec(AssetRecord row,byte[] bytes) => new(row,bytes.LongLength,StreamFixtures.Digest(bytes),Math.Max(1,bytes.LongLength));
    private static async Task<AssetWriteReceipt> PublishSmall(CatalogOwner owner,AssetRecord row,byte[] bytes)
    {
        using var source = new ByteSource(bytes);
        var ticket = await owner.ReserveResourcesAsync(owner.Header.Generation,[Spec(row,bytes)]);
        var result = await owner.PublishResourcesAsync(ticket,Inputs((row,source)));
        Check(!source.WasDisposed && source.LengthRequests==0 && source.SeekRequests==0 && source.LargestRequest<=65536,"borrowed nonseekable short-read input contract");
        return result;
    }
    internal static async Task<string[]> Run(string scratch)
    {
        var passed = new List<string>();
        foreach(var item in new (string Name,Func<string,Task> Body)[]
        {
            (nameof(StreamResourceKinds),StreamResourceKinds),
            (nameof(BoundedLargeStreaming),BoundedLargeStreaming),
            (nameof(StreamLengthHashBudgets),StreamLengthHashBudgets),
            (nameof(AtomicBatchJournalAndDedup),AtomicBatchJournalAndDedup),
            (nameof(StreamCancellationCommitRaces),StreamCancellationCommitRaces),
            (nameof(StreamOwnershipAndStop),StreamOwnershipAndStop),
            (nameof(CorruptObjectsAndReadLeases),CorruptObjectsAndReadLeases),
            (nameof(ResourceCrashRecovery),ResourceCrashRecovery)
        })
        { await item.Body(scratch); passed.Add(item.Name); }
        return passed.ToArray();
    }

    private static async Task StreamResourceKinds(string scratch)
    {
        using var factory = new CatalogFactory(); using var owner = factory.Create(Args(scratch,"kinds"));
        var geometry = StreamFixtures.Row("triangle",StreamFixtures.GeometryKind);
        var image = StreamFixtures.Row("pixel",StreamFixtures.ImageKind);
        var shader = StreamFixtures.Row("vertex",StreamFixtures.ShaderKind);
        var geoBytes=StreamFixtures.Geometry(); var imageBytes=StreamFixtures.Image(); var shaderBytes=StreamFixtures.Shader();
        using var geoSource=new ByteSource(geoBytes,3); using var imageSource=new ByteSource(imageBytes,5); using var shaderSource=new ByteSource(shaderBytes,2);
        var ticket=await owner.ReserveResourcesAsync(0,[Spec(geometry,geoBytes),Spec(image,imageBytes),Spec(shader,shaderBytes)]);
        var receipt=await owner.PublishResourcesAsync(ticket,Inputs((geometry,geoSource),(image,imageSource),(shader,shaderSource)));
        Check(receipt.Generation==1 && owner.Header.Generation==1 && receipt.Resources.Length==3,"all three streamed kinds publish as one generation");
        foreach(var pair in new[]{(geometry,geoBytes),(image,imageBytes),(shader,shaderBytes)})
        {
            var value=Version(receipt,pair.Item1);
            Check(value.KindId==pair.Item1.Kind && value.Length==pair.Item2.LongLength && value.Digest==StreamFixtures.Digest(pair.Item2),"exact kind/length/digest");
            Check(await owner.FindResourceAsync(pair.Item1.Id)==value,"published current head resolves by AssetId");
        }
        await using(var stream=await owner.OpenResourceAsync(Version(receipt,geometry)))
        {
            Check(stream.CanRead && stream.CanSeek && !stream.CanWrite && stream.Position==0 && stream.Length==60,"owned geometry read stream contract");
            using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            Check(reader.ReadBytes(4).SequenceEqual("GEO1"u8.ToArray()) && reader.ReadUInt32()==3 && reader.ReadUInt32()==3,"literal geometry header/counts");
            var expected=new float[]{0,0,0,1,0,0,0,1,0};
            foreach(var value in expected) Check(reader.ReadSingle()==value,"literal triangle position");
            Check(reader.ReadUInt32()==0 && reader.ReadUInt32()==1 && reader.ReadUInt32()==2 && stream.ReadByte()==-1,"literal indices and EOF");
            Check(stream.Seek(24,SeekOrigin.Begin)==24 && reader.ReadSingle()==1,"geometry range via same owned handle");
            Throws<NotSupportedException>(()=>stream.WriteByte(1));
        }
        await using(var stream=await owner.OpenResourceAsync(Version(receipt,image)))
        {
            using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            Check(reader.ReadByte()=='B' && reader.ReadByte()=='M' && reader.ReadInt32()==58,"literal BMP file header");
            stream.Position=18; Check(reader.ReadInt32()==1 && reader.ReadInt32()==1,"literal image dimensions");
            stream.Seek(54,SeekOrigin.Begin); Check(reader.ReadBytes(4).SequenceEqual(new byte[]{0x11,0x22,0x33,0}),"pixel range read");
        }
        await using(var stream=await owner.OpenResourceAsync(Version(receipt,shader)))
        {
            using var reader=new StreamReader(stream,Encoding.UTF8,false,128,true);
            Check(await reader.ReadToEndAsync()==StreamFixtures.ShaderText,"shader text consumed through StreamReader");
        }
        Check(new[]{geoSource,imageSource,shaderSource}.All(s=>!s.WasDisposed && s.LargestRequest<=65536 && s.LengthRequests==0 && s.SeekRequests==0),"publisher does not seek/measure/dispose input streams");
    }

    private static async Task BoundedLargeStreaming(string scratch)
    {
        const long size=32L*1024*1024;
        using var factory=new CatalogFactory(); using var owner=factory.Create(Args(scratch,"large"));
        var row=StreamFixtures.Row("generated-large-geometry"); var digest=StreamFixtures.PatternDigest(size);
        using var source=new PatternSource(size);
        var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(row,size,digest,size)]);
        var before=GC.GetTotalAllocatedBytes(true);
        var receipt=await owner.PublishResourcesAsync(ticket,Inputs((row,source)));
        var uploadAllocated=GC.GetTotalAllocatedBytes(true)-before;
        Check(uploadAllocated<size/2,"upload allocates less than half a32MiB payload; catches eager full-copy implementation");
        Check(source.LargestRequest<=65536 && source.Reads>400 && source.LengthRequests==0 && source.SeekRequests==0 && !source.WasDisposed,"large input is bounded repeated nonseekable reads");
        before=GC.GetTotalAllocatedBytes(true);
        await using(var stream=await owner.OpenResourceAsync(Version(receipt,row)))
        {
            var oversized=new byte[131072];
            var bounded=await stream.ReadAsync(oversized);
            Check(bounded is >0 and <=65536 && stream.Position==bounded,"oversized caller buffer is served as one bounded64KiB read");
            stream.Position=0;
            var result=await StreamFixtures.Consume(stream);
            Check(result.Bytes==size && result.Digest==digest && result.LargestRead<=65536,"large output streamed incrementally with exact digest/length");
        }
        var openReadAllocated=GC.GetTotalAllocatedBytes(true)-before;
        Check(openReadAllocated<size/2,"open/integrity scan/read do not allocate a full32MiB payload");
        observations.Add(new { Check=nameof(BoundedLargeStreaming), PayloadBytes=size, UploadAllocatedBytes=uploadAllocated,
            OpenReadAllocatedBytes=openReadAllocated, ExclusiveAllocationBoundBytes=size/2, MaximumInputRequestBytes=source.LargestRequest, InputReadCalls=source.Reads });
        Console.WriteLine("stream-observation:"+JsonSerializer.Serialize(observations[^1]));
    }

    private static async Task StreamLengthHashBudgets(string scratch)
    {
        using var factory=new CatalogFactory(); using var owner=factory.Create(Args(scratch,"lengths"));
        foreach(var length in new[]{0,7,8})
        {
            var row=StreamFixtures.Row("unknown-length-"+length); var bytes=Enumerable.Range(0,length).Select(i=>(byte)i).ToArray();
            using var input=new ByteSource(bytes,1);
            var ticket=await owner.ReserveResourcesAsync(owner.Header.Generation,[new AssetResourceWrite(row,MaximumBytes:8)]);
            var result=await owner.PublishResourcesAsync(ticket,Inputs((row,input)));
            await using var read=await owner.OpenResourceAsync(Version(result,row));
            var data=await StreamFixtures.Consume(read);
            Check(data.Bytes==length && data.Digest==StreamFixtures.Digest(bytes),"empty/before/at unknown-length cap");
        }
        var baseline=owner.Header.Generation;
        foreach(var failure in new[]{"over-limit","short-length","long-length","bad-hash"})
        {
            var row=StreamFixtures.Row(failure); var bytes=new byte[failure=="over-limit"?9:4];
            var expected=failure=="short-length"?5L:failure=="long-length"?3L:(long?)null;
            var hash=failure=="bad-hash"?new string('0',64):null;
            using var source=new ByteSource(bytes,2);
            var ticket=await owner.ReserveResourcesAsync(baseline,[new AssetResourceWrite(row,expected,hash,8)]);
            await ThrowsAsync<InvalidDataException>(()=>owner.PublishResourcesAsync(ticket,Inputs((row,source))));
            Check(owner.Header.Generation==baseline && await owner.FindResourceAsync(row.Id)==null && (await owner.LookupResourceOperationAsync(ticket)).Status=="aborted","failed length/hash/limit never publishes head/generation");
            Check(!source.WasDisposed,"failure keeps borrowed stream owned by caller");
        }
        foreach(var totalLength in new[]{4,5,6})
        {
            baseline=owner.Header.Generation;
            var first=StreamFixtures.Row("total-a-"+totalLength); var second=StreamFixtures.Row("total-b-"+totalLength);
            using var a=new ByteSource(new byte[2]); using var b=new ByteSource(new byte[totalLength-2]);
            var total=await owner.ReserveResourcesAsync(baseline,[new AssetResourceWrite(first,MaximumBytes:4),new AssetResourceWrite(second,MaximumBytes:4)],maximumTotalBytes:5);
            if(totalLength<=5)
            {
                var receipt=await owner.PublishResourcesAsync(total,Inputs((first,a),(second,b)));
                Check(receipt.Generation==baseline+1 && receipt.Resources.Sum(v=>v.Length)==totalLength,"aggregate before/at byte cap");
            }
            else
            {
                await ThrowsAsync<InvalidDataException>(()=>owner.PublishResourcesAsync(total,Inputs((first,a),(second,b))));
                Check(owner.Header.Generation==baseline && await owner.FindResourceAsync(first.Id)==null && await owner.FindResourceAsync(second.Id)==null,"aggregate over-limit does not publish earlier member");
            }
        }
        baseline=owner.Header.Generation;
        await ThrowsAsync<InvalidDataException>(()=>owner.ReserveResourcesAsync(baseline,[new AssetResourceWrite(StreamFixtures.Row("bad-declared"),9,MaximumBytes:8)]));
        await ThrowsAsync<InvalidDataException>(()=>owner.ReserveResourcesAsync(baseline,[new AssetResourceWrite(StreamFixtures.Row("negative-declared"),-1)]));
        await ThrowsAsync<InvalidDataException>(()=>owner.ReserveResourcesAsync(baseline,[new AssetResourceWrite(StreamFixtures.Row("malformed-hash"),ExpectedDigest:"../escape")]));
        Check(owner.Header.Generation==baseline,"invalid declared length fails before stream access");
    }

    private static async Task AtomicBatchJournalAndDedup(string scratch)
    {
        using var factory=new CatalogFactory(); using var owner=factory.Create(Args(scratch,"journal"));
        var first=StreamFixtures.Row("batch-first",id:Guid.Parse("10000000-0000-0000-0000-000000000000"));
        var second=StreamFixtures.Row("batch-failing",id:Guid.Parse("20000000-0000-0000-0000-000000000000"));
        using var good=new ByteSource([1,2,3]); using var bad=new ThrowingSource();
        var aborted=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(first),new AssetResourceWrite(second)]);
        await ThrowsAsync<IOException>(()=>owner.PublishResourcesAsync(aborted,Inputs((first,good),(second,bad))));
        Check(good.Reads>0 && bad.Reads==1 && !good.WasDisposed && !bad.WasDisposed,"second canonical input fails after first was fully staged; borrowed inputs survive");
        Check(owner.Header.Generation==0 && (await owner.ReadAsync()).Length==0 && await owner.FindResourceAsync(first.Id)==null && await owner.FindResourceAsync(second.Id)==null,"second input failure leaves no partial asset metadata or heads");
        Check((await owner.LookupResourceOperationAsync(aborted)).Status=="aborted","journal records aborted batch");
        await owner.PruneResourceOperationsAsync(0);
        Check(owner.Header.Generation==0 && (await owner.LookupResourceOperationAsync(aborted)).Status=="aborted","Prune(0) is a no-op with retained operation history");
        var duplicate=StreamFixtures.Row("duplicate-id");
        await ThrowsAsync<InvalidDataException>(()=>owner.ReserveResourcesAsync(0,[new AssetResourceWrite(duplicate),new AssetResourceWrite(duplicate)]));
        using var retrySource=new MustNotReadSource(); using var retrySecond=new MustNotReadSource();
        await ThrowsAsync<AbortedOperationException>(()=>owner.PublishResourcesAsync(aborted,Inputs((first,retrySource),(second,retrySecond))));
        Check(retrySource.Reads==0 && retrySecond.Reads==0,"aborted token cannot replay upload");
        var a=StreamFixtures.Row("dedup-a"); var b=StreamFixtures.Row("dedup-b"); var data=StreamFixtures.Geometry();
        using var inputA=new ByteSource(data); using var inputB=new ByteSource(data);
        var ticket=await owner.ReserveResourcesAsync(0,[Spec(a,data),Spec(b,data)]);
        await ThrowsAsync<InvalidOperationException>(()=>owner.ReserveResourcesAsync(0,[new AssetResourceWrite(StreamFixtures.Row("competing"))]));
        var receipt=await owner.PublishResourcesAsync(ticket,Inputs((a,inputA),(b,inputB)));
        Check(receipt.Generation==1 && receipt.Resources.Length==2 && Version(receipt,a).Digest==Version(receipt,b).Digest && Version(receipt,a).AssetId!=Version(receipt,b).AssetId,"blob dedup preserves distinct logical assets");
        Check(Directory.GetFiles(Path.Combine(owner.Root,"objects"),"*.blob",SearchOption.AllDirectories).Length==1,"one immutable CAS object for equal bytes");
        using var neverA=new MustNotReadSource(); using var neverB=new MustNotReadSource();
        var retried=await owner.PublishResourcesAsync(ticket,Inputs((a,neverA),(b,neverB)));
        Check(retried.Generation==1 && owner.Header.Generation==1 && retried.Resources.ToHashSet().SetEquals(receipt.Resources) && neverA.Reads==0 && neverB.Reads==0,"committed retry returns exact receipt without new generation or input reads");
        await ThrowsAsync<InvalidDataException>(()=>owner.LookupResourceOperationAsync(ticket with { RequestDigest=new string('f',64) }));
        await ThrowsAsync<InvalidDataException>(()=>owner.ReserveResourcesAsync(0,[new AssetResourceWrite(StreamFixtures.Row("stale"))]));
        await owner.PruneResourceOperationsAsync(ticket.Sequence);
        await ThrowsAsync<ExpiredOperationException>(()=>owner.LookupResourceOperationAsync(ticket));
        await ThrowsAsync<ExpiredOperationException>(()=>owner.PublishResourcesAsync(ticket,Inputs((a,neverA),(b,neverB))));
        Check(owner.Header.Generation==1 && neverA.Reads==0,"expired token cannot silently execute again");
        await using var retained=await owner.OpenResourceAsync(Version(receipt,a));
        Check((await StreamFixtures.Consume(retained)).Digest==StreamFixtures.Digest(data),"journal pruning does not remove committed revision bytes");
        var capturedFirst=StreamFixtures.Row("captured-first",id:Guid.Parse("30000000-0000-0000-0000-000000000000"));
        var capturedSecond=StreamFixtures.Row("captured-second",id:Guid.Parse("40000000-0000-0000-0000-000000000000"));
        using var blocked=new IgnoringCancellationSource(); using var originalSecond=new ByteSource([8,6,7,5]); using var replacement=new ThrowingSource();
        var mutableInputs=Inputs((capturedFirst,blocked),(capturedSecond,originalSecond));
        var capturedTicket=await owner.ReserveResourcesAsync(1,[new AssetResourceWrite(capturedFirst),new AssetResourceWrite(capturedSecond)]);
        var capturedWrite=owner.PublishResourcesAsync(capturedTicket,mutableInputs);
        try
        {
            await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            mutableInputs[capturedSecond.Id]=replacement;
        }
        finally { blocked.Release.TrySetResult(); }
        var capturedReceipt=await capturedWrite;
        Check(capturedReceipt.Generation==2 && originalSecond.Reads>0 && replacement.Reads==0 && !originalSecond.WasDisposed && !replacement.WasDisposed,"caller dictionary mutation cannot replace captured borrowed input references");
        await using var capturedRead=await owner.OpenResourceAsync(Version(capturedReceipt,capturedSecond));
        var capturedResult=await StreamFixtures.Consume(capturedRead);
        Check(capturedResult.Bytes==4 && capturedResult.Digest==StreamFixtures.Digest(new byte[]{8,6,7,5}),"captured second resource publishes original bytes after first input gate");
        var updated=await PublishSmall(owner,a,[6,2,6]); var updatedVersion=Version(updated,a);
        Check(updated.Generation==3 && updatedVersion.Revision!=Version(receipt,a).Revision && await owner.FindResourceAsync(a.Id)==updatedVersion,"same AssetId advances current head to a new exact revision");
        await using(var oldRevision=await owner.OpenResourceAsync(Version(receipt,a)))
            Check((await StreamFixtures.Consume(oldRevision)).Digest==StreamFixtures.Digest(data),"previous exact revision remains readable after current head advances");
        await using(var newRevision=await owner.OpenResourceAsync(updatedVersion))
        {
            var contents=await StreamFixtures.Consume(newRevision);
            Check(contents.Bytes==3 && contents.Digest==StreamFixtures.Digest(new byte[]{6,2,6}),"new exact revision serves new bytes");
        }
        var queuedRow=StreamFixtures.Row("queued-submission");
        using var queueOriginal=new ByteSource([7,3,1]); using var queueReplacement=new ThrowingSource();
        var queuedInputs=Inputs((queuedRow,queueOriginal));
        var queuedTicket=await owner.ReserveResourcesAsync(3,[new AssetResourceWrite(queuedRow)]);
        var gate=new OwnerGate(); var holding=owner.HoldAsync(gate);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<AssetWriteReceipt> queuedWrite;
        try
        {
            queuedWrite=owner.PublishResourcesAsync(queuedTicket,queuedInputs);
            queuedInputs[queuedRow.Id]=queueReplacement;
            Check(!queuedWrite.IsCompleted,"publish acquisition is queued behind held owner");
        }
        finally { gate.Release.Set(); }
        await holding; var queuedReceipt=await queuedWrite;
        Check(queuedReceipt.Generation==4 && queueOriginal.Reads>0 && queueReplacement.Reads==0 && !queueOriginal.WasDisposed && !queueReplacement.WasDisposed,"submission captures original borrowed streams before queued owner acquisition");
        await using var queuedRead=await owner.OpenResourceAsync(Version(queuedReceipt,queuedRow));
        var queuedBytes=await StreamFixtures.Consume(queuedRead);
        Check(queuedBytes.Bytes==3 && queuedBytes.Digest==StreamFixtures.Digest(new byte[]{7,3,1}),"queued publish serves original submitted bytes after caller dictionary mutation");
        await BindingRollback(scratch);
        await ExplicitReservationAbort(scratch);
    }

    private static async Task ExplicitReservationAbort(string scratch)
    {
        using var factory=new CatalogFactory(); using var owner=factory.Create(Args(scratch,"explicit-reservation-abort"));
        var abandoned=StreamFixtures.Row("explicit-abort"); using var unused=new MustNotReadSource();
        var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(abandoned)]);
        await owner.AbortResourceOperationAsync(ticket);
        Check((await owner.LookupResourceOperationAsync(ticket)).Status=="aborted" && owner.Header.Generation==0 && await owner.FindResourceAsync(abandoned.Id)==null,"explicit abort durably terminates unused reservation without publication");
        await ThrowsAsync<AbortedOperationException>(()=>owner.PublishResourcesAsync(ticket,Inputs((abandoned,unused))));
        Check(unused.Reads==0 && !unused.WasDisposed,"explicitly aborted reservation cannot read or dispose borrowed retry source");
        var next=StreamFixtures.Row("after-explicit-abort"); var receipt=await PublishSmall(owner,next,[1,4,9]);
        Check(receipt.Generation==1 && await owner.FindResourceAsync(next.Id)==Version(receipt,next),"explicit abort releases admission for next reservation and publication");
        await using var read=await owner.OpenResourceAsync(Version(receipt,next));
        var contents=await StreamFixtures.Consume(read);
        Check(contents.Bytes==3 && contents.Digest==StreamFixtures.Digest(new byte[]{1,4,9}),"resource after explicit abort serves complete expected bytes");
    }

    private static async Task BindingRollback(string scratch)
    {
        using var factory=new CatalogFactory(); var args=Args(scratch,"catalog-binding-rollback"); var seed=StreamFixtures.Row("reserved-binding");
        var first=StreamFixtures.Row("inserted-before-catalog-conflict",id:Guid.Parse("10000000-0000-0000-0000-000000000000"));
        var conflict=StreamFixtures.Row("reserved-binding",id:Guid.Parse("20000000-0000-0000-0000-000000000000"));
        AssetWriteReceipt original; AssetWriteTicket failed;
        using(var owner=factory.Create(args))
        {
            original=await PublishSmall(owner,seed,[2,7,1,8]);
            var firstBytes=new byte[]{4,2}; var conflictBytes=new byte[]{1,6,1};
            using var firstInput=new ByteSource(firstBytes); using var conflictInput=new ByteSource(conflictBytes);
            failed=await owner.ReserveResourcesAsync(1,[Spec(first,firstBytes),Spec(conflict,conflictBytes)]);
            var error=await ThrowsAsync<IOException>(()=>owner.PublishResourcesAsync(failed,Inputs((first,firstInput),(conflict,conflictInput))));
            Check(error.Message.Length>0 && firstInput.Reads>0 && conflictInput.Reads>0,"second canonical resource hits real catalog unique binding conflict after both inputs staged");
            var rows=await owner.ReadAsync();
            Check(owner.Header.Generation==1 && rows.Length==1 && rows[0].Id==seed.Id && rows[0].Locator=="reserved-binding" && rows[0].Metadata.SequenceEqual(new byte[]{9,8,7}),"catalog partial-batch failure preserves original generation and metadata");
            Check(await owner.FindResourceAsync(seed.Id)==Version(original,seed) && await owner.FindResourceAsync(first.Id)==null && await owner.FindResourceAsync(conflict.Id)==null,"catalog rollback removes earlier new head and preserves previous head");
            Check((await owner.LookupResourceOperationAsync(failed)).Status=="aborted" && !firstInput.WasDisposed && !conflictInput.WasDisposed,"catalog failure records aborted ticket without disposing borrowed inputs");
        }
        using(var f=new NomKvStoreFactory()) using(var raw=f.Open(new(args.Root,args.ProjectId,args.StoreId)))
            Check(raw.ReadPage(NomCatalogKeys.Prefix(NomKeySpace.Revision)).Items.Count==1 && raw.ReadPage(NomCatalogKeys.Prefix(NomKeySpace.Head)).Items.Count==1 && raw.ReadPage(NomCatalogKeys.Prefix(NomKeySpace.Asset)).Items.Count==1,"failed batch left no new rows/heads/revisions");
        using var reopened=factory.Create(args with {Create=false});
        Check(reopened.Header.Generation==1 && (await reopened.ReadAsync()).Single().Id==seed.Id && (await reopened.LookupResourceOperationAsync(failed)).Status=="aborted","reopen preserves catalog partial-batch rollback and aborted outcome");
        await using var oldBytes=await reopened.OpenResourceAsync(Version(original,seed));
        var retained=await StreamFixtures.Consume(oldBytes);
        Check(retained.Bytes==4 && retained.Digest==StreamFixtures.Digest(new byte[]{2,7,1,8}),"original resource bytes survive catalog duplicate-key rollback");
    }

    private static async Task StreamCancellationCommitRaces(string scratch)
    {
        using var factory=new CatalogFactory(); var args=Args(scratch,"cancel"); var owner=factory.Create(args);
        try
        {
            using var pre=new CancellationTokenSource(); pre.Cancel();
            await ThrowsAsync<OperationCanceledException>(()=>owner.ReserveResourcesAsync(0,[new AssetResourceWrite(StreamFixtures.Row("pre"))],token:pre.Token));
            Check(owner.Header.Generation==0 && (await owner.ReadAsync()).Length==0,"pre-cancel no metadata publication");
            var queuedGate=new OwnerGate(); var holding=owner.HoldAsync(queuedGate); using var queuedCancel=new CancellationTokenSource();
            await queuedGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var queued=owner.ReserveResourcesAsync(0,[new AssetResourceWrite(StreamFixtures.Row("queued-cancel"))],token:queuedCancel.Token);
            try { queuedCancel.Cancel(); Check(!queued.IsCompleted,"queued cancel waits until owner observes work"); }
            finally { queuedGate.Release.Set(); }
            await holding; await ThrowsAsync<OperationCanceledException>(()=>queued);
            Check(owner.Header.Generation==0 && (await owner.ReadAsync()).Length==0,"queued cancellation reserves no resource or metadata");
            var row=StreamFixtures.Row("copy-cancel"); using var input=new IgnoringCancellationSource(); using var cancellation=new CancellationTokenSource();
            var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(row)]);
            var writing=owner.PublishResourcesAsync(ticket,Inputs((row,input)),cancellation.Token);
            try
            {
                await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancellation.Cancel();
                Check(!writing.IsCompleted,"cancel does not abandon ignored-token input I/O");
            }
            finally { input.Release.TrySetResult(); }
            await ThrowsAsync<OperationCanceledException>(()=>writing);
            Check(owner.Header.Generation==0 && await owner.FindResourceAsync(row.Id)==null && (await owner.LookupResourceOperationAsync(ticket)).Status=="aborted" && !input.WasDisposed,"cancelled copy completes abort before returning");
            var before=StreamFixtures.Row("before-commit"); using var beforeInput=new ByteSource([1,2,3]); using var beforeCancel=new CancellationTokenSource(); var beforeGate=new ResourceGate();
            var beforeTicket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(before)]);
            var beforeTask=owner.PublishResourcesAsync(beforeTicket,Inputs((before,beforeInput)),beforeCancel.Token,new ResourceProofOptions(BeforeCommit:beforeGate));
            try { await beforeGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); beforeCancel.Cancel(); }
            finally { beforeGate.Release.Set(); }
            await ThrowsAsync<OperationCanceledException>(()=>beforeTask);
            Check(owner.Header.Generation==0 && await owner.FindResourceAsync(before.Id)==null,"cancel before commit gate never publishes");
            var after=StreamFixtures.Row("after-commit"); using var afterInput=new ByteSource([3,2,1]); using var afterCancel=new CancellationTokenSource(); var afterGate=new ResourceGate();
            var afterTicket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(after)]);
            var afterTask=owner.PublishResourcesAsync(afterTicket,Inputs((after,afterInput)),afterCancel.Token,new ResourceProofOptions(AfterCommit:afterGate));
            try { await afterGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); afterCancel.Cancel(); }
            finally { afterGate.Release.Set(); }
            var committed=await afterTask;
            Check(committed.Generation==1 && owner.Header.Generation==1 && (await owner.LookupResourceOperationAsync(afterTicket)).Status=="committed","post-commit cancellation does not relabel durable success");
            var unknown=StreamFixtures.Row("unknown-ack"); using var unknownInput=new ByteSource([4,5]);
            var unknownTicket=await owner.ReserveResourcesAsync(1,[new AssetResourceWrite(unknown)]);
            var outcome=await ThrowsAsync<UnknownOutcomeException>(()=>owner.PublishResourcesAsync(unknownTicket,Inputs((unknown,unknownInput)),proof:new ResourceProofOptions(ThrowAfterCommit:true)));
            Check(outcome.Ticket==unknownTicket,"unknown outcome carries exact ticket for durable resolution");
            owner.Dispose();
            using var reopened=factory.Create(args with { Create=false });
            Check(reopened.Header.Generation==2 && (await reopened.LookupResourceOperationAsync(unknownTicket)).Status=="committed","reopen resolves lost acknowledgment from durable journal");
            using var noRead=new MustNotReadSource(); var resolved=await reopened.PublishResourcesAsync(unknownTicket,Inputs((unknown,noRead)));
            Check(resolved.Generation==2 && noRead.Reads==0,"resolved committed retry does not duplicate publication");
        }
        finally { owner.Dispose(); }
    }

    private static async Task StreamOwnershipAndStop(string scratch)
    {
        using var factory=new CatalogFactory(); var args=Args(scratch,"stop-writer",TimeSpan.FromMilliseconds(100)); var owner=factory.Create(args);
        using var input=new IgnoringCancellationSource(); var row=StreamFixtures.Row("blocked-writer");
        var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(row)]); var writing=owner.PublishResourcesAsync(ticket,Inputs((row,input)));
        try
        {
            await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Throws<TimeoutException>(owner.Dispose);
            Check(!owner.IsClosed && factory.Count==1 && !writing.IsCompleted,"stop timeout retains owner until actual writer I/O returns");
            using var contender=new CatalogFactory(); Throws<IOException>(()=>contender.Create(args with { Create=false }));
        }
        finally { input.Release.TrySetResult(); }
        await ThrowsAsync<OperationCanceledException>(()=>writing); await WaitClosed(owner); owner.Dispose();
        Check(owner.IsClosed && factory.Count==0 && !input.WasDisposed,"writer abort drains before root release and input remains borrowed");
        var readArgs=Args(scratch,"stop-reader",TimeSpan.FromMilliseconds(100)); var readerOwner=factory.Create(readArgs);
        var readRow=StreamFixtures.Row("held-reader"); var committed=await PublishSmall(readerOwner,readRow,[1,3,5,7]);
        var lease=await readerOwner.OpenResourceAsync(Version(committed,readRow));
        try
        {
            Throws<TimeoutException>(readerOwner.Dispose);
            Check(!readerOwner.IsClosed && factory.Count==1,"idle read lease keeps DB/root owner alive during stop");
            using var contender=new CatalogFactory(); Throws<IOException>(()=>contender.Create(readArgs with { Create=false }));
        }
        finally { await lease.DisposeAsync(); await WaitClosed(readerOwner); readerOwner.Dispose(); }
        var activeArgs=Args(scratch,"stop-active-reader",TimeSpan.FromMilliseconds(100)); var activeOwner=factory.Create(activeArgs);
        var activeRow=StreamFixtures.Row("active-reader"); var activeReceipt=await PublishSmall(activeOwner,activeRow,[4,3,2,1]); var readGate=new ResourceGate();
        var activeLease=await activeOwner.OpenResourceAsync(Version(activeReceipt,activeRow),readGate:readGate);
        var reading=Task.Run(async()=>await activeLease.ReadAsync(new byte[1])); Task closing;
        try
        {
            await readGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); closing=activeLease.DisposeAsync().AsTask();
            Check(!closing.IsCompleted && !reading.IsCompleted,"read lease disposal waits for actually held I/O semaphore");
            Throws<TimeoutException>(activeOwner.Dispose);
            Check(!activeOwner.IsClosed && factory.Count==1 && !closing.IsCompleted,"owner stop retains root until active read and lease disposal really finish");
            using var contender=new CatalogFactory(); Throws<IOException>(()=>contender.Create(activeArgs with {Create=false}));
        }
        finally { readGate.Release.Set(); }
        await ThrowsAsync<OperationCanceledException>(()=>reading); await closing; await WaitClosed(activeOwner); activeOwner.Dispose();
        Check(activeOwner.IsClosed && factory.Count==0,"cancelled active read and actual handle close release root after semaphore drain");
        var reservedArgs=Args(scratch,"stop-reservation"); var reserved=factory.Create(reservedArgs);
        var pending=await reserved.ReserveResourcesAsync(0,[new AssetResourceWrite(StreamFixtures.Row("never-started"))]);
        reserved.Dispose();
        using(var f=new NomKvStoreFactory()) using(var raw=f.Open(new(reservedArgs.Root,reservedArgs.ProjectId,reservedArgs.StoreId)))
            Check(new NomCatalog(raw).Operation(pending.Sequence)!.State=="aborted","stop persisted aborted outcome before reopen recovery");
        using var reopened=factory.Create(reservedArgs with { Create=false });
        Check(reopened.Header.Generation==0 && (await reopened.LookupResourceOperationAsync(pending)).Status=="aborted","stop aborts reserved but unstarted operation");
    }

    private static async Task CorruptObjectsAndReadLeases(string scratch)
    {
        using var factory=new CatalogFactory(); using var owner=factory.Create(Args(scratch,"corruption")); var row=StreamFixtures.Row("integrity");
        var bytes=Enumerable.Range(0,128).Select(i=>(byte)i).ToArray(); var receipt=await PublishSmall(owner,row,bytes); var version=Version(receipt,row); var path=StreamFixtures.ObjectPath(owner.Root,version.Digest);
        async Task RejectRead()
        {
            try { await using var unexpected=await owner.OpenResourceAsync(version); }
            catch(Exception ex) when(ex is IOException or InvalidDataException) { return; }
            throw new InvalidDataException("Missing/corrupt resource was exposed as a read lease.");
        }
        File.Delete(path); await RejectRead(); File.WriteAllBytes(path,bytes[..^1]); await RejectRead();
        var changed=bytes.ToArray(); changed[63]^=0x80;
        for(var i=0;i<18;i++) { File.WriteAllBytes(path,changed); await RejectRead(); }
        File.WriteAllBytes(path,bytes);
        var leases=new List<Stream>();
        try
        {
            for(var i=0;i<16;i++) leases.Add(await owner.OpenResourceAsync(version));
            await ThrowsAsync<InvalidOperationException>(()=>owner.OpenResourceAsync(version));
            Throws<IOException>(()=>File.WriteAllBytes(path,changed));
            Check(leases[0].ReadByte()==0,"held verified handle prevents writer replacement while leased");
        }
        finally { foreach(var stream in leases) await stream.DisposeAsync(); }
        await using(var healthy=await owner.OpenResourceAsync(version)) Check((await StreamFixtures.Consume(healthy)).Digest==version.Digest,"failed opens and lease disposal release reader capacity");
        await ThrowsAsync<InvalidDataException>(()=>owner.OpenResourceAsync(version with { Digest=new string('f',64) }));
        var collision=StreamFixtures.Row("existing-corrupt-object"); var collisionBytes=new byte[]{2,4,6}; var collisionDigest=StreamFixtures.Digest(collisionBytes);
        var collisionPath=StreamFixtures.ObjectPath(owner.Root,collisionDigest); Directory.CreateDirectory(Path.GetDirectoryName(collisionPath)!); File.WriteAllBytes(collisionPath,[9,9,9]);
        using var collisionSource=new ByteSource(collisionBytes);
        var ticket=await owner.ReserveResourcesAsync(1,[Spec(collision,collisionBytes)]);
        await ThrowsAsync<InvalidDataException>(()=>owner.PublishResourcesAsync(ticket,Inputs((collision,collisionSource))));
        Check(File.ReadAllBytes(collisionPath).SequenceEqual(new byte[]{9,9,9}) && owner.Header.Generation==1 && await owner.FindResourceAsync(collision.Id)==null,"same-named bad CAS bytes are not overwritten or published");
        await RejectCrossRecordTampering(scratch);
    }

    private static async Task RejectCrossRecordTampering(string scratch)
    {
        using var factory = new CatalogFactory(); var headArgs = Args(scratch, "tamper-head");
        var a = StreamFixtures.Row("head-a"); var b = StreamFixtures.Row("head-b"); AssetWriteReceipt receipt;
        using (var seed = factory.Create(headArgs)) { await PublishSmall(seed, a, [1, 3]); receipt = await PublishSmall(seed, b, [2, 4, 6]); }
        Tamper(headArgs, NomCatalogKeys.Head(a.Id), NomCatalogRecordCodec.EncodeHead(a.Id, Version(receipt, b).Revision));
        Throws<InvalidDataException>(() => factory.Create(headArgs with { Create = false })); Check(factory.Count == 0, "foreign head rejected before exposure");
        var args = Args(scratch, "tamper-journal"); var first = StreamFixtures.Row("journal-a"); var second = StreamFixtures.Row("journal-b"); AssetWriteTicket ticket;
        using (var seed = factory.Create(args))
        {
            using var x = new ByteSource([5, 7]); using var y = new ByteSource([8, 9, 10]);
            ticket = await seed.ReserveResourcesAsync(0, [Spec(first, [5, 7]), Spec(second, [8, 9, 10])]);
            receipt = await seed.PublishResourcesAsync(ticket, Inputs((first, x), (second, y)));
        }
        byte[] key;
        using (var f = new NomKvStoreFactory()) using (var raw = f.Open(new(args.Root, args.ProjectId, args.StoreId)))
        {
            var item = new NomCatalog(raw).Items(ticket.Sequence).Single(item => CatalogCodec.Decode(item.Asset).Id == first.Id);
            key = NomCatalogKeys.Item(item.Sequence, item.Index);
            raw.WriteBatch(new(raw.Sequence + 1, raw.Generation, raw.Generation, Guid.NewGuid(), [new(key, NomCatalogRecordCodec.EncodeItem(item with { Revision = Version(receipt, second).Revision }))]));
        }
        Throws<InvalidDataException>(() => factory.Create(args with { Create = false }));
        using (var f = new NomKvStoreFactory()) using (var raw = f.Open(new(args.Root, args.ProjectId, args.StoreId)))
            Check(new NomCatalog(raw).Operation(ticket.Sequence)!.State == "committed" && NomCatalogRecordCodec.DecodeItem(raw.Get(key)!).Revision == Version(receipt, second).Revision, "invalid journal not repaired/relabelled");
        var controlArgs = Args(scratch, "tamper-control");
        using (var seed = factory.Create(controlArgs)) { var row = StreamFixtures.Row("pruned-control"); var result = await PublishSmall(seed, row, [3, 5, 8]); _ = result; await seed.PruneResourceOperationsAsync(1); }
        Tamper(controlArgs, NomCatalogKeys.Control, null);
        Throws<InvalidDataException>(() => factory.Create(controlArgs with { Create = false }));
        using (var f = new NomKvStoreFactory()) using (var raw = f.Open(new(controlArgs.Root, controlArgs.ProjectId, controlArgs.StoreId)))
            Check(raw.Get(NomCatalogKeys.Control) == null && raw.ReadPage(NomCatalogKeys.Prefix(NomKeySpace.Revision)).Items.Count == 1, "missing control not recreated, revision preserved");
    }
    private static void Tamper(OpenArguments args, byte[] key, byte[]? value)
    {
        using var f = new NomKvStoreFactory(); using var raw = f.Open(new(args.Root, args.ProjectId, args.StoreId));
        raw.WriteBatch(new(raw.Sequence + 1, raw.Generation, raw.Generation, Guid.NewGuid(), [new(key, value)]));
    }

    internal static int ChildCrash(string[] args) => ChildCrashAsync(args).GetAwaiter().GetResult();
    private static async Task<int> ChildCrashAsync(string[] args)
    {
        using var factory=new CatalogFactory();
        using var owner=factory.Create(new(args[1],Guid.Parse(args[2]),Guid.Parse(args[3]),false));
        const long size=2*1024*1024; var row=StreamFixtures.Row("crash-new",StreamFixtures.ShaderKind,CrashAsset);
        using var source=new PatternSource(size);
        var ticket=await owner.ReserveResourcesAsync(owner.Header.Generation,[new AssetResourceWrite(row,size,StreamFixtures.PatternDigest(size),size)]);
        Console.WriteLine("ticket:"+JsonSerializer.Serialize(ticket)); Console.Out.Flush();
        await owner.PublishResourcesAsync(ticket,Inputs((row,source)),proof:new ResourceProofOptions(CrashPoint:args[4]));
        Console.Error.WriteLine("Expected crash checkpoint was not reached."); return 2;
    }
    private static async Task<AssetWriteTicket> KillAt(string point,OpenArguments args)
    {
        var info=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in new[]{Assembly.GetExecutingAssembly().Location,"resource-crash",args.Root,args.ProjectId.ToString(),args.StoreId.ToString(),point}) info.ArgumentList.Add(arg);
        using var process=new Process{StartInfo=info}; var started=false;
        try
        {
            Check(process.Start(),"crash child starts"); started=true;
            var errors=process.StandardError.ReadToEndAsync(); using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var line=await process.StandardOutput.ReadLineAsync(timeout.Token);
            Check(line!=null && line.StartsWith("ticket:",StringComparison.Ordinal),"crash child reserves ticket before cut");
            var ticket=JsonSerializer.Deserialize<AssetWriteTicket>(line![7..])!;
            var cut=await process.StandardOutput.ReadLineAsync(timeout.Token);
            Check(cut=="cut:"+point && !process.HasExited,"child reached exact live cut point");
            process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(process.HasExited && (await errors).Length==0,"owned crash child actually reaped without cleanup path");
            return ticket;
        }
        finally { if(started && !process.HasExited){process.Kill(true);await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));} }
    }
    private static async Task ResourceCrashRecovery(string scratch)
    {
        foreach(var point in new[]{"after-stage","after-finalize","before-commit","after-commit"})
        {
            using var factory=new CatalogFactory(); var args=Args(scratch,"crash-"+point); var old=StreamFixtures.Row("old-committed"); AssetWriteReceipt original;
            using(var owner=factory.Create(args)) original=await PublishSmall(owner,old,[3,1,4,1,5]);
            var ticket=await KillAt(point,args);
            using var recovered=factory.Create(args with {Create=false}); var operation=await recovered.LookupResourceOperationAsync(ticket);
            var committed=point=="after-commit";
            Check(operation.Status==(committed?"committed":"aborted") && recovered.Header.Generation==(committed?2:1),"recovery gives complete committed or aborted generation at "+point);
            Check((await recovered.ReadAsync()).Length==(committed?2:1) && (await recovered.FindResourceAsync(CrashAsset)!=null)==committed,"no half resource heads/metadata after "+point);
            await using(var oldBytes=await recovered.OpenResourceAsync(Version(original,old)))
                Check((await StreamFixtures.Consume(oldBytes)).Digest==StreamFixtures.Digest(new byte[]{3,1,4,1,5}),"previous committed bytes survive crash");
            if(committed)
            {
                var exact=(await recovered.FindResourceAsync(CrashAsset))!; await using var bytes=await recovered.OpenResourceAsync(exact);
                var data=await StreamFixtures.Consume(bytes);
                Check(data.Bytes==2*1024*1024 && data.Digest==StreamFixtures.PatternDigest(2*1024*1024),"recovered new bytes complete");
                using var noRead=new MustNotReadSource(); var row=StreamFixtures.Row("unused-retry",StreamFixtures.ShaderKind,CrashAsset);
                var retry=await recovered.PublishResourcesAsync(ticket,Inputs((row,noRead)));
                Check(retry.Generation==2 && noRead.Reads==0,"lost-ack child commit retry is idempotent");
            }
            var staging=Path.Combine(args.Root,"staging");
            Check(!Directory.Exists(staging) || !Directory.EnumerateFiles(staging,"*.tmp",SearchOption.AllDirectories).Any(),"uncommitted owned staging cleaned after actual child exit");
        }
    }
}
