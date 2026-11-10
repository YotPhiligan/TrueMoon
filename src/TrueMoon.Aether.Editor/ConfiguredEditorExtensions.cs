using TrueMoon.Aether.Editor.Diagnostics;
using TrueMoon.Aether.Hosting;
using TrueMoon.Aether.Scenes.Authoring;
using TrueMoon.Configuration;
using TrueMoon.Alloy.Hosting;
using TrueMoon.Alloy.Rendering.Skia;

namespace TrueMoon.Aether.Editor;

/// <summary>Connects prepared Editor/engine data to the existing reusable editor ownership graph.</summary>
public static class ConfiguredEditorExtensions
{
    /// <summary>Registers the standard Editor window from prepared scalars and an explicit borrowed font catalog.</summary>
    /// <param name="context">App context after configured Editor.</param><param name="fonts">Borrowed font catalog.</param>
    /// <param name="configure">Pure scalar overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEditorWindow(this IAppConfigurationContext context,
        SkiaFontCatalog fonts, Action<WindowSettings>? configure = null)
        => ConfigurationModuleBinding.Configure(context, EditorConfigurationSchemas.Window, (app, value) => app.UseAetherEditorWindow(fonts, value.ApplyTo), configure);
    /// <summary>Registers the four standard Editor, diagnostics, engine and authoring schemas together.</summary>
    /// <param name="context">App context.</param><param name="composition">Explicit immutable engine identity/factories.</param>
    /// <param name="configureEditor">Pure Editor data overrides.</param><param name="configureEngine">Pure engine data overrides.</param>
    /// <param name="configureAuthoring">Pure authoring limits.</param><param name="configureDiagnostics">Pure presentation budgets.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEditor(this IAppConfigurationContext context, AetherComposition composition,
        Action<AetherEditorOptions>? configureEditor = null, Action<AetherSettings>? configureEngine = null,
        Action<SceneAuthoringOptions>? configureAuthoring = null, Action<EditorDiagnosticSettings>? configureDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var frozen = composition with { };
        AetherSettings? engine = null; SceneAuthoringOptions? authoring = null;
        ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Engine, (_, value) => engine = value, configureEngine);
        ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Authoring, (_, value) => authoring = value, configureAuthoring);
        context.UseConfiguredAetherEditorDiagnostics(configureDiagnostics);
        return ConfigurationModuleBinding.Configure(context, EditorConfigurationSchemas.Options, (app, value) => app.UseAetherEditor(options =>
        {
            frozen.ApplyTo(options); engine!.ApplyTo(options);
        }, options =>
        {
            options.ProjectPath = value.ProjectPath; options.SourceTracking = value.SourceTracking.Snapshot();
        }, options =>
        {
            options.MaxHistoryEntries = authoring!.MaxHistoryEntries; options.MaxBatchEdits = authoring.MaxBatchEdits; options.MaxOperations = authoring.MaxOperations;
        }), configureEditor);
    }
    /// <summary>Registers prepared console presentation before Editor's idempotent diagnostic defaults.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure presentation overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEditorDiagnostics(this IAppConfigurationContext context, Action<EditorDiagnosticSettings>? configure = null)
        => ConfigurationModuleBinding.Configure(context, EditorConfigurationSchemas.Diagnostics, (app, value) => app.UseAetherEditorDiagnostics(value.ToOptions()), configure);
    /// <summary>Registers an explicit command from prepared data without launching an editor process.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure command overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherEditorSourceNavigation(this IAppConfigurationContext context, Action<SourceEditorOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, EditorConfigurationSchemas.SourceEditor, (app, value) => app.UseAetherEditorSourceNavigation(value), configure);
}
