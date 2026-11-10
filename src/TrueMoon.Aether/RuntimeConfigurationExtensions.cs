using TrueMoon.Aether.Builds;
using TrueMoon.Aether.Frames;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Sessions;
using TrueMoon.Aether.Transport;
using TrueMoon.Configuration;
using TrueMoon.Enerit.IO.Pipes;
using TrueMoon.Generated.TrueMoon.Aether;
using TrueMoon.Mithril;
using TrueMoon.Services;
using TrueMoon.Aether.Preview;
using TrueMoon.Diagnostics;
using TrueMoon.Aether.Diagnostics;

namespace TrueMoon.Aether;

/// <summary>Registers the selected game's runtime graph in the child App.</summary>
public static class RuntimeConfigurationExtensions
{
    /// <summary>Registers a selected game from a frozen protected bootstrap and captured public flags.</summary>
    /// <param name="context">App context.</param><param name="bootstrap">Validated pre-App capture.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherRuntime(this IAppConfigurationContext context, RuntimeBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap); ArgumentNullException.ThrowIfNull(bootstrap.Launch.Build);
        if (bootstrap.Launch.Role != RuntimeRole.Game) throw new ArgumentException("Selected games require the game role.", nameof(bootstrap));
        return RegisterRuntime(context, bootstrap.Launch, bootstrap.ParentProcessId, bootstrap.Settings,
            bootstrap.ParentLaunch ?? new UnitLaunchSettings(ParentProcessId: bootstrap.ParentProcessId));
    }

    /// <summary>Registers built-in preview from a frozen protected bootstrap.</summary>
    /// <param name="context">App context.</param><param name="bootstrap">Validated pre-App capture.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherPreviewRuntime(this IAppConfigurationContext context, RuntimeBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        var launch = bootstrap.Launch;
        if (launch.Role != RuntimeRole.EditPreview || launch.Build != null || launch.RequireRestore || launch.Identity.GameBuildId != BuildIdentity.PreviewId)
            throw new ArgumentException("Preview requires the built-in identity without a build or restore.", nameof(bootstrap));
        return RegisterRuntime(context, launch, bootstrap.ParentProcessId, bootstrap.Settings,
            bootstrap.ParentLaunch ?? new UnitLaunchSettings(ParentProcessId: bootstrap.ParentProcessId));
    }

    /// <summary>Registers caller-independent runtime factories and one App-owned runtime host.</summary>
    /// <param name="context">App configuration with UseAether.</param><param name="launch">Validated child launch.</param>
    /// <param name="parentId">Owned parent process id.</param><returns>The same configuration.</returns>
    /// <exception cref="InvalidDataException">The selected game artifact directory is missing, relative or malformed.</exception>
    public static IAppConfigurationContext UseAetherRuntime(this IAppConfigurationContext context, SessionLaunch launch, int parentId)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(launch.Build);
        if (launch.Role != RuntimeRole.Game) throw new ArgumentException("Selected games require the game runtime role.", nameof(launch));
        return RegisterRuntime(context, launch, parentId);
    }

    /// <summary>Registers the built-in static preview child without a game artifact, game loader activation or compiler.</summary>
    /// <param name="context">Child App configuration with UseAether.</param>
    /// <param name="launch">Explicit EditPreview role with matching PreviewId and no build/restore.</param>
    /// <param name="parentId">Owned parent process ID.</param><returns>The same configuration.</returns>
    public static IAppConfigurationContext UseAetherPreviewRuntime(this IAppConfigurationContext context, SessionLaunch launch, int parentId)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(launch);
        if (launch.Role != RuntimeRole.EditPreview || launch.Build != null || launch.RequireRestore || launch.Identity.GameBuildId != BuildIdentity.PreviewId)
            throw new ArgumentException("Preview requires the built-in role/identity and no game artifact or restore.", nameof(launch));
        return RegisterRuntime(context, launch, parentId);
    }

    private static IAppConfigurationContext RegisterRuntime(IAppConfigurationContext context, SessionLaunch launch, int parentId,
        RuntimeStartupSettings? settings = null, UnitLaunchSettings? parentLaunch = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        launch.Identity.Validate();
        if (launch.Build is { } build)
        {
            if (string.IsNullOrWhiteSpace(build.DirectoryPath) || !Path.IsPathFullyQualified(build.DirectoryPath) || build.DirectoryPath.Contains('\0'))
                throw new InvalidDataException("Runtime game artifact directory must be an absolute path without NUL.");
            try { launch = launch with { Build = build with { DirectoryPath = Path.GetFullPath(build.DirectoryPath) } }; }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
            {
                throw new InvalidDataException("Invalid runtime game artifact directory.", error);
            }
        }
        if (launch.RuntimeIdentity != null) BuildIdentity.ValidateRuntime(launch);
        launch = launch with { DiagnosticDelivery = launch.DiagnosticDelivery?.Snapshot() };
        if (parentId <= 0 || parentId == Environment.ProcessId) throw new ArgumentOutOfRangeException(nameof(parentId));
        if (launch.Build != null && launch.Build.BuildId != launch.Identity.GameBuildId || string.IsNullOrWhiteSpace(launch.Endpoint) ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new ArgumentException("Runtime launch build/endpoint mismatch.", nameof(launch));
        ArgumentException.ThrowIfNullOrWhiteSpace(launch.ReportDirectory);
        launch = launch with { ReportDirectory = Path.GetFullPath(launch.ReportDirectory) };
        if (settings is null || parentLaunch is null)
        {
            var captured = ConfigurationEnvironment.Capture(RuntimeStartupSettings.LegacyValidationKey, RuntimeStartupSettings.ValidationKey,
                UnitParentProcessEventsHandler.ControlPipeKey);
            settings ??= RuntimeStartupSettings.Parse(captured);
            parentLaunch ??= new UnitLaunchSettings(ParentProcessId: parentId,
                ControlPipe: captured.GetValueOrDefault(UnitParentProcessEventsHandler.ControlPipeKey));
        }
        if (parentLaunch.ParentProcessId != parentId) throw new ArgumentException("Bootstrap parent identities differ.", nameof(parentLaunch));
        var frozenSettings = settings with { };
        var frozenParent = parentLaunch;
        context.Services(services =>
        {
            if (!services.Exist<AetherHost>()) throw new InvalidOperationException("Call UseAether before UseAetherRuntime.");
            if (services.Exist<AetherRuntimeHost>()) throw new InvalidOperationException("The runtime is already registered.");
            services.Instance(frozenSettings);
            PrimitiveEcsConfigurationExtensions.RegisterDefaults(services);
            if (!services.Exist<IFactory<Ecs.World>>())
                services.Singleton<IFactory<Ecs.World>>(_ => new Factory<Ecs.World>(() => new()));
            if (!services.Exist<IFactory<Ecs.Loading.SceneWorldLoader, Ecs.Loading.ComponentRegistry>>())
                services.Singleton<IFactory<Ecs.Loading.SceneWorldLoader, Ecs.Loading.ComponentRegistry>>(resolver =>
                    new Factory<Ecs.Loading.SceneWorldLoader, Ecs.Loading.ComponentRegistry>(registry =>
                        new(registry, resolver.Resolve<IFactory<Ecs.World>>(), resolver.Resolve<Scenes.SceneResolutionService>())));
            if (launch.Role == RuntimeRole.EditPreview)
                Register<PreviewGameSession>(services, _ => new Factory<PreviewGameSession>(() => new()));
            Register<FrameProducer>(services, _ => new Factory<FrameProducer>(() => new()));
            Register<UnitParentProcessEventsHandler>(services, resolver => new Factory<UnitParentProcessEventsHandler>(() =>
                    new(frozenParent, resolver.Resolve<IAppLifetime>(), resolver.Resolve<IEventsSource<UnitParentProcessEventsHandler>>())));
            Register<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)>(services, resolver =>
                    new Factory<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)>(args =>
                        new(args.Launch.Identity, args.Game, Environment.ProcessId, args.Frames.Configure, () => args.Frames.Dropped,
                            args.Launch.RequireRestore, args.Launch.Role,
                            resolver.TryResolve<Ecs.Loading.SceneWorldCodec>() ?? EcsSceneLoadingConfigurationExtensions.CreateNeutralCodec(resolver))));
            Register<PipesInvocationServer<IEngineControl>, PipesInvocationServerArguments<IEngineControl>>(services, resolver =>
                    new PipesInvocationServerFactory<IEngineControl>(resolver.Resolve<IEventsSource<PipesInvocationServer<IEngineControl>>>(),
                        new PipesTransportOptions { PropagateTraceContext = true }, resolver.Resolve<IDiagnosticTelemetry>()));
            Register<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)>(services, resolver =>
                {
                    var pipes = resolver.Resolve<IFactory<PipesInvocationServer<IEngineControl>, PipesInvocationServerArguments<IEngineControl>>>();
                    return new Factory<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)>(args =>
                        pipes.Create(new(args.Launch.Endpoint, new BoundedControlHandler(new EngineControlInvocationServerHandler(
                            args.Protocol, resolver.Resolve<IEventsSource<EngineControlInvocationServerHandler>>())))));
                });
            Register<AetherRuntimeHost, SessionLaunch>(services, resolver => new RuntimeHostFactory(
                    resolver.Resolve<AetherHost>(), resolver.Resolve<IAppLifetime>(), parentId,
                    resolver.Resolve<IFactory<UnitParentProcessEventsHandler>>(), resolver.Resolve<IFactory<GameBuildLoader, GameBuildReference>>(),
                    resolver.Resolve<IFactory<FrameProducer>>(),
                    resolver.Resolve<IFactory<RuntimeProtocol, (SessionLaunch, IGameSession, FrameProducer)>>(),
                    resolver.Resolve<IFactory<PipesInvocationServer<IEngineControl>, (SessionLaunch, RuntimeProtocol)>>(),
                    launch.Role == RuntimeRole.EditPreview ? resolver.Resolve<IFactory<PreviewGameSession>>() : null,
                    resolver.Resolve<IFactory<ChildDiagnosticForwarder, SessionLaunch>>(), frozenSettings));
            services.Singleton<AetherRuntimeHost>(resolver => resolver.Resolve<IFactory<AetherRuntimeHost, SessionLaunch>>().Create(launch));
        });
        return context;
    }

    private static void Register<T>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T>> factory)
    { if (!services.Exist<IFactory<T>>()) services.Singleton(factory); }
    private static void Register<T, TArguments>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T, TArguments>> factory)
    { if (!services.Exist<IFactory<T, TArguments>>()) services.Singleton(factory); }
}
