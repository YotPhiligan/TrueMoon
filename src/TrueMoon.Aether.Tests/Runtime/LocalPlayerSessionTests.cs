using System.Numerics;
using Game.Logic;
using TrueMoon.Aether.Checkpoints;
using TrueMoon.Aether.Ecs.Loading;
using TrueMoon.Aether.Ecs.Rendering;
using TrueMoon.Aether.Ecs.Transforms;
using TrueMoon.Aether.Input;
using TrueMoon.Aether.Rendering;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Builds;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Ecs.Runtime;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Rendering.Snapshots;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Tests.Rendering.Resources;
using TrueMoon.Aether.Tooling;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Dependencies;
using TrueMoon.Extensions.DependencyInjection;
using TrueMoon.Mithril;
using Xunit;

namespace TrueMoon.Aether.Tests.Runtime;

public sealed class LocalPlayerSessionTests
{
    private const string Hash="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    [Fact]
    public async Task Hud_FollowsTicksAndIsClearedWhenPlayerStops()
    {
        using var artifact=new Artifact();
        var game=new HudGame();
        await using var app=App.Build(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(_=>new Factory<IGameSession,Type>(_=>game)));
            context.UseAetherLocalRuntime(PlayerStart.FromSnapshot(artifact.Build,new("local",7,[])));
        });
        var player=app.Services.Resolve<PlayerSession>()!;
        Assert.Null(player.Hud);
        await app.StartAsync();
        var initial=Assert.IsType<GameHudSnapshot>(player.Hud);
        Assert.Equal("Observe",initial.Prompt);
        await player.SetPlayingAsync(true);
        await player.InputAsync(new(true,GameButtons.Forward,InteractPressed:true));
        await player.FixedTickAsync();
        Assert.Null(player.Hud!.Prompt);
        Assert.Equal("Reported",Assert.Single(player.Hud.Journal));
        Assert.Equal("Observe",initial.Prompt);
        await app.StopAsync();
        Assert.Null(player.Hud);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task BothProviders_CreateOneLazyLocalGraph_WithoutHostCompilerParentOrIpc(bool microsoft)
    {
        using var artifact=new Artifact();var game=new Game();var calls=0;
        await using var app=Builder(microsoft).Setup(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(_=>new Factory<IGameSession,Type>(type=>{calls++;return game;})));
            context.UseAetherLocalRuntime(PlayerStart.FromSnapshot(artifact.Build,new("local",7,[])));
        }).Build();
        var player=app.Services.Resolve<PlayerSession>()!;Assert.Equal(0,calls);Assert.Equal(PlayerState.Created,player.Status.State);
        Assert.Same(player,Assert.Single(app.Services.ResolveAll<PlayerSession>()));
        Assert.Empty(app.Services.ResolveAll<AetherHost>());Assert.Empty(app.Services.ResolveAll<GameBuildCompiler>());
        Assert.Empty(app.Services.ResolveAll<AetherRuntimeHost>());Assert.Empty(app.Services.ResolveAll<UnitParentProcessEventsHandler>());
        Assert.Empty(app.Services.ResolveAll<IRuntimeFactory>());Assert.Null(player.Surface);
        await app.StartAsync();Assert.Equal(1,calls);Assert.Equal(PlayerState.Paused,player.Status.State);Assert.Equal(7,game.Revision);
        await player.SetPlayingAsync(true);await player.InputAsync(new(true,GameButtons.Forward,InteractPressed:true));await player.FixedTickAsync();
        Assert.Equal(1UL,player.Status.Tick);Assert.Equal(GameButtons.Forward,game.LastInput.Buttons);Assert.True(game.LastInput.InteractPressed);
        await player.FixedTickAsync();Assert.False(game.LastInput.InteractPressed);Assert.Equal(GameButtons.Forward,game.LastInput.Buttons);
        await player.SetPlayingAsync(false);await player.FixedTickAsync();Assert.Equal(2UL,game.TickNumber);
        await player.StepAsync();Assert.Equal(3UL,game.TickNumber);
        await app.StopAsync();Assert.Equal(PlayerState.Stopped,player.Status.State);Assert.Equal(1,game.Disposals);
        await app.DisposeAsync();Assert.Equal(1,game.Disposals);
        using(var pinCheck=new FileStream(Path.Combine(artifact.Build.DirectoryPath,GameBuildArtifact.ManifestFile),FileMode.Open,FileAccess.Read,FileShare.None))Assert.True(pinCheck.Length>0);
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>player.StepAsync());

        // Cancellation after the game exists must release the partially activated
        // session even though App has not added its failed Start to the started list.
        using var cancelled=new CancellationTokenSource();var partial=new Game{OnLoad=cancelled.Cancel};
        await using var cancelledApp=Builder(microsoft).Setup(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(_=>new Factory<IGameSession,Type>(_=>partial)));
            context.UseAetherLocalRuntime(PlayerStart.FromSnapshot(artifact.Build,new("local",7,[])));
        }).Build();
        var cancelledPlayer=cancelledApp.Services.Resolve<PlayerSession>()!;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>cancelledApp.StartAsync(cancelled.Token));
        Assert.Equal(PlayerState.Stopped,cancelledPlayer.Status.State);Assert.Equal(1,partial.Disposals);Assert.True(partial.Released);
        await cancelledApp.DisposeAsync();Assert.Equal(1,partial.Disposals);
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void CompositionsAreMutuallyExclusiveInEitherOrder_AndDuplicateLocalModuleIsRejected(bool microsoft)
    {
        using var artifact=new Artifact();var start=PlayerStart.FromSnapshot(artifact.Build,new("local",7,[]));
        void Host(IAppConfigurationContext context)=>context.UseAether(options=>{options.HostContractId=Hash;options.GameBuildId=Hash;options.ReportDirectory=artifact.Root;});
        Assert.Throws<InvalidOperationException>(()=>Builder(microsoft).Setup(context=>{Host(context);context.UseAetherLocalRuntime(start);}).Build());
        Assert.Throws<InvalidOperationException>(()=>Builder(microsoft).Setup(context=>{context.UseAetherLocalRuntime(start);Host(context);}).Build());
        Assert.Throws<InvalidOperationException>(()=>Builder(microsoft).Setup(context=>{context.UseAetherLocalRuntime(start);context.UseAetherLocalRuntime(start);}).Build());
    }
    [Fact]
    public async Task FailedMeshRetirement_RetainsWorldAndLoaderUntilSuccessfulStopRetry()
    {
        using var artifact=new Artifact();var game=new MeshGame();
        await using var app=App.Build(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(_=>new Factory<IGameSession,Type>(_=>game)));
            context.UseAetherLocalRuntime(PlayerStart.FromSnapshot(artifact.Build,new("local",7,[])));
        });
        await app.StartAsync();var player=app.Services.Resolve<PlayerSession>()!;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>player.SetPlayingAsync(true));Assert.Equal(0UL,game.TickNumber);
        var backend=new ResourceTestBackend{DisposeFailuresRemaining=1};var backends=new ResourceTestBackendFactory(backend);
        var stamp=new GpuFrameStamp(player.Identity,1,1,0,2);
        var data=await ((IGpuSurfaceFrameSource)player).CaptureAsync(new(stamp,320,180,backends),CancellationToken.None);
        await data.Snapshot!.DisposeAsync();
        await Assert.ThrowsAsync<AggregateException>(()=>player.StopAsync());
        Assert.Equal(PlayerState.CleanupFailed,player.Status.State);Assert.False(game.World.IsDisposed);Assert.Equal(0,game.Disposals);
        using(var file=File.OpenRead(Path.Combine(artifact.Build.DirectoryPath,artifact.Build.EntryAssembly)))Assert.True(file.Length>0);
        await player.StopAsync();Assert.True(game.World.IsDisposed);Assert.Equal(1,game.Disposals);Assert.True(backend.IsReleased);
        Assert.Equal(PlayerState.Stopped,player.Status.State);
    }
    [Fact]
    public async Task FailedSceneActivation_IsOwned_AndFailedGameCleanupCanRetryWithoutLosingThePin()
    {
        using var artifact=new Artifact();var game=new Game{FailLoad=true,DisposeFailures=1};
        var app=App.Build(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(_=>new Factory<IGameSession,Type>(_=>game)));
            context.UseAetherLocalRuntime(PlayerStart.FromSnapshot(artifact.Build,new("local",7,[])));
        });
        var player=app.Services.Resolve<PlayerSession>()!;
        await Assert.ThrowsAnyAsync<Exception>(()=>app.StartAsync());Assert.True(game.Disposals>=1);
        await player.StopAsync();Assert.Equal(PlayerState.Stopped,player.Status.State);Assert.True(game.Released);
        await app.DisposeAsync();
    }
    [Fact]
    public async Task CompleteWorld_ActivatesThroughTheLocalBoundary_WithoutTransferChunks()
    {
        using var artifact=new Artifact();WorldGame game=null!;
        var scene=new SceneWorldData(new(new(Guid.NewGuid()),11,[],[],[]));
        await using var app=App.Build(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(resolver=>new Factory<IGameSession,Type>(_=>
                game=new(resolver.Resolve<IFactory<SceneWorldLoader,ComponentRegistry>>().Create(new ComponentRegistryBuilder().Build())))));
            context.UseAetherLocalRuntime(PlayerStart.FromWorld(artifact.Build,scene));
        });
        await app.StartAsync();var player=app.Services.Resolve<PlayerSession>()!;var world=game.ActiveScene!.World;
        Assert.Equal(11,game.Revision);Assert.False(world.IsDisposed);Assert.Equal(0,world.EntityCount);
        await player.StepAsync();Assert.Equal(1UL,game.TickNumber);
        await player.StopAsync();Assert.True(world.IsDisposed);
    }

    [Theory]
    [InlineData(13UL)]
    [InlineData(ulong.MaxValue-1)]
    public async Task WorldCheckpoint_IsConfirmedBeforeHudAndCanBeCapturedOnlyWhilePaused(ulong savedTick)
    {
        using var artifact=new Artifact();
        var start=WorldCheckpointStart(artifact.Build,savedTick);
        PlayerSession appPlayer=null!;
        await using var app=WorldCheckpointApp(start,discardPayload:false,observeGame:game=>game.OnRestore=()=>
        {
            var player=appPlayer!;
            Assert.Equal(PlayerState.Loading,player.Status.State);
            Assert.Null(player.Hud);
            Assert.Null(player.Surface);
        });
        appPlayer=app.Services.Resolve<PlayerSession>()!;
        await app.StartAsync();

        Assert.Equal(PlayerState.Paused,appPlayer.Status.State);
        Assert.Equal(savedTick,appPlayer.Status.Tick);
        Assert.Equal(start.Checkpoint!.State.Payload,appPlayer.Hud!.Status);
        var captured=await appPlayer.CaptureCheckpointAsync();
        Assert.Equal((start.Checkpoint.HostContractId,start.Checkpoint.SourceGameBuildId,start.Checkpoint.SceneId,start.Checkpoint.SceneRevision,start.Checkpoint.State),
            (captured.HostContractId,captured.SourceGameBuildId,captured.SceneId,captured.SceneRevision,captured.State));
        await appPlayer.SetPlayingAsync(true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>appPlayer.CaptureCheckpointAsync());
        await appPlayer.FixedTickAsync();
        await appPlayer.SetPlayingAsync(false);
        Assert.Equal(captured.State with{Tick=savedTick+1},(await appPlayer.CaptureCheckpointAsync()).State);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>appPlayer.StartAsync());
    }

    [Fact]
    public async Task WorldCheckpoint_DroppedPayloadRejectsStartupWithoutPublishingHud()
    {
        using var artifact=new Artifact();
        var start=WorldCheckpointStart(artifact.Build);
        CheckpointWorldGame game=null!;
        await using var app=WorldCheckpointApp(start,discardPayload:true,observeGame:value=>game=value);
        var player=app.Services.Resolve<PlayerSession>()!;

        var error=await Assert.ThrowsAsync<InvalidDataException>(()=>player.StartAsync());

        Assert.Contains("complete state",error.Message);
        Assert.Equal(0,game.HudReads);
        Assert.Equal(PlayerState.Stopped,player.Status.State);
        Assert.Null(player.Hud);
        Assert.Null(player.Surface);
    }

    private static PlayerStart WorldCheckpointStart(GameBuildArtifact artifact,ulong tick=13)
    {
        var scene=new SceneWorldData(new(new(Guid.NewGuid()),11,[],[],[]));
        var state=new GameCheckpointState(tick,new(1,2,3,.1f,.2f,0),true)
        {
            Objects=new([new("switch",true)]),
            Payload="状態🐉"
        };
        return PlayerStart.FromWorld(artifact,scene)
            .WithCheckpoint(new(artifact.HostContractId,artifact.BuildId,scene.Scene.Id.Value.ToString("D"),scene.Scene.Revision,state));
    }

    private static IApp WorldCheckpointApp(PlayerStart start,bool discardPayload,Action<CheckpointWorldGame> observeGame)
        => App.Build(context=>
        {
            context.Services(services=>services.Singleton<IFactory<IGameSession,Type>>(resolver=>new Factory<IGameSession,Type>(_=>
            {
                var loader=resolver.Resolve<IFactory<SceneWorldLoader,ComponentRegistry>>().Create(new ComponentRegistryBuilder().Build());
                var game=new CheckpointWorldGame(loader,discardPayload);
                observeGame(game);
                return game;
            })));
            context.UseAetherLocalRuntime(start);
        });
    private static IAppBuilder Builder(bool microsoft)=>App.Builder(context=>{if(microsoft)context.UseDI();});
    internal sealed class Artifact:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"TrueMoon-player-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly GameBuildArtifact Build;
        internal Artifact()=>Build=GameArtifactPublisher.Publish(Path.GetDirectoryName(typeof(ProbeGame).Assembly.Location)!,Root,Hash,"Game.Logic.dll",typeof(ProbeGame).FullName!,7, runtimeAccess: true);
        public void Dispose(){} // Preserve evidence; collectible assembly unload is cooperative and may defer DLL file release until GC.
    }
    private class Game:IInputGameSession
    {
        public ulong TickNumber{get;private set;}public long Revision{get;private set;}=-1;public uint PatternColor=>0xff000000;
        internal int Disposals,DisposeFailures;internal bool FailLoad,Released;internal GameInputTick LastInput;internal Action? OnLoad;
        public virtual void Load(SceneSnapshot scene){Revision=scene.Revision;OnLoad?.Invoke();if(FailLoad)throw new InvalidDataException("load marker");}
        public void Tick()=>Tick(default);
        public void Tick(GameInputTick input){LastInput=input;TickNumber++;}
        public virtual void Dispose(){Disposals++;if(DisposeFailures-->0)throw new IOException("game cleanup marker");Released=true;}
    }
    private sealed class HudGame:Game,IGameHudSource
    {
        public GameHudSnapshot ReadHud()=>TickNumber==0
            ? new("Observe","Open",[])
            : new(null,"Inspecting",[LastInput.InteractPressed?"Reported":"Waiting"]);
    }
    private sealed class MeshGame:Game,IRenderSnapshotGameSession
    {
        internal readonly World World=new();private readonly RenderContentManifest _content=ResourceTestFixture.Content();
        public override void Load(SceneSnapshot scene)
        {
            base.Load(scene);var entity=World.CreateEntity();World.AddComponent(entity,new WorldTransform(Matrix4x4.Identity,0));
            World.AddComponent(entity,new MeshRenderer(_content.Resolve(RenderContentProfile.CubeAssetId,RenderContentProfile.MeshKindId).Key,
                _content.Resolve(RenderContentProfile.CubeBindingSetAssetId,RenderContentProfile.BindingSetKindId).Key,true,uint.MaxValue,false,Vector3.One));
        }
        public RenderSceneSource ReadRenderScene()=>new(World,_content,new(new Guid("458ddc79-8803-4dc0-b24b-9445695d980a")),Revision,RenderCameraSource.External(Matrix4x4.CreateTranslation(0,0,6),PerspectiveCamera.Default));
        public override void Dispose(){base.Dispose();World.Dispose();}
    }
    private sealed class WorldGame(SceneWorldLoader loader):SceneWorldGameSession(loader)
    {public override uint PatternColor=>0;protected override void TickWorld(SceneWorld scene,ulong nextTick){} }

    private sealed class CheckpointWorldGame(SceneWorldLoader loader,bool discardPayload)
        : SceneWorldGameSession(loader),ICheckpointGameSession,IGameHudSource
    {
        private GameCheckpointState _state=new(0,default,false);
        internal int HudReads;
        internal Action? OnRestore;
        public override uint PatternColor=>0;
        protected override void TickWorld(SceneWorld scene,ulong nextTick){}
        public GameCheckpointState CaptureCheckpoint()=>_state with{Tick=TickNumber};
        public void RestoreCheckpoint(GameCheckpointState state)
        {
            OnRestore?.Invoke();
            RestoreSceneTick(state.Tick);
            _state=discardPayload?state with{Payload=null}:state;
        }
        public GameHudSnapshot ReadHud(){HudReads++;return new(null,_state.Payload,[]);}
    }
}
