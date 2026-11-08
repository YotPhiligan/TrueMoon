using Xunit;
using TrueMoon.Nom.Backup;

namespace TrueMoon.Nom.Tests;

public sealed class Nom6BackupFaultTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task BackupTransfer_PreCancelledAdmissionCreatesNoStageOrFileIo(bool restore)
    {
        await using var context=new Nom6Context(); if(restore)await context.Store.ExportBackupAsync(context.Backup);
        var before=Nom5Context.Snapshot(context.Arguments.Root); using var cancel=new CancellationTokenSource(); cancel.Cancel(); context.Io.Events.Clear();
        if(restore)Assert.ThrowsAny<OperationCanceledException>(()=>context.RestoreFactory.Create(context.RestoreArguments() with { CancellationToken=cancel.Token }));
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>context.Store.ExportBackupAsync(context.Backup,token:cancel.Token));
        Assert.Empty(context.Io.Events); Assert.False(Directory.Exists(restore?context.Restored:context.Backup)); Nom5Context.AssertSnapshot(before,context.Arguments.Root);
    }

    [Theory]
    [InlineData(false,"write-zero")][InlineData(false,"write-partial")][InlineData(false,"write-full")][InlineData(false,"flush-before")][InlineData(false,"flush-after")][InlineData(false,"close-before")][InlineData(false,"close-after")]
    [InlineData(true,"write-zero")][InlineData(true,"write-partial")][InlineData(true,"write-full")][InlineData(true,"flush-before")][InlineData(true,"flush-after")][InlineData(true,"close-before")][InlineData(true,"close-after")]
    public async Task BackupTransfer_PrepublicationIoFailureRetainsDiagnosticStageAndNeverPublishesOrChangesSource(bool restore,string mode)
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2,3]);
        if(restore) await context.Store.ExportBackupAsync(context.Backup);
        var source=restore ? context.Backup : context.Arguments.Root; var before=Nom5Context.Snapshot(source); var failure=new IOException("transfer IO "+mode);
        context.Io.Arm(mode.StartsWith("flush",StringComparison.Ordinal) ? "flush" : mode.StartsWith("close",StringComparison.Ordinal) ? "close" : "write",failure,
            pathContains:".nom-",prefix:mode=="write-partial" ? 13 : 0,after:mode is "write-full" or "flush-after" or "close-after");
        NomBackupException error;
        if(restore) error=Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(context.RestoreArguments()));
        else error=await Assert.ThrowsAsync<NomBackupException>(()=>context.Store.ExportBackupAsync(context.Backup));
        Assert.False(error.PublicationAttempted); Assert.Same(failure,error.InnerException); Assert.True(Directory.Exists(error.StagingRoot));
        Assert.False(Directory.Exists(restore ? context.Restored : context.Backup)); Nom5Context.AssertSnapshot(before,source); context.Io.Disarm();
        Assert.DoesNotContain(context.Io.Opened,f=>f.Path.Contains(".nom-",StringComparison.Ordinal) && !f.Handle.IsClosed);
        Assert.Equal(1,context.Store.ContentGeneration); await NomFaultContext.AssertBytes(context.Store,resource.Version,[1,2,3]);
        var next=Path.Combine(context.Directory.Root,"retry");
        if(restore) { await using var result=context.RestoreFactory.Create(context.RestoreArguments(next)); Assert.Equal(resource.Version,await result.FindResourceAsync(resource.Version.AssetId)); }
        else Assert.Equal(1,(await context.Store.ExportBackupAsync(next)).Generation);
    }

    [Theory]
    [InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task BackupTransfer_PublicationFailurePreservesWholePreparedOrPublishedStateWithoutReset(bool restore,bool after)
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2]); if(restore) await context.Store.ExportBackupAsync(context.Backup);
        var failure=new IOException("directory publication acknowledgement"); context.Io.Arm("publish-backup",failure,after:after);
        var error=restore ? Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(context.RestoreArguments()))
            : await Assert.ThrowsAsync<NomBackupException>(()=>context.Store.ExportBackupAsync(context.Backup));
        Assert.True(error.PublicationAttempted); Assert.Same(failure,error.InnerException); context.Io.Disarm();
        var destination=restore ? context.Restored : context.Backup; Assert.Equal(after,Directory.Exists(destination)); Assert.Equal(!after,Directory.Exists(error.StagingRoot));
        if(after)
        {
            await using var recovered=restore ? context.Factory.Create(context.Arguments with { Root=destination,Mode=AssetStoreOpenMode.Open })
                : context.RestoreFactory.Create(context.RestoreArguments());
            Assert.Equal(resource.Version,await recovered.FindResourceAsync(resource.Version.AssetId)); await NomFaultContext.AssertBytes(recovered,resource.Version,[1,2]);
        }
        await NomFaultContext.AssertBytes(context.Store,resource.Version,[1,2]);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task BackupTransfer_CancellationDuringCopyKeepsDestinationAbsentAndSourceUnchanged(bool restore)
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1,2]); if(restore) await context.Store.ExportBackupAsync(context.Backup);
        var source=restore ? context.Backup : context.Arguments.Root; var before=Nom5Context.Snapshot(source); using var cancel=new CancellationTokenSource();
        context.Io.After=(point,path)=>{if(point=="write" && path.Contains(".nom-",StringComparison.Ordinal)) cancel.Cancel();};
        OperationCanceledException error;
        if(restore) error=Assert.ThrowsAny<OperationCanceledException>(()=>context.RestoreFactory.Create(context.RestoreArguments() with { CancellationToken=cancel.Token }));
        else error=await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>context.Store.ExportBackupAsync(context.Backup,token:cancel.Token));
        Assert.Equal(cancel.Token,error.CancellationToken); Assert.IsType<string>(error.Data["NomBackupStagingRoot"]); context.Io.Disarm();
        Assert.False(Directory.Exists(restore ? context.Restored : context.Backup)); Nom5Context.AssertSnapshot(before,source);
        Assert.DoesNotContain(context.Io.Opened,f=>f.Path.Contains(".nom-",StringComparison.Ordinal) && !f.Handle.IsClosed);
    }

    [Fact]
    public async Task Export_CancellationAfterPublicationReturnsAcknowledgedBackup()
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1]); using var cancel=new CancellationTokenSource();
        context.Io.After=(point,_)=>{if(point=="publish-backup") cancel.Cancel();}; var receipt=await context.Store.ExportBackupAsync(context.Backup,token:cancel.Token); context.Io.Disarm();
        Assert.True(cancel.IsCancellationRequested); Assert.Equal(context.Backup,receipt.Root); Assert.True(File.Exists(Path.Combine(receipt.Root,"nom.backup")));
        await using var restored=context.RestoreFactory.Create(context.RestoreArguments()); Assert.Equal(1,restored.ContentGeneration);
    }

    [Fact]
    public async Task Restore_CancellationAfterPublicationReturnsUsableOwnedStoreInsteadOfRelabelingAcknowledgedCopy()
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2]); await context.Store.ExportBackupAsync(context.Backup);
        using var cancel=new CancellationTokenSource(); context.Io.After=(point,_)=>{if(point=="publish-backup")cancel.Cancel();};
        await using var restored=context.RestoreFactory.Create(context.RestoreArguments() with { CancellationToken=cancel.Token }); context.Io.Disarm();
        Assert.True(cancel.IsCancellationRequested); Assert.Equal(context.Restored,restored.Root); Assert.Equal(1,restored.ContentGeneration);
        Assert.Equal(resource.Version,await restored.FindResourceAsync(resource.Version.AssetId)); await NomFaultContext.AssertBytes(restored,resource.Version,[1,2]);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task BackupTransfer_PreparedFileCorruptionAfterActualCloseCannotPublish(bool restore)
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1]); if(restore)await context.Store.ExportBackupAsync(context.Backup);
        var source=restore?context.Backup:context.Arguments.Root; var before=Nom5Context.Snapshot(source); var changed=false;
        context.Io.After=(point,path)=>
        {
            if(!changed && point=="close" && path.Contains(".nom-",StringComparison.Ordinal) && Path.GetFileName(path)=="nom.root")
            {changed=true;var bytes=File.ReadAllBytes(path);bytes[0]^=1;File.WriteAllBytes(path,bytes);}
        };
        var error=restore ? Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(context.RestoreArguments()))
            : await Assert.ThrowsAsync<NomBackupException>(()=>context.Store.ExportBackupAsync(context.Backup)); context.Io.Disarm();
        Assert.True(changed); Assert.False(error.PublicationAttempted); Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.False(Directory.Exists(restore?context.Restored:context.Backup)); Nom5Context.AssertSnapshot(before,source);
    }

    [Fact]
    public async Task Export_StopWaitsForEnteredCopyAndReleasesAllHandlesOnlyAfterPublication()
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2,3]); var gate=new NomFaultGate();
        context.Io.Before=(point,path)=>
        {if(point=="write" && path.Contains(".nom-",StringComparison.Ordinal)) {gate.Entered.TrySetResult();if(!gate.Release.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("copy gate");}};
        var exporting=context.Store.ExportBackupAsync(context.Backup); Task? stopping=null; var host=NomTestDirectory.Host(context.App);
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); stopping=host.StopAsync(); Assert.False(exporting.IsCompleted); Assert.False(stopping.IsCompleted);
            Assert.False(context.Store.IsClosed); Assert.Equal(1,host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(context.Arguments); Assert.False(Directory.Exists(context.Backup));
        }
        finally {gate.Release.Set();}
        var receipt=await exporting.WaitAsync(TimeSpan.FromSeconds(5)); await stopping!.WaitAsync(TimeSpan.FromSeconds(5)); context.Io.Disarm();
        Assert.Equal(1,receipt.Generation); Assert.True(context.Store.IsClosed); Assert.True(host.IsReleased); Nom6Context.AssertActualHandlesClosed(context.Io); NomTestDirectory.AssertRootReleased(context.Arguments);
        await using var other=new Nom6Context(); await using var restored=other.RestoreFactory.Create(context.RestoreArguments()); await NomFaultContext.AssertBytes(restored,resource.Version,[1,2,3]);
    }

    [Fact]
    public async Task Export_FailedActualCloseRetainsOwnerRootAndOriginalCausesUntilExplicitDrainRetry()
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1,2]); var closeFailure=new IOException("retained actual handle close"); var retain=true;
        context.Io.Before=(point,path)=>{if(point=="close" && path.Contains(".nom-",StringComparison.Ordinal) && Volatile.Read(ref retain))throw closeFailure;};
        var error=await Assert.ThrowsAsync<NomBackupException>(()=>context.Store.ExportBackupAsync(context.Backup));
        Assert.False(error.PublicationAttempted); Assert.IsType<AggregateException>(error.InnerException); Assert.Contains(Flatten(error),e=>ReferenceEquals(e,closeFailure));
        Assert.Contains(context.Io.Opened,f=>f.Path.Contains(".nom-",StringComparison.Ordinal) && !f.Handle.IsClosed); var host=NomTestDirectory.Host(context.App);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(()=>context.Store.ReadAsync());
            var stopError=await Record.ExceptionAsync(()=>host.StopAsync()); Assert.NotNull(stopError); Assert.Contains(Flatten(stopError),e=>ReferenceEquals(e,closeFailure));
            Assert.False(context.Store.IsClosed); Assert.False(host.IsReleased); Assert.Equal(1,host.ActiveStoreCount); NomTestDirectory.AssertRootLocked(context.Arguments);
        }
        finally {Volatile.Write(ref retain,false);context.Io.Disarm();await host.StopAsync();}
        Assert.True(context.Store.IsClosed); Assert.True(host.IsReleased); Nom6Context.AssertActualHandlesClosed(context.Io); NomTestDirectory.AssertRootReleased(context.Arguments);
    }

    private static IEnumerable<Exception> Flatten(Exception error)
    {yield return error;if(error is AggregateException aggregate){foreach(var item in aggregate.InnerExceptions)foreach(var inner in Flatten(item))yield return inner;}
        else if(error.InnerException is { } cause)foreach(var inner in Flatten(cause))yield return inner;}
}
