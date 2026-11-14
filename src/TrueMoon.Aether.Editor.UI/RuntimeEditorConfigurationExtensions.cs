using TrueMoon.Aether.Runtime;
using TrueMoon.Aether.Runtime.Hosting;
using TrueMoon.Aether.Runtime.Scenes;
using TrueMoon.Argentis;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Editor.UI;

/// <summary>Registers Editor UI against a prepared runtime deployment, with no Tooling or compiler dependency.</summary>
public static class RuntimeEditorConfigurationExtensions
{
    /// <summary>Registers one runtime/editor session and parent-owned View factory. Startup does not load or start the game.</summary>
    /// <param name="context">Fresh application graph.</param><param name="start">Captured artifact and scene, outside configuration providers.</param>
    /// <param name="configureRuntime">Explicit Runtime identity/factories and checked startup options. Null viewport selects the standard GPU window binding.</param>
    /// <returns>The same context.</returns>
    public static IAppConfigurationContext UseAetherEditorUi(this IAppConfigurationContext context, PlayerStart start, Action<RuntimeOptions> configureRuntime)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(start); ArgumentNullException.ThrowIfNull(configureRuntime);
        var defaultViewport = true;
        context.Services(services =>
        {
            if (services.Exist<RuntimeEngine>() || services.Exist<RuntimeEditorSession>() || services.Exist<IFactory<EditorView>>())
                throw new InvalidOperationException("Runtime Editor UI requires one fresh runtime/editor graph.");
            services.Singleton<RuntimeViewportBinding>(_ => new(defaultViewport));
        });
        context.UseAetherRuntime(options =>
        {
            configureRuntime(options);
            if (options.HostContractId != start.Artifact.HostContractId || options.GameBuildId != start.Artifact.BuildId)
                throw new ArgumentException("Runtime composition must match the captured deployment.", nameof(configureRuntime));
            defaultViewport = options.ViewportTransport == null && options.ViewportTransportFactory == null;
            if (defaultViewport) options.ViewportTransportFactory = resolver => resolver.Resolve<RuntimeViewportBinding>();
        });
        context.Services(services =>
        {
            services.Singleton<RuntimeEditorSession>(resolver => new(resolver.Resolve<RuntimeEngine>(), start,
                resolver.Resolve<RuntimeViewportBinding>(), resolver.Resolve<IAppLifetime>().AppCancellationToken));
            services.Singleton<IStoppable>(resolver => new AppServiceLifecycle(stoppable: resolver.Resolve<RuntimeEditorSession>()));
            services.Singleton<RuntimeEditorViewModel>(resolver => new(start, resolver.Resolve<RuntimeEditorSession>(), resolver.Resolve<SceneResolutionService>()));
            if (!services.Exist<IViewStyle<EditorView>>()) services.Singleton<IViewStyle<EditorView>>(_ => new EditorViewStyle());
            services.Singleton<IFactory<EditorView>>(resolver =>
            {
                var data = resolver.Resolve<RuntimeEditorViewModel>(); var style = resolver.Resolve<IViewStyle<EditorView>>();
                return new Factory<EditorView>(() => new(data, style));
            });
        });
        return context;
    }

    /// <summary>Binds the standard Runtime schema; protected artifact identity and service factories remain explicit.</summary>
    /// <param name="context">App with exact RuntimeConfigurationSchemas.Engine instance and sources.</param><param name="start">Captured deployment.</param>
    /// <param name="composition">Explicit Runtime identity/factories.</param><param name="configure">Pure startup overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEditorUi(this IAppConfigurationContext context, PlayerStart start,
        RuntimeComposition composition, Action<RuntimeSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var frozen = composition with { };
        return ConfigurationModuleBinding.Configure(context, RuntimeConfigurationSchemas.Engine,
            (app, settings) => app.UseAetherEditorUi(start, options => { frozen.ApplyTo(options); settings.ApplyTo(options); }), configure);
    }
}
