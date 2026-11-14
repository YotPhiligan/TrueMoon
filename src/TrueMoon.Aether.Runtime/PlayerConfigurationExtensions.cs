using TrueMoon.Aether.Abstractions.Builds;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Aether.Runtime.Ecs;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Gpu.Surfaces;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;

namespace TrueMoon.Aether.Runtime;

/// <summary>Standalone local runtime composition, without AetherHost/compiler/project/process/IPC services.</summary>
public static class PlayerConfigurationExtensions
{
    /// <summary>Registers one lazy App-owned local game session and factories. Register before its window lifecycle.</summary>
    /// <param name="context">Player App.</param><param name="start">Pre-App validated, captured artifact/scene.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherLocalRuntime(this IAppConfigurationContext context,PlayerStart start)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(start);
        context.UseAetherSurfaceRendering();
        context.Services(services=>
        {
            if(services.Exist<PlayerSession>()||services.Exist<AetherRuntimeHost>()||services.Exist<RuntimeEngine>())
                throw new InvalidOperationException("Choose one local Player or process-host runtime composition.");
            RuntimeAppConfigurationExtensions.RegisterSceneResolution(services);PrimitiveEcsConfigurationExtensions.RegisterDefaults(services);
            if(!services.Exist<IFactory<World>>())services.Singleton<IFactory<World>>(_=>new Factory<World>(()=>new()));
            if(!services.Exist<IFactory<SceneWorldLoader,ComponentRegistry>>())services.Singleton<IFactory<SceneWorldLoader,ComponentRegistry>>(resolver=>
                new Factory<SceneWorldLoader,ComponentRegistry>(registry=>new(registry,resolver.Resolve<IFactory<World>>(),resolver.Resolve<SceneResolutionService>())));
            if(!services.Exist<IFactory<IGameSession,Type>>())services.Singleton<IFactory<IGameSession,Type>>(resolver=>new GameSessionFactory(resolver));
            if(!services.Exist<IFactory<GameBuildLoader,GameBuildReference>>())services.Singleton<IFactory<GameBuildLoader,GameBuildReference>>(resolver=>
                new Factory<GameBuildLoader,GameBuildReference>(build=>GameBuildLoader.Load(build,start.Artifact.HostContractId,resolver.Resolve<IFactory<IGameSession,Type>>())));
            if(!services.Exist<GpuResourceHost>())services.Singleton<GpuResourceHost>(_=>new());
            services.Singleton<IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>>(resolver=>new Factory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>(resolver.Resolve<GpuResourceHost>().CreateLocalSurface));
            services.Singleton<IFactory<RuntimeProtocol,(SessionIdentity,IGameSession)>>(resolver=>new Factory<RuntimeProtocol,(SessionIdentity,IGameSession)>(arguments=>
                new(arguments.Item1,arguments.Item2,Environment.ProcessId,sceneWorldCodec:EcsSceneLoadingConfigurationExtensions.CreateNeutralCodec(resolver))));
            services.Singleton<PlayerSession>(resolver=>new(start,resolver.Resolve<IFactory<GameBuildLoader,GameBuildReference>>(),
                resolver.Resolve<IFactory<RuntimeProtocol,(SessionIdentity,IGameSession)>>(),resolver.Resolve<IFactory<GpuLocalSurfaceSession,GpuLocalSurfaceRequest>>(),resolver.Resolve<RuntimeMeshRenderHost>()));
            services.Singleton<IStartable>(resolver=>new AppServiceLifecycle(startable:resolver.Resolve<PlayerSession>()));
            services.Singleton<IStoppable>(resolver=>new AppServiceLifecycle(stoppable:resolver.Resolve<PlayerSession>()));
        });
        return context;
    }
}
