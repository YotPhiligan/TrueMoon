using TrueMoon.Dependencies;
using TrueMoon.Services;
using Xunit;
using TrueMoon.Nom.Hosting;

namespace TrueMoon.Nom.Tests;

public sealed class NomAppRegistrationTests
{
    [Fact]
    public void UseNom_ReturnsSameContextAndRejectsNullContext()
    {
        var context=new AppConfigurationContext();
        Assert.Same(context,context.UseNom());
        Assert.Single(context.GetServicesRegistrations());
        Assert.Throws<ArgumentNullException>(()=>((IAppConfigurationContext)null!).UseNom());
    }

    [Fact]
    public void UseNom_ConfigurationDelegateFailure_LeavesContextUnmodified()
    {
        var context=new AppConfigurationContext();var failure=new IOException("invalid caller configuration");
        Assert.Same(failure,Assert.Throws<IOException>(()=>context.UseNom(_=>throw failure)));
        Assert.Empty(context.GetServicesRegistrations());
    }

    [Theory]
    [InlineData(nameof(NomOptions.MaximumOpenStores),-1)]
    [InlineData(nameof(NomOptions.MaximumOpenStores),0)]
    [InlineData(nameof(NomOptions.MaximumOpenStores),5)]
    [InlineData(nameof(NomOptions.QueueCount),-1)]
    [InlineData(nameof(NomOptions.QueueCount),0)]
    [InlineData(nameof(NomOptions.QueueCount),33)]
    [InlineData(nameof(NomOptions.QueueBytes),-1)]
    [InlineData(nameof(NomOptions.QueueBytes),0)]
    [InlineData(nameof(NomOptions.QueueBytes),16777217)]
    [InlineData(nameof(NomOptions.BatchRows),-1)]
    [InlineData(nameof(NomOptions.BatchRows),0)]
    [InlineData(nameof(NomOptions.BatchRows),257)]
    [InlineData(nameof(NomOptions.BatchBytes),-1)]
    [InlineData(nameof(NomOptions.BatchBytes),0)]
    [InlineData(nameof(NomOptions.BatchBytes),4194305)]
    [InlineData(nameof(NomOptions.DocumentBytes),-1)]
    [InlineData(nameof(NomOptions.DocumentBytes),0)]
    [InlineData(nameof(NomOptions.DocumentBytes),131073)]
    public void UseNom_InvalidNumericLimit_RejectsBeforeRegistering(string name,int value)
    {
        var context=new AppConfigurationContext();
        Assert.Throws<ArgumentOutOfRangeException>(()=>context.UseNom(options=>typeof(NomOptions).GetProperty(name)!.SetValue(options,value)));
        Assert.Empty(context.GetServicesRegistrations());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(10001)]
    public void UseNom_InvalidDrainTimeout_RejectsBeforeRegistering(int milliseconds)
    {
        var context=new AppConfigurationContext();
        Assert.Throws<ArgumentOutOfRangeException>(()=>context.UseNom(options=>options.DrainTimeout=TimeSpan.FromMilliseconds(milliseconds)));
        Assert.Empty(context.GetServicesRegistrations());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UseNom_AtAllMinimumOrMaximumBounds_BuildsAndStops(bool maximum)
    {
        await using var app=App.Build(context=>context.UseNom(options=>
        {
            options.MaximumOpenStores=maximum?4:1; options.QueueCount=maximum?32:1;
            options.QueueBytes=maximum?16*1024*1024:1; options.BatchRows=maximum?256:1;
            options.BatchBytes=maximum?4*1024*1024:1; options.DocumentBytes=maximum?128*1024:1;
            options.DrainTimeout=maximum?TimeSpan.FromSeconds(10):TimeSpan.FromTicks(1);
        }));
        await app.StartAsync();
        Assert.Equal(0,NomTestDirectory.Host(app).ActiveStoreCount);
        await app.StopAsync();
        Assert.True(NomTestDirectory.Host(app).IsReleased);
    }

    [Fact]
    public async Task AppBuildAndStart_AreLazyAndResolveOneNonOwningTypedFactory()
    {
        using var directory=new NomTestDirectory(); var args=directory.Arguments();
        await using var app=App.Build(context=>context.UseNom());
        var factory=NomTestDirectory.Factory(app); var host=NomTestDirectory.Host(app);
        Assert.Same(factory,NomTestDirectory.Factory(app)); Assert.Same(host,NomTestDirectory.Host(app));
        Assert.False(factory is IDisposable); Assert.False(factory is IAsyncDisposable);
        var lifecycle=Assert.Single(app.Services.ResolveAll<IStartable>());
        Assert.Same(lifecycle,Assert.Single(app.Services.ResolveAll<IStoppable>()));
        Assert.False(lifecycle is IDisposable);Assert.False(lifecycle is IAsyncDisposable);
        Assert.Equal(0,host.ActiveStoreCount); Assert.False(Directory.Exists(args.Root));
        await app.StartAsync();
        Assert.Equal(0,host.ActiveStoreCount); Assert.False(Directory.Exists(args.Root));
        Assert.Throws<InvalidOperationException>(()=>((IFactory<IProjectAssetStore>)factory).Create());
        Assert.Throws<ArgumentException>(()=>((IFactory<IProjectAssetStore>)factory).Create("wrong arguments"));
        await app.StopAsync();
        Assert.True(host.IsReleased);
        Assert.Throws<ObjectDisposedException>(()=>factory.Create(args));
    }

    [Fact]
    public async Task UseNom_SnapshotsOptionsBeforeLaterMutation()
    {
        using var directory=new NomTestDirectory(); NomOptions? captured=null;
        await using var app=App.Build(context=>
        {
            context.UseNom(options=>{ captured=options; options.MaximumOpenStores=2; options.DocumentBytes=512; });
            captured!.MaximumOpenStores=4; captured.DocumentBytes=1;
        });
        var factory=NomTestDirectory.Factory(app);
        await using var first=factory.Create(directory.Arguments("first"));
        await using var second=factory.Create(directory.Arguments("second"));
        Assert.Equal(2,NomTestDirectory.Host(app).ActiveStoreCount);
        Assert.Throws<InvalidOperationException>(()=>factory.Create(directory.Arguments("over-limit")));
        var version=await NomTestDirectory.Publish(first,[1,2,3]);
        Assert.Equal(3,version.Length); Assert.Equal(1,first.ContentGeneration);
    }

    [Fact]
    public void UseNom_RepeatedRegistration_IsRejectedAtBuild()
    {
        Assert.Throws<InvalidOperationException>(()=>App.Build(context=>{context.UseNom();context.UseNom();}));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UseNom_ConflictingHostOrFactory_IsPreservedAndRejectedAtBuild(bool host)
    {
        var calls=0;
        Assert.Throws<InvalidOperationException>(()=>App.Build(context=>
        {
            context.Services(services=>
            {
                if(host) services.Singleton<NomStoreHost>(_=>{calls++;throw new InvalidOperationException("must not activate conflicting host");});
                else services.Singleton<IFactory<IProjectAssetStore,OpenAssetStoreArguments>>(_=>
                    new Factory<IProjectAssetStore,OpenAssetStoreArguments>(_=>{calls++;throw new InvalidOperationException("must not be called");}));
            });
            context.UseNom();
        }));
        Assert.Equal(0,calls);
    }

    [Fact]
    public async Task RepeatedBuilderBuilds_HaveIndependentRegistriesAndLifetimes()
    {
        using var directory=new NomTestDirectory();
        var builder=App.Builder(_=>{}).Setup(context=>context.UseNom());
        await using var first=builder.Build(); await using var second=builder.Build();
        var firstFactory=NomTestDirectory.Factory(first);var secondFactory=NomTestDirectory.Factory(second);
        Assert.NotSame(firstFactory,secondFactory); Assert.NotSame(NomTestDirectory.Host(first),NomTestDirectory.Host(second));
        var left=firstFactory.Create(directory.Arguments("left")); await using var right=secondFactory.Create(directory.Arguments("right"));
        await first.StopAsync();
        Assert.True(left.IsClosed); Assert.Equal(0,NomTestDirectory.Host(first).ActiveStoreCount);
        Assert.False(right.IsClosed); Assert.Equal(1,NomTestDirectory.Host(second).ActiveStoreCount);
        Assert.Equal(3,(await NomTestDirectory.Publish(right,[5,6,7])).Length);
    }
}
