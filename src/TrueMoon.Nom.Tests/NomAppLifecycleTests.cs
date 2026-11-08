using TrueMoon.Dependencies;
using Xunit;
using TrueMoon.Nom.Hosting;
using TrueMoon.Nom.Resources;

namespace TrueMoon.Nom.Tests;

public sealed class NomAppLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppRunAsync_ConsumerSignalsShutdown_DrainsAndDisposesOwnedStores(bool externalCancellation)
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();using var cancellation=new CancellationTokenSource();
        NomStoreHost? host=null;OpenAndStop? consumer=null;
        var run=App.RunAsync(context=>
        {
            context.UseNom();
            context.Services(services=>services.Singleton<IStartable>(resolver=>
            {
                host=resolver.Resolve<NomStoreHost>();var lifetime=resolver.Resolve<IAppLifetime>()!;
                return consumer=new OpenAndStop(resolver.Resolve<IFactory<IProjectAssetStore,OpenAssetStoreArguments>>()!,args,
                    externalCancellation?cancellation.Cancel:lifetime.Cancel);
            }));
        },cancellation.Token);
        if(externalCancellation)
        {
            var error=await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(cancellation.Token,error.CancellationToken);
        }
        else await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(consumer);Assert.NotNull(consumer.Opened);Assert.True(consumer.Opened.IsClosed);
        Assert.NotNull(host);Assert.True(host.IsReleased);Assert.Equal(0,host.ActiveStoreCount);NomTestDirectory.AssertRootReleased(args);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppStopThenDispose_DrainsOutstandingStoresAndKeepsCleanupIdempotent(bool start)
    {
        using var directory=new NomTestDirectory();var leftArgs=directory.Arguments("left");var rightArgs=directory.Arguments("right");
        var app=App.Build(context=>context.UseNom());var host=NomTestDirectory.Host(app);var factory=NomTestDirectory.Factory(app);
        try
        {
            if(start) await app.StartAsync();
            var left=factory.Create(leftArgs);var right=factory.Create(rightArgs);
            Assert.Equal(2,host.ActiveStoreCount);await app.StopAsync();await app.StopAsync();
            Assert.True(left.IsClosed);Assert.True(right.IsClosed);Assert.Equal(0,host.ActiveStoreCount);Assert.True(host.IsReleased);
            NomTestDirectory.AssertRootReleased(leftArgs);NomTestDirectory.AssertRootReleased(rightArgs);
            Assert.Throws<ObjectDisposedException>(()=>factory.Create(directory.Arguments("too-late")));
            await app.DisposeAsync();await app.DisposeAsync();Assert.Equal(0,host.ActiveStoreCount);Assert.True(host.IsReleased);
        }
        finally { await app.DisposeAsync(); }
    }

    [Fact]
    public async Task AppDisposeWithoutStart_DrainsResolvedStoreThroughOwnedRoot()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();var app=App.Build(context=>context.UseNom());
        var host=NomTestDirectory.Host(app);var factory=NomTestDirectory.Factory(app);var owner=factory.Create(args);
        await app.DisposeAsync();await app.DisposeAsync();
        Assert.True(owner.IsClosed);Assert.Equal(0,host.ActiveStoreCount);Assert.True(host.IsReleased);NomTestDirectory.AssertRootReleased(args);
        Assert.Throws<ObjectDisposedException>(()=>factory.Create(directory.Arguments("late")));
    }

    [Fact]
    public async Task FailedStartup_AfterOpeningStore_RollsBackBeforeAppDisposal()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();var failure=new IOException("consumer startup failed");
        await using var app=App.Build(context=>
        {
            context.UseNom();
            context.Services(services=>services.Singleton<IStartable>(resolver=>new OpenThenFail(resolver.Resolve<IFactory<IProjectAssetStore,OpenAssetStoreArguments>>()!,args,failure)));
        });
        var service=Assert.Single(app.Services.ResolveAll<IStartable>().OfType<OpenThenFail>());
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>app.StartAsync()));
        Assert.NotNull(service.Opened);Assert.True(service.Opened.IsClosed);Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);
        Assert.True(NomTestDirectory.Host(app).IsReleased);NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public void FailedBuild_AfterOpeningStore_DisposesRootAndPreservesOriginalError()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();var failure=new IOException("app activation failed");
        IProjectAssetStore? owner=null;NomStoreHost? host=null;
        Assert.Same(failure,Assert.Throws<IOException>(()=>App.Build(context=>
        {
            context.UseNom();
            context.Services(services=>services.RemoveAll<IApp>().Singleton<IApp>(resolver=>
            {
                host=resolver.Resolve<NomStoreHost>();owner=resolver.Resolve<IFactory<IProjectAssetStore,OpenAssetStoreArguments>>()!.Create(args);throw failure;
            }));
        })));
        Assert.NotNull(owner);Assert.True(owner.IsClosed);Assert.NotNull(host);Assert.Equal(0,host.ActiveStoreCount);Assert.True(host.IsReleased);
        NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public async Task AppStop_WithOwnedReadLease_TimesOutAndRetainsRootUntilActualClose()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom(options=>options.DrainTimeout=TimeSpan.FromMilliseconds(50)));
        var owner=NomTestDirectory.Factory(app).Create(args);var host=NomTestDirectory.Host(app);
        var version=await NomTestDirectory.Publish(owner,[1,2,3]);var stream=await owner.OpenResourceAsync(version);
        try
        {
            await NomTestDirectory.AssertDrainTimeout(()=>app.StopAsync());
            Assert.False(owner.IsClosed);Assert.False(host.IsReleased);Assert.Equal(1,host.ActiveStoreCount);NomTestDirectory.AssertRootLocked(args);
            Assert.Throws<ObjectDisposedException>(()=>NomTestDirectory.Factory(app).Create(directory.Arguments("late")));
        }
        finally { await stream.DisposeAsync();await host.StopAsync(); }
        Assert.True(owner.IsClosed);Assert.True(host.IsReleased);Assert.Equal(0,host.ActiveStoreCount);NomTestDirectory.AssertRootReleased(args);
    }

    [Fact]
    public async Task AppStop_IgnoredCancellationWriter_RetainsBorrowedInputAndRootUntilReadReturns()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom(options=>options.DrainTimeout=TimeSpan.FromMilliseconds(50)));
        var owner=NomTestDirectory.Factory(app).Create(args);var host=NomTestDirectory.Host(app);var asset=NomTestDirectory.Record();using var input=new IgnoringCancellationSource();
        var ticket=await owner.ReserveResourcesAsync(0,[new AssetResourceWrite(asset,MaximumBytes:1)]);
        var publish=owner.PublishResourcesAsync(ticket,new Dictionary<Guid,Stream>{{asset.Id,input}});
        try
        {
            await input.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await NomTestDirectory.AssertDrainTimeout(()=>app.StopAsync());
            Assert.False(publish.IsCompleted);Assert.False(input.WasDisposed);Assert.False(owner.IsClosed);
            Assert.Equal(1,host.ActiveStoreCount);Assert.False(host.IsReleased);NomTestDirectory.AssertRootLocked(args);
            Assert.Throws<ObjectDisposedException>(()=>NomTestDirectory.Factory(app).Create(directory.Arguments("late")));
        }
        finally
        {
            input.Release.TrySetResult();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>publish);await host.StopAsync();
        }
        Assert.False(input.WasDisposed);Assert.True(owner.IsClosed);Assert.True(host.IsReleased);Assert.Equal(0,host.ActiveStoreCount);NomTestDirectory.AssertRootReleased(args);
        await using var otherApp=App.Build(context=>context.UseNom());
        await using var reopened=NomTestDirectory.Factory(otherApp).Create(args with{Mode=AssetStoreOpenMode.Open});
        Assert.Equal("aborted",(await reopened.LookupResourceOperationAsync(ticket)).Status);Assert.Equal(0,reopened.ContentGeneration);Assert.Empty(await reopened.ReadAsync());
    }

    [Fact]
    public async Task CancelledStopWait_ClosesAdmissionButKeepsCleanupOwnedUntilRetry()
    {
        using var directory=new NomTestDirectory();var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom(options=>options.DrainTimeout=TimeSpan.FromSeconds(2)));
        var owner=NomTestDirectory.Factory(app).Create(args);var host=NomTestDirectory.Host(app);
        var version=await NomTestDirectory.Publish(owner,[9,8,7]);var stream=await owner.OpenResourceAsync(version);using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>host.StopAsync(cancellation.Token));
            Assert.False(owner.IsClosed);Assert.Equal(1,host.ActiveStoreCount);Assert.False(host.IsReleased);NomTestDirectory.AssertRootLocked(args);
            Assert.Throws<ObjectDisposedException>(()=>NomTestDirectory.Factory(app).Create(directory.Arguments("late")));
            await Assert.ThrowsAsync<ObjectDisposedException>(async()=>await owner.ReadAsync());
        }
        finally { await stream.DisposeAsync();await host.StopAsync(); }
        Assert.True(owner.IsClosed);Assert.True(host.IsReleased);Assert.Equal(0,host.ActiveStoreCount);NomTestDirectory.AssertRootReleased(args);
    }

    private sealed class OpenThenFail(IFactory<IProjectAssetStore,OpenAssetStoreArguments> factory,OpenAssetStoreArguments arguments,Exception failure) : IStartable
    {
        internal IProjectAssetStore? Opened { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken=default) { Opened=factory.Create(arguments);return Task.FromException(failure); }
    }
    private sealed class OpenAndStop(IFactory<IProjectAssetStore,OpenAssetStoreArguments> factory,OpenAssetStoreArguments arguments,Action stop) : IStartable
    {
        internal IProjectAssetStore? Opened { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken=default) { Opened=factory.Create(arguments);stop();return Task.CompletedTask; }
    }
}
