using System.Globalization;
using System.Security.Cryptography;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;


namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    internal async Task<AssetWriteReceipt> PublishResourcesAsync(AssetWriteTicket ticket,IReadOnlyDictionary<Guid,Stream> sources,
        CancellationToken token=default,ResourceProofOptions? proof=null)
    {
        // Capture references at submission, before waiting for the owner; never inspect or own inputs here.
        ArgumentNullException.ThrowIfNull(sources);
        Require(sources.Count<=256,"Stream reference budget.");
        var submitted=new Dictionary<Guid,Stream>();
        foreach(var pair in sources)
        { Require(submitted.Count<256 && submitted.TryAdd(pair.Key,pair.Value),"Stream reference budget/identity."); }
        var state=await Enqueue(owner=>
        {
            owner.RequireResourceReady(); var op=owner.Operation(ticket); var items=owner.Items(op);
            if(op.State=="committed")
            {
                var versions=owner.Outputs(op,items); ResourceWrite? retained;
                lock(owner.admission)
                {
                    retained=owner.activeResourceWrite;
                    if(retained?.Ticket!=ticket) retained=null;
                    if(retained?.Running==true) return (Write:(ResourceWrite?)null,Receipt:new AssetWriteReceipt(op.Result!.Value,versions,true));
                    if(retained!=null) retained.Running=true;
                }
                var pending=!owner.TryCleanupStage(ticket,items.Length,out _);
                if(retained!=null)
                {
                    if(!pending) owner.EndWrite(retained);
                    else
                    {
                        bool stopping;
                        lock(owner.admission) { retained.Running=false; stopping=owner.stopping; }
                        if(stopping) { try { owner.StopReservedResource(); } catch(Exception error) { owner.ReportDrainFailure(error); } }
                    }
                }
                return (Write:(ResourceWrite?)null,Receipt:new AssetWriteReceipt(op.Result!.Value,versions,pending));
            }
            if(op.State=="aborted") throw new AbortedOperationException();
            lock(owner.admission)
            {
                ObjectDisposedException.ThrowIf(owner.stopping,owner); var write=owner.activeResourceWrite;
                Require(write!=null && write.Ticket==ticket && !write.Reserving && !write.Running,"Pending operation is not writable.");
                write!.Running=true; return (Write:write,Receipt:(AssetWriteReceipt?)null);
            }
        }).ConfigureAwait(false);
        if(state.Receipt != null) return state.Receipt;
        var active=state.Write!; var release=false;
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,resourceShutdown.Token); linked.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            Require(submitted.Count==active.Specs.Length,"Stream set does not match operation."); var identities=new HashSet<Stream>(ReferenceEqualityComparer.Instance); var capturedInputs=new Stream[active.Specs.Length];
            for(var i=0;i<active.Specs.Length;i++)
            {
                var row=CatalogCodec.Decode(active.Specs[i].Asset);
                Require(submitted.TryGetValue(row.Id,out var source) && source!=null && source.CanRead && identities.Add(source),"Missing/unreadable/aliased input stream.");
                capturedInputs[i]=source!;
            }
            var directory=StageDirectory(ticket,true); var versions=new AssetResourceVersion[active.Specs.Length]; var buffer=new byte[ResourceCodec.BufferBytes]; long total=0;
            for(var i=0;i<active.Specs.Length;i++)
            {
                linked.Token.ThrowIfCancellationRequested(); var spec=active.Specs[i]; var row=CatalogCodec.Decode(spec.Asset); var source=capturedInputs[i];
                var path=Path.Combine(directory,i.ToString("D4",CultureInfo.InvariantCulture)+".tmp"); long length=0;
                using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using(var target=files.CreateStaging(path))
                {
                    while(true)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        var remaining=Math.Min(spec.Maximum-length,active.Total-total);
                        if(spec.Length is { } declared) remaining=Math.Min(remaining,declared-length);
                        var ask=(int)Math.Min(buffer.Length,checked(remaining+1)); Require(ask>0,"Streaming byte budget exhausted.");
                        var count=await source.ReadAsync(buffer.AsMemory(0,ask),linked.Token).ConfigureAwait(false);
                        linked.Token.ThrowIfCancellationRequested(); Require(count>=0 && count<=ask,"Invalid Stream read count.");
                        if(count==0) break;
                        length=checked(length+count); total=checked(total+count);
                        Require(length<=spec.Maximum && total<=active.Total && (spec.Length==null || length<=spec.Length),"Streaming byte budget exceeded.");
                        hash.AppendData(buffer.AsSpan(0,count)); await files.WriteStagingAsync(target,buffer.AsMemory(0,count),linked.Token).ConfigureAwait(false);
                    }
                    Require(spec.Length==null || length==spec.Length,"Truncated input stream.");
                    var digest=Convert.ToHexStringLower(hash.GetHashAndReset()); Require(spec.Digest==null || spec.Digest==digest,"Input digest mismatch.");
                    await files.FlushStagingAsync(target,linked.Token).ConfigureAwait(false); files.FlushStagingToDisk(target);
                    versions[i]=new(row.Id,row.Kind,ResourceCodec.Revision(spec.Asset,digest,length),digest,length);
                }
            }
            proof?.At("after-stage");
            for(var i=0;i<versions.Length;i++)
            {
                linked.Token.ThrowIfCancellationRequested(); var version=versions[i]; var destination=ObjectPath(version.Digest,true);
                var staged=Path.Combine(directory,i.ToString("D4",CultureInfo.InvariantCulture)+".tmp");
                if(!File.Exists(destination))
                {
                    try { files.MoveObject(staged,destination); }
                    catch(IOException) when(File.Exists(destination)) { }
                }
                await using var existing=new FileStream(destination,FileMode.Open,FileAccess.Read,FileShare.Read,1,FileOptions.Asynchronous|FileOptions.SequentialScan);
                Require(existing.Length==version.Length && await ResourceCodec.HashStream(existing,linked.Token).ConfigureAwait(false)==version.Digest,"Existing CAS object integrity failure.");
            }
            proof?.At("after-finalize"); linked.Token.ThrowIfCancellationRequested();
            var receipt=await Enqueue(owner=>owner.CommitResources(active,versions,linked.Token,proof),control:true).ConfigureAwait(false);
            lock(admission) active.Committed=true;
            var cleanupPending=!TryCleanupStage(ticket,active.Specs.Length,out _); release=!cleanupPending; return receipt with { CleanupPending=cleanupPending };
        }
        catch(UnknownOutcomeException unknown)
        {
            lock(admission) active.OutcomeUncertain=true;
            release=TryCleanupStage(ticket,active.Specs.Length,out var cleanupError);
            if(cleanupError!=null) throw new UnknownOutcomeException(unknown.Ticket,new AggregateException("Unknown publication outcome and staging cleanup failure.",unknown,cleanupError));
            throw;
        }
        catch(Exception publicationError)
        {
            // Internal control remains admissible after Stop and when the public data queue is full.
            try { await Enqueue(owner=>{ owner.AbortWrite(active); return true; },control:true).ConfigureAwait(false); }
            catch(Exception cleanupError) { throw new AggregateException("Publication failed and abort cleanup retained an operation.",publicationError,cleanupError); }
            release=true; throw;
        }
        finally
        {
            if(release) EndWrite(active);
            else
            {
                bool retry;
                lock(admission) { active.Running=false; retry=stopping; }
                if(retry) { try { StopReservedResource(); } catch(Exception error) { ReportDrainFailure(error); } }
            }
        }
    }
    private bool TryCleanupStage(AssetWriteTicket ticket,int count,out Exception? error)
    {
        try { CleanupStage(ticket,count); error=null; return true; }
        catch(Exception failure) { error=failure; return false; }
    }
    private AssetWriteReceipt CommitResources(ResourceWrite write, AssetResourceVersion[] versions, CancellationToken token, ResourceProofOptions? proof)
    {
        RequireResourceReady(); var op = Operation(write.Ticket); var items = Items(op);
        Require(op.State == "pending" && Header.Generation == op.Expected, "Stale or completed resource publication.");
        token.ThrowIfCancellationRequested(); var next = checked(Header.Generation + 1); var committed = false; Begin();
        try
        {
            for (var i = 0; i < versions.Length; i++)
            {
                var version = versions[i]; var asset = CatalogCodec.Decode(write.Specs[i].Asset); var current = Catalog.Asset(asset.Id);
                if (current != null) Require(current.Source == asset.Source && current.Kind == asset.Kind, "Resource identity/kind cannot be retargeted.");
                Catalog.PutRevision(new(version, write.Specs[i].Asset)); Catalog.PutAsset(asset); Catalog.SetHead(asset.Id, version.Revision);
                Catalog.SetItem(items[i] with { Revision = version.Revision });
            }
            Catalog.SetHeader(Header with { Generation = next }); Catalog.SetOperation(op with { State = "committed", Result = next });
            proof?.At("before-commit"); token.ThrowIfCancellationRequested(); Commit(); committed = true; Header = Header with { Generation = next };
            proof?.At("after-commit"); if (proof?.ThrowAfterCommit == true) throw new IOException("Injected lost commit acknowledgement.");
            return new(next, versions.ToArray());
        }
        catch (Exception error)
        {
            if (committed || error is NomKvUnknownOutcomeException) { resourceRecoveryRequired = true; throw new UnknownOutcomeException(write.Ticket, error); }
            Rollback(error); throw;
        }
    }
}
