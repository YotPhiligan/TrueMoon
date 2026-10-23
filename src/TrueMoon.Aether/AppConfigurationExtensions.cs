using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Editing;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Gpu;
using TrueMoon.Services;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Scenes;
using TrueMoon.Aether.Transport;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Aether.Preview;

namespace TrueMoon.Aether;

/// <summary>Connects the Aether engine module to TrueMoon App services and lifecycle.</summary>
public static class AppConfigurationExtensions
{
    /// <summary>Registers one App-owned engine host. Game sessions are started explicitly.</summary>
    /// <param name="context">The application configuration context.</param>
    /// <param name="configure">Engine identity, report directory, deadlines and runtime factory.</param>
    /// <returns>The same application configuration context for chaining.</returns>
    /// <exception cref="InvalidOperationException">Aether or its runtime factory is already registered.</exception>
    public static IAppConfigurationContext UseAether(this IAppConfigurationContext context, Action<AetherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new AetherOptions(); configure(options);
        var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<AetherHost>() || services.Exist<IRuntimeFactory>() || services.Exist<AetherAppLifetime>())
                throw new InvalidOperationException("Aether is already registered. Configure its runtime factory through UseAether.");
            services.Singleton<IRuntimeFactory>(resolver => snapshot.RuntimeFactory(resolver)
                    ?? throw new InvalidOperationException("Aether runtime factory returned null."));
            Register<PipesInvocationClient<IEngineControl>, string>(services, _ =>
                new PipesInvocationClientFactory<IEngineControl>(new AetherEvents<PipesInvocationClient<IEngineControl>>()));
            Register<IRuntimeLease, SessionLaunch>(services, resolver =>
                new Factory<IRuntimeLease, SessionLaunch>(resolver.Resolve<IRuntimeFactory>().Create));
            Register<SessionSupervisor>(services, resolver => new Factory<SessionSupervisor>(() =>
                new(resolver.Resolve<IFactory<IRuntimeLease, SessionLaunch>>(), snapshot.HostContractId,
                    snapshot.GameBuildId, snapshot.ReportDirectory, snapshot.Deadlines, snapshot.StatusChanged)));
            Register<GpuResourceHost>(services, _ => new Factory<GpuResourceHost>(() => new()));
            Register<IGameSession, Type>(services, resolver => new GameSessionFactory(resolver));
            Register<GameBuildLoader, GameBuildReference>(services, resolver =>
                new Factory<GameBuildLoader, GameBuildReference>(build => GameBuildLoader.Load(build,
                    snapshot.HostContractId, resolver.Resolve<IFactory<IGameSession, Type>>())));
            RegisterSceneResolution(services);
            if (snapshot.ViewportTransportFactory != null)
                Register<IGameViewportTransport, GpuResourceHost>(services, snapshot.ViewportTransportFactory);
            else if (snapshot.ViewportTransport != null)
                Register<IGameViewportTransport, GpuResourceHost>(services, _ =>
                    new Factory<IGameViewportTransport, GpuResourceHost>(snapshot.ViewportTransport));
            var viewport = services.Exist<IFactory<IGameViewportTransport, GpuResourceHost>>();
            Register<FrameReceiver, (SharedFrameRing Ring, ulong After, LatencySamples Samples)>(services, _ =>
                new Factory<FrameReceiver, (SharedFrameRing Ring, ulong After, LatencySamples Samples)>(args =>
                    new(args.Ring, args.After, args.Samples)));
            Register<FrameSession, (SessionSupervisor Supervisor, GpuResourceHost Gpu)>(services, resolver =>
            {
                var transport = viewport ? resolver.Resolve<IFactory<IGameViewportTransport, GpuResourceHost>>() : null;
                var receivers = resolver.Resolve<IFactory<FrameReceiver, (SharedFrameRing, ulong, LatencySamples)>>();
                return new Factory<FrameSession, (SessionSupervisor Supervisor, GpuResourceHost Gpu)>(args =>
                    new(args.Supervisor, transport == null ? null : transport.Create(args.Gpu)
                        ?? throw new InvalidOperationException("Viewport factory returned null."), receivers));
            });
            Register<GameBuildCompiler>(services, _ => new Factory<GameBuildCompiler>(() =>
                new(snapshot.HostContractId, snapshot.BuildDirectory!, snapshot.BuildTimeout)));
            Register<ScenePreview, FrameSession>(services, _ => new Factory<ScenePreview, FrameSession>(frames => new(frames)));
            Register<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)>(services, _ =>
                new Factory<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)>(args =>
                    new(args.Supervisor, args.Frames, snapshot.ReplacementDeadlines)));
            Register<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)>(services, _ =>
                new Factory<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)>(args =>
                    new(args.Supervisor, args.Frames, args.Builds, args.Replacement, snapshot.HostContractId)));
            Register<AetherHost>(services, resolver => new AetherHostFactory(
                resolver.Resolve<IFactory<SessionSupervisor>>(), resolver.Resolve<IFactory<GpuResourceHost>>(),
                resolver.Resolve<IFactory<FrameSession, (SessionSupervisor, GpuResourceHost)>>(),
                resolver.Resolve<IFactory<GameBuildCompiler>>(),
                resolver.Resolve<IFactory<GameReplacement, (SessionSupervisor, FrameSession)>>(),
                resolver.Resolve<IFactory<GameEditor, (SessionSupervisor, FrameSession, GameBuildCompiler, GameReplacement)>>(),
                resolver.Resolve<IFactory<ScenePreview, FrameSession>>()));
            services.Singleton<AetherHost>(resolver => resolver.Resolve<IFactory<AetherHost>>().Create()!)
                .Singleton<AetherAppLifetime>(resolver => new AetherAppLifetime(resolver.Resolve<AetherHost>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<AetherAppLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<AetherAppLifetime>());
        });
        return context;
    }

    internal static void RegisterSceneResolution(IServicesRegistrationContext services)
    {
        Register<EntityResolver, SceneDocument>(services, _ => new Factory<EntityResolver, SceneDocument>(scene =>
            new(scene.Definitions.OrderBy(d => d.Id.Value), scene.Schemas.OrderBy(s => s.TypeId.Value).ThenBy(s => s.Version))));
        if (!services.Exist<SceneResolutionService>())
            services.Singleton<SceneResolutionService>(resolver => new(resolver.Resolve<IFactory<EntityResolver, SceneDocument>>()));
    }

    private static void Register<T>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T>> factory)
    { if (!services.Exist<IFactory<T>>()) services.Singleton(factory); }
    private static void Register<T, TArguments>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T, TArguments>> factory)
    { if (!services.Exist<IFactory<T, TArguments>>()) services.Singleton(factory); }
}
