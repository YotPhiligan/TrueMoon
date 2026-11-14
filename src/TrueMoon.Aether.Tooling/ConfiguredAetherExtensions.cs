using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Aether.Tooling.Scenes.Authoring;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Tooling;

/// <summary>Connects prepared settings to existing factories, validation and ownership.</summary>
public static class ConfiguredAetherExtensions
{
    /// <summary>Registers engine data with explicit identity and borrowed composition.</summary>
    /// <param name="context">App context.</param><param name="composition">Identity and factories.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAether(this IAppConfigurationContext context, AetherComposition composition, Action<AetherSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var frozen = composition with { };
        return ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Engine, (app, data) => app.UseAether(options =>
        { frozen.ApplyTo(options); data.ApplyTo(options); }), configure);
    }
    /// <summary>Registers prepared Authoring limits; products keep existing caller ownership.</summary>
    /// <param name="context">App context.</param><param name="configure">Pure data overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherAuthoring(this IAppConfigurationContext context, Action<SceneAuthoringOptions>? configure = null)
        => ConfigurationModuleBinding.Configure(context, AetherConfigurationSchemas.Authoring, (app, value) => app.UseAetherAuthoring(options =>
        {
            options.MaxHistoryEntries = value.MaxHistoryEntries;
            options.MaxBatchEdits = value.MaxBatchEdits;
            options.MaxOperations = value.MaxOperations;
        }), configure);
}
