using Game.Logic;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Configuration;
using Xunit;

namespace TrueMoon.Aether.Player.Tests;

public sealed class PlayerCompositionTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task BothProviders_RegisterOneLazyWindowGraph_WithoutEditorOrCompiler(bool microsoft)
    {
        var root=Path.Combine(Path.GetTempPath(),"TrueMoon-player-ui-"+Guid.NewGuid().ToString("N"));
        var artifact=GameArtifactPublisher.Publish(Path.GetDirectoryName(typeof(ProbeGame).Assembly.Location)!,root,new('A',64),"Game.Logic.dll","Game.Logic.ProbeGame",1,runtimeAccess:true);
        var start=PlayerStart.FromSnapshot(artifact,new("local",1,[]));
        PlayerOptions mutable=null!;
        await using var app=App.Builder(configuration=>{if(microsoft)configuration.UseDI();}).Setup(context=>
            context.UseAetherPlayer(start,SkiaFontCatalog.SystemDefault, options=>mutable=options)).Build();
        mutable.Title="subsequent change";mutable.Width=-1;
        var session=app.Services.Resolve<PlayerSession>()!; var window=app.Services.Resolve<PlayerWindow>()!;
        Assert.Equal(PlayerState.Created,session.Status.State); Assert.Null(session.Surface); Assert.False(window.Ready.IsCompleted);
        Assert.Same(window,Assert.Single(app.Services.ResolveAll<PlayerWindow>())); Assert.Single(app.Services.ResolveAll<HostedUiWindow>());
        Assert.Empty(app.Services.ResolveAll<AetherHost>()); Assert.Empty(app.Services.ResolveAll<GameBuildCompiler>());
        Assert.Equal("Aether Player",app.Services.Resolve<PlayerViewModel>()!.Title);
        var factory=app.Services.Resolve<IFactory<PlayerView>>()!;
        using var first=factory.Create()!; using var second=factory.Create()!; Assert.NotSame(first,second); Assert.NotSame(first.Canvas,second.Canvas);
        var profile=ConfigurationProfiles.Create(builder=>
        {
            builder.AddSchema(PlayerOptions.Schema);
            builder.Override(PlayerOptions.Schema.Keys.Single(key=>key.Path.EndsWith(":Title",StringComparison.Ordinal)).Path,"from profile");
        });
        await using var configured=App.Builder(configuration=>{if(microsoft)configuration.UseDI();}).Setup(context=>
            context.UseConfiguration(profile).UseConfiguredAetherPlayer(start,SkiaFontCatalog.SystemDefault,options=>options.Title+=" plus explicit")).Build();
        Assert.Equal("from profile plus explicit",configured.Services.Resolve<PlayerViewModel>()!.Title);
        Assert.Equal(PlayerState.Created,configured.Services.Resolve<PlayerSession>()!.Status.State);
    }
    [Theory]
    [InlineData(15,540)][InlineData(1921,540)][InlineData(960,15)][InlineData(960,1081)]
    public void InvalidOptions_AreRejectedDuringComposition(int width,int height)
        =>Assert.Throws<ArgumentOutOfRangeException>(()=>App.Build(context=>context.UseAetherPlayer(null!,SkiaFontCatalog.SystemDefault,options=>{options.Width=width;options.Height=height;})));
}
