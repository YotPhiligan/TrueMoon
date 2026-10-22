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

namespace TrueMoon.Aether;

/// <summary>Registers the selected game's runtime graph in the child App.</summary>
public static class RuntimeConfigurationExtensions
{
    /// <summary>Registers caller-independent runtime factories and one App-owned runtime host.</summary>
    /// <param name="context">App configuration with UseAether.</param><param name="launch">Validated child launch.</param>
    /// <param name="parentId">Owned parent process id.</param><returns>The same configuration.</returns>
    public static IAppConfigurationContext UseAetherRuntime(this IAppConfigurationContext context, SessionLaunch launch, int parentId)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(launch);
        launch.Identity.Validate(); ArgumentNullException.ThrowIfNull(launch.Build);
        if (parentId <= 0 || parentId == Environment.ProcessId) throw new ArgumentOutOfRangeException(nameof(parentId));
        if (launch.Build.BuildId != launch.Identity.GameBuildId || string.IsNullOrWhiteSpace(launch.Endpoint) ||
            !launch.Endpoint.StartsWith("engine-", StringComparison.Ordinal) || launch.Endpoint.Length > 100)
            throw new ArgumentException("Runtime launch build/endpoint mismatch.", nameof(launch));
        ArgumentException.ThrowIfNullOrWhiteSpace(launch.ReportDirectory);
        launch = launch with { ReportDirectory = Path.GetFullPath(launch.ReportDirectory) };
        context.Services(services =>
        {
            if (!services.Exist<AetherHost>()) throw new InvalidOperationException("Call UseAether before UseAetherRuntime.");
            if (services.Exist<AetherRuntimeHost>()) throw new InvalidOperationException("The runtime is already registered.");
            Register<FrameProducer>(services, _ => new Factory<FrameProducer>(() => new()));
            Register<UnitParentProcessEventsHandler>(services, resolver => new Factory<UnitParentProcessEventsHandler>(() =>
                    new(resolver.Resolve<IConfiguration>(), resolver.Resolve<IAppLifetime>(), new AetherEvents<UnitParentProcessEventsHandler>())));
            Register<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)>(services, _ =>
                    new Factory<RuntimeProtocol, (SessionLaunch Launch, IGameSession Game, FrameProducer Frames)>(args =>
                        new(args.Launch.Identity, args.Game, Environment.ProcessId, args.Frames.Configure, () => args.Frames.Dropped, args.Launch.RequireRestore)));
            Register<PipesInvocationServer<IEngineControl>, PipesInvocationServerArguments<IEngineControl>>(services, _ =>
                    new PipesInvocationServerFactory<IEngineControl>(new AetherEvents<PipesInvocationServer<IEngineControl>>()));
            Register<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)>(services, resolver =>
                {
                    var pipes = resolver.Resolve<IFactory<PipesInvocationServer<IEngineControl>, PipesInvocationServerArguments<IEngineControl>>>();
                    return new Factory<PipesInvocationServer<IEngineControl>, (SessionLaunch Launch, RuntimeProtocol Protocol)>(args =>
                        pipes.Create(new(args.Launch.Endpoint, new BoundedControlHandler(new EngineControlInvocationServerHandler(
                            args.Protocol, new AetherEvents<EngineControlInvocationServerHandler>())))));
                });
            Register<AetherRuntimeHost, SessionLaunch>(services, resolver => new RuntimeHostFactory(
                    resolver.Resolve<AetherHost>(), resolver.Resolve<IAppLifetime>(), parentId,
                    resolver.Resolve<IFactory<UnitParentProcessEventsHandler>>(), resolver.Resolve<IFactory<GameBuildLoader, GameBuildReference>>(),
                    resolver.Resolve<IFactory<FrameProducer>>(),
                    resolver.Resolve<IFactory<RuntimeProtocol, (SessionLaunch, IGameSession, FrameProducer)>>(),
                    resolver.Resolve<IFactory<PipesInvocationServer<IEngineControl>, (SessionLaunch, RuntimeProtocol)>>()));
            services.Singleton<AetherRuntimeHost>(resolver => resolver.Resolve<IFactory<AetherRuntimeHost, SessionLaunch>>().Create(launch));
        });
        return context;
    }

    private static void Register<T>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T>> factory)
    { if (!services.Exist<IFactory<T>>()) services.Singleton(factory); }
    private static void Register<T, TArguments>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T, TArguments>> factory)
    { if (!services.Exist<IFactory<T, TArguments>>()) services.Singleton(factory); }
}
