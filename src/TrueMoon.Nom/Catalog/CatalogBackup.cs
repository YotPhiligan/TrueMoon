using System.Security.Cryptography;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Resources;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Catalog;

internal sealed partial class CatalogOwner
{
    private NomBackupTransfer? backupTransfer;
    private NomKvStoreFactory? backupVerification;
    internal Task<NomBackupReceipt> ExportBackupAsync(string destinationRoot,NomBackupOptions? options=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested(); var destination=NomBackupTransfer.LocalRoot(destinationRoot); var captured=(options??new()).Snapshot();
        OperationCanceledException? cancellation=null;
        var queued=Enqueue(owner =>
        { try { return owner.ExportBackup(destination,captured,token); } catch(OperationCanceledException error) { cancellation=error; throw; } },token:token);
        return AwaitExport();
        async Task<NomBackupReceipt> AwaitExport()
        {
            try { return await queued.ConfigureAwait(false); }
            catch(OperationCanceledException) when(cancellation!=null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellation).Throw(); throw; }
        }
    }
    private static NomBackupEntry[] BackupSelection(NomKvStore store,NomCatalog catalog)
    {
        var entries=new List<NomBackupEntry>(store.BackupFiles());
        foreach(var group in catalog.Index.Versions.Values.GroupBy(v => v.Digest,StringComparer.Ordinal))
        {
            var first=group.First(); Require(group.All(v => v.Length==first.Length),"Conflicting CAS lengths.");
            entries.Add(new("objects/sha256/"+first.Digest[..2]+"/"+first.Digest+".blob",first.Length,first.Digest));
        }
        return entries.OrderBy(e => e.Path,StringComparer.Ordinal).ToArray();
    }
    private static long BackupBudget(NomBackupEntry[] entries,NomBackupOptions options)
    {
        Require(entries.Length<=options.MaximumFiles,"Backup file count budget.");
        var bytes=checked(entries.Sum(e => e.Length)+NomBackupCodec.Length(entries)); Require(bytes<=options.MaximumBytes,"Backup transfer byte budget."); return bytes;
    }
    private NomBackupReceipt ExportBackup(string destination,NomBackupOptions options,CancellationToken token)
    {
        RequireGcOffline(); RequireNoGarbage(); var staging=NomBackupTransfer.PrepareDestination(Root,destination);
        var selected=BackupSelection(kvStore!,Catalog); var bytes=BackupBudget(selected,options);
        // All source lengths and ancestor paths pass before the first destination write.
        foreach(var entry in selected) { var path=NomBackupTransfer.Resolve(Root,entry.Path); NomBackupTransfer.CheckPath(path); Require(new FileInfo(path).Length==entry.Length,"Missing/changed backup source."); }
        backupTransfer=new(files); var publishing=false;
        try
        {
            files.CreateBackupDirectory(staging); var entries=new List<NomBackupEntry>();
            foreach(var entry in selected)
                entries.Add(backupTransfer.Copy(NomBackupTransfer.Resolve(Root,entry.Path),NomBackupTransfer.Resolve(staging,entry.Path),entry.Path,entry.Length,
                    entry.Path.StartsWith("objects/",StringComparison.Ordinal) ? entry.Digest : null,token));
            var manifest=new NomBackupManifest(Header.ProjectId,Header.StoreId,kvStore!.Sequence,Header.Generation,entries.ToArray()); var encoded=NomBackupCodec.Encode(manifest);
            backupTransfer.Write(Path.Combine(staging,"owner.lock"),[],token); backupTransfer.Write(Path.Combine(staging,"nom.backup"),encoded,token);
            backupTransfer.Verify(Path.Combine(staging,"nom.backup"),encoded.Length,ResourceCodec.Hash(encoded),token);
            VerifyBackupCatalog(staging,manifest,token); backupTransfer.CloseAll(); backupTransfer=null;
            token.ThrowIfCancellationRequested(); publishing=true; files.PublishBackupDirectory(staging,destination);
            return new(destination,Header.ProjectId,Header.StoreId,kvStore.Sequence,Header.Generation,entries.Count,bytes,ResourceCodec.Hash(encoded));
        }
        catch(Exception error) { throw BackupFailure(staging,publishing,error); }
    }
    private Exception BackupFailure(string staging,bool publishing,Exception error)
    {
        var failures=new List<Exception> { error };
        try { backupVerification?.Dispose(); backupVerification=null; } catch(Exception close) { failures.Add(close); }
        try { backupTransfer?.CloseAll(); backupTransfer=null; } catch(Exception close) { failures.Add(close); }
        if(backupTransfer!=null || backupVerification!=null) resourceRecoveryRequired=true;
        if(error is OperationCanceledException && !publishing && failures.Count==1)
        { error.Data["NomBackupStagingRoot"]=staging; return error; }
        return new NomBackupException(staging,publishing,failures.Count==1 ? error : new AggregateException("Transfer and cleanup failures.",failures));
    }
    private void VerifyBackupCatalog(string root,NomBackupManifest manifest,CancellationToken token)
    {
        backupVerification=new NomKvStoreFactory(files,1);
        var store=backupVerification.Open(new(root,manifest.ProjectId,manifest.StoreId,Token:token,RepairIncompleteTail:false,AllowBackupRoot:true));
        Require(!store.HasPendingTail && store.Sequence==manifest.Sequence && store.Generation==manifest.Generation,"Backup authority sequence/generation/tail mismatch.");
        var catalog=new NomCatalog(store,limits.DomainIndexBytes); catalog.ValidateAll(); catalog.ValidateJournal();
        Require(catalog.Garbage().Length==0 && catalog.Operations().All(op => op.State!="pending"),"Backup contains unfinished maintenance/reservation.");
        var selection=BackupSelection(store,catalog);
        Require(selection.Length==manifest.Entries.Length,"Backup selected file closure.");
        for(var n=0;n<selection.Length;n++)
        {
            var expected=selection[n]; var actual=manifest.Entries[n];
            Require(expected.Path==actual.Path && expected.Length==actual.Length && (!expected.Path.StartsWith("objects/",StringComparison.Ordinal) || expected.Digest==actual.Digest),"Backup file authority/identity mismatch.");
        }
        backupVerification.Dispose(); backupVerification=null;
    }
    private void RestoreBackup(RestoreAssetStoreArguments restore)
    {
        var source=NomBackupTransfer.LocalRoot(restore.BackupRoot); var destination=NomBackupTransfer.LocalRoot(Root); var options=(restore.Options??new()).Snapshot();
        var staging=NomBackupTransfer.PrepareDestination(source,destination); var publishing=false; backupTransfer=new(files);
        try
        {
            var manifest=NomBackupCodec.Decode(backupTransfer.ReadManifest(source));
            Require(manifest.ProjectId==arguments.ProjectId && manifest.StoreId==arguments.StoreId,"Backup restore identities do not match."); _=BackupBudget(manifest.Entries,options);
            foreach(var entry in manifest.Entries) { var path=NomBackupTransfer.Resolve(source,entry.Path); NomBackupTransfer.CheckPath(path); Require(new FileInfo(path).Length==entry.Length,"Missing/changed backup file."); }
            arguments.Token.ThrowIfCancellationRequested(); files.CreateBackupDirectory(staging);
            foreach(var entry in manifest.Entries)
                backupTransfer.Copy(NomBackupTransfer.Resolve(source,entry.Path),NomBackupTransfer.Resolve(staging,entry.Path),entry.Path,entry.Length,entry.Digest,arguments.Token);
            backupTransfer.Write(Path.Combine(staging,"owner.lock"),[],arguments.Token); VerifyBackupCatalog(staging,manifest,arguments.Token); backupTransfer.CloseAll(); backupTransfer=null;
            arguments.Token.ThrowIfCancellationRequested(); publishing=true; files.PublishBackupDirectory(staging,destination); RestorePublished=true;
        }
        catch(Exception error) { throw BackupFailure(staging,publishing,error); }
    }
}
