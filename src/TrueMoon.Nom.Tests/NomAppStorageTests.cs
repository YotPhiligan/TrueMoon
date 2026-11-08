using Xunit;
using TrueMoon.Nom.Catalog;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomAppStorageTests
{
    [Fact]
    public async Task RegisteredFactory_PublicStreamRoundtripReopenAndJournal_PreserveExactContent()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());var factory=NomTestDirectory.Factory(app);
        var metadata=new byte[]{7,8,9};var sourceId=Guid.NewGuid();var kindId=Guid.NewGuid();
        var asset=new AssetRecord(Guid.NewGuid(),sourceId,kindId,"triangle",metadata);metadata[0]=0;
        var bytes="GEO1\u0001\u0002\u0003resource"u8.ToArray();using var input=new ShortSource(bytes);
        var owner=factory.Create(args);
        var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(asset,bytes.Length,NomTestDirectory.Digest(bytes),bytes.Length)]);
        var receipt=await owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,input}});
        var version=Assert.Single(receipt.Resources);
        Assert.Equal(1,receipt.Generation);Assert.Equal(1,owner.ContentGeneration);Assert.False(receipt.CleanupPending);
        Assert.Equal(asset.Id,version.AssetId);Assert.Equal(kindId,version.KindId);Assert.Equal(bytes.Length,version.Length);
        Assert.Equal(NomTestDirectory.Digest(bytes),version.Digest);Assert.Equal(version,await owner.FindResourceAsync(asset.Id));
        Assert.False(input.WasDisposed);Assert.Equal(0,input.LengthRequests);Assert.Equal(0,input.SeekRequests);Assert.True(input.Reads>3);
        var found=Assert.Single(await owner.ReadAsync());var detached=found.Metadata;detached[0]=0;
        Assert.Equal((asset.Id,sourceId,kindId,"triangle"),(found.Id,found.Source,found.Kind,found.Locator));
        Assert.Equal(new byte[]{7,8,9},found.Metadata);Assert.Equal(new byte[]{7,8,9},asset.Metadata);
        var lookup=await owner.LookupResourceOperationAsync(ticket);
        Assert.Equal("committed",lookup.Status);Assert.Equal(1,lookup.Generation);Assert.Equal(version,Assert.Single(lookup.Resources));
        await using(var stream=await owner.OpenResourceAsync(version))
        {
            Assert.True(stream.CanRead);Assert.True(stream.CanSeek);Assert.False(stream.CanWrite);
            using var output=new MemoryStream();await stream.CopyToAsync(output);Assert.Equal(bytes,output.ToArray());
            Assert.Equal(4,stream.Seek(4,SeekOrigin.Begin));Assert.Equal(bytes[4],stream.ReadByte());
        }
        await owner.DisposeAsync();Assert.True(owner.IsClosed);Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);
        await using var reopened=factory.Create(args with {Mode=AssetStoreOpenMode.Open});
        Assert.Equal(args.ProjectId,reopened.ProjectId);Assert.Equal(args.StoreId,reopened.StoreId);
        Assert.Equal(Path.GetFullPath(args.Root),reopened.Root);Assert.Equal(1,reopened.ContentGeneration);
        Assert.Equal(version,await reopened.FindResourceAsync(asset.Id));
        await using(var stream=await reopened.OpenResourceAsync(version))
        { using var output=new MemoryStream();await stream.CopyToAsync(output);Assert.Equal(bytes,output.ToArray()); }
        var pending=await reopened.ReserveResourcesAsync(1,[new AssetResourceWrite(NomTestDirectory.Record("aborted"))]);
        await reopened.AbortResourceOperationAsync(pending);Assert.Equal("aborted",(await reopened.LookupResourceOperationAsync(pending)).Status);
        await reopened.PruneResourceOperationsAsync(pending.Sequence);
        await Assert.ThrowsAsync<ExpiredOperationException>(()=>reopened.LookupResourceOperationAsync(pending));
        Assert.Equal(1,reopened.ContentGeneration);Assert.Single(await reopened.ReadAsync());
    }

    [Fact]
    public async Task ConsumerDispose_ReleasesRootAndRegistryBeforeAppDisposal()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());var factory=NomTestDirectory.Factory(app);var host=NomTestDirectory.Host(app);
        var owner=factory.Create(args);Assert.Equal(1,host.ActiveStoreCount);NomTestDirectory.AssertRootLocked(args);
        owner.Dispose();owner.Dispose();await owner.DisposeAsync();
        Assert.True(owner.IsClosed);Assert.Equal(0,host.ActiveStoreCount);Assert.False(host.IsReleased);
        await Assert.ThrowsAsync<ObjectDisposedException>(async()=>await owner.ReadAsync());
        NomTestDirectory.AssertRootReleased(args);
        await using var reopened=factory.Create(args with {Mode=AssetStoreOpenMode.Open});
        Assert.Equal(1,host.ActiveStoreCount);Assert.Empty(await reopened.ReadAsync());
    }

    [Theory]
    [InlineData("null-root")]
    [InlineData("empty-root")]
    [InlineData("relative-root")]
    [InlineData("unc-root")]
    [InlineData("forward-unc-root")]
    [InlineData("project-id")]
    [InlineData("store-id")]
    [InlineData("mode")]
    public async Task Factory_InvalidArguments_RejectBeforeFilesystemOrCapacityAdmission(string invalid)
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());var factory=NomTestDirectory.Factory(app);
        var bad=invalid switch
        {
            "null-root"=>args with{Root=null!},"empty-root"=>args with{Root=" "},"relative-root"=>args with{Root="relative/nom"},
            "unc-root"=>args with{Root=@"\\server\share\nom"},"forward-unc-root"=>args with{Root="//server/share/nom"},"project-id"=>args with{ProjectId=Guid.Empty},
            "store-id"=>args with{StoreId=Guid.Empty},_=>args with{Mode=(AssetStoreOpenMode)999}
        };
        Assert.ThrowsAny<ArgumentException>(()=>factory.Create(bad));
        Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);Assert.False(Directory.Exists(args.Root));
    }

    [Fact]
    public async Task Factory_CancelledCreation_RejectsBeforeCreatingAndCanCreateLater()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();using var cancel=new CancellationTokenSource();cancel.Cancel();
        await using var app=App.Build(context=>context.UseNom());var factory=NomTestDirectory.Factory(app);
        var error=Assert.Throws<OperationCanceledException>(()=>factory.Create(args with{CancellationToken=cancel.Token}));
        Assert.Equal(cancel.Token,error.CancellationToken);Assert.False(Directory.Exists(args.Root));Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);
        await using var owner=factory.Create(args);Assert.False(owner.IsClosed);Assert.Equal(1,NomTestDirectory.Host(app).ActiveStoreCount);
    }

    [Fact]
    public async Task Factory_FailedOpen_ReleasesFailedOwnerAndPreservesExistingCatalog()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());var factory=NomTestDirectory.Factory(app);var host=NomTestDirectory.Host(app);
        Assert.Throws<ArgumentNullException>(()=>factory.Create(null!));
        Assert.Throws<DirectoryNotFoundException>(()=>factory.Create(args with{Mode=AssetStoreOpenMode.Open}));
        Assert.False(Directory.Exists(args.Root));Assert.Equal(0,host.ActiveStoreCount);
        using(var owner=factory.Create(args)) Assert.Equal(0,owner.ContentGeneration);
        var file=Path.Combine(args.Root,"nom.root");var hash=NomTestDirectory.Digest(File.ReadAllBytes(file));
        Assert.Throws<InvalidDataException>(()=>factory.Create(args with{Mode=AssetStoreOpenMode.Open,StoreId=Guid.NewGuid()}));
        Assert.Equal(hash,NomTestDirectory.Digest(File.ReadAllBytes(file)));Assert.Equal(0,host.ActiveStoreCount);
        NomTestDirectory.AssertRootReleased(args);
        await using var reopened=factory.Create(args with{Mode=AssetStoreOpenMode.Open});Assert.Equal(0,reopened.ContentGeneration);
    }

    [Fact]
    public async Task Factory_CorruptCatalog_RejectsWithoutResetAndReleasesOwner()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());
        using(var owner=NomTestDirectory.Factory(app).Create(args)) Assert.Equal(0,owner.ContentGeneration);
        var file=Path.Combine(args.Root,"nom.root");var corrupt=File.ReadAllBytes(file);corrupt[0]^=1;File.WriteAllBytes(file,corrupt);
        var hash=NomTestDirectory.Digest(corrupt);
        Assert.Throws<InvalidDataException>(()=>NomTestDirectory.Factory(app).Create(args with{Mode=AssetStoreOpenMode.Open}));
        Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);Assert.Equal(hash,NomTestDirectory.Digest(File.ReadAllBytes(file)));
        NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public async Task FactoryCapacity_BeforeAtAndAboveLimit_ReusesReleasedSlot()
    {
        using var directory=new NomTestDirectory();
        await using var app=App.Build(context=>context.UseNom(options=>options.MaximumOpenStores=2));
        var factory=NomTestDirectory.Factory(app);var host=NomTestDirectory.Host(app);
        var first=factory.Create(directory.Arguments("first"));Assert.Equal(1,host.ActiveStoreCount);
        await using var second=factory.Create(directory.Arguments("second"));Assert.Equal(2,host.ActiveStoreCount);
        var thirdArgs=directory.Arguments("third");Assert.Throws<InvalidOperationException>(()=>factory.Create(thirdArgs));
        Assert.False(Directory.Exists(thirdArgs.Root));Assert.Equal(2,host.ActiveStoreCount);
        await first.DisposeAsync();Assert.Equal(1,host.ActiveStoreCount);
        await using var third=factory.Create(thirdArgs);Assert.Equal(2,host.ActiveStoreCount);Assert.Empty(await third.ReadAsync());
    }

    [Fact]
    public async Task PublicPublication_BindingConflict_IsIoErrorAndKeepsCommittedStateIntact()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());await using var owner=NomTestDirectory.Factory(app).Create(args);
        var seed=NomTestDirectory.Record("unique-binding");var bytes=new byte[]{1,2,3};
        using var firstInput=new MemoryStream(bytes,false);
        var first=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(seed)]);
        var committed=Assert.Single((await owner.PublishResourcesAsync(first,new Dictionary<Guid,Stream>{{seed.Id,firstInput}})).Resources);
        var duplicate=new AssetRecord(Guid.NewGuid(),seed.Source,seed.Kind,seed.Locator,[4]);using var secondInput=new MemoryStream(new byte[]{4},false);
        var ticket=await owner.ReserveResourcesAsync(1,[new AssetResourceWrite(duplicate)]);
        await Assert.ThrowsAsync<IOException>(()=>owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{duplicate.Id,secondInput}}));
        Assert.Equal(1,owner.ContentGeneration);Assert.Equal(seed.Id,Assert.Single(await owner.ReadAsync()).Id);
        Assert.Equal(committed,await owner.FindResourceAsync(seed.Id));Assert.Null(await owner.FindResourceAsync(duplicate.Id));
        Assert.Equal("aborted",(await owner.LookupResourceOperationAsync(ticket)).Status);
        await using var stream=await owner.OpenResourceAsync(committed);using var output=new MemoryStream();await stream.CopyToAsync(output);Assert.Equal(bytes,output.ToArray());
    }

    [Fact]
    public async Task PublicStore_NullRequests_AreRejectedWithoutReservingOrChangingGeneration()
    {
        using var directory=new NomTestDirectory();
        await using var app=App.Build(context=>context.UseNom());await using var owner=NomTestDirectory.Factory(app).Create(directory.Arguments());
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.ReserveResourcesAsync(0,null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.ReserveResourcesAsync(0,[null!]));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(null!)]));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.PublishResourcesAsync(null!,new Dictionary<Guid,Stream>()));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.PublishResourcesAsync(new AssetWriteTicket(owner.StoreId,1,new string('0',64)),null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.LookupResourceOperationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.AbortResourceOperationAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(async()=>await owner.OpenResourceAsync(null!));
        Assert.Equal(0,owner.ContentGeneration);Assert.Empty(await owner.ReadAsync());
        Assert.Equal(2,(await NomTestDirectory.Publish(owner,[4,5])).Length);
    }
}
