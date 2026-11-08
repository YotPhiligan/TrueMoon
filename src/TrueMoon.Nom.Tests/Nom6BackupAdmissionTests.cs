using Xunit;
using TrueMoon.Nom.Backup;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Storage;

namespace TrueMoon.Nom.Tests;

public sealed class Nom6BackupAdmissionTests
{
    [Fact]
    public async Task BackupTransfer_ExactFileAndByteCeilingsAcceptAndImmediatelyLowerLimitsRefuseWithoutPublication()
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1,2,3]);
        var reference=await context.Store.ExportBackupAsync(context.Backup); var before=Nom5Context.Snapshot(context.Arguments.Root);
        foreach(var options in new[]{new NomBackupOptions(reference.Files-1,reference.Bytes),new NomBackupOptions(reference.Files,reference.Bytes-1)})
        {
            var destination=Path.Combine(context.Directory.Root,"refused-"+Guid.NewGuid().ToString("N"));
            await Assert.ThrowsAsync<InvalidDataException>(()=>context.Store.ExportBackupAsync(destination,options)); Assert.False(Directory.Exists(destination)); Nom5Context.AssertSnapshot(before,context.Arguments.Root);
            var restore=Path.Combine(context.Directory.Root,"restore-refused-"+Guid.NewGuid().ToString("N"));
            Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(context.RestoreArguments(restore,options:options))); Assert.False(Directory.Exists(restore));
        }
        var exactRoot=Path.Combine(context.Directory.Root,"exact"); var exact=await context.Store.ExportBackupAsync(exactRoot,new(reference.Files,reference.Bytes));
        Assert.Equal(reference.Files,exact.Files); Assert.Equal(reference.Bytes,exact.Bytes); Assert.Equal(reference.ManifestDigest,exact.ManifestDigest);
        await using var restored=context.RestoreFactory.Create(context.RestoreArguments(options:new(reference.Files,reference.Bytes)));
        Assert.Equal(1,restored.ContentGeneration); await NomFaultContext.AssertBytes(restored,(await restored.FindResourceAsync(Nom5Context.Id(1)))!,[1,2,3]);
    }

    [Theory]
    [InlineData("same")][InlineData("inside")][InlineData("ancestor")][InlineData("existing-empty")][InlineData("existing-file")][InlineData("parent-missing")]
    public async Task Export_InvalidDestinationNeverOverwritesSourceOrCallerFiles(string invalid)
    {
        await using var context=new Nom6Context(); await context.Publish(Nom5Context.Asset(1),[1]);
        var target=invalid switch { "same"=>context.Arguments.Root,"inside"=>Path.Combine(context.Arguments.Root,"nested"),"ancestor"=>context.Directory.Root,
            "parent-missing"=>Path.Combine(context.Directory.Root,"missing-parent","backup"),_=>context.Backup };
        if(invalid=="existing-empty") Directory.CreateDirectory(target);
        if(invalid=="existing-file") File.WriteAllBytes(target,[9,8,7]);
        var before=Nom5Context.Snapshot(context.Arguments.Root); context.Io.Events.Clear();
        await Assert.ThrowsAsync<InvalidDataException>(()=>context.Store.ExportBackupAsync(target)); Nom5Context.AssertSnapshot(before,context.Arguments.Root);
        Assert.DoesNotContain(context.Io.Events,e=>e.Point is "write" or "create-backup" or "publish-backup");
        if(invalid=="existing-empty") Assert.Empty(Directory.EnumerateFileSystemEntries(target));
        if(invalid=="existing-file") Assert.Equal(new byte[]{9,8,7},File.ReadAllBytes(target));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Export_MissingOrCorruptReferencedCasCannotPublishBackupOrMutateCatalog(bool missing)
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2,3]); var path=Nom5Context.ObjectPath(context.Arguments.Root,resource.Version.Digest);
        if(missing) File.Delete(path); else File.WriteAllBytes(path,[4,5,6]); var before=Nom5Context.Snapshot(context.Arguments.Root);
        await Assert.ThrowsAnyAsync<IOException>(()=>context.Store.ExportBackupAsync(context.Backup)); Assert.False(Directory.Exists(context.Backup)); Nom5Context.AssertSnapshot(before,context.Arguments.Root);
        Assert.Equal(resource.Version,await context.Store.FindResourceAsync(resource.Version.AssetId));
        File.WriteAllBytes(path,[1,2,3]); Assert.True((await context.Store.ExportBackupAsync(context.Backup)).Files>=3);
    }

    [Theory]
    [InlineData("manifest")][InlineData("file-missing")][InlineData("file-hash")][InlineData("foreign-project")][InlineData("foreign-store")][InlineData("future-format")]
    public async Task Restore_InvalidBackupRejectsBeforeDestinationPublicationAndLeavesArchiveUnchanged(string invalid)
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2,3]); await context.Store.ExportBackupAsync(context.Backup);
        var manifestPath=Path.Combine(context.Backup,"nom.backup"); var arguments=context.RestoreArguments();
        if(invalid=="manifest") { var bytes=File.ReadAllBytes(manifestPath);bytes[^1]^=1;File.WriteAllBytes(manifestPath,bytes); }
        if(invalid=="future-format") { var bytes=File.ReadAllBytes(manifestPath);bytes[8]=2;File.WriteAllBytes(manifestPath,bytes); }
        if(invalid=="file-missing") File.Delete(Nom5Context.ObjectPath(context.Backup,resource.Version.Digest));
        if(invalid=="file-hash") File.WriteAllBytes(Nom5Context.ObjectPath(context.Backup,resource.Version.Digest),[4,5,6]);
        if(invalid=="foreign-project") arguments=arguments with { ProjectId=Guid.NewGuid() };
        if(invalid=="foreign-store") arguments=arguments with { StoreId=Guid.NewGuid() };
        var before=Nom5Context.Snapshot(context.Backup); var error=Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(arguments));
        Assert.False(error.PublicationAttempted); Assert.False(Directory.Exists(context.Restored)); Nom5Context.AssertSnapshot(before,context.Backup);
        Assert.Equal(1,NomTestDirectory.Host(context.App).ActiveStoreCount); await NomFaultContext.AssertBytes(context.Store,resource.Version,[1,2,3]);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Restore_TransportAuthenticatedDomainOrReferenceClosureCorruptionFailsBeforeFinalRootExists(bool domain)
    {
        await using var context=new Nom6Context(); var resource=await context.Publish(Nom5Context.Asset(1),[1,2,3]); await context.Store.ExportBackupAsync(context.Backup);
        var manifestPath=Path.Combine(context.Backup,"nom.backup"); var manifest=NomBackupCodec.Decode(File.ReadAllBytes(manifestPath));
        if(domain)
        {
            using var factory=new NomKvStoreFactory();
            using var raw=factory.Open(new(context.Backup,context.Arguments.ProjectId,context.Arguments.StoreId,AllowBackupRoot:true));
            var header=NomCatalogRecordCodec.DecodeHeader(raw.Get(NomCatalogKeys.Header)!);
            raw.WriteBatch(new(raw.Sequence+1,raw.Generation,raw.Generation,Guid.NewGuid(),[new(NomCatalogKeys.Header,NomCatalogRecordCodec.EncodeHeader(header with { ProjectId=Guid.NewGuid() }))]));
            manifest=manifest with { Sequence=raw.Sequence };
        }
        var entries=manifest.Entries.Where(e=>domain || !e.Path.StartsWith("objects/",StringComparison.Ordinal)).Select(e=>
        { var bytes=File.ReadAllBytes(NomBackupTransfer.Resolve(context.Backup,e.Path));return new NomBackupEntry(e.Path,bytes.Length,NomTestDirectory.Digest(bytes)); }).ToArray();
        File.WriteAllBytes(manifestPath,NomBackupCodec.Encode(manifest with { Entries=entries })); var before=Nom5Context.Snapshot(context.Backup);
        var error=Assert.Throws<NomBackupException>(()=>context.RestoreFactory.Create(context.RestoreArguments()));
        Assert.False(error.PublicationAttempted); Assert.True(Directory.Exists(error.StagingRoot)); Assert.False(Directory.Exists(context.Restored));
        Nom5Context.AssertSnapshot(before,context.Backup); Assert.Equal(resource.Version,await context.Store.FindResourceAsync(resource.Version.AssetId));
    }

    [Fact]
    public async Task Restore_ExistingDestinationIsRejectedWithoutResetIncludingAnEmptyDirectory()
    {
        await using var context=new Nom6Context(); await context.Store.ExportBackupAsync(context.Backup); Directory.CreateDirectory(context.Restored);
        Assert.Throws<InvalidDataException>(()=>context.RestoreFactory.Create(context.RestoreArguments())); Assert.Empty(Directory.EnumerateFileSystemEntries(context.Restored));
        File.WriteAllBytes(Path.Combine(context.Restored,"sentinel"),[7]);
        Assert.Throws<InvalidDataException>(()=>context.RestoreFactory.Create(context.RestoreArguments())); Assert.Equal(new byte[]{7},File.ReadAllBytes(Path.Combine(context.Restored,"sentinel")));
    }
}
