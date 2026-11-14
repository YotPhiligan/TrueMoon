using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Diagnostics;
using TrueMoon.Aether.Runtime.Frames;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Diagnostics;
using TrueMoon.Aether.Tooling.Editing;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Preview;
using TrueMoon.Diagnostics;
using TrueMoon.Services;

namespace TrueMoon.Aether.Tooling;

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
            if (services.Exist<ToolingHost>()) throw new InvalidOperationException("Headless Tooling and the full Aether workflow are mutually exclusive per App.");
        });
        RuntimeAppConfigurationExtensions.RegisterRuntimeCore(context, snapshot, false);
        context.Services(services =>
        {
            Register<GameBuildCompiler>(services, resolver =>
            {
                var events = resolver.Resolve<IEventsSource<GameBuildCompiler>>();
                var telemetry = resolver.Resolve<IDiagnosticTelemetry>();
                return new Factory<GameBuildCompiler>(() => new(snapshot.HostContractId,
                    snapshot.BuildDirectory!, snapshot.BuildTimeout, events, snapshot.BuildRetention, telemetry));
            });
            Register<ScenePreview, FrameSession>(services, resolver =>
            {
                var events = resolver.Resolve<IEventsSource<ScenePreview>>();
                var telemetry = resolver.Resolve<IDiagnosticTelemetry>();
                return new Factory<ScenePreview, FrameSession>(frames => new(frames, events, telemetry));
            });
            Register<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)>(services, _ =>
                new Factory<GameEditor, (SessionSupervisor Supervisor, FrameSession Frames, GameBuildCompiler Builds, GameReplacement Replacement)>(args =>
                    new(args.Supervisor, args.Frames, args.Builds, args.Replacement, snapshot.HostContractId)));
            Register<AetherHost>(services, resolver => new AetherHostFactory(
                new Factory<RuntimeEngine>(() => resolver.Resolve<RuntimeEngine>()), resolver.Resolve<IFactory<GameBuildCompiler>>(),
                resolver.Resolve<IFactory<GameEditor, (SessionSupervisor, FrameSession, GameBuildCompiler, GameReplacement)>>(),
                resolver.Resolve<IFactory<ScenePreview, FrameSession>>()));
            services.Singleton<AetherHost>(resolver => resolver.Resolve<IFactory<AetherHost>>().Create()!)
                .Singleton<AetherAppLifetime>(resolver => new AetherAppLifetime(resolver.Resolve<AetherHost>()))
                .Singleton<IStartable>(resolver => resolver.Resolve<AetherAppLifetime>())
                .Singleton<IStoppable>(resolver => resolver.Resolve<AetherAppLifetime>());
            if (!services.Exist<IProcessDiagnosticsSource>())
                services.Singleton<IProcessDiagnosticsSource>(resolver => new AetherProcessDiagnostics(
                    resolver.Resolve<IRuntimeFactory>(), resolver.Resolve<AetherHost>().Builds));
        });
        return context;
    }

    internal static void RegisterSceneResolution(IServicesRegistrationContext services)
        => RuntimeAppConfigurationExtensions.RegisterSceneResolution(services);

    private static void Register<T>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T>> factory)
    { if (!services.Exist<IFactory<T>>()) services.Singleton(factory); }
    private static void Register<T, TArguments>(IServicesRegistrationContext services, Func<IServiceResolver, IFactory<T, TArguments>> factory)
    { if (!services.Exist<IFactory<T, TArguments>>()) services.Singleton(factory); }
}
