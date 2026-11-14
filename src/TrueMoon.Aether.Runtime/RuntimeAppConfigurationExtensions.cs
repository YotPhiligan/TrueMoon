using TrueMoon.Aether.Abstractions.Builds;
using TrueMoon.Aether.Abstractions.Scenes;
using TrueMoon.Aether.Abstractions.Sessions;
using TrueMoon.Services;
using TrueMoon.Aether.Runtime;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Diagnostics;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Ecs.Loading;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Gpu;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Aether.Runtime.Sessions;
using TrueMoon.Aether.Runtime.Transport;

namespace TrueMoon.Aether.Runtime;

/// <summary>Connects the Aether engine module to TrueMoon App services and lifecycle.</summary>
public static class RuntimeAppConfigurationExtensions
{
    /// <summary>Registers one App-owned engine host. Game sessions are started explicitly.</summary>
    /// <param name="context">The application configuration context.</param>
    /// <param name="configure">Engine identity, report directory, deadlines and runtime factory.</param>
    /// <returns>The same application configuration context for chaining.</returns>
    /// <exception cref="InvalidOperationException">Aether or its runtime factory is already registered.</exception>
    public static IAppConfigurationContext UseAetherRuntime(this IAppConfigurationContext context, Action<RuntimeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new RuntimeOptions(); configure(options);
        var snapshot = options.Snapshot();
        return RegisterRuntimeCore(context, snapshot, true);
    }

    internal static IAppConfigurationContext RegisterRuntimeCore(IAppConfigurationContext context, RuntimeOptions snapshot, bool diagnostics)
    {
        context.Services(services =>
        {
            if (services.Exist<RuntimeEngine>() || services.Exist<IRuntimeFactory>() || services.Exist<RuntimeAppLifetime>() || services.Exist<PlayerSession>())
                throw new InvalidOperationException("Aether is already registered. Configure its runtime factory through UseAether.");
            if (snapshot.RuntimeLaunch != null) services.Singleton<NativeRuntimeLaunch>(_ => snapshot.RuntimeLaunch);
            services.Singleton<IRuntimeFactory>(resolver => snapshot.RuntimeFactory(resolver)
                    ?? throw new InvalidOperationException("Aether runtime factory returned null."));
            services.Singleton<ChildDiagnosticConfiguration>(_ => new(snapshot.ChildDiagnostics));
            Register<PipesInvocationClient<IChildDiagnosticDelivery>, string>(services, _ =>
                new PipesInvocationClientFactory<IChildDiagnosticDelivery>(new AetherEvents<PipesInvocationClient<IChildDiagnosticDelivery>>(),
                    ChildDiagnosticFactories.Limits(snapshot.ChildDiagnostics)));
            Register<PipesInvocationServer<IChildDiagnosticDelivery>, PipesInvocationServerArguments<IChildDiagnosticDelivery>>(services, _ =>
                new PipesInvocationServerFactory<IChildDiagnosticDelivery>(new AetherEvents<PipesInvocationServer<IChildDiagnosticDelivery>>(),
                    ChildDiagnosticFactories.Limits(snapshot.ChildDiagnostics)));
            Register<ChildDiagnosticReceiver, ChildDiagnosticReceiverArguments>(services, resolver => new ChildDiagnosticReceiverFactory(
                resolver.TryResolve<IDiagnosticRecordImporter>(),
                resolver.Resolve<IFactory<PipesInvocationServer<IChildDiagnosticDelivery>, PipesInvocationServerArguments<IChildDiagnosticDelivery>>>()));
            Register<ChildDiagnosticForwarder, SessionLaunch>(services, resolver => new ChildDiagnosticForwarderFactory(
                resolver.TryResolve<IDiagnosticRecordReader>(), resolver.Resolve<IFactory<PipesInvocationClient<IChildDiagnosticDelivery>, string>>(),
                new RuntimeDiagnosticObservationSource(resolver.TryResolve<IDiagnosticHealthReader>(), resolver.Resolve<RuntimeEngine>().Gpu)));
            Register<PipesInvocationClient<IEngineControl>, string>(services, resolver =>
                new PipesInvocationClientFactory<IEngineControl>(resolver.Resolve<IEventsSource<PipesInvocationClient<IEngineControl>>>(),
                    new PipesTransportOptions { PropagateTraceContext = true }, resolver.Resolve<IDiagnosticTelemetry>()));
            Register<IRuntimeLease, SessionLaunch>(services, resolver =>
                new Factory<IRuntimeLease, SessionLaunch>(resolver.Resolve<IRuntimeFactory>().Create));
            Register<SessionSupervisor>(services, resolver =>
            {
                var leases = resolver.Resolve<IFactory<IRuntimeLease, SessionLaunch>>();
                var events = resolver.Resolve<IEventsSource<SessionSupervisor>>();
                var sceneCodec = resolver.TryResolve<SceneWorldCodec>() ?? EcsSceneLoadingConfigurationExtensions.CreateNeutralCodec(resolver);
                return new Factory<SessionSupervisor>(() => new(leases, snapshot.HostContractId,
                    snapshot.GameBuildId, snapshot.ReportDirectory, snapshot.Deadlines, status =>
                    {
                        AetherDiagnosticEvents.Session(events, status);
                        snapshot.StatusChanged?.Invoke(status);
                    }, sceneCodec, resolver.Resolve<SceneResolutionService>()));
            });
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
                var events = resolver.Resolve<IEventsSource<FrameSession>>();
                var telemetry = resolver.Resolve<IDiagnosticTelemetry>();
                return new Factory<FrameSession, (SessionSupervisor Supervisor, GpuResourceHost Gpu)>(args =>
                    new(args.Supervisor, transport == null ? null : transport.Create(args.Gpu)
                        ?? throw new InvalidOperationException("Viewport factory returned null."), receivers, events, telemetry));
            });
            Register<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)>(services, resolver =>
            {
                var events = resolver.Resolve<IEventsSource<GameReplacement>>();
                var telemetry = resolver.Resolve<IDiagnosticTelemetry>();
                return new Factory<GameReplacement, (SessionSupervisor Supervisor, FrameSession Frames)>(args =>
                    new(args.Supervisor, args.Frames, snapshot.ReplacementDeadlines, events, telemetry));
            });
            Register<RuntimeEngine>(services, resolver => new RuntimeEngineFactory(
                resolver.Resolve<IFactory<SessionSupervisor>>(), resolver.Resolve<IFactory<GpuResourceHost>>(),
                resolver.Resolve<IFactory<FrameSession, (SessionSupervisor, GpuResourceHost)>>(),
                resolver.Resolve<IFactory<GameReplacement, (SessionSupervisor, FrameSession)>>()));
            services.Singleton<RuntimeEngine>(resolver => resolver.Resolve<IFactory<RuntimeEngine>>().Create()!)
                .Singleton<RuntimeAppLifetime>(resolver => new(resolver.Resolve<RuntimeEngine>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<RuntimeAppLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<RuntimeAppLifetime>());
            if (diagnostics && !services.Exist<IProcessDiagnosticsSource>())
                services.Singleton<IProcessDiagnosticsSource>(resolver => new RuntimeProcessDiagnostics(resolver.Resolve<IRuntimeFactory>()));
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
