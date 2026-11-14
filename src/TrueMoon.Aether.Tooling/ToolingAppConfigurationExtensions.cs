using TrueMoon.Aether.Tooling.Builds;
using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Projects;
using TrueMoon.Aether.Tooling.SourceTracking;
using TrueMoon.Services;
using TrueMoon.Diagnostics;

namespace TrueMoon.Aether.Tooling;

/// <summary>Public App integration for headless project/document authoring and explicit builds.</summary>
public static class ToolingAppConfigurationExtensions
{
    /// <summary>Registers one Tooling owner, project/document/source factories and compiler without runtime or UI activation.</summary>
    /// <param name="context">App composition context.</param><param name="configure">Explicit target identity and startup settings, copied before registration.</param>
    /// <returns>The same context.</returns>
    /// <remarks>Register once in a fresh Tooling graph. Existing persistence, preflight, authoring and product-factory overrides
    /// are preserved. Registration and empty startup perform no project IO, SDK probe, compiler or native startup.</remarks>
    public static IAppConfigurationContext UseAetherTooling(this IAppConfigurationContext context, Action<ToolingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(configure);
        var options = new ToolingOptions(); configure(options); var snapshot = options.Snapshot();
        context.Services(services =>
        {
            if (services.Exist<ToolingHost>() || services.Exist<EditorProjects>() || services.Exist<ToolingAppLifetime>() || services.Exist<AetherHost>())
                throw new InvalidOperationException("Aether Tooling requires one headless or full workflow registration per App.");
            AppConfigurationExtensions.RegisterSceneResolution(services);
            if (!services.Exist<IFactory<Scenes.Authoring.ISceneAuthoringSession, Abstractions.Scenes.SceneDocument>>())
                services.Singleton<IFactory<Scenes.Authoring.ISceneAuthoringSession, Abstractions.Scenes.SceneDocument>>(resolver =>
                    new Scenes.Authoring.SceneAuthoringSessionFactory(resolver.Resolve<Runtime.Scenes.SceneResolutionService>(), snapshot.Authoring));
        });
        context.UseAetherEditorDocuments();
        context.Services(services =>
        {
            if (!services.Exist<Diagnostics.EditorDiagnosticReportWriter>())
                services.Singleton<Diagnostics.EditorDiagnosticReportWriter>(_ => new());
            if (!services.Exist<IFactory<EditorSourceWatch, ResolvedProject>>())
                services.Singleton<IFactory<EditorSourceWatch, ResolvedProject>>(resolver =>
                    new EditorSourceWatchFactory(snapshot.SourceTracking, resolver.TryResolve<TimeProvider>() ?? TimeProvider.System));
            if (!services.Exist<IFactory<GameBuildCompiler>>())
                services.Singleton<IFactory<GameBuildCompiler>>(resolver => new Factory<GameBuildCompiler>(() =>
                    new(snapshot.HostContractId, snapshot.BuildDirectory, snapshot.BuildTimeout, resolver.TryResolve<IEventsSource<GameBuildCompiler>>(),
                        snapshot.BuildRetention, resolver.TryResolve<TrueMoon.Diagnostics.IDiagnosticTelemetry>())));
        });
        context.UseAetherEditorProjects();
        context.Services(services => services
            .Singleton<ToolingHost>(resolver => new(resolver.Resolve<EditorProjects>(), resolver.Resolve<IFactory<GameBuildCompiler>>().Create()
                ?? throw new InvalidOperationException("The Tooling compiler factory returned null."), snapshot.RuntimeAccess))
            .Singleton<ToolingAppLifetime>(resolver => new(resolver.Resolve<ToolingHost>()))
            .Singleton<IStartable>(resolver => resolver.Resolve<ToolingAppLifetime>())
            .Singleton<IStoppable>(resolver => resolver.Resolve<ToolingAppLifetime>()));
        return context;
    }
}
