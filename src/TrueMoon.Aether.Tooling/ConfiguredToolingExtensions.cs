using TrueMoon.Aether.Tooling.Hosting;
using TrueMoon.Configuration;

namespace TrueMoon.Aether.Tooling;

/// <summary>Binds common App configuration to explicit headless Tooling composition.</summary>
public static class ConfiguredToolingExtensions
{
    /// <summary>Uses the exact Tooling schema in the host's App profile, preserving protected target identity.</summary>
    /// <param name="context">App context.</param><param name="composition">Explicit target host identity.</param>
    /// <param name="configure">Optional final typed overrides.</param><returns>The same context.</returns>
    public static IAppConfigurationContext UseConfiguredAetherTooling(this IAppConfigurationContext context,
        ToolingComposition composition, Action<ToolingSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(composition); var identity = composition.HostContractId;
        return ConfigurationModuleBinding.Configure(context, ToolingConfigurationSchemas.Settings, (app, data) => app.UseAetherTooling(value =>
        {
            value.HostContractId = identity; value.RuntimeAccess = composition.RuntimeAccess; value.BuildDirectory = data.BuildDirectory; value.BuildTimeout = data.BuildTimeout;
            value.BuildRetention = data.BuildRetention; value.Authoring = data.Authoring; value.SourceTracking = data.SourceTracking;
        }), configure);
    }
}
